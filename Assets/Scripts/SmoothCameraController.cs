using UnityEngine;
using Cinemachine;

public class SmoothCameraController : MonoBehaviour
{
    [Header("Mouse Look Settings")]
    public float mouseSensitivity = 100f;
    public bool invertYAxis = false;
    public float smoothTime = 0.1f;
    
    [Header("Control Settings")]
    [Tooltip("Hold right mouse button to control vertical camera movement")]
    public bool requireRightClickForVertical = true;
    
    [Header("Angle Limits")]
    public float maxLookAngle = 80f;
    public float minLookAngle = -80f;
    
    [Header("Auto-Follow Settings")]
    public bool enableAutoFollow = true;
    public float autoFollowDelay = 2f;
    public float autoFollowSpeed = 50f;
    
    [Header("References")]
    public CinemachineFreeLook freeLookCamera;
    
    // Private variables
    private float mouseX;
    private float mouseY;
    private Vector2 currentMouseDelta;
    private Vector2 targetMouseDelta;
    private float lastInputTime;
    private bool isAutoFollowing;
    
    void Start()
    {
        if (freeLookCamera == null)
            freeLookCamera = FindObjectOfType<CinemachineFreeLook>();
            
        if (freeLookCamera != null)
        {
            SetupFreeLookCamera();
        }
        
        // Lock cursor initially (player can press ESC to unlock)
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
    
    void SetupFreeLookCamera()
    {
        // Configure the FreeLook camera for smooth movement
        freeLookCamera.m_XAxis.m_MaxSpeed = 0f; // We'll control this manually
        freeLookCamera.m_YAxis.m_MaxSpeed = 0f; // We'll control this manually
        
        // Set smooth acceleration/deceleration
        freeLookCamera.m_XAxis.m_AccelTime = 0.1f;
        freeLookCamera.m_XAxis.m_DecelTime = 0.1f;
        freeLookCamera.m_YAxis.m_AccelTime = 0.1f;
        freeLookCamera.m_YAxis.m_DecelTime = 0.1f;
        
        // Enable input handling
        freeLookCamera.m_XAxis.m_InputAxisName = "";
        freeLookCamera.m_YAxis.m_InputAxisName = "";
    }
    
    void Update()
    {
        HandleInput();
        HandleCameraMovement();
        HandleCursorLock();
    }
    
    void HandleInput()
    {
        targetMouseDelta = Vector2.zero;
        
        if (Cursor.lockState == CursorLockMode.Locked)
        {
            // Get mouse input
            float rawMouseX = Input.GetAxis("Mouse X");
            float rawMouseY = Input.GetAxis("Mouse Y");
            
            // Only move camera if there's actual mouse movement
            bool hasHorizontalInput = Mathf.Abs(rawMouseX) > 0.001f;
            bool hasVerticalInput = Mathf.Abs(rawMouseY) > 0.001f;
            
            // Apply right-click requirement for vertical movement if enabled
            if (requireRightClickForVertical && hasVerticalInput)
            {
                hasVerticalInput = Input.GetMouseButton(1);
            }
            
            // Only apply input if there's actual movement
            if (hasHorizontalInput || hasVerticalInput)
            {
                mouseX = hasHorizontalInput ? rawMouseX * mouseSensitivity * Time.deltaTime : 0f;
                mouseY = hasVerticalInput ? rawMouseY * mouseSensitivity * Time.deltaTime : 0f;
                
                if (invertYAxis)
                    mouseY = -mouseY;
                
                targetMouseDelta = new Vector2(mouseX, mouseY);
            }
        }
    }
    
    void HandleCameraMovement()
    {
        if (freeLookCamera == null) return;
        
        // Only apply movement if there's actual input
        if (targetMouseDelta.magnitude > 0.001f)
        {
            freeLookCamera.m_XAxis.Value += targetMouseDelta.x;
            freeLookCamera.m_YAxis.Value = Mathf.Clamp(freeLookCamera.m_YAxis.Value + targetMouseDelta.y, 0f, 1f);
        }
        // No smoothing, no residual movement - camera stops immediately when no input
    }
    
    void HandleCursorLock()
    {
        // Toggle cursor lock with ESC key
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (Cursor.lockState == CursorLockMode.Locked)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }
        
        // Re-lock cursor when clicking in game (but not right-click if it's used for camera)
        if (Cursor.lockState == CursorLockMode.None)
        {
            bool shouldLock = Input.GetMouseButtonDown(0);
            // Don't lock on right-click if it's used for vertical camera control
            if (!requireRightClickForVertical)
            {
                shouldLock = shouldLock || Input.GetMouseButtonDown(1);
            }
            
            if (shouldLock)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }
    }
    
    // Public methods
    public void SetMouseSensitivity(float sensitivity)
    {
        mouseSensitivity = sensitivity;
    }
    
    public void SetInvertY(bool invert)
    {
        invertYAxis = invert;
    }
    
    public void EnableAutoFollow(bool enable)
    {
        enableAutoFollow = enable;
    }
    
    public void SetRequireRightClickForVertical(bool require)
    {
        requireRightClickForVertical = require;
    }
    
    public void ResetCamera()
    {
        if (freeLookCamera != null && freeLookCamera.Follow != null)
        {
            // Reset to behind the target
            Vector3 targetForward = freeLookCamera.Follow.forward;
            float targetAngle = Mathf.Atan2(targetForward.x, targetForward.z) * Mathf.Rad2Deg + 180f;
            freeLookCamera.m_XAxis.Value = targetAngle;
            freeLookCamera.m_YAxis.Value = 0.5f;
        }
    }
    
    void OnValidate()
    {
        mouseSensitivity = Mathf.Clamp(mouseSensitivity, 1f, 500f);
        smoothTime = Mathf.Clamp(smoothTime, 0.01f, 1f);
        maxLookAngle = Mathf.Clamp(maxLookAngle, 0f, 90f);
        minLookAngle = Mathf.Clamp(minLookAngle, -90f, 0f);
        autoFollowDelay = Mathf.Clamp(autoFollowDelay, 0.5f, 10f);
        autoFollowSpeed = Mathf.Clamp(autoFollowSpeed, 10f, 200f);
    }
}