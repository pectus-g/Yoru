using UnityEngine;

/// <summary>
/// Controls vegetation/foliage color tinting based on karma balance.
/// 
/// Dark (more LEFT rings): Desaturated, purple/grey tint, withered look
/// Light (more RIGHT rings): Vibrant greens, golden highlights
/// Eclipse (5L + 5R): Mystical purple/silver tint
/// 
/// Works with: Terrain grass, SpeedTree, custom foliage shaders
/// Uses global shader property for efficiency across all vegetation.
/// </summary>
public class FoliageController : MonoBehaviour
{
    #region Serialized Fields
    
    [Header("Transition")]
    [SerializeField, Range(0.1f, 2f)] private float transitionSpeed = 0.4f;
    
    [Header("Color Mode")]
    [SerializeField] private FoliageColorMode colorMode = FoliageColorMode.GlobalShaderProperty;
    
    [Header("Dark Settings (More LEFT rings)")]
    [SerializeField] private Color darkFoliageTint = new Color(0.5f, 0.45f, 0.55f);    // Desaturated purple-grey
    [SerializeField] private Color darkGrassTint = new Color(0.4f, 0.35f, 0.3f);       // Dead brown
    [SerializeField, Range(0, 2)] private float darkSaturation = 0.5f;
    
    [Header("Light Settings (More RIGHT rings)")]
    [SerializeField] private Color lightFoliageTint = new Color(0.5f, 0.8f, 0.4f);     // Vibrant green
    [SerializeField] private Color lightGrassTint = new Color(0.6f, 0.75f, 0.35f);     // Lush golden-green
    [SerializeField, Range(0, 2)] private float lightSaturation = 1.3f;
    
    [Header("Neutral Settings (Game Start)")]
    [SerializeField] private Color neutralFoliageTint = new Color(0.45f, 0.6f, 0.35f); // Natural green
    [SerializeField] private Color neutralGrassTint = new Color(0.5f, 0.6f, 0.3f);
    [SerializeField, Range(0, 2)] private float neutralSaturation = 1f;
    
    [Header("Eclipse Settings (Perfect Balance 5L + 5R)")]
    [SerializeField] private Color eclipseFoliageTint = new Color(0.55f, 0.5f, 0.65f); // Mystical purple
    [SerializeField] private Color eclipseGrassTint = new Color(0.5f, 0.45f, 0.55f);
    [SerializeField, Range(0, 2)] private float eclipseSaturation = 0.9f;
    
    [Header("Direct Renderer Mode (Optional)")]
    [Tooltip("Assign specific foliage renderers if not using global shader property")]
    [SerializeField] private Renderer[] foliageRenderers;
    [SerializeField] private Terrain terrain;
    
    [Header("Shader Property Names")]
    [SerializeField] private string globalFoliageTintProperty = "_GlobalFoliageTint";
    [SerializeField] private string globalGrassTintProperty = "_GlobalGrassTint";
    [SerializeField] private string globalSaturationProperty = "_GlobalVegetationSaturation";
    [SerializeField] private string colorPropertyName = "_BaseColor";
    
    #endregion
    
    #region Private State
    
    private struct FoliageState
    {
        public Color foliageTint;
        public Color grassTint;
        public float saturation;
        
        public static FoliageState Lerp(FoliageState a, FoliageState b, float t)
        {
            return new FoliageState
            {
                foliageTint = Color.Lerp(a.foliageTint, b.foliageTint, t),
                grassTint = Color.Lerp(a.grassTint, b.grassTint, t),
                saturation = Mathf.Lerp(a.saturation, b.saturation, t)
            };
        }
        
        public bool ApproximatelyEquals(FoliageState other, float tolerance = 0.001f)
        {
            return Mathf.Abs(saturation - other.saturation) < tolerance &&
                   ColorApprox(foliageTint, other.foliageTint, tolerance) &&
                   ColorApprox(grassTint, other.grassTint, tolerance);
        }
        
        private static bool ColorApprox(Color a, Color b, float t)
        {
            return Mathf.Abs(a.r - b.r) < t && Mathf.Abs(a.g - b.g) < t && Mathf.Abs(a.b - b.b) < t;
        }
    }
    
    private FoliageState currentState;
    private FoliageState targetState;
    private bool isTransitioning;
    
    // Material property blocks for direct renderer mode
    private MaterialPropertyBlock propertyBlock;
    
    #endregion
    
    #region Enums
    
    public enum FoliageColorMode
    {
        GlobalShaderProperty,   // Sets global shader properties (best for many objects)
        DirectRenderers,        // Directly modifies assigned renderers
        Both                    // Uses both methods
    }
    
    #endregion
    
    #region Unity Lifecycle
    
    private void Start()
    {
        propertyBlock = new MaterialPropertyBlock();
        
        InitializeState();
        SubscribeToEvents();
    }
    
    private void OnDestroy()
    {
        if (WorldStateManager.Instance != null)
            WorldStateManager.Instance.OnRingsChanged.RemoveListener(OnRingsChanged);
        
        // Reset global properties
        if (colorMode != FoliageColorMode.DirectRenderers)
        {
            Shader.SetGlobalColor(globalFoliageTintProperty, Color.white);
            Shader.SetGlobalColor(globalGrassTintProperty, Color.white);
            Shader.SetGlobalFloat(globalSaturationProperty, 1f);
        }
    }
    
    private void Update()
    {
        if (!isTransitioning) return;
        
        float t = transitionSpeed * Time.deltaTime;
        currentState = FoliageState.Lerp(currentState, targetState, t);
        ApplyState(currentState);
        
        if (currentState.ApproximatelyEquals(targetState))
        {
            currentState = targetState;
            ApplyState(currentState);
            isTransitioning = false;
        }
    }
    
    #endregion
    
    #region Setup
    
    private void InitializeState()
    {
        currentState = new FoliageState
        {
            foliageTint = neutralFoliageTint,
            grassTint = neutralGrassTint,
            saturation = neutralSaturation
        };
        targetState = currentState;
        ApplyState(currentState);
    }
    
    private void SubscribeToEvents()
    {
        if (WorldStateManager.Instance != null)
        {
            WorldStateManager.Instance.OnRingsChanged.AddListener(OnRingsChanged);
            OnRingsChanged(WorldStateManager.Instance.LeftRings, WorldStateManager.Instance.RightRings);
        }
    }
    
    #endregion
    
    #region Event Handler
    
    private void OnRingsChanged(int left, int right)
    {
        targetState = CalculateTargetState(left, right);
        isTransitioning = true;
    }
    
    #endregion
    
    #region State Calculation
    
    private FoliageState CalculateTargetState(int left, int right)
    {
        // Perfect Balance - Eclipse
        if (left == 5 && right == 5)
        {
            return new FoliageState
            {
                foliageTint = eclipseFoliageTint,
                grassTint = eclipseGrassTint,
                saturation = eclipseSaturation
            };
        }
        
        float balance = (right - left) / 5f;
        FoliageState state;
        
        if (balance >= 0)
        {
            state = new FoliageState
            {
                foliageTint = Color.Lerp(neutralFoliageTint, lightFoliageTint, balance),
                grassTint = Color.Lerp(neutralGrassTint, lightGrassTint, balance),
                saturation = Mathf.Lerp(neutralSaturation, lightSaturation, balance)
            };
        }
        else
        {
            float darkAmount = -balance;
            state = new FoliageState
            {
                foliageTint = Color.Lerp(neutralFoliageTint, darkFoliageTint, darkAmount),
                grassTint = Color.Lerp(neutralGrassTint, darkGrassTint, darkAmount),
                saturation = Mathf.Lerp(neutralSaturation, darkSaturation, darkAmount)
            };
        }
        
        return state;
    }
    
    #endregion
    
    #region Apply State
    
    private void ApplyState(FoliageState state)
    {
        // Global shader properties
        if (colorMode == FoliageColorMode.GlobalShaderProperty || colorMode == FoliageColorMode.Both)
        {
            Shader.SetGlobalColor(globalFoliageTintProperty, state.foliageTint);
            Shader.SetGlobalColor(globalGrassTintProperty, state.grassTint);
            Shader.SetGlobalFloat(globalSaturationProperty, state.saturation);
        }
        
        // Direct renderers
        if (colorMode == FoliageColorMode.DirectRenderers || colorMode == FoliageColorMode.Both)
        {
            ApplyToRenderers(state);
            ApplyToTerrain(state);
        }
    }
    
    private void ApplyToRenderers(FoliageState state)
    {
        if (foliageRenderers == null) return;
        
        foreach (var renderer in foliageRenderers)
        {
            if (renderer == null) continue;
            
            renderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(colorPropertyName, state.foliageTint);
            renderer.SetPropertyBlock(propertyBlock);
        }
    }
    
    private void ApplyToTerrain(FoliageState state)
    {
        if (terrain == null) return;
        
        // Terrain grass tint
        terrain.terrainData.wavingGrassTint = state.grassTint;
    }
    
    #endregion
    
    #region Public API
    
    public void SnapToCurrentState()
    {
        if (WorldStateManager.Instance == null) return;
        
        targetState = CalculateTargetState(
            WorldStateManager.Instance.LeftRings,
            WorldStateManager.Instance.RightRings
        );
        currentState = targetState;
        ApplyState(currentState);
        isTransitioning = false;
    }
    
    public void AddFoliageRenderer(Renderer renderer)
    {
        if (foliageRenderers == null)
        {
            foliageRenderers = new Renderer[] { renderer };
            return;
        }
        
        var newArray = new Renderer[foliageRenderers.Length + 1];
        foliageRenderers.CopyTo(newArray, 0);
        newArray[foliageRenderers.Length] = renderer;
        foliageRenderers = newArray;
    }
    
    public void SetTerrain(Terrain newTerrain)
    {
        terrain = newTerrain;
    }
    
    #endregion
}