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
    [SerializeField] private Transform leftTailTip;   // Will auto-find if not assigned
    [SerializeField] private Transform rightTailTip;  // Will auto-find if not assigned
    [SerializeField] private Transform centerBody;
    
    [Header("Settings")]
    [SerializeField] private float effectLifetime = 3f;
    [SerializeField] private bool debugMode = true;  // Enable for testing
    [SerializeField] private bool autoDetectAnimations = false;  // TURN OFF - use Animation Events instead
    [SerializeField] private bool findTailTips = true;
    [SerializeField] private bool parentEffectsToTail = true;  // Makes effects follow tail movement
    [SerializeField] private float tailProjectileSpeed = 10f;  // Speed of tail projectiles
    
    // Components
    private Animator animator;
    private CharacterController controller;
    
    // Tracking
    private float lastFootstepTime;
    private bool wasGrounded;
    private GameObject activeRunTrail;
    
    private PlayerCombat playerCombat;
    
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
        playerCombat = GetComponent<PlayerCombat>();
        
        if (!centerBody) centerBody = transform;
        
        // Auto-find tail tips if enabled
        if (findTailTips)
        {
            FindTailTips();
        }
        
        if (debugMode)
        {
            Debug.Log("=== VFX MANAGER INITIALIZED ===");
            Debug.Log($"Auto-detect animations: {autoDetectAnimations}");
            Debug.Log($"Parent effects to tail: {parentEffectsToTail}");
            Debug.Log($"Prefabs: Jump={jumpLaunchPrefab != null}, Land={landingImpactPrefab != null}, LTail={leftTailMagicPrefab != null}, RTail={rightTailMagicPrefab != null}");
            
            if (leftTailTip) Debug.Log($"✅ Left Tail Tip: {leftTailTip.name}");
            else Debug.LogWarning("❌ Left Tail Tip not found!");
            
            if (rightTailTip) Debug.Log($"✅ Right Tail Tip: {rightTailTip.name}");
            else Debug.LogWarning("❌ Right Tail Tip not found!");
        }
    }
    
    void FindTailTips()
    {
        // Find left tail tip - look for Tail159_L (the last bone)
        if (leftTailTip == null)
        {
            Transform[] allChildren = GetComponentsInChildren<Transform>();
            foreach (Transform child in allChildren)
            {
                if (child.name == "Tail159_L")
                {
                    leftTailTip = child;
                    Debug.Log($"✅ Auto-found left tail tip: {leftTailTip.name}");
                    break;
                }
            }
            
            if (leftTailTip == null)
            {
                Debug.LogWarning("⚠️ Could not find Tail159_L - searching for any Tail.*_L...");
                foreach (Transform child in allChildren)
                {
                    if (child.name.Contains("Tail") && child.name.Contains("_L"))
                    {
                        leftTailTip = child;
                        Debug.Log($"Found alternative left tail: {leftTailTip.name}");
                        break;
                    }
                }
            }
        }
        
        // Find right tail tip - look for Tail6_R_end_end_end (the last bone)
        if (rightTailTip == null)
        {
            Transform[] allChildren = GetComponentsInChildren<Transform>();
            foreach (Transform child in allChildren)
            {
                if (child.name == "Tail6_R_end_end_end")
                {
                    rightTailTip = child;
                    Debug.Log($"✅ Auto-found right tail tip: {rightTailTip.name}");
                    break;
                }
            }
            
            if (rightTailTip == null)
            {
                Debug.LogWarning("⚠️ Could not find Tail6_R_end_end_end - searching for any Tail.*_R_end...");
                foreach (Transform child in allChildren)
                {
                    if (child.name.Contains("Tail") && child.name.Contains("_R") && child.name.Contains("_end"))
                    {
                        rightTailTip = child;
                        Debug.Log($"Found alternative right tail: {rightTailTip.name}");
                        break;
                    }
                }
            }
        }
    }
    
    void Update()
    {
        if (!controller || !animator) return;
        
        bool isGrounded = controller.isGrounded;
        float speed = animator.GetFloat("Speed");
        
        // Always check movement effects
        HandleMovementEffects(isGrounded, speed);
        
        // Only auto-detect if enabled (should be OFF if using Animation Events)
        if (autoDetectAnimations)
        {
            if (animator.layerCount > 1)
            {
                CheckCombatAnimations();
            }
            
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
        
        // Landing effect — skip during dodge (CharacterController.Move causes ground flicker)
if (!wasGrounded && isGrounded && landingImpactPrefab != null)
{
    if (playerCombat == null || !playerCombat.IsDodging())
    {
        Vector3 landingPos = transform.position;
        landingPos.y += 0.5f;
        Quaternion rotation = Quaternion.Euler(-90, 0, 0);
        SpawnEffect(landingImpactPrefab, landingPos, rotation);
        if (debugMode) Debug.Log("💨 Landing VFX spawned!");
    }
}
    }
    
    // ========== COMBAT ANIMATIONS AUTO-DETECTION ==========
    void CheckCombatAnimations()
    {
        AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(1);
        float layerWeight = animator.GetLayerWeight(1);
        
        if (state.fullPathHash != lastStateHash)
        {
            attackPawTriggered = false;
            towPawTriggered = false;
            leftTailTriggered = false;
            rightTailTriggered = false;
            lastStateHash = state.fullPathHash;
        }
        
        if (layerWeight < 0.1f) return;
        
        if (state.IsName("AttackPaw") && !attackPawTriggered && pawAttack1Prefab != null)
        {
            if (state.normalizedTime > 0.3f && state.normalizedTime < 0.5f)
            {
                VFX_AttackPaw();
                attackPawTriggered = true;
            }
        }
        else if (state.IsName("towpaw") && !towPawTriggered && pawAttack2Prefab != null)
        {
            if (state.normalizedTime > 0.3f && state.normalizedTime < 0.5f)
            {
                VFX_TowPaw();
                towPawTriggered = true;
            }
        }
        else if (state.IsName("leftTailcast") && !leftTailTriggered && leftTailMagicPrefab != null)
        {
            if (state.normalizedTime > 0.2f && state.normalizedTime < 0.4f)
            {
                VFX_LeftTail();
                leftTailTriggered = true;
            }
        }
        else if (state.IsName("RightTailCast") && !rightTailTriggered && rightTailMagicPrefab != null)
        {
            if (state.normalizedTime > 0.2f && state.normalizedTime < 0.4f)
            {
                VFX_RightTail();
                rightTailTriggered = true;
            }
        }
    }
    
    // ========== CINEMATIC ANIMATIONS AUTO-DETECTION ==========
    void CheckCinematicAnimations()
    {
        AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(2);
        float layerWeight = animator.GetLayerWeight(2);
        
        if (state.fullPathHash != lastStateHash)
        {
            soulTriggered = false;
            circleTriggered = false;
            absorbTriggered = false;
            lastStateHash = state.fullPathHash;
        }
        
        if (layerWeight < 0.1f) return;
        
        if (state.IsName("FreeingSoul") && !soulTriggered && soulFreeingPrefab != null)
        {
            if (state.normalizedTime > 0.3f && state.normalizedTime < 0.5f)
            {
                VFX_Soul();
                soulTriggered = true;
            }
        }
        else if (state.IsName("CircleActivation") && !circleTriggered && circleActivationPrefab != null)
        {
            if (state.normalizedTime > 0.2f && state.normalizedTime < 0.4f)
            {
                VFX_Circle();
                circleTriggered = true;
            }
        }
        else if (state.IsName("Absorbing") && !absorbTriggered && absorbingPrefab != null)
        {
            if (state.normalizedTime > 0.1f && state.normalizedTime < 0.3f)
            {
                VFX_Absorb();
                absorbTriggered = true;
            }
        }
    }
    
    // Called by PlayerMovement when jumping
public void OnJump(int jumpNumber)
{
    if (jumpLaunchPrefab == null) 
    {
        if (debugMode) Debug.LogWarning("⚠️ Jump VFX prefab not assigned!");
        return;
    }
    
    Vector3 pos = transform.position;
    pos.y += 0.5f;
    // Make it vertical (pointing up)
    Quaternion rotation = Quaternion.Euler(-90, 0, 0);  // Try 90 if this points down
    GameObject effect = SpawnEffect(jumpLaunchPrefab, pos, rotation);
    
    if (effect != null)
    {
        float scale = jumpNumber == 1 ? 1f : jumpNumber == 2 ? 1.5f : 2f;
        effect.transform.localScale = Vector3.one * scale;
    }
    
    if (debugMode) Debug.Log($"🐾 Jump {jumpNumber} VFX spawned!");
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
    
    // ========== ANIMATION EVENT METHODS ==========
    // These are called directly from Animation Events at specific frames
    
    public void VFX_AttackPaw()
    {
        if (debugMode) Debug.Log("🐾 VFX_AttackPaw triggered!");
        
        Transform spawnPoint = rightPaw ? rightPaw : transform;
        Vector3 pos = spawnPoint.position;
        
        if (pawAttack1Prefab != null)
        {
            SpawnEffect(pawAttack1Prefab, pos, spawnPoint.rotation);
        }
    }
    
    public void VFX_TowPaw()
    {
        if (debugMode) Debug.Log("🐾🐾 VFX_TowPaw triggered!");
        
        Vector3 pos = transform.position + Vector3.up;
        
        if (pawAttack2Prefab != null)
        {
            SpawnEffect(pawAttack2Prefab, pos, transform.rotation);
        }
    }
    
 public void VFX_LeftTail()
{
    if (debugMode) Debug.Log("✨ VFX_LeftTail triggered!");
    
    if (leftTailMagicPrefab == null)
    {
        Debug.LogWarning("⚠️ Left tail prefab not assigned!");
        return;
    }
    
    Transform spawnPoint = leftTailTip ? leftTailTip : transform;
    
    if (debugMode) Debug.Log($"Spawning projectile at: {spawnPoint.name}, Position: {spawnPoint.position}");
    
    GameObject effect = SpawnEffect(leftTailMagicPrefab, spawnPoint.position, spawnPoint.rotation);
    
    if (effect != null)
    {
        // DON'T parent - make it a projectile!
    Vector3 shootDirection = spawnPoint.forward;
        Rigidbody rb = effect.GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = effect.AddComponent<Rigidbody>();
            rb.useGravity = false;
            rb.linearDamping = 0;
        }
        
        rb.linearVelocity = shootDirection * tailProjectileSpeed;
        
        var ps = effect.GetComponent<ParticleSystem>();
        if (ps)
        {
            var main = ps.main;
            main.startColor = new Color(0.3f, 0.6f, 1f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
        }
        
        if (debugMode) Debug.Log($"✅ Left Tail projectile launched!");
    }
}
    
 public void VFX_RightTail()
{
    if (debugMode) Debug.Log("✨ VFX_RightTail triggered!");
    
    if (rightTailMagicPrefab == null)
    {
        Debug.LogWarning("⚠️ Right tail prefab not assigned!");
        return;
    }
    
    Transform spawnPoint = rightTailTip ? rightTailTip : transform;
    
    if (debugMode) Debug.Log($"Spawning projectile at: {spawnPoint.name}, Position: {spawnPoint.position}");
    
    GameObject effect = SpawnEffect(rightTailMagicPrefab, spawnPoint.position, spawnPoint.rotation);
    
    if (effect != null)
    {
        // DON'T parent - make it a projectile!
        Vector3 shootDirection = spawnPoint.forward;
        
        Rigidbody rb = effect.GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = effect.AddComponent<Rigidbody>();
            rb.useGravity = false;
            rb.linearDamping = 0;
        }
        
        rb.linearVelocity = shootDirection * tailProjectileSpeed;
        
        var ps = effect.GetComponent<ParticleSystem>();
        if (ps)
        {
            var main = ps.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
        }
        
        if (debugMode) Debug.Log($"✅ Right Tail projectile launched!");
    }
}
    
    public void VFX_Soul()
    {
        if (debugMode) Debug.Log("👻 VFX_Soul triggered!");
        
        if (soulFreeingPrefab == null) return;
        
        Vector3 position = (centerBody ? centerBody.position : transform.position) + Vector3.up * 1.5f;
        // CHANGED: Now rotates with character direction
        SpawnEffect(soulFreeingPrefab, position, transform.rotation);
    }
    
    public void VFX_Circle()
    {
        if (debugMode) Debug.Log("⭕ VFX_Circle triggered!");
        
        if (circleActivationPrefab == null) return;
        
        // Spawn at character's position
        Vector3 position = transform.position;
        
        // Position at ground level (character's feet)
       if (controller != null)
{
    position.y = transform.position.y - (controller.height / 2f) + 1.2f;  // Raised from 0.2f to 1.0f
}
        // FIXED: Rotate to make it horizontal (flat on ground)
        // Try Euler(90, 0, 0) first. If upside down, use Euler(-90, 0, 0)
        Quaternion rotation = Quaternion.Euler(90, 0, 0);
        
        if (debugMode) Debug.Log($"⭕ Spawning circle at position: {position} with rotation: {rotation.eulerAngles}");
        
        SpawnEffect(circleActivationPrefab, position, rotation);
    }
    
   public void VFX_Absorb()
{
    if (debugMode) Debug.Log("🌀 VFX_Absorb triggered!");
    
    if (absorbingPrefab == null) return;
    
    Vector3 position = centerBody ? centerBody.position : transform.position;
    position.y += 1.0f;  // Raise it above ground
    
    // CHANGED: Now rotates with character direction
    SpawnEffect(absorbingPrefab, position, transform.rotation);
}
}