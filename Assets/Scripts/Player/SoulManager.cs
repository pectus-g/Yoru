using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SoulManager : MonoBehaviour
{
    [Header("Soul Settings")]
    [SerializeField] private int maxSoul = 100;
    private int currentSoul;
    
    [Header("Regeneration")]
    [SerializeField] private float soulRegenRate = 0.5f; // Soul per second
    [SerializeField] private float regenDelay = 5f; // Delay after using soul
    private float regenTimer = 0f;
    private float regenAccumulator = 0f; // Accumulates fractional soul regen
    
    [Header("UI References")]
    [SerializeField] private Image soulBarFill;
    [SerializeField] private TMP_Text soulText;
    
    [Header("Soul Bar Colors")]
    [SerializeField] private Color soulHighColor = new Color(0.4f, 0.6f, 1f); // Blue
    [SerializeField] private Color soulMediumColor = new Color(0.6f, 0.4f, 1f); // Purple
    [SerializeField] private Color soulLowColor = new Color(0.8f, 0.3f, 0.8f); // Magenta
    
    // Reference to rect transform for scaling
    private RectTransform fillRect;
    
    void Start()
    {
        currentSoul = maxSoul;
        
        if (soulBarFill != null)
        {
            fillRect = soulBarFill.GetComponent<RectTransform>();
        }
        
        UpdateSoulBar();
        
        Debug.Log("SoulManager started. Current soul: " + currentSoul);
    }
    
    void Update()
    {
        HandleSoulRegeneration();
    }
    
    private void HandleSoulRegeneration()
    {
        // Only regenerate if not at max soul
        if (currentSoul >= maxSoul)
        {
            regenAccumulator = 0f;
            return;
        }
        
        // Handle regen delay countdown
        if (regenTimer > 0)
        {
            regenTimer -= Time.deltaTime;
            regenAccumulator = 0f; // Reset accumulator during delay
            
            // Debug log every second
            if (Mathf.FloorToInt(regenTimer) != Mathf.FloorToInt(regenTimer + Time.deltaTime))
            {
                Debug.Log($"Regen delay: {regenTimer:F1}s remaining");
            }
            
            return;
        }
        
        // Accumulate regeneration over time
        regenAccumulator += soulRegenRate * Time.deltaTime;
        
        // Only add integer soul when we've accumulated at least 1
        if (regenAccumulator >= 1f)
        {
            int soulToAdd = Mathf.FloorToInt(regenAccumulator);
            currentSoul += soulToAdd;
            currentSoul = Mathf.Clamp(currentSoul, 0, maxSoul);
            regenAccumulator -= soulToAdd; // Keep the fractional remainder
            
            // Debug log
            Debug.Log($"Added {soulToAdd} soul. Current: {currentSoul}/{maxSoul}");
            
            UpdateSoulBar();
        }
    }
    
    /// <summary>
    /// Spend soul for abilities
    /// </summary>
    public bool SpendSoul(int amount)
    {
        if (currentSoul >= amount)
        {
            currentSoul -= amount;
            currentSoul = Mathf.Clamp(currentSoul, 0, maxSoul);
            
            Debug.Log($"Spent {amount} soul. Remaining: {currentSoul}/{maxSoul}");
            
            UpdateSoulBar();
            
            // Start regen delay
            regenTimer = regenDelay;
            regenAccumulator = 0f;
            
            return true;
        }
        else
        {
            Debug.Log($"Not enough soul! Need {amount}, have {currentSoul}");
            return false;
        }
    }
    
    /// <summary>
    /// Restore soul (from pickups)
    /// </summary>
    public void RestoreSoul(int amount)
    {
        currentSoul += amount;
        currentSoul = Mathf.Clamp(currentSoul, 0, maxSoul);
        
        Debug.Log($"Restored {amount} soul. Current: {currentSoul}/{maxSoul}");
        
        UpdateSoulBar();
    }
    
    /// <summary>
    /// Drain soul (enemy attack)
    /// </summary>
    public void DrainSoul(int amount)
    {
        currentSoul -= amount;
        currentSoul = Mathf.Clamp(currentSoul, 0, maxSoul);
        
        Debug.Log($"Drained {amount} soul. Current: {currentSoul}/{maxSoul}");
        
        UpdateSoulBar();
        
        // Start regen delay
        regenTimer = regenDelay;
        regenAccumulator = 0f;
    }
    
    /// <summary>
    /// Set soul to specific amount
    /// </summary>
    public void SetSoul(int amount)
    {
        currentSoul = Mathf.Clamp(amount, 0, maxSoul);
        UpdateSoulBar();
    }
    
    /// <summary>
    /// Update soul bar UI
    /// </summary>
    private void UpdateSoulBar()
    {
        float soulPercentage = (float)currentSoul / maxSoul;
        
        // Update fill using Rect Transform scaling
        if (fillRect != null)
        {
            fillRect.anchorMax = new Vector2(soulPercentage, 1f);
        }
        
        // Update color based on percentage
        if (soulBarFill != null)
        {
            if (soulPercentage > 0.5f)
                soulBarFill.color = soulHighColor;
            else if (soulPercentage > 0.25f)
                soulBarFill.color = soulMediumColor;
            else
                soulBarFill.color = soulLowColor;
        }
        
        // Update text
        if (soulText != null)
        {
            soulText.text = $"{currentSoul} / {maxSoul}";
        }
    }
    
    // Public getters
    public int GetCurrentSoul() => currentSoul;
    public int GetMaxSoul() => maxSoul;
    public float GetSoulPercentage() => (float)currentSoul / maxSoul;
    public bool HasEnoughSoul(int amount) => currentSoul >= amount;
}