using UnityEngine;
using UnityEngine.AI;
using System.Collections;

/// <summary>
/// Universal Enemy AI — works for all enemy tiers.
/// Per-enemy differences handled entirely through Inspector values.
/// 
/// States: Idle → Alert → Chase → Telegraph → Attack → Recovery → (loop)
/// + Stagger (from heavy/parry), Teleport (optional), Dead
/// + Narrative overrides: LostSoul, Dialogue, Peaceful
/// </summary>
public class EnemyCombat : MonoBehaviour
{
    #region Enums
    public enum EnemyState
    {
        // Narrative states (override combat)
        LostSoul,
        Dialogue,
        Peaceful,
        
        // Combat states
        Idle,
        Alert,
        Chase,
        Telegraph,
        Attack,
        Recovery,
        HitReact,
        Stagger,
        Teleport,
        Dead,
        
        // Disengage — appended last so existing serialized currentState indices don't shift.
        Returning
    }
    
    public enum AttackPhase
    {
        Both,
        Phase1Only,
        Phase2Only
    }
    #endregion
    
    #region Attack Definition
    [System.Serializable]
    public class EnemyAttack
    {
        public string attackName = "Attack";
        
        [Header("Animation")]
        [Tooltip("Animator state name for telegraph wind-up")]
        public string telegraphAnim = "HairLash_Telegraph";
        [Tooltip("Animator state name for attack strike (leave empty for AoE/scream type)")]
        public string attackAnim = "HairLash_Attack";
        
        [Header("Speed")]
        [Tooltip("Playback speed for telegraph animation")]
        public float telegraphSpeed = 1f;
        [Tooltip("Playback speed for attack animation")]
        public float attackSpeed = 1f;
        
        [Header("Timing")]
        [Tooltip("Base telegraph duration before speed modifier")]
        public float telegraphDuration = 0.4f;
        [Tooltip("Base attack duration before speed modifier")]
        public float attackDuration = 0.3f;
        
        [Header("Damage")]
        public int damage = 3;
        [Tooltip("Attack range — how close player must be to get hit")]
        public float range = 3.5f;
        [Tooltip("Is this AoE? (damage all in range vs single target)")]
        public bool isAoE = false;
        
        [Header("Player Effects")]
        [Tooltip("Stun player for this duration on hit (0 = no stun)")]
        public float stunPlayerDuration = 0f;
        
        [Header("Phase")]
        public AttackPhase phase = AttackPhase.Both;
        [Tooltip("Selection weight — higher = more likely to be picked")]
        [Range(1, 100)]
        public int weight = 50;
    }
    #endregion
    
    #region Serialized Fields
    [Header("Current State (Debug)")]
    [SerializeField] private EnemyState currentState = EnemyState.LostSoul;
    
    [Header("Detection")]
    [SerializeField] private float detectionRange = 10f;
    [SerializeField] private float attackRange = 3.5f;
    [SerializeField] private float escapeRange = 15f;
    [Tooltip("Full vision-cone width in degrees (e.g. 120 = 60° each side of forward). Player must be within this cone AND within detectionRange to be seen. Only gates INITIAL detection and re-detection — once chasing, the enemy tracks without FOV check.")]
    [SerializeField, Range(30f, 360f)] private float visionAngle = 120f;
    
    [Header("Timing")]
    [SerializeField] private float alertDuration = 0.5f;
    [SerializeField] private float recoveryDuration = 0.4f;
    [SerializeField] private float hitReactDuration = 0.5f;
    [SerializeField] private float staggerDuration = 1.0f;
    [SerializeField] private float attackCooldown = 3.0f;
    
    [Header("Movement")]
    [SerializeField] private float patrolSpeed = 1.5f;
    [SerializeField] private float chaseSpeed = 3.0f;
    [SerializeField] private float rotationSpeed = 5f;
    [Tooltip("Strafe speed when circling player during attack cooldown")]
    [SerializeField] private float strafeSpeed = 2.0f;
    
    [Header("Chase Teleport Timer")]
    [Tooltip("Seconds of chasing without attacking before forced teleport (0 = disabled)")]
    [SerializeField] private float chaseTeleportTime = 8f;
    
    [Header("Attacks")]
    [SerializeField] private EnemyAttack[] attacks;
    
    [Header("Phase System")]
    [SerializeField] private bool hasPhases = false;
    [Tooltip("HP percentage to trigger Phase 2 (0.5 = 50%)")]
    [SerializeField] private float phaseThreshold = 0.5f;
    [SerializeField] private float chaseSpeedP2 = 4.0f;
    [SerializeField] private float attackCooldownP2 = 2.0f;
    
    [Header("Teleport")]
    [SerializeField] private bool canTeleport = true;
    [Tooltip("Chance to teleport after recovery (0-1)")]
    [SerializeField] private float teleportChance = 0.5f;
    [SerializeField] private float teleportChanceP2 = 0.8f;
    [SerializeField] private float teleportDistance = 5f;
    [Tooltip("Playback speed for teleport animations")]
    [SerializeField] private float teleportSpeed = 1.0f;
    [SerializeField] private float teleportSpeedP2 = 1.4f;
    [Tooltip("Duration of Teleport_Out animation at 1x speed")]
    [SerializeField] private float teleportOutDuration = 0.5f;
    [Tooltip("Duration of Teleport_In animation at 1x speed")]
    [SerializeField] private float teleportInDuration = 0.4f;
    
    [Header("Animation State Names")]
    // NOTE: Each enemy prefab must set these fields in the Inspector to match its Animator state names.
    // Kodama defaults: idleAnim="Kodama_Idle", walkAnim="Kodama_Walk", runAnim="Kodama_Run",
    //   alertAnim="Kodama_Alert", staggerAnim="Kodama_Stagger", hitReactAnim="Kodama_HitReact",
    //   deathAnim="Kodama_Death". Nopperabo: set hitReactAnim to match its Animator state name.
    [SerializeField] private string idleAnim = "Float_Idle";
    [SerializeField] private string walkAnim = "Walk_Glide";
    [SerializeField] private string runAnim = "Run_Chase";
    [SerializeField] private string alertAnim = "Alert_Notice";
    [SerializeField] private string staggerAnim = "Stagger";
    [SerializeField] private string deathAnim = "Death_Dissolve";
    [SerializeField] private string hitReactAnim = "Hit_Reaction";
    [SerializeField] private string recoveryAnim = "";
    [SerializeField] private string teleportOutAnim = "Teleport_Out";
    [SerializeField] private string teleportInAnim = "Teleport_In";
    
    [Header("Animator Layer")]
    [SerializeField] private int combatLayerIndex = 0;
    
    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;
    [SerializeField] private bool showGizmos = true;
    
    [Header("VFX")]
    [Tooltip("Particle effect for telegraph wind-up")]
    [SerializeField] private ParticleSystem telegraphVFX;
    [Tooltip("Particle effect for attack strike")]
    [SerializeField] private ParticleSystem attackVFX;
    [Tooltip("Particle effect for stagger")]
    [SerializeField] private ParticleSystem staggerVFX;
    [Tooltip("Particle effect for hit reaction")]
    [SerializeField] private ParticleSystem hitReactVFX;
    [Tooltip("Particle effect for teleport out")]
    [SerializeField] private ParticleSystem teleportOutVFX;
    [Tooltip("Particle effect for teleport in")]
    [SerializeField] private ParticleSystem teleportInVFX;
    [Tooltip("Particle effect for death")]
    [SerializeField] private ParticleSystem deathVFX;
    [Tooltip("Particle effect for alert/notice")]
    [SerializeField] private ParticleSystem alertVFX;
    
    [Header("Damage")]
    [Tooltip("Damage at or above this threshold is treated as a heavy hit")]
    [SerializeField] private int heavyHitThreshold = 2;
    
    [Header("Disengage")]
    [Tooltip("When the enemy loses its target (player became Tomoe, or escaped beyond escapeRange), walk back to spawn position instead of idling in place. GDD Doc 07 universal rule — continue normal behaviour at home.")]
    [SerializeField] private bool returnToSpawnOnDisengage = true;
    [Tooltip("Seconds the enemy stands still in idle (rotating to face spawn) before starting the walk home. The 'beat' between losing target and committing to walk-home.")]
    [SerializeField] private float returnPauseDuration = 2f;
    [Tooltip("Planar (XZ) distance from spawn position considered 'home' — once within this range, transition to Idle. Slightly larger than navAgent.stoppingDistance for slope/platform robustness.")]
    [SerializeField] private float returnArrivalThreshold = 1.0f;
    [Tooltip("Multiplier on rotationSpeed during the disengage pause — used to snap-pivot toward Granny when transform is detected. 8x normal completes a 180° turn in ~0.2s.")]
    [SerializeField] private float disengageRotationSpeedMultiplier = 8f;
    #endregion
    
    #region Private Fields
    private Transform player;
    private FormController playerFormController;
    private EnemyHealth enemyHealth;
    private Animator animator;
    private NavMeshAgent navAgent;
    
    // Timing
    private float stateTimer;
    private float cooldownTimer;
    
    // Phase
    private bool isPhase2;
    
    // Attack
    private EnemyAttack currentAttack;
    
    // Alert (only triggers once per encounter)
    private bool hasAlerted;
    
    // Teleport
    private bool isTeleporting;
    
    // Chase timer — teleport if chasing too long without attacking
    private float chaseTimer;
    
    // Disengage — cached spawn position for return-to-spawn behaviour.
    private Vector3 spawnPosition;
    private float returnStartTime;
    private bool returnWalkStarted;
    
    // Animation tracking — prevents CrossFade from restarting every frame
    private string currentPlayingAnim = "";
    
    // Animator speed parameter
    private static readonly int HashAnimSpeed = Animator.StringToHash("AnimSpeed");
    #endregion
    
    #region Unity Lifecycle
    private void Start()
    {
        player = GameObject.FindGameObjectWithTag("Player")?.transform;
        if (player == null)
            Debug.LogError($"{gameObject.name}: No Player found! Tag your player 'Player'.");
        
        // Phase 3 — cache FormController on the player. Per GDD Doc 07 universal rule,
        // enemies never attack Tomoe. Cached once; queried inline (no per-frame GetComponent).
        playerFormController = player != null ? player.GetComponent<FormController>() : null;
        if (player != null && playerFormController == null)
            Debug.LogWarning($"{gameObject.name}: FormController not found on Player. Tomoe-ignore gate will be INACTIVE — this enemy will attack Tomoe.");
        
        enemyHealth = GetComponent<EnemyHealth>();
        animator = GetComponent<Animator>();
        navAgent = GetComponent<NavMeshAgent>();
        
        if (navAgent != null)
        {
            navAgent.speed = patrolSpeed;
            navAgent.stoppingDistance = 0.5f;
            navAgent.updateRotation = false; // We handle rotation manually
        }
        
        if (attacks == null || attacks.Length == 0)
            Debug.LogWarning($"{gameObject.name}: No attacks defined!");
        
        // Cache spawn position so Returning state can walk the enemy home after disengage.
        spawnPosition = transform.position;
        
        SetState(EnemyState.LostSoul);
        DebugLog("Initialized");
    }
    
    private void Update()
    {
        // Dead check
        if (enemyHealth != null && enemyHealth.IsDead())
        {
            if (currentState != EnemyState.Dead)
                SetState(EnemyState.Dead);
            return;
        }
        
        // Phase check
        UpdatePhase();
        
        // Cooldown tick
        if (cooldownTimer > 0)
            cooldownTimer -= Time.deltaTime;
        
        // State timer tick
        if (stateTimer > 0)
            stateTimer -= Time.deltaTime;
        
        // Run current state
        switch (currentState)
        {
            case EnemyState.LostSoul: HandleLostSoul(); break;
            case EnemyState.Dialogue: HandleDialogue(); break;
            case EnemyState.Peaceful: HandlePeaceful(); break;
            case EnemyState.Idle: HandleIdle(); break;
            case EnemyState.Returning: HandleReturning(); break;
            case EnemyState.Alert: HandleAlert(); break;
            case EnemyState.Chase: HandleChase(); break;
            case EnemyState.Telegraph: HandleTelegraph(); break;
            case EnemyState.Attack: HandleAttack(); break;
            case EnemyState.Recovery: HandleRecovery(); break;
            case EnemyState.HitReact: HandleHitReact(); break;
            case EnemyState.Stagger: HandleStagger(); break;
            case EnemyState.Teleport: break; // Handled by coroutine
            case EnemyState.Dead: HandleDead(); break;
        }
        
        // Debug keys
        HandleDebugInput();
    }
    #endregion
    
    #region State Handlers
    private void HandleLostSoul()
    {
        StopNav();
        PlayAnimation(idleAnim);
        
        // Transition to combat when player is in vision cone — but never engage Tomoe (GDD Doc 07).
        if (player != null && !PlayerIsTomoe() && PlayerInVision())
        {
            SetState(EnemyState.Alert);
        }
    }
    
    private void HandleDialogue()
    {
        StopNav();
    }
    
    private void HandlePeaceful()
    {
        StopNav();
        PlayAnimation(idleAnim);
    }
    
    private void HandleIdle()
    {
        StopNav();
        PlayAnimation(idleAnim);
        
        if (player == null) return;
        
        // GDD Doc 07: enemies ignore Tomoe — no engagement from Idle while she is in human form.
        if (PlayerIsTomoe()) return;
        
        if (PlayerInVision())
        {
            if (!hasAlerted)
            {
                SetState(EnemyState.Alert);
            }
            else
            {
                SetState(EnemyState.Chase);
            }
        }
    }
    
    /// <summary>
    /// Walking back to spawn after disengage (Tomoe-form or escape-range).
    /// Two phases: (1) pause + idle for returnPauseDuration, rotating smoothly to face spawn.
    /// (2) walk to spawn at patrolSpeed, no timeout — keeps going until arrived.
    /// Interruptible at any phase — if Yoru re-enters detection range, drop everything and chase.
    /// On arrival the Idle SetState fires PlayAnimation(idleAnim) immediately so the walk anim
    /// can never persist past arrival. Future stare-at-Granny behaviour will live inside the
    /// idle animation itself.
    /// </summary>
    private void HandleReturning()
    {
        if (player == null) return;
        
        // 1. INTERRUPT — Yoru is back and in vision cone. Overrides everything (pause, walk, rotation).
        //    Vision check applies here so Yoru can sneak around an enemy's back during return.
        if (!PlayerIsTomoe() && PlayerInVision())
        {
            SetState(EnemyState.Chase);
            return;
        }
        
        float elapsed = Time.time - returnStartTime;
        
        // 2. PHASE 1 — Pause. Idle anim is already playing (set in SetState).
        //    Snap-pivot to face the player (Granny) — "confused stare" beat per GDD-aligned design.
        //    Uses disengageRotationSpeedMultiplier for a sharp turn (~0.2s for 180°).
        if (!returnWalkStarted)
        {
            FaceTowardsPlayerFast();
            
            if (elapsed >= returnPauseDuration)
            {
                // Kick off the walk phase.
                returnWalkStarted = true;
                if (navAgent != null && navAgent.isOnNavMesh)
                {
                    navAgent.isStopped = false;
                    navAgent.speed = patrolSpeed;
                    navAgent.SetDestination(spawnPosition);
                }
                PlayAnimation(walkAnim);
                DebugLog("Pause complete — walking home");
            }
            return;
        }
        
        // 3. PHASE 2 — Walking home. Arrival checked via navAgent AND planar XZ distance
        //    (full 3D Vector3.Distance would miss arrival on slopes/platforms where y differs).
        bool arrivedByNav = navAgent != null && navAgent.isOnNavMesh && !navAgent.pathPending
                            && navAgent.remainingDistance <= navAgent.stoppingDistance + 0.05f;
        Vector3 toSpawn = spawnPosition - transform.position;
        toSpawn.y = 0f;
        bool arrivedByDistance = toSpawn.sqrMagnitude <= returnArrivalThreshold * returnArrivalThreshold;
        
        if (arrivedByNav || arrivedByDistance)
        {
            DebugLog("Arrived at spawn — entering Idle");
            SetState(EnemyState.Idle);
            return;
        }
        
        // Continue facing direction of travel.
        FaceTowardsSpawnOrVelocity();
    }
    
    private void HandleAlert()
    {
        StopNav();
        LookAtPlayer();
        
        if (stateTimer <= 0)
        {
            hasAlerted = true;
            SetState(EnemyState.Chase);
        }
    }
    
    private void HandleChase()
    {
        if (player == null) return;
        
        // GDD Doc 07: drop combat the moment the player is Tomoe.
        // Defensive catch-all — Phase 2 transform lock should prevent mid-combat transform,
        // but this also resolves wrong-choice BecomeHostile() chains (Alert → Chase → here → Idle/Returning)
        // and covers any scripted force-transform or dev hot-reload edge case.
        if (PlayerIsTomoe())
        {
            SetState(returnToSpawnOnDisengage ? EnemyState.Returning : EnemyState.Idle);
            return;
        }

        float dist = DistanceToPlayer();
        
        // Increment chase timer
        chaseTimer += Time.deltaTime;

        // In attack range and cooldown ready — attack
        if (dist <= attackRange && cooldownTimer <= 0)
        {
            chaseTimer = 0f; // Reset — we're attacking
            EnemyAttack chosen = ChooseAttack();
            if (chosen != null)
            {
                currentAttack = chosen;
                SetState(EnemyState.Telegraph);
                return;
            }
        }

        // In attack range but cooldown not ready — CIRCLE the player instead of standing idle
        if (dist <= attackRange)
        {
            LookAtPlayer();
            if (navAgent != null && navAgent.isOnNavMesh)
            {
                // Strafe sideways around the player
                Vector3 strafeDir = Vector3.Cross(Vector3.up, (player.position - transform.position).normalized);
                Vector3 strafeTarget = transform.position + strafeDir * 2f;
                navAgent.isStopped = false;
                navAgent.speed = strafeSpeed;
                navAgent.SetDestination(strafeTarget);
            }
            PlayAnimation(walkAnim);
            return;
        }

        // Chase timer teleport — been chasing too long without attacking
        if (canTeleport && !isTeleporting && chaseTeleportTime > 0 && chaseTimer >= chaseTeleportTime)
        {
            chaseTimer = 0f;
            DebugLog($"Chase timer expired ({chaseTeleportTime}s), teleporting to close gap");
            StartCoroutine(TeleportSequence());
            return;
        }

        // Distance-based teleport — player escaped far away
        if (canTeleport && !isTeleporting && dist > escapeRange)
        {
            chaseTimer = 0f;
            DebugLog("Player too far, teleporting to close gap");
            StartCoroutine(TeleportSequence());
            return;
        }

        // Player beyond escape range and no teleport — disengage.
        if (!canTeleport && dist > escapeRange)
        {
            SetState(returnToSpawnOnDisengage ? EnemyState.Returning : EnemyState.Idle);
            return;
        }

        // Chase — distance-based speed and animation
        LookAtPlayer();
        float walkThreshold = attackRange + 3f;

        if (dist <= walkThreshold)
        {
            // Close — walk
            if (navAgent != null && navAgent.isOnNavMesh)
            {
                navAgent.isStopped = false;
                navAgent.speed = patrolSpeed;
                navAgent.SetDestination(player.position);
            }
            PlayAnimation(walkAnim);
        }
        else
        {
            // Far — run
            float speed = isPhase2 ? chaseSpeedP2 : chaseSpeed;
            if (navAgent != null && navAgent.isOnNavMesh)
            {
                navAgent.isStopped = false;
                navAgent.speed = speed;
                navAgent.SetDestination(player.position);
            }
            PlayAnimation(runAnim);
        }
    }
    private void HandleTelegraph()
    {
        StopNav();
        LookAtPlayer();
        
        if (stateTimer <= 0)
        {
            SetState(EnemyState.Attack);
        }
    }
    
    private void HandleAttack()
    {
        StopNav();
        
        if (stateTimer <= 0)
        {
            // Deal damage at end of attack
            DealDamageToPlayer();
            SetState(EnemyState.Recovery);
        }
    }
    
    private void HandleRecovery()
    {
        StopNav();
        
        if (stateTimer <= 0)
        {
            // Decide: teleport or chase
            float tpChance = isPhase2 ? teleportChanceP2 : teleportChance;
            
            if (canTeleport && Random.value < tpChance)
            {
                StartCoroutine(TeleportSequence());
            }
            else
            {
                float cd = isPhase2 ? attackCooldownP2 : attackCooldown;
                cooldownTimer = cd;
                SetState(EnemyState.Chase);
            }
        }
    }
    
    private void HandleStagger()
    {
        StopNav();
        
        if (stateTimer <= 0)
        {
            SetState(EnemyState.Chase);
        }
    }
    
    private void HandleHitReact()
    {
        StopNav();
        LookAtPlayer();
        
        if (stateTimer <= 0)
        {
            SetState(EnemyState.Chase);
        }
    }
    
    private void HandleDead()
    {
        StopNav();
    }
    #endregion
    
    #region State Transitions
    public void SetState(EnemyState newState)
    {
        if (currentState == EnemyState.Dead && newState != EnemyState.Dead)
            return; // Can't leave dead state
        
        if (currentState == newState) return;
        
        EnemyState oldState = currentState;
        currentState = newState;
        
        DebugLog($"{oldState} → {newState}");
        
        // Stop any ongoing coroutines when changing state (except teleport managing itself)
        if (newState != EnemyState.Teleport)
            isTeleporting = false;
        
        switch (newState)
        {
            case EnemyState.Alert:
                stateTimer = alertDuration;
                PlayAnimation(alertAnim);
                SetAnimSpeed(1f);
                PlayVFX(alertVFX);
                break;
                
            case EnemyState.Telegraph:
                if (currentAttack != null)
                {
                    float speed = currentAttack.telegraphSpeed;
                    float duration = currentAttack.telegraphDuration / speed;
                    stateTimer = duration;
                    PlayAnimation(currentAttack.telegraphAnim);
                    SetAnimSpeed(speed);
                    PlayVFX(telegraphVFX);
                    DebugLog($"Telegraph: {currentAttack.attackName} ({duration:F2}s)");
                }
                break;
                
            case EnemyState.Attack:
                if (currentAttack != null)
                {
                    float speed = currentAttack.attackSpeed;
                    float duration = currentAttack.attackDuration / speed;
                    stateTimer = duration;
                    
                    // Some attacks have no attack anim (scream type — telegraph IS the attack)
                    if (!string.IsNullOrEmpty(currentAttack.attackAnim))
                    {
                        PlayAnimation(currentAttack.attackAnim);
                        SetAnimSpeed(speed);
                    }
                    
                    PlayVFX(attackVFX);
                    DebugLog($"Attack: {currentAttack.attackName} ({duration:F2}s, {currentAttack.damage} dmg)");
                }
                break;
                
            case EnemyState.Recovery:
                stateTimer = recoveryDuration;
                PlayAnimation(string.IsNullOrEmpty(recoveryAnim) ? idleAnim : recoveryAnim);
                SetAnimSpeed(1f);
                break;
                
            case EnemyState.HitReact:
                stateTimer = hitReactDuration;
                ForcePlayAnimation(hitReactAnim);
                SetAnimSpeed(1f);
                PlayVFX(hitReactVFX);
                break;
                
            case EnemyState.Stagger:
                stateTimer = staggerDuration;
                PlayAnimation(staggerAnim);
                SetAnimSpeed(1f);
                cooldownTimer = 0; // Reset cooldown after stagger
                PlayVFX(staggerVFX);
                DebugLog($"STAGGERED for {staggerDuration}s");
                break;
                
            case EnemyState.Dead:
                StopNav();
                PlayAnimation(deathAnim);
                SetAnimSpeed(1f);
                PlayVFX(deathVFX);
                break;
                
            case EnemyState.Chase:
                chaseTimer = 0f;
                SetAnimSpeed(1f);
                break;
                
            case EnemyState.Idle:
                StopNav();
                PlayAnimation(idleAnim);
                SetAnimSpeed(1f);
                break;
                
            case EnemyState.Returning:
                returnStartTime = Time.time;
                returnWalkStarted = false;
                StopNav();
                PlayAnimation(idleAnim);
                SetAnimSpeed(1f);
                DebugLog("Disengaging — pausing before return walk");
                break;
                
            case EnemyState.Teleport:
                // Handled by coroutine
                break;
        }
    }
    #endregion
    
    #region Phase System
    private void UpdatePhase()
    {
        if (!hasPhases || isPhase2) return;
        if (enemyHealth == null) return;
        
        if (enemyHealth.GetHealthPercentage() <= phaseThreshold)
        {
            isPhase2 = true;
            DebugLog("⚡ PHASE 2 ACTIVATED");
        }
    }
    #endregion
    
    #region Attack Selection
    private EnemyAttack ChooseAttack()
    {
        if (attacks == null || attacks.Length == 0) return null;
        
        // Build list of valid attacks for current phase
        int totalWeight = 0;
        
        for (int i = 0; i < attacks.Length; i++)
        {
            if (IsAttackValid(attacks[i]))
                totalWeight += attacks[i].weight;
        }
        
        if (totalWeight == 0) return null;
        
        // Weighted random selection
        int roll = Random.Range(0, totalWeight);
        int running = 0;
        
        for (int i = 0; i < attacks.Length; i++)
        {
            if (!IsAttackValid(attacks[i])) continue;
            
            running += attacks[i].weight;
            if (roll < running)
                return attacks[i];
        }
        
        return attacks[0]; // Fallback
    }
    
    private bool IsAttackValid(EnemyAttack atk)
    {
        if (atk.phase == AttackPhase.Phase1Only && isPhase2) return false;
        if (atk.phase == AttackPhase.Phase2Only && !isPhase2) return false;
        return true;
    }
    #endregion
    
    #region Damage
    private void DealDamageToPlayer()
    {
        if (currentAttack == null || player == null) return;
        
        float dist = DistanceToPlayer();
        
        if (dist > currentAttack.range)
        {
            DebugLog($"Attack missed — player out of range ({dist:F1}m > {currentAttack.range}m)");
            return;
        }
        
        PlayerHealth playerHealth = player.GetComponent<PlayerHealth>();
        if (playerHealth == null) return;
        
        bool isHeavy = currentAttack.damage >= heavyHitThreshold;
        playerHealth.TakeDamage(currentAttack.damage, isHeavy, transform.position);
        DebugLog($"⚔️ Hit player for {currentAttack.damage} ({currentAttack.attackName})");
        
        // Apply stun if attack has it
        if (currentAttack.stunPlayerDuration > 0)
        {
            playerHealth.ApplyStun(currentAttack.stunPlayerDuration);
            DebugLog($"Player stunned for {currentAttack.stunPlayerDuration}s");
        }
    }
    #endregion
    
    #region Teleport
    private IEnumerator TeleportSequence()
    {
        SetState(EnemyState.Teleport);
        isTeleporting = true;
        
        float speed = isPhase2 ? teleportSpeedP2 : teleportSpeed;
        
        // Phase 1: Teleport Out
        PlayAnimation(teleportOutAnim);
        SetAnimSpeed(speed);
        PlayVFX(teleportOutVFX);
        
        float outTime = teleportOutDuration / speed;
        yield return new WaitForSeconds(outTime);
        
        if (!isTeleporting) yield break; // State was changed externally
        
        // Reposition behind player
        Vector3 behindPlayer = GetPositionBehindPlayer();
        
        if (navAgent != null)
        {
            navAgent.enabled = false;
            transform.position = behindPlayer;
            navAgent.enabled = true;
            
            if (navAgent.isOnNavMesh)
                navAgent.Warp(behindPlayer);
        }
        else
        {
            transform.position = behindPlayer;
        }
        
        // Phase 2: Teleport In
        PlayAnimation(teleportInAnim);
        SetAnimSpeed(speed);
        PlayVFX(teleportInVFX);
        
        float inTime = teleportInDuration / speed;
        yield return new WaitForSeconds(inTime);
        
        if (!isTeleporting) yield break;
        
        isTeleporting = false;
        
        // After teleport, set cooldown and chase
        float cd = isPhase2 ? attackCooldownP2 : attackCooldown;
        cooldownTimer = cd * 0.5f; // Shorter cooldown after teleport — keeps pressure on
        SetState(EnemyState.Chase);
    }
    
    private Vector3 GetPositionBehindPlayer()
    {
        if (player == null) return transform.position;
        
        Vector3 behindDir = -player.forward;
        Vector3 targetPos = player.position + behindDir * teleportDistance;
        
        // Use larger search radius to ensure NavMesh hit
        float searchRadius = teleportDistance * 3f;
        if (NavMesh.SamplePosition(targetPos, out NavMeshHit hit, searchRadius, NavMesh.AllAreas))
        {
            return hit.position;
        }
        
        // Fallback: try to the side
        Vector3 sidePos = player.position + player.right * teleportDistance;
        if (NavMesh.SamplePosition(sidePos, out hit, searchRadius, NavMesh.AllAreas))
        {
            return hit.position;
        }
        
        // Fallback: try in front
        Vector3 frontPos = player.position + player.forward * teleportDistance;
        if (NavMesh.SamplePosition(frontPos, out hit, searchRadius, NavMesh.AllAreas))
        {
            return hit.position;
        }
        
        return transform.position; // Stay put if no valid position
    }
    #endregion
    
    #region Public Methods (called by other scripts)
    /// <summary>
    /// Called by EnemyHealth when hit by heavy attack or parried.
    /// Interrupts current action and enters stagger state.
    /// </summary>
    public void TriggerStagger()
    {
        if (currentState == EnemyState.Dead) return;
        
        StopAllCoroutines();
        isTeleporting = false;
        SetState(EnemyState.Stagger);
    }

    /// <summary>
    /// Overload with custom stagger duration (used by perfect parry).
    /// </summary>
    public void TriggerStagger(float customDuration)
    {
        if (currentState == EnemyState.Dead) return;
        
        StopAllCoroutines();
        isTeleporting = false;
        SetState(EnemyState.Stagger);
        // Override the default stagger duration set by SetState
        stateTimer = customDuration;
        DebugLog($"STAGGERED (parry) for {customDuration}s");
    }
    
 /// <summary>
/// Called by EnemyHealth on light hit. Transitions to HitReact state so the
/// animation plays fully without being overridden by Chase/Idle handlers.
/// During Telegraph or Attack, does a quick white flash for hit confirmation.
/// </summary>
public void TriggerHitReact()
{
    if (currentState == EnemyState.Dead) return;
    if (currentState == EnemyState.Stagger) return;
    if (currentState == EnemyState.HitReact) return;

    // Interrupt non-critical states with a proper HitReact state
    if (currentState == EnemyState.Idle ||
        currentState == EnemyState.Chase ||
        currentState == EnemyState.Recovery)
    {
        SetState(EnemyState.HitReact);
    }
    else if (currentState == EnemyState.Telegraph || currentState == EnemyState.Attack)
    {
        // Can't interrupt attack — give the player visual confirmation instead
        TriggerHitFlash();
    }
}

/// <summary>
/// Plays a quick white flash to confirm a hit landed during Telegraph or Attack.
/// </summary>
private void TriggerHitFlash()
{
    if (enemyHealth != null)
        enemyHealth.FlashWhite();
    DebugLog($"🤕 Hit during {currentState} — flash only");
}
    /// <summary>
    /// Start combat — called when dialogue fails or player attacks.
    /// </summary>
    public void BecomeHostile()
    {
        if (currentState == EnemyState.Dead) return;
        hasAlerted = false;
        SetState(EnemyState.Alert);
        DebugLog("Became HOSTILE");
    }
    
    /// <summary>
    /// Light path — soul passes on peacefully.
    /// </summary>
    public void BecomePeaceful()
    {
        if (currentState == EnemyState.Dead) return;
        StopAllCoroutines();
        SetState(EnemyState.Peaceful);
        StartCoroutine(PassOnPeacefully());
    }
    
    /// <summary>
    /// Reset to initial state.
    /// </summary>
    public void ResetCombatState()
    {
        StopAllCoroutines();
        isTeleporting = false;
        isPhase2 = false;
        hasAlerted = false;
        cooldownTimer = 0;
        stateTimer = 0;
        chaseTimer = 0;
        currentAttack = null;
        currentPlayingAnim = "";
        currentState = EnemyState.LostSoul;
        SetAnimSpeed(1f);
        DebugLog("Combat state RESET");
    }
    #endregion
    
    #region Form Gate
    /// <summary>
    /// Returns true when the player is in Tomoe (human) form.
    /// Used by combat state transitions to enforce GDD Doc 07 universal rule —
    /// enemies never attack Tomoe. Cheap inline check (null + bool read).
    /// </summary>
    private bool PlayerIsTomoe() => playerFormController != null && playerFormController.IsHuman;
    
    /// <summary>
    /// Vision-based detection — player is in this enemy's forward cone AND within range.
    /// Used for INITIAL detection (LostSoul → Alert, Idle → Chase) and re-engage from Returning.
    /// NOT checked during active Chase — once committed, the enemy tracks regardless of facing.
    /// Cheap: one distance check + one angle check, both planar (XZ). No allocations.
    /// </summary>
    private bool PlayerInVision()
    {
        if (player == null) return false;
        
        Vector3 toPlayer = player.position - transform.position;
        toPlayer.y = 0f;
        float sqrDist = toPlayer.sqrMagnitude;
        
        // Range gate (squared compare — avoids sqrt).
        if (sqrDist > detectionRange * detectionRange) return false;
        
        // Player on top of enemy — always seen.
        if (sqrDist < 0.001f) return true;
        
        // Cone gate.
        Vector3 forward = transform.forward;
        forward.y = 0f;
        float angle = Vector3.Angle(forward, toPlayer);
        return angle <= visionAngle * 0.5f;
    }
    #endregion
    
    #region Navigation Helpers
    private void StopNav()
    {
        if (navAgent != null && navAgent.isOnNavMesh)
        {
            navAgent.isStopped = true;
            navAgent.velocity = Vector3.zero;
        }
    }
    
    private void LookAtPlayer()
    {
        if (player == null) return;
        
        Vector3 dir = (player.position - transform.position).normalized;
        dir.y = 0;
        
        if (dir != Vector3.zero)
        {
            Quaternion target = Quaternion.LookRotation(dir);
            transform.rotation = Quaternion.Slerp(transform.rotation, target, Time.deltaTime * rotationSpeed);
        }
    }
    
    /// <summary>
    /// Rotation helper for the Returning state. Faces nav velocity direction while walking
    /// (so the body follows path bends around obstacles), falls back to spawn-direction when
    /// the agent is stopped (phase 1 pause). Smooth Slerp — never snaps.
    /// </summary>
    private void FaceTowardsSpawnOrVelocity()
    {
        Vector3 dir;
        if (navAgent != null && navAgent.velocity.sqrMagnitude > 0.01f)
        {
            dir = navAgent.velocity;
        }
        else
        {
            dir = spawnPosition - transform.position;
        }
        dir.y = 0f;
        
        if (dir.sqrMagnitude > 0.001f)
        {
            Quaternion target = Quaternion.LookRotation(dir);
            transform.rotation = Quaternion.Slerp(transform.rotation, target, Time.deltaTime * rotationSpeed);
        }
    }
    
    /// <summary>
    /// Fast rotation toward the player — the "confused stare" pivot used in phase 1 of Returning.
    /// Multiplies rotationSpeed by disengageRotationSpeedMultiplier so a 180° turn finishes
    /// in roughly 0.2s at default values. Still a Slerp (no snap) — looks sharp, not jarring.
    /// </summary>
    private void FaceTowardsPlayerFast()
    {
        if (player == null) return;
        
        Vector3 dir = player.position - transform.position;
        dir.y = 0f;
        
        if (dir.sqrMagnitude > 0.001f)
        {
            Quaternion target = Quaternion.LookRotation(dir);
            transform.rotation = Quaternion.Slerp(transform.rotation, target,
                Time.deltaTime * rotationSpeed * disengageRotationSpeedMultiplier);
        }
    }
    
    private float DistanceToPlayer()
    {
        if (player == null) return float.MaxValue;
        return Vector3.Distance(transform.position, player.position);
    }
    #endregion
    
    #region Animation Helpers
    private void PlayAnimation(string stateName)
    {
        if (animator == null || string.IsNullOrEmpty(stateName)) return;
        if (stateName == currentPlayingAnim) return; // Already playing — don't restart
        
        currentPlayingAnim = stateName;
        animator.CrossFadeInFixedTime(stateName, 0.1f, combatLayerIndex);
    }
    
    /// <summary>
    /// Force play — used when same animation must restart (e.g. hit react while idling).
    /// Uses animator.Play() (immediate, non-blendable) so it cannot be overridden by
    /// the next PlayAnimation() call in the same or next frame. Same approach as PlayerCombat.
    /// </summary>
    private void ForcePlayAnimation(string stateName)
    {
        if (animator == null || string.IsNullOrEmpty(stateName)) return;
        currentPlayingAnim = stateName;
        animator.Play(stateName, combatLayerIndex, 0f);
    }
    
    private void SetAnimSpeed(float speed)
    {
        if (animator == null) return;
        animator.SetFloat(HashAnimSpeed, speed);
    }
    
    private void PlayVFX(ParticleSystem vfx)
    {
        if (vfx != null) vfx.Play();
    }
    #endregion
    
    #region Coroutines
    private IEnumerator PassOnPeacefully()
    {
        yield return new WaitForSeconds(2f);
        DebugLog("✨ Passing on peacefully...");
        Destroy(gameObject, 1f);
    }
    #endregion
    
    #region Getters
    public EnemyState GetCurrentState() => currentState;
    public bool IsPhase2() => isPhase2;
    public bool IsInCombat() => currentState >= EnemyState.Idle && currentState <= EnemyState.Teleport;

    /// <summary>
    /// Public Animator access — CombatFeedbackManager uses this for hitstop.
    /// </summary>
    public Animator GetAnimator() => animator;
    #endregion
    
    #region Debug
    private void HandleDebugInput()
    {
        // T key removed — conflicts with Transform (cat/human) keybind.
        // Use Inspector button or console command to force hostile.
    }
    
    private void DebugLog(string msg)
    {
        if (showDebugLogs)
            Debug.Log($"[{gameObject.name}] {msg}");
    }
    
    private void OnDrawGizmosSelected()
    {
        if (!showGizmos) return;
        
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);
        
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
        
        Gizmos.color = new Color(1, 0.5f, 0, 0.3f);
        Gizmos.DrawWireSphere(transform.position, escapeRange);
    }
    #endregion
}