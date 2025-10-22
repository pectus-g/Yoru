using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SoulManager : MonoBehaviour
{
    [Header("Soul Settings")]
    [SerializeField] private int maxSoul = 12; // Changed from 100!
    private int currentSoul;
    
    [Header("Regeneration")]
    [SerializeField] private float soulRegenRate = 0.5f;
    [SerializeField] private float regenDelay = 5f;
    private float regenTimer = 0f;
    private float regenAccumulator = 0f;
    
    [Header("OLD UI References")]
    [SerializeField] private Image soulBarFill;
    [SerializeField] private TMP_Text soulText;
    
    [Header("Soul Bar Colors")]
    [SerializeField] private Color soulHighColor = new Color(0.4f, 0.6f, 1f);
    [SerializeField] private Color soulMediumColor = new Color(0.6f, 0.4f, 1f);
    [SerializeField] private Color soulLowColor = new Color(0.8f, 0.3f, 0.8f);
    
    private RectTransform fillRect;
    private SoulIconUI soulIconUI;
    
    void Start()
    {
        currentSoul = maxSoul;
        
        if (soulBarFill != null)
        {
            fillRect = soulBarFill.GetComponent<RectTransform>();
        }
        
        soulIconUI = FindObjectOfType<SoulIconUI>();
        
        UpdateSoulBar();
        
        if (soulIconUI != null)
        {
            soulIconUI.UpdateSoul(currentSoul);
            Debug.Log("✅ Soul icon system connected!");
        }
    }
    
    void Update()
    {
        HandleSoulRegeneration();
    }
    
    private void HandleSoulRegeneration()
    {
        if (currentSoul >= maxSoul)
        {
            regenAccumulator = 0f;
            return;
        }
        
        if (regenTimer > 0)
        {
            regenTimer -= Time.deltaTime;
            regenAccumulator = 0f;
            return;
        }
        
        regenAccumulator += soulRegenRate * Time.deltaTime;
        
        if (regenAccumulator >= 1f)
        {
            int soulToAdd = Mathf.FloorToInt(regenAccumulator);
            currentSoul += soulToAdd;
            currentSoul = Mathf.Clamp(currentSoul, 0, maxSoul);
            regenAccumulator -= soulToAdd;
            
            Debug.Log($"💙 Regen: {currentSoul}/{maxSoul}");
            
            UpdateSoulBar();
            
            if (soulIconUI != null)
            {
                soulIconUI.UpdateSoul(currentSoul);
            }
        }
    }
    
    public bool SpendSoul(int amount)
    {
        if (currentSoul >= amount)
        {
            currentSoul -= amount;
            currentSoul = Mathf.Clamp(currentSoul, 0, maxSoul);
            
            Debug.Log($"🔮 Spent {amount} soul. Remaining: {currentSoul}/{maxSoul}");
            
            UpdateSoulBar();
            
            if (soulIconUI != null)
            {
                soulIconUI.UpdateSoul(currentSoul);
            }
            
            regenTimer = regenDelay;
            regenAccumulator = 0f;
            
            return true;
        }
        else
        {
            Debug.Log($"❌ Not enough soul! Need {amount}, have {currentSoul}");
            return false;
        }
    }
    
    public void RestoreSoul(int amount)
    {
        currentSoul += amount;
        currentSoul = Mathf.Clamp(currentSoul, 0, maxSoul);
        
        Debug.Log($"💙 Restored {amount} soul. Current: {currentSoul}/{maxSoul}");
        
        UpdateSoulBar();
        
        if (soulIconUI != null)
        {
            soulIconUI.UpdateSoul(currentSoul);
        }
    }
    
    public void DrainSoul(int amount)
    {
        currentSoul -= amount;
        currentSoul = Mathf.Clamp(currentSoul, 0, maxSoul);
        
        Debug.Log($"💔 Drained {amount} soul. Current: {currentSoul}/{maxSoul}");
        
        UpdateSoulBar();
        
        if (soulIconUI != null)
        {
            soulIconUI.UpdateSoul(currentSoul);
        }
        
        regenTimer = regenDelay;
        regenAccumulator = 0f;
    }
    
    public void SetSoul(int amount)
    {
        currentSoul = Mathf.Clamp(amount, 0, maxSoul);
        UpdateSoulBar();
        
        if (soulIconUI != null)
        {
            soulIconUI.UpdateSoul(currentSoul);
        }
    }
    
    private void UpdateSoulBar()
    {
        float soulPercentage = (float)currentSoul / maxSoul;
        
        if (fillRect != null)
        {
            fillRect.anchorMax = new Vector2(soulPercentage, 1f);
        }
        
        if (soulBarFill != null)
        {
            if (soulPercentage > 0.5f)
                soulBarFill.color = soulHighColor;
            else if (soulPercentage > 0.25f)
                soulBarFill.color = soulMediumColor;
            else
                soulBarFill.color = soulLowColor;
        }
        
        if (soulText != null)
        {
            soulText.text = $"{currentSoul} / {maxSoul}";
        }
    }
    
    public int GetCurrentSoul() => currentSoul;
    public int GetMaxSoul() => maxSoul;
    public float GetSoulPercentage() => (float)currentSoul / maxSoul;
    public bool HasEnoughSoul(int amount) => currentSoul >= amount;
}