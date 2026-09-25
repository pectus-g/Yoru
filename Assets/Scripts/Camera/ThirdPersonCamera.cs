using UnityEngine;
using UnityEngine.Rendering.PostProcessing;
using Unity.Cinemachine;

public class ThirdPersonCamera : MonoBehaviour
{
    [Header("Camera Settings")]
    [SerializeField] private float mouseSensitivity = 2f;
    [SerializeField] private float minVerticalAngle = -30f;
    [SerializeField] private float maxVerticalAngle = 60f;
    [SerializeField] private float cameraDistance = 6f;
    [SerializeField] private float cameraHeight = 3f;
    
    [Header("Zoom Settings")]
    [Tooltip("How far each scroll tick moves the camera (before smoothing)")]
    [SerializeField] private float zoomSpeed = 0.5f;
    [Tooltip("Closest the camera can zoom in")]
    [SerializeField] private float minZoomDistance = 2f;
    [Tooltip("Farthest the camera can zoom out")]
    [SerializeField] private float maxZoomDistance = 15f;
    [Tooltip("How smoothly the camera moves to the target zoom (lower = snappier)")]
    [SerializeField] private float zoomSmoothTime = 0.15f;
    [Tooltip("Time window to detect double-tap right-click for zoom reset")]
    [SerializeField] private float doubleTapWindow = 0.3f;
    
    [Header("Depth of Field")]
    [Tooltip("Automatically disable DOF when zoomed in closer than this distance")]
    [SerializeField] private bool autoDofControl = true;
    [Tooltip("DOF turns OFF below this distance, ON above it")]
    [SerializeField] private float dofDisableDistance = 4f;
    
    [Header("Ground Clamp")]
    [Tooltip("Lowest the camera may sit, measured above the player's footing. At low pitch the " +
             "orbit math would otherwise place the camera below the ground and bury it. Raise this " +
             "value if the camera grazes the ground when you tilt down to look up at Yoru.")]
    [SerializeField] private float minHeightAboveGround = 0.5f;
    
    [Header("Sky Peek — round 64 (testing aid)")]
    [Tooltip("ROUND 64 — hold the key below and the camera's AIM point glides up above Yoru, tilting the lens to the SKY (for checking the weather). Release = glides back to exactly what it was. The camera's POSITION never moves: floor clamp, deoccluder, zoom and damping are untouched. Untick to disable the key completely.")]
    [SerializeField] private bool skyPeekEnabled = true;
    [Tooltip("The hold-to-look-up key.")]
    [SerializeField] private KeyCode skyPeekKey = KeyCode.U;
    [Tooltip("How high above Yoru the aim point rises, metres. Higher = steeper look at the sky.")]
    [SerializeField] private float skyPeekHeight = 14f;
    [Tooltip("Seconds for the tilt up (and back down).")]
    [SerializeField] private float skyPeekTime = 0.35f;

    [Header("References")]
    [SerializeField] private Transform playerTransform;
    [SerializeField] private CinemachineCamera virtualCamera;
    
    private float yaw = 0f;
    private float pitch = 0f;
    private bool cameraEnabled = true;

    // Aim mode: while true the mouse rotates the camera without holding right-click, and right-click
    // is left alone for the tail shot to consume as its fire button. Set by TailAimController.
    private bool aimModeActive = false;
    
    private CinemachineFollow followComponent;
    private CinemachineHardLookAt lookAtComponent;
    
    // Form height offset: written by FormController on transform to lift the camera for Granny's
    // taller silhouette. Cat form uses 0. See SetFormHeightOffset.
    private float formHeightOffset = 0f;

    // ROUND 64 — sky peek state. The peek rides ADDITIVELY on the form's own aim height
    // (lookAtBaseY), so a form change mid-peek stays correct and the release restores exactly.
    private float skyPeekAmount = 0f;   // 0 = normal aim, 1 = fully tilted to the sky
    private float lookAtBaseY = 1f;     // the aim's own height (form-dependent), tracked below
    
    // Zoom state
    private float defaultDistance;
    private float targetDistance;
    private float currentDistance;
    private float zoomVelocity;
    
    // Double-tap detection
    private float lastRightClickTime = -1f;
    
    // DOF: every Depth Of Field that was switched on in the scene's post-processing volumes at Start.
    // ROUND 91 (25 Sep 2026, step 7d): the camera used to take only the FIRST volume it found. After
    // the combat managers moved (step 7c) the hit pulse's own volume came first, so the day's depth of
    // field was never switched off and close-ups blurred. A depth of field that is off in its profile
    // (the cave's) is never touched.
    private readonly System.Collections.Generic.List<DepthOfField> dofSettings = new System.Collections.Generic.List<DepthOfField>();
    
    private void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        
        if (playerTransform == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) playerTransform = player.transform;
        }
        
        if (virtualCamera == null)
        {
            virtualCamera = FindObjectOfType<CinemachineCamera>();
        }
        
        if (virtualCamera != null)
        {
            followComponent = virtualCamera.GetComponent<CinemachineFollow>();
            lookAtComponent = virtualCamera.GetComponent<CinemachineHardLookAt>();
        }

        // ROUND 64 — remember the aim's own height so the sky peek can ride on it and restore it.
        if (lookAtComponent != null)
            lookAtBaseY = lookAtComponent.LookAtOffset.y;
        
        if (playerTransform != null)
        {
            yaw = playerTransform.eulerAngles.y;
        }
        
        defaultDistance = cameraDistance;
        targetDistance = cameraDistance;
        currentDistance = cameraDistance;
        
        // Find every depth of field that is switched on in the scene's post-processing volumes
        // (round 91). Volumes without one, like the hit pulse's, are skipped.
        if (autoDofControl)
        {
            var dofNames = new System.Text.StringBuilder();
            foreach (PostProcessVolume volume in FindObjectsByType<PostProcessVolume>(FindObjectsSortMode.None))
            {
                PostProcessProfile source = volume.HasInstantiatedProfile() ? volume.profile : volume.sharedProfile;
                DepthOfField switchedOn;
                if (source == null || !source.TryGetSettings(out switchedOn) || !switchedOn.active)
                    continue;

                // The volume's own copy of its profile, as before: the profile file is never changed.
                DepthOfField dof;
                if (!volume.profile.TryGetSettings(out dof))
                    continue;

                dofSettings.Add(dof);
                if (dofNames.Length > 0) dofNames.Append(", ");
                dofNames.Append(volume.name);
            }

            if (dofSettings.Count > 0)
                Debug.Log("[Camera] DOF auto-control on " + dofSettings.Count + " volume(s): " + dofNames + ". Off below " + dofDisableDistance + " m, on above.");
            else
                Debug.Log("[Camera] DOF auto-control: no depth of field switched on in this scene, nothing to control.");
        }
    }
    
    private void OnDestroy()
    {
        // Restore DOF to its original state when this script is destroyed (exiting play mode):
        // every one this camera controls was switched on at Start.
        foreach (DepthOfField dof in dofSettings)
        {
            if (dof != null)
                dof.active = true;
        }
    }
    
    private void LateUpdate()
    {
        if (!cameraEnabled || playerTransform == null) return;
        
        if (InventoryUI.Instance != null && InventoryUI.Instance.IsInventoryOpen())
        {
            return;
        }
        
        // === Double-tap right-click detection (zoom reset) ===
        // Skipped while aiming so the fire click does not reset the zoom.
        if (!aimModeActive && Input.GetMouseButtonDown(1))
        {
            if (Time.unscaledTime - lastRightClickTime < doubleTapWindow)
            {
                targetDistance = defaultDistance;
                lastRightClickTime = -1f;
            }
            else
            {
                lastRightClickTime = Time.unscaledTime;
            }
        }
        
        // Right-click to rotate camera.
        // While aiming the mouse rotates the view freely without holding right-click,
        // so the player can angle the shot while right-click stays free to fire.
        if (aimModeActive || Input.GetMouseButton(1))
        {
            float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
            float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;
            
            yaw += mouseX;
            pitch -= mouseY;
            pitch = Mathf.Clamp(pitch, minVerticalAngle, maxVerticalAngle);
        }
        
        // === Scroll wheel zoom — ONLY when right-click is NOT held and not aiming ===
        if (!aimModeActive && !Input.GetMouseButton(1))
        {
            float rawScroll = Input.mouseScrollDelta.y;
            if (Mathf.Abs(rawScroll) > 0.01f)
            {
                float scroll = Mathf.Clamp(rawScroll, -1f, 1f);
                targetDistance -= scroll * zoomSpeed;
                targetDistance = Mathf.Clamp(targetDistance, minZoomDistance, maxZoomDistance);
            }
        }
        
        // === SKY PEEK (round 64, testing aid) ===
        // Hold the key: the AIM point (not the camera) glides Sky Peek Height metres above Yoru —
        // the lens tilts up to the sky. Release: glides back to the form's own aim height.
        // Skipped while the tail-shot aim owns the view. Writes only while moving or held, so
        // the normal frame path costs nothing when idle.
        if (skyPeekEnabled && lookAtComponent != null)
        {
            bool wantPeek = !aimModeActive && Input.GetKey(skyPeekKey);
            float peekTarget = wantPeek ? 1f : 0f;
            if (wantPeek || !Mathf.Approximately(skyPeekAmount, peekTarget))
            {
                skyPeekAmount = Mathf.MoveTowards(skyPeekAmount, peekTarget,
                    Time.unscaledDeltaTime / Mathf.Max(0.05f, skyPeekTime));
                Vector3 lo = lookAtComponent.LookAtOffset;
                lo.y = lookAtBaseY + skyPeekAmount * skyPeekHeight;
                lookAtComponent.LookAtOffset = lo;
            }
        }

        // Smooth zoom interpolation
        currentDistance = Mathf.SmoothDamp(currentDistance, targetDistance, ref zoomVelocity, zoomSmoothTime);
        
        // === DOF control: OFF when zoomed in, ON when zoomed out (every one found at Start) ===
        if (autoDofControl && dofSettings.Count > 0)
        {
            bool dofOn = currentDistance >= dofDisableDistance;
            for (int i = 0; i < dofSettings.Count; i++)
            {
                if (dofSettings[i] != null)
                    dofSettings[i].active = dofOn;
            }
        }
        
        // Calculate camera offset
        // Horizontal distance stays CONSTANT regardless of pitch
        if (followComponent != null)
        {
            Vector3 horizontalBack = Quaternion.Euler(0f, yaw, 0f) * Vector3.back;
            float pitchHeight = Mathf.Sin(pitch * Mathf.Deg2Rad) * currentDistance;
            
            Vector3 offset = horizontalBack * currentDistance + Vector3.up * (cameraHeight + formHeightOffset + pitchHeight);
            
            // Ground clamp: at low (negative) pitch, sin(pitch) is negative and pulls the vertical
            // offset down. With a tall enough cameraHeight or a far enough zoom this drives the
            // camera below the player's footing and buries it underground. Clamp the vertical offset
            // so the camera is never placed below the pivot by more than this floor allows.
            // Sideways and overhead occluders (walls, trees, cave roofs) are handled separately by
            // the Cinemachine Deoccluder on the virtual camera, so this only guards the floor.
            if (offset.y < minHeightAboveGround)
            {
                offset.y = minHeightAboveGround;
            }
            
            followComponent.FollowOffset = offset;
        }
    }
    
    public void SetCameraEnabled(bool enabled)
    {
        cameraEnabled = enabled;
    }

    /// <summary>
    /// Enter or leave aim mode. While active the mouse rotates the camera without holding
    /// right-click, and right-click is left free for the tail shot to fire. Called by
    /// TailAimController when the draw starts and ends.
    /// </summary>
    public void SetAimMode(bool active)
    {
        aimModeActive = active;
    }
    
    public Vector3 GetCameraForward()
    {
        Vector3 forward = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
        return forward;
    }
    
    public Vector3 GetCameraRight()
    {
        Vector3 right = Quaternion.Euler(0f, yaw, 0f) * Vector3.right;
        return right;
    }
    
    /// <summary>
    /// Adjust camera follow height by a runtime offset (added to cameraHeight in the offset calc).
    /// Called by FormController on cat → Granny transform so Granny's taller silhouette frames
    /// the same way as cat-Yoru. Pass 0 to reset (cat form).
    /// </summary>
    public void SetFormHeightOffset(float offset)
    {
        formHeightOffset = offset;
    }

    /// <summary>
    /// Adjust the CinemachineHardLookAt vertical aim offset for the active form.
    /// The aim point is (target.position + LookAtOffset). In cat form this should sit at
    /// Yoru's head (~1.0 above the player root pivot). In Granny form Yoru's head is well
    /// below Granny's head, so without this update the camera converges on Granny's chest
    /// when zooming in. Called by FormController on cat → Granny transform.
    /// </summary>
    public void SetFormLookAtOffset(float yOffset)
    {
        lookAtBaseY = yOffset;   // ROUND 64: the sky peek rides ON TOP of the form's aim height
        if (lookAtComponent != null)
        {
            Vector3 offset = lookAtComponent.LookAtOffset;
            offset.y = yOffset + skyPeekAmount * skyPeekHeight;
            lookAtComponent.LookAtOffset = offset;
        }
    }
}
