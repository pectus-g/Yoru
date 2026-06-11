using System.Collections;
using UnityEngine;

/// <summary>
/// YORU Form System — Phase 1 (snap-swap) + Phase 3 (cross-fade overlay).
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
[RequireComponent(typeof(AudioSource))]
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
    [Tooltip("CinemachineHardLookAt vertical aim offset for cat form. Should match Yoru's head height above the player root pivot. Default 1.0 matches the camera prefab's authored value.")]
    [SerializeField] private float catCameraLookAtYOffset = 1.0f;
    [Tooltip("CinemachineHardLookAt vertical aim offset for Granny form. Should match Granny's head height above the player root pivot. Granny capsule is ~3.5 tall — head sits around 3.0. Tune in Inspector until RMB-zoom converges on her head, not her chest.")]
    [SerializeField] private float grannyCameraLookAtYOffset = 3.0f;
    
    [Header("Granny Movement")]
    [Tooltip("Absolute walk speed (m/s) for Granny form. Tune this LIVE in Play mode while watching her feet — find the value where her feet plant cleanly on the ground without sliding or skating. That value is the speed her walk animation was authored at. Starting guess: 1.5. The cat's walk speed is untouched, this only affects Granny.")]
    [SerializeField] private float grannyWalkSpeed = 1.5f;
    [Tooltip("Absolute run speed (m/s) for Granny form. Same tuning workflow: hold Shift in Granny form, watch her feet while she runs, adjust this value until feet plant cleanly. Starting guess: 4.0.")]
    [SerializeField] private float grannyRunSpeed = 4.0f;
    
    [Header("Animation")]
    [Tooltip("Damping time (seconds) for smoothing the Speed value sent to Granny's animator. Smooths transitions between Idle/Walk/Run so they ease rather than snap. Higher = smoother but less responsive. Lower = snappier but can show micro-jitter. Typical 0.1-0.25. Mirrors PlayerMovement's speedDampTime for the cat.")]
    [SerializeField] private float speedSmoothTime = 0.15f;
    
    [Header("Transform Fade")]
    [Tooltip("Total cross-fade duration in seconds. Cat fades out while Granny fades in (or vice versa). VFX, SFX, and capsule swap fire at the midpoint (duration/2). Input is locked for the full duration. 0.6s reads as a deliberate transformation beat without feeling draggy; reduce to 0.3-0.4 for snappier, increase to 0.8-1.0 for more dramatic.")]
    [SerializeField] private float fadeDuration = 0.6f;
    [Tooltip("VFX prefab spawned at fade midpoint when transforming cat → Granny. Prefab should self-destruct when its effect completes (Particle System Stop Action = Destroy, or attached timeline that disables/destroys the GameObject).")]
    [SerializeField] private GameObject catToGrannyVFX;
    [Tooltip("VFX prefab spawned at fade midpoint when transforming Granny → cat.")]
    [SerializeField] private GameObject grannyToCatVFX;
    [Tooltip("One-shot SFX played at fade midpoint when transforming cat → Granny.")]
    [SerializeField] private AudioClip catToGrannySFX;
    [Tooltip("One-shot SFX played at fade midpoint when transforming Granny → cat.")]
    [SerializeField] private AudioClip grannyToCatSFX;
    [Tooltip("Y offset from the player root for spawning the VFX prefab. 1.0 puts the burst around chest height which reads well from the default camera framing. Adjust if the VFX prefab has its own pivot offset baked in.")]
    [SerializeField] private float vfxSpawnHeightOffset = 1.0f;
    [Tooltip("Uniform scale multiplier applied to the spawned VFX instance's localScale. Combat-tuned VFX prefabs (e.g. Mirza Beig hit bursts) are typically scaled 0.3-0.5 for melee feedback, which reads tiny on a full-body transformation moment. Bump this until both bursts read clearly on camera at default framing. Start at 2.5, expect to land between 2.0 and 4.0 depending on the prefab's baked scale.")]
    [SerializeField] private float vfxScale = 2.5f;
    [Tooltip("When true, the spawned VFX is parented to the player root so it tracks the character through the fade beat. Recommended for body-anchored bursts (smoke, sparkles, swirls) since the transformation moment lasts long enough for unparented VFX to visibly desync from the body if the player is moving. Toggle off only for world-anchored effects where a fixed-position burst reads better.")]
    [SerializeField] private bool parentVFXToPlayer = true;
    [Tooltip("Euler rotation (degrees, XYZ) applied to the spawned VFX instance. Rotation set on the prefab asset itself is NOT respected — the spawn forces this rotation so the orientation is controlled from one place. Common case: flat disc-shaped bursts ship oriented along Y (lying flat on the ground); set X=90 to stand them upright facing the camera. Leave at zero if the prefab already orients correctly.")]
    [SerializeField] private Vector3 vfxRotationEuler = Vector3.zero;
    
    [Header("Debug")]
    [Tooltip("Log a message to the console on every form transform.")]
    [SerializeField] private bool logTransforms = true;
    
    #endregion
    
    #region Private Fields
    
    private CharacterController controller;
    private ThirdPersonCamera cameraController;
    private PlayerMovement playerMovement;
    private bool isHuman;
    
    // Cached animator hashes (faster than strings). Match the Granny controller's parameter names.
    private readonly int speedHash = Animator.StringToHash("Speed");
    private readonly int isGroundedHash = Animator.StringToHash("IsGrounded");
    
    // Smoothed-speed tracking so Idle/Walk/Run transitions ease rather than snap.
    // SmoothDamp pattern matches PlayerMovement's currentSpeed/speedVelocity for the cat.
    private float currentSmoothedSpeed;
    private float speedSmoothVelocity;
    
    // Transform fade state. isTransforming gates re-entry (second T-press during fade is ignored
    // — see ToggleForm). activeFadeCoroutine is cached for potential future cancellation hooks
    // and for debug introspection.
    private bool isTransforming;
    private Coroutine activeFadeCoroutine;
    
    // SFX channel for transform one-shots. Auto-attached via RequireComponent on this GameObject,
    // with an Awake fallback to AddComponent if the attribute didn't fire on an existing prefab.
    private AudioSource audioSource;
    
    // Cached per-instance materials on both bodies. Walked once on Awake via GetComponentsInChildren
    // so the per-frame alpha lerp has a flat array to iterate — no per-frame allocation or hierarchy
    // traversal. We use .materials (instantiates per-renderer copies) NOT .sharedMaterials, which
    // would mutate the source asset and persist across play sessions in editor.
    private Material[] catMaterials;
    private Material[] grannyMaterials;
    
    // Cached _Color property ID for alpha modulation. Materials that don't expose _Color
    // (some custom shaders or image-effect shaders) are skipped silently inside SetBodyAlpha.
    private static readonly int colorPropertyID = Shader.PropertyToID("_Color");
    
    // Cached original localScale for both bodies. Captured on Awake so the cross-fade scale
    // animation respects any non-(1,1,1) authored scale on the body roots. Lerping always
    // between cached original and (original * NEAR_ZERO_SCALE) means the fade end-state
    // exactly restores the authored size, no floating-point drift after repeated toggles.
    private Vector3 catBodyOriginalScale = Vector3.one;
    private Vector3 grannyBodyOriginalScale = Vector3.one;
    
    // Floor scale during the fade. Going to literal zero can cause skinned-mesh normal
    // math to produce NaN warnings and weird tearing for one frame; 0.001 is invisible at
    // any reasonable camera distance but mathematically safe for the matrix math.
    private const float NEAR_ZERO_SCALE = 0.001f;
    
    #endregion
    
    #region Public API
    
    /// <summary>
    /// True when Granny form is active. Query this from future systems (dialogue,
    /// combat lockout, stats, persuasion ability gates, etc) to branch by current form.
    /// Flips at the START of a transform fade (not the end), so Granny's animator drive
    /// runs from frame one of her fade-in rather than waiting and rendering frozen T-pose.
    /// </summary>
    public bool IsHuman => isHuman;
    
    /// <summary>
    /// True during the cross-fade window between forms. Used by PlayerMovement to lock
    /// input for the fade duration so the transform reads as a deliberate moment rather
    /// than the player fighting controls through a half-rendered state.
    /// </summary>
    public bool IsTransforming => isTransforming;
    
    #endregion
    
    #region Unity Lifecycle
    
    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        cameraController = FindObjectOfType<ThirdPersonCamera>();
        playerMovement = GetComponent<PlayerMovement>();
        
        // AudioSource is normally guaranteed by [RequireComponent], but that attribute only
        // fires on initial AddComponent. If FormController was already attached to a prefab
        // BEFORE this attribute was added, the component won't auto-appear on recompile —
        // so we add it here as a runtime fallback. playOnAwake is force-disabled because the
        // SFX clips are one-shots fired at fade midpoints, never on scene load.
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            if (logTransforms)
                Debug.Log("[FormController] AudioSource auto-attached at runtime (was missing on prefab).");
        }
        
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
        
        // Cache per-instance materials on both bodies for the cross-fade alpha lerp.
        // Done once at Awake — walks the renderer hierarchy under each body, instantiates
        // a per-renderer material copy (.materials handles multi-material renderers correctly),
        // and flattens into a single array per body for fast iteration during the fade.
        catMaterials = CacheBodyMaterials(catBody);
        grannyMaterials = CacheBodyMaterials(grannyBody);
        
        // Cache original localScale on both body roots BEFORE Start() runs ApplyForm and
        // cycles the SetActive states. This is what we lerp to/from during the cross-fade.
        // Respects any non-(1,1,1) authored scale on the body roots in case you've tweaked
        // either body's overall size in the prefab.
        if (catBody != null) catBodyOriginalScale = catBody.transform.localScale;
        if (grannyBody != null) grannyBodyOriginalScale = grannyBody.transform.localScale;
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
        // GDD Doc 04 §4a: cannot transform during active combat. Active = a hit has been
        // exchanged in either direction (Yoru hits enemy, or enemy hits Yoru) within the
        // last engagedInCombatDuration seconds (~5s, tunable on PlayerCombat). Pre-combat
        // transform IS allowed — no hit yet = flag is false = transform goes through.
        // Environmental damage doesn't trip this flag, matching the GDD carve-out.
        //
        // Uses PlayerCombat.IsEngagedInCombat() rather than the 6 action flags
        // (IsAttacking/IsDodging/etc) because those accessors have self-heal logic that
        // returns false when a flag has been stuck — masking the "real" state at the worst
        // possible moment (a T-press during a freeze).
        var combat = GetComponent<PlayerCombat>();
        if (combat != null && combat.IsEngagedInCombat())
        {
            if (logTransforms)
                Debug.Log("[FormController] Transform BLOCKED — combat engaged (GDD Doc 04 §4a).");
            return;
        }

        // Mirror of the combat lock for conversations: Tomoe cannot transform mid-dialogue,
        // the same way Yoru cannot open one. Finish or leave the conversation first.
        if (DialogueManager.Instance != null && DialogueManager.Instance.IsDialogueActive)
        {
            if (logTransforms)
                Debug.Log("[FormController] Transform BLOCKED — dialogue active. Close the conversation first.");
            return;
        }
        
        // Re-entry guard: a second T-press during an in-flight fade is ignored. Prevents
        // double-starting the coroutine, which would corrupt material alpha state (mid-fade
        // alpha values would become the new "starting" alpha for the second fade) and could
        // leave a body permanently invisible if a fade was cancelled at the wrong frame.
        // Cleaner to lock the toggle for the brief 0.6s window.
        if (isTransforming)
        {
            if (logTransforms)
                Debug.Log("[FormController] Transform BLOCKED — fade already in progress.");
            return;
        }
        
        // Kick off the cross-fade. ApplyForm is no longer the runtime entry point —
        // it stays as the snap-swap path used by Start() for initial form setup.
        activeFadeCoroutine = StartCoroutine(TransformFadeRoutine(!isHuman));
    }
    
    /// <summary>
    /// Snap-swap form change — used only by Start() for initial form setup. The runtime
    /// transform path (T-key) goes through TransformFadeRoutine instead. Kept as a clean
    /// no-fade entry point for editor-time / Start() initialization where a 0.6s fade-in
    /// from invisible would be visually wrong (player would see nothing for half a second
    /// at game start).
    /// </summary>
    private void ApplyForm(bool toHuman)
    {
        isHuman = toHuman;
        
        // Reset smoothing baseline so the first frame in Granny form doesn't carry over
        // a stale smoothed value from the previous form session.
        currentSmoothedSpeed = 0f;
        speedSmoothVelocity = 0f;
        
        // Visual body swap (snap-swap)
        if (catBody != null) catBody.SetActive(!toHuman);
        if (grannyBody != null) grannyBody.SetActive(toHuman);
        
        ApplyCapsuleAndCameraSwap(toHuman);
        
        if (logTransforms)
        {
            Debug.Log($"[FormController] Transformed to {(toHuman ? "Granny" : "Cat")} form (snap).");
        }
    }
    
    /// <summary>
    /// CharacterController capsule + camera offset swap. Extracted from ApplyForm so the
    /// fade coroutine can call it at its midpoint — capsule swaps only when both bodies are
    /// 50/50 visible, so neither body renders inside the "wrong" collision shape long enough
    /// to be visible from camera, and any collision-resolution repositioning happens while
    /// input is locked.
    /// </summary>
    private void ApplyCapsuleAndCameraSwap(bool toHuman)
    {
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
            // Camera aim offset: Yoru's invisible head sits ~1.0 above pivot; Granny's
            // head sits ~3.0 above pivot. Without this update, zooming in (RMB or scroll)
            // converges on Granny's chest because the aim point is still at Yoru's head.
            cameraController.SetFormLookAtOffset(toHuman ? grannyCameraLookAtYOffset : catCameraLookAtYOffset);
        }
        
        // Granny speed override per GDD Doc 04 §4b. Cat form passes (0, 0) which clears
        // the override on PlayerMovement, falling back to the authored walkSpeed/runSpeed.
        // Granny form passes the tuned absolute values that match her animation's stride
        // length — when those match, her feet plant cleanly without sliding.
        // Called from both the snap-swap path (Start init) and the fade-coroutine midpoint
        // so the speed override is always in sync with the visual + capsule change.
        if (playerMovement != null)
        {
            playerMovement.SetGrannySpeed(
                toHuman ? grannyWalkSpeed : 0f,
                toHuman ? grannyRunSpeed : 0f);
        }
    }
    
    #endregion
    
    #region Transform Fade
    
    /// <summary>
    /// Cross-fade coroutine. Both bodies stay active for the fade duration with their
    /// material alpha lerped in opposite directions. At the midpoint, the capsule + camera
    /// swap fires and the VFX/SFX cues play. Input is locked for the full duration via
    /// IsTransforming, which PlayerMovement's HandleInput early-returns on.
    /// 
    /// isHuman flips at fade START so Granny's animator drive (gated on isHuman inside
    /// DriveGrannyAnimator) runs from frame one of her fade-in instead of rendering frozen.
    /// PlayerMovement only reads IsHuman for the jump gate — not for animation drive —
    /// so the cat animator continues animating naturally during its fade-out (toward Idle,
    /// since input is locked and the cat will report zero velocity).
    /// </summary>
    private IEnumerator TransformFadeRoutine(bool toHuman)
    {
        isTransforming = true;
        isHuman = toHuman;
        
        // Reset smoothing baseline so Granny's first animator-drive sample isn't
        // contaminated by stale smoothed value from the previous form session.
        currentSmoothedSpeed = 0f;
        speedSmoothVelocity = 0f;
        
        // Both bodies active for the fade window so the cross-fade is actually visible.
        if (catBody != null) catBody.SetActive(true);
        if (grannyBody != null) grannyBody.SetActive(true);
        
        // Resolve direction-specific references ONCE up front so the loop stays tight.
        Material[] outgoingMats = toHuman ? catMaterials : grannyMaterials;
        Material[] incomingMats = toHuman ? grannyMaterials : catMaterials;
        GameObject outgoingBody = toHuman ? catBody : grannyBody;
        GameObject incomingBody = toHuman ? grannyBody : catBody;
        Vector3 outgoingOriginalScale = toHuman ? catBodyOriginalScale : grannyBodyOriginalScale;
        Vector3 incomingOriginalScale = toHuman ? grannyBodyOriginalScale : catBodyOriginalScale;
        Vector3 outgoingNearZero = outgoingOriginalScale * NEAR_ZERO_SCALE;
        Vector3 incomingNearZero = incomingOriginalScale * NEAR_ZERO_SCALE;
        
        // Initialize alpha + scale: outgoing starts at full size + full alpha,
        // incoming starts at near-zero size + zero alpha (invisible either way).
        // Setting scale BEFORE the next yield ensures no frame where incoming body
        // renders at its previous full scale right after SetActive(true).
        SetBodyAlpha(outgoingMats, 1f);
        SetBodyAlpha(incomingMats, 0f);
        if (outgoingBody != null) outgoingBody.transform.localScale = outgoingOriginalScale;
        if (incomingBody != null) incomingBody.transform.localScale = incomingNearZero;
        
        float halfDuration = fadeDuration * 0.5f;
        float elapsed = 0f;
        bool midpointFired = false;
        
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / fadeDuration);
            
            // SmoothStep easing — S-curve where the fastest rate of change happens at the
            // midpoint, which is exactly when the VFX/SFX burst fires. Peak visual motion
            // coincides with the burst, hiding the moment where both bodies are mid-scale.
            // Linear lerp here felt mechanical; SmoothStep reads as a natural transformation.
            float easedT = Mathf.SmoothStep(0f, 1f, t);
            
            // Scale animation — works regardless of shader transparency, the guaranteed
            // visual fade. Outgoing shrinks toward invisible, incoming grows from invisible.
            if (outgoingBody != null)
                outgoingBody.transform.localScale = Vector3.Lerp(outgoingOriginalScale, outgoingNearZero, easedT);
            if (incomingBody != null)
                incomingBody.transform.localScale = Vector3.Lerp(incomingNearZero, incomingOriginalScale, easedT);
            
            // Alpha animation — does nothing if materials are Opaque mode (current state),
            // becomes a free bonus visual layer if any material is ever switched to Fade mode.
            SetBodyAlpha(outgoingMats, 1f - easedT);
            SetBodyAlpha(incomingMats, easedT);
            
            // Midpoint event fires ONCE at t=0.5: capsule swap + VFX + SFX cue.
            if (!midpointFired && elapsed >= halfDuration)
            {
                midpointFired = true;
                ApplyCapsuleAndCameraSwap(toHuman);
                SpawnTransformVFX(toHuman ? catToGrannyVFX : grannyToCatVFX);
                PlayTransformSFX(toHuman ? catToGrannySFX : grannyToCatSFX);
            }
            
            yield return null;
        }
        
        // Settle final state. Reset scales to exact original values (guarantees no
        // floating-point drift after repeated toggles leaves either body at 0.99x or 1.01x).
        // Outgoing body deactivated AFTER its scale is restored, so the next time it
        // becomes the incoming body, its starting scale state is clean for our near-zero
        // initialization to overwrite.
        if (incomingBody != null) incomingBody.transform.localScale = incomingOriginalScale;
        if (outgoingBody != null)
        {
            outgoingBody.transform.localScale = outgoingOriginalScale;
            outgoingBody.SetActive(false);
        }
        SetBodyAlpha(outgoingMats, 1f);
        
        // Safety: if the midpoint didn't fire (extreme frame drop where the first deltaTime
        // sample already exceeded fadeDuration), fire it now so the player can't end up in a
        // half-swapped state with cat capsule + Granny body or vice versa.
        if (!midpointFired)
        {
            ApplyCapsuleAndCameraSwap(toHuman);
            SpawnTransformVFX(toHuman ? catToGrannyVFX : grannyToCatVFX);
            PlayTransformSFX(toHuman ? catToGrannySFX : grannyToCatSFX);
        }
        
        isTransforming = false;
        activeFadeCoroutine = null;
        
        if (logTransforms)
        {
            Debug.Log($"[FormController] Transformed to {(toHuman ? "Granny" : "Cat")} form (fade complete).");
        }
    }
    
    /// <summary>
    /// Lerp the alpha component of every cached material's _Color property. Materials that
    /// don't expose _Color are skipped silently — common for custom shaders, image effects,
    /// or URP shaders using _BaseColor (this project is Built-in RP so the dominant property
    /// is _Color, but defensive skipping costs nothing and prevents shader-property errors).
    /// 
    /// CRITICAL: requires the material's shader to support transparency. Unity Standard
    /// shader's default Opaque rendering mode IGNORES alpha at render time — set the
    /// material's Rendering Mode to "Fade" in the Inspector for the fade to be visible.
    /// XFur shaders have their own transparency setup; consult the XFur docs if the cat
    /// body doesn't fade visibly while VFX + SFX + timing all fire correctly.
    /// </summary>
    private void SetBodyAlpha(Material[] mats, float alpha)
    {
        if (mats == null) return;
        for (int i = 0; i < mats.Length; i++)
        {
            Material m = mats[i];
            if (m == null) continue;
            if (!m.HasProperty(colorPropertyID)) continue;
            Color c = m.GetColor(colorPropertyID);
            c.a = alpha;
            m.SetColor(colorPropertyID, c);
        }
    }
    
    /// <summary>
    /// Walk the renderer hierarchy under a body GameObject, instantiate per-renderer
    /// material copies, and flatten into a single array for fast per-frame alpha iteration.
    /// 
    /// Uses .materials (plural, instantiates per-renderer copies) NOT .sharedMaterials —
    /// we must not mutate the source material asset (would persist across play sessions in
    /// editor and corrupt the project file). includeInactive: true so this still works on
    /// the body that's SetActive(false) at startup (Start always begins in cat form, so
    /// Granny is inactive when Awake runs).
    /// </summary>
    private Material[] CacheBodyMaterials(GameObject body)
    {
        if (body == null) return new Material[0];
        Renderer[] renderers = body.GetComponentsInChildren<Renderer>(includeInactive: true);
        var collected = new System.Collections.Generic.List<Material>();
        for (int i = 0; i < renderers.Length; i++)
        {
            // .materials instantiates per-renderer copies. We hold the references so
            // subsequent .material / .materials reads return our copies, not new instances.
            Material[] mats = renderers[i].materials;
            for (int j = 0; j < mats.Length; j++)
            {
                if (mats[j] != null) collected.Add(mats[j]);
            }
        }
        return collected.ToArray();
    }
    
    /// <summary>
    /// Spawn the transform VFX prefab at chest height on the player root. The prefab is
    /// responsible for its own lifetime (Particle System Stop Action = Destroy, or an
    /// attached timeline/script that disables/destroys the GameObject when finished).
    /// Optionally parents to the player so the burst tracks the body through the fade,
    /// applies a uniform scale multiplier so combat-tuned prefabs can read larger for
    /// the transformation beat, and forces an Inspector-controlled rotation so prefab
    /// rotation tweaks aren't silently overridden by Quaternion.identity on spawn.
    /// </summary>
    private void SpawnTransformVFX(GameObject vfxPrefab)
    {
        if (vfxPrefab == null) return;
        Vector3 spawnPos = transform.position + new Vector3(0f, vfxSpawnHeightOffset, 0f);
        Transform parent = parentVFXToPlayer ? transform : null;
        GameObject instance = Instantiate(vfxPrefab, spawnPos, Quaternion.Euler(vfxRotationEuler), parent);
        instance.transform.localScale *= vfxScale;
    }
    
    /// <summary>
    /// Play the transform SFX one-shot on the cached AudioSource. Auto-attached via
    /// [RequireComponent(typeof(AudioSource))] with a runtime AddComponent fallback in Awake,
    /// so the channel is guaranteed present. Volume, mixer routing, and spatial blend are
    /// configured in Inspector on that AudioSource component (not on FormController).
    /// </summary>
    private void PlayTransformSFX(AudioClip clip)
    {
        if (clip == null || audioSource == null) return;
        audioSource.PlayOneShot(clip);
    }
    
    #endregion
    
    #region Animator Mirror
    
    /// <summary>
    /// Drive Granny's animator using INPUT INTENT (not raw position-delta velocity).
    /// 
    /// Why intent-based: Granny's blend tree thresholds were calibrated for cat's walkSpeed
    /// and runSpeed (the position-delta values it originally received when Granny moved at
    /// cat speeds). Now that Granny's world speed is an explicit Inspector value tuned to
    /// match her animation stride, sending raw position-delta would be wrong — a position-
    /// delta of (e.g.) 1.5 would fall between idle and walk thresholds, giving partial
    /// blend. Instead we send cat's walkSpeed / runSpeed values directly when input fires,
    /// so the blend tree always picks the right clip at full weight.
    /// 
    /// Mirror of PlayerMovement's intent-based cat animator drive (targetSpeed = 0/1/2 flags).
    /// 
    /// ALSO pushes the absolute Granny speed values to PlayerMovement every FixedUpdate
    /// while in Granny form, so Inspector tuning changes propagate immediately without
    /// requiring a form transform re-press.
    /// </summary>
    private void DriveGrannyAnimator()
    {
        if (!isHuman || grannyAnimator == null) return;
        
        // Push current Inspector values to PlayerMovement EVERY frame in Granny form. This
        // makes runtime Inspector tuning take effect immediately — change Granny Walk Speed
        // from 4 to 0.2 mid-play and the next FixedUpdate uses 0.2. Without this, the
        // override is only refreshed on form transform (ApplyCapsuleAndCameraSwap).
        if (playerMovement != null)
        {
            playerMovement.SetGrannySpeed(grannyWalkSpeed, grannyRunSpeed);
        }
        
        // Read input directly. We can't use PlayerMovement's PlayerState flags because
        // PlayerMovement's HandleInput early-returns during the transform-fade lock, so
        // those flags stay stale for a moment. Reading raw input here gives current intent.
        bool hasMoveInput = Mathf.Abs(Input.GetAxisRaw("Horizontal")) > 0.1f
                         || Mathf.Abs(Input.GetAxisRaw("Vertical")) > 0.1f;
        bool isRunning = Input.GetKey(KeyCode.LeftShift);
        bool isGrounded = controller != null && controller.isGrounded;
        
        // Determine target intent. Cat's walkSpeed/runSpeed are what the blend tree was
        // calibrated against. Falls back to (2, 7) defaults if PlayerMovement is missing.
        float targetIntent;
        if (!isGrounded || !hasMoveInput)
        {
            targetIntent = 0f;
        }
        else if (isRunning)
        {
            targetIntent = playerMovement != null ? playerMovement.RunSpeed : 7f;
        }
        else
        {
            targetIntent = playerMovement != null ? playerMovement.WalkSpeed : 2f;
        }
        
        // Smooth the intent toward the target so blend tree transitions ease between
        // Idle/Walk/Run rather than step-snapping. Time.fixedDeltaTime is passed explicitly
        // since we're in FixedUpdate.
        currentSmoothedSpeed = Mathf.SmoothDamp(
            currentSmoothedSpeed, targetIntent, ref speedSmoothVelocity,
            speedSmoothTime, Mathf.Infinity, Time.fixedDeltaTime);
        
        grannyAnimator.SetFloat(speedHash, currentSmoothedSpeed);
        grannyAnimator.SetBool(isGroundedHash, isGrounded);
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
        if (playerMovement == null)
            Debug.LogWarning("[FormController] PlayerMovement not found on this GameObject. Granny speed multiplier (GDD Doc 04 §4b) will be INACTIVE — Granny will move at full Yoru speed.");
    }
    
    #endregion
}