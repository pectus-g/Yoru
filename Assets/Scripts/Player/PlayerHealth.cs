using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Player Health — all defensive gates live in TakeDamage() BEFORE HP subtraction.
/// Gate order: post-hit i-frames → dodge i-frames → [Phase 3C: parry/guard] → subtract HP.
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

    private void Start()
    {
        currentHealth = maxHealth;
        playerCombat = GetComponent<PlayerCombat>();
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
    public void TakeDamage(int damage, bool isHeavy, Vector3 attackerPos)
    {
        // GATE 1: Post-hit i-frames — brief invincibility after last hit
        if (iFrameTimer > 0f)
            return;

        // GATE 2: Dodge i-frames — mid-dodge invincibility window
        if (playerCombat != null && playerCombat.IsInDodgeIFrames())
            return;

        // GATE 3: Dash i-frames — mid-dash invincibility window
        if (playerCombat != null && playerCombat.IsInDashIFrames())
            return;

        // Already dead — don't process further
        if (currentHealth <= 0)
            return;

        // --- All gates passed — apply damage ---
        currentHealth = Mathf.Max(currentHealth - damage, 0);
        iFrameTimer = iFrameDuration;

        if (peachHealthUI != null)
            peachHealthUI.UpdateHealth(currentHealth);

        // Hit reaction — visual feedback
        if (playerCombat != null)
            playerCombat.PlayHitReaction(isHeavy, attackerPos);

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