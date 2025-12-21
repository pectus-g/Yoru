using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Controls ending skyboxes based on final karma balance.
/// 
/// Game ends when TOTAL rings = 10 (except Perfect Balance which opens new chapter)
/// 
/// 7 ENDINGS (combinations that total 10):
/// 
/// 1. Pure Dark (10L, 0R) - Eternal darkness, Yoru consumed by shadow
/// 2. Dark Dominant (8-9L, 1-2R) - Tyrant spirit, feared ruler
/// 3. Dark Leaning (6-7L, 3-4R) - Uneasy peace through fear
/// 4. [Eclipse/Perfect Balance (5L, 5R) - Opens NEW CHAPTER, not an ending!]
/// 5. Light Leaning (3-4L, 6-7R) - Gentle harmony, some injustice remains
/// 6. Light Dominant (1-2L, 8-9R) - Revered deity, passive guardian
/// 7. Pure Light (0L, 10R) - World ascends, Yoru becomes divine
/// 
/// Note: Eclipse (5+5) triggers new chapter via OnPerfectBalanceAchieved event,
/// not handled by this controller.
/// </summary>
public class SkyboxController : MonoBehaviour
{
    #region Serialized Fields
    
    [Header("Transition")]
    [SerializeField, Range(0.5f, 5f)] private float skyboxTransitionDuration = 2f;
    
    [Header("=== ENDING SKYBOXES ===")]
    
    [Tooltip("10L, 0R - Eternal darkness")]
    [SerializeField] private Material pureDarkSkybox;
    
    [Tooltip("8-9L, 1-2R - Tyrant spirit")]
    [SerializeField] private Material darkDominantSkybox;
    
    [Tooltip("6-7L, 3-4R - Uneasy peace")]
    [SerializeField] private Material darkLeaningSkybox;
    
    [Tooltip("3-4L, 6-7R - Gentle harmony")]
    [SerializeField] private Material lightLeaningSkybox;
    
    [Tooltip("1-2L, 8-9R - Revered deity")]
    [SerializeField] private Material lightDominantSkybox;
    
    [Tooltip("0L, 10R - World ascends")]
    [SerializeField] private Material pureLightSkybox;
    
    [Header("Gameplay Skybox")]
    [Tooltip("Default skybox during normal gameplay")]
    [SerializeField] private Material neutralSkybox;
    
    [Header("Events")]
    public UnityEvent<EndingType> OnEndingTriggered;
    
    #endregion
    
    #region Private State
    
    private Material currentSkybox;
    private Material targetSkybox;
    private float transitionProgress;
    private bool isTransitioning;
    private EndingType currentEnding = EndingType.None;
    
    private static readonly int ExposureID = Shader.PropertyToID("_Exposure");
    
    #endregion
    
    #region Enums
    
    public enum EndingType
    {
        None,           // Game not ended
        PureDark,       // 10L, 0R
        DarkDominant,   // 8-9L, 1-2R
        DarkLeaning,    // 6-7L, 3-4R
        // Eclipse (5L, 5R) is NOT an ending - it opens new chapter
        LightLeaning,   // 3-4L, 6-7R
        LightDominant,  // 1-2L, 8-9R
        PureLight       // 0L, 10R
    }
    
    #endregion
    
    #region Unity Lifecycle
    
    private void Start()
    {
        currentSkybox = RenderSettings.skybox;
        OnEndingTriggered ??= new UnityEvent<EndingType>();
        
        // Subscribe to game end event
        if (WorldStateManager.Instance != null)
        {
            WorldStateManager.Instance.OnGameEndReached.AddListener(OnGameEndReached);
        }
    }
    
    private void OnDestroy()
    {
        if (WorldStateManager.Instance != null)
        {
            WorldStateManager.Instance.OnGameEndReached.RemoveListener(OnGameEndReached);
        }
    }
    
    private void Update()
    {
        if (!isTransitioning) return;
        
        transitionProgress += Time.deltaTime / skyboxTransitionDuration;
        
        if (transitionProgress >= 1f)
        {
            transitionProgress = 1f;
            isTransitioning = false;
            RenderSettings.skybox = targetSkybox;
            currentSkybox = targetSkybox;
        }
        else
        {
            ApplyTransition(transitionProgress);
        }
    }
    
    #endregion
    
    #region Event Handlers
    
    private void OnGameEndReached(int left, int right)
    {
        // Auto-trigger ending when game reaches 10 total rings
        TriggerEnding();
    }
    
    #endregion
    
    #region Public API
    
    /// <summary>
    /// Trigger ending based on current ring state.
    /// Called automatically when total rings reach 10.
    /// </summary>
    public void TriggerEnding()
    {
        if (WorldStateManager.Instance == null)
        {
            Debug.LogError("[SkyboxController] WorldStateManager not found!");
            return;
        }
        
        int left = WorldStateManager.Instance.LeftRings;
        int right = WorldStateManager.Instance.RightRings;
        
        // Perfect Balance is NOT an ending
        if (left == 5 && right == 5)
        {
            Debug.Log("[SkyboxController] Perfect Balance - This opens Eclipse chapter, not an ending.");
            return;
        }
        
        EndingType ending = DetermineEnding(left, right);
        TriggerEnding(ending);
    }
    
    /// <summary>
    /// Trigger a specific ending directly.
    /// </summary>
    public void TriggerEnding(EndingType ending)
    {
        if (ending == EndingType.None) return;
        
        currentEnding = ending;
        Material endingSkybox = GetSkyboxForEnding(ending);
        
        if (endingSkybox != null)
        {
            TransitionToSkybox(endingSkybox);
        }
        
        OnEndingTriggered?.Invoke(ending);
        
        Debug.Log($"[SkyboxController] ENDING TRIGGERED: {ending}");
    }
    
    /// <summary>
    /// Reset to gameplay skybox.
    /// </summary>
    public void ResetToNeutral()
    {
        if (neutralSkybox != null)
        {
            TransitionToSkybox(neutralSkybox);
        }
        currentEnding = EndingType.None;
    }
    
    /// <summary>
    /// Get current ending type.
    /// </summary>
    public EndingType GetCurrentEnding() => currentEnding;
    
    /// <summary>
    /// Preview what ending would trigger without actually triggering it.
    /// </summary>
    public EndingType PreviewEnding()
    {
        if (WorldStateManager.Instance == null) return EndingType.None;
        
        return DetermineEnding(
            WorldStateManager.Instance.LeftRings,
            WorldStateManager.Instance.RightRings
        );
    }
    
    /// <summary>
    /// Get ending description for UI.
    /// </summary>
    public static string GetEndingDescription(EndingType ending)
    {
        switch (ending)
        {
            case EndingType.PureDark:
                return "Eternal Darkness - Yoru is consumed by shadow, becoming one with the void.";
            case EndingType.DarkDominant:
                return "The Tyrant Spirit - Yoru rules through fear, an iron judge of the damned.";
            case EndingType.DarkLeaning:
                return "Uneasy Peace - Order through severity. The souls obey, but do not love.";
            case EndingType.LightLeaning:
                return "Gentle Harmony - Most souls find peace, though some injustices linger.";
            case EndingType.LightDominant:
                return "The Revered Deity - Worshipped but distant. Mercy without justice.";
            case EndingType.PureLight:
                return "Ascension - The world transcends. Yoru becomes divine light itself.";
            default:
                return "";
        }
    }
    
    #endregion
    
    #region Ending Determination
    
    private EndingType DetermineEnding(int left, int right)
    {
        int total = left + right;
        
        // Shouldn't happen, but safety check
        if (total < 10)
        {
            Debug.LogWarning($"[SkyboxController] Total rings ({total}) < 10. Ending not yet reached.");
            return EndingType.None;
        }
        
        // Perfect Balance is not an ending (opens new chapter)
        if (left == 5 && right == 5)
        {
            return EndingType.None;
        }
        
        // Pure extremes
        if (left == 10 && right == 0) return EndingType.PureDark;
        if (left == 0 && right == 10) return EndingType.PureLight;
        
        // Calculate balance
        int balance = right - left; // -10 to +10
        
        // Dark endings (negative balance)
        if (balance <= -6) return EndingType.DarkDominant;  // 8-9L, 1-2R
        if (balance < 0) return EndingType.DarkLeaning;      // 6-7L, 3-4R
        
        // Light endings (positive balance)
        if (balance >= 6) return EndingType.LightDominant;   // 1-2L, 8-9R
        if (balance > 0) return EndingType.LightLeaning;     // 3-4L, 6-7R
        
        // Exactly balanced but not 5+5 (shouldn't happen with 10 total)
        return EndingType.None;
    }
    
    private Material GetSkyboxForEnding(EndingType ending)
    {
        switch (ending)
        {
            case EndingType.PureDark: return pureDarkSkybox;
            case EndingType.DarkDominant: return darkDominantSkybox;
            case EndingType.DarkLeaning: return darkLeaningSkybox;
            case EndingType.LightLeaning: return lightLeaningSkybox;
            case EndingType.LightDominant: return lightDominantSkybox;
            case EndingType.PureLight: return pureLightSkybox;
            default: return neutralSkybox;
        }
    }
    
    #endregion
    
    #region Skybox Transition
    
    private void TransitionToSkybox(Material newSkybox)
    {
        if (newSkybox == null) return;
        
        targetSkybox = newSkybox;
        transitionProgress = 0f;
        isTransitioning = true;
    }
    
    private void ApplyTransition(float progress)
    {
        if (progress < 0.5f)
        {
            float exposure = Mathf.Lerp(1f, 0f, progress * 2f);
            if (currentSkybox != null && currentSkybox.HasProperty(ExposureID))
                currentSkybox.SetFloat(ExposureID, exposure);
            RenderSettings.skybox = currentSkybox;
        }
        else
        {
            float exposure = Mathf.Lerp(0f, 1f, (progress - 0.5f) * 2f);
            if (targetSkybox != null && targetSkybox.HasProperty(ExposureID))
                targetSkybox.SetFloat(ExposureID, exposure);
            RenderSettings.skybox = targetSkybox;
        }
        
        DynamicGI.UpdateEnvironment();
    }
    
    #endregion
    
    #region Editor Helpers
    
    /// <summary>
    /// Preview specific ending in editor.
    /// </summary>
    public void PreviewSkybox(EndingType ending)
    {
        Material skybox = GetSkyboxForEnding(ending);
        if (skybox != null)
        {
            RenderSettings.skybox = skybox;
            DynamicGI.UpdateEnvironment();
        }
    }
    
    #endregion
}