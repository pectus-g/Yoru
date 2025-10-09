using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PlayerHealth : MonoBehaviour
{
    [Header("Health Settings")]
    [SerializeField] private int maxHealth = 100;
    [SerializeField] private int currentHealth;
    
    [Header("UI References")]
    [SerializeField] private Image healthBarFill; // Changed from Slider to Image!
    [SerializeField] private TMP_Text healthText; // Optional text display
    
    [Header("Health Bar Colors")]
    [SerializeField] private Color healthHighColor = Color.green;
    [SerializeField] private Color healthMediumColor = Color.yellow;
    [SerializeField] private Color healthLowColor = Color.red;
    
    [Header("Invincibility")]
    [SerializeField] private float invincibilityDuration = 1f;
    private bool isInvincible = false;
    private float invincibilityTimer = 0f;
    
    void Start()
    {
        currentHealth = maxHealth;
        UpdateHealthBar();
    }
    
    void Update()
    {
        if (isInvincible)
        {
            invincibilityTimer -= Time.deltaTime;
            if (invincibilityTimer <= 0)
            {
                isInvincible = false;
            }
        }
    }
    
    public void TakeDamage(int damage)
    {
        if (isInvincible) return;
        
        currentHealth -= damage;
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);
        
        Debug.Log($"Player took {damage} damage. Health: {currentHealth}/{maxHealth}");
        
        UpdateHealthBar();
        
        isInvincible = true;
        invincibilityTimer = invincibilityDuration;
        
        if (currentHealth <= 0)
        {
            Die();
        }
    }
    
    public void Heal(int amount)
    {
        currentHealth += amount;
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);
        
        Debug.Log($"Player healed {amount}. Health: {currentHealth}/{maxHealth}");
        UpdateHealthBar();
    }
    
    private void UpdateHealthBar()
{
    float healthPercentage = (float)currentHealth / maxHealth;
    
    // Update using Rect Transform scale
    if (healthBarFill != null)
    {
        // Get the RectTransform
        RectTransform fillRect = healthBarFill.GetComponent<RectTransform>();
        
        // Scale the width based on health percentage
        fillRect.anchorMax = new Vector2(healthPercentage, 1f);
        
        // Update color based on health percentage
        if (healthPercentage > 0.5f)
            healthBarFill.color = healthHighColor;
        else if (healthPercentage > 0.25f)
            healthBarFill.color = healthMediumColor;
        else
            healthBarFill.color = healthLowColor;
    }
    
    // Update text (optional)
    if (healthText != null)
    {
        healthText.text = $"{currentHealth} / {maxHealth}";
    }
}
    
    private void Die()
    {
        Debug.Log("Player died!");
        // Add death logic here later
    }
    
    // Public getters
    public int GetCurrentHealth() => currentHealth;
    public int GetMaxHealth() => maxHealth;
    public float GetHealthPercentage() => (float)currentHealth / maxHealth;
    public bool IsAlive() => currentHealth > 0;
}