using UnityEngine;

public class YoruVFXManager : MonoBehaviour
{
    [Header("=== MOVEMENT EFFECTS ===")]
    [SerializeField] private GameObject dustPuffPrefab;        
    [SerializeField] private GameObject jumpLaunchPrefab;      
    [SerializeField] private GameObject landingImpactPrefab;   
    [SerializeField] private GameObject runTrailPrefab;        
    
    [Header("=== COMBAT EFFECTS ===")]
    [SerializeField] private GameObject pawAttack1Prefab;      
    [SerializeField] private GameObject pawAttack2Prefab;      
    [SerializeField] private GameObject leftTailMagicPrefab;   
    [SerializeField] private GameObject rightTailMagicPrefab;  
    
    [Header("=== CINEMATIC EFFECTS ===")]
    [SerializeField] private GameObject soulFreeingPrefab;     
    [SerializeField] private GameObject circleActivationPrefab;
    [SerializeField] private GameObject absorbingPrefab;       
    
    [Header("Effect Spawn Points")]
    [SerializeField] private Transform leftPaw;
    [SerializeField] private Transform rightPaw;
    [SerializeField] private Transform leftTail;
    [SerializeField] private Transform rightTail;
    [SerializeField] private Transform centerBody;
    
    [Header("Settings")]
    [SerializeField] private float effectLifetime = 3f;
    [SerializeField] private bool debugMode = false;
    [SerializeField] private bool autoDetectAnimations = true;
    [SerializeField] private bool findTailTips = true;
    
    // Components
    private Animator animator;
    private CharacterController controller;
    
    // Tracking
    private float lastFootstepTime;
    private bool wasGrounded;
    private GameObject activeRunTrail;
    
    // Track if effects have been triggered this animation
    private bool attackPawTriggered;
    private bool towPawTriggered;
    private bool leftTailTriggered;
    private bool rightTailTriggered;
    private bool soulTriggered;
    private bool circleTriggered;
    private bool absorbTriggered;
    private int lastStateHash;
    
    void Start()
    {
        animator = GetComponent<Animator>();
        controller = GetComponent<CharacterController>();
        
        if (!centerBody) centerBody = transform;
        
        // Auto-find tail tips if enabled
        if (findTailTips)
        {
            FindTailTips();
        }
        
        if (debugMode)
        {
            Debug.Log("VFX Manager initialized. Prefabs loaded: " + 
                     (jumpLaunchPrefab != null ? "Jump✓ " : "Jump✗ ") +
                     (landingImpactPrefab != null ? "Land✓ " : "Land✗ ") +
                     (leftTailMagicPrefab != null ? "LTail✓ " : "LTail✗ ") +
                     (rightTailMagicPrefab != null ? "RTail✓ " : "RTail✗ "));
            
            if (leftTail) Debug.Log($"Left Tail: {leftTail.name}");
            if (rightTail) Debug.Log($"Right Tail: {rightTail.name}");
        }
    }
    
    void FindTailTips()
    {
        // Find left tail tip
        if (leftTail == null)
        {
            Transform tail = FindDeepChild(transform, "Tail15_L", "Tail.*L", "L.*Tail");
            if (tail != null)
            {
                leftTail = GetDeepestChild(tail);
                Debug.Log($"Auto-found left tail tip: {leftTail.name}");
            }
        }
        
        // Find right tail tip
        if (rightTail == null)
        {
            Transform tail = FindDeepChild(transform, "Tail15_R", "Tail.*R", "R.*Tail");
            if (tail != null)
            {
                rightTail = GetDeepestChild(tail);
                Debug.Log($"Auto-found right tail tip: {rightTail.name}");
            }
        }
    }
    
    Transform FindDeepChild(Transform parent, params string[] possibleNames)
    {
        foreach (string name in possibleNames)
        {
            Transform[] allChildren = parent.GetComponentsInChildren<Transform>();
            foreach (Transform child in allChildren)
            {
                if (child.name.Contains(name.Replace(".*", "")))
                {
                    return child;
                }
            }
        }
        return null;
    }
    
    Transform GetDeepestChild(Transform parent)
    {
        Transform deepest = parent;
        while (deepest.childCount > 0)
        {
            deepest = deepest.GetChild(deepest.childCount - 1);
        }
        return deepest;
    }
    
    void Update()
    {
        if (!controller || !animator) return;
        
        bool isGrounded = controller.isGrounded;
        float speed = animator.GetFloat("Speed");
        
        // Always check movement effects
        HandleMovementEffects(isGrounded, speed);
        
        // Only auto-detect if enabled (can disable if using Animation Events)
        if (autoDetectAnimations)
        {
            // Check combat animations (Layer 1)
            if (animator.layerCount > 1)
            {
                CheckCombatAnimations();
            }
            
            // Check cinematic animations (Layer 2)
            if (animator.layerCount > 2)
            {
                CheckCinematicAnimations();
            }
        }
        
        wasGrounded = isGrounded;
    }
    
    // ========== MOVEMENT EFFECTS ==========
    void HandleMovementEffects(bool isGrounded, float speed)
    {
        // Footstep dust
        if (isGrounded && speed > 0.1f && dustPuffPrefab != null)
        {
            float interval = speed > 1.5f ? 0.2f : 0.4f;
            
            if (Time.time - lastFootstepTime > interval)
            {
                lastFootstepTime = Time.time;
                SpawnEffect(dustPuffPrefab, transform.position, Quaternion.identity);
            }
        }
        
        // Running trail
        if (runTrailPrefab != null)
        {
            if (speed > 1.5f && isGrounded && activeRunTrail == null)
            {
                activeRunTrail = SpawnEffect(runTrailPrefab, transform.position, Quaternion.identity, true);
                if (activeRunTrail)
                {
                    activeRunTrail.transform.SetParent(transform);
                }
            }
            else if ((speed <= 1.5f || !isGrounded) && activeRunTrail != null)
            {
                var ps = activeRunTrail.GetComponent<ParticleSystem>();
                if (ps) ps.Stop();
                Destroy(activeRunTrail, 2f);
                activeRunTrail = null;
            }
        }
        
        // Landing effect
        if (!wasGrounded && isGrounded && landingImpactPrefab != null)
        {
            SpawnEffect(landingImpactPrefab, transform.position, Quaternion.identity);
            if (debugMode) Debug.Log("Landing VFX spawned!");
        }
    }
    
    // ========== COMBAT ANIMATIONS AUTO-DETECTION ==========
    void CheckCombatAnimations()
    {
        AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(1);
        float layerWeight = animator.GetLayerWeight(1);
        
        // Debug actual state names
        if (debugMode && layerWeight > 0.1f && state.fullPathHash != lastStateHash)
        {
            Debug.Log($"Combat Layer Active - State Hash: {state.fullPathHash}, Time: {state.normalizedTime:F2}");
        }
        
        // Reset triggers when animation changes
        if (state.fullPathHash != lastStateHash)
        {
            attackPawTriggered = false;
            towPawTriggered = false;
            leftTailTriggered = false;
            rightTailTriggered = false;
            lastStateHash = state.fullPathHash;
        }
        
        // Only check if layer is active
        if (layerWeight < 0.1f) return;
        
        // AttackPaw
        if (state.IsName("AttackPaw") && !attackPawTriggered && pawAttack1Prefab != null)
        {
            if (state.normalizedTime > 0.3f && state.normalizedTime < 0.5f)
            {
                TriggerPawAttack(false);
                attackPawTriggered = true;
            }
        }
        
        // TowPaw (Two Paw Attack)
        else if (state.IsName("TowPaw") && !towPawTriggered && pawAttack2Prefab != null)
        {
            if (state.normalizedTime > 0.3f && state.normalizedTime < 0.5f)
            {
                TriggerPawAttack(true);
                towPawTriggered = true;
            }
        }
        
        // LeftTailCast
        else if (state.IsName("LeftTailCast") && !leftTailTriggered && leftTailMagicPrefab != null)
        {
            if (state.normalizedTime > 0.2f && state.normalizedTime < 0.4f)
            {
                TriggerTailCast(true);
                leftTailTriggered = true;
            }
        }
        
        // RightTailCast
        else if (state.IsName("RightTailCast") && !rightTailTriggered && rightTailMagicPrefab != null)
        {
            if (state.normalizedTime > 0.2f && state.normalizedTime < 0.4f)
            {
                TriggerTailCast(false);
                rightTailTriggered = true;
            }
        }
    }
    
    // ========== CINEMATIC ANIMATIONS AUTO-DETECTION ==========
    void CheckCinematicAnimations()
    {
        AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(2);
        float layerWeight = animator.GetLayerWeight(2);
        
        // Debug actual state names
        if (debugMode && layerWeight > 0.1f && state.fullPathHash != lastStateHash)
        {
            Debug.Log($"Cinematic Layer Active - State Hash: {state.fullPathHash}, Time: {state.normalizedTime:F2}");
        }
        
        // Reset triggers when animation changes
        if (state.fullPathHash != lastStateHash)
        {
            soulTriggered = false;
            circleTriggered = false;
            absorbTriggered = false;
            lastStateHash = state.fullPathHash;
        }
        
        // Only check if layer is active
        if (layerWeight < 0.1f) return;
        
        // FreeingSoul
        if (state.IsName("FreeingSoul") && !soulTriggered && soulFreeingPrefab != null)
        {
            if (state.normalizedTime > 0.3f && state.normalizedTime < 0.5f)
            {
                TriggerCinematicEffect("soul");
                soulTriggered = true;
            }
        }
        
        // CircleActivation
        else if (state.IsName("CircleActivation") && !circleTriggered && circleActivationPrefab != null)
        {
            if (state.normalizedTime > 0.2f && state.normalizedTime < 0.4f)
            {
                TriggerCinematicEffect("circle");
                circleTriggered = true;
            }
        }
        
        // Absorbing
        else if (state.IsName("Absorbing") && !absorbTriggered && absorbingPrefab != null)
        {
            if (state.normalizedTime > 0.1f && state.normalizedTime < 0.3f)
            {
                TriggerCinematicEffect("absorb");
                absorbTriggered = true;
            }
        }
    }
    
    // Called by PlayerMovement when jumping
    public void OnJump(int jumpNumber)
    {
        if (jumpLaunchPrefab == null) 
        {
            if (debugMode) Debug.LogWarning("Jump VFX prefab not assigned!");
            return;
        }
        
        Vector3 pos = transform.position;
        GameObject effect = SpawnEffect(jumpLaunchPrefab, pos, Quaternion.identity);
        
        if (effect != null)
        {
            float scale = jumpNumber == 1 ? 1f : jumpNumber == 2 ? 1.5f : 2f;
            effect.transform.localScale = Vector3.one * scale;
        }
        
        if (debugMode) Debug.Log($"Jump {jumpNumber} VFX spawned!");
    }
    
    // ========== SPAWNING SYSTEM ==========
    GameObject SpawnEffect(GameObject prefab, Vector3 position, Quaternion rotation, bool dontDestroy = false)
    {
        if (prefab == null) return null;
        
        GameObject effect = Instantiate(prefab, position, rotation);
        
        ParticleSystem ps = effect.GetComponent<ParticleSystem>();
        if (ps && !ps.isPlaying)
        {
            ps.Play();
        }
        
        if (!dontDestroy)
        {
            Destroy(effect, effectLifetime);
        }
        
        return effect;
    }
    
    // ========== TRIGGER METHODS ==========
    public void TriggerPawAttack(bool isDouble)
    {
        GameObject prefab = isDouble ? pawAttack2Prefab : pawAttack1Prefab;
        Transform spawnPoint = isDouble ? transform : (rightPaw ? rightPaw : transform);
        Vector3 pos = spawnPoint.position + (isDouble ? Vector3.up : Vector3.zero);
        
        if (prefab != null)
        {
            SpawnEffect(prefab, pos, spawnPoint.rotation);
            if (debugMode) Debug.Log($"Paw Attack VFX spawned (Double: {isDouble})");
        }
    }
    
    public void TriggerTailCast(bool isLeft)
    {
        GameObject prefab = isLeft ? leftTailMagicPrefab : rightTailMagicPrefab;
        Transform spawnPoint = isLeft ? leftTail : rightTail;
        if (spawnPoint == null) spawnPoint = transform;
        
        if (prefab != null)
        {
            GameObject effect = SpawnEffect(prefab, spawnPoint.position, Quaternion.identity);
            if (effect && isLeft)
            {
                var ps = effect.GetComponent<ParticleSystem>();
                if (ps)
                {
                    var main = ps.main;
                    main.startColor = new Color(0.3f, 0.6f, 1f);
                }
            }
            if (debugMode) Debug.Log($"{(isLeft ? "Left" : "Right")} Tail VFX spawned at {spawnPoint.name}!");
        }
    }
    
    public void TriggerCinematicEffect(string effectName)
    {
        GameObject prefab = null;
        Vector3 position = transform.position;
        
        switch (effectName)
        {
            case "soul":
                prefab = soulFreeingPrefab;
                position = (centerBody ? centerBody.position : transform.position) + Vector3.up;
                break;
            case "circle":
                prefab = circleActivationPrefab;
                break;
            case "absorb":
                prefab = absorbingPrefab;
                position = centerBody ? centerBody.position : transform.position;
                break;
        }
        
        if (prefab != null)
        {
            SpawnEffect(prefab, position, Quaternion.identity);
            if (debugMode) Debug.Log($"Cinematic VFX '{effectName}' spawned!");
        }
    }
    
    // ========== ANIMATION EVENT METHODS ==========
    // These can be called directly from Animation Events
    public void VFX_AttackPaw()
    {
        TriggerPawAttack(false);
    }
    
    public void VFX_TowPaw()
    {
        TriggerPawAttack(true);
    }
    
    public void VFX_LeftTail()
    {
        TriggerTailCast(true);
    }
    
    public void VFX_RightTail()
    {
        TriggerTailCast(false);
    }
    
    public void VFX_Soul()
    {
        TriggerCinematicEffect("soul");
    }
    
    public void VFX_Circle()
    {
        TriggerCinematicEffect("circle");
    }
    
    public void VFX_Absorb()
    {
        TriggerCinematicEffect("absorb");
    }
}