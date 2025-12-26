using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// YORU: World State Manager V3 - COMPLETE RING COMBINATION SYSTEM
/// 
/// This is the CORE component that ALL controllers depend on.
/// It calculates the correct state for EVERY possible ring combination.
/// 
/// RING SYSTEM:
/// - Left Rings: 0-10 (dark/chaos choices)
/// - Right Rings: 0-10 (light/order choices)  
/// - Total Maximum: Left + Right ≤ 10
/// 
/// STATE CALCULATION:
/// - Balance = Right - Left (ranges -10 to +10)
/// - ClampedBalance = clamp(Balance, -5, +5) → determines AtmosphereState
/// - WeatherStage = (|Balance| >= 5 AND Total > 5) ? (Total - 5) : 0
/// 
/// EXAMPLE CALCULATIONS:
/// | Left | Right | Balance | Total | ClampedBal | WeatherStage | Result State |
/// |------|-------|---------|-------|------------|--------------|--------------|
/// | 1    | 9     | +8      | 10    | +5         | 5            | Light5+Stage5 |
/// | 9    | 1     | -8      | 10    | -5         | 5            | Dark5+Stage5 |
/// | 3    | 7     | +4      | 10    | +4         | 0            | Light4 (no escalation!) |
/// | 0    | 8     | +8      | 8     | +5         | 3            | Light5+Stage3 |
/// | 5    | 5     | 0       | 10    | 0          | 0 (special)  | Eclipse |
/// | 0    | 5     | +5      | 5     | +5         | 0            | Light5 (no escalation, total=5) |
/// | 0    | 6     | +6      | 6     | +5         | 1            | Light5+Stage1 |
/// 
/// WHY 7R-3L HAS NO ESCALATION:
/// Balance = +4, which is NOT >= 5, so no escalation even though Total = 10.
/// Escalation ONLY happens when you're STRONGLY committed to one path (|Balance| >= 5).
/// 
/// DEBUG HOTKEYS:
/// - Shift + 1-0: Set left rings (1-10)
/// - Shift + Q,W,E,R,T,Y,U,I,O,P: Set right rings (1-10)
/// - Shift + F1: Eclipse (5L + 5R)
/// - Shift + Backspace: Reset to 0 rings
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
        Eclipse,  // Special: 5L + 5R (balance 0, total 10)
        Dark5,    // Balance -5 or beyond
        Dark4,    // Balance -4
        Dark3,    // Balance -3
        Dark2,    // Balance -2
        Dark1,    // Balance -1
        Neutral,  // Balance 0
        Light1,   // Balance +1
        Light2,   // Balance +2
        Light3,   // Balance +3
        Light4,   // Balance +4
        Light5    // Balance +5 or beyond
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
        
        // Check for Eclipse and Eclipse Process
        // FULL Eclipse: exactly 5L + 5R
        // PARTIAL Eclipse: When rings are close (diff ≤ 1) AND total is high enough
        int diff = Mathf.Abs(leftRings - rightRings);
        
        // Full eclipse at 5L+5R
        isEclipse = (leftRings == 5 && rightRings == 5);
        
        // Calculate eclipse AMOUNT for gradual visibility
        // Show eclipse process when: total >= 5 AND diff <= 1
        if (totalRings >= 5 && diff <= 1)
        {
            // Higher total = more intense eclipse
            // diff=0 = full eclipse (1.0), diff=1 = partial eclipse (0.7)
            float baseAmount = diff == 0 ? 1.0f : 0.7f;
            
            // Scale by total rings (5 rings = 50%, 10 rings = 100%)
            float totalScale = Mathf.Clamp01((totalRings - 4f) / 6f);  // 5→0.17, 10→1.0
            
            eclipseAmount = baseAmount * Mathf.Max(0.5f, totalScale);
            
            if (logStateChanges && eclipseAmount > 0)
            {
                Debug.Log($"[WorldStateManager] Eclipse Process: {eclipseAmount:F2} (diff={diff}, total={totalRings})");
            }
        }
        else
        {
            eclipseAmount = 0f;
        }
        
        // Clamp balance for atmosphere (-5 to +5)
        clampedBalance = Mathf.Clamp(balance, -5, 5);
        
        // Calculate weather stage (escalation beyond ±5)
        // CRITICAL: Only escalate when STRONGLY committed to one path
        // |Balance| >= 5 means you need at least 5 more of one type than the other
        if (Mathf.Abs(balance) >= 5 && totalRings > 5)
        {
            weatherStage = totalRings - 5;  // 1 to 5
            weatherStage = Mathf.Clamp(weatherStage, 0, 5);
        }
        else
        {
            weatherStage = 0;
        }
        
        // Determine atmosphere state
        if (isEclipse)
        {
            currentState = AtmosphereState.Eclipse;
        }
        else
        {
            currentState = GetAtmosphereFromBalance(clampedBalance);
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
        if (!Input.GetKey(KeyCode.LeftShift) && !Input.GetKey(KeyCode.RightShift))
            return;
        
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
        
        // Right rings: Shift + Q,W,E,R,T,Y,U,I,O,P
        if (Input.GetKeyDown(KeyCode.Q)) SetRings(leftRings, 1);
        if (Input.GetKeyDown(KeyCode.W)) SetRings(leftRings, 2);
        if (Input.GetKeyDown(KeyCode.E)) SetRings(leftRings, 3);
        if (Input.GetKeyDown(KeyCode.R)) SetRings(leftRings, 4);
        if (Input.GetKeyDown(KeyCode.T)) SetRings(leftRings, 5);
        if (Input.GetKeyDown(KeyCode.Y)) SetRings(leftRings, 6);
        if (Input.GetKeyDown(KeyCode.U)) SetRings(leftRings, 7);
        if (Input.GetKeyDown(KeyCode.I)) SetRings(leftRings, 8);
        if (Input.GetKeyDown(KeyCode.O)) SetRings(leftRings, 9);
        if (Input.GetKeyDown(KeyCode.P)) SetRings(leftRings, 10);
        
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
    
    #endregion
    
    #region Context Menu
    
    [ContextMenu("Log All Ring Combinations")]
    void LogAllCombinations()
    {
        Debug.Log("=== ALL RING COMBINATIONS WITH ESCALATION ===");
        Debug.Log("Left | Right | Balance | Total | Clamped | Stage | State");
        Debug.Log("-----|-------|---------|-------|---------|-------|------");
        
        for (int left = 0; left <= 10; left++)
        {
            for (int right = 0; right <= 10 - left; right++)
            {
                int bal = right - left;
                int total = left + right;
                int clamped = Mathf.Clamp(bal, -5, 5);
                
                int stage = 0;
                if (Mathf.Abs(bal) >= 5 && total > 5)
                {
                    stage = total - 5;
                }
                
                bool eclipse = (left == 5 && right == 5);
                string state = eclipse ? "Eclipse" : GetAtmosphereFromBalance(clamped).ToString();
                if (stage > 0) state += $"+S{stage}";
                
                if (stage > 0 || eclipse)
                {
                    Debug.Log($"{left,4} | {right,5} | {bal,7} | {total,5} | {clamped,7} | {stage,5} | {state}");
                }
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