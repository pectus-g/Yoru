using UnityEngine;

/// <summary>
/// YORU Form System — Phase 1 (snap-swap).
/// 
/// Handles the cat ↔ Granny transform on T-press.
/// 
/// Phase 1 scope:
///   - T toggles between the cat visual body (bodyYoru) and the Granny visual body
///   - Snap-swap via GameObject.SetActive — no fade or dissolve yet (Phase 1.5)
///   - CharacterController capsule dimensions swap on transform (cat capsule vs human capsule)
///   - Granny's animator is driven by a parallel state mirror in Update — reads
///     CharacterController velocity + isGrounded directly so PlayerMovement is not touched
///   - Camera FollowOffset gets a height offset in Granny form to compensate for her taller silhouette
/// 
/// Phase 1 caveats (intentional — these layers belong to phase 2+):
///   - No -30% speed: Granny inherits cat speed
///   - No combat-input lockout in Granny form
///   - No "outside combat only" gate on transform
///   - Listen/Talk animator states wired but not triggered (no dialogue system yet)
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
    [Tooltip("Cat visual body GameObject. Auto-finds the child named 'bodyYoru' if left empty.")]
    [SerializeField] private GameObject catBody;
    [Tooltip("Granny visual body GameObject. After parenting GrannyFINAL under PlayerYoru_Def, drag the instance here.")]
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
    [Tooltip("Capsule height in Granny form. Typical humanoid value 1.7-1.8. Tune in-engine.")]
    [SerializeField] private float grannyCapsuleHeight = 1.8f;
    [Tooltip("Capsule center Y in Granny form. Typically half of height so feet sit at the parent's Y.")]
    [SerializeField] private float grannyCapsuleCenterY = 0.9f;
    [Tooltip("Capsule radius in Granny form. Typical humanoid value 0.3-0.4. Tune in-engine.")]
    [SerializeField] private float grannyCapsuleRadius = 0.35f;
    
    [Header("Camera")]
    [Tooltip("Extra height (world units) added to camera FollowOffset when in Granny form, to compensate for her taller silhouette. Cat form uses 0. Tune in-engine — typical range 0.5-1.2.")]
    [SerializeField] private float grannyCameraHeightOffset = 0.8f;
    
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
        
        // Visual body swap (snap-swap — phase 1)
        if (catBody != null) catBody.SetActive(!toHuman);
        if (grannyBody != null) grannyBody.SetActive(toHuman);
        
        // CharacterController capsule swap so collision matches the active form's silhouette
        if (controller != null)
        {
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
    /// Parallel state mirror: when Granny is active, drive her animator's locomotion params
    /// from the CharacterController directly. PlayerMovement is untouched and still drives
    /// the cat animator — that animator is dormant under the disabled cat body so its writes
    /// are no-ops while Granny is up.
    /// </summary>
    private void DriveGrannyAnimator()
    {
        if (!isHuman || grannyAnimator == null || controller == null) return;
        
        Vector3 horizontalVel = controller.velocity;
        horizontalVel.y = 0f;
        
        grannyAnimator.SetFloat(speedHash, horizontalVel.magnitude);
        grannyAnimator.SetBool(isGroundedHash, controller.isGrounded);
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
