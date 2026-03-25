using UnityEngine;
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
    
    [Header("Ground Collision")]
    [Tooltip("Prevent camera from going underground")]
    [SerializeField] private bool preventUnderground = true;
    [Tooltip("Minimum height above ground the camera is allowed")]
    [SerializeField] private float minHeightAboveGround = 0.5f;
    [Tooltip("Layers the camera collides with (set to terrain/environment layers)")]
    [SerializeField] private LayerMask groundLayers = ~0;
    
    [Header("References")]
    [SerializeField] private Transform playerTransform;
    [SerializeField] private CinemachineCamera virtualCamera;
    
    private float yaw = 0f;
    private float pitch = 0f;
    private bool cameraEnabled = true;
    
    private CinemachineFollow followComponent;
    
    // Zoom state
    private float defaultDistance;
    private float targetDistance;
    private float currentDistance;
    private float zoomVelocity;
    
    // Double-tap detection
    private float lastRightClickTime = -1f;
    
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
        }
        
        if (playerTransform != null)
        {
            yaw = playerTransform.eulerAngles.y;
        }
        
        defaultDistance = cameraDistance;
        targetDistance = cameraDistance;
        currentDistance = cameraDistance;
    }
    
    private void LateUpdate()
    {
        if (!cameraEnabled || playerTransform == null) return;
        
        if (InventoryUI.Instance != null && InventoryUI.Instance.IsInventoryOpen())
        {
            return;
        }
        
        // === Double-tap right-click detection (zoom reset) ===
        // Must be checked BEFORE the rotation block so we detect the tap
        if (Input.GetMouseButtonDown(1))
        {
            if (Time.unscaledTime - lastRightClickTime < doubleTapWindow)
            {
                // Double-tap detected — reset zoom
                targetDistance = defaultDistance;
                lastRightClickTime = -1f; // reset so triple-click doesn't re-trigger
            }
            else
            {
                lastRightClickTime = Time.unscaledTime;
            }
        }
        
        // Right-click to rotate camera (unchanged from original)
        if (Input.GetMouseButton(1))
        {
            float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
            float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;
            
            yaw += mouseX;
            pitch -= mouseY;
            pitch = Mathf.Clamp(pitch, minVerticalAngle, maxVerticalAngle);
        }
        
        // === Scroll wheel zoom ===
        // Clamp raw scroll to ±1 — MX Master free-spin sends values up to 25+ per frame
        float rawScroll = Input.mouseScrollDelta.y;
        if (Mathf.Abs(rawScroll) > 0.01f)
        {
            float scroll = Mathf.Clamp(rawScroll, -1f, 1f);
            targetDistance -= scroll * zoomSpeed;
            targetDistance = Mathf.Clamp(targetDistance, minZoomDistance, maxZoomDistance);
        }
        
        // Smooth zoom interpolation
        currentDistance = Mathf.SmoothDamp(currentDistance, targetDistance, ref zoomVelocity, zoomSmoothTime);
        
        // Calculate camera offset
        if (followComponent != null)
        {
            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 direction = rotation * Vector3.back;
            
            Vector3 offset = direction * currentDistance + Vector3.up * cameraHeight;
            
            // Ground collision: prevent camera from going underground
            if (preventUnderground && playerTransform != null)
            {
                Vector3 worldCamPos = playerTransform.position + offset;
                
                Vector3 rayOrigin = new Vector3(worldCamPos.x, playerTransform.position.y + 50f, worldCamPos.z);
                if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 100f, groundLayers))
                {
                    float groundY = hit.point.y + minHeightAboveGround;
                    if (worldCamPos.y < groundY)
                    {
                        offset.y += groundY - worldCamPos.y;
                    }
                }
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
}