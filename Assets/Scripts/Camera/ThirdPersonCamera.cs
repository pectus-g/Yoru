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
    
    [Header("References")]
    [SerializeField] private Transform playerTransform;
    [SerializeField] private CinemachineCamera virtualCamera;
    
    private float yaw = 0f;
    private float pitch = 0f;
    private bool cameraEnabled = true;
    
    private CinemachineFollow followComponent;
    private CinemachineHardLookAt lookAtComponent;
    
    // Form height offset: written by FormController on transform to lift the camera for Granny's
    // taller silhouette. Cat form uses 0. See SetFormHeightOffset.
    private float formHeightOffset = 0f;
    
    // Zoom state
    private float defaultDistance;
    private float targetDistance;
    private float currentDistance;
    private float zoomVelocity;
    
    // Double-tap detection
    private float lastRightClickTime = -1f;
    
    // DOF
    private DepthOfField dofSettings;
    private bool dofWasActive;
    
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
        
        if (playerTransform != null)
        {
            yaw = playerTransform.eulerAngles.y;
        }
        
        defaultDistance = cameraDistance;
        targetDistance = cameraDistance;
        currentDistance = cameraDistance;
        
        // Find DOF settings from any PostProcessVolume in the scene
        if (autoDofControl)
        {
            PostProcessVolume volume = FindObjectOfType<PostProcessVolume>();
            if (volume != null && volume.profile != null)
            {
                volume.profile.TryGetSettings(out dofSettings);
                if (dofSettings != null)
                {
                    dofWasActive = dofSettings.active;
                    Debug.Log("[Camera] DOF auto-control enabled, will disable when zoomed in");
                }
            }
        }
    }
    
    private void OnDestroy()
    {
        // Restore DOF to its original state when this script is destroyed (exiting play mode)
        if (dofSettings != null)
            dofSettings.active = dofWasActive;
    }
    
    private void LateUpdate()
    {
        if (!cameraEnabled || playerTransform == null) return;
        
        if (InventoryUI.Instance != null && InventoryUI.Instance.IsInventoryOpen())
        {
            return;
        }
        
        // === Double-tap right-click detection (zoom reset) ===
        if (Input.GetMouseButtonDown(1))
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
        
        // Right-click to rotate camera
        if (Input.GetMouseButton(1))
        {
            float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
            float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;
            
            yaw += mouseX;
            pitch -= mouseY;
            pitch = Mathf.Clamp(pitch, minVerticalAngle, maxVerticalAngle);
        }
        
        // === Scroll wheel zoom — ONLY when right-click is NOT held ===
        if (!Input.GetMouseButton(1))
        {
            float rawScroll = Input.mouseScrollDelta.y;
            if (Mathf.Abs(rawScroll) > 0.01f)
            {
                float scroll = Mathf.Clamp(rawScroll, -1f, 1f);
                targetDistance -= scroll * zoomSpeed;
                targetDistance = Mathf.Clamp(targetDistance, minZoomDistance, maxZoomDistance);
            }
        }
        
        // Smooth zoom interpolation
        currentDistance = Mathf.SmoothDamp(currentDistance, targetDistance, ref zoomVelocity, zoomSmoothTime);
        
        // === DOF control — OFF when zoomed in, ON when zoomed out ===
        if (autoDofControl && dofSettings != null)
        {
            dofSettings.active = currentDistance >= dofDisableDistance;
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
    /// Called by FormController on cat ↔ Granny transform so Granny's taller silhouette frames
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
    /// when zooming in. Called by FormController on cat ↔ Granny transform.
    /// </summary>
    public void SetFormLookAtOffset(float yOffset)
    {
        if (lookAtComponent != null)
        {
            Vector3 offset = lookAtComponent.LookAtOffset;
            offset.y = yOffset;
            lookAtComponent.LookAtOffset = offset;
        }
    }
}