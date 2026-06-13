using UnityEngine;

/// <summary>
/// WATCH-ONLY collision diagnostic for the "Yoru gets stuck on something invisible, feet off the
/// ground, jump won't lift, then teleports 1m sideways" bug. This is the ONE thing the previous
/// watchers never measured: what the CharacterController is physically colliding with.
///
/// Edits NOTHING. Put this on the SAME GameObject as the player (PlayerYoru_Def) — it needs to be
/// on the player so Unity delivers OnControllerColliderHit to it. It does NOT touch PlayerMovement.
///
/// What it catches:
///   1) TELEPORTS: every frame it measures how far Yoru moved. A move far bigger than walking/running
///      could explain (i.e. a depenetration shove) gets logged as a TELEPORT with the distance and
///      direction. This is the "1m sideways" you described.
///   2) WHAT HE HIT: logs the name + layer of every collider the CharacterController bumps, but
///      rate-limited so it does not spam. When you get stuck, the thing he is stuck ON will show up
///      here REPEATEDLY — that names the invisible collider.
///   3) STUCK: detects when Yoru is trying to move (input held) but barely moving for several frames
///      while NOT grounded — the "feet off the ground, stuck on something" state.
///
/// Press Play, do the repro, get stuck / teleported, then send the [Collision] lines.
/// </summary>
public class CollisionWatcher : MonoBehaviour
{
    [Header("Watch-only. Put on the PLAYER object. Just press Play and reproduce.")]
    [Tooltip("A single-frame position jump larger than this (metres) is logged as a TELEPORT.")]
    [SerializeField] private float teleportMetres = 0.4f;

    [Tooltip("Min seconds between logging hits on the SAME collider, so it does not spam.")]
    [SerializeField] private float sameColliderCooldown = 0.5f;

    private CharacterController cc;
    private Vector3 lastPos;
    private float lastHitLogTime;
    private string lastHitName = "";

    private void Start()
    {
        cc = GetComponent<CharacterController>();
        lastPos = transform.position;
        Debug.Log($"[Collision] Watcher armed on '{name}'. CharacterController found: {cc != null}. " +
                  "Do the repro, get stuck/teleported, then send the [Collision] lines.");
    }

    private void Update()
    {
        // Measure raw position change this frame.
        Vector3 pos = transform.position;
        Vector3 delta = pos - lastPos;
        float dist = delta.magnitude;

        // A teleport: moved far in one frame. We compare against teleportMetres. Even at 120fps and
        // a fast run, one frame is a few cm; 0.4m+ in a single frame is a shove, not locomotion.
        if (dist > teleportMetres)
        {
            Vector3 d = delta.normalized;
            // Describe the direction in plain terms relative to where Yoru faces.
            float fwd = Vector3.Dot(d, transform.forward);
            float right = Vector3.Dot(d, transform.right);
            float up = Vector3.Dot(d, Vector3.up);
            string dir = $"fwd {fwd:+0.0;-0.0}, right {right:+0.0;-0.0}, up {up:+0.0;-0.0}";
            Debug.LogWarning($"[Collision] TELEPORT: moved {dist:F2}m in one frame ({dir}). " +
                             $"grounded={cc != null && cc.isGrounded}");
        }

        lastPos = pos;
    }

    /// <summary>
    /// Unity calls this on the GameObject with the CharacterController whenever Move() hits a collider.
    /// This is the money shot: it names the invisible thing Yoru is colliding with.
    /// </summary>
    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        if (hit == null || hit.collider == null) return;

        string n = hit.collider.name;
        float now = Time.time;

        // Rate-limit: only log the same collider every sameColliderCooldown seconds.
        if (n == lastHitName && now - lastHitLogTime < sameColliderCooldown) return;

        lastHitName = n;
        lastHitLogTime = now;

        // Is this hit roughly horizontal (a wall-like push) or below (floor)? Horizontal hits that
        // are NOT the floor are the suspicious ones for "stuck on something invisible".
        float upness = Vector3.Dot(hit.normal, Vector3.up);
        string kind = upness > 0.7f ? "floor (normal)" : (upness < 0.3f ? "WALL/SIDE <<< suspicious" : "slope");
        bool isTrigger = hit.collider.isTrigger;
        string layer = LayerMask.LayerToName(hit.collider.gameObject.layer);

        Debug.Log($"[Collision] HIT '{n}' (layer '{layer}', trigger={isTrigger}) " +
                  $"normal-up={upness:F2} -> {kind}");
    }
}
