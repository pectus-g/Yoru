using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// YORU: World State Manager V3 - COMPLETE RING COMBINATION SYSTEM
/// 
/// Source of truth for all atmospheric state resolution. All other controllers
/// (PostProcess, Fog, Lighting, Ambience, RingMesh, Cozy, Music, LightPathFX)
/// subscribe to events fired here.
/// 
/// RING SYSTEM:
/// - Left Rings: 0-10 (dark / chaos / combat path)
/// - Right Rings: 0-10 (light / order / persuasion path)
/// - Total Maximum: Left + Right less than or equal to 10
/// 
/// STATE RESOLUTION - GDD 03_BALANCE_SYSTEM v2.0, Section 4 priority cascade.
/// First match wins; all lower priorities are ignored.
/// 
///   1. Eclipse     min(L,R) >= 2  AND  abs(L-R) less than or equal to 1   (7-stage, see GDD Section 5)
///   2. Sunset      (1L/0R)  OR  (diff=2, dark wins, both have rings, max less than or equal to 4)
///   3. Sunrise     (0L/1R)  OR  (diff=2, light wins, both have rings, max less than or equal to 4)
///   4. Escalation  abs(L-R) >= 6      stage = abs(L-R) - 5
///   5. Path        1 less than or equal to abs(L-R) less than or equal to 5     stage = abs(L-R)
///   6. Neutral     default (0L/0R, 1L/1R)
/// 
/// CORE PRINCIPLE: state is determined by abs(L - R) alone, with Eclipse and
/// Sunset/Sunrise as priority overrides at specific combos. Right rings cancel
/// left rings symmetrically: 6L/4R has diff=2, same state as 5L/3R (both Dark2).
/// 
/// EXAMPLE CALCULATIONS:
/// | Left | Right | Diff | State                                       |
/// |------|-------|------|---------------------------------------------|
/// | 0    | 0     | 0    | Neutral                                     |
/// | 1    | 1     | 0    | Neutral                                     |
/// | 1    | 0     | 1    | Sunset       (priority override)            |
/// | 0    | 1     | 1    | Sunrise      (priority override)            |
/// | 2    | 2     | 0    | Eclipse 15%  (priority override)            |
/// | 3    | 3     | 0    | Eclipse 40%                                 |
/// | 5    | 5     | 0    | Eclipse 100% (Secret Realm portal)          |
/// | 6    | 1     | 5    | Dark5         (right rings cancel)          |
/// | 7    | 2     | 5    | Dark5         (same as 6L/1R)               |
/// | 6    | 4     | 2    | Dark2         (right rings cancel)          |
/// | 6    | 0     | 6    | Dark5 + Stage 1 = DarkStage1                |
/// | 7    | 1     | 6    | DarkStage1    (same as 6L/0R)               |
/// | 10   | 0     | 10   | Dark5 + Stage 5 = DarkStage5                |
/// 
/// ESCALATION RULE: diff >= 6 means at least 6 more of one ring than the other.
/// weatherStage = diff - 5 (diff=6 -> stage 1, diff=10 -> stage 5). Worker
/// controllers compose currentState (Dark5/Light5) with weatherStage to render
/// DarkStage1-5 / LightStage1-5 visuals.
/// 
/// DEBUG HOTKEYS:
/// - Shift + 1-0:       Set left rings (1-10)
/// - Alt + 1-0:         Set right rings (1-10) (Alt = Option on Mac)
/// - Shift + F1:        Eclipse 100% (5L + 5R)
/// - Shift + Backspace: Reset to 0 rings
/// - Shift + F2-F5:     Preset test combinations
/// 
/// NOTE: Q-P keys are reserved for player combat (Q=parry, R=tail, T=transform).
/// Ring debug uses Alt+number to avoid collision with the live control scheme.
/// </summary>
public class WorldStateManager : MonoBehaviour
{
    #region Singleton
    
    public static WorldStateManager Instance { get; private set; }
    
    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }
    
    #endregion
    
    #region Enums
    
    /// <summary>
    /// Atmosphere states based on CLAMPED balance (-5 to +5).
    /// The actual balance can be -10 to +10, but atmosphere caps at ±5.
    /// Further intensity comes from WeatherStage.
    /// </summary>
    public enum AtmosphereState
    {
        Eclipse,  // min(L,R) >= 2 AND |L-R| <= 1 (7-stage gradient, see GDD Section 5)
        Sunset,   // (1L/0R) OR (diff=2, dark wins, both have rings, max <= 4)
        Sunrise,  // (0L/1R) OR (diff=2, light wins, both have rings, max <= 4)
        Dark5,    // diff = 5 (path); escalation handled via WeatherStage
        Dark4,    // diff = 4
        Dark3,    // diff = 3
        Dark2,    // diff = 2
        Dark1,    // diff = 1
        Neutral,  // 0L/0R, 1L/1R
        Light1,   // diff = 1
        Light2,   // diff = 2
        Light3,   // diff = 3
        Light4,   // diff = 4
        Light5    // diff = 5 (path); escalation handled via WeatherStage
    }
    
    #endregion
    
    #region Events
    
    /// <summary>Fires when atmosphere state changes (based on clamped balance)</summary>
    public UnityEvent<AtmosphereState> OnStateChanged = new UnityEvent<AtmosphereState>();
    
    /// <summary>Fires when ring counts change (useful for ring visuals)</summary>
    public UnityEvent<int, int> OnRingsChanged = new UnityEvent<int, int>();
    
    /// <summary>Fires when weather stage changes (escalation beyond ±5)</summary>
    public UnityEvent<int> OnWeatherStageChanged = new UnityEvent<int>();
    
    /// <summary>Fires when eclipse state changes</summary>
    public UnityEvent<bool> OnEclipseChanged = new UnityEvent<bool>();
    
    /// <summary>Fires when eclipse amount changes (0-1, for gradual eclipse visibility)</summary>
    public UnityEvent<float> OnEclipseAmountChanged = new UnityEvent<float>();
    
    #endregion
    
    #region Serialized Fields
    
    [Header("=== RING COUNTS ===")]
    [SerializeField, Range(0, 10)] 
    private int leftRings = 0;
    
    [SerializeField, Range(0, 10)] 
    private int rightRings = 0;
    
    [Header("=== CALCULATED VALUES (Read-Only) ===")]
    [SerializeField] private int balance;           // Right - Left
    [SerializeField] private int clampedBalance;    // Clamped to ±5
    [SerializeField] private int totalRings;        // Left + Right
    [SerializeField] private int weatherStage;      // 0-5 for escalation
    [SerializeField] private AtmosphereState currentState;
    [SerializeField] private bool isEclipse;
    [SerializeField, Range(0, 1)] private float eclipseAmount;  // Gradual eclipse visibility
    
    [Header("=== DEBUG ===")]
    [SerializeField] private bool enableDebugKeys = true;
    [SerializeField] private bool logStateChanges = true;
    
    #endregion
    
    #region Properties
    
    public int LeftRings => leftRings;
    public int RightRings => rightRings;
    public int Balance => balance;
    public int ClampedBalance => clampedBalance;
    public int TotalRings => totalRings;
    public int WeatherStage => weatherStage;
    public AtmosphereState CurrentState => currentState;
    public bool IsEclipse => isEclipse;
    public float EclipseAmount => eclipseAmount;  // 0-1, gradual eclipse visibility
    
    #endregion
    
    #region Unity Lifecycle
    
    void Start()
    {
        RecalculateState();
    }
    
    void Update()
    {
        if (enableDebugKeys)
        {
            HandleDebugInput();
        }
    }
    
    void OnValidate()
    {
        // Ensure rings don't exceed maximum
        EnforceRingLimits();
        RecalculateState();
    }
    
    #endregion
    
    #region Public Methods
    
    /// <summary>
    /// Add rings to a tail. Returns true if successful.
    /// Will fail if it would exceed 10 total rings.
    /// </summary>
    public bool AddRing(bool isLeftTail)
    {
        int newLeft = leftRings + (isLeftTail ? 1 : 0);
        int newRight = rightRings + (isLeftTail ? 0 : 1);
        
        if (newLeft + newRight > 10)
        {
            Debug.LogWarning($"[WorldStateManager] Cannot add ring: would exceed 10 total ({newLeft}L + {newRight}R = {newLeft + newRight})");
            return false;
        }
        
        if (isLeftTail && newLeft > 10) return false;
        if (!isLeftTail && newRight > 10) return false;
        
        leftRings = newLeft;
        rightRings = newRight;
        RecalculateState();
        return true;
    }
    
    /// <summary>
    /// Set ring counts directly. Enforces 10 total maximum.
    /// </summary>
    public void SetRings(int left, int right)
    {
        // Enforce limits
        left = Mathf.Clamp(left, 0, 10);
        right = Mathf.Clamp(right, 0, 10);
        
        // Enforce total maximum
        if (left + right > 10)
        {
            Debug.LogWarning($"[WorldStateManager] Ring total {left + right} exceeds 10. Adjusting...");
            // Keep the balance direction, reduce proportionally
            float ratio = 10f / (left + right);
            left = Mathf.FloorToInt(left * ratio);
            right = 10 - left;
        }
        
        bool changed = (leftRings != left || rightRings != right);
        leftRings = left;
        rightRings = right;
        
        if (changed)
        {
            RecalculateState();
        }
    }
    
    /// <summary>
    /// Reset to neutral (0 rings)
    /// </summary>
    public void Reset()
    {
        SetRings(0, 0);
    }
    
    /// <summary>
    /// Force recalculation and event firing
    /// </summary>
    public void ForceUpdate()
    {
        RecalculateState();
    }
    
    #endregion
    
    #region State Calculation
    
    void EnforceRingLimits()
    {
        leftRings = Mathf.Clamp(leftRings, 0, 10);
        rightRings = Mathf.Clamp(rightRings, 0, 10);
        
        // Reduce right rings if total exceeds 10
        if (leftRings + rightRings > 10)
        {
            rightRings = 10 - leftRings;
        }
    }
    
    void RecalculateState()
    {
        EnforceRingLimits();
        
        // Store previous values
        AtmosphereState prevState = currentState;
        int prevWeatherStage = weatherStage;
        bool prevEclipse = isEclipse;
        float prevEclipseAmount = eclipseAmount;
        
        // Calculate basic values
        balance = rightRings - leftRings;
        totalRings = leftRings + rightRings;
        int diff = Mathf.Abs(leftRings - rightRings);
        int minOneSide = Mathf.Min(leftRings, rightRings);
        int maxOneSide = Mathf.Max(leftRings, rightRings);
        bool darkWinning = leftRings > rightRings;
        bool lightWinning = rightRings > leftRings;
        bool bothHaveRings = leftRings > 0 && rightRings > 0;
        
        // === PRIORITY 1: ECLIPSE (GDD §4) ===
        // Trigger: min(L,R) >= 2 AND diff <= 1
        if (minOneSide >= 2 && diff <= 1)
        {
            currentState = AtmosphereState.Eclipse;
            isEclipse = (leftRings == 5 && rightRings == 5);
            weatherStage = 0;
            clampedBalance = 0;
            
            // Eclipse amount per 7-stage gradient (GDD §5)
            if (leftRings == 5 && rightRings == 5)         eclipseAmount = 1.00f;
            else if (minOneSide == 4 && maxOneSide == 5)   eclipseAmount = 0.85f;
            else if (leftRings == 4 && rightRings == 4)    eclipseAmount = 0.70f;
            else if (minOneSide == 3 && maxOneSide == 4)   eclipseAmount = 0.55f;
            else if (leftRings == 3 && rightRings == 3)    eclipseAmount = 0.40f;
            else if (minOneSide == 2 && maxOneSide == 3)   eclipseAmount = 0.25f;
            else if (leftRings == 2 && rightRings == 2)    eclipseAmount = 0.15f;
            else                                            eclipseAmount = 0f;
        }
        // === PRIORITY 2: SUNSET (GDD §4) ===
        // Trigger: (1L/0R) OR (diff=2, darkWinning, both have rings, max<=4)
        else if ((leftRings == 1 && rightRings == 0) ||
                 (diff == 2 && darkWinning && bothHaveRings && maxOneSide <= 4))
        {
            currentState = AtmosphereState.Sunset;
            weatherStage = 0;
            isEclipse = false;
            eclipseAmount = 0f;
            clampedBalance = balance;
        }
        // === PRIORITY 3: SUNRISE (GDD §4) ===
        // Trigger: (0L/1R) OR (diff=2, lightWinning, both have rings, max<=4)
        else if ((leftRings == 0 && rightRings == 1) ||
                 (diff == 2 && lightWinning && bothHaveRings && maxOneSide <= 4))
        {
            currentState = AtmosphereState.Sunrise;
            weatherStage = 0;
            isEclipse = false;
            eclipseAmount = 0f;
            clampedBalance = balance;
        }
        // === PRIORITY 4: ESCALATION (GDD §4) ===
        // Trigger: diff >= 6, stage = diff - 5
        // State is driven by |L - R| alone. Right rings cancel left symmetrically.
        else if (diff >= 6)
        {
            weatherStage = Mathf.Clamp(diff - 5, 1, 5);
            currentState = darkWinning 
                ? AtmosphereState.Dark5 
                : AtmosphereState.Light5;
            isEclipse = false;
            eclipseAmount = 0f;
            clampedBalance = darkWinning ? -5 : 5;
        }
        // === PRIORITY 5-6: PATH or NEUTRAL ===
        // Diff 1-5 → Dark/Light by diff value. Diff 0 → Neutral.
        else
        {
            weatherStage = 0;
            clampedBalance = Mathf.Clamp(balance, -5, 5);
            currentState = GetAtmosphereFromBalance(clampedBalance);
            isEclipse = false;
            eclipseAmount = 0f;
        }
        
        // Log changes
        if (logStateChanges)
        {
            LogCurrentState();
        }
        
        // Fire events
        OnRingsChanged.Invoke(leftRings, rightRings);
        
        if (currentState != prevState)
        {
            OnStateChanged.Invoke(currentState);
        }
        
        if (weatherStage != prevWeatherStage)
        {
            OnWeatherStageChanged.Invoke(weatherStage);
        }
        
        if (isEclipse != prevEclipse)
        {
            OnEclipseChanged.Invoke(isEclipse);
        }
        
        // Fire eclipse amount change event (for gradual eclipse visibility)
        if (Mathf.Abs(eclipseAmount - prevEclipseAmount) > 0.01f)
        {
            OnEclipseAmountChanged.Invoke(eclipseAmount);
        }
    }
    
    AtmosphereState GetAtmosphereFromBalance(int bal)
    {
        switch (bal)
        {
            case -5: return AtmosphereState.Dark5;
            case -4: return AtmosphereState.Dark4;
            case -3: return AtmosphereState.Dark3;
            case -2: return AtmosphereState.Dark2;
            case -1: return AtmosphereState.Dark1;
            case 0: return AtmosphereState.Neutral;
            case 1: return AtmosphereState.Light1;
            case 2: return AtmosphereState.Light2;
            case 3: return AtmosphereState.Light3;
            case 4: return AtmosphereState.Light4;
            case 5: return AtmosphereState.Light5;
            default:
                return bal < 0 ? AtmosphereState.Dark5 : AtmosphereState.Light5;
        }
    }
    
    void LogCurrentState()
    {
        string stateStr = currentState.ToString();
        string stageStr = weatherStage > 0 ? $" + Stage{weatherStage}" : "";
        string eclipseStr = isEclipse ? " [ECLIPSE]" : "";
        
        Debug.Log($"[WorldStateManager] {leftRings}L + {rightRings}R = Balance {balance} " +
                  $"→ {stateStr}{stageStr}{eclipseStr}");
    }
    
    #endregion
    
    #region Debug Input
    
    void HandleDebugInput()
    {
        bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        bool alt = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
        
        if (!shift && !alt) return;
        
        // Shift block: left rings + eclipse + reset + preset tests
        if (shift)
        {
            // Left rings: Shift + 1-0
            if (Input.GetKeyDown(KeyCode.Alpha1)) SetRings(1, rightRings);
            if (Input.GetKeyDown(KeyCode.Alpha2)) SetRings(2, rightRings);
            if (Input.GetKeyDown(KeyCode.Alpha3)) SetRings(3, rightRings);
            if (Input.GetKeyDown(KeyCode.Alpha4)) SetRings(4, rightRings);
            if (Input.GetKeyDown(KeyCode.Alpha5)) SetRings(5, rightRings);
            if (Input.GetKeyDown(KeyCode.Alpha6)) SetRings(6, rightRings);
            if (Input.GetKeyDown(KeyCode.Alpha7)) SetRings(7, rightRings);
            if (Input.GetKeyDown(KeyCode.Alpha8)) SetRings(8, rightRings);
            if (Input.GetKeyDown(KeyCode.Alpha9)) SetRings(9, rightRings);
            if (Input.GetKeyDown(KeyCode.Alpha0)) SetRings(10, rightRings);
            
            // Eclipse: Shift + F1
            if (Input.GetKeyDown(KeyCode.F1)) SetRings(5, 5);
            
            // Reset: Shift + Backspace
            if (Input.GetKeyDown(KeyCode.Backspace)) Reset();
            
            // Test combinations: Shift + F2-F5
            if (Input.GetKeyDown(KeyCode.F2)) SetRings(1, 9);  // 9R 1L → Light5+Stage5
            if (Input.GetKeyDown(KeyCode.F3)) SetRings(9, 1);  // 9L 1R → Dark5+Stage5
            if (Input.GetKeyDown(KeyCode.F4)) SetRings(3, 7);  // 7R 3L → Light4 (no escalation!)
            if (Input.GetKeyDown(KeyCode.F5)) SetRings(0, 8);  // 8R 0L → Light5+Stage3
        }
        
        // Alt block: right rings (Alt = Option on Mac)
        if (alt)
        {
            // Right rings: Alt + 1-0
            if (Input.GetKeyDown(KeyCode.Alpha1)) SetRings(leftRings, 1);
            if (Input.GetKeyDown(KeyCode.Alpha2)) SetRings(leftRings, 2);
            if (Input.GetKeyDown(KeyCode.Alpha3)) SetRings(leftRings, 3);
            if (Input.GetKeyDown(KeyCode.Alpha4)) SetRings(leftRings, 4);
            if (Input.GetKeyDown(KeyCode.Alpha5)) SetRings(leftRings, 5);
            if (Input.GetKeyDown(KeyCode.Alpha6)) SetRings(leftRings, 6);
            if (Input.GetKeyDown(KeyCode.Alpha7)) SetRings(leftRings, 7);
            if (Input.GetKeyDown(KeyCode.Alpha8)) SetRings(leftRings, 8);
            if (Input.GetKeyDown(KeyCode.Alpha9)) SetRings(leftRings, 9);
            if (Input.GetKeyDown(KeyCode.Alpha0)) SetRings(leftRings, 10);
        }
    }
    
    #endregion
    
    #region Context Menu
    
    [ContextMenu("Log All Ring Combinations")]
    void LogAllCombinations()
    {
        Debug.Log("=== ALL RING COMBINATIONS (per GDD §4 priority cascade) ===");
        Debug.Log("Left | Right | Diff | Stage | EclipseAmt | State");
        Debug.Log("-----|-------|------|-------|------------|------");
        
        for (int left = 0; left <= 10; left++)
        {
            for (int right = 0; right <= 10 - left; right++)
            {
                // Mirror of cascade in RecalculateState — keep in sync with GDD §4
                int diff = Mathf.Abs(left - right);
                int minSide = Mathf.Min(left, right);
                int maxSide = Mathf.Max(left, right);
                bool darkWins = left > right;
                bool lightWins = right > left;
                bool both = left > 0 && right > 0;
                
                AtmosphereState state;
                int stage = 0;
                float eclipseAmt = 0f;
                
                if (minSide >= 2 && diff <= 1)
                {
                    state = AtmosphereState.Eclipse;
                    if (left == 5 && right == 5)              eclipseAmt = 1.00f;
                    else if (minSide == 4 && maxSide == 5)    eclipseAmt = 0.85f;
                    else if (left == 4 && right == 4)         eclipseAmt = 0.70f;
                    else if (minSide == 3 && maxSide == 4)    eclipseAmt = 0.55f;
                    else if (left == 3 && right == 3)         eclipseAmt = 0.40f;
                    else if (minSide == 2 && maxSide == 3)    eclipseAmt = 0.25f;
                    else if (left == 2 && right == 2)         eclipseAmt = 0.15f;
                }
                else if ((left == 1 && right == 0) ||
                         (diff == 2 && darkWins && both && maxSide <= 4))
                {
                    state = AtmosphereState.Sunset;
                }
                else if ((left == 0 && right == 1) ||
                         (diff == 2 && lightWins && both && maxSide <= 4))
                {
                    state = AtmosphereState.Sunrise;
                }
                else if (diff >= 6)
                {
                    stage = Mathf.Clamp(diff - 5, 1, 5);
                    state = darkWins ? AtmosphereState.Dark5 : AtmosphereState.Light5;
                }
                else
                {
                    state = GetAtmosphereFromBalance(Mathf.Clamp(right - left, -5, 5));
                }
                
                string stateName = state.ToString();
                if (stage > 0) stateName += $"+S{stage}";
                if (state == AtmosphereState.Eclipse) stateName += $" ({eclipseAmt:P0})";
                
                Debug.Log($"{left,4} | {right,5} | {diff,4} | {stage,5} | {eclipseAmt,10:F2} | {stateName}");
            }
        }
    }
    
    [ContextMenu("Test: 9R 1L")]
    void Test9R1L() => SetRings(1, 9);
    
    [ContextMenu("Test: 9L 1R")]
    void Test9L1R() => SetRings(9, 1);
    
    [ContextMenu("Test: 7R 3L")]
    void Test7R3L() => SetRings(3, 7);
    
    [ContextMenu("Test: 8R 0L")]
    void Test8R0L() => SetRings(0, 8);
    
    [ContextMenu("Test: Eclipse")]
    void TestEclipse() => SetRings(5, 5);
    
    #endregion
}