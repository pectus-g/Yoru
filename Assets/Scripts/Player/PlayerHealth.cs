using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PlayerHealth : MonoBehaviour
{
    [Header("Health Settings")]
    [SerializeField] private int maxHealth = 12;
    private int currentHealth;
    
    [Header("UI")]
    [SerializeField] private Image healthBarFill;
    [SerializeField] private TMP_Text healthText;
    
    private PeachHealthUI peachHealthUI;
    
    void Start()
    {
        currentHealth = maxHealth;
        peachHealthUI = FindObjectOfType<PeachHealthUI>();
        
        if (peachHealthUI != null)
        {
            peachHealthUI.UpdateHealth(currentHealth);
        }
    }
    
    public void TakeDamage(int damage)
    {
        currentHealth -= damage;
        if (currentHealth < 0) currentHealth = 0;
        
        Debug.Log($"💔 DAMAGE! Current HP: {currentHealth}/{maxHealth}");
        
        if (peachHealthUI != null)
        {
            peachHealthUI.UpdateHealth(currentHealth);
        }
        
        if (currentHealth <= 0)
        {
            Debug.Log("💀 PLAYER DIED!");
        }
    }
    
    public void Heal(int amount)
    {
        currentHealth += amount;
        if (currentHealth > maxHealth) currentHealth = maxHealth;
        
        Debug.Log($"💚 HEAL! Current HP: {currentHealth}/{maxHealth}");
        
        if (peachHealthUI != null)
        {
            peachHealthUI.UpdateHealth(currentHealth);
        }
    }
    
    public int GetCurrentHealth() => currentHealth;
    public int GetMaxHealth() => maxHealth;
}