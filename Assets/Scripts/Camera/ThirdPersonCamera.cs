using UnityEngine;
using Unity.Cinemachine;

public class ThirdPersonCamera : MonoBehaviour
{
    [Header("Camera Rotation Settings")]
    [Tooltip("Mouse sensitivity for camera rotation")]
    [SerializeField] private float mouseSensitivity = 2f;
    
    [Tooltip("How far down you can look (negative = downward)")]
    [SerializeField] private float minVerticalAngle = -30f;
    
    [Tooltip("How far up you can look (positive = upward)")]
    [SerializeField] private float maxVerticalAngle = 60f;
    
    [Tooltip("How much vertical look affects camera tilt (higher = more dramatic)")]
    [SerializeField] private float verticalLookMultiplier = 0.05f;
    
    [Header("References")]
    [Tooltip("Drag PlayerYoru GameObject here")]
    [SerializeField] private Transform playerTransform;
    
    [Tooltip("Drag CM vcam_FollowYoru here")]
    [SerializeField] private CinemachineCamera cinemachineCamera;
    
    // Private variables
    private float currentRotationY = 0f;   // Horizontal rotation (around Y axis)
    private float currentRotationX = 0f;   // Vertical rotation (up/down)
    private bool cameraEnabled = true;
    private CinemachineHardLookAt hardLookAt;
    private Vector3 originalLookAtOffset;
    
    private void Start()
    {
        // Lock and hide cursor at start
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        
        // Auto-find player if not assigned
        if (playerTransform == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                playerTransform = player.transform;
                Debug.Log("ThirdPersonCamera: Found Player automatically");
            }
            else
            {
                Debug.LogError("ThirdPersonCamera: No Player found! Make sure Player has 'Player' tag!");
            }
        }
        
        // Auto-find Cinemachine camera if not assigned
        if (cinemachineCamera == null)
        {
            cinemachineCamera = FindObjectOfType<CinemachineCamera>();
            if (cinemachineCamera != null)
            {
                Debug.Log("ThirdPersonCamera: Found Cinemachine Camera automatically");
            }
            else
            {
                Debug.LogError("ThirdPersonCamera: No Cinemachine Camera found in scene!");
            }
        }
        
        // Get Hard Look At component
        if (cinemachineCamera != null)
        {
            hardLookAt = cinemachineCamera.GetComponent<CinemachineHardLookAt>();
            if (hardLookAt != null)
            {
                // Store original look at offset
                originalLookAtOffset = hardLookAt.LookAtOffset;
                Debug.Log($"ThirdPersonCamera: Hard Look At found. Original offset: {originalLookAtOffset}");
            }
            else
            {
                Debug.LogWarning("ThirdPersonCamera: No CinemachineHardLookAt component found on virtual camera!");
            }
        }
        
        // Initialize rotation to match player's current rotation
        if (playerTransform != null)
        {
            currentRotationY = playerTransform.eulerAngles.y;
        }
        
        // Initialize vertical rotation to 0 (looking straight)
        currentRotationX = 0f;
    }
    
    private void LateUpdate()
    {
        // Don't update camera if disabled or player doesn't exist
        if (!cameraEnabled || playerTransform == null) 
        {
            return;
        }
        
        // Don't rotate camera when inventory is open
        if (InventoryUI.Instance != null && InventoryUI.Instance.IsInventoryOpen())
        {
            return;
        }
        
        // Only rotate camera when right mouse button is held
        if (Input.GetMouseButton(1))
        {
            // Get mouse input
            float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
            float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;
            
            // === HORIZONTAL ROTATION (Left/Right) ===
            currentRotationY += mouseX;
            
            // Rotate the player to face the camera direction
            playerTransform.rotation = Quaternion.Euler(0f, currentRotationY, 0f);
            
            // === VERTICAL ROTATION (Up/Down) ===
            currentRotationX -= mouseY; // Subtract because mouse Y is inverted
            
            // Clamp vertical rotation between min and max angles
            currentRotationX = Mathf.Clamp(currentRotationX, minVerticalAngle, maxVerticalAngle);
            
            // Apply vertical rotation to camera look offset
            if (hardLookAt != null)
            {
                // Calculate new Y offset based on vertical rotation
                float verticalOffset = originalLookAtOffset.y + (currentRotationX * verticalLookMultiplier);
                
                // Apply to Hard Look At offset
                hardLookAt.LookAtOffset = new Vector3(
                    originalLookAtOffset.x,
                    verticalOffset,
                    originalLookAtOffset.z
                );
            }
        }
    }
    
    /// <summary>
    /// Enable or disable camera rotation (called by InventoryUI)
    /// </summary>
    public void SetCameraEnabled(bool enabled)
    {
        cameraEnabled = enabled;
        
        if (enabled)
        {
            Debug.Log("ThirdPersonCamera: Camera rotation enabled");
        }
        else
        {
            Debug.Log("ThirdPersonCamera: Camera rotation disabled");
        }
    }
    
    /// <summary>
    /// Reset camera rotation to default forward position
    /// </summary>
    public void ResetCameraRotation()
    {
        currentRotationX = 0f;
        
        if (playerTransform != null)
        {
            currentRotationY = playerTransform.eulerAngles.y;
        }
        
        if (hardLookAt != null)
        {
            hardLookAt.LookAtOffset = originalLookAtOffset;
        }
        
        Debug.Log("ThirdPersonCamera: Camera rotation reset");
    }
    
    /// <summary>
    /// Get current vertical rotation angle (for debugging)
    /// </summary>
    public float GetVerticalAngle()
    {
        return currentRotationX;
    }
    
    /// <summary>
    /// Get current horizontal rotation angle (for debugging)
    /// </summary>
    public float GetHorizontalAngle()
    {
        return currentRotationY;
    }
}
