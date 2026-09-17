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
    
    [Header("=== COMBO / ATTACK VFX (drag a prefab, same as every other slot) ===")]
    [Tooltip("Spawned at the RIGHT paw when the first combo hit lands.")]
    [SerializeField] private GameObject combo1VFX;
    [Tooltip("Spawned at the LEFT paw when the second combo hit lands.")]
    [SerializeField] private GameObject combo2VFX;
    [Tooltip("Spawned at the body centre when the combo finisher lands.")]
    [SerializeField] private GameObject combo3VFX;
    [Tooltip("Prefab spawned at rightPaw on heavy release. Same prefab+spawn pattern as heavyChargeBuildupPrefab and pawAttack1Prefab.")]
    [SerializeField] private GameObject heavyAttackPrefab;
    [Tooltip("Prefab spawned at leftPaw on charge start, destroyed on release/cancel/hit. Same prefab+spawn pattern as pawAttack1Prefab. (Buildup uses leftPaw; release uses rightPaw, matches the punch animation.)")]
    [SerializeField] private GameObject heavyChargeBuildupPrefab;
    [Tooltip("AIR spin: jump then attack. Born at Center Body and parented to her for the whole spin. At spawn every particle system inside is forced to Local simulation space (nothing is left behind when she lunges), every start delay is zeroed (it starts with the move), and the whole effect is sped up or slowed down so it finishes at Spin Length (it ends with the move). Drop in any prefab, no hand editing needed.")]
    [SerializeField] private GameObject airSpinVFX;
    [Tooltip("GROUND spin, the beyblade finisher: the hazard zone it leaves on the floor. Born at Ground Spin Point and NOT parented to her, so it stays where she spun while she moves on. Plays exactly as authored, no speed or delay change, and lives as long as its own effect is visible. What it does to enemies (tick damage, radius, unlock, cooldown) is set on Player Combat under Beyblade Ground Hazard.")]
    [SerializeField] private GameObject groundSpinVFX;
    [Tooltip("Length of the Combo3 spin clip in seconds. The AIR spin effect is squeezed as one piece so its longest system finishes exactly here. 0.79 is the current clip. Only change it if the animation changes.")]
    [SerializeField] private float spinLength = 0.79f;
    [Tooltip("Seconds taken off every start delay in the ground hazard prefab at spawn, floored at 0. Vefects builds every Area effect as 'warn for 2 seconds, then erupt'; a spin has no warning phase, so 2 makes the eruption fire on the first frame and keeps the rest of the sequence in order. 0 = play the prefab exactly as authored.")]
    [SerializeField] private float groundHazardDelayShift = 2f;
    
    [Header("=== HIT SPARK VFX (spawned at contact point) ===")]
    [SerializeField] private GameObject lightHitSparkPrefab;
    [SerializeField] private GameObject heavyHitSparkPrefab;
    [SerializeField] private float hitSparkLifetime = 1.5f;
    
    [Header("=== DODGE VFX ===")]
    [SerializeField] private GameObject dodgeTrailPrefab;
    [SerializeField] private GameObject dodgeDashTrailPrefab;
    [SerializeField] private float dodgeVFXLifetime = 1.5f;
    
    [Header("=== HIT REACTION VFX ===")]
    [Tooltip("Spawned at the body centre when she takes a light hit.")]
    [SerializeField] private GameObject lightHitReactVFX;
    [Tooltip("Spawned at the body centre when she takes a heavy hit.")]
    [SerializeField] private GameObject heavyHitReactVFX;
    
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
    [Tooltip("Where the GROUND hazard zone is born. Drag GroundVFX (her feet) here. Left empty it falls back to Center Body.")]
    [SerializeField] private Transform groundSpinPoint;
    
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
    private float airborneTimer; // Track how long airborne, prevents landing VFX spam
    
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
        
        // Landing effect, only fires after actually being airborne (not ground flicker)
        if (!isGrounded)
        {
            airborneTimer += Time.deltaTime;
        }

        if (!wasGrounded && isGrounded && landingImpactPrefab != null)
        {
            // Only trigger if actually airborne for > 0.1s (prevents CharacterController ground flicker spam)
            if (airborneTimer > 0.1f)
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
            airborneTimer = 0f;
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

    // ========== COMBAT VFX, Called by PlayerCombat ==========

    /// <summary>Play combo attack VFX for the given combo step (1, 2, or 3).</summary>
    public void PlayComboVFX(int comboStep)
    {
        GameObject prefab = null;
        Transform spawnPoint = null;

        switch (comboStep)
        {
            case 1: prefab = combo1VFX; spawnPoint = rightPaw;   break;
            case 2: prefab = combo2VFX; spawnPoint = leftPaw;    break;
            case 3: prefab = combo3VFX; spawnPoint = centerBody; break;
        }

        if (prefab == null) return;
        if (spawnPoint == null) spawnPoint = transform;

        SpawnEffect(prefab, spawnPoint.position, spawnPoint.rotation);
    }

    /// <summary>Spawn the heavy release VFX prefab at rightPaw. Same spawn-at-paw
    /// pattern as PlayHeavyChargeBuildupVFX, one consistent mechanism for all heavy attack VFX.
    /// Auto-destroyed after effectLifetime seconds (no manual stop needed, it's a one-shot).</summary>
    public void PlayHeavyAttackVFX()
    {
        if (heavyAttackPrefab == null)
        {
            if (debugMode) Debug.LogWarning("🐾⚡ Heavy attack prefab is NULL, assign it in YoruVFXManager Inspector");
            return;
        }

        Transform spawnPoint = rightPaw ? rightPaw : transform;
        GameObject instance = Instantiate(heavyAttackPrefab, spawnPoint.position, spawnPoint.rotation, spawnPoint);
        instance.SetActive(true);

        ParticleSystem ps = instance.GetComponent<ParticleSystem>();
        if (ps == null) ps = instance.GetComponentInChildren<ParticleSystem>();
        if (ps != null) ps.Play();

        Destroy(instance, effectLifetime);

        if (debugMode) Debug.Log($"🐾⚡ Heavy release VFX spawned at {spawnPoint.name}. PS found: {ps != null}");
    }

    // Tracks the currently spawned charge buildup instance so StopHeavyChargeBuildupVFX
    // can tear it down. Null when nothing is charging.
    private GameObject activeChargeBuildupInstance;
    private GameObject activeSpinInstance;
    private float activeSpinTail = 0.5f;    // real seconds the spin effect keeps fading after PlaySpinStop

    /// <summary>Spawn the charge buildup prefab at rightPaw and parent it to the bone
    /// so it follows the paw through the wind-up. Same SpawnEffect/rightPaw pattern as
    /// VFX_AttackPaw, just persistent (not destroyed by effectLifetime).</summary>
    public void PlayHeavyChargeBuildupVFX()
    {
        if (heavyChargeBuildupPrefab == null)
        {
            if (debugMode) Debug.LogWarning("🐾⚡ Heavy charge buildup prefab is NULL, assign it in YoruVFXManager Inspector");
            return;
        }
        if (activeChargeBuildupInstance != null) return; // already active, don't double-spawn

        Transform spawnPoint = leftPaw ? leftPaw : transform;
        activeChargeBuildupInstance = Instantiate(heavyChargeBuildupPrefab, spawnPoint.position, spawnPoint.rotation, spawnPoint);
        activeChargeBuildupInstance.SetActive(true); // in case the prefab was saved disabled

        ParticleSystem ps = activeChargeBuildupInstance.GetComponent<ParticleSystem>();
        if (ps == null) ps = activeChargeBuildupInstance.GetComponentInChildren<ParticleSystem>();
        if (ps != null) ps.Play();

        if (debugMode) Debug.Log($"🐾⚡ Heavy charge buildup spawned at {spawnPoint.name} (pos {spawnPoint.position}). PS found: {ps != null}");
    }

    /// <summary>Stop the buildup and destroy the spawned instance.</summary>
    public void StopHeavyChargeBuildupVFX()
    {
        if (activeChargeBuildupInstance == null) return;

        ParticleSystem ps = activeChargeBuildupInstance.GetComponent<ParticleSystem>();
        if (ps == null) ps = activeChargeBuildupInstance.GetComponentInChildren<ParticleSystem>();
        if (ps != null) ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);

        Destroy(activeChargeBuildupInstance, 0.5f);
        activeChargeBuildupInstance = null;

        if (debugMode) Debug.Log("🐾⚡ Heavy charge buildup stopped");
    }

    /// <summary>Spawn the AIR spin effect and keep it on her until PlaySpinStop. The ground spin
    /// takes the other road, see SpawnGroundHazard. Three corrections are applied to every particle
    /// system in the prefab at spawn,
    /// so any prefab works without hand editing: Local simulation space (follows her through the
    /// lunge), zero start delay (starts with the move), and one shared simulation speed that makes
    /// the longest system finish at Spin Length (ends with the move). Safe to call more than once
    /// per spin: a second call while one is live does nothing.</summary>
    public void PlaySpinStart(bool airborne)
    {
        // The ground spin wears nothing on her body: the beyblade lays a hazard on the floor
        // instead (SpawnGroundHazard, driven by PlayerCombat). Kept as a no-op here so the clip's
        // VFX_SpinStart event can never spawn a stray effect if it ever fires on the ground.
        if (!airborne) return;

        GameObject prefab = airSpinVFX;
        if (prefab == null) return;
        if (activeSpinInstance != null) return;   // already spinning, do not double-spawn

        Transform spawnPoint = centerBody ? centerBody : transform;

        activeSpinInstance = Instantiate(prefab, spawnPoint.position, spawnPoint.rotation, spawnPoint);
        activeSpinInstance.SetActive(true);       // in case the prefab was saved disabled

        ParticleSystem[] systems = activeSpinInstance.GetComponentsInChildren<ParticleSystem>(true);
        if (systems.Length == 0)
        {
            if (debugMode) Debug.LogWarning($"[YoruVFX] {(airborne ? "Air" : "Ground")} spin prefab '{prefab.name}' has no ParticleSystem. Nothing to play.");
            return;
        }

        // Play On Awake may already have started them on the Instantiate frame. Stop and clear
        // first so the settings below apply to a clean run, then start everything together.
        foreach (ParticleSystem ps in systems)
            ps.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);

        float longestDuration = 0f;
        float longestLifetime = 0f;
        int forcedLocal = 0;
        int zeroedDelays = 0;
        foreach (ParticleSystem ps in systems)
        {
            ParticleSystem.MainModule main = ps.main;
            if (main.simulationSpace != ParticleSystemSimulationSpace.Local)
            {
                main.simulationSpace = ParticleSystemSimulationSpace.Local;
                forcedLocal++;
            }
            if (MaxOf(main.startDelay) > 0.0001f)
            {
                main.startDelay = new ParticleSystem.MinMaxCurve(0f);
                zeroedDelays++;
            }
            if (main.duration > longestDuration) longestDuration = main.duration;
            float life = MaxOf(main.startLifetime);
            if (life > longestLifetime) longestLifetime = life;
        }

        // One speed for the whole effect, so the prefab keeps its internal proportions and its
        // longest system lands exactly on Spin Length.
        float speed = 1f;
        if (spinLength > 0.01f && longestDuration > 0.0001f)
            speed = longestDuration / spinLength;
        foreach (ParticleSystem ps in systems)
        {
            ParticleSystem.MainModule main = ps.main;
            main.simulationSpeed = speed;
        }

        // Real seconds the last particle can still be alive after emission stops. PlaySpinStop
        // waits this long before destroying, so the tail fades instead of being cut.
        activeSpinTail = Mathf.Clamp(longestLifetime / speed, 0.1f, 3f);

        foreach (ParticleSystem ps in systems)
            ps.Play(false);

        if (debugMode)
            Debug.Log($"[YoruVFX] {(airborne ? "Air" : "Ground")} spin: '{prefab.name}' at {spawnPoint.name}, "
                + $"{systems.Length} systems, longest {longestDuration:F2}s fitted to {spinLength:F2}s at speed {speed:F2}, "
                + $"{forcedLocal} forced Local, {zeroedDelays} delays zeroed, tail {activeSpinTail:F2}s");
    }

    /// <summary>Stop emitting and let the live particles fade out on her. The instance is destroyed
    /// only after the longest particle lifetime has passed, so nothing is ever cut mid frame.</summary>
    public void PlaySpinStop()
    {
        if (activeSpinInstance == null) return;

        foreach (ParticleSystem ps in activeSpinInstance.GetComponentsInChildren<ParticleSystem>(true))
            ps.Stop(false, ParticleSystemStopBehavior.StopEmitting);

        Destroy(activeSpinInstance, activeSpinTail);
        activeSpinInstance = null;

        if (debugMode) Debug.Log($"[YoruVFX] Spin VFX stopped, fading for {activeSpinTail:F2}s");
    }

    /// <summary>Lay the beyblade's ground hazard where she is standing and hand it back. Born at
    /// Ground Spin Point and NOT parented, so it stays on the floor when she moves on. Plays
    /// exactly as authored: no speed change, no delay change, the prefab keeps its own pacing and
    /// its own lifetime. PlayerCombat owns what it does to enemies, and whether it may spawn at
    /// all (unlock, cooldown). Returns null when no prefab is assigned.</summary>
    public GameObject SpawnGroundHazard()
    {
        if (groundSpinVFX == null) return null;

        Transform at = groundSpinPoint ? groundSpinPoint : (centerBody ? centerBody : transform);
        GameObject zone = Instantiate(groundSpinVFX, at.position, at.rotation);
        zone.SetActive(true);                     // in case the prefab was saved disabled

        // Pull the authored telegraph forward so the eruption lands with the spin. Delays are
        // shifted, not zeroed, so IN, LOOP and OUT keep their order. Done before the systems
        // start so it applies to this run, and before NaturalLifetimeOf so the zone measures
        // the shortened timeline.
        int shifted = 0;
        if (groundHazardDelayShift > 0.0001f)
        {
            foreach (ParticleSystem ps in zone.GetComponentsInChildren<ParticleSystem>(true))
            {
                ParticleSystem.MainModule main = ps.main;
                float delay = MaxOf(main.startDelay);
                if (delay <= 0.0001f) continue;
                bool wasPlaying = ps.isPlaying;
                if (wasPlaying) ps.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                main.startDelay = new ParticleSystem.MinMaxCurve(Mathf.Max(0f, delay - groundHazardDelayShift));
                if (wasPlaying) ps.Play(false);
                shifted++;
            }
        }

        if (debugMode)
            Debug.Log($"[YoruVFX] Ground hazard: '{groundSpinVFX.name}' laid at {at.name} ({at.position}), "
                + $"{shifted} delays shifted by {groundHazardDelayShift:F2}s, own lifetime {NaturalLifetimeOf(zone):F2}s");
        return zone;
    }

    /// <summary>Real seconds until the last particle of this effect can be gone: the longest
    /// start delay plus duration plus particle lifetime across its systems, at each system's own
    /// simulation speed. A looping system reports one cycle, so give a looping prefab an explicit
    /// duration instead of relying on this.</summary>
    public float NaturalLifetimeOf(GameObject effect)
    {
        float longest = 0f;
        foreach (ParticleSystem ps in effect.GetComponentsInChildren<ParticleSystem>(true))
        {
            ParticleSystem.MainModule main = ps.main;
            float speed = Mathf.Max(0.01f, main.simulationSpeed);
            float t = (MaxOf(main.startDelay) + main.duration + MaxOf(main.startLifetime)) / speed;
            if (t > longest) longest = t;
        }
        return Mathf.Max(0.1f, longest);
    }

    /// <summary>Largest value a MinMaxCurve can produce, whichever of its four modes it is in.</summary>
    private static float MaxOf(ParticleSystem.MinMaxCurve curve)
    {
        switch (curve.mode)
        {
            case ParticleSystemCurveMode.Constant:     return curve.constant;
            case ParticleSystemCurveMode.TwoConstants: return Mathf.Max(curve.constantMin, curve.constantMax);
            case ParticleSystemCurveMode.Curve:        return curve.curveMultiplier * PeakOf(curve.curve);
            default:                                   return curve.curveMultiplier * Mathf.Max(PeakOf(curve.curveMin), PeakOf(curve.curveMax));
        }
    }

    private static float PeakOf(AnimationCurve c)
    {
        if (c == null || c.length == 0) return 0f;
        float peak = float.MinValue;
        for (int i = 0; i <= 16; i++)
            peak = Mathf.Max(peak, c.Evaluate(i / 16f));
        return peak;
    }

    /// <summary>Play hit reaction VFX on Yoru.</summary>
    public void PlayHitReactVFX(bool isHeavy)
    {
        GameObject prefab = isHeavy ? heavyHitReactVFX : lightHitReactVFX;
        if (prefab == null) return;

        Transform spawnPoint = centerBody ? centerBody : transform;
        SpawnEffect(prefab, spawnPoint.position, spawnPoint.rotation);
    }

    // ========== DODGE VFX, Called by PlayerCombat ==========

    /// <summary>Spawn dodge trail at Yoru's feet.</summary>
    public void PlayDodgeTrailVFX()
    {
        if (dodgeTrailPrefab == null) return;
        Vector3 pos = transform.position;
        pos.y += 0.1f;
        GameObject vfx = Instantiate(dodgeTrailPrefab, pos, transform.rotation);
        Destroy(vfx, dodgeVFXLifetime);
    }

    /// <summary>Spawn dodge dash damage trail.</summary>
    public void PlayDodgeDashTrailVFX()
    {
        if (dodgeDashTrailPrefab == null) return;
        Vector3 pos = transform.position;
        pos.y += 0.1f;
        GameObject vfx = Instantiate(dodgeDashTrailPrefab, pos, transform.rotation);
        Destroy(vfx, dodgeVFXLifetime);
    }

    // ========== HIT SPARK VFX, Called by CombatFeedbackManager ==========

    /// <summary>Spawn hit spark at contact point.</summary>
    public void PlayHitSparkVFX(Vector3 contactPoint, bool isHeavy)
    {
        GameObject prefab = isHeavy ? heavyHitSparkPrefab : lightHitSparkPrefab;
        if (prefab == null) return;
        GameObject vfx = Instantiate(prefab, contactPoint, Quaternion.identity);
        Destroy(vfx, hitSparkLifetime);
    }
}