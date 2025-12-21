using UnityEngine;

/// <summary>
/// Controls the Ancient Tree's appearance based on karma balance.
/// 
/// Dark (more LEFT rings): Bare branches, dark bark, red/purple glow
/// Light (more RIGHT rings): Full foliage, golden bark, white/gold glow
/// Eclipse (5L + 5R): Mystical appearance, both light and dark elements
/// 
/// Requires: Tree with separate mesh renderers or material property blocks
/// </summary>
public class TreeController : MonoBehaviour
{
    #region Serialized Fields
    
    [Header("Tree References")]
    [SerializeField] private Renderer treeBarkRenderer;
    [SerializeField] private Renderer treeLeavesRenderer;
    [SerializeField] private Light treeGlowLight;
    [SerializeField] private ParticleSystem treeParticles;
    
    [Header("Transition")]
    [SerializeField, Range(0.1f, 2f)] private float transitionSpeed = 0.4f;
    
    [Header("Dark Settings (More LEFT rings)")]
    [SerializeField] private Color darkBarkColor = new Color(0.2f, 0.15f, 0.2f);      // Dark purple-grey
    [SerializeField] private Color darkLeafColor = new Color(0.3f, 0.1f, 0.15f);      // Dead red-brown
    [SerializeField, Range(0, 1)] private float darkLeafDensity = 0.2f;                // Sparse leaves
    [SerializeField] private Color darkGlowColor = new Color(0.8f, 0.2f, 0.3f);       // Red glow
    [SerializeField, Range(0, 5)] private float darkGlowIntensity = 2f;
    
    [Header("Light Settings (More RIGHT rings)")]
    [SerializeField] private Color lightBarkColor = new Color(0.6f, 0.5f, 0.35f);     // Warm golden brown
    [SerializeField] private Color lightLeafColor = new Color(0.4f, 0.7f, 0.3f);      // Vibrant green
    [SerializeField, Range(0, 1)] private float lightLeafDensity = 1f;                 // Full foliage
    [SerializeField] private Color lightGlowColor = new Color(1f, 0.95f, 0.7f);       // Golden white
    [SerializeField, Range(0, 5)] private float lightGlowIntensity = 3f;
    
    [Header("Neutral Settings (Game Start)")]
    [SerializeField] private Color neutralBarkColor = new Color(0.4f, 0.35f, 0.3f);   // Natural brown
    [SerializeField] private Color neutralLeafColor = new Color(0.3f, 0.5f, 0.25f);   // Normal green
    [SerializeField, Range(0, 1)] private float neutralLeafDensity = 0.7f;
    [SerializeField] private Color neutralGlowColor = new Color(0.8f, 0.85f, 1f);     // Soft white-blue
    [SerializeField, Range(0, 5)] private float neutralGlowIntensity = 1.5f;
    
    [Header("Eclipse Settings (Perfect Balance 5L + 5R)")]
    [SerializeField] private Color eclipseBarkColor = new Color(0.35f, 0.3f, 0.4f);   // Mystical purple-grey
    [SerializeField] private Color eclipseLeafColor = new Color(0.5f, 0.4f, 0.6f);    // Purple-tinted
    [SerializeField, Range(0, 1)] private float eclipseLeafDensity = 0.85f;
    [SerializeField] private Color eclipseGlowColor = new Color(0.9f, 0.6f, 1f);      // Bright purple
    [SerializeField, Range(0, 5)] private float eclipseGlowIntensity = 4f;
    
    [Header("Material Properties")]
    [SerializeField] private string colorPropertyName = "_BaseColor";
    [SerializeField] private string emissionPropertyName = "_EmissionColor";
    
    #endregion
    
    #region Private State
    
    private struct TreeState
    {
        public Color barkColor;
        public Color leafColor;
        public float leafDensity;
        public Color glowColor;
        public float glowIntensity;
        
        public static TreeState Lerp(TreeState a, TreeState b, float t)
        {
            return new TreeState
            {
                barkColor = Color.Lerp(a.barkColor, b.barkColor, t),
                leafColor = Color.Lerp(a.leafColor, b.leafColor, t),
                leafDensity = Mathf.Lerp(a.leafDensity, b.leafDensity, t),
                glowColor = Color.Lerp(a.glowColor, b.glowColor, t),
                glowIntensity = Mathf.Lerp(a.glowIntensity, b.glowIntensity, t)
            };
        }
        
        public bool ApproximatelyEquals(TreeState other, float tolerance = 0.001f)
        {
            return Mathf.Abs(leafDensity - other.leafDensity) < tolerance &&
                   Mathf.Abs(glowIntensity - other.glowIntensity) < tolerance &&
                   ColorApprox(barkColor, other.barkColor, tolerance) &&
                   ColorApprox(leafColor, other.leafColor, tolerance) &&
                   ColorApprox(glowColor, other.glowColor, tolerance);
        }
        
        private static bool ColorApprox(Color a, Color b, float t)
        {
            return Mathf.Abs(a.r - b.r) < t && Mathf.Abs(a.g - b.g) < t && Mathf.Abs(a.b - b.b) < t;
        }
    }
    
    private TreeState currentState;
    private TreeState targetState;
    private bool isTransitioning;
    
    // Material property blocks (avoid creating new materials)
    private MaterialPropertyBlock barkPropertyBlock;
    private MaterialPropertyBlock leafPropertyBlock;
    
    #endregion
    
    #region Unity Lifecycle
    
    private void Start()
    {
        barkPropertyBlock = new MaterialPropertyBlock();
        leafPropertyBlock = new MaterialPropertyBlock();
        
        InitializeState();
        SubscribeToEvents();
    }
    
    private void OnDestroy()
    {
        if (WorldStateManager.Instance != null)
            WorldStateManager.Instance.OnRingsChanged.RemoveListener(OnRingsChanged);
    }
    
    private void Update()
    {
        if (!isTransitioning) return;
        
        float t = transitionSpeed * Time.deltaTime;
        currentState = TreeState.Lerp(currentState, targetState, t);
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
        currentState = new TreeState
        {
            barkColor = neutralBarkColor,
            leafColor = neutralLeafColor,
            leafDensity = neutralLeafDensity,
            glowColor = neutralGlowColor,
            glowIntensity = neutralGlowIntensity
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
    
    private TreeState CalculateTargetState(int left, int right)
    {
        // Perfect Balance - Eclipse
        if (left == 5 && right == 5)
        {
            return new TreeState
            {
                barkColor = eclipseBarkColor,
                leafColor = eclipseLeafColor,
                leafDensity = eclipseLeafDensity,
                glowColor = eclipseGlowColor,
                glowIntensity = eclipseGlowIntensity
            };
        }
        
        float balance = (right - left) / 5f;
        TreeState state;
        
        if (balance >= 0)
        {
            state = new TreeState
            {
                barkColor = Color.Lerp(neutralBarkColor, lightBarkColor, balance),
                leafColor = Color.Lerp(neutralLeafColor, lightLeafColor, balance),
                leafDensity = Mathf.Lerp(neutralLeafDensity, lightLeafDensity, balance),
                glowColor = Color.Lerp(neutralGlowColor, lightGlowColor, balance),
                glowIntensity = Mathf.Lerp(neutralGlowIntensity, lightGlowIntensity, balance)
            };
        }
        else
        {
            float darkAmount = -balance;
            state = new TreeState
            {
                barkColor = Color.Lerp(neutralBarkColor, darkBarkColor, darkAmount),
                leafColor = Color.Lerp(neutralLeafColor, darkLeafColor, darkAmount),
                leafDensity = Mathf.Lerp(neutralLeafDensity, darkLeafDensity, darkAmount),
                glowColor = Color.Lerp(neutralGlowColor, darkGlowColor, darkAmount),
                glowIntensity = Mathf.Lerp(neutralGlowIntensity, darkGlowIntensity, darkAmount)
            };
        }
        
        return state;
    }
    
    #endregion
    
    #region Apply State
    
    private void ApplyState(TreeState state)
    {
        // Bark
        if (treeBarkRenderer != null)
        {
            treeBarkRenderer.GetPropertyBlock(barkPropertyBlock);
            barkPropertyBlock.SetColor(colorPropertyName, state.barkColor);
            treeBarkRenderer.SetPropertyBlock(barkPropertyBlock);
        }
        
        // Leaves
        if (treeLeavesRenderer != null)
        {
            treeLeavesRenderer.GetPropertyBlock(leafPropertyBlock);
            leafPropertyBlock.SetColor(colorPropertyName, state.leafColor);
            treeLeavesRenderer.SetPropertyBlock(leafPropertyBlock);
            
            // Leaf density via alpha or scale
            Color leafWithAlpha = state.leafColor;
            leafWithAlpha.a = state.leafDensity;
            leafPropertyBlock.SetColor(colorPropertyName, leafWithAlpha);
            treeLeavesRenderer.SetPropertyBlock(leafPropertyBlock);
        }
        
        // Glow light
        if (treeGlowLight != null)
        {
            treeGlowLight.color = state.glowColor;
            treeGlowLight.intensity = state.glowIntensity;
        }
        
        // Particles
        if (treeParticles != null)
        {
            var main = treeParticles.main;
            main.startColor = state.glowColor;
        }
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
    
    #endregion
}