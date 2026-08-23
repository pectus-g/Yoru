using UnityEngine;

/// <summary>
/// ROUND 29 — the Oni's swing wave, as something that actually travels.
///
/// The wave used to be an instant distance check at the strike moment: if Yoru happened to be
/// inside a radius, she took damage that same frame. That is invisible by nature — there is
/// nothing to see leave the club and nothing to dodge — and it went completely dormant once he
/// was sped up, because his club started reaching her on nearly every swing.
///
/// This carries the slash visual outward from the club instead, and hurts her when the wave
/// itself arrives. His reach becomes something you can watch coming and step out of.
///
/// Collision is deliberately NOT a physics query. It measures the distance from Yoru's body to
/// the SEGMENT the wave swept this frame, so a fast wave cannot skip past her between frames, and
/// there are no layer masks involved — a mask that silently matched nothing is what killed the
/// launch for weeks, and this needs no mask to be correct.
/// </summary>
public class SwingWaveProjectile : MonoBehaviour
{
    private Transform target;
    private PlayerHealth targetHealth;
    private Transform attacker;

    private Vector3 direction;
    private Vector3 startPos;
    private float speed;
    private float maxDistance;
    private float hitRadius;
    private float bodyHeight;
    private int damage;

    private GameObject hitVFX;
    private float hitVFXLifetime;
    private float hitVFXOffset;

    private bool spent;
    private GameObject visualGO;

    /// <summary>
    /// Spawns a wave carrying `visual` and sends it along `dir`. Pass damage 0 for a wave that is
    /// purely cosmetic — used when the club already connected, so the two can never both charge
    /// Yoru for the same swing.
    /// </summary>
    public static SwingWaveProjectile Launch(GameObject visual, Vector3 origin, Vector3 dir,
                                             Transform attacker, Transform target, PlayerHealth targetHealth,
                                             float speed, float maxDistance, float hitRadius, float bodyHeight,
                                             int damage, GameObject hitVFX, float hitVFXLifetime, float hitVFXOffset,
                                             float visualLifetime, Vector3 visualTilt, float visualPlaybackSpeed)
    {
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) dir = Vector3.forward;
        dir.Normalize();

        var go = new GameObject("OniSwingWave");
        go.transform.position = origin;
        go.transform.rotation = Quaternion.LookRotation(dir);

        GameObject visualInstance = null;
        if (visual != null)
        {
            visualInstance = Instantiate(visual, origin, Quaternion.LookRotation(dir));
            visualInstance.transform.SetParent(go.transform, true);   // keeps its authored size

            // ROUND 33: tilt, applied AFTER parenting so it is an offset from the direction of
            // travel rather than a world angle. 0,0,0 faces straight down the wave's path;
            // rolling Z is what lays a slash diagonally across the swing instead of flat.
            visualInstance.transform.localRotation = Quaternion.Euler(visualTilt);

            // ROUND 33: destroying an effect later cannot make a short one last longer — a burst
            // that finishes in 0.3s is over at 0.3s whenever it is deleted. Slowing its SIMULATION
            // is what actually stretches it: 0.5 plays it at half speed, so it takes twice as long.
            if (visualPlaybackSpeed > 0f && !Mathf.Approximately(visualPlaybackSpeed, 1f))
            {
                foreach (var ps in visualInstance.GetComponentsInChildren<ParticleSystem>(true))
                {
                    var main = ps.main;
                    main.simulationSpeed = main.simulationSpeed * visualPlaybackSpeed;
                }
            }
            // ROUND 31: the visual gets its OWN lifetime, independent of how long the wave flies.
            // Before this it was a child of the wave, so when the wave finished its travel the
            // whole object was destroyed and the effect was cut off mid-animation — a 2 second
            // slash was being killed after 0.29s and never had a chance to be seen.
            if (visualLifetime > 0f) Destroy(visualInstance, visualLifetime);
        }

        var w = go.AddComponent<SwingWaveProjectile>();
        w.direction = dir;
        w.startPos = origin;
        w.attacker = attacker;
        w.target = target;
        w.targetHealth = targetHealth;
        w.speed = Mathf.Max(0f, speed);
        w.maxDistance = Mathf.Max(0.1f, maxDistance);
        w.hitRadius = Mathf.Max(0.1f, hitRadius);
        w.bodyHeight = bodyHeight;
        w.damage = damage;
        w.hitVFX = hitVFX;
        w.hitVFXLifetime = hitVFXLifetime;
        w.hitVFXOffset = hitVFXOffset;
        w.visualGO = visualInstance;

        // A wave that never reaches its range still has to clean itself up.
        Destroy(go, speed > 0.01f ? (maxDistance / speed) + 1.5f : 3f);
        return w;
    }

    /// <summary>
    /// ROUND 31. Let the effect go before this object dies, so it finishes playing where it got
    /// to instead of vanishing with the wave. Covers every path out: reaching its range, hitting
    /// her, or the safety timer.
    /// </summary>
    private void OnDestroy()
    {
        if (visualGO != null) visualGO.transform.SetParent(null, true);
    }

    private void Update()
    {
        Vector3 prev = transform.position;
        Vector3 next = prev + direction * speed * Time.deltaTime;
        transform.position = next;

        if (!spent && target != null)
        {
            Vector3 body = target.position + Vector3.up * bodyHeight;
            if (DistanceToSegment(body, prev, next) <= hitRadius)
            {
                spent = true;
                Connect(body);
            }
        }

        Vector3 flown = transform.position - startPos;
        flown.y = 0f;
        if (flown.sqrMagnitude >= maxDistance * maxDistance) Destroy(gameObject);
    }

    private void Connect(Vector3 body)
    {
        // damage 0 = the club already hit her this swing; the wave passes through as visuals only.
        if (damage <= 0 || targetHealth == null) return;

        Vector3 from = transform.position;
        Vector3 contact = body;
        Collider col = target != null ? target.GetComponent<Collider>() : null;
        if (col != null && col.enabled)
        {
            Vector3 p = col.ClosestPoint(from);
            if ((p - from).sqrMagnitude > 0.0001f) contact = p;
        }

        Vector3 approach = from - contact;
        if (approach.sqrMagnitude < 0.0001f) approach = -direction;
        approach.Normalize();

        if (hitVFX != null)
        {
            GameObject fx = Instantiate(hitVFX, contact + approach * hitVFXOffset,
                                        Quaternion.LookRotation(approach));
            if (hitVFXLifetime > 0f) Destroy(fx, hitVFXLifetime);
        }

        // feedbackOnly = false, so this interrupts her and plays the full hit reaction — being
        // caught by the wave should stop what she is doing, not just tick her health down.
        Vector3 reactFrom = attacker != null ? attacker.position : transform.position;
        targetHealth.TakeDamage(damage, false, reactFrom, false);

        Debug.Log($"[OniBoss:Wave] wave reached Yoru for {damage} after "
                + $"{Vector3.Distance(startPos, transform.position):F1}m of travel.");

        Destroy(gameObject);
    }

    /// <summary>Shortest distance from a point to the segment the wave swept this frame.</summary>
    private static float DistanceToSegment(Vector3 p, Vector3 a, Vector3 b)
    {
        Vector3 ab = b - a;
        float len2 = ab.sqrMagnitude;
        if (len2 < 0.000001f) return Vector3.Distance(p, a);
        float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / len2);
        return Vector3.Distance(p, a + ab * t);
    }
}
