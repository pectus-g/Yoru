using UnityEngine;

public class UltraSimpleCameraController : MonoBehaviour
{
    [Header("Settings")]
    public float mouseSensitivity = 2f;
    public float smoothing = 2f;
    
    [Header("References")]
    public Transform target;
    public Vector3 offset = new Vector3(0, 2, -5);
    
    private Vector2 mouseLook;
    private Vector2 smoothV;
    
    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
    }
    
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Cursor.lockState = Cursor.lockState == CursorLockMode.Locked ? 
                CursorLockMode.None : CursorLockMode.Locked;
        }
        
        if (Cursor.lockState == CursorLockMode.Locked)
        {
            Vector2 mouseInput = new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y"));
            mouseInput = Vector2.Scale(mouseInput, new Vector2(mouseSensitivity, mouseSensitivity));
            
            smoothV.x = Mathf.Lerp(smoothV.x, mouseInput.x, 1f / smoothing);
            smoothV.y = Mathf.Lerp(smoothV.y, mouseInput.y, 1f / smoothing);
            
            mouseLook += smoothV;
            mouseLook.y = Mathf.Clamp(mouseLook.y, -90f, 90f);
            
            if (target != null)
            {
                Quaternion rotation = Quaternion.Euler(-mouseLook.y, mouseLook.x, 0);
                transform.position = target.position + rotation * offset;
                transform.LookAt(target.position + Vector3.up * 1.5f);
            }
        }
    }
}