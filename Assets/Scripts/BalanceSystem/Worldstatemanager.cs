using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// YORU: World State Manager - V2 (Balance System)
/// Tracks karma rings on Yoru's tails with balance calculation helpers.
/// 
/// IMPORTANT: Maximum 10 TOTAL rings (left + right combined)
/// Example: If you have 7 left rings, you can only have 3 right rings max.
/// 
/// NEW IN V2:
/// - Balance calculation (Right - Left, ranges -10 to +10)
/// - ClampedBalance for atmosphere (capped at ±5)
/// - WeatherStage for escalation beyond cap
/// - AtmosphereState enum for easy state identification
/// 
/// Debug Controls (Shift + Key):
/// - Shift + 1-0: Set left rings (1-10)
/// - Shift + Q,W,E,R,T,Y,U,I,O,P: Set right rings (1-10)
/// - Shift + F1: Set Eclipse state (5L + 5R)
/// - Shift + Backspace: Reset to 0 rings
/// </summary>
public class WorldStateManager : MonoBehaviour
{
    public static WorldStateManager Instance { get; private set; }
    
    #region Enums
    
    /// <summary>
    /// Atmosphere states based on balance.
    /// Used by controllers to determine visual settings.
    /// </summary>
    public enum AtmosphereState
    {
        Eclipse,        // Special: 5L + 5R exactly
        Dark5,          // Balance -5 or less (midnight, eerie)
        Dark4,          // Balance -4
        Dark3,          // Balance -3
        Dark2,          // Balance -2
        Dark1,          // Balance -1
        Neutral,        // Balance 0
        Light1,         // Balance +1
        Light2,         // Balance +2
        Light3,         // Balance +3
        Light4,         // Balance +4
        Light5          // Balance +5 or more (heavenly)
    }
    
    #endregion
    
    #region Serialized Fields
    
    [Header("=== RING STATE ===")]
    [SerializeField, Range(0, 10)]
    private int leftRings = 0;
    
    [SerializeField, Range(0, 10)]
    private int rightRings = 0;
    
    [Header("=== CALCULATED VALUES (Read-Only) ===")]
    [SerializeField, Tooltip("Right - Left (ranges -10 to +10)")]
    private int currentBalance = 0;
    
    [SerializeField, Tooltip("Balance capped at ±5 for atmosphere")]
    private int clampedBalance = 0;
    
    [SerializeField, Tooltip("Total rings on both tails")]
    private int totalRings = 0;
    
    [SerializeField, Tooltip("Weather escalation stage (0-5)")]
    private int weatherStage = 0;
    
    [SerializeField]
    private AtmosphereState currentState = AtmosphereState.Neutral;
    
    [Header("=== EVENTS ===")]
    public UnityEvent<int, int> OnRingsChanged;
    public UnityEvent<AtmosphereState> OnStateChanged;
    public UnityEvent<int> OnWeatherStageChanged;
    public UnityEvent OnEclipseTriggered;
    public UnityEvent<int, int> OnGameEndReached;  // Fired when total rings = 10 (for endings)
    
    #endregion
    
    #region Properties
    
    /// <summary>Left tail rings (dark/chaos path)</summary>
    public int LeftRings => leftRings;
    
    /// <summary>Right tail rings (light/order path)</summary>
    public int RightRings => rightRings;
    
    /// <summary>Total rings on both tails (max 10)</summary>
    public int TotalRings => totalRings;
    
    /// <summary>
    /// Balance score: Right - Left
    /// Negative = Dark leaning, Positive = Light leaning
    /// Range: -10 to +10
    /// </summary>
    public int Balance => currentBalance;
    
    /// <summary>
    /// Balance capped at ±5 for atmosphere layer.
    /// Beyond ±5, weather escalation kicks in instead.
    /// </summary>
    public int ClampedBalance => clampedBalance;
    
    /// <summary>
    /// Weather escalation stage (0-5).
    /// Only active when |Balance| >= 5 AND TotalRings > 5.
    /// </summary>
    public int WeatherStage => weatherStage;
    
    /// <summary>Current atmosphere state</summary>
    public AtmosphereState CurrentState => currentState;
    
    /// <summary>True if in Eclipse state (exactly 5L + 5R)</summary>
    public bool IsEclipse => leftRings == 5 && rightRings == 5;
    
    /// <summary>True if in any Dark state (balance negative)</summary>
    public bool IsDarkPath => currentBalance < 0;
    
    /// <summary>True if in any Light state (balance positive)</summary>
    public bool IsLightPath => currentBalance > 0;
    
    /// <summary>Normalized balance for lerping (-1 to +1)</summary>
    public float NormalizedBalance => currentBalance / 10f;
    
    /// <summary>Normalized clamped balance for atmosphere lerping (-1 to +1)</summary>
    public float NormalizedClampedBalance => clampedBalance / 5f;
    
    #endregion
    
    #region Unity Lifecycle
    
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        
        // Initialize events if null
        OnRingsChanged ??= new UnityEvent<int, int>();
        OnStateChanged ??= new UnityEvent<AtmosphereState>();
        OnWeatherStageChanged ??= new UnityEvent<int>();
        OnEclipseTriggered ??= new UnityEvent();
        OnGameEndReached ??= new UnityEvent<int, int>();
    }
    
    private void Start()
    {
        // Calculate initial state
        RecalculateState();
    }
    
    private void Update()
    {
        HandleDebugInput();
    }
    
    #endregion
    
    #region Public Methods
    
    /// <summary>
    /// Set left rings directly (enforces max 10 total).
    /// </summary>
    public void SetLeftRings(int count)
    {
        int maxAllowed = 10 - rightRings;
        leftRings = Mathf.Clamp(count, 0, maxAllowed);
        RecalculateState();
    }
    
    /// <summary>
    /// Set right rings directly (enforces max 10 total).
    /// </summary>
    public void SetRightRings(int count)
    {
        int maxAllowed = 10 - leftRings;
        rightRings = Mathf.Clamp(count, 0, maxAllowed);
        RecalculateState();
    }
    
    /// <summary>
    /// Set both ring counts at once (enforces max 10 total).
    /// </summary>
    public void SetRings(int left, int right)
    {
        // Clamp total to 10
        int total = left + right;
        if (total > 10)
        {
            float ratio = 10f / total;
            left = Mathf.RoundToInt(left * ratio);
            right = 10 - left;
        }
        
        leftRings = Mathf.Clamp(left, 0, 10);
        rightRings = Mathf.Clamp(right, 0, 10 - leftRings);
        RecalculateState();
    }
    
    /// <summary>
    /// Add one left ring (dark choice made).
    /// </summary>
    public void AddLeftRing()
    {
        if (totalRings < 10)
        {
            leftRings++;
            RecalculateState();
        }
    }
    
    /// <summary>
    /// Add one right ring (light choice made).
    /// </summary>
    public void AddRightRing()
    {
        if (totalRings < 10)
        {
            rightRings++;
            RecalculateState();
        }
    }
    
    /// <summary>
    /// Reset to neutral state (0 rings).
    /// </summary>
    public void Reset()
    {
        leftRings = 0;
        rightRings = 0;
        RecalculateState();
    }
    
    /// <summary>
    /// Set Eclipse state (5L + 5R).
    /// </summary>
    public void SetEclipse()
    {
        leftRings = 5;
        rightRings = 5;
        RecalculateState();
    }
    
    /// <summary>
    /// Get atmosphere state for a hypothetical balance value.
    /// Useful for previewing without changing actual state.
    /// </summary>
    public static AtmosphereState GetStateForBalance(int balance, bool isEclipse)
    {
        if (isEclipse) return AtmosphereState.Eclipse;
        
        // Clamp to atmosphere range
        int clamped = Mathf.Clamp(balance, -5, 5);
        
        return clamped switch
        {
            -5 => AtmosphereState.Dark5,
            -4 => AtmosphereState.Dark4,
            -3 => AtmosphereState.Dark3,
            -2 => AtmosphereState.Dark2,
            -1 => AtmosphereState.Dark1,
            0 => AtmosphereState.Neutral,
            1 => AtmosphereState.Light1,
            2 => AtmosphereState.Light2,
            3 => AtmosphereState.Light3,
            4 => AtmosphereState.Light4,
            5 => AtmosphereState.Light5,
            _ => AtmosphereState.Neutral
        };
    }
    
    /// <summary>
    /// Get the COZY time of day (0-1) for current atmosphere state.
    /// 0 = Midnight, 0.5 = Noon, 1 = Midnight
    /// </summary>
    public float GetTimeOfDay()
    {
        if (IsEclipse) return 0.625f; // 3 PM for eclipse
        
        // Map atmosphere states to time of day
        return currentState switch
        {
            AtmosphereState.Dark5 => 0f,        // Midnight (0:00)
            AtmosphereState.Dark4 => 0.04f,     // 1 AM
            AtmosphereState.Dark3 => 0.125f,    // 3 AM
            AtmosphereState.Dark2 => 0.25f,     // 6 AM (dawn)
            AtmosphereState.Dark1 => 0.3125f,   // 7:30 AM
            AtmosphereState.Neutral => 0.5f,    // Noon (12:00)
            AtmosphereState.Light1 => 0.5625f,  // 1:30 PM
            AtmosphereState.Light2 => 0.625f,   // 3 PM
            AtmosphereState.Light3 => 0.6875f,  // 4:30 PM
            AtmosphereState.Light4 => 0.75f,    // 6 PM (sunset)
            AtmosphereState.Light5 => 0.8125f,  // 7:30 PM (golden hour)
            _ => 0.5f
        };
    }
    
    #endregion
    
    #region Private Methods
    
    private void RecalculateState()
    {
        // Calculate totals
        totalRings = leftRings + rightRings;
        currentBalance = rightRings - leftRings;
        clampedBalance = Mathf.Clamp(currentBalance, -5, 5);
        
        // Calculate weather stage (only when at atmosphere cap)
        int oldWeatherStage = weatherStage;
        if (Mathf.Abs(currentBalance) >= 5 && totalRings > 5)
        {
            weatherStage = Mathf.Min(totalRings - 5, 5);
        }
        else
        {
            weatherStage = 0;
        }
        
        // Determine atmosphere state
        AtmosphereState oldState = currentState;
        currentState = GetStateForBalance(currentBalance, IsEclipse);
        
        // Fire events
        OnRingsChanged?.Invoke(leftRings, rightRings);
        
        if (currentState != oldState)
        {
            OnStateChanged?.Invoke(currentState);
            
            if (currentState == AtmosphereState.Eclipse)
            {
                OnEclipseTriggered?.Invoke();
            }
        }
        
        if (weatherStage != oldWeatherStage)
        {
            OnWeatherStageChanged?.Invoke(weatherStage);
        }
        
        // Fire game end event when 10 total rings reached (for endings)
        if (totalRings == 10 && !IsEclipse)
        {
            OnGameEndReached?.Invoke(leftRings, rightRings);
        }
        
        Debug.Log($"[WorldStateManager] L:{leftRings} R:{rightRings} | Balance:{currentBalance} | State:{currentState} | Weather Stage:{weatherStage}");
    }
    
    private void HandleDebugInput()
    {
        if (!Input.GetKey(KeyCode.LeftShift) && !Input.GetKey(KeyCode.RightShift))
            return;
        
        // Left rings: Shift + 1-0 (number row)
        if (Input.GetKeyDown(KeyCode.Alpha1)) SetLeftRings(1);
        else if (Input.GetKeyDown(KeyCode.Alpha2)) SetLeftRings(2);
        else if (Input.GetKeyDown(KeyCode.Alpha3)) SetLeftRings(3);
        else if (Input.GetKeyDown(KeyCode.Alpha4)) SetLeftRings(4);
        else if (Input.GetKeyDown(KeyCode.Alpha5)) SetLeftRings(5);
        else if (Input.GetKeyDown(KeyCode.Alpha6)) SetLeftRings(6);
        else if (Input.GetKeyDown(KeyCode.Alpha7)) SetLeftRings(7);
        else if (Input.GetKeyDown(KeyCode.Alpha8)) SetLeftRings(8);
        else if (Input.GetKeyDown(KeyCode.Alpha9)) SetLeftRings(9);
        else if (Input.GetKeyDown(KeyCode.Alpha0)) SetLeftRings(10);
        
        // Right rings: Shift + Q,W,E,R,T,Y,U,I,O,P
        else if (Input.GetKeyDown(KeyCode.Q)) SetRightRings(1);
        else if (Input.GetKeyDown(KeyCode.W)) SetRightRings(2);
        else if (Input.GetKeyDown(KeyCode.E)) SetRightRings(3);
        else if (Input.GetKeyDown(KeyCode.R)) SetRightRings(4);
        else if (Input.GetKeyDown(KeyCode.T)) SetRightRings(5);
        else if (Input.GetKeyDown(KeyCode.Y)) SetRightRings(6);
        else if (Input.GetKeyDown(KeyCode.U)) SetRightRings(7);
        else if (Input.GetKeyDown(KeyCode.I)) SetRightRings(8);
        else if (Input.GetKeyDown(KeyCode.O)) SetRightRings(9);
        else if (Input.GetKeyDown(KeyCode.P)) SetRightRings(10);
        
        // Special commands
        else if (Input.GetKeyDown(KeyCode.F1)) SetEclipse();
        else if (Input.GetKeyDown(KeyCode.Backspace)) Reset();
    }
    
    #endregion
    
    #region Editor Validation
    
    private void OnValidate()
    {
        // Enforce max 10 total in editor
        if (leftRings + rightRings > 10)
        {
            rightRings = 10 - leftRings;
        }
        
        // Recalculate display values
        totalRings = leftRings + rightRings;
        currentBalance = rightRings - leftRings;
        clampedBalance = Mathf.Clamp(currentBalance, -5, 5);
        
        if (Mathf.Abs(currentBalance) >= 5 && totalRings > 5)
        {
            weatherStage = Mathf.Min(totalRings - 5, 5);
        }
        else
        {
            weatherStage = 0;
        }
        
        currentState = GetStateForBalance(currentBalance, leftRings == 5 && rightRings == 5);
    }
    
    #endregion
}