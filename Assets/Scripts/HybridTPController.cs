using UnityEngine;
using UnityEngine.AI;

[AddComponentMenu("Controller/Hybrid Third-Person (Terrain + NavMesh)")]
[RequireComponent(typeof(CharacterController))]
public class HybridTPController : MonoBehaviour
{
    public Transform cameraTransform;                 // if null -> Camera.main

    [Header("Manual (WASD)")]
    public float walkSpeed = 5f;
    public float sprintSpeed = 8f;
    public float turnSpeed = 720f;                   // deg/sec
    public float inputDeadzone = 0.15f;

    [Header("Jump & Gravity")]
    public float gravity = -30f;
    public float groundedGravity = -2f;
    public float jumpHeight = 2.0f;                  // first jump
    public float airJumpHeight = 1.6f;               // subsequent jumps
    [Tooltip("1 = double jump, 2 = triple, etc.")]
    public int extraAirJumps = 1;

    [Header("Ground Probe (robust on Terrain)")]
    public LayerMask groundLayers = ~0;
    [Range(0.6f, 1f)] public float probeRadiusMul = 0.95f;
    public float probeUpOffset = 0.05f;

    [Header("Click-to-Move (NavMesh)")]
    public bool enableClickToMove = true;
    public KeyCode clickButton = KeyCode.Mouse1;     // Right click
    public float rayMaxDist = 300f;
    public LayerMask clickRayMask = ~0;              // ground/terrain layers

    // --- internals
    CharacterController cc;
    NavMeshAgent agent;                               // optional
    Vector3 velocity;                                 // vertical velocity only
    int airJumpsUsed = 0;
    bool isGrounded;

    void Awake()
    {
        cc = GetComponent<CharacterController>();
        agent = GetComponent<NavMeshAgent>();

        // Use camera.main if not assigned
        if (!cameraTransform && Camera.main) cameraTransform = Camera.main.transform;

        // Good default capsule
        if (cc.height < 1.2f) cc.height = 2f;
        if (cc.radius < 0.3f) cc.radius = 0.5f;
        var c = cc.center; c.y = cc.height * 0.5f; cc.center = c;

        // If there is an agent, let US move the body via CharacterController
        if (agent)
        {
            agent.updatePosition = false;
            agent.updateRotation = false;
            agent.autoBraking = true;
        }
    }

    void Update()
    {
        // --- 1) Gather inputs
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");
        bool sprint = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        bool jumpPressed = Input.GetButtonDown("Jump");

        // --- 2) Manual camera-relative move vector
        Vector3 manualDir = Vector3.zero;
        if (cameraTransform)
        {
            Vector3 camF = cameraTransform.forward; camF.y = 0f; camF.Normalize();
            Vector3 camR = cameraTransform.right;   camR.y = 0f; camR.Normalize();
            manualDir = (camF * v + camR * h);
        }
        else manualDir = new Vector3(h, 0f, v);

        float manualMag = manualDir.magnitude;
        if (manualMag > 1f) manualDir.Normalize();

        // --- 3) Click-to-move (set agent destination)
        if (enableClickToMove && agent && Input.GetKeyDown(clickButton))
        {
            var cam = cameraTransform ? cameraTransform.GetComponent<Camera>() : Camera.main;
            if (cam)
            {
                Ray ray = cam.ScreenPointToRay(Input.mousePosition);
                if (Physics.Raycast(ray, out var hit, rayMaxDist, clickRayMask, QueryTriggerInteraction.Ignore))
                {
                    if (NavMesh.SamplePosition(hit.point, out var sample, 2.0f, NavMesh.AllAreas))
                    {
                        agent.isStopped = false;
                        agent.SetDestination(sample.position);
                    }
                }
            }
        }

        // If player gives manual input, cancel agent path
        if (agent && manualMag > inputDeadzone)
        {
            agent.ResetPath();
            agent.isStopped = true;
        }

        // --- 4) Decide horizontal velocity (manual vs agent)
        Vector3 horiz = Vector3.zero;

        if (manualMag > inputDeadzone)
        {
            float speed = sprint ? sprintSpeed : walkSpeed;
            horiz = manualDir * speed;

            // Rotate toward manual direction
            Quaternion target = Quaternion.LookRotation(manualDir, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, target, turnSpeed * Time.deltaTime);
        }
        else if (agent && agent.hasPath)
        {
            // Use agent desired velocity but move with CharacterController
            Vector3 desired = agent.desiredVelocity;
            desired.y = 0f;
            float speed = desired.magnitude;

            if (speed > 0.001f)
            {
                horiz = desired; // already scaled by agent acceleration/speed
                Quaternion target = Quaternion.LookRotation(desired.normalized, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, target, turnSpeed * Time.deltaTime);
            }

            // When close enough, stop
            if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.05f)
            {
                agent.ResetPath();
                agent.isStopped = true;
            }

            // Keep agent synced to our position
            agent.nextPosition = transform.position;
        }

        // --- 5) Grounding + jumps (robust on Terrain)
        isGrounded = cc.isGrounded || ProbeGround();
        if (isGrounded)
        {
            if (velocity.y < 0f) velocity.y = groundedGravity;
            airJumpsUsed = 0;

            if (jumpPressed)
                velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
        }
        else
        {
            if (jumpPressed && airJumpsUsed < extraAirJumps)
            {
                velocity.y = Mathf.Sqrt(airJumpHeight * -2f * gravity);
                airJumpsUsed++;
            }
            velocity.y += gravity * Time.deltaTime;
        }

        // --- 6) Apply motion
        Vector3 motion = horiz + Vector3.up * velocity.y;
        cc.Move(motion * Time.deltaTime);
    }

    bool ProbeGround()
    {
        Vector3 centerWS = transform.TransformPoint(cc.center);
        float bottomOffset = cc.height * 0.5f - cc.radius;
        Vector3 bottom = centerWS + Vector3.down * bottomOffset;
        Vector3 probePos = bottom + Vector3.up * probeUpOffset;
        float r = cc.radius * probeRadiusMul;

        return Physics.CheckSphere(probePos, r, groundLayers, QueryTriggerInteraction.Ignore);
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (!cc) cc = GetComponent<CharacterController>();
        if (!cc) return;
        Vector3 centerWS = transform.TransformPoint(cc.center);
        float bottomOffset = cc.height * 0.5f - cc.radius;
        Vector3 bottom = centerWS + Vector3.down * bottomOffset;
        Vector3 probePos = bottom + Vector3.up * probeUpOffset;
        float r = cc.radius * probeRadiusMul;
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(probePos, r);
    }
#endif
}
