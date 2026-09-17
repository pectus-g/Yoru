using UnityEngine;

/// <summary>
/// The momentum counter. Every hit Yoru lands adds one: x1, x2, x3 and on without end. Not
/// getting hit is what keeps it. It drains when she stops attacking and drops when she takes a
/// heavy hit; light hits, dodges, dashes and parries never touch it, so weaving through his
/// swings keeps what she built. Each point also makes her a little faster, capped so the clips
/// stay readable; PlayerCombat multiplies its Combo Speed and Dodge Speed by SpeedMultiplier.
///
/// One component, on PlayerYoru_1.1, next to Combat Floating Text (which draws the count).
/// Hits are counted from EnemyHealth.AnyHit, so every source of hers counts: combo, dash strike,
/// air spin ticks, parry counter, tail bolts. The floor zone's silent drain does not count.
/// </summary>
public class CombatMomentum : MonoBehaviour
{
    #region Inspector
    [Header("Counting")]
    [Tooltip("Seconds without landing a hit before the count starts to drain away.")]
    [SerializeField] private float idleBeforeDrain = 2.5f;
    [Tooltip("Seconds between each point lost while draining.")]
    [SerializeField] private float drainInterval = 0.5f;
    [Tooltip("Points lost when she takes a HEAVY hit. 0 = heavy hits never touch it. 999 = a heavy hit resets it.")]
    [SerializeField] private int heavyHitCost = 3;
    [Tooltip("Points lost when she takes a LIGHT hit. Not getting hit is the whole point, so every hit costs something; light ones less.")]
    [SerializeField] private int lightHitCost = 1;
    [Tooltip("Seconds that must pass between two counted hits. The air spin and the beyblade tick damage many times a second; without this one spin was worth 5 to 8 points. 0.25 makes a spin worth about 3.")]
    [SerializeField] private float minSecondsBetweenPoints = 0.25f;
    [Tooltip("Points gained on a perfect parry, on top of the counter hit itself.")]
    [SerializeField] private int parryBonus = 2;

    [Header("Speed Reward")]
    [Tooltip("Attack and flip speed added per point, on top of Combo Speed and Dodge Speed. 0.03 = at x10 she is 30% faster.")]
    [SerializeField] private float speedPerPoint = 0.03f;
    [Tooltip("Cap on the bonus so the clips stay readable. 0.5 = never more than 50% faster from momentum, however high the count.")]
    [SerializeField] private float maxSpeedBonus = 0.5f;

    [Header("Text")]
    [Tooltip("Draw the count in the corner through Combat Floating Text on every landed hit.")]
    [SerializeField] private bool showCount = true;
    [Tooltip("Every this many points the text pops bigger with a bigger camera kick. 0 = never.")]
    [SerializeField] private int milestoneEvery = 5;
    [SerializeField] private bool logMomentum = false;
    #endregion

    #region State
    private static CombatMomentum instance;
    private int count;
    private float lastHitTime = -999f;
    private float nextDrainAt;
    #endregion

    #region Public API
    /// <summary>Current count. 0 with no instance in the scene.</summary>
    public static int Count => instance != null ? instance.count : 0;

    /// <summary>1 + the capped speed bonus. 1 with no instance, so nothing changes without the component.</summary>
    public static float SpeedMultiplier =>
        instance != null ? 1f + Mathf.Min(instance.maxSpeedBonus, instance.count * instance.speedPerPoint) : 1f;

    /// <summary>True when a momentum counter is running, so the plain combo step text steps aside.</summary>
    public static bool Active => instance != null && instance.showCount;

    /// <summary>She took a hit. Called by PlayerCombat from the hit reaction.</summary>
    public static void OnPlayerHit(bool isHeavy)
    {
        if (instance == null) return;
        int cost = isHeavy ? instance.heavyHitCost : instance.lightHitCost;
        if (cost <= 0) return;
        instance.Set(instance.count - cost, "hit taken");
    }

    /// <summary>She parried. Called by PlayerCombat from OnPerfectParry.</summary>
    public static void OnParry()
    {
        if (instance == null || instance.parryBonus <= 0) return;
        instance.lastHitTime = Time.time;
        instance.Set(instance.count + instance.parryBonus, "parry");
        instance.Show();
    }
    #endregion

    #region Lifecycle
    private void Awake() { instance = this; }
    private void OnDestroy() { if (instance == this) instance = null; }
    private void OnEnable()  { EnemyHealth.AnyHit += OnHitLanded; }
    private void OnDisable() { EnemyHealth.AnyHit -= OnHitLanded; }

    private float lastCountedAt = -999f;

    private void OnHitLanded(EnemyHealth enemy, int damage, bool isHeavy)
    {
        lastHitTime = Time.time;
        nextDrainAt = Time.time + idleBeforeDrain;
        if (Time.time - lastCountedAt < minSecondsBetweenPoints) return;   // rapid ticks of one move count once
        lastCountedAt = Time.time;
        Set(count + 1, "hit");
        Show();
    }

    private void Update()
    {
        if (count <= 0) return;
        if (Time.time - lastHitTime < idleBeforeDrain) return;
        if (Time.time < nextDrainAt) return;
        nextDrainAt = Time.time + drainInterval;
        Set(count - 1, "drain");
    }

    private void Set(int value, string why)
    {
        int before = count;
        count = Mathf.Max(0, value);
        if (logMomentum && count != before)
            Debug.Log($"[Momentum] {before} -> {count} ({why}), speed x{SpeedMultiplier:F2}");
    }

    private void Show()
    {
        if (!showCount || count <= 0) return;
        bool milestone = milestoneEvery > 0 && count % milestoneEvery == 0;
        CombatFloatingText.ShowMomentum(count, milestone);
    }
    #endregion
}
