using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The ground hazard the beyblade finisher leaves on the floor.
///
/// Lives on the spawned zone effect for as long as that effect is visible, and drains every
/// enemy standing inside its radius for that whole time. The drain is silent damage: health,
/// red flash and the numbers, no flinch and no stagger, so the Oni fights on through it. Nothing here is serialized on
/// purpose. The zone is a runtime object; every number lives in the Inspector on PlayerCombat,
/// which also decides whether the hazard is unlocked and whether it is off cooldown. That keeps
/// the ability tree and the cooldown in one place when they arrive, and this script untouched.
///
/// Prefab agnostic: drop any effect into Ground Spin VFX and it becomes the zone.
/// </summary>
public class SpinHazardZone : MonoBehaviour
{
    #region Runtime configuration
    private float lifetime;
    private float radius;
    private int tickDamage;
    private float tickInterval;
    private LayerMask enemyLayer;
    private System.Action<EnemyHealth, int> onTick;

    private readonly Collider[] hits = new Collider[16];
    private readonly HashSet<EnemyHealth> hitThisTick = new HashSet<EnemyHealth>();
    private float bornAt;
    #endregion

    #region Public API
    /// <summary>Seconds this zone has left. Zero once it is fading out.</summary>
    public float TimeRemaining => Mathf.Max(0f, bornAt + lifetime - Time.time);

    /// <summary>Arm the zone. Call once, right after AddComponent. onTick is optional and fires
    /// once per enemy per tick, after the damage has been applied, so the caller can keep its
    /// own bookkeeping (combat engaged timer, hit sparks) without this script knowing about it.</summary>
    public void Configure(float lifetime, float radius, int tickDamage, float tickInterval,
                          LayerMask enemyLayer, System.Action<EnemyHealth, int> onTick)
    {
        this.lifetime = Mathf.Max(0.1f, lifetime);
        this.radius = Mathf.Max(0.05f, radius);
        this.tickDamage = Mathf.Max(0, tickDamage);
        this.tickInterval = Mathf.Max(0.05f, tickInterval);
        this.enemyLayer = enemyLayer;
        this.onTick = onTick;

        bornAt = Time.time;
        StopAllCoroutines();
        StartCoroutine(Run());
    }
    #endregion

    #region Loop
    private IEnumerator Run()
    {
        // Play everything exactly as authored. No speed change, no delay change: the prefab keeps
        // its own pacing and the zone lasts as long as its own lifetime.
        foreach (ParticleSystem ps in GetComponentsInChildren<ParticleSystem>(true))
            if (!ps.isPlaying) ps.Play(false);

        WaitForSeconds wait = new WaitForSeconds(tickInterval);
        while (Time.time < bornAt + lifetime)
        {
            Tick();
            yield return wait;
        }

        // The last particle has died by now (lifetime covers delay + duration + particle life),
        // so this removes an empty object, never a visible one.
        Destroy(gameObject);
    }

    private void Tick()
    {
        if (tickDamage <= 0) return;

        int count = Physics.OverlapSphereNonAlloc(transform.position, radius, hits, enemyLayer, QueryTriggerInteraction.Ignore);
        hitThisTick.Clear();
        for (int i = 0; i < count; i++)
        {
            // An enemy can have several colliders (body, weapon). One tick per enemy, not per collider.
            EnemyHealth enemy = hits[i].GetComponentInParent<EnemyHealth>();
            if (enemy == null || !hitThisTick.Add(enemy)) continue;

            enemy.TakeDamageSilent(tickDamage);   // health, red flash, numbers. No flinch: he fights on through the drain.
            onTick?.Invoke(enemy, tickDamage);
        }
    }
    #endregion

    #region Editor
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.25f, 0.1f, 0.6f);
        Gizmos.DrawWireSphere(transform.position, radius);
    }
    #endregion
}
