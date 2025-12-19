using UnityEngine;
using DistantLands.Cozy;

/// <summary>
/// Connects WorldStateManager to COZY Eclipse module.
/// Controls the eclipse ratio based on karma ring balance.
/// 
/// Eclipse Logic:
/// - Eclipse appears when BOTH tails have rings AND they're balanced
/// - Maximum eclipse (ring of fire) at 5L + 5R (Perfect Balance)
/// - Unbalanced states (e.g., 5L + 0R) show no eclipse
/// </summary>
public class YoruCozyIntegration : MonoBehaviour
{
    // ═══════════════════════════════════════════════════════════
    // REFERENCES
    // ═══════════════════════════════════════════════════════════
    
    [Header("Eclipse Settings")]
    [Tooltip("How fast the eclipse transitions (higher = faster)")]
    [SerializeField] private float transitionSpeed = 0.5f;
    
    [Tooltip("Show debug info in console")]
    [SerializeField] private bool debugMode = true;
    
    // ═══════════════════════════════════════════════════════════
    // PRIVATE STATE
    // ═══════════════════════════════════════════════════════════
    
    private float targetEclipseRatio = 0f;
    private float currentEclipseRatio = 0f;
    private EclipseModule eclipseModule;
    
    // ═══════════════════════════════════════════════════════════
    // LIFECYCLE
    // ═══════════════════════════════════════════════════════════
    
    private void Start()
    {
        // Find Eclipse Module via COZY's GetModule API
        FindEclipseModule();
        
        // Subscribe to WorldStateManager
        if (WorldStateManager.Instance != null)
        {
            WorldStateManager.Instance.OnRingsChanged.AddListener(OnRingsChanged);
            
            // Initialize to current state
            OnRingsChanged(WorldStateManager.Instance.LeftRings, WorldStateManager.Instance.RightRings);
        }
        else
        {
            Debug.LogError("[YoruCozyIntegration] WorldStateManager.Instance not found!");
        }
    }
    
    private void OnDestroy()
    {
        if (WorldStateManager.Instance != null)
        {
            WorldStateManager.Instance.OnRingsChanged.RemoveListener(OnRingsChanged);
        }
    }
    
    private void Update()
    {
        // Smoothly transition toward target eclipse ratio
        if (Mathf.Abs(currentEclipseRatio - targetEclipseRatio) > 0.001f)
        {
            currentEclipseRatio = Mathf.MoveTowards(
                currentEclipseRatio,
                targetEclipseRatio,
                Time.deltaTime * transitionSpeed
            );
            
            ApplyEclipseRatio(currentEclipseRatio);
        }
    }
    
    // ═══════════════════════════════════════════════════════════
    // ECLIPSE MODULE SETUP
    // ═══════════════════════════════════════════════════════════
    
    private void FindEclipseModule()
    {
        // Use COZY's singleton and GetModule pattern
        if (CozyWeather.instance != null)
        {
            eclipseModule = CozyWeather.instance.GetModule<EclipseModule>();
            
            if (eclipseModule != null)
            {
                if (debugMode)
                    Debug.Log("[YoruCozyIntegration] Found Eclipse Module!");
            }
            else
            {
                Debug.LogError("[YoruCozyIntegration] Eclipse Module not found! " +
                              "Make sure Eclipse module is added to your COZY Weather setup.");
            }
        }
        else
        {
            Debug.LogError("[YoruCozyIntegration] CozyWeather.instance not found! " +
                          "Make sure COZY Weather exists in the scene.");
        }
    }
    
    // ═══════════════════════════════════════════════════════════
    // EVENT HANDLER
    // ═══════════════════════════════════════════════════════════
    
    private void OnRingsChanged(int leftRings, int rightRings)
    {
        targetEclipseRatio = CalculateEclipseRatio(leftRings, rightRings);
        
        if (debugMode)
        {
            Debug.Log($"[YoruCozyIntegration] Rings: {leftRings}L / {rightRings}R → Eclipse Target: {targetEclipseRatio:F2}");
        }
    }
    
    // ═══════════════════════════════════════════════════════════
    // ECLIPSE RATIO CALCULATION
    // ═══════════════════════════════════════════════════════════
    
    /// <summary>
    /// Calculate eclipse ratio based on ring counts.
    /// 
    /// The eclipse appears when:
    /// 1. Both tails have rings (need both light and dark)
    /// 2. The rings are balanced (equal or close to equal)
    /// 
    /// Eclipse Ratio Table:
    /// | Left | Right | Ratio | Why |
    /// |------|-------|-------|-----|
    /// | 0    | 0     | 0%    | No journey yet |
    /// | 5    | 0     | 0%    | Pure dark, no balance |
    /// | 0    | 5     | 0%    | Pure light, no balance |
    /// | 1    | 1     | 10%   | Starting balance |
    /// | 2    | 2     | 20%   | Early balance |
    /// | 3    | 3     | 40%   | Mid balance |
    /// | 4    | 4     | 70%   | Near perfect |
    /// | 5    | 5     | 100%  | PERFECT BALANCE |
    /// | 5    | 4     | 60%   | Almost there |
    /// | 4    | 5     | 60%   | Almost there |
    /// | 3    | 1     | 5%    | Unbalanced |
    /// </summary>
    private float CalculateEclipseRatio(int left, int right)
    {
        // No rings = no eclipse
        if (left == 0 && right == 0)
            return 0f;
        
        // Need BOTH tails to have rings for eclipse
        if (left == 0 || right == 0)
            return 0f;
        
        // Calculate balance factor (1.0 = perfectly balanced, 0.0 = completely unbalanced)
        int smaller = Mathf.Min(left, right);
        int larger = Mathf.Max(left, right);
        float balanceFactor = (float)smaller / larger;
        
        // Calculate progress factor (how many total rings, normalized)
        // Max total is 10 (5+5)
        float progressFactor = (float)(left + right) / 10f;
        
        // Eclipse ratio combines both factors
        // - Need high balance (both tails similar)
        // - Need high progress (many rings total)
        float ratio = balanceFactor * progressFactor;
        
        // Apply curve to make the final stretch more dramatic
        // This makes 5+5 feel like a real climax
        ratio = Mathf.Pow(ratio, 0.7f);
        
        // Perfect balance (5+5) always = 100%
        if (left == 5 && right == 5)
            ratio = 1f;
        
        return Mathf.Clamp01(ratio);
    }
    
    // ═══════════════════════════════════════════════════════════
    // APPLY TO COZY
    // ═══════════════════════════════════════════════════════════
    
    private void ApplyEclipseRatio(float ratio)
    {
        if (eclipseModule != null)
        {
            eclipseModule.eclipseRatio = ratio;
        }
    }
    
    // ═══════════════════════════════════════════════════════════
    // PUBLIC METHODS (for testing/debugging)
    // ═══════════════════════════════════════════════════════════
    
    /// <summary>
    /// Force eclipse to a specific ratio (0-1). For testing only.
    /// </summary>
    public void ForceEclipseRatio(float ratio)
    {
        targetEclipseRatio = Mathf.Clamp01(ratio);
        currentEclipseRatio = targetEclipseRatio;
        ApplyEclipseRatio(currentEclipseRatio);
        
        if (debugMode)
            Debug.Log($"[YoruCozyIntegration] Forced eclipse ratio to: {ratio:F2}");
    }
    
    /// <summary>
    /// Instantly snap to target (no smooth transition). For testing.
    /// </summary>
    public void SnapToTarget()
    {
        currentEclipseRatio = targetEclipseRatio;
        ApplyEclipseRatio(currentEclipseRatio);
    }
}