using UnityEngine;

/// <summary>
/// Right tail bolt. Owns all the logic for the shot so it does not depend on any VFX pack's own
/// mover script. It flies straight along the launch direction, detects the first thing on the hit
/// or environment mask with a per frame sphere cast (so a fast bolt cannot tunnel), damages an
/// EnemyHealth, spawns a muzzle on launch and an impact on landing, and despawns. Movement is
/// kinematic, so the prefab needs no Rigidbody and no collider.
///
/// Plug your VFX pack in as art (works with Gabriel, Epic Toon FX, Mirza Beig, any of them):
///  - Bolt body: a child Particle System on this prefab. It plays itself, no code needed.
///  - Muzzle Prefab: your pack's muzzle flash. Spawned at the tail tip, aimed along the shot.
///  - Impact Prefab: your pack's hit or explosion effect. Spawned where the bolt lands.
///  - Impact Alignment: Surface Up matches Gabriel Aguiar and Epic Toon FX hit prefabs.
///  - Launch Sfx and Impact Sfx: your sounds.
/// </summary>
public class TailProjectile : MonoBehaviour
{
    public enum ImpactAlignment
    {
        SurfaceUp,      // impact local +Y faces the surface normal (Gabriel Aguiar, Epic Toon FX)
        SurfaceForward  // impact local +Z faces the surface normal (some other packs)
    }

    #region Inspector
    [Header("Time")]
    [Tooltip("Fly, land and play effects on REAL time, so the shot keeps its normal speed while the tail aim slow motion is running. The world stays slowed, the arrow does not, which is what keeps the release motion and the arrow leaving the tail in sync. Turn this off to have the bolt crawl with the world.")]
    [SerializeField] private bool ignoreTimeScale = true;

    [Header("Flight")]
    [Tooltip("Travel speed in units per second once launched.")]
    [SerializeField] private float speed = 25f;
    [Tooltip("Seconds before the bolt despawns if it never hits anything.")]
    [SerializeField] private float lifetime = 3f;
    [Tooltip("Cast radius for hit detection. Larger is more forgiving but can clip nearby geometry.")]
    [SerializeField] private float castRadius = 0.25f;

    [Header("Damage")]
    [Tooltip("Damage dealt to an EnemyHealth on contact.")]
    [SerializeField] private int damage = 20;
    [Tooltip("Play the enemy's heavy hit reaction instead of the light one.")]
    [SerializeField] private bool isHeavy = false;

    [Header("Layers")]
    [Tooltip("Layers treated as targets. Set this to your Enemy layer.")]
    [SerializeField] private LayerMask hitMask;
    [Tooltip("Layers that stop the bolt without taking damage (walls, terrain).")]
    [SerializeField] private LayerMask environmentMask;

    [Header("Launch")]
    [Tooltip("Optional muzzle flash spawned at the tail tip when the bolt launches, aimed along the shot. Use your VFX pack's muzzle prefab.")]
    [SerializeField] private GameObject muzzlePrefab;
    [Tooltip("Seconds before the spawned muzzle flash is destroyed.")]
    [SerializeField] private float muzzleLifetime = 1.5f;
    [Tooltip("Optional sound played at the tail tip the moment the bolt launches. You assign the clip.")]
    [SerializeField] private AudioClip launchSfx;

    [Header("Impact")]
    [Tooltip("Optional particle effect spawned where the bolt lands. Use your VFX pack's hit or explosion prefab.")]
    [SerializeField] private GameObject impactPrefab;
    [Tooltip("How the impact is rotated to the surface. Surface Up matches the Gabriel Aguiar and Epic Toon FX hit prefabs. Flip to Surface Forward only if an impact looks rotated wrong.")]
    [SerializeField] private ImpactAlignment impactAlignment = ImpactAlignment.SurfaceUp;
    [Tooltip("Pushes the impact slightly off the surface along its normal to stop the effect clipping into the wall.")]
    [SerializeField] private float impactSurfaceOffset = 0.1f;
    [Tooltip("Seconds before the spawned impact effect is destroyed.")]
    [SerializeField] private float impactLifetime = 2f;
    [Tooltip("Optional sound played where the bolt lands. You assign the clip.")]
    [SerializeField] private AudioClip impactSfx;

    [Header("Trails")]
    [Tooltip("On hit, unparent trailing child particle systems so they linger and fade instead of vanishing with the bolt. Matches how the VFX packs expect trails to behave.")]
    [SerializeField] private bool detachTrailsOnHit = true;
    [Tooltip("Only child particle systems whose name contains this word are detached. Leave blank to detach all children.")]
    [SerializeField] private string trailNameContains = "Trail";
    [Tooltip("Seconds a detached trail lingers before it is destroyed.")]
    [SerializeField] private float trailLinger = 2f;
    #endregion

    #region State
    private Vector3 direction;
    private bool launched;
    private float launchTime;

    // One clock for the whole bolt. Real time by default so a shot fired inside the aim slow
    // motion still travels, lands, ages out and plays its effects at its authored speed.
    private float Now => ignoreTimeScale ? Time.unscaledTime : Time.time;
    private float DeltaTime => ignoreTimeScale ? Time.unscaledDeltaTime : Time.deltaTime;
    #endregion

    /// <summary>Send the bolt flying along worldDirection. Call this right after spawning it.</summary>
    public void Launch(Vector3 worldDirection)
    {
        direction = worldDirection.sqrMagnitude > 0.0001f ? worldDirection.normalized : transform.forward;
        transform.rotation = Quaternion.LookRotation(direction);
        launchTime = Now;
        launched = true;

        // The bolt's own trail and body particles run on the same clock as its movement, or a
        // fast bolt would outrun a slowed trail.
        ApplyClockToParticles(gameObject);

        if (muzzlePrefab != null)
        {
            GameObject muzzle = Instantiate(muzzlePrefab, transform.position, Quaternion.LookRotation(direction));
            ApplyClockToParticles(muzzle);
            DestroyTimed(muzzle, muzzleLifetime);
        }

        if (launchSfx != null)
            AudioSource.PlayClipAtPoint(launchSfx, transform.position);
    }

    /// <summary>The damage this bolt will actually deal, so the firing script can log a real number.</summary>
    public int Damage => damage;

    /// <summary>
    /// Multiplies this bolt's damage. Call it right after Instantiate, before Launch.
    ///
    /// Added for the 4 leg air shot, which fires the same bolt prefab as the 2 leg shot but is
    /// meant to hit harder. Keeping the number on the ability that owns the rule means one bolt
    /// prefab to maintain instead of two that can drift apart. This is purely additive: nothing
    /// calls it unless it asks to, so the 2 leg shot still does exactly what it did before.
    /// </summary>
    public void ScaleDamage(float multiplier)
    {
        // Zero or negative is never what anyone means, so it is ignored rather than obeyed. The
        // caller reports it in the Console.
        if (multiplier <= 0f) return;

        // Floor of value plus a half, not RoundToInt. RoundToInt sends halves to the nearest EVEN
        // number, so a fractional multiplier would round 37.5 up to 38 and 22.5 down to 22, which
        // looks like a bug in the damage numbers. This always rounds a half upward.
        damage = Mathf.Max(1, Mathf.FloorToInt(damage * multiplier + 0.5f));
    }

    private void Update()
    {
        if (!launched) return;

        if (Now - launchTime >= lifetime)
        {
            Destroy(gameObject);
            return;
        }

        float step = speed * DeltaTime;
        int combinedMask = hitMask | environmentMask;

        if (Physics.SphereCast(transform.position, castRadius, direction, out RaycastHit hit, step, combinedMask, QueryTriggerInteraction.Ignore))
        {
            HandleHit(hit);
            return;
        }

        transform.position += direction * step;
    }

    private void HandleHit(RaycastHit hit)
    {
        bool isEnemy = ((1 << hit.collider.gameObject.layer) & hitMask) != 0;
        if (isEnemy)
        {
            EnemyHealth enemy = hit.collider.GetComponentInParent<EnemyHealth>();
            if (enemy != null && !enemy.IsDead() && !enemy.IsInvulnerable)
            {
                enemy.TakeDamage(damage, isHeavy);
            }
        }

        SpawnImpact(hit.point, hit.normal);
        DetachTrails();
        Destroy(gameObject);
    }

    private void SpawnImpact(Vector3 point, Vector3 normal)
    {
        Vector3 pos = point + normal * impactSurfaceOffset;

        if (impactPrefab != null)
        {
            Quaternion rot = impactAlignment == ImpactAlignment.SurfaceUp
                ? Quaternion.FromToRotation(Vector3.up, normal)
                : Quaternion.LookRotation(normal);

            GameObject fx = Instantiate(impactPrefab, pos, rot);
            ApplyClockToParticles(fx);
            DestroyTimed(fx, impactLifetime);
        }

        if (impactSfx != null)
            AudioSource.PlayClipAtPoint(impactSfx, pos);
    }

    /// <summary>Unparent trailing child particle systems so they keep playing and fade after the bolt is gone.</summary>
    private void DetachTrails()
    {
        if (!detachTrailsOnHit) return;

        ParticleSystem[] systems = GetComponentsInChildren<ParticleSystem>();
        foreach (ParticleSystem ps in systems)
        {
            if (ps.gameObject == gameObject) continue;
            if (!string.IsNullOrEmpty(trailNameContains) && !ps.gameObject.name.Contains(trailNameContains)) continue;

            ps.transform.SetParent(null);
            DestroyTimed(ps.gameObject, trailLinger);
        }
    }

    /// <summary>Put every particle system under root on the bolt's clock, so effects fired inside the aim slow motion play at their authored speed instead of crawling.</summary>
    private void ApplyClockToParticles(GameObject root)
    {
        if (!ignoreTimeScale || root == null) return;

        ParticleSystem[] systems = root.GetComponentsInChildren<ParticleSystem>(true);
        foreach (ParticleSystem ps in systems)
        {
            ParticleSystem.MainModule main = ps.main;
            main.useUnscaledTime = true;
        }
    }

    /// <summary>Despawn on the bolt's clock. Timed Destroy runs on scaled time, which would leave
    /// a muzzle or an impact hanging around ten times too long during the slow motion. The timer
    /// rides on the spawned object itself, so it still fires after this bolt is gone.</summary>
    private void DestroyTimed(GameObject go, float seconds)
    {
        if (go == null) return;

        if (!ignoreTimeScale)
        {
            Destroy(go, seconds);
            return;
        }

        UnscaledDespawn despawn = go.AddComponent<UnscaledDespawn>();
        despawn.seconds = seconds;
    }
}

/// <summary>
/// Destroys its own GameObject after a number of REAL seconds. Added at runtime by TailProjectile
/// to muzzles, impacts and detached trails, so effects spawned during the tail aim slow motion
/// clean up on schedule instead of stretching with the game clock.
/// </summary>
public class UnscaledDespawn : MonoBehaviour
{
    public float seconds = 2f;
    private float elapsed;

    private void Update()
    {
        elapsed += Time.unscaledDeltaTime;
        if (elapsed >= seconds) Destroy(gameObject);
    }
}