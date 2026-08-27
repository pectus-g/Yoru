using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Player Health — all defensive gates live in TakeDamage() BEFORE HP subtraction.
/// Gate order: post-hit i-frames → dodge i-frames → dash i-frames → perfect parry → regular guard → subtract HP.
/// </summary>
public class PlayerHealth : MonoBehaviour
{
    [Header("Health Settings")]
    [SerializeField] private int maxHealth = 24;
    private int currentHealth;

    [Header("I-Frames")]
    [Tooltip("Invincibility duration after taking a hit")]
    [SerializeField] private float iFrameDuration = 0.3f;
    private float iFrameTimer;

    [Header("Stun")]
    [SerializeField] private float stunTimer;

    [Header("UI")]
    [SerializeField] private Image healthBarFill;
    [SerializeField] private TMP_Text healthText;

    private PeachHealthUI peachHealthUI;
    private PlayerCombat playerCombat;
    private FormController formController;

    private void Start()
    {
        currentHealth = maxHealth;
        playerCombat = GetComponent<PlayerCombat>();
        formController = GetComponent<FormController>();
        peachHealthUI = FindObjectOfType<PeachHealthUI>();

        if (peachHealthUI != null)
            peachHealthUI.UpdateHealth(currentHealth);
    }

    private void Update()
    {
        if (iFrameTimer > 0f)
            iFrameTimer -= Time.deltaTime;

        if (stunTimer > 0f)
            stunTimer -= Time.deltaTime;
    }

    /// <summary>
    /// Backward compatible — light hit, no knockback.
    /// </summary>
    public void TakeDamage(int damage)
    {
        TakeDamage(damage, false, Vector3.zero);
    }

    /// <summary>
    /// Hit type overload — no knockback.
    /// </summary>
    public void TakeDamage(int damage, bool isHeavy)
    {
        TakeDamage(damage, isHeavy, Vector3.zero);
    }

    /// <summary>
    /// Full TakeDamage — all defensive gates checked BEFORE HP subtraction.
    /// Called by EnemyCombat.DealDamageToPlayer().
    /// </summary>
    public void TakeDamage(int damage, bool isHeavy, Vector3 attackerPos, bool feedbackOnly = false)
    {
        // GATE 0: Tomoe (human form) is never damaged. GDD Doc 04 §4b:
        // "Damage taken: 0x — Tomoe is never attacked. Enemies ignore her."
        // The "enemies ignore her" AI rule is deferred to a later phase — enemies may
        // still swing at Granny visually, but no damage lands and no i-frames trigger.
        if (formController != null && formController.IsHuman)
            return;

        // GATE 0.5: Menus are a safe space. While the Memory Parchments or Inventory
        // are open the player is frozen and cannot respond, so no damage lands and no
        // combat engagement is marked. The world keeps moving; the player does not bleed.
        if (MenuGuard.IsAnyMenuOpen)
            return;

        // Mark combat engaged — enemy→Yoru half of "hit exchanged either way" per
        // GDD Doc 04 §4a. Registered as soon as the hit reaches Yoru's hitbox in
        // cat form, even if i-frames/parry/guard absorb the damage — the contact
        // itself is the combat engagement. Locks form transform for ~5s.
        if (playerCombat != null)
            playerCombat.MarkCombatEngaged();

        // GATE 1: Post-hit i-frames — brief invincibility after last hit
        if (iFrameTimer > 0f)
            return;

        // GATE 2: Dodge i-frames — mid-dodge invincibility window
        if (playerCombat != null && playerCombat.IsInDodgeIFrames())
            return;

        // GATE 3: Dash i-frames — mid-dash invincibility window
        if (playerCombat != null && playerCombat.IsInDashIFrames())
            return;

        // GATE 4: Perfect parry — 0.2s window after Q press, zero damage + enemy stagger
        if (playerCombat != null && playerCombat.IsInPerfectParryWindow())
        {
            playerCombat.OnPerfectParry(attackerPos);
            iFrameTimer = iFrameDuration; // Brief i-frames after parry too
            return;
        }

        // GATE 5: Regular guard — 70% damage blocked, no hit reaction, stay in guard
        if (playerCombat != null && playerCombat.IsGuarding())
        {
            float reduction = playerCombat.GetGuardDamageReduction();
            int reducedDamage = Mathf.Max(1, Mathf.RoundToInt(damage * (1f - reduction)));

            currentHealth = Mathf.Max(currentHealth - reducedDamage, 0);
            iFrameTimer = iFrameDuration;

            if (peachHealthUI != null)
                peachHealthUI.UpdateHealth(currentHealth);

            // Guard hit feedback — NO hit reaction (Yoru stays in guard stance)
            playerCombat.OnGuardHit(isHeavy);

            if (currentHealth <= 0)
                OnDeath();
            return;
        }

        // Already dead — don't process further
        if (currentHealth <= 0)
            return;

        // --- All gates passed — apply full damage ---
        currentHealth = Mathf.Max(currentHealth - damage, 0);
        iFrameTimer = iFrameDuration;

        if (peachHealthUI != null)
            peachHealthUI.UpdateHealth(currentHealth);

        // Hit reaction — visual feedback
        if (playerCombat != null)
            playerCombat.PlayHitReaction(isHeavy, attackerPos, feedbackOnly);

        if (currentHealth <= 0)
            OnDeath();
    }

    private void OnDeath()
    {
        // Batch 6: death animation, respawn, reload from Tori Gate auto-save
        Debug.Log("[Health] Player died");
    }

    public void ApplyStun(float duration)
    {
        stunTimer = duration;
    }

    public void Heal(int amount)
    {
        currentHealth = Mathf.Min(currentHealth + amount, maxHealth);

        if (peachHealthUI != null)
            peachHealthUI.UpdateHealth(currentHealth);
    }

    public int GetCurrentHealth() => currentHealth;
    public int GetMaxHealth() => maxHealth;
    public bool IsStunned() => stunTimer > 0f;
    public bool IsInvincible() => iFrameTimer > 0f;
    public bool IsAlive() => currentHealth > 0;
}