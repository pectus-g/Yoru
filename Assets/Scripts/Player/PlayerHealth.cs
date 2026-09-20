using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Player Health, counted in PEACHES. One bite = a quarter peach. Every defensive gate lives in
/// TakeDamage() BEFORE any bite is taken.
/// Gate order: Tomoe -> menus -> cinematic -> post-hit i-frames -> dodge i-frames -> dash i-frames
///             -> perfect parry -> regular guard (chip) -> bites.
///
/// The bite rules (Sep 2026, Hazel):
///   bites = damage / Points Per Quarter, rounded to the nearest bite, exactly halfway rounds DOWN.
///   A clean hit takes at least 1 bite. A heavy hit takes at least Heavy Min Bites.
///   A blocked hit (Q held) is the guard reduced damage through the same rounding and CAN be 0.
///   Gold peaches (temporary, from items) are eaten first, vanish when eaten, never come back.
///   Cannot Die keeps her at 1 bite for testing.
/// </summary>
public class PlayerHealth : MonoBehaviour
{
    [Header("Peaches")]
    [Tooltip("Peaches she owns at the start. 4 peaches = 16 bites. Raise this to make the whole game softer; the bite rules below stay the same.")]
    [SerializeField] private int maxPeaches = 4;
    [Tooltip("Enemy damage points that equal ONE bite (a quarter peach). With 8: a 16 damage club swing = 2 bites (half a peach), the 30 damage slam finisher = 4 bites (a whole peach), the 45 damage pound = 6 bites.")]
    [SerializeField] private int pointsPerQuarter = 8;
    [Tooltip("A heavy hit (combo finisher, charge, pound) never takes fewer bites than this, whatever its damage number says. 3 = three quarters of a peach.")]
    [SerializeField] private int heavyMinBites = 3;
    [Tooltip("One console line per bite taken or given: attack damage, bites, peaches left. Turn off when the numbers feel right.")]
    [SerializeField] private bool logBites = true;

    [Header("Testing")]
    [Tooltip("ON = she cannot die: every hit still lands, hurts, flinches and eats the peaches, but she never drops below 1 bite. For testing fights without restarting. OFF for the real game.")]
    [SerializeField] private bool cannotDie = false;

    [Header("I-Frames")]
    [Tooltip("Invincibility duration after taking a hit")]
    [SerializeField] private float iFrameDuration = 0.3f;
    private float iFrameTimer;

    [Header("Stun")]
    [SerializeField] private float stunTimer;

    // ROUND 53 (Oni phase-2 cinematic): while > 0, NOTHING can hurt Yoru. The entrance cinematic
    // freezes her, and a frozen player must never eat a hit she cannot answer. The boss layer
    // refreshes this every cinematic frame and zeroes it the moment control returns; it counts
    // down in UNSCALED time (the cinematic slows the world clock), so even if the boss object
    // died mid-cinematic it expires by itself a fraction of a second later. Not serialized.
    private float cinematicGuardTimer;

    [Header("UI (optional, other scenes)")]
    [Tooltip("Old style fill bar. Left empty in the Oni scene; the peach row draws itself.")]
    [SerializeField] private Image healthBarFill;
    [SerializeField] private TMP_Text healthText;

    // Health state, in quarters (bites)
    private int quarters;          // real peaches, 0 .. maxPeaches * 4
    private int goldQuarters;      // temporary gold peaches, no cap
    private bool isDead;

    private PeachHealthUI peachHealthUI;
    private PlayerCombat playerCombat;
    private FormController formController;

    private void Start()
    {
        maxPeaches = Mathf.Max(1, maxPeaches);
        pointsPerQuarter = Mathf.Max(1, pointsPerQuarter);
        quarters = MaxQuarters;
        goldQuarters = 0;
        playerCombat = GetComponent<PlayerCombat>();
        formController = GetComponent<FormController>();
        if (peachHealthUI == null)
            peachHealthUI = FindFirstObjectByType<PeachHealthUI>();
        PushToUI(0);
    }

    private void Update()
    {
        if (iFrameTimer > 0f)
            iFrameTimer -= Time.deltaTime;

        if (stunTimer > 0f)
            stunTimer -= Time.deltaTime;

        if (cinematicGuardTimer > 0f)
            cinematicGuardTimer -= Time.unscaledDeltaTime; // unscaled: the cinematic slows Time.timeScale
    }

    /// <summary>
    /// Backward compatible: light hit, no knockback.
    /// </summary>
    public void TakeDamage(int damage)
    {
        TakeDamage(damage, false, Vector3.zero);
    }

    /// <summary>
    /// Hit type overload, no knockback.
    /// </summary>
    public void TakeDamage(int damage, bool isHeavy)
    {
        TakeDamage(damage, isHeavy, Vector3.zero);
    }

    /// <summary>What her defence did with the last hit that reached her. Set inside TakeDamage before it
    /// returns, so an attacker that calls TakeDamage can read it in the same frame and draw its
    /// impact only when the hit really landed (a parried, blocked or dodged club leaves no red mark).</summary>
    public enum HitOutcome { None, Immune, Dodged, Parried, Blocked, Damaged }
    public HitOutcome LastHitOutcome { get; private set; } = HitOutcome.None;

    /// <summary>
    /// Full TakeDamage: all defensive gates checked BEFORE any bite is taken.
    /// Called by EnemyCombat.DealDamageToPlayer(), the Oni's pound and the ground wave.
    /// </summary>
    public void TakeDamage(int damage, bool isHeavy, Vector3 attackerPos, bool feedbackOnly = false)
    {
        LastHitOutcome = HitOutcome.Immune;   // every early return below is a hit that did not land

        // Dead already: nothing lands, nothing reacts. The death clip owns her from here.
        if (isDead)
            return;

        // GATE 0: Tomoe (human form) is never damaged. GDD Doc 04 section 4b:
        // "Damage taken: 0x. Tomoe is never attacked. Enemies ignore her."
        // The "enemies ignore her" AI rule is deferred to a later phase; enemies may
        // still swing at Granny visually, but no damage lands and no i-frames trigger.
        if (formController != null && formController.IsHuman)
            return;

        // GATE 0.5: Menus are a safe space. While the Memory Parchments or Inventory
        // are open the player is frozen and cannot respond, so no damage lands and no
        // combat engagement is marked. The world keeps moving; the player does not bleed.
        if (MenuGuard.IsAnyMenuOpen)
            return;

        // GATE 0.7: Boss cinematic (round 53). Yoru is frozen and untouchable while the Oni's
        // phase-2 entrance plays. No damage, no i-frames, no combat engagement marked: the hit
        // simply never happened. The window is refreshed per frame by the boss layer and cleared
        // the instant control returns, so the slam that follows is dodged in normal gameplay.
        if (cinematicGuardTimer > 0f)
            return;

        // Mark combat engaged: the enemy to Yoru half of "hit exchanged either way" per
        // GDD Doc 04 section 4a. Registered as soon as the hit reaches Yoru's hitbox in
        // cat form, even if i-frames/parry/guard absorb the damage; the contact
        // itself is the combat engagement. Locks form transform for ~5s.
        if (playerCombat != null)
            playerCombat.MarkCombatEngaged();

        // GATE 1: Post-hit i-frames, brief invincibility after the last hit
        if (iFrameTimer > 0f)
            return;

        // GATE 2: Dodge i-frames, mid-dodge invincibility window
        if (playerCombat != null && playerCombat.IsInDodgeIFrames())
        {
            LastHitOutcome = HitOutcome.Dodged;
            return;
        }

        // GATE 3: Dash i-frames, mid-dash invincibility window
        if (playerCombat != null && playerCombat.IsInDashIFrames())
        {
            LastHitOutcome = HitOutcome.Dodged;
            return;
        }

        // GATE 4: Perfect parry, 0.2s window after Q press: zero damage + enemy stagger
        if (playerCombat != null && playerCombat.IsInPerfectParryWindow())
        {
            LastHitOutcome = HitOutcome.Parried;
            playerCombat.OnPerfectParry(attackerPos);
            iFrameTimer = iFrameDuration; // Brief i-frames after parry too
            return;
        }

        // GATE 5: Regular guard: the hit is reduced (Guard Damage Reduction on PlayerCombat), then
        // rounded to bites like any hit, and it can round to 0. No hit reaction, she stays in guard.
        if (playerCombat != null && playerCombat.IsGuarding())
        {
            LastHitOutcome = HitOutcome.Blocked;
            float reduction = playerCombat.GetGuardDamageReduction();
            int chip = BitesFor(damage * (1f - reduction), false, true);

            EatBites(chip, $"blocked {damage} dmg");
            iFrameTimer = iFrameDuration;

            // Guard hit feedback, NO hit reaction (Yoru stays in guard stance)
            playerCombat.OnGuardHit(isHeavy);

            if (quarters <= 0)
                OnDeath(isHeavy);
            return;
        }

        // --- All gates passed: take the bites ---
        LastHitOutcome = HitOutcome.Damaged;
        int bites = BitesFor(damage, isHeavy, false);
        EatBites(bites, $"{(isHeavy ? "heavy" : "light")} hit {damage} dmg");
        iFrameTimer = iFrameDuration;

        if (quarters <= 0)
        {
            // The killing hit: the death clip replaces the hit reaction.
            OnDeath(isHeavy);
            return;
        }

        // Hit reaction, visual feedback
        if (playerCombat != null)
            playerCombat.PlayHitReaction(isHeavy, attackerPos, feedbackOnly);
    }

    // ---------- the bite maths ----------

    /// <summary>Round to the nearest whole bite; exactly halfway rounds down (2.5 = 2, 2.51 = 3).</summary>
    private static int RoundHalfDown(float x)
    {
        return Mathf.Max(0, Mathf.CeilToInt(x - 0.5f));
    }

    /// <summary>How many quarters a hit of this many points eats. Blocked chip may be 0; a clean hit is
    /// at least 1; a clean heavy hit is at least Heavy Min Bites.</summary>
    public int BitesFor(float points, bool isHeavy, bool blocked)
    {
        int bites = RoundHalfDown(points / Mathf.Max(1, pointsPerQuarter));
        if (blocked) return bites;
        bites = Mathf.Max(1, bites);
        if (isHeavy) bites = Mathf.Max(Mathf.Max(0, heavyMinBites), bites);
        return bites;
    }

    /// <summary>Takes bites: gold peaches first (they vanish), then the real ones. Cannot Die floors the
    /// real peaches at 1 bite.</summary>
    private void EatBites(int bites, string why)
    {
        if (bites <= 0)
        {
            if (logBites) Debug.Log($"[Peach] {why}: 0 bites. {Describe()}");
            return;
        }

        int fromGold = Mathf.Min(goldQuarters, bites);
        goldQuarters -= fromGold;
        int fromReal = bites - fromGold;
        int floor = cannotDie ? 1 : 0;
        int before = quarters;
        quarters = Mathf.Max(quarters - fromReal, floor);
        int taken = fromGold + (before - quarters);

        if (logBites) Debug.Log($"[Peach] {why}: {bites} bite{(bites == 1 ? "" : "s")}{(fromGold > 0 ? $" ({fromGold} from gold)" : "")}{(cannotDie && before - quarters < fromReal ? " (Cannot Die held her at 1 bite)" : "")}. {Describe()}");
        PushToUI(-taken);
    }

    private string Describe()
    {
        return $"{quarters}/{MaxQuarters} bites = {quarters / 4f:0.##} of {maxPeaches} peaches{(goldQuarters > 0 ? $", gold {goldQuarters / 4f:0.##}" : "")}";
    }

    private void PushToUI(int deltaQuarters)
    {
        if (peachHealthUI != null)
            peachHealthUI.SetHealth(quarters, MaxQuarters, goldQuarters, deltaQuarters);

        if (healthBarFill != null)
            healthBarFill.fillAmount = MaxQuarters > 0 ? (float)quarters / MaxQuarters : 0f;
        if (healthText != null)
            healthText.text = $"{quarters}/{MaxQuarters}";
    }

    private void OnDeath(bool killingHitWasHeavy)
    {
        if (isDead) return;
        isDead = true;
        Debug.Log($"[Health] Player died ({(killingHitWasHeavy ? "heavy" : "light")} killing hit). Game over menu: next pass.");
        if (playerCombat != null)
            playerCombat.PlayDeath(killingHitWasHeavy);
    }

    // ---------- public API ----------

    /// <summary>The peach row registers itself here so it is found even when it spawns after Yoru.</summary>
    public void RegisterUI(PeachHealthUI ui)
    {
        peachHealthUI = ui;
        PushToUI(0);
    }

    public void ApplyStun(float duration)
    {
        stunTimer = duration;
    }

    /// <summary>
    /// ROUND 53: set (not extend) the cinematic protection window, in seconds of UNSCALED time.
    /// The Oni's boss layer refreshes it every frame the phase-2 cinematic runs and sets it to 0
    /// the moment control returns. While > 0, TakeDamage drops every hit at GATE 0.7.
    /// </summary>
    public void SetCinematicGuard(float seconds)
    {
        cinematicGuardTimer = Mathf.Max(0f, seconds);
    }

    /// <summary>Give back whole bites (quarter peaches). Never past the real maximum.</summary>
    public void HealQuarters(int bites)
    {
        if (isDead || bites <= 0) return;
        int before = quarters;
        quarters = Mathf.Min(quarters + bites, MaxQuarters);
        if (logBites) Debug.Log($"[Peach] healed {quarters - before} bite{(quarters - before == 1 ? "" : "s")}. {Describe()}");
        PushToUI(quarters - before);
    }

    /// <summary>Old points API kept for callers that still think in damage points: converted with the
    /// same rounding as a hit, at least 1 bite when the amount is positive.</summary>
    public void Heal(int points)
    {
        if (points <= 0) return;
        HealQuarters(Mathf.Max(1, RoundHalfDown((float)points / Mathf.Max(1, pointsPerQuarter))));
    }

    /// <summary>Gold, temporary peaches from items. No cap. Eaten before the real peaches, gone for good once eaten.</summary>
    public void AddGoldPeaches(int peaches)
    {
        AddGoldQuarters(peaches * 4);
    }

    public void AddGoldQuarters(int bites)
    {
        if (isDead || bites <= 0) return;
        goldQuarters += bites;
        if (logBites) Debug.Log($"[Peach] +{bites} gold bites. {Describe()}");
        PushToUI(bites);
    }

    /// <summary>Permanent growth (a new peach earned). Grows the maximum and fills the new peach.</summary>
    public void AddMaxPeaches(int peaches)
    {
        if (peaches <= 0) return;
        maxPeaches += peaches;
        quarters = Mathf.Min(quarters + peaches * 4, MaxQuarters);
        if (logBites) Debug.Log($"[Peach] max peaches now {maxPeaches}. {Describe()}");
        PushToUI(peaches * 4);
    }

    /// <summary>Full real peaches, gold untouched. For checkpoints and the restart.</summary>
    public void RestoreFull()
    {
        int before = quarters;
        quarters = MaxQuarters;
        isDead = false;
        PushToUI(quarters - before);
    }

    public int MaxQuarters => maxPeaches * 4;
    public int GetQuarters() => quarters;
    public int GetGoldQuarters() => goldQuarters;
    public int GetMaxPeaches() => maxPeaches;
    public int PointsPerQuarter => pointsPerQuarter;

    /// <summary>Kept for old callers: health is now in quarters (bites).</summary>
    public int GetCurrentHealth() => quarters;
    public int GetMaxHealth() => MaxQuarters;
    public bool IsStunned() => stunTimer > 0f;
    public bool IsInvincible() => iFrameTimer > 0f;
    public bool IsAlive() => !isDead && quarters > 0;
    public bool IsDead() => isDead;
}
