using UnityEngine;

/// <summary>
/// YORU Form System — Phase 1 (snap-swap).
/// 
/// Handles the cat ↔ Granny transform on T-press.
/// 
/// Phase 1 scope:
///   - T toggles between the cat visual body and the Granny visual body via GameObject.SetActive
///   - CharacterController capsule dimensions swap on transform (cat capsule vs human capsule),
///     bracketed by enabled=false/true so Unity safely recomputes collision state for the new
///     capsule. Without this bracket, runtime resize can leave the controller wedged: Move()
///     succeeds in code but produces zero actual motion. Known Unity quirk.
///   - Granny's animator is driven by a parallel state mirror in Update — reads
///     CharacterController velocity + isGrounded directly so PlayerMovement is not touched
///   - Camera FollowOffset gets a height offset in Granny form to compensate for her taller silhouette
/// 
/// Phase 1 caveats (intentional — these layers belong to phase 2+):
///   - No -30% speed: Granny inherits cat speed
///   - No combat-input lockout in Granny form: she can still jump/dash/attack via cat controls
///   - No "outside combat only" gate on transform
///   - Listen/Talk animator states wired into Granny controller but not triggered (no dialogue system yet)
/// 
/// Architectural notes:
///   - PlayerMovement.cs is DO NOT TOUCH. It keeps driving the cat animator. When Granny
///     is active, the cat body and its animator are inactive — those drives become no-ops
///     and Granny's animator is driven independently from here.
///   - Cinemachine camera follows the parent PlayerYoru_Def transform, so the camera does
///     not need to switch follow targets on transform — only its vertical offset adjusts.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class FormController : MonoBehaviour
{
    #region Serialized Fields
    
    [Header("Form Bodies")]
    [Tooltip("Cat visual body GameObject. Auto-finds the child named 'bodyYoru' if left empty, but explicit assignment is more reliable.")]
    [SerializeField] private GameObject catBody;
    [Tooltip("Granny visual body GameObject. Drag the Granny instance from the hierarchy here.")]
    [SerializeField] private GameObject grannyBody;
    [Tooltip("Animator component on the Granny body. Must use Granny_Animator_Controller with Speed (float) and IsGrounded (bool) params.")]
    [SerializeField] private Animator grannyAnimator;
    
    [Header("Input")]
    [Tooltip("Key that toggles between cat and Granny forms.")]
    [SerializeField] private KeyCode transformKey = KeyCode.T;
    
    [Header("CharacterController Dimensions — Cat")]
    [Tooltip("Capsule height in cat form. Leave at 0 to auto-capture from the live CharacterController on Awake.")]
    [SerializeField] private float catCapsuleHeight = 0f;
    [Tooltip("Capsule center Y in cat form. Auto-captured if catCapsuleHeight is 0.")]
    [SerializeField] private float catCapsuleCenterY = 0f;
    [Tooltip("Capsule radius in cat form. Auto-captured if catCapsuleHeight is 0.")]
    [SerializeField] private float catCapsuleRadius = 0f;
    
    [Header("CharacterController Dimensions — Granny")]
    [Tooltip("Capsule height in Granny form. Should match Granny's actual standing height.")]
    [SerializeField] private float grannyCapsuleHeight = 3.5f;
    [Tooltip("Capsule center Y in Granny form. Should be half of height so capsule bottom sits at the parent's Y.")]
    [SerializeField] private float grannyCapsuleCenterY = 1.75f;
    [Tooltip("Capsule radius in Granny form.")]
    [SerializeField] private float grannyCapsuleRadius = 0.5f;
    
    [Header("Camera")]
    [Tooltip("Extra height (world units) added to camera FollowOffset when in Granny form, to compensate for her taller silhouette. Cat form uses 0.")]
    [SerializeField] private float grannyCameraHeightOffset = 0.8f;
    
    [Header("Animation")]
    [Tooltip("Damping time (seconds) for smoothing the Speed value sent to Granny's animator. Smooths transitions between Idle/Walk/Run so they ease rather than snap. Higher = smoother but less responsive. Lower = snappier but can show micro-jitter. Typical 0.1-0.25. Mirrors PlayerMovement's speedDampTime for the cat.")]
    [SerializeField] private float speedSmoothTime = 0.15f;
    
    [Header("Debug")]
    [Tooltip("Log a message to the console on every form transform.")]
    [SerializeField] private bool logTransforms = true;
    
    #endregion
    
    #region Private Fields
    
    private CharacterController controller;
    private ThirdPersonCamera cameraController;
    private bool isHuman;
    
    // Cached animator hashes (faster than strings). Match the Granny controller's parameter names.
    private readonly int speedHash = Animator.StringToHash("Speed");
    private readonly int isGroundedHash = Animator.StringToHash("IsGrounded");
    
    // Position-delta speed tracking. Computing speed from transform.position changes
    // between frames sidesteps Unity's CharacterController.velocity quirks — it doesn't
    // matter HOW the position changed (Move(), direct transform assignment, etc), only
    // that it changed. More robust than reading controller.velocity directly.
    private Vector3 lastPosition;
    private bool lastPositionInitialized;
    
    // Smoothed-speed tracking so Idle/Walk/Run transitions ease rather than snap.
    // SmoothDamp pattern matches PlayerMovement's currentSpeed/speedVelocity for the cat.
    private float currentSmoothedSpeed;
    private float speedSmoothVelocity;
    
    #endregion
    
    #region Public API
    
    /// <summary>
    /// True when Granny form is active. Query this from future systems (dialogue,
    /// combat lockout, stats, persuasion ability gates, etc) to branch by current form.
    /// </summary>
    public bool IsHuman => isHuman;
    
    #endregion
    
    #region Unity Lifecycle
    
    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        cameraController = FindObjectOfType<ThirdPersonCamera>();
        
        // Auto-find cat body via existing convention if not assigned
        if (catBody == null)
        {
            Transform catTransform = transform.Find("bodyYoru");
            if (catTransform != null) catBody = catTransform.gameObject;
        }
        
        // Auto-capture live cat capsule dims if Inspector values are zero
        if (controller != null && catCapsuleHeight <= 0f)
        {
            catCapsuleHeight = controller.height;
            catCapsuleCenterY = controller.center.y;
            catCapsuleRadius = controller.radius;
        }
        
        ValidateReferences();
    }
    
    private void Start()
    {
        // Always start in cat form. Ensures known state regardless of which body was
        // left active in the Inspector at edit time.
        ApplyForm(toHuman: false);
    }
    
    private void Update()
    {
        if (Input.GetKeyDown(transformKey))
        {
            ToggleForm();
        }
    }
    
    private void FixedUpdate()
    {
        // Drive Granny's animator here — at physics rate — because PlayerMovement calls
        // controller.Move() in its FixedUpdate. Sampling position delta at the same rate
        // gives stable speed (walkSpeed exactly when walking, runSpeed exactly when running).
        // Sampling in Update gave wildly variable readings: zero on frames between FixedUpdates,
        // doubled on frames where two FixedUpdates ran (frame catch-up).
        DriveGrannyAnimator();
    }
    
    #endregion
    
    #region Form Switching
    
    private void ToggleForm()
    {
        ApplyForm(!isHuman);
    }
    
    private void ApplyForm(bool toHuman)
    {
        isHuman = toHuman;
        
        // Reset position-delta + smoothing baselines so the first frame in Granny form
        // doesn't produce a huge speed reading from a stale lastPosition, and the smoothed
        // value doesn't carry over from the previous form session.
        lastPositionInitialized = false;
        currentSmoothedSpeed = 0f;
        speedSmoothVelocity = 0f;
        
        // Visual body swap (snap-swap — phase 1)
        if (catBody != null) catBody.SetActive(!toHuman);
        if (grannyBody != null) grannyBody.SetActive(toHuman);
        
        // CharacterController capsule swap. Bracketed by enabled=false/true so Unity
        // recomputes collision state cleanly for the new capsule shape. Without this,
        // runtime resize can leave the controller in a wedged state where Move()
        // succeeds in code but produces zero actual motion (especially when the
        // new capsule is significantly larger than the old).
        if (controller != null)
        {
            controller.enabled = false;
            if (toHuman)
            {
                controller.height = grannyCapsuleHeight;
                controller.center = new Vector3(controller.center.x, grannyCapsuleCenterY, controller.center.z);
                controller.radius = grannyCapsuleRadius;
            }
            else
            {
                controller.height = catCapsuleHeight;
                controller.center = new Vector3(controller.center.x, catCapsuleCenterY, controller.center.z);
                controller.radius = catCapsuleRadius;
            }
            controller.enabled = true;
        }
        
        // Camera vertical adjust so Granny doesn't push out of frame
        if (cameraController != null)
        {
            cameraController.SetFormHeightOffset(toHuman ? grannyCameraHeightOffset : 0f);
        }
        
        if (logTransforms)
        {
            Debug.Log($"[FormController] Transformed to {(toHuman ? "Granny" : "Cat")} form.");
        }
    }
    
    #endregion
    
    #region Animator Mirror
    
    /// <summary>
    /// Drive Granny's animator from physics-rate position delta. Called from FixedUpdate so
    /// the sample rate matches PlayerMovement's controller.Move() rate. Speed = motion-per-
    /// FixedUpdate / Time.fixedDeltaTime, which produces stable walkSpeed/runSpeed readings.
    /// SmoothDamp then eases transitions between Idle/Walk/Run so blend changes feel natural
    /// rather than snapping.
    /// 
    /// We deliberately use transform.position delta rather than CharacterController.velocity —
    /// velocity has shown unreliable readings in this project after runtime capsule resize.
    /// Position is ground truth.
    /// </summary>
    private void DriveGrannyAnimator()
    {
        if (!isHuman || grannyAnimator == null) return;
        
        Vector3 currentPosition = transform.position;
        if (!lastPositionInitialized)
        {
            lastPosition = currentPosition;
            lastPositionInitialized = true;
            return;
        }
        
        Vector3 delta = currentPosition - lastPosition;
        delta.y = 0f;
        float rawSpeed = delta.magnitude / Time.fixedDeltaTime;
        lastPosition = currentPosition;
        
        // Smooth the raw speed so the blend tree eases between Idle/Walk/Run rather than
        // step-snapping. Time.fixedDeltaTime is passed explicitly since we're in FixedUpdate.
        currentSmoothedSpeed = Mathf.SmoothDamp(
            currentSmoothedSpeed, rawSpeed, ref speedSmoothVelocity,
            speedSmoothTime, Mathf.Infinity, Time.fixedDeltaTime);
        
        grannyAnimator.SetFloat(speedHash, currentSmoothedSpeed);
        if (controller != null) grannyAnimator.SetBool(isGroundedHash, controller.isGrounded);
    }
    
    #endregion
    
    #region Validation
    
    private void ValidateReferences()
    {
        if (catBody == null)
            Debug.LogWarning("[FormController] catBody is not assigned and no 'bodyYoru' child was found.");
        if (grannyBody == null)
            Debug.LogWarning("[FormController] grannyBody is not assigned. Drag the Granny body GameObject from the hierarchy into this Inspector field.");
        if (grannyAnimator == null)
            Debug.LogWarning("[FormController] grannyAnimator is not assigned. Granny will appear but will not animate when active.");
        if (cameraController == null)
            Debug.LogWarning("[FormController] ThirdPersonCamera not found in scene. Camera height will not adjust on transform.");
    }
    
    #endregion
}