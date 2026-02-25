using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Player Health — updated with i-frames and stun support.
/// </summary>
public class PlayerHealth : MonoBehaviour
{
    [Header("Health Settings")]
    [SerializeField] private int maxHealth = 12;
    private int currentHealth;
    
    [Header("I-Frames")]
    [Tooltip("Invincibility duration after taking damage")]
    [SerializeField] private float iFrameDuration = 0.5f;
    private float iFrameTimer;
    
    [Header("Stun")]
    private float stunTimer;
    
    [Header("UI")]
    [SerializeField] private Image healthBarFill;
    [SerializeField] private TMP_Text healthText;
    
    private PeachHealthUI peachHealthUI;
    private Animator animator;
    
    void Start()
    {
        currentHealth = maxHealth;
        animator = GetComponent<Animator>();
        peachHealthUI = FindObjectOfType<PeachHealthUI>();
        
        if (peachHealthUI != null)
            peachHealthUI.UpdateHealth(currentHealth);
    }
    
    void Update()
    {
        // Tick i-frame timer
        if (iFrameTimer > 0)
            iFrameTimer -= Time.deltaTime;
        
        // Tick stun timer
        if (stunTimer > 0)
            stunTimer -= Time.deltaTime;
    }
    
    public void TakeDamage(int damage)
    {
        if (currentHealth <= 0) return;
        
        // I-frame check — invincible after recent hit
        if (iFrameTimer > 0)
        {
            Debug.Log($"🛡️ I-FRAMES active, damage ignored");
            return;
        }
        
        currentHealth -= damage;
        if (currentHealth < 0) currentHealth = 0;
        
        // Start i-frames
        iFrameTimer = iFrameDuration;
        
        Debug.Log($"💔 DAMAGE! {damage} dmg → HP: {currentHealth}/{maxHealth}");
        
        if (peachHealthUI != null)
            peachHealthUI.UpdateHealth(currentHealth);
        
        if (currentHealth <= 0)
        {
            Debug.Log("💀 PLAYER DIED!");
            // TODO: death animation, game over screen
        }
    }
    
    /// <summary>
    /// Stun player — prevents movement for duration.
    /// Called by enemy scream attacks.
    /// PlayerMovement checks IsStunned() to block input.
    /// </summary>
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
    
    // Getters
    public int GetCurrentHealth() => currentHealth;
    public int GetMaxHealth() => maxHealth;
    public bool IsStunned() => stunTimer > 0;
    public bool IsInvincible() => iFrameTimer > 0;
    public bool IsAlive() => currentHealth > 0;
}