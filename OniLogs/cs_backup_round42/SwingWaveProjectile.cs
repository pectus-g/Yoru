using UnityEngine;

/// <summary>
/// ROUND 40 — the Oni's ground wave, Hazel's rule set as of round 40: the wave VISUAL appears on
/// EVERY swing, and only its DAMAGE depends on the club. Club did not connect → the wave is
/// ARMED (damage &gt; 0, half the club's). Club connected → the wave is spawned with damage 0 and
/// is pure effect, start to finish. A club that connects only after the wave was released
/// disarms it in flight (CancelledByClub) — the visual keeps flying either way, so a wave is
/// SEEN on every single swing.
///
/// An armed wave resolves ON TOUCH. The instant its front reaches Yoru while she is ON THE
/// GROUND, she takes its damage and plays her full hit reaction, that same frame — and the wave
/// then KEEPS FLYING as spent effect to the end of its travel (round 40: it no longer dies on
/// her, which made close hits invisible). While she is airborne it passes underneath and spends
/// nothing — jumping is the designed escape, so the gate is her movement state, not collider
/// geometry.
///
/// It is fast and short-lived on purpose: armed life = travel / speed (≈ half a second at the
/// defaults), so a hit can only ever come from the swing she is watching — the old
/// four-second waves that arrived from swings long finished are physically impossible now.
/// At the end of its travel it does not vanish and does not hang frozen: it stops emitting,
/// eases to a halt, and is destroyed once its particles have faded.
///
/// Collision is deliberately NOT a physics query. It measures the PLANAR distance from Yoru to
/// the segment the wave swept this frame, so a fast wave cannot skip past her between frames,
/// and no layer mask is involved — a mask that silently matched nothing is what killed the
/// launch for weeks, and this needs no mask to be correct.
/// </summary>
public class SwingWaveProjectile : MonoBehaviour
{
    private Transform target;
    private PlayerHealth targetHealth;
    private PlayerMovement targetMove;
    private Transform attacker;

    private Vector3 direction;
    private Vector3 startPos;
    private float speed;
    private float maxDistance;
    private float hitWidth;
    private int damage;

    private GameObject hitVFX;
    private float hitVFXLifetime;
    private float hitVFXOffset;
    private float fadeLifetime;

    /// <summary>ROUND 39: tells the owning swing its wave touched her first (first touch wins).</summary>
    public System.Action<SwingWaveProjectile> onConnected;

    private bool spent;          // the hit is delivered — one wave can never hit twice
    private bool dissolving;     // travel over (or hit landed): hitbox dead, effect fading out
    private float dissolveDecel; // how hard it brakes while dissolving, m/s^2
    private float bornTime;
    private ParticleSystem[] particles;   // cached once at spawn — never queried per frame

    /// <summary>
    /// Spawns a ground wave carrying `visual` and sends it along `dir`. damage &gt; 0 = armed for
    /// its whole travel; damage 0 = pure visual (ROUND 40: the club already connected, but the
    /// wave still appears — "wave won't hit but looks on the effect", her words).
    /// </summary>
    public static SwingWaveProjectile Launch(GameObject visual, Vector3 origin, Vector3 dir,
                                             Transform attacker, Transform target,
                                             PlayerHealth targetHealth, PlayerMovement targetMove,
                                             float speed, float maxDistance, float hitWidth,
                                             int damage,
                                             GameObject hitVFX, float hitVFXLifetime, float hitVFXOffset,
                                             float fadeLifetime, Vector3 visualTilt, float visualPlaybackSpeed)
    {
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) dir = Vector3.forward;
        dir.Normalize();

        var go = new GameObject("OniGroundWave");
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
        }

        var w = go.AddComponent<SwingWaveProjectile>();
        w.direction = dir;
        w.startPos = origin;
        w.attacker = attacker;
        w.target = target;
        w.targetHealth = targetHealth;
        w.targetMove = targetMove;
        w.speed = Mathf.Max(0.1f, speed);
        w.maxDistance = Mathf.Max(0.1f, maxDistance);
        w.hitWidth = Mathf.Max(0.1f, hitWidth);
        w.damage = damage;
        w.hitVFX = hitVFX;
        w.hitVFXLifetime = hitVFXLifetime;
        w.hitVFXOffset = hitVFXOffset;
        w.fadeLifetime = Mathf.Max(0.3f, fadeLifetime);
        w.bornTime = Time.time;

        // ROUND 38: the wave and its effect live and die TOGETHER — no detach-on-destroy hack
        // (round 31's detach is what left slashes hanging frozen in the air). The whole object
        // dissolves in place instead: emission stops, it brakes to a halt, particles finish.
        w.particles = go.GetComponentsInChildren<ParticleSystem>(true);
        if (visualPlaybackSpeed > 0f && !Mathf.Approximately(visualPlaybackSpeed, 1f))
        {
            // ROUND 33: destroying an effect later cannot make a short one last longer. Slowing
            // its SIMULATION is what stretches it: 0.5 plays it at half speed, twice as long.
            foreach (var ps in w.particles)
            {
                var main = ps.main;
                main.simulationSpeed = main.simulationSpeed * visualPlaybackSpeed;
            }
        }

        return w;
    }

    private void Update()
    {
        if (dissolving)
        {
            // Ease out instead of freezing mid-air or vanishing: the wave visibly runs out of
            // force. Destruction was scheduled when the dissolve began.
            speed = Mathf.MoveTowards(speed, 0f, dissolveDecel * Time.deltaTime);
            transform.position += direction * (speed * Time.deltaTime);
            return;
        }

        Vector3 prev = transform.position;
        Vector3 next = prev + direction * (speed * Time.deltaTime);
        transform.position = next;

        // Armed for the WHOLE travel — but only while it carries damage: a visual-only wave
        // (damage 0) never even looks for contact and just flies. Airborne = safe,
        // unconditionally: this is ground force and jumping over it is the counter Hazel
        // designed. If PlayerMovement was not found the check fails open (treated as grounded).
        if (!spent && damage > 0 && target != null)
        {
            bool airborne = targetMove != null && targetMove.IsAirborne();
            if (!airborne && PlanarDistanceToSegment(target.position, prev, next) <= hitWidth)
            {
                spent = true;
                Connect();
            }
        }

        // ROUND 40: the travel end is the ONLY thing that retires a wave — hitting her no longer
        // does, so the effect is always seen completing its run.
        Vector3 flown = transform.position - startPos;
        flown.y = 0f;
        if (flown.sqrMagnitude >= maxDistance * maxDistance) BeginDissolve();
    }

    /// <summary>
    /// The touch. Fires her full hit reaction the same frame — feedbackOnly = false, because
    /// being caught by the wave should stop what she is doing, not just tick her health down.
    /// </summary>
    private void Connect()
    {
        if (targetHealth != null && damage > 0)
        {
            Vector3 from = transform.position;
            Vector3 contact = target.position + Vector3.up * 0.4f;   // shin height fallback
            Collider col = target.GetComponent<Collider>();
            if (col != null && col.enabled)
            {
                Vector3 p = col.ClosestPoint(from);
                if ((p - from).sqrMagnitude > 0.0001f) contact = p;
            }

            Vector3 approach = from - contact;
            approach.y = 0f;
            if (approach.sqrMagnitude < 0.0001f) approach = -direction;
            approach.Normalize();

            if (hitVFX != null)
            {
                GameObject fx = Instantiate(hitVFX, contact + approach * hitVFXOffset,
                                            Quaternion.LookRotation(approach));
                if (hitVFXLifetime > 0f) Destroy(fx, hitVFXLifetime);
            }

            Vector3 reactFrom = attacker != null ? attacker.position : transform.position;
            targetHealth.TakeDamage(damage, false, reactFrom, false);

            Vector3 flownV = transform.position - startPos; flownV.y = 0f;
            Debug.Log($"[OniBoss:Wave] wave TOUCHED Yoru (grounded) for {damage} after "
                    + $"{flownV.magnitude:F1}m / {Time.time - bornTime:F2}s of travel.");

            onConnected?.Invoke(this);   // ROUND 39: the swing is spent — the club stands down
        }

        // ROUND 40: no dissolve here — the wave flies on as spent effect so the hit is SEEN.
    }

    /// <summary>
    /// ROUND 40: the club reached her after this wave was already released — her rule says the
    /// club's hit cancels the wave's DAMAGE, not its picture: "wave won't hit but looks on the
    /// effect". So it is disarmed and keeps flying to the end of its travel. Harmless if it
    /// already connected or is already visual-only.
    /// </summary>
    public void CancelledByClub()
    {
        damage = 0;
    }

    /// <summary>
    /// The only way out. Hitbox off, emission off, brake to a stop, die when the particles have
    /// had their fade time. Nothing is detached, nothing hangs frozen, nothing is cut off.
    /// </summary>
    private void BeginDissolve()
    {
        if (dissolving) return;
        dissolving = true;
        spent = true;
        dissolveDecel = Mathf.Max(1f, speed / 0.35f);   // ~0.35s ease-out from full speed

        if (particles != null)
        {
            foreach (var ps in particles)
                if (ps != null) ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        Destroy(gameObject, fadeLifetime);
    }

    /// <summary>Shortest FLAT distance from a point to the segment the wave swept this frame —
    /// height plays no part, because the airborne check is what decides "over it".</summary>
    private static float PlanarDistanceToSegment(Vector3 p, Vector3 a, Vector3 b)
    {
        p.y = 0f; a.y = 0f; b.y = 0f;
        Vector3 ab = b - a;
        float len2 = ab.sqrMagnitude;
        if (len2 < 0.000001f) return Vector3.Distance(p, a);
        float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / len2);
        return Vector3.Distance(p, a + ab * t);
    }
}
