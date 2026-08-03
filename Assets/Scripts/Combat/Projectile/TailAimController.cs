using UnityEngine;
using UnityEngine.UI;
using Unity.Cinemachine;

/// <summary>
/// Left tail air shot. Zelda style. While airborne, hold the draw key to enter a slowed aim, move
/// the mouse to angle the shot, then press the fire button to loose a straight bolt at the reticle.
/// The whole game clock is slowed (the same approach Flurry Rush uses) so enemies, projectiles and
/// gravity all slow together while the aim stays responsive. PlayerMovement is never touched.
///
/// Two clocks are handled on entry and restored exactly on every exit path:
///  - Time.fixedDeltaTime is scaled together with Time.timeScale. Without this, anything stepped
///    by the physics clock (PlayerMovement moves in FixedUpdate) ticks only a few times per real
///    second during the slow and reads as choppy frame by frame motion.
///  - The CinemachineBrain's IgnoreTimeScale switch is turned ON for the duration of the draw only,
///    so camera damping runs on real time and the mouse look stays responsive instead of dragging
///    a 1 second damp out across 10 real seconds. Nothing else about the camera is touched, and
///    outside the draw the brain is exactly as it was.
///
/// The draw clip is not frozen on a pose. It simply plays under the slow time, so a short clip
/// stretches out across the aim window and never reaches its release until you fire. This keeps
/// it clear of the hitstop system, which owns Animator.speed.
///
/// Releasing the draw key always cancels with no shot and returns everything to normal instantly.
/// Every exit (fire, cancel, landing, disable) crossfades the combat layer back to Combat_Empty,
/// the same idle state PlayerCombat's ReturnToIdle uses, so the layer releases and base locomotion
/// shows through instead of pinning the draw pose.
///
/// Setup:
///  - Put this on the player, the same object as PlayerCombat and PlayerMovement.
///  - Assign Bolt Prefab (give it the TailProjectile component and your bolt visual).
///  - Set Draw State Name to the animator state that plays Ability_LeftTail_Fast on the combat layer.
///  - Set Enemy Layer and Environment Layer to match the rest of combat.
///  - Assign Left Tail Tip, or leave it blank to auto find the bone named in Tail Tip Bone Name
///    (Tail6_L_end_end by default). The bolt spawns here and is
///    aimed toward the target, so make a small empty child of the tail tip bone if you want to
///    fine tune where it leaves the tail.
///  - Keep Aim Duration shorter than (draw clip length divided by Slow Factor) so the draw does not
///    finish before the window ends. With the defaults that is about 11s, well above the 3s window.
///  - Scene instances serialized before this version keep their old Inspector values. Check that
///    Draw Key is R and Slow Factor is 0.1 on the player after updating.
/// </summary>
public class TailAimController : MonoBehaviour
{
    #region Inspector
    [Header("Input")]
    [Tooltip("Hold this while airborne to draw and slow time. R by default. Control is hard to reach on a Mac and Control plus click is the system right click on a trackpad.")]
    [SerializeField] private KeyCode drawKey = KeyCode.R;
    [Tooltip("Mouse button that fires the bolt. 1 is the right mouse button.")]
    [SerializeField] private int fireMouseButton = 1;

    [Header("Slow Motion")]
    [Tooltip("Game time scale while aiming. 0.1 is a 10x slow. Lower is slower. The physics clock is scaled with it so the slow stays smooth.")]
    [Range(0.05f, 1f)]
    [SerializeField] private float slowFactor = 0.1f;
    [Tooltip("Real seconds you may stay in the draw before it force fires straight ahead.")]
    [SerializeField] private float aimDuration = 3f;

    [Header("Animation")]
    [Tooltip("Animator state that plays the draw (the Ability_LeftTail_Fast clip) on the combat layer. Set this to whatever that state is named in your controller.")]
    [SerializeField] private string drawStateName = "LeftTail_Fast";
    [Tooltip("Combat layer index. PlayerCombat uses 1.")]
    [SerializeField] private int combatLayerIndex = 1;
    [Tooltip("Crossfade time into the draw state.")]
    [SerializeField] private float drawCrossfade = 0.08f;
    [Tooltip("Combat layer state to crossfade back to on every exit (fire, cancel or landing). Combat_Empty is the layer's default state and the same idle PlayerCombat returns to, so the layer releases and base locomotion plays. Without this the layer stays pinned on the draw pose and Yoru looks frozen.")]
    [SerializeField] private string exitStateName = "Combat_Empty";
    [Tooltip("Crossfade time out of the draw state on exit.")]
    [SerializeField] private float exitCrossfade = 0.12f;
    [Tooltip("Turn Yoru to face the aim direction while drawing so the draw points the right way.")]
    [SerializeField] private bool faceAimWhileDrawing = true;

    [Header("Bolt")]
    [Tooltip("Prefab with a TailProjectile component. Spawned from the left tail tip on fire.")]
    [SerializeField] private GameObject boltPrefab;
    [Tooltip("Left tail tip spawn point. Auto finds the bone named below if left blank.")]
    [SerializeField] private Transform leftTailTip;
    [Tooltip("Bone name used to auto find the tail tip when Left Tail Tip is empty.")]
    [SerializeField] private string tailTipBoneName = "Tail6_L_end_end";
    [Tooltip("How far ahead the straight aim point sits when no enemy is locked.")]
    [SerializeField] private float aimRayDistance = 60f;

    [Header("Targeting")]
    [Tooltip("How far to look for an enemy to snap onto.")]
    [SerializeField] private float targetingRange = 60f;
    [Tooltip("An enemy this close to the screen centre (in pixels) gets snapped as the locked target.")]
    [SerializeField] private float snapRadiusPixels = 120f;
    [Tooltip("Layers searched for lockable enemies. Set to your Enemy layer.")]
    [SerializeField] private LayerMask enemyLayer;
    [Tooltip("Layers that block line of sight to a target (walls, terrain).")]
    [SerializeField] private LayerMask environmentMask = ~0;
    [Tooltip("Require a clear line of sight before an enemy can be locked.")]
    [SerializeField] private bool requireLineOfSight = true;

    [Header("Behaviour")]
    [Tooltip("If true, landing during the draw cancels with no shot. If false, landing force fires straight.")]
    [SerializeField] private bool landCancels = false;
    [Tooltip("Real seconds of cooldown after a shot fires. The old scheme used 3. Zero is best for testing.")]
    [SerializeField] private float cooldownAfterFire = 0f;

    [Header("Reticle")]
    [Tooltip("Optional crosshair sprite. A simple dot is generated if left blank.")]
    [SerializeField] private Sprite reticleSprite;
    [Tooltip("Optional lock marker sprite. A simple ring is generated if left blank.")]
    [SerializeField] private Sprite lockSprite;
    [SerializeField] private Color reticleColor = new Color(1f, 1f, 1f, 0.85f);
    [SerializeField] private Color reticleLockedColor = new Color(1f, 0.5f, 0.2f, 0.95f);
    [SerializeField] private Color lockMarkerColor = new Color(1f, 0.4f, 0.2f, 0.95f);
    [SerializeField] private float reticleSize = 18f;
    [SerializeField] private float lockMarkerSize = 64f;
    #endregion

    #region State
    /// <summary>True while the draw is active. Other systems can read this to stand down (for example to suppress the air spin).</summary>
    public static bool IsAiming { get; private set; }

    private Animator animator;
    private PlayerMovement playerMovement;
    private PlayerCombat playerCombat;
    private FormController formController;
    private ThirdPersonCamera thirdPersonCamera;
    private CinemachineBrain cinemachineBrain;
    private Camera mainCamera;

    private bool aiming;
    private float aimStartUnscaled;
    private float lastFireTime = -999f;

    // Clock and camera state cached on aim entry and restored exactly on every exit path.
    // fixedDeltaTime must scale with timeScale or every FixedUpdate driven system steps at a
    // visible stutter. The brain flag makes camera damping run on real time during the slow.
    private float cachedTimeScale = 1f;
    private float cachedFixedDeltaTime = 0.02f;
    private bool cachedBrainIgnoreTimeScale;

    private int drawStateHash;
    private int exitStateHash;

    private Transform lockedTarget;
    private Collider lockedCollider;

    // Reusable buffer for the target scan so aiming does not allocate every frame.
    private readonly Collider[] targetBuffer = new Collider[16];

    // Reticle UI, built at runtime as a root canvas so it never inherits the player's transform.
    private Canvas reticleCanvas;
    private Image reticleImage;
    private Image lockImage;
    #endregion

    #region Unity
    private void Awake()
    {
        animator = GetComponent<Animator>();
        playerMovement = GetComponent<PlayerMovement>();
        playerCombat = GetComponent<PlayerCombat>();
        formController = GetComponent<FormController>();
        mainCamera = Camera.main;
        thirdPersonCamera = FindObjectOfType<ThirdPersonCamera>();
        cinemachineBrain = FindObjectOfType<CinemachineBrain>();

        drawStateHash = Animator.StringToHash(drawStateName);
        exitStateHash = Animator.StringToHash(exitStateName);

        if (leftTailTip == null) FindLeftTailTip();
        BuildReticle();
    }

    private void OnDisable()
    {
        // Never leave the game stuck in slow motion if this is turned off mid aim.
        if (aiming) EndAim(safetyOnly: true);
    }

    private void OnDestroy()
    {
        // The reticle canvas is a root object, so it does not die with the player automatically.
        if (reticleCanvas != null) Destroy(reticleCanvas.gameObject);
    }

    private void Update()
    {
        // Yoru form only. In Granny form the tail abilities are disabled.
        if (formController != null && formController.IsHuman)
        {
            if (aiming) CancelAim();
            return;
        }

        if (!aiming)
        {
            if (CanStartAim() && Input.GetKeyDown(drawKey)) StartAim();
            return;
        }

        // Aiming.
        if (Input.GetMouseButtonDown(fireMouseButton)) { Fire(); return; }

        if (!Input.GetKey(drawKey)) { CancelAim(); return; }

        if (playerMovement != null && !playerMovement.IsAirborne())
        {
            if (landCancels) CancelAim(); else Fire();
            return;
        }

        if (Time.unscaledTime - aimStartUnscaled >= aimDuration) { Fire(); return; }

        UpdateLock();
    }

    private void LateUpdate()
    {
        if (!aiming) return;
        if (faceAimWhileDrawing) FaceAim();
        UpdateReticlePositions();
    }
    #endregion

    #region Aim flow
    private bool CanStartAim()
    {
        if (playerMovement == null || !playerMovement.IsAirborne()) return false;
        if (Time.unscaledTime - lastFireTime < cooldownAfterFire) return false;

        // Do not interrupt another combat action.
        if (playerCombat != null && (playerCombat.IsAttacking() || playerCombat.IsChargingHeavy()
            || playerCombat.IsDodging() || playerCombat.IsDashing()
            || playerCombat.IsGuarding() || playerCombat.IsInHitReaction()))
            return false;

        return true;
    }

    private void StartAim()
    {
        aiming = true;
        IsAiming = true;
        aimStartUnscaled = Time.unscaledTime;
        lockedTarget = null;
        lockedCollider = null;

        // Slow the game clock AND the physics clock together. PlayerMovement moves in FixedUpdate,
        // so leaving fixedDeltaTime unscaled makes her fall in visible steps instead of smoothly.
        cachedTimeScale = Time.timeScale;
        cachedFixedDeltaTime = Time.fixedDeltaTime;
        Time.timeScale = slowFactor;
        Time.fixedDeltaTime = cachedFixedDeltaTime * slowFactor;

        // Camera damping on real time for the duration of the draw only, restored on exit.
        if (cinemachineBrain != null)
        {
            cachedBrainIgnoreTimeScale = cinemachineBrain.IgnoreTimeScale;
            cinemachineBrain.IgnoreTimeScale = true;
        }

        animator.SetLayerWeight(combatLayerIndex, 1f);
        animator.CrossFadeInFixedTime(drawStateHash, drawCrossfade, combatLayerIndex);

        if (thirdPersonCamera != null) thirdPersonCamera.SetAimMode(true);
        ShowReticle(true);
    }

    private void Fire()
    {
        // Resolve the shot while the lock is still valid, then restore time before the bolt spawns
        // so it travels at full speed from its very first frame.
        Vector3 spawn = leftTailTip != null ? leftTailTip.position : transform.position + Vector3.up;
        Vector3 dir = (GetAimPoint() - spawn).normalized;

        lastFireTime = Time.unscaledTime;
        EndAim(safetyOnly: false);

        if (boltPrefab != null)
        {
            GameObject bolt = Instantiate(boltPrefab, spawn, Quaternion.LookRotation(dir));
            TailProjectile proj = bolt.GetComponent<TailProjectile>();
            if (proj != null) proj.Launch(dir);
        }
    }

    private void CancelAim()
    {
        EndAim(safetyOnly: false);
    }

    /// <summary>Shared teardown. safetyOnly is the minimal path used when disabled mid aim, restoring the clocks, the brain flag and the animator.</summary>
    private void EndAim(bool safetyOnly)
    {
        aiming = false;
        IsAiming = false;
        lockedTarget = null;
        lockedCollider = null;

        // Restore both clocks to their exact cached values. Never assume 1 and 0.02, so this stays
        // polite if some other system (Flurry Rush) had its own scale running.
        Time.timeScale = cachedTimeScale;
        Time.fixedDeltaTime = cachedFixedDeltaTime;

        // Put the brain back exactly as it was.
        if (cinemachineBrain != null)
            cinemachineBrain.IgnoreTimeScale = cachedBrainIgnoreTimeScale;

        // Always leave the combat layer on Combat_Empty, the same idle PlayerCombat returns to.
        // Restoring a cached weight is not enough: PlayerCombat keeps the layer at weight 1
        // permanently, so a pinned LeftTail_Fast pose sits on top of locomotion forever and reads
        // as a freeze. Crossfading to the empty state releases the layer and base locomotion plays.
        if (animator != null)
        {
            animator.SetLayerWeight(combatLayerIndex, 1f);
            animator.CrossFadeInFixedTime(exitStateHash, exitCrossfade, combatLayerIndex);
        }

        if (safetyOnly) return;

        if (thirdPersonCamera != null) thirdPersonCamera.SetAimMode(false);
        ShowReticle(false);
    }
    #endregion

    #region Targeting
    /// <summary>Pick the enemy nearest the screen centre within the snap radius and cache it plus its collider.</summary>
    private void UpdateLock()
    {
        lockedTarget = null;
        lockedCollider = null;
        if (mainCamera == null) return;

        int count = Physics.OverlapSphereNonAlloc(transform.position, targetingRange, targetBuffer, enemyLayer);
        if (count == 0) return;

        Vector2 screenCentre = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        float bestPixelDist = snapRadiusPixels;

        for (int i = 0; i < count; i++)
        {
            Collider col = targetBuffer[i];
            if (col == null) continue;

            EnemyHealth eh = col.GetComponentInParent<EnemyHealth>();
            if (eh != null && (eh.IsDead() || eh.IsInvulnerable)) continue;

            Vector3 worldCentre = col.bounds.center;
            Vector3 screenPos = mainCamera.WorldToScreenPoint(worldCentre);
            if (screenPos.z <= 0f) continue; // behind the camera

            float pixelDist = Vector2.Distance(new Vector2(screenPos.x, screenPos.y), screenCentre);
            if (pixelDist > bestPixelDist) continue;

            if (requireLineOfSight && !HasLineOfSight(worldCentre)) continue;

            bestPixelDist = pixelDist;
            lockedTarget = col.transform;
            lockedCollider = col;
        }
    }

    private bool HasLineOfSight(Vector3 targetPoint)
    {
        Vector3 eye = transform.position + Vector3.up * 0.6f;
        if (Physics.Linecast(eye, targetPoint, out RaycastHit hit, environmentMask, QueryTriggerInteraction.Ignore))
        {
            if (hit.transform.root != transform.root) return false; // an obstacle blocks the line
        }
        return true;
    }

    /// <summary>The world point the bolt should fly toward: the locked enemy, or a point straight ahead.</summary>
    private Vector3 GetAimPoint()
    {
        if (lockedTarget != null)
            return lockedCollider != null ? lockedCollider.bounds.center : lockedTarget.position;

        if (mainCamera != null)
        {
            Ray ray = mainCamera.ScreenPointToRay(new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f));
            if (Physics.Raycast(ray, out RaycastHit hit, aimRayDistance, environmentMask, QueryTriggerInteraction.Ignore))
                return hit.point;
            return ray.origin + ray.direction * aimRayDistance;
        }

        return transform.position + transform.forward * aimRayDistance;
    }

    private void FaceAim()
    {
        if (mainCamera == null) return;
        Vector3 flat = mainCamera.transform.forward;
        flat.y = 0f;
        if (flat.sqrMagnitude < 0.0001f) return;
        transform.rotation = Quaternion.LookRotation(flat);
    }
    #endregion

    #region Reticle UI
    private void BuildReticle()
    {
        // Root object, no parent. Parenting the canvas under the player made a ScreenSpaceOverlay
        // canvas inherit the player transform, which pushed the crosshair off screen centre and
        // onto Yoru herself.
        GameObject canvasGo = new GameObject("TailAimReticle");
        reticleCanvas = canvasGo.AddComponent<Canvas>();
        reticleCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        reticleCanvas.sortingOrder = 500;

        CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        Sprite dot = reticleSprite != null ? reticleSprite : MakeCircleSprite(64, 0f);
        Sprite ring = lockSprite != null ? lockSprite : MakeCircleSprite(64, 0.72f);

        reticleImage = MakeImage("Crosshair", dot, reticleColor, reticleSize);
        lockImage = MakeImage("LockMarker", ring, lockMarkerColor, lockMarkerSize);

        ShowReticle(false);
    }

    private Image MakeImage(string imageName, Sprite sprite, Color color, float size)
    {
        GameObject go = new GameObject(imageName);
        go.transform.SetParent(reticleCanvas.transform, false);
        Image img = go.AddComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.raycastTarget = false;
        RectTransform rt = img.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(size, size);
        rt.anchoredPosition = Vector2.zero;
        return img;
    }

    private void ShowReticle(bool show)
    {
        if (reticleCanvas != null) reticleCanvas.enabled = show;
        if (!show && lockImage != null) lockImage.enabled = false;
    }

    private void UpdateReticlePositions()
    {
        if (reticleImage != null)
            reticleImage.color = lockedTarget != null ? reticleLockedColor : reticleColor;

        if (lockImage == null) return;

        if (lockedTarget != null && mainCamera != null)
        {
            Vector3 worldCentre = lockedCollider != null ? lockedCollider.bounds.center : lockedTarget.position;
            Vector3 screenPos = mainCamera.WorldToScreenPoint(worldCentre);
            if (screenPos.z > 0f)
            {
                lockImage.enabled = true;
                lockImage.rectTransform.position = new Vector3(screenPos.x, screenPos.y, 0f);
            }
            else
            {
                lockImage.enabled = false;
            }
        }
        else
        {
            lockImage.enabled = false;
        }
    }

    /// <summary>Generate a simple sprite. inner01 of 0 makes a filled dot, above 0 makes a ring.</summary>
    private Sprite MakeCircleSprite(int size, float inner01)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        float c = (size - 1) * 0.5f;
        float outer = c;
        float innerR = inner01 * outer;
        Color clear = new Color(1f, 1f, 1f, 0f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(c, c));
                bool on = d <= outer && d >= innerR;
                tex.SetPixel(x, y, on ? Color.white : clear);
            }
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }
    #endregion

    #region Helpers
    /// <summary>Locate the tail tip bone to spawn the bolt from, by exact name first, then by best guess.</summary>
    private void FindLeftTailTip()
    {
        Transform[] bones = GetComponentsInChildren<Transform>();

        // Exact match on the configured bone name.
        foreach (Transform t in bones)
        {
            if (t.name == tailTipBoneName) { leftTailTip = t; return; }
        }

        // Fallback: prefer the deepest left tail bone, since tip bones carry the most _end suffixes.
        Transform best = null;
        int bestDepth = -1;
        foreach (Transform t in bones)
        {
            if (!t.name.Contains("Tail") || !t.name.Contains("_L")) continue;

            int depth = 0;
            for (Transform p = t; p != null; p = p.parent) depth++;
            if (depth > bestDepth) { bestDepth = depth; best = t; }
        }

        leftTailTip = best;
        if (leftTailTip == null)
            Debug.LogWarning("[TailAimController] No left tail tip bone found. Assign Left Tail Tip in the Inspector.");
    }
    #endregion
}