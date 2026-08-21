using UnityEngine;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(Animator))]
public class PlayerMovement : MonoBehaviour
{
    #region Serialized Fields
    [Header("Movement")]
    [SerializeField] private float walkSpeed = 2f;
    [SerializeField] private float runSpeed = 7f;
    [SerializeField] private float rotationSpeed = 8f;
    [SerializeField] private float speedDampTime = 0.25f;
    
    [Header("Jump")]
    [SerializeField] private float jumpHeight = 2f;
    [SerializeField] private float doubleJumpHeight = 1.5f;
    [SerializeField] private float tripleJumpHeight = 1.2f;
    [SerializeField] private float jumpForwardSpeed = 4f;
    
    [Header("Timing")]
    [SerializeField] private float multiJumpWindow = 0.5f;
    [SerializeField] private float coyoteTime = 0.1f;
    
    [Header("Physics")]
    [SerializeField] private float gravity = -15f;
    [SerializeField] private float fallMultiplier = 1.5f;
    
    [Header("VFX")]
    [SerializeField] private bool enableVFX = true;

    [Header("Debug — Freeze Diagnosis (temporary)")]
    [Tooltip("Log which combat flags are blocking movement. Toggle OFF once freeze is confirmed fixed.")]
    [SerializeField] private bool showFreezeDebug = true;
    #endregion
    
    #region Private Fields
    // Cached components (allocated once)
    private PlayerCombat playerCombat;
    private GuardMovementController guardMovement;
    private FormController formController;
    private PlayerHealth playerHealth;

    private CharacterController controller;
    private Animator animator;
    private ThirdPersonCamera cameraController;
    private YoruVFXManager vfxManager; // VFX Integration
    private Transform cachedTransform;
    
    // Cached animator hashes (much faster than strings)
    private readonly int speedHash = Animator.StringToHash("Speed");
    private readonly int isGroundedHash = Animator.StringToHash("IsGrounded");
    private readonly int locomotionHash = Animator.StringToHash("Locomotion");
    private readonly int jump2LegsHash = Animator.StringToHash("JumpWith2Legs");
    private readonly int jump4LegsHash = Animator.StringToHash("JumpWith4Legs");
    
    // State flags (use bitwise flags for multiple states)
    private enum PlayerState
    {
        Grounded = 1 << 0,
        Jumping = 1 << 1,
        Running = 1 << 2,
        Moving = 1 << 3,
        Landing = 1 << 4
    }
    private PlayerState currentState;
    
    // Physics state
    private Vector3 velocity;
    private Vector3 jumpMomentum;
    private Vector3 moveDirection;
    
    // External pull (e.g. Nopperabō HairLash). Owned by PlayerMovement so there is only ever
    // ONE controller.Move per system — set via ApplyExternalPull, applied in FixedUpdate,
    // auto-expires. Never runs during dodge/dash/guard so it can't fight those movers.
    private Vector3 externalPullVelocity;
    private float externalPullEndTime;
    
    // Jump state
    private byte jumpCount; // byte is smaller than int
    private float jumpWindowTimer;
    private float coyoteTimer;
    private bool wasRunningForJump; // Track if running when started jumping
    
    // Phase 3 Option A — Granny absolute speed overrides per GDD Doc 04 §4b. Set externally
    // by FormController.SetGrannySpeed on form transform. Value of 0 means "no override, use
    // cat default speed" — used in cat form. Granny form supplies the explicit speed values
    // (tuned in FormController Inspector to match Granny's animation stride for foot-locking).
    private float grannyWalkSpeedOverride = 0f;
    private float grannyRunSpeedOverride = 0f;
    
    // Animation state
    private float currentSpeed;
    private float speedVelocity;
    private int currentAnimStateHash;
    
    // Constants
    private const float MIN_MOVE_THRESHOLD = 0.01f;
    private const float GROUND_CHECK_DISTANCE = 0.1f;
    private const float ANIMATION_CROSS_FADE = 0.05f;
    private const float MIN_AIRBORNE_FOR_LANDING = 0.15f; // Must be airborne this long before OnLanded fires
    // ROUND 10: grounded + PlayerState.Jumping for longer than this is impossible in normal play
    // once no combat action is deferring the landing, so it is treated as the stuck state and rescued.
    private const float JUMPING_STUCK_LIMIT = 0.4f;
    private float groundedWhileJumpingTimer;
    private const float FALL_DEATH_Y = -50f; // Teleport back if Yoru falls below this
    
    // Ground flicker fix
    private float airborneTimer;
    // Deferred landing: when CharacterController grounds while a combat action is active,
    // OnLanded is skipped (combat animations would be interrupted). But the !wasGrounded
    // edge condition won't retrigger, so PlayerState.Jumping stays stuck forever and
    // ApplyMovement (line 335) is blocked. Setting this flag re-tries OnLanded every
    // frame until the combat action ends and a clean landing can fire.
    private bool pendingLanding;
    
    // Fall safety net
    private Vector3 lastSafePosition;
    #endregion
    
    #region Unity Lifecycle
    private void Awake()
    {playerCombat = GetComponent<PlayerCombat>();
        guardMovement = GetComponent<GuardMovementController>();
        formController = GetComponent<FormController>();
        playerHealth = GetComponent<PlayerHealth>();
        // Cache all components once
        controller = GetComponent<CharacterController>();
        animator = GetComponent<Animator>();
        cachedTransform = transform;
        lastSafePosition = transform.position;
        
        // Get VFX Manager if enabled
        if (enableVFX)
        {
            vfxManager = GetComponent<YoruVFXManager>();
            if (vfxManager == null && enableVFX)
            {
                Debug.LogWarning("VFX enabled but YoruVFXManager not found!");
            }
        }
    }
    
    private void Start()
    {
        cameraController = FindObjectOfType<ThirdPersonCamera>();
        
        // Disable NavMesh if present
        var agent = GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (agent) agent.enabled = false;
    }
    
    private void Update()
    {
        // Handle input and state updates
        HandleInput();
        UpdateState();
        UpdateAnimation();
        
        // Fall safety net — teleport back if Yoru falls through terrain
        if (cachedTransform.position.y < FALL_DEATH_Y)
        {
            Debug.LogWarning($"[Movement] Yoru fell below {FALL_DEATH_Y}! Teleporting to last safe position: {lastSafePosition}");
            controller.enabled = false;
            cachedTransform.position = lastSafePosition;
            controller.enabled = true;
            velocity = Vector3.zero;
            jumpMomentum = Vector3.zero;
        }
        
        // Track last safe grounded position
        if (HasState(PlayerState.Grounded))
        {
            lastSafePosition = cachedTransform.position;
        }
    }
    
    private bool wasDodgingLastFrame; // Track dodge transition for velocity reset
    
    private void FixedUpdate()
    {
        bool isDodgingNow = playerCombat != null && playerCombat.IsDodging();
        bool isDashingNow = playerCombat != null && playerCombat.IsDashing();
        bool isGuardingNow = playerCombat != null && playerCombat.IsGuarding();
        
        // During dodge/dash, the coroutine handles ALL movement via CharacterController.Move()
        // We must NOT apply gravity or movement here or they fight each other = stutter
        if (isDodgingNow || isDashingNow)
        {
            wasDodgingLastFrame = true;
            return;
        }
        
        // During guard: combine horizontal (from GuardMovementController) + gravity into single Move
        // v15 fix: v14 had TWO controller.Move() calls (GuardMovementController.Update + here)
        // causing CharacterController grounded oscillation → feet underground + landing VFX spam.
        // Now GuardMovementController only caches velocity; we do the single Move here.
        if (isGuardingNow)
        {
            if (controller.isGrounded)
                velocity.y = -2f;
            else
                velocity.y += gravity * fallMultiplier * Time.fixedDeltaTime;
            velocity.y = Mathf.Max(velocity.y, -50f);

            Vector3 guardHorizontal = Vector3.zero;
            if (guardMovement != null)
                guardHorizontal = guardMovement.GetGuardHorizontalVelocity();

            Vector3 guardMove = new Vector3(guardHorizontal.x, velocity.y, guardHorizontal.z);
            controller.Move(guardMove * Time.fixedDeltaTime);
            return;
        }
        
        // Just exited dodge — reset velocity so there's no gravity jolt
        if (wasDodgingLastFrame)
        {
            wasDodgingLastFrame = false;
            velocity.y = controller.isGrounded ? -2f : 0f;
            jumpMomentum = Vector3.zero;
        }
        
        // Physics should be in FixedUpdate for consistency
        ApplyMovement();
        ApplyGravity();
        controller.Move(velocity * Time.fixedDeltaTime);
        
        // External pull — applied AFTER normal locomotion, only in the normal path (dodge/dash/
        // guard already returned above, so this never touches them). Time-boxed and self-expiring.
        if (Time.time < externalPullEndTime)
        {
            Vector3 pull = externalPullVelocity;
            pull.y = 0f; // horizontal drag only — never fight gravity/jump on Y
            controller.Move(pull * Time.fixedDeltaTime);
        }
    }
    #endregion
    
    #region Input & State Management
    private void HandleInput()
    {
        // Early exit for inventory
        if (InventoryUI.Instance?.IsInventoryOpen() ?? false)
        {
            SetState(PlayerState.Moving, false);
            return;
        }
        
        // Phase 3 — input locked during form transform fade window (FormController coroutine).
        // The fade is 0.6s (Inspector-tunable); during it, neither cat nor Granny should
        // accept WASD/jump/etc input so the transformation reads as a deliberate moment
        // rather than the player fighting controls through a half-rendered state.
        if (formController != null && formController.IsTransforming)
        {
            SetState(PlayerState.Moving, false);
            return;
        }
        
        // Block ALL movement while stunned/captured (e.g. an enemy grab). Mirrors the hit-react
        // block. The external pull is applied separately in FixedUpdate, so a grabbed Yoru can
        // still be reeled in toward the enemy while he's held.
        if (playerHealth != null && playerHealth.IsStunned())
        {
            SetState(PlayerState.Moving, false);
            return;
        }
        
        // Block movement during hit reaction
        if (playerCombat != null && playerCombat.IsInHitReaction())
        {
            SetState(PlayerState.Moving, false);
            return;
        }
        
        // During attacks: block movement, allow rotation only (feet planted, body can turn)
        // FaceNearestEnemy in PlayerCombat handles enemy targeting rotation.
        // This just stops WASD from sliding Yoru while swinging.
        if (playerCombat != null && (playerCombat.IsAttacking() || playerCombat.IsChargingHeavy() || playerCombat.IsDodging() || playerCombat.IsDashing() || playerCombat.IsGuarding()))
        {
            // Temporary freeze diagnosis — toggle showFreezeDebug in Inspector
            if (showFreezeDebug)
            {
                Debug.Log($"[FREEZE-DEBUG] atk={playerCombat.IsAttacking()} hvy={playerCombat.IsChargingHeavy()} " +
                    $"dod={playerCombat.IsDodging()} dsh={playerCombat.IsDashing()} grd={playerCombat.IsGuarding()} " +
                    $"hit={playerCombat.IsInHitReaction()} lock={playerCombat.IsPositionLocked()} " +
                    $"animSpeed={playerCombat.GetAnimatorSpeed():F2}");
            }
            SetState(PlayerState.Moving, false);
            SetState(PlayerState.Running, false);
            return;
        }
        
        // Get input once per frame
        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");
        
        // Calculate movement efficiently
        bool isMoving = (h * h + v * v) > MIN_MOVE_THRESHOLD;
        SetState(PlayerState.Moving, isMoving);
        
        if (isMoving)
        {
            // Only calculate direction if moving
            if (cameraController != null)
            {
                Vector3 forward = cameraController.GetCameraForward();
                Vector3 right = cameraController.GetCameraRight();
                moveDirection = (forward * v + right * h).normalized;
            }
            else
            {
                moveDirection.Set(h, 0, v);
                moveDirection.Normalize();
            }
            
            SetState(PlayerState.Running, Input.GetKey(KeyCode.LeftShift));
        }
        else
        {
            SetState(PlayerState.Running, false);
        }
        
        // Jump input — Phase 2: blocked in Tomoe (human) form per GDD Doc 04 §4b
        // ("no dodge, dash, jump, or aerial moves — walks and runs only")
        if (Input.GetKeyDown(KeyCode.Space) && (formController == null || !formController.IsHuman))
        {
            TryJump();
        }
    }
    
    private void UpdateState()
    {
        // Ground check
        bool wasGrounded = HasState(PlayerState.Grounded);
        bool isGrounded = controller.isGrounded;
        SetState(PlayerState.Grounded, isGrounded);
        
        // Track airborne time — prevents ground flicker from triggering false landings
        if (!isGrounded)
            airborneTimer += Time.deltaTime;
        
        // Landing detection — only fires if actually airborne for MIN_AIRBORNE_FOR_LANDING
        // Also skip during dodge/attack — combat layer handles those animations
        if (!wasGrounded && isGrounded && airborneTimer >= MIN_AIRBORNE_FOR_LANDING)
        {
            bool inCombatAction = IsInCombatActionForLanding();
            
            if (!inCombatAction)
            {
                OnLanded();
            }
            else
            {
                pendingLanding = true;  // defer the landing ANIMATION until the action ends...
                ClearAirborneState();   // ...but never defer her control. ROUND 10b.
            }
            
            airborneTimer = 0f;
        }
        else if (!wasGrounded && isGrounded)
        {
            // Ground flicker — reset timer but don't trigger landing
            airborneTimer = 0f;
        }
        else if (wasGrounded && !isGrounded)
        {
            coyoteTimer = coyoteTime;
            // ROUND 10 — THE FREEZE. This edge also fires on a SINGLE frame of ground flicker,
            // and cancelling the deferred landing here is what killed Yoru's movement for good:
            //   jump (PlayerState.Jumping = true)
            //   -> land while dodging/attacking/flinching  -> landing deferred, pendingLanding = true
            //   -> one flicker frame                       -> pendingLanding = false, retry gone
            //   -> OnLanded() never runs, and it is the ONLY place Jumping is ever cleared
            //   -> ApplyMovement matches neither branch (grounded AND Jumping) = no walking,
            //      no turning, no jumping, forever. Three logged freezes, all with a jump
            //      and no matching "LANDED!" line. Repro: jump, then front-flip.
            // The cancel now waits for REAL airtime, using the same threshold the landing edge
            // above already uses. A flicker is not a jump.
        }

        // Cancel the deferred landing only once Yoru is genuinely airborne again.
        if (pendingLanding && !isGrounded && airborneTimer >= MIN_AIRBORNE_FOR_LANDING)
            pendingLanding = false;

        // Retry deferred landing once combat action clears.
        // Without this, the !wasGrounded edge condition above would never re-fire,
        // leaving PlayerState.Jumping stuck and ApplyMovement (line 335) blocked.
        if (pendingLanding && isGrounded && !IsInCombatActionForLanding())
        {
            OnLanded();
            pendingLanding = false;
        }

        // ROUND 10 SAFETY NET — Jumping must never outlive being on the ground.
        // Grounded, no combat action holding the landing back, and Jumping STILL set means the
        // deferred landing was lost. Whatever the path (including any we have not found), this
        // turns a permanent freeze into a fraction of a second, and says so out loud so the next
        // log proves whether the fix above actually held. It stays silent during a legitimate
        // deferral, because a live combat action fails the condition.
        if (isGrounded && HasState(PlayerState.Jumping) && !IsInCombatActionForLanding())
        {
            groundedWhileJumpingTimer += Time.deltaTime;
            if (groundedWhileJumpingTimer >= JUMPING_STUCK_LIMIT)
            {
                Debug.LogWarning($"[PlayerMovement] STUCK-JUMP RESCUE: on the ground for "
                    + $"{groundedWhileJumpingTimer:F2}s with PlayerState.Jumping still set — movement was "
                    + $"dead (ApplyMovement can match neither branch). Forcing the landing. "
                    + $"pendingLanding={pendingLanding} jumpCount={jumpCount} airborneTimer={airborneTimer:F3}");
                OnLanded();
                pendingLanding = false;
                groundedWhileJumpingTimer = 0f;
            }
        }
        else
        {
            groundedWhileJumpingTimer = 0f;
        }
        
        // Update timers
        if (jumpWindowTimer > 0)
            jumpWindowTimer -= Time.deltaTime;
        
        if (coyoteTimer > 0)
            coyoteTimer -= Time.deltaTime;
        
        // Reset velocity when grounded
        if (isGrounded && velocity.y < 0)
        {
            velocity.y = -2f;
        }
    }
    #endregion
    
    #region Movement & Physics
    private void ApplyMovement()
    {
        if (!HasState(PlayerState.Moving))
        {
            jumpMomentum = Vector3.Lerp(jumpMomentum, Vector3.zero, Time.fixedDeltaTime * 5f);
            return;
        }
        
        // Only calculate when needed
        if (HasState(PlayerState.Grounded) && !HasState(PlayerState.Jumping))
        {
            float speed = HasState(PlayerState.Running) 
                ? (grannyRunSpeedOverride > 0f ? grannyRunSpeedOverride : runSpeed) 
                : (grannyWalkSpeedOverride > 0f ? grannyWalkSpeedOverride : walkSpeed);
            Vector3 movement = moveDirection * speed * Time.fixedDeltaTime;
            controller.Move(movement);
            
            // Rotation
            if (moveDirection.sqrMagnitude > MIN_MOVE_THRESHOLD)
            {
                Quaternion targetRot = Quaternion.LookRotation(moveDirection);
                cachedTransform.rotation = Quaternion.Slerp(cachedTransform.rotation, targetRot, 
                    rotationSpeed * Time.fixedDeltaTime);
            }
        }
        else if (!HasState(PlayerState.Grounded))
        {
            // Air movement
            controller.Move(jumpMomentum * Time.fixedDeltaTime);
        }
    }
    
    private void ApplyGravity()
    {
        // Apply gravity with optional fall multiplier
        float gravityMult = (velocity.y < 0) ? fallMultiplier : 1f;
        velocity.y += gravity * gravityMult * Time.fixedDeltaTime;
        velocity.y = Mathf.Max(velocity.y, -50f); // Terminal velocity
    }
    
    /// <summary>
    /// Applies an external world-space pull to Yoru for a short duration (e.g. an enemy hair-grab
    /// dragging Yoru toward them). PlayerMovement integrates it into its own single Move so it never
    /// fights the normal locomotion/gravity Move. Call every frame while the pull should be active;
    /// the latest call wins and refreshes the expiry.
    /// </summary>
    public void ApplyExternalPull(Vector3 worldVelocity, float duration)
    {
        externalPullVelocity = worldVelocity;
        externalPullEndTime = Time.time + duration;
    }
    #endregion
    
    #region Jump System
    private void TryJump()
    {if (playerCombat != null && (playerCombat.IsAttacking() && !playerCombat.IsAerialAttack() || playerCombat.IsGuarding())) return;
        // First jump
        if (jumpCount == 0 && (HasState(PlayerState.Grounded) || coyoteTimer > 0))
        {
            bool isRunning = HasState(PlayerState.Running);
            wasRunningForJump = isRunning; // Remember if we were running for multi-jump
            PerformJump(jumpHeight, isRunning);
            jumpCount = 1;
            jumpWindowTimer = multiJumpWindow;
            
            // VFX for first jump
            vfxManager?.OnJump(jumpCount);
        }
        // Multi-jump (only if was running and within window)
        else if (jumpCount > 0 && jumpCount < 3 && jumpWindowTimer > 0 && wasRunningForJump)
        {
            float power = (jumpCount == 1) ? doubleJumpHeight : tripleJumpHeight;
            PerformJump(power, true);
            jumpCount++;
            jumpWindowTimer = (jumpCount < 3) ? multiJumpWindow : 0;
            
            // VFX for multi-jump
            vfxManager?.OnJump(jumpCount);
            
            if (jumpCount >= 3)
            {
                Debug.Log("🚫 Max jumps reached!");
            }
        }
    }
    
    private void PerformJump(float power, bool isFourLegged)
    {
        velocity.y = Mathf.Sqrt(power * -2f * gravity);
        
        // Set momentum
        if (HasState(PlayerState.Moving))
        {
            float momentumSpeed = isFourLegged ? jumpForwardSpeed * 1.5f : jumpForwardSpeed;
            jumpMomentum = moveDirection * momentumSpeed;
        }
        else
        {
            jumpMomentum = cachedTransform.forward * 0.5f;
        }
        
        // Trigger animation efficiently
        SetState(PlayerState.Jumping, true);
        int animHash = isFourLegged ? jump4LegsHash : jump2LegsHash;
        animator.CrossFadeInFixedTime(animHash, ANIMATION_CROSS_FADE, 0);
        
        // Debug output
        if (isFourLegged)
            Debug.Log($"🐾 4-LEG JUMP #{jumpCount}");
        else
            Debug.Log($"🚶 2-LEG JUMP");
    }
    
    /// <summary>
    /// ROUND 10b. The half of landing that must happen the INSTANT her feet touch the floor,
    /// no matter what animation is still finishing. PlayerState.Jumping blocks BOTH branches of
    /// ApplyMovement, so leaving it set costs her walking AND turning — and jumpCount left at its
    /// old value blocks the next jump. Control is not an animation concern, so it is no longer
    /// deferred with one. This is also what makes the freeze structurally impossible: the flag
    /// that kills movement is now cleared on touchdown by every path, not only the clean one.
    /// </summary>
    private void ClearAirborneState()
    {
        jumpCount = 0;
        jumpWindowTimer = 0;
        wasRunningForJump = false;
        jumpMomentum *= 0.5f; // Gradual stop
        SetState(PlayerState.Jumping, false);
    }

    private void OnLanded()
    {
        Debug.Log($"✅ LANDED! (was jump #{jumpCount})");
        
        ClearAirborneState();
        SetState(PlayerState.Landing, true);
        
        // Force locomotion state
        animator.CrossFadeInFixedTime(locomotionHash, 0.2f, 0);
        
        // Clear landing state after a moment
        Invoke(nameof(ClearLandingState), 0.3f);
        
        // VFX handles landing automatically by watching grounded state
    }
    
    private void ClearLandingState()
    {
        SetState(PlayerState.Landing, false);
    }
    #endregion
    
    #region Animation
    private void UpdateAnimation()
    {
        // Don't update locomotion during hit reaction — it overrides the flinch animation
        if (playerCombat != null && playerCombat.IsInHitReaction())
            return;
        
        // Don't update locomotion during attacks, dodge, or dash — combat layer handles it
        if (playerCombat != null && (playerCombat.IsAttacking() || playerCombat.IsChargingHeavy() || playerCombat.IsDodging() || playerCombat.IsDashing() || playerCombat.IsGuarding()))
            return;
        
        // Skip if jumping or landing
        if (HasState(PlayerState.Jumping | PlayerState.Landing))
            return;
        
        // Calculate target speed
        float targetSpeed = 0f;
        if (HasState(PlayerState.Moving) && HasState(PlayerState.Grounded))
        {
            targetSpeed = HasState(PlayerState.Running) ? 2f : 1f;
        }
        // Bug 2 fix: during post-dodge fall, keep speed at run value if player is sprinting
        // Without this, speed damps to 0 while airborne → base layer shows 2-leg idle
        else if (playerCombat != null && Time.time - playerCombat.GetDodgeEndTime() < 0.5f
                 && Input.GetKey(KeyCode.LeftShift))
        {
            targetSpeed = 2f;
        }
        
        // Smooth transition (only update when changed)
        if (Mathf.Abs(currentSpeed - targetSpeed) > 0.01f)
        {
            currentSpeed = Mathf.SmoothDamp(currentSpeed, targetSpeed, ref speedVelocity, speedDampTime);
            animator.SetFloat(speedHash, currentSpeed);
        }
        
        // Update grounded state (only when changed)
        animator.SetBool(isGroundedHash, HasState(PlayerState.Grounded));
    }
    #endregion
    
    #region Helper Methods
    private void SetState(PlayerState state, bool value)
    {
        if (value)
            currentState |= state;
        else
            currentState &= ~state;
    }
    
    private bool HasState(PlayerState state)
    {
        return (currentState & state) != 0;
    }
    
    /// <summary>
    /// Combat-action gate used by landing detection. If any of these are active when
    /// Yoru becomes grounded, OnLanded is deferred until they clear (see pendingLanding).
    /// Mirrors the legacy inCombatAction inline check — extracted so the landing detection
    /// and the deferred-landing retry share one source of truth.
    /// </summary>
    private bool IsInCombatActionForLanding()
    {
        // ROUND 10b: the 0.5s post-dodge grace was REMOVED from this check. Measured in the
        // 14:31 log, every jump+flip landed 0.503-0.505s after 'Dodge ended', four out of four
        // — that window, to the millisecond. Until it expired the landing could not fire, so the
        // base layer stayed in the jump pose (she held the flip's tipped-forward shape) and
        // PlayerState.Jumping stayed set (no walking, no turning). It was guarding EndDodge's
        // crossfade back to combat idle, which is only 0.25s and runs on a DIFFERENT animator
        // layer than the landing's 0.2s blend, so the two never actually collided.
        // The grace is untouched everywhere else — this is the landing check only.
        return playerCombat != null && 
            (playerCombat.IsDodging() || playerCombat.IsDashing() || 
             playerCombat.IsAttacking() || playerCombat.IsInHitReaction() ||
             playerCombat.IsGuarding());
    }
    
    /// <summary>Returns true if the player is currently in a running state.</summary>
    public bool IsRunning() => HasState(PlayerState.Running);
/// <summary>Returns true if the player is currently in a running state.</summary>
   
    
    /// <summary>Returns true while the player is off the ground (jumping or falling). Read-only; used by enemies for anti-air timing.</summary>
    public bool IsAirborne() => !HasState(PlayerState.Grounded);    
    /// <summary>Returns remaining time in the jump window. Used by PlayerCombat for air dodge/dash timing.</summary>
    public float GetJumpWindowTimer() => jumpWindowTimer;
    
    /// <summary>Cat's walk speed value, read by FormController to send the correct intent
    /// value to Granny's animator blend tree (which was calibrated against cat's speed).</summary>
    public float WalkSpeed => walkSpeed;
    
    /// <summary>Cat's run speed value, read by FormController to send the correct intent
    /// value to Granny's animator blend tree (which was calibrated against cat's speed).</summary>
    public float RunSpeed => runSpeed;
    
    /// <summary>
    /// Set absolute walk and run speeds for Granny form. Called by FormController on
    /// cat ↔ Granny transform. Pass 0 to clear the override (cat form falls back to
    /// the authored walkSpeed/runSpeed values). Granny form passes the tuned absolute
    /// values from FormController's Inspector (grannyWalkSpeed, grannyRunSpeed) which
    /// should match the speed her animation was authored at for clean foot-locking.
    /// </summary>
    public void SetGrannySpeed(float walkOverride, float runOverride)
    {
        grannyWalkSpeedOverride = walkOverride;
        grannyRunSpeedOverride = runOverride;
    }
    #endregion
    
    #region Debug
    #if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (!Application.isPlaying || controller == null) return;
        
        Gizmos.color = HasState(PlayerState.Grounded) ? Color.green : Color.red;
        Gizmos.DrawWireSphere(cachedTransform.position - Vector3.up * (controller.height * 0.5f), 0.3f);
        
        // Jump momentum visualization
        if (jumpMomentum.magnitude > 0.1f)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawRay(cachedTransform.position, jumpMomentum);
        }
        
        // Jump window visualization
        if (wasRunningForJump && jumpCount > 0 && jumpWindowTimer > 0)
        {
            float percent = jumpWindowTimer / multiJumpWindow;
            Gizmos.color = percent > 0.5f ? Color.green : percent > 0.2f ? Color.yellow : Color.red;
            Gizmos.DrawWireCube(cachedTransform.position + Vector3.up * 2, 
                                new Vector3(percent * 2f, 0.1f, 0.1f));
        }
    }
    #endif
    #endregion
}