using UnityEngine;

/// <summary>
/// Keeps a rain emitter centred on the player and sitting exactly on the ground.
///
/// Why this exists: the Ultimate VFX rain prefabs only cover about 8x8 units and
/// are authored with the root AT ground level - ripples, splashes and ground water
/// sit within 0.1 of the root, and the rain emitter is 3 units above it. They also
/// use scalingMode: Shape, so scaling the object moves the emitter without scaling
/// particle speed, which makes the drops die in mid-air.
///
/// So: never scale it, never guess its height. Raycast down and put it on whatever
/// the ground actually is. The particles simulate in world space
/// (moveWithTransform: 0), so moving the emitter does not drag existing drops.
/// </summary>
[DisallowMultipleComponent]
public class RainFollow : MonoBehaviour
{
    [Tooltip("Usually the player. Left empty, the object tagged 'Player' is used.")]
    [SerializeField] private Transform target;

    [Tooltip("The downward ray starts this far above the target.")]
    [SerializeField] private float rayStartHeight = 5f;

    [SerializeField] private float rayLength = 80f;

    [Tooltip("What counts as ground. Default is everything.")]
    [SerializeField] private LayerMask groundMask = ~0;

    [Tooltip("Used only if the ray hits nothing.")]
    [SerializeField] private float fallbackY = 2f;

    [Tooltip("Lifts the emitter slightly so ripples don't z-fight with the floor.")]
    [SerializeField] private float groundOffset = 0.02f;

    [Tooltip("0 = snap instantly. Higher = smoother but the rain lags behind you.")]
    [SerializeField, Range(0f, 20f)] private float followSmoothing = 0f;

    [SerializeField] private bool debugLog = true;

    private bool warnedNoTarget;
    private bool loggedFirstHit;

    private void Start()
    {
        if (target == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) target = p.transform;
        }

        if (target == null && debugLog)
            Debug.LogWarning("[RainFollow] No target and nothing tagged 'Player'. Rain will not move.");
    }

    private void LateUpdate()
    {
        if (target == null)
        {
            if (!warnedNoTarget && debugLog)
            {
                warnedNoTarget = true;
                Debug.LogWarning("[RainFollow] No target assigned.");
            }
            return;
        }

        Vector3 p = target.position;
        float groundY = fallbackY;

        Vector3 rayOrigin = p + Vector3.up * rayStartHeight;
        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit,
                            rayLength, groundMask, QueryTriggerInteraction.Ignore))
        {
            groundY = hit.point.y;

            if (!loggedFirstHit && debugLog)
            {
                loggedFirstHit = true;
                Debug.Log($"[RainFollow] Ground found at y={groundY:F2} on '{hit.collider.name}'. " +
                          $"Rain emitter placed there.");
            }
        }
        else if (!loggedFirstHit && debugLog)
        {
            loggedFirstHit = true;
            Debug.LogWarning($"[RainFollow] Ray hit nothing below the player - using fallbackY {fallbackY}. " +
                             $"Check that your floor has a collider.");
        }

        Vector3 wanted = new Vector3(p.x, groundY + groundOffset, p.z);

        transform.position = followSmoothing <= 0f
            ? wanted
            : Vector3.Lerp(transform.position, wanted, 1f - Mathf.Exp(-followSmoothing * Time.deltaTime));
    }
}
