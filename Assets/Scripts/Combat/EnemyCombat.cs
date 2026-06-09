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
        [Tooltip("VESTIGIAL — no longer used for state timing. Telegraph/Attack now run until the real animation clip finishes (read at runtime), so the full clip always plays. Safe to ignore; left in place to avoid re-serialising every attack entry. Use attackSpeed to slow a clip down.")]
        public float telegraphDuration = 0.4f;
        [Tooltip("VESTIGIAL — see telegraphDuration. State length now comes from the actual clip; this value is not read.")]
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
        
        [Header("Hallucination Mechanic")]
        [Tooltip("Seconds the magic-mushroom hallucination runs when this attack lands. 0 = no hallucination. While active, HallucinationEffect.IsActive gates ALL of Yoru's outgoing damage to 0 (see EnemyHealth.TakeDamage).")]
        public float hallucinationDuration = 0f;
        [Tooltip("If true, trigger hallucination during TELEGRAPH phase instead of Attack. Use for HairLash_Telegraph mushroom effect.")]
        public bool hallucinationOnTelegraph = false;
        
        [Header("Pull Mechanic")]
        [Tooltip("If true, this attack drags Yoru toward the enemy while it plays (HairLash hair-grab). The pull is applied through PlayerMovement.ApplyExternalPull so it never fights normal locomotion.")]
        public bool pullsPlayer = false;
        [Tooltip("Yank speed in m/s while the pull is active. Higher = snappier. ~12 reads as a strong fast yank.")]
        public float pullSpeed = 12f;
        [Tooltip("Stop pulling once Yoru is within this planar distance of the enemy (melee range). Prevents overshoot/clipping.")]
        public float pullStopDistance = 1.5f;
        
        [Header("State Machine")]
        [Tooltip("Skip the Telegraph state and route Chase → Attack directly. Use when the attack clip is self-contained and should play full-length with no separate wind-up (HairLash_Telegraph IS the full pull animation, so it skips telegraph).")]
        public bool skipTelegraph = false;
        
        [Header("Phase")]
        public AttackPhase phase = AttackPhase.Both;
        [Tooltip("Selection weight — higher = more likely to be picked")]
        [Range(1, 100)]
        public int weight = 50;
    }
    #endregion
    
    #region Combo Definition
    [System.Serializable]
    public class EnemyAttackCombo
    {
        public string comboName = "Combo";
        
        [Tooltip("Ordered attackName references resolved against the Attacks array. Each plays its full-length animation back-to-back with no telegraph between them.")]
        public string[] attackNames;
        
        [Header("Phase")]
        public AttackPhase phase = AttackPhase.Both;
        [Tooltip("Selection weight among combos — higher = more likely to be picked when a combo is rolled")]
        [Range(1, 100)]
        public int weight = 50;
    }
    #endregion
    
    #region Serialized Fields
    [Header("Current State (Debug)")]
    [SerializeField] private EnemyState currentState = EnemyState.LostSoul;
    
    [Header("Detection")]
    [Tooltip("REALIZE distance (cone-gated): how close the player must be for an IDLE enemy to notice and engage. Inside this band (but outside pullRange) the enemy chases on foot; beyond it (up to escapeRange) it teleports to close the gap. This is NOT the pull range. See pullRange.")]
    [SerializeField] private float detectionRange = 9f;
    [SerializeField] private float attackRange = 3.5f;
    [Tooltip("PULL range: the enemy only yanks the player in (the pull/grab attack) when they are within this distance. Must be smaller than detectionRange. Between pullRange and detectionRange the enemy is realized but chases on foot instead of pulling, so the player has room to flee. Set 0 to disable pulling entirely.")]
    [SerializeField] private float pullRange = 6f;
    [Tooltip("Leash distance (player↔enemy). Once chasing, the enemy gives up and returns home only after the player stays beyond this for leashGraceDuration. Larger than detectionRange so a committed enemy chases further than it first noticed.")]
    [SerializeField] private float escapeRange = 15f;
    [Tooltip("Full vision-cone width in degrees (e.g. 120 = 60° each side of forward). Player must be within this cone AND within detectionRange to be seen. Only gates INITIAL detection and re-detection — once chasing, the enemy tracks without FOV check.")]
    [SerializeField, Range(30f, 360f)] private float visionAngle = 120f;
    
    [Header("Close Attack Grab")]
    [Tooltip("The attack whose animation is the special close-attack grab (matched by attackName, so it fires standalone OR as a combo step). This one attack gets: the swoop-down + forward lean, the yank-in, the camera roll-shake, and the Yoru freeze. Leave blank to disable all of it. Other attacks are untouched.")]
    [SerializeField] private string closeAttackName = "CloseStrike";
    [Tooltip("How far she drops DOWN into the grab, in world units from her floating height. She floats, so a value around 2 to 4 brings her body down toward Yoru. Increase it if she still hovers too high, decrease it if she sinks into the ground. Watch it live in Play mode.")]
    [SerializeField] private float grabDropAmount = 2f;
    [Tooltip("How far she leans forward (degrees about her side axis) into the grab. 35 is a strong lunge.")]
    [SerializeField] private float grabLeanAngle = 35f;
    [Tooltip("Seconds to smoothly swoop down + lean into the grab pose before the strike. Lower = snappier.")]
    [SerializeField] private float grabSwoopTime = 0.25f;
    [Tooltip("Seconds to smoothly rise back to her normal floating pose after the strike.")]
    [SerializeField] private float grabReturnTime = 0.3f;
    [Tooltip("How hard the grab yanks Yoru toward her during the swoop (world units/sec).")]
    [SerializeField] private float grabPullSpeed = 12f;
    [Tooltip("The grab stops yanking Yoru once he is this close, so he snaps into the grab without overshooting.")]
    [SerializeField] private float grabPullStopDistance = 1.5f;
    
    [Header("Timing")]
    [SerializeField] private float alertDuration = 0.5f;
    [SerializeField] private float recoveryDuration = 0.4f;
    [SerializeField] private float hitReactDuration = 0.5f;
    [Tooltip("Minimum seconds between hit-react flinches. A flinch CAN interrupt the enemy's own attack so it stays visible, but within this window further hits only flash — stops a fast player from stun-locking it out of every attack. Raise for a more relentless enemy, lower for more reactive.")]
    [SerializeField] private float hitReactCooldown = 1.0f;
    [SerializeField] private float staggerDuration = 1.0f;
    [SerializeField] private float attackCooldown = 3.0f;
    
    [Header("Movement")]
    [SerializeField] private float patrolSpeed = 1.5f;
    [SerializeField] private float chaseSpeed = 3.0f;
    [SerializeField] private float rotationSpeed = 5f;
    [Tooltip("Strafe speed when circling player during attack cooldown")]
    [SerializeField] private float strafeSpeed = 2.0f;
    
    [Header("Attacks")]
    [SerializeField] private EnemyAttack[] attacks;
    
    [Header("Combos")]
    [Tooltip("Chained attack sequences. Each step plays full-length with no telegraph between steps. Any attack can also fire alone — combos and singles are both rolled randomly per engagement.")]
    [SerializeField] private EnemyAttackCombo[] combos;
    [Tooltip("Chance (0-1) that an engagement opens with a combo instead of a single attack, in Phase 1")]
    [SerializeField, Range(0f, 1f)] private float comboChanceP1 = 0.4f;
    [Tooltip("Chance (0-1) that an engagement opens with a combo instead of a single attack, in Phase 2")]
    [SerializeField, Range(0f, 1f)] private float comboChanceP2 = 0.6f;
    
    [Header("Pull vs Run (mid-band)")]
    [Tooltip("In the pull band (attackRange → detectionRange), chance per action to YANK Yoru in with the pull attack instead of running to close the gap. Phase 1. The pull attack is whichever attack has pullsPlayer = true.")]
    [SerializeField, Range(0f, 1f)] private float pullChanceP1 = 0.5f;
    [Tooltip("Pull chance in the pull band during Phase 2 — more aggressive, since the pull is a damaging attack.")]
    [SerializeField, Range(0f, 1f)] private float pullChanceP2 = 0.7f;
    
    [Header("Phase System")]
    [SerializeField] private bool hasPhases = false;
    [Tooltip("HP percentage to trigger Phase 2 (0.5 = 50%)")]
    [SerializeField] private float phaseThreshold = 0.5f;
    [SerializeField] private float chaseSpeedP2 = 4.0f;
    [SerializeField] private float attackCooldownP2 = 2.0f;
    [SerializeField] private string bossBarName = "";
    
    [Header("Teleport")]
    [SerializeField] private bool canTeleport = true;
    [SerializeField] private float teleportDistance = 5f;
    [Tooltip("Playback speed for teleport animations")]
    [SerializeField] private float teleportSpeed = 1.0f;
    [SerializeField] private float teleportSpeedP2 = 1.4f;
    [Tooltip("Duration of Teleport_Out animation at 1x speed")]
    [SerializeField] private float teleportOutDuration = 0.5f;
    [Tooltip("Duration of Teleport_In animation at 1x speed")]
    [SerializeField] private float teleportInDuration = 0.4f;
    [Tooltip("Minimum seconds between teleports. Stops the enemy re-blinking every frame when the player kites in and out of the teleport band.")]
    [SerializeField] private float teleportCooldown = 1.5f;
    
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
    [Tooltip("Seconds before instantiated VFX is auto-destroyed. Set higher than your longest particle lifetime.")]
    [SerializeField] private float vfxLifetime = 3f;
    [Tooltip("Prefab spawned during telegraph wind-up. Drag prefab from Project window.")]
    [SerializeField] private GameObject telegraphVFX;
    [Tooltip("Prefab spawned on attack strike. Drag prefab from Project window.")]
    [SerializeField] private GameObject attackVFX;
    [Tooltip("Prefab spawned on stagger. Drag prefab from Project window.")]
    [SerializeField] private GameObject staggerVFX;
    [Tooltip("Prefab spawned on hit reaction. Drag prefab from Project window.")]
    [SerializeField] private GameObject hitReactVFX;
    [Tooltip("Prefab spawned at teleport-out position (old location). Drag prefab from Project window.")]
    [SerializeField] private GameObject teleportOutVFX;
    [Tooltip("Prefab spawned at teleport-in position (new location). Drag prefab from Project window.")]
    [SerializeField] private GameObject teleportInVFX;
    [Tooltip("Prefab spawned on death. Drag prefab from Project window.")]
    [SerializeField] private GameObject deathVFX;
    [Tooltip("Prefab spawned on alert/notice. Drag prefab from Project window.")]
    [SerializeField] private GameObject alertVFX;
    
    [Header("Damage")]
    [Tooltip("Damage at or above this threshold is treated as a heavy hit")]
    [SerializeField] private int heavyHitThreshold = 2;
    
    [Header("Disengage")]
    [Tooltip("When the enemy loses its target (player became Tomoe, or escaped beyond escapeRange), walk back to spawn position instead of idling in place. GDD Doc 07 universal rule — continue normal behaviour at home.")]
    [SerializeField] private bool returnToSpawnOnDisengage = true;
    [Tooltip("Player must stay beyond escapeRange (leash) for this many seconds before the enemy gives up and walks home. Prevents flickering between chase and return right at the boundary.")]
    [SerializeField] private float leashGraceDuration = 1.5f;
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
    private PlayerMovement playerMovement;
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
    
    // Combo — queued attackNames for the active sequence. Empty = single attack.
    private readonly System.Collections.Generic.Queue<EnemyAttack> comboQueue = new System.Collections.Generic.Queue<EnemyAttack>();
    private string activeComboName = "";
    
    // Alert (only triggers once per encounter)
    private bool hasAlerted;
    
    // Teleport
    private bool isTeleporting;
    
    // Teleport cooldown — gates distance-band teleports so the enemy can't re-blink every frame.
    private float teleportCooldownTimer;
    
    // Leash grace — accumulates while the player is beyond escapeRange; once it passes
    // leashGraceDuration the enemy gives up and returns home. Reset whenever the player is back in range.
    private float leashTimer;
    
    // Re-engage from Returning — when the enemy spots the player while walking home it runs
    // straight in (no pull, no teleport) for the first approach, then resumes normal behaviour
    // the moment it commits to an attack (flag cleared in SetState Telegraph/Attack).
    private bool forceRunReengage;
    
    // Pull-vs-run decision — made once per action cycle while in the pull band so the coin flip
    // doesn't re-roll every frame. Reset on every (re)entry to Chase.
    private bool pullDecisionMade;
    private bool pullDecisionResult; // true = pull (yank), false = run in
    
    // Hallucination fires exactly once per attack — on telegraph if flagged AND a telegraph runs,
    // otherwise on the attack. Tracking this decouples it from the hallucinationOnTelegraph flag,
    // so a skipped telegraph can't swallow the trigger.
    private bool hallucinationFiredThisAttack;
    
    // Hit-react rate limit — stops a fast player perma-flinching the enemy out of every attack.
    private float hitReactReadyTime;
    
    // Disengage — cached spawn position for return-to-spawn behaviour.
    private Vector3 spawnPosition;
    private Quaternion spawnRotation;
    private float returnStartTime;
    private bool returnWalkStarted;
    
    // Animation tracking — prevents CrossFade from restarting every frame
    private string currentPlayingAnim = "";
    
    // Missing animator states already logged — so a bad state name doesn't spam the console every frame.
    private readonly System.Collections.Generic.HashSet<string> missingStatesWarned = new System.Collections.Generic.HashSet<string>();
    
    // Clip-driven attack/telegraph transitions — the state runs until the real animation clip
    // reaches its end (full play, no early cut), with a runtime-derived safety net so it can never hang.
    private float attackStateEntryTime;
    private float cachedClipLength;
    private bool clipLengthRead;
    
    // Close-attack grab sequence (cinematic swoop + lean + yank-in + freeze, run as a coroutine
    // like the teleport). isGrabbing bypasses the normal Attack handler while it plays.
    private bool isGrabbing;
    private float grabOriginalBaseOffset;
    // 0..1 forward-lean blend, eased in/held/out by CloseGrabSequence and applied to the body in
    // LateUpdate (after the Animator) so the attack clip cannot stomp the tilt.
    private float grabLeanFactor;
    private PlayerHealth playerHealthTarget; // the PLAYER's health, for the capture freeze (ApplyStun)
    private PlayerCombat playerCombatTarget; // the PLAYER's combat, for the stance-aware grab reaction
    private const float AnimCompleteThreshold = 0.99f;   // normalizedTime at which a clip counts as fully played
    private const float AnimSafetyBuffer = 0.5f;         // extra seconds on top of clip length before the safety fires
    private const float AnimSafetyFallback = 5f;         // hard cap used only if the clip length can't be read at runtime
    private const float AnimSettleTime = 0.05f;          // ignore completion checks for this long so the new clip can start
    
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
        
        // Cache PlayerMovement for the HairLash pull. Pull is routed through its ApplyExternalPull
        // so there is only ever one Move per system (no fighting locomotion/gravity).
        playerMovement = player != null ? player.GetComponent<PlayerMovement>() : null;
        playerHealthTarget = player != null ? player.GetComponent<PlayerHealth>() : null;
        playerCombatTarget = player != null ? player.GetComponent<PlayerCombat>() : null;
        
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
        
        // Cache spawn pose so Returning state can walk the enemy home AND restore its original facing.
        spawnPosition = transform.position;
        spawnRotation = transform.rotation;
        
        SetState(EnemyState.LostSoul);

        // Log animator settings at startup to help diagnose freeze issues
        if (showDebugLogs && animator != null)
        {
            Debug.Log($"[{gameObject.name}] ANIMATOR SETUP: " +
                $"cullingMode={animator.cullingMode} " +
                $"updateMode={animator.updateMode} " +
                $"applyRootMotion={animator.applyRootMotion} " +
                $"speed={animator.speed}");
        }

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
        
        // Teleport cooldown tick
        if (teleportCooldownTimer > 0)
            teleportCooldownTimer -= Time.deltaTime;
        
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
            case EnemyState.Alert: HandleAlert();if (!string.IsNullOrEmpty(bossBarName) && BossHealthBarUI.Instance != null && enemyHealth != null)
    BossHealthBarUI.Instance.Show(enemyHealth, bossBarName); break;
            case EnemyState.Chase: HandleChase(); break;
            case EnemyState.Telegraph: HandleTelegraph(); break;
            case EnemyState.Attack: HandleAttack(); break;
            case EnemyState.Recovery: HandleRecovery(); break;
            case EnemyState.HitReact: HandleHitReact(); break;
            case EnemyState.Stagger: HandleStagger(); break;
            case EnemyState.Teleport: break; // Handled by coroutine
            case EnemyState.Dead: HandleDead(); break;
        }
    }

    /// <summary>
    /// Applies the grab's facing and forward lean AFTER the Animator has written its pose, so the
    /// attack clip cannot stomp the tilt. Active only while a grab is in progress; the blend amount
    /// (grabLeanFactor) is eased in, held, and out by CloseGrabSequence.
    /// </summary>
    private void LateUpdate()
    {
        if (!isGrabbing || player == null) return;

        Vector3 flatDir = player.position - transform.position;
        flatDir.y = 0f;
        if (flatDir.sqrMagnitude <= 0.0001f) return;

        Quaternion face = Quaternion.LookRotation(flatDir.normalized);
        transform.rotation = face * Quaternion.Euler(grabLeanAngle * grabLeanFactor, 0f, 0f);
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
        //    Re-engaging from a return always runs straight in (no pull, no teleport) until the
        //    first attack lands — forceRunReengage carries that intent into HandleChase.
        if (!PlayerIsTomoe() && PlayerInVision())
        {
            forceRunReengage = true;
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
            // 4. SETTLING — arrived at spawn, but rotation may be off. Stop nav, play idle,
            //    slerp toward the cached spawnRotation. Only transition to Idle once aligned.
            //    Runs every frame after arrival until alignment threshold is met.
            if (navAgent != null && navAgent.isOnNavMesh && !navAgent.isStopped)
            {
                DebugLog("Arrived at spawn — settling to original facing");
                navAgent.isStopped = true;
                navAgent.velocity = Vector3.zero;
                PlayAnimation(idleAnim);

                // Reached home — instant full heal. The player loses all the chip damage they
                // dealt before letting the enemy leash, so re-engaging starts from a full bar.
                // Fires once (this block only runs on the frame nav first stops).
                if (enemyHealth != null)
                    enemyHealth.ResetHealth();
            }
            
            transform.rotation = Quaternion.Slerp(transform.rotation, spawnRotation,
                Time.deltaTime * rotationSpeed);
            
            if (Quaternion.Angle(transform.rotation, spawnRotation) < 1f)
            {
                // Snap to exact spawn rotation so the enemy locks to its original pose.
                transform.rotation = spawnRotation;
                SetState(EnemyState.Idle);
            }
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

        // ── LEASH (with grace) ────────────────────────────────────────────────
        // Leash is the raw player↔enemy distance. The enemy only gives up once the player
        // has stayed beyond escapeRange for leashGraceDuration straight — a brief overshoot
        // past the edge won't break the chase.
        if (dist > escapeRange)
        {
            leashTimer += Time.deltaTime;
            if (leashTimer >= leashGraceDuration)
            {
                leashTimer = 0f;
                DebugLog($"Leash exceeded ({dist:F1}m > {escapeRange}m for {leashGraceDuration}s) — returning home");
                SetState(returnToSpawnOnDisengage ? EnemyState.Returning : EnemyState.Idle);
                return;
            }
            // Still inside the grace window — keep pursuing (falls through to approach below).
        }
        else
        {
            leashTimer = 0f;
        }

        // ── MELEE BAND (≤ attackRange) ────────────────────────────────────────
        // Attack when ready; otherwise circle the player. This is the ONLY place the walk
        // animation is used (slow circling reads fine up close; walking to close a gap looks wrong).
        if (dist <= attackRange)
        {
            if (cooldownTimer <= 0)
            {
                EnemyAttack chosen = ChooseAttackOrCombo();
                if (chosen != null)
                {
                    currentAttack = chosen;
                    // skipTelegraph (or no telegraph clip) routes straight to Attack so the self-contained clip plays full-length.
                    SetState(ShouldSkipTelegraph(currentAttack) ? EnemyState.Attack : EnemyState.Telegraph);
                    return;
                }
            }

            // On cooldown — strafe-circle around the player.
            LookAtPlayer();
            if (navAgent != null && navAgent.isOnNavMesh)
            {
                Vector3 strafeDir = Vector3.Cross(Vector3.up, (player.position - transform.position).normalized);
                Vector3 strafeTarget = transform.position + strafeDir * 2f;
                navAgent.isStopped = false;
                navAgent.speed = strafeSpeed;
                navAgent.SetDestination(strafeTarget);
            }
            PlayAnimation(walkAnim);
            return;
        }

        // ── TELEPORT BAND (detectionRange < dist ≤ escapeRange) ───────────────
        // Player is too far to chase on foot — blink to close the gap. Suppressed while
        // re-engaging from a return (the enemy runs in instead) and while on teleport cooldown.
        if (dist > detectionRange && dist <= escapeRange)
        {
            if (canTeleport && !isTeleporting && !forceRunReengage && teleportCooldownTimer <= 0f)
            {
                DebugLog($"Teleport band ({dist:F1}m) — blinking to close the gap");
                StartCoroutine(TeleportSequence());
                return;
            }
            // Can't teleport right now (cooldown / re-engage / disabled) — run in (falls through).
        }

        // ── PULL BAND (attackRange < dist ≤ detectionRange) ───────────────────
        // Once ready to act, roll pull-vs-run a single time (sticky for the cycle so it doesn't
        // re-roll every frame). Pull = yank Yoru into melee with a damaging grab; run = close on foot.
        // The pull is gated by pullRange (< detectionRange): only when the player is within pullRange
        // can a pull be rolled; between pullRange and detectionRange the enemy is realized but always
        // runs in, so the player can still escape instead of being yanked back from far away.
        // Re-engage from a return always runs in (no pull) until the first attack lands.
        if (dist > attackRange && dist <= detectionRange)
        {
            if (cooldownTimer <= 0 && !forceRunReengage)
            {
                if (!pullDecisionMade)
                {
                    pullDecisionMade = true;
                    float pullChance = isPhase2 ? pullChanceP2 : pullChanceP1;
                    // Pull only if the player is close enough (within pullRange) AND the roll says pull.
                    pullDecisionResult = (dist <= pullRange) && (Random.value < pullChance);
                    DebugLog($"Pull-band decision: {(pullDecisionResult ? "PULL" : "RUN-IN")} (dist {dist:F1}m, pullRange {pullRange}m, chance {pullChance:F2})");
                }

                if (pullDecisionResult)
                {
                    EnemyAttack pull = FindPullAttack();
                    if (pull != null)
                    {
                        currentAttack = pull;
                        SetState(ShouldSkipTelegraph(pull) ? EnemyState.Attack : EnemyState.Telegraph);
                        return;
                    }
                    // No pull attack defined — fall through to run in.
                }
            }
            // Decided to run, cooldown not ready, or re-engaging — fall through to approach.
        }

        // ── APPROACH ──────────────────────────────────────────────────────────
        // Run toward the player. Covers: pull-band run-in, teleport-band run (when blink is
        // unavailable), grace-window chase, and re-engage-from-return. Always the run animation —
        // walking is reserved for melee circling.
        LookAtPlayer();
        float chaseSpd = isPhase2 ? chaseSpeedP2 : chaseSpeed;
        if (navAgent != null && navAgent.isOnNavMesh)
        {
            navAgent.isStopped = false;
            navAgent.speed = chaseSpd;
            navAgent.SetDestination(player.position);
        }
        PlayAnimation(runAnim);
    }
    private void HandleTelegraph()
    {
        StopNav();
        LookAtPlayer();

        // Run until the telegraph clip has actually finished (full play, no early cut).
        if (AttackAnimationComplete())
            SetState(EnemyState.Attack);
    }
    
    private void HandleAttack()
    {
        // The close-attack grab runs as its own coroutine (swoop, strike, return); don't let the
        // normal per-frame attack handler interfere while it plays.
        if (isGrabbing) return;

        StopNav();

        // Face player during attack — especially important for skipTelegraph attacks that
        // didn't go through Telegraph state where LookAtPlayer is normally called.
        LookAtPlayer();

        // HairLash pull — while a pulling attack plays, drag Yoru toward the enemy via
        // PlayerMovement (single-Move owner). Stops at pullStopDistance so it snaps to melee
        // range without overshoot.
        if (currentAttack != null && currentAttack.pullsPlayer && playerMovement != null && player != null)
        {
            Vector3 toEnemy = transform.position - player.position;
            toEnemy.y = 0f;
            float dist = toEnemy.magnitude;
            if (dist > currentAttack.pullStopDistance)
                playerMovement.ApplyExternalPull(toEnemy.normalized * currentAttack.pullSpeed, Time.deltaTime * 2f);
        }

        // Run until the attack clip has actually finished, then resolve damage on the strike.
        if (AttackAnimationComplete())
        {
            DealDamageToPlayer();
            SetState(EnemyState.Recovery);
        }
    }
    
    private void HandleRecovery()
    {
        StopNav();
        
        if (stateTimer <= 0)
        {
            // Combo chaining — if a sequence is queued, jump straight to the next attack.
            // Skips Chase + Telegraph so the chained clips play back-to-back, full-length.
            if (comboQueue.Count > 0)
            {
                currentAttack = comboQueue.Dequeue();
                LookAtPlayer();
                SetState(ShouldSkipTelegraph(currentAttack) ? EnemyState.Attack : EnemyState.Telegraph);
                return;
            }
            
            // Sequence finished (or was a single attack) — clear combo label.
            activeComboName = "";
            
            // Back to Chase — the distance bands decide what happens next (attack / pull / teleport / leash).
            // Teleport is no longer rolled randomly here; it only fires when the player is in the teleport band.
            cooldownTimer = isPhase2 ? attackCooldownP2 : attackCooldown;
            SetState(EnemyState.Chase);
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
                    // Committing to an attack — clear the re-engage run flag so normal behaviour
                    // resumes after this strike, and snap to face the player so the wind-up aims true.
                    forceRunReengage = false;
                    hallucinationFiredThisAttack = false; // fresh attack
                    FacePlayerInstant();

                    float speed = currentAttack.telegraphSpeed;
                    // Play from frame 0 (instant, not blended) so the full clip plays start-to-end
                    // and its real length is immediately readable for the clip-driven transition.
                    BeginAttackClip(currentAttack.telegraphAnim, speed);
                    PlayVFX(telegraphVFX);
                    DebugLog($"Telegraph: {currentAttack.attackName}");

                    // Hallucination — fire here only if explicitly flagged for the telegraph phase.
                    // Otherwise it fires when the Attack state begins (see below). Tracked so it
                    // can never fire twice.
                    if (currentAttack.hallucinationOnTelegraph && currentAttack.hallucinationDuration > 0f
                        && !hallucinationFiredThisAttack && HallucinationEffect.Instance != null)
                    {
                        HallucinationEffect.Instance.Trigger(currentAttack.hallucinationDuration);
                        hallucinationFiredThisAttack = true;
                        DebugLog($"Hallucination triggered on telegraph: {currentAttack.hallucinationDuration}s");
                    }
                }
                break;
                
            case EnemyState.Attack:
                if (currentAttack != null)
                {
                    // Committing to an attack — clear the re-engage run flag and snap to face the
                    // player so the strike (and the hair-grab visual) aims where Yoru actually is.
                    forceRunReengage = false;
                    // If we did NOT come through Telegraph, this is a fresh attack (skipped
                    // telegraph), so reset the fired flag so the hallucination still fires here.
                    if (oldState != EnemyState.Telegraph)
                        hallucinationFiredThisAttack = false;

                    // Close attack becomes the cinematic grab (swoop + lean + yank-in + freeze +
                    // camera roll + return), run as its own coroutine. It owns the entire strike,
                    // so skip the normal attack setup below.
                    if (!string.IsNullOrEmpty(closeAttackName)
                        && currentAttack.attackName == closeAttackName && !isGrabbing)
                    {
                        StartCoroutine(CloseGrabSequence());
                        break;
                    }

                    FacePlayerInstant();

                    float speed = currentAttack.attackSpeed;

                    // Some attacks have no attack anim (scream type — telegraph IS the attack).
                    if (!string.IsNullOrEmpty(currentAttack.attackAnim))
                    {
                        // Play from frame 0 (instant) for clean full-length playback and readable length.
                        BeginAttackClip(currentAttack.attackAnim, speed);
                    }
                    else
                    {
                        // No clip — fall back to a fixed safety window so the state can't hang.
                        attackStateEntryTime = Time.time;
                        clipLengthRead = false;
                        cachedClipLength = 0f;
                    }
                    
                    PlayVFX(attackVFX);
                    DebugLog($"Attack: {currentAttack.attackName} ({currentAttack.damage} dmg)");
                    
                    // Magic-mushroom hallucination — fires the standalone post-process effect and
                    // raises HallucinationEffect.IsActive, which gates Yoru's outgoing damage to 0
                    // for its duration (gate lives in EnemyHealth.TakeDamage). No player code touched.
                    // Fires here unless it already fired during a telegraph this attack — so it works
                    // whether the attack telegraphs or skips straight in.
                    if (currentAttack.hallucinationDuration > 0f && !hallucinationFiredThisAttack
                        && HallucinationEffect.Instance != null)
                    {
                        HallucinationEffect.Instance.Trigger(currentAttack.hallucinationDuration);
                        hallucinationFiredThisAttack = true;
                        DebugLog($"Hallucination triggered: {currentAttack.hallucinationDuration}s");
                    }
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
                comboQueue.Clear(); // Interruption — drop any remaining combo steps
                activeComboName = "";
                PlayVFX(staggerVFX);
                DebugLog($"STAGGERED for {staggerDuration}s");
                break;
                
            case EnemyState.Dead:
                StopNav();
                EndGrab();
                PlayAnimation(deathAnim);
                SetAnimSpeed(1f);
                PlayVFX(deathVFX);
                break;
                
            case EnemyState.Chase:
                // Fresh (re)entry to Chase — reset the per-cycle pull decision and the leash grace.
                // (During an uninterrupted run-in the enemy stays in Chase, so this never fires mid-run.)
                pullDecisionMade = false;
                leashTimer = 0f;
                SetAnimSpeed(1f);
                break;
                
            case EnemyState.Idle:
                StopNav();
                forceRunReengage = false;
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
        
        // A pull at melee range is pointless (the player is already here) and it crowds out the
        // real melee attacks, which is why CloseStrike rarely showed. Exclude pulls when in close —
        // the pull is the pull band's gap-closer, not a melee move.
        bool atMelee = DistanceToPlayer() <= attackRange;
        
        // Build list of valid attacks for current phase
        int totalWeight = 0;
        
        for (int i = 0; i < attacks.Length; i++)
        {
            if (atMelee && attacks[i].pullsPlayer) continue;
            if (IsAttackValid(attacks[i]))
                totalWeight += attacks[i].weight;
        }
        
        if (totalWeight == 0) return null;
        
        // Weighted random selection
        int roll = Random.Range(0, totalWeight);
        int running = 0;
        
        for (int i = 0; i < attacks.Length; i++)
        {
            if (atMelee && attacks[i].pullsPlayer) continue;
            if (!IsAttackValid(attacks[i])) continue;
            
            running += attacks[i].weight;
            if (roll < running)
                return attacks[i];
        }
        
        return null; // No valid non-pull attack at this range
    }
    
    private bool IsAttackValid(EnemyAttack atk)
    {
        if (atk.phase == AttackPhase.Phase1Only && isPhase2) return false;
        if (atk.phase == AttackPhase.Phase2Only && !isPhase2) return false;
        return true;
    }
    
    /// <summary>
    /// True when an attack should bypass the Telegraph state and go straight to Attack. Honours the
    /// skipTelegraph flag, but ALSO treats a missing telegraphAnim as skip — otherwise the Telegraph
    /// state sits on the previous clip (e.g. Float_Idle) for the whole wind-up, which reads as the
    /// enemy freezing/floating. The HairLash clips are self-contained, so the Nopperabō always skips.
    /// </summary>
    private bool ShouldSkipTelegraph(EnemyAttack atk)
    {
        if (atk == null) return true;
        return atk.skipTelegraph || string.IsNullOrEmpty(atk.telegraphAnim);
    }
    
    /// <summary>
    /// Top-level selection for an engagement. Rolls combo-vs-single by phase chance; on a combo
    /// roll, queues the remaining steps and returns the first attack. Falls back to a single
    /// attack if no valid combo exists. Singles and combos are both fully random.
    /// </summary>
    private EnemyAttackCombo ChooseCombo()
    {
        if (combos == null || combos.Length == 0) return null;
        
        int totalWeight = 0;
        for (int i = 0; i < combos.Length; i++)
        {
            if (IsComboValid(combos[i]))
                totalWeight += combos[i].weight;
        }
        
        if (totalWeight == 0) return null;
        
        int roll = Random.Range(0, totalWeight);
        int running = 0;
        for (int i = 0; i < combos.Length; i++)
        {
            if (!IsComboValid(combos[i])) continue;
            
            running += combos[i].weight;
            if (roll < running)
                return combos[i];
        }
        
        return null;
    }
    
    private bool IsComboValid(EnemyAttackCombo combo)
    {
        if (combo.attackNames == null || combo.attackNames.Length == 0) return false;
        if (combo.phase == AttackPhase.Phase1Only && isPhase2) return false;
        if (combo.phase == AttackPhase.Phase2Only && !isPhase2) return false;
        return true;
    }
    
    /// <summary>
    /// Rolls whether this engagement is a combo (by phase chance) or a single attack.
    /// On a combo, queues steps 2..n and returns step 1. On a single, returns a weighted
    /// random attack. Clears any stale queue first so interruptions never leak into a new engagement.
    /// </summary>
    private EnemyAttack ChooseAttackOrCombo()
    {
        comboQueue.Clear();
        activeComboName = "";
        
        float comboChance = isPhase2 ? comboChanceP2 : comboChanceP1;
        
        if (Random.value < comboChance)
        {
            EnemyAttackCombo combo = ChooseCombo();
            if (combo != null && QueueComboSequence(combo))
            {
                // At melee range, drop any leading pull step(s) — the player is already close, so
                // the combo should open on its first real strike instead of a pointless yank.
                // Keep at least one step so the combo still fires.
                bool atMelee = DistanceToPlayer() <= attackRange;
                while (atMelee && comboQueue.Count > 1 && comboQueue.Peek().pullsPlayer)
                    comboQueue.Dequeue();

                // First step dequeued and returned; the rest stay queued for HandleRecovery.
                EnemyAttack first = comboQueue.Dequeue();
                DebugLog($"Combo started: {combo.comboName} ({comboQueue.Count + 1} steps remaining)");
                return first;
            }
        }
        
        // Single attack fallback (may be null at melee if only pulls are valid — caller then circles).
        return ChooseAttack();
    }
    
    /// <summary>
    /// Resolves a combo's attackName references into the comboQueue. Returns false (and leaves
    /// the queue empty) if any name fails to resolve, so a misconfigured combo degrades to a single.
    /// </summary>
    private bool QueueComboSequence(EnemyAttackCombo combo)
    {
        comboQueue.Clear();
        
        for (int i = 0; i < combo.attackNames.Length; i++)
        {
            EnemyAttack atk = FindAttackByName(combo.attackNames[i]);
            if (atk == null)
            {
                Debug.LogWarning($"{gameObject.name}: combo '{combo.comboName}' references unknown attack '{combo.attackNames[i]}' — falling back to single attack.");
                comboQueue.Clear();
                return false;
            }
            comboQueue.Enqueue(atk);
        }
        
        activeComboName = combo.comboName;
        return comboQueue.Count > 0;
    }
    
    private EnemyAttack FindAttackByName(string name)
    {
        if (attacks == null || string.IsNullOrEmpty(name)) return null;
        for (int i = 0; i < attacks.Length; i++)
        {
            if (attacks[i].attackName == name)
                return attacks[i];
        }
        return null;
    }
    
    /// <summary>
    /// Returns the first attack flagged pullsPlayer (the HairLash hair-grab). Used by the pull
    /// band so the gap-closer is data-driven — no hardcoded attack name. Returns null if the
    /// enemy has no pulling attack (in which case the pull band just runs in).
    /// </summary>
    private EnemyAttack FindPullAttack()
    {
        if (attacks == null) return null;
        for (int i = 0; i < attacks.Length; i++)
        {
            if (attacks[i].pullsPlayer && IsAttackValid(attacks[i]))
                return attacks[i];
        }
        return null;
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
        
        // After teleport, set attack cooldown and a teleport cooldown (so the enemy can't
        // immediately re-blink), then chase. It lands ~teleportDistance behind the player,
        // which is inside the pull band, so the next action is a pull/run rather than another blink.
        float cd = isPhase2 ? attackCooldownP2 : attackCooldown;
        cooldownTimer = cd * 0.5f; // Shorter cooldown after teleport — keeps pressure on
        teleportCooldownTimer = teleportCooldown;
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

    /// <summary>
    /// Cinematic close-attack grab. Swoops down to Yoru's level (Inspector-tunable offset) and
    /// leans forward into a grab pose while yanking Yoru in, freezes him completely for the strike
    /// (with the camera roll), then rises back to the normal floating pose and resumes combat.
    /// Runs as a coroutine like the teleport; isGrabbing bypasses the normal Attack handler.
    /// Height is tied to Yoru's position, so it holds up anywhere on the map; the offset keeps
    /// her from sitting underground.
    /// </summary>
    private IEnumerator CloseGrabSequence()
    {
        isGrabbing = true;
        grabOriginalBaseOffset = navAgent != null ? navAgent.baseOffset : 0f;

        // She plants and grabs, she doesn't drift, so hold position for the whole sequence.
        StopNav();
        FacePlayerInstant();

        // Freeze Yoru immediately so he can't act during the capture (refreshed through the swoop).
        if (playerHealthTarget != null) playerHealthTarget.ApplyStun(grabSwoopTime + 0.2f);

        // Kick off the stance-aware grab reaction on Yoru. It plays to its freeze frame and holds
        // there through the strike; ResumeGrabReaction below lets it finish when the strike is over.
        if (playerCombatTarget != null) playerCombatTarget.PlayGrabReaction();

        // Phase 1: swoop down + lean in, yanking Yoru toward her.
        float t = 0f;
        while (t < grabSwoopTime)
        {
            t += Time.deltaTime;
            float k = Mathf.SmoothStep(0f, 1f, grabSwoopTime > 0f ? t / grabSwoopTime : 1f);

            // Dip: drop her by lowering the agent's vertical offset, so the root (and the floating
            // mesh above it) descends into the grab. A fixed drop you tune for the arena.
            if (navAgent != null)
            {
                float targetOffset = grabOriginalBaseOffset - grabDropAmount;
                navAgent.baseOffset = Mathf.Lerp(grabOriginalBaseOffset, targetOffset, k);
            }

            // Lean: ease the forward tilt in. The rotation itself (face Yoru + pitch) is applied in
            // LateUpdate so the Animator cannot stomp it; here we only drive the blend amount.
            grabLeanFactor = k;

            // Reel Yoru all the way in during the swoop so he is in range when the strike lands.
            // The speed auto-scales to close the remaining gap by the end of the swoop (never below
            // grabPullSpeed), routed through PlayerMovement's single-Move pull. This is the yank,
            // and it happens HERE at the start, not at the strike.
            if (playerMovement != null && player != null)
            {
                Vector3 toEnemy = transform.position - player.position;
                toEnemy.y = 0f;
                float gap = toEnemy.magnitude - grabPullStopDistance;
                if (gap > 0.01f)
                {
                    float timeLeft = Mathf.Max(0.02f, grabSwoopTime - t);
                    float reelSpeed = Mathf.Max(grabPullSpeed, gap / timeLeft);
                    playerMovement.ApplyExternalPull(toEnemy.normalized * reelSpeed, Time.deltaTime * 2f);
                }
            }

            if (playerHealthTarget != null) playerHealthTarget.ApplyStun(0.2f); // hold the freeze
            if (!isGrabbing) yield break; // cut short (stagger or death); EndGrab handled the restore
            yield return null;
        }

        // Hold the full forward lean through the strike (LateUpdate keeps applying it).
        grabLeanFactor = 1f;

        // Phase 2: the strike. Clip plays from frame 0; the camera roll + freeze span its length.
        PlayVFX(attackVFX);
        if (currentAttack != null && !string.IsNullOrEmpty(currentAttack.attackAnim))
        {
            BeginAttackClip(currentAttack.attackAnim, currentAttack.attackSpeed);
        }
        else
        {
            attackStateEntryTime = Time.time;
            clipLengthRead = false;
            cachedClipLength = 0f;
        }

        bool rollFired = false;
        while (!AttackAnimationComplete())
        {
            // The moment the real clip length is known, fire the camera roll for exactly that long
            // and extend the freeze to cover the whole strike.
            if (!rollFired && clipLengthRead)
            {
                float strikeDuration = cachedClipLength / Mathf.Max(0.01f, currentAttack != null ? currentAttack.attackSpeed : 1f);
                if (CameraGameFeel.Instance != null) CameraGameFeel.Instance.RollShake(strikeDuration);
                if (playerHealthTarget != null) playerHealthTarget.ApplyStun(strikeDuration + 0.1f);
                rollFired = true;
            }
            if (!isGrabbing) yield break;
            yield return null;
        }

        // Strike connects. Deal damage inline with a FLAT (horizontal) range check: the dip lowers
        // the root, so the shared 3D distance check would read as out-of-range during the grab.
        // feedbackOnly = true: HP plus the impact feedback (hit VFX, sound, screen) still fire, but
        // the normal hit reaction animation and the pull are skipped, since the held grab reaction
        // is the visible reaction. Vector3.zero also means no pull.
        if (currentAttack != null && player != null && playerHealthTarget != null)
        {
            Vector3 flat = transform.position - player.position;
            flat.y = 0f;
            if (flat.magnitude <= currentAttack.range)
            {
                bool isHeavy = currentAttack.damage >= heavyHitThreshold;
                playerHealthTarget.TakeDamage(currentAttack.damage, isHeavy, Vector3.zero, true);
            }
        }

        // Strike is over: release the held grab reaction so it plays out to its end as Yoru recovers.
        if (playerCombatTarget != null) playerCombatTarget.ResumeGrabReaction();

        // Phase 3: rise back to the normal floating pose, upright.
        float dipOffset = navAgent != null ? navAgent.baseOffset : grabOriginalBaseOffset;
        float r = 0f;
        while (r < grabReturnTime)
        {
            r += Time.deltaTime;
            float k = Mathf.SmoothStep(0f, 1f, grabReturnTime > 0f ? r / grabReturnTime : 1f);

            if (navAgent != null)
                navAgent.baseOffset = Mathf.Lerp(dipOffset, grabOriginalBaseOffset, k);

            // Ease the lean back out; LateUpdate keeps her facing Yoru while the pitch unwinds.
            grabLeanFactor = 1f - k;

            if (!isGrabbing) yield break;
            yield return null;
        }

        if (navAgent != null) navAgent.baseOffset = grabOriginalBaseOffset;
        grabLeanFactor = 0f;
        isGrabbing = false;

        // Cooldown, then resume the normal loop.
        cooldownTimer = isPhase2 ? attackCooldownP2 : attackCooldown;
        SetState(EnemyState.Recovery);
    }

    /// <summary>
    /// Safety restore if the grab is cut short (stagger/death). Puts the agent's vertical offset
    /// back, snaps her upright (keeping her facing), and clears the grab flag. Yoru's freeze is
    /// time-boxed in PlayerHealth, so it releases on its own.
    /// </summary>
    private void EndGrab()
    {
        if (!isGrabbing) return;
        isGrabbing = false;
        grabLeanFactor = 0f;
        if (playerCombatTarget != null) playerCombatTarget.CancelGrabReaction();
        if (navAgent != null) navAgent.baseOffset = grabOriginalBaseOffset;
        Vector3 e = transform.eulerAngles;
        transform.rotation = Quaternion.Euler(0f, e.y, 0f);
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
        EndGrab();
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
        EndGrab();
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

    // Which states a flinch may interrupt. Attack/Telegraph are now included so small/medium hits
    // are actually VISIBLE during the enemy's own offence (previously they only flashed). Teleport
    // and narrative states (LostSoul/Dialogue/Peaceful) are not interrupted.
    bool interruptible = currentState == EnemyState.Idle
        || currentState == EnemyState.Chase
        || currentState == EnemyState.Recovery
        || currentState == EnemyState.Telegraph
        || currentState == EnemyState.Attack;

    if (!interruptible)
    {
        TriggerHitFlash();
        return;
    }

    // Rate limit — a flinch interrupts, then for hitReactCooldown seconds further hits only flash,
    // so the enemy gets to resume attacking instead of being stun-locked. Big hits use TriggerStagger
    // (no cooldown), so heavy damage always staggers.
    if (Time.time < hitReactReadyTime)
    {
        TriggerHitFlash();
        return;
    }

    hitReactReadyTime = Time.time + hitReactCooldown;
    SetState(EnemyState.HitReact);
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
        EndGrab();
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
        EndGrab();
        isPhase2 = false;
        hasAlerted = false;
        cooldownTimer = 0;
        stateTimer = 0;
        teleportCooldownTimer = 0;
        leashTimer = 0;
        forceRunReengage = false;
        pullDecisionMade = false;
        hallucinationFiredThisAttack = false;
        hitReactReadyTime = 0f;
        clipLengthRead = false;
        cachedClipLength = 0f;
        currentAttack = null;
        comboQueue.Clear();
        activeComboName = "";
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
    
    /// <summary>
    /// Instantly snaps to face the player (no slerp). Used at the START of an attack so the
    /// strike — and the HairLash hair-grab visual in particular — aims where Yoru actually is.
    /// The slow LookAtPlayer slerp can't catch a moving target before a short clip ends, which
    /// is what made the pull look like it had "horrible aim".
    /// </summary>
    private void FacePlayerInstant()
    {
        if (player == null) return;
        
        Vector3 dir = player.position - transform.position;
        dir.y = 0f;
        
        if (dir.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.LookRotation(dir);
    }
    
    private float DistanceToPlayer()
    {
        if (player == null) return float.MaxValue;
        return Vector3.Distance(transform.position, player.position);
    }
    #endregion
    
    #region Animation Helpers
    private void PlayAnimation(string stateName, bool forceRestart = false)
    {
        if (animator == null || string.IsNullOrEmpty(stateName))
        {
            if (showDebugLogs && string.IsNullOrEmpty(stateName))
                Debug.LogWarning($"[{gameObject.name}] PlayAnimation called with EMPTY state name!");
            return;
        }
        if (!forceRestart && stateName == currentPlayingAnim) return;

        // Check if state exists in animator
        int stateHash = Animator.StringToHash(stateName);
        bool stateExists = animator.HasState(combatLayerIndex, stateHash);

        if (!stateExists)
        {
            // Log each missing state name only once per enemy — a misconfigured anim name (e.g. an
            // idle played every frame in LostSoul) would otherwise flood the console every frame.
            if (missingStatesWarned.Add(stateName))
                Debug.LogError($"[{gameObject.name}] ANIMATION STATE NOT FOUND: '{stateName}' on layer {combatLayerIndex}! Check this enemy's animator controller / state-name fields.");
            return;
        }

        currentPlayingAnim = stateName;
        animator.CrossFadeInFixedTime(stateName, 0.1f, combatLayerIndex);

        if (showDebugLogs)
            DebugLog($"Playing animation: {stateName}");
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

    /// <summary>
    /// Begins an attack/telegraph clip from frame 0 (instant Play, not blended) and arms the
    /// clip-driven transition. Playing from frame 0 guarantees the full clip plays start-to-end
    /// with no blended-away wind-up, and makes the clip's real length readable on the next frame.
    /// </summary>
    private void BeginAttackClip(string stateName, float speed)
    {
        SetAnimSpeed(speed);
        
        attackStateEntryTime = Time.time;
        clipLengthRead = false;
        cachedClipLength = 0f;
        
        if (animator == null || string.IsNullOrEmpty(stateName)) return;
        
        int stateHash = Animator.StringToHash(stateName);
        if (!animator.HasState(combatLayerIndex, stateHash))
        {
            Debug.LogError($"[{gameObject.name}] ANIMATION STATE NOT FOUND: '{stateName}' on layer {combatLayerIndex}! Check animator controller.");
            return;
        }
        
        currentPlayingAnim = stateName;
        animator.Play(stateName, combatLayerIndex, 0f);
    }
    
    /// <summary>
    /// Drives Telegraph/Attack completion. Returns true once the current clip has played to its
    /// end (full play — no early cut from a hand-set duration), with a runtime-derived safety so
    /// the state can never hang if the clip length can't be read. The clip's real length is read
    /// at runtime, so timing is correct regardless of the (now-informational) per-attack durations.
    /// </summary>
    private bool AttackAnimationComplete()
    {
        if (animator == null) return true; // no animator — don't hang the state machine

        float elapsed = Time.time - attackStateEntryTime;

        // Read the real clip length once the instant Play has settled (one frame in).
        if (!clipLengthRead && elapsed > AnimSettleTime)
        {
            AnimatorStateInfo s = animator.GetCurrentAnimatorStateInfo(combatLayerIndex);
            if (s.length > 0.01f)
            {
                cachedClipLength = s.length;
                clipLengthRead = true;
            }
        }

        // Primary completion — the clip reached its end. normalizedTime accounts for playback
        // speed and, for a non-looping clip, caps at 1 and holds, so this latches true.
        if (elapsed > AnimSettleTime)
        {
            AnimatorStateInfo s = animator.GetCurrentAnimatorStateInfo(combatLayerIndex);
            if (s.normalizedTime >= AnimCompleteThreshold)
                return true;
        }

        // Safety net — derived from the real clip length / speed when known, else a fixed cap.
        float speed = currentState == EnemyState.Telegraph
            ? Mathf.Max(0.1f, currentAttack != null ? currentAttack.telegraphSpeed : 1f)
            : Mathf.Max(0.1f, currentAttack != null ? currentAttack.attackSpeed : 1f);
        float safetyTime = clipLengthRead ? (cachedClipLength / speed) + AnimSafetyBuffer : AnimSafetyFallback;

        if (elapsed > safetyTime)
        {
            Debug.LogWarning($"[{gameObject.name}] Attack/Telegraph safety transition fired (elapsed {elapsed:F2}s > {safetyTime:F2}s) — clip '{currentPlayingAnim}' may not have completed cleanly.");
            return true;
        }

        return false;
    }
    
    /// <summary>
    /// Instantiates a VFX prefab at the enemy's current position and rotation, plays its
    /// ParticleSystem, and schedules auto-destroy. Mirrors YoruVFXManager.SpawnEffect.
    /// Null-safe — silently no-ops if the prefab slot is unassigned.
    /// </summary>
    private void PlayVFX(GameObject vfxPrefab)
    {
        if (vfxPrefab == null) return;
        
        GameObject effect = Instantiate(vfxPrefab, transform.position, transform.rotation);
        
        ParticleSystem ps = effect.GetComponent<ParticleSystem>();
        if (ps != null && !ps.isPlaying)
            ps.Play();
        
        Destroy(effect, vfxLifetime);
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
        
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, pullRange);
        
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
        
        Gizmos.color = new Color(1, 0.5f, 0, 0.3f);
        Gizmos.DrawWireSphere(transform.position, escapeRange);
    }
    #endregion
}