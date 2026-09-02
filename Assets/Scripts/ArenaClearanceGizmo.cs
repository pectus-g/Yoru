using UnityEngine;

/// <summary>
/// ARENA CLEARANCE GIZMO — an editor-only ruler for shaping the boss arena.
///
/// Drop this on an empty GameObject placed where the boss fight happens (the same
/// object you will later use as the cinematic stage mark). In the Scene view it draws
/// the rings that must stay empty for the phase-2 cinematic camera to work:
///
///   RED ring    — nothing may stand here. The camera orbits INSIDE this circle.
///   YELLOW ring — the camera's outer reach; keep it mostly open.
///   GREEN ring  — where the arena wall may begin.
///   BLUE column — the clear air above the middle (he jumps high, and the
///                 lightning has to be visible up there).
///
/// SELECT the object and it also scans the scene: anything solid standing inside the
/// red ring gets a red box drawn around it and its name written next to it — so you
/// can see exactly which pieces to move instead of guessing distances.
///
/// Costs the game nothing: gizmos are editor-only and are stripped from builds.
/// </summary>
[DisallowMultipleComponent]
public class ArenaClearanceGizmo : MonoBehaviour
{
    [Header("Rings — metres, world space")]
    [Tooltip("NOTHING may stand inside this circle — the cinematic camera orbits in here. 14 is the working number for the Oni fight.")]
    public float clearRadius = 14f;
    [Tooltip("The camera's outer reach. Keep it mostly open; small props are tolerable.")]
    public float cameraRadius = 20f;
    [Tooltip("Where the arena wall may begin.")]
    public float arenaRadius = 25f;
    [Tooltip("Clear air needed straight above the middle — he jumps high and the lightning happens up there.")]
    public float clearHeight = 15f;

    [Header("Blocker scan — runs only while this object is SELECTED")]
    [Tooltip("Draw a red box around anything solid standing inside the red ring.")]
    public bool scanBlockers = true;
    [Tooltip("Only things reaching within this height above the mark count as blockers (a rock far below the floor cannot block the camera).")]
    public float blockerHeightBand = 12f;
    [Tooltip("Ignore anything smaller than this across (weeds, grass, small ivy — they do not block a camera).")]
    public float ignoreSmallerThan = 1.5f;
    [Tooltip("Write each blocker's name in the Scene view.")]
    public bool showNames = true;

    private void OnDrawGizmos()
    {
        Vector3 c = transform.position;

        DrawCircle(c, clearRadius,  new Color(1f, 0.25f, 0.2f, 0.95f));
        DrawCircle(c, cameraRadius, new Color(1f, 0.85f, 0.2f, 0.75f));
        DrawCircle(c, arenaRadius,  new Color(0.3f, 1f, 0.45f, 0.6f));

        // the column of clear air above the fight
        Gizmos.color = new Color(0.4f, 0.8f, 1f, 0.7f);
        Gizmos.DrawLine(c, c + Vector3.up * clearHeight);
        DrawCircle(c + Vector3.up * clearHeight, 2.5f, new Color(0.4f, 0.8f, 1f, 0.7f));

        // a small cross at the mark itself
        Gizmos.color = Color.white;
        Gizmos.DrawLine(c + Vector3.left * 1f, c + Vector3.right * 1f);
        Gizmos.DrawLine(c + Vector3.back * 1f, c + Vector3.forward * 1f);
    }

    private void OnDrawGizmosSelected()
    {
        if (!scanBlockers) return;
        Vector3 c = transform.position;
        int hits = 0;

        var renderers = FindObjectsByType<Renderer>(FindObjectsSortMode.None);
        foreach (var r in renderers)
        {
            if (r == null || !r.enabled) continue;
            if (r.transform.IsChildOf(transform)) continue;

            Bounds b = r.bounds;
            if (b.size.magnitude < Mathf.Max(0.1f, ignoreSmallerThan)) continue;   // weeds and grass
            if (b.min.y > c.y + blockerHeightBand) continue;                       // hanging far overhead
            if (b.max.y < c.y - 2f) continue;                                      // buried below the floor

            // horizontal distance from the mark to the object's box
            float dx = Mathf.Max(0f, Mathf.Max(b.min.x - c.x, c.x - b.max.x));
            float dz = Mathf.Max(0f, Mathf.Max(b.min.z - c.z, c.z - b.max.z));
            float d = Mathf.Sqrt(dx * dx + dz * dz);
            if (d > clearRadius) continue;

            hits++;
            Gizmos.color = new Color(1f, 0.2f, 0.15f, 0.9f);
            Gizmos.DrawWireCube(b.center, b.size);
#if UNITY_EDITOR
            if (showNames)
                UnityEditor.Handles.Label(b.center + Vector3.up * (b.extents.y + 0.5f),
                                          $"{r.gameObject.name}  ({d:F1}m)");
#endif
        }

#if UNITY_EDITOR
        UnityEditor.Handles.Label(c + Vector3.up * 2f,
            hits == 0 ? $"CLEAR — nothing inside {clearRadius:F0}m"
                      : $"{hits} blocker(s) inside the {clearRadius:F0}m ring");
#endif
    }

    private void DrawCircle(Vector3 center, float radius, Color color)
    {
        if (radius <= 0.01f) return;
        Gizmos.color = color;
        const int STEPS = 72;
        Vector3 prev = center + new Vector3(radius, 0f, 0f);
        for (int i = 1; i <= STEPS; i++)
        {
            float a = i * Mathf.PI * 2f / STEPS;
            Vector3 p = center + new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius);
            Gizmos.DrawLine(prev, p);
            prev = p;
        }
    }
}
