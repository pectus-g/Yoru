using UnityEngine;
using Unity.Cinemachine;

public class ThirdPersonCamera : MonoBehaviour
{
    [Header("Camera Settings")]
    [SerializeField] private float mouseSensitivity = 2f;
    [SerializeField] private float minVerticalAngle = -30f;
    [SerializeField] private float maxVerticalAngle = 60f;
    [SerializeField] private float cameraDistance = 6f;      // NEW - Adjustable!
    [SerializeField] private float cameraHeight = 3f;        // NEW - Adjustable!
    
    [Header("References")]
    [SerializeField] private Transform playerTransform;
    [SerializeField] private CinemachineCamera virtualCamera;
    
    private float yaw = 0f;
    private float pitch = 0f;
    private bool cameraEnabled = true;
    
    private CinemachineFollow followComponent;
    
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
    }
    
    private void LateUpdate()
    {
        if (!cameraEnabled || playerTransform == null) return;
        
        if (InventoryUI.Instance != null && InventoryUI.Instance.IsInventoryOpen())
        {
            return;
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
        
        // Update camera position based on angles
        if (followComponent != null)
        {
            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 direction = rotation * Vector3.back;
            
            // NOW USES ADJUSTABLE VARIABLES!
            followComponent.FollowOffset = direction * cameraDistance + Vector3.up * cameraHeight;
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
