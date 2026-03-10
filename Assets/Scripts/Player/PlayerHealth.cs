using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Player Health — i-frames, stun, hit reaction with knockback pull.
/// </summary>
public class PlayerHealth : MonoBehaviour
{
    [Header("Health Settings")]
    [SerializeField] private int maxHealth = 20;
    private int currentHealth;

    [Header("I-Frames")]
    [Tooltip("Invincibility duration after taking damage")]
    [SerializeField] private float iFrameDuration = 0.3f;
    private float iFrameTimer;

    [Header("Stun")]
    [Tooltip("Remaining stun duration in seconds — player input is blocked while this is > 0")]
    [SerializeField] private float stunTimer;

    [Header("UI")]
    [SerializeField] private Image healthBarFill;
    [SerializeField] private TMP_Text healthText;

    // Cached references
    private PeachHealthUI peachHealthUI;
    private PlayerCombat playerCombat;
    private Animator animator;

    void Start()
    {
        currentHealth = maxHealth;
        animator = GetComponent<Animator>();
        playerCombat = GetComponent<PlayerCombat>();
        peachHealthUI = FindObjectOfType<PeachHealthUI>();

        if (peachHealthUI != null)
            peachHealthUI.UpdateHealth(currentHealth);
    }

    void Update()
    {
        if (iFrameTimer > 0)
            iFrameTimer -= Time.deltaTime;

        if (stunTimer > 0)
            stunTimer -= Time.deltaTime;
    }

    /// <summary>
    /// Take damage — light hit, no knockback (backward compatible).
    /// </summary>
    public void TakeDamage(int damage)
    {
        TakeDamage(damage, false, Vector3.zero);
    }

    /// <summary>
    /// Take damage with hit type — no knockback.
    /// </summary>
    public void TakeDamage(int damage, bool isHeavy)
    {
        TakeDamage(damage, isHeavy, Vector3.zero);
    }

    /// <summary>
    /// Take damage with hit type and attacker position for knockback pull.
    /// attackerPos = Vector3.zero means no knockback.
    /// </summary>
    public void TakeDamage(int damage, bool isHeavy, Vector3 attackerPos)
    {
        // I-frames check first
        if (iFrameTimer > 0)
        {
            Debug.Log("🛡️ I-FRAMES active, damage ignored");
            return;
        }

        // Apply damage only if alive
        if (currentHealth > 0)
        {
            currentHealth -= damage;
            if (currentHealth < 0) currentHealth = 0;

            iFrameTimer = iFrameDuration;

            Debug.Log($"💔 DAMAGE! {damage} dmg → HP: {currentHealth}/{maxHealth}");

            if (peachHealthUI != null)
                peachHealthUI.UpdateHealth(currentHealth);

            if (currentHealth <= 0)
            {
                Debug.Log("💀 PLAYER DIED!");
            }
        }

        // ALWAYS play hit reaction (even after death for testing feedback)
        if (playerCombat != null)
        {
            playerCombat.PlayHitReaction(isHeavy, attackerPos);
        }
    }

    public void ApplyStun(float duration)
    {
        stunTimer = duration;
        Debug.Log($"😵 Player STUNNED for {duration}s");
    }

    public void Heal(int amount)
    {
        currentHealth += amount;
        if (currentHealth > maxHealth) currentHealth = maxHealth;

        Debug.Log($"💚 HEAL! {amount} → HP: {currentHealth}/{maxHealth}");

        if (peachHealthUI != null)
            peachHealthUI.UpdateHealth(currentHealth);
    }

    public int GetCurrentHealth() => currentHealth;
    public int GetMaxHealth() => maxHealth;
    public bool IsStunned() => stunTimer > 0;
    public bool IsInvincible() => iFrameTimer > 0;
    public bool IsAlive() => currentHealth > 0;
}