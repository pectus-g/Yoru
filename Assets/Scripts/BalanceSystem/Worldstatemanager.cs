using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Central manager for YORU's karma ring system.
/// 
/// Ring System:
/// - Left tail (dark/condemn): 0-10 rings
/// - Right tail (light/forgive): 0-10 rings
/// - Game ends when TOTAL rings = 10
/// - Perfect Balance (5L + 5R) = Eclipse → Opens new chapter (not an ending!)
/// 
/// Atmosphere Scaling:
/// - 0 rings = Neutral (game start)
/// - 5 rings one side = Sunset/sunrise feel (warm transition)
/// - 10 rings one side = Extreme (very dark eerie OR very bright heavenly)
/// 
/// Performance: Event-driven, no per-frame updates.
/// </summary>
public class WorldStateManager : MonoBehaviour
{
    #region Singleton
    
    public static WorldStateManager Instance { get; private set; }
    
    #endregion
    
    #region Serialized Fields
    
    [Header("Current Ring State")]
    [SerializeField, Range(0, 10)] private int leftRings;
    [SerializeField, Range(0, 10)] private int rightRings;
    
    [Header("Events")]
    [Tooltip("Fired when rings change. Parameters: (leftRings, rightRings)")]
    public UnityEvent<int, int> OnRingsChanged;
    
    [Tooltip("Fired when perfect balance (5L + 5R) is achieved - Opens Eclipse Chapter")]
    public UnityEvent OnPerfectBalanceAchieved;
    
    [Tooltip("Fired when total rings reach 10 - Game ending triggered")]
    public UnityEvent<int, int> OnGameEndReached;
    
    #endregion
    
    #region Constants
    
    public const int MAX_RINGS_PER_TAIL = 10;
    public const int TOTAL_RINGS_FOR_ENDING = 10;
    public const int PERFECT_BALANCE_RINGS = 5; // 5L + 5R
    
    #endregion
    
    #region Properties
    
    public int LeftRings => leftRings;
    public int RightRings => rightRings;
    public int TotalRings => leftRings + rightRings;
    
    /// <summary>
    /// Balance Score: -10 (pure dark) to +10 (pure light)
    /// 0 = balanced (could be 0+0, 1+1, 2+2, etc.)
    /// </summary>
    public int BalanceScore => rightRings - leftRings;
    
    /// <summary>
    /// Perfect Balance = exactly 5 left AND 5 right (triggers Eclipse chapter)
    /// </summary>
    public bool IsPerfectBalance => leftRings == PERFECT_BALANCE_RINGS && rightRings == PERFECT_BALANCE_RINGS;
    
    /// <summary>
    /// Game ends when total rings reach 10
    /// </summary>
    public bool HasReachedEnding => TotalRings >= TOTAL_RINGS_FOR_ENDING;
    
    /// <summary>
    /// Normalized dark intensity: 0 (no dark) to 1 (maximum dark at 10L)
    /// </summary>
    public float DarkIntensity => leftRings / (float)MAX_RINGS_PER_TAIL;
    
    /// <summary>
    /// Normalized light intensity: 0 (no light) to 1 (maximum light at 10R)
    /// </summary>
    public float LightIntensity => rightRings / (float)MAX_RINGS_PER_TAIL;
    
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
        
        OnRingsChanged ??= new UnityEvent<int, int>();
        OnPerfectBalanceAchieved ??= new UnityEvent();
        OnGameEndReached ??= new UnityEvent<int, int>();
    }
    
    private void Start()
    {
        NotifyRingsChanged();
    }
    
    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }
    
    #endregion
    
    #region Public API
    
    /// <summary>
    /// Add a dark ring (condemn choice). Returns false if at max or game ended.
    /// </summary>
    public bool AddLeftRing()
    {
        if (leftRings >= MAX_RINGS_PER_TAIL)
            return false;
        
        // Check if this would exceed total limit (shouldn't happen in normal gameplay)
        if (TotalRings >= TOTAL_RINGS_FOR_ENDING)
            return false;
        
        leftRings++;
        NotifyRingsChanged();
        return true;
    }
    
    /// <summary>
    /// Add a light ring (forgive choice). Returns false if at max or game ended.
    /// </summary>
    public bool AddRightRing()
    {
        if (rightRings >= MAX_RINGS_PER_TAIL)
            return false;
        
        // Check if this would exceed total limit
        if (TotalRings >= TOTAL_RINGS_FOR_ENDING)
            return false;
        
        rightRings++;
        NotifyRingsChanged();
        return true;
    }
    
    /// <summary>
    /// Set rings directly. Values are clamped to valid range.
    /// </summary>
    public void SetRings(int left, int right)
    {
        left = Mathf.Clamp(left, 0, MAX_RINGS_PER_TAIL);
        right = Mathf.Clamp(right, 0, MAX_RINGS_PER_TAIL);
        
        if (left == leftRings && right == rightRings)
            return; // No change
        
        leftRings = left;
        rightRings = right;
        NotifyRingsChanged();
    }
    
    /// <summary>
    /// Reset to game start (0 rings).
    /// </summary>
    public void ResetRings()
    {
        if (leftRings == 0 && rightRings == 0)
            return;
        
        leftRings = 0;
        rightRings = 0;
        NotifyRingsChanged();
    }
    
    #endregion
    
    #region Private Methods
    
    private void NotifyRingsChanged()
    {
        OnRingsChanged?.Invoke(leftRings, rightRings);
        
        // Check for Perfect Balance (Eclipse chapter trigger)
        if (IsPerfectBalance)
        {
            OnPerfectBalanceAchieved?.Invoke();
            Debug.Log("[WorldStateManager] PERFECT BALANCE ACHIEVED! Eclipse chapter unlocked.");
        }
        
        // Check for game ending (10 total rings, but NOT perfect balance)
        if (HasReachedEnding && !IsPerfectBalance)
        {
            OnGameEndReached?.Invoke(leftRings, rightRings);
            Debug.Log($"[WorldStateManager] GAME ENDING REACHED: {leftRings}L / {rightRings}R");
        }
    }
    
    #endregion
    
    #region Debug (Editor Only)
    
#if UNITY_EDITOR
    private void Update()
    {
        if (!Input.GetKey(KeyCode.LeftShift) && !Input.GetKey(KeyCode.RightShift))
            return;
        
        // LEFT tail: Shift + 1-9, 0 for 10
        if (Input.GetKeyDown(KeyCode.Alpha1)) SetRings(1, rightRings);
        else if (Input.GetKeyDown(KeyCode.Alpha2)) SetRings(2, rightRings);
        else if (Input.GetKeyDown(KeyCode.Alpha3)) SetRings(3, rightRings);
        else if (Input.GetKeyDown(KeyCode.Alpha4)) SetRings(4, rightRings);
        else if (Input.GetKeyDown(KeyCode.Alpha5)) SetRings(5, rightRings);
        
        // RIGHT tail: Shift + Q-T (maps to 1-5 right), Y-P (maps to 6-10 right)
        else if (Input.GetKeyDown(KeyCode.Q)) SetRings(leftRings, 1);
        else if (Input.GetKeyDown(KeyCode.W)) SetRings(leftRings, 2);
        else if (Input.GetKeyDown(KeyCode.E)) SetRings(leftRings, 3);
        else if (Input.GetKeyDown(KeyCode.T)) SetRings(leftRings, 4);
        else if (Input.GetKeyDown(KeyCode.Y)) SetRings(leftRings, 5);
        
        // Extended: Shift + 6-9, 0 for left 6-10
        else if (Input.GetKeyDown(KeyCode.Alpha6)) SetRings(6, rightRings);
        else if (Input.GetKeyDown(KeyCode.Alpha7)) SetRings(7, rightRings);
        else if (Input.GetKeyDown(KeyCode.Alpha8)) SetRings(8, rightRings);
        else if (Input.GetKeyDown(KeyCode.Alpha9)) SetRings(9, rightRings);
        else if (Input.GetKeyDown(KeyCode.Alpha0)) SetRings(10, rightRings);
        
        // Extended right: Shift + U-P for right 6-10
        else if (Input.GetKeyDown(KeyCode.U)) SetRings(leftRings, 6);
        else if (Input.GetKeyDown(KeyCode.I)) SetRings(leftRings, 7);
        else if (Input.GetKeyDown(KeyCode.O)) SetRings(leftRings, 8);
        else if (Input.GetKeyDown(KeyCode.P)) SetRings(leftRings, 9);
        else if (Input.GetKeyDown(KeyCode.BackQuote)) SetRings(leftRings, 10); // ` key for 10 right
        
        // Reset: Shift + R
        else if (Input.GetKeyDown(KeyCode.R)) ResetRings();
        
        // Quick presets: Shift + F keys
        else if (Input.GetKeyDown(KeyCode.F1)) SetRings(5, 5);   // Perfect Balance (Eclipse)
        else if (Input.GetKeyDown(KeyCode.F2)) SetRings(10, 0);  // Pure Dark ending
        else if (Input.GetKeyDown(KeyCode.F3)) SetRings(0, 10);  // Pure Light ending
        else if (Input.GetKeyDown(KeyCode.F4)) SetRings(7, 3);   // Dark ending
        else if (Input.GetKeyDown(KeyCode.F5)) SetRings(3, 7);   // Light ending
    }
#endif
    
    #endregion
}