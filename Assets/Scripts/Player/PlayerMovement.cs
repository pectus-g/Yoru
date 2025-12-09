using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float walkSpeed = 3f;
    [SerializeField] private float runSpeed = 6f;
    [SerializeField] private float rotationSpeed = 10f;
    
    [Header("Jump Settings")]
    [SerializeField] private float jumpHeight = 2f;
    [SerializeField] private float doubleJumpHeight = 1.5f;
    [SerializeField] private float tripleJumpHeight = 1.2f;      // NEW
    [SerializeField] private bool allowDoubleJump = true;
    [SerializeField] private bool allowTripleJump = true;        // NEW
    
    [Header("Gravity")]
    [SerializeField] private float gravity = -15f;
    [SerializeField] private float groundCheckDistance = 0.2f;
    [SerializeField] private LayerMask groundLayer;
    
    private CharacterController controller;
    private ThirdPersonCamera cameraController;
    private Vector3 velocity;
    private bool isGrounded;
    private int jumpCount = 0;
    
    private void Start()
    {
        controller = GetComponent<CharacterController>();
        cameraController = FindObjectOfType<ThirdPersonCamera>();
        
        NavMeshAgent agent = GetComponent<NavMeshAgent>();
        if (agent != null) agent.enabled = false;
    }
    
    private void Update()
    {
        if (InventoryUI.Instance != null && InventoryUI.Instance.IsInventoryOpen())
        {
            return;
        }
        
        // Ground check
        isGrounded = Physics.Raycast(transform.position, Vector3.down, 
            groundCheckDistance + 0.1f, groundLayer);
        
        if (isGrounded && velocity.y < 0)
        {
            velocity.y = -2f;
            jumpCount = 0;
        }
        
        // === JUMP - CHANGED TO DIRECT SPACEBAR ===
        if (Input.GetKeyDown(KeyCode.Space))
        {
            if (isGrounded && jumpCount == 0)
            {
                Jump(jumpHeight);
            }
            else if (allowDoubleJump && jumpCount == 1)
            {
                Jump(doubleJumpHeight);
            }
            else if (allowTripleJump && jumpCount == 2)    // NEW
            {
                Jump(tripleJumpHeight);
            }
        }
        
        // === MOVEMENT (UNTOUCHED) ===
        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");
        
        if (Mathf.Abs(horizontal) > 0.1f || Mathf.Abs(vertical) > 0.1f)
        {
            Vector3 cameraForward = cameraController != null ? 
                cameraController.GetCameraForward() : transform.forward;
            Vector3 cameraRight = cameraController != null ? 
                cameraController.GetCameraRight() : transform.right;
            
            Vector3 moveDirection = (cameraForward * vertical + cameraRight * horizontal).normalized;
            
            float currentSpeed = Input.GetKey(KeyCode.LeftShift) ? runSpeed : walkSpeed;
            
            controller.Move(moveDirection * currentSpeed * Time.deltaTime);
            
            if (moveDirection.magnitude > 0.1f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(moveDirection);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, 
                    Time.deltaTime * rotationSpeed);
            }
        }
        
        // Gravity
        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
    }
    
    private void Jump(float height)
    {
        velocity.y = Mathf.Sqrt(height * 2f * -gravity);
        jumpCount++;
    }
}