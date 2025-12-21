using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// YORU: World State Manager
/// Tracks karma rings on Yoru's tails.
/// 
/// IMPORTANT: Maximum 10 TOTAL rings (left + right combined)
/// Example: If you have 7 left rings, you can only have 3 right rings max.
/// 
/// Debug Controls (Shift + Key):
/// - Shift + 1-0: Add left rings (1-10)
/// - Shift + Q,W,E,R,T,Y,U,I,O,P: Add right rings (1-10)
/// - Shift + F1: Set Eclipse state (5L + 5R)
/// - Shift + R: Reset to 0 rings
/// </summary>
public class WorldStateManager : MonoBehaviour
{
    public static WorldStateManager Instance { get; private set; }
    
    [Header("=== RING STATE ===")]
    [SerializeField, Range(0, 10)] private int leftRings = 0;
    [SerializeField, Range(0, 10)] private int rightRings = 0;
    
    [Header("=== LIMITS ===")]
    [Tooltip("Maximum TOTAL rings (left + right combined)")]
    [SerializeField] private int maxTotalRings = 10;
    
    [Header("=== EVENTS ===")]
    public UnityEvent<int, int> OnRingsChanged;
    public UnityEvent OnEclipseAchieved;
    public UnityEvent<int, int> OnGameEndReached;  // Fires when total rings = 10
    
    [Header("=== DEBUG ===")]
    [SerializeField] private bool enableDebugKeys = true;
    [SerializeField] private bool logChanges = true;
    
    // Properties
    public int LeftRings => leftRings;
    public int RightRings => rightRings;
    public int TotalRings => leftRings + rightRings;
    public int RemainingSlots => maxTotalRings - TotalRings;
    public bool IsEclipse => leftRings == 5 && rightRings == 5;
    public bool CanAddRing => TotalRings < maxTotalRings;
    
    // Track if eclipse was already triggered this session
    private bool eclipseTriggered = false;
    
    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }
        
        if (OnRingsChanged == null)
            OnRingsChanged = new UnityEvent<int, int>();
        if (OnEclipseAchieved == null)
            OnEclipseAchieved = new UnityEvent();
        if (OnGameEndReached == null)
            OnGameEndReached = new UnityEvent<int, int>();
    }
    
    void Start()
    {
        // Validate initial state
        ClampRings();
        NotifyRingsChanged();
        
        if (logChanges)
        {
            Debug.Log($"[WorldStateManager] Initialized: {leftRings}L/{rightRings}R (Max {maxTotalRings} total)");
        }
    }
    
    void Update()
    {
        if (enableDebugKeys)
        {
            HandleDebugInput();
        }
    }
    
    #region PUBLIC API
    
    /// <summary>
    /// Add a left (dark) ring. Returns false if at max capacity.
    /// </summary>
    public bool AddLeftRing()
    {
        if (!CanAddRing)
        {
            if (logChanges) Debug.LogWarning("[WorldStateManager] Cannot add ring - at max capacity!");
            return false;
        }
        
        leftRings++;
        ClampRings();
        NotifyRingsChanged();
        return true;
    }
    
    /// <summary>
    /// Add a right (light) ring. Returns false if at max capacity.
    /// </summary>
    public bool AddRightRing()
    {
        if (!CanAddRing)
        {
            if (logChanges) Debug.LogWarning("[WorldStateManager] Cannot add ring - at max capacity!");
            return false;
        }
        
        rightRings++;
        ClampRings();
        NotifyRingsChanged();
        return true;
    }
    
    /// <summary>
    /// Set rings directly. Will clamp to max total.
    /// </summary>
    public void SetRings(int left, int right)
    {
        leftRings = left;
        rightRings = right;
        ClampRings();
        NotifyRingsChanged();
    }
    
    /// <summary>
    /// Reset all rings to zero.
    /// </summary>
    public void ResetRings()
    {
        leftRings = 0;
        rightRings = 0;
        eclipseTriggered = false;
        NotifyRingsChanged();
        
        if (logChanges) Debug.Log("[WorldStateManager] Rings reset to 0/0");
    }
    
    /// <summary>
    /// Set to eclipse state (5L + 5R).
    /// </summary>
    public void SetEclipseState()
    {
        leftRings = 5;
        rightRings = 5;
        NotifyRingsChanged();
        
        if (logChanges) Debug.Log("[WorldStateManager] Eclipse state set (5L + 5R)");
    }
    
    #endregion
    
    #region INTERNAL
    
    /// <summary>
    /// Ensure rings don't exceed limits.
    /// If total exceeds max, proportionally reduce both.
    /// </summary>
    void ClampRings()
    {
        // Clamp individual values
        leftRings = Mathf.Clamp(leftRings, 0, maxTotalRings);
        rightRings = Mathf.Clamp(rightRings, 0, maxTotalRings);
        
        // Clamp total
        int total = leftRings + rightRings;
        if (total > maxTotalRings)
        {
            // Reduce the most recently added (or proportionally)
            // For simplicity, cap each to available space
            float ratio = (float)maxTotalRings / total;
            leftRings = Mathf.FloorToInt(leftRings * ratio);
            rightRings = maxTotalRings - leftRings;
            
            if (logChanges)
            {
                Debug.LogWarning($"[WorldStateManager] Rings clamped to max {maxTotalRings}: {leftRings}L/{rightRings}R");
            }
        }
    }
    
    void NotifyRingsChanged()
    {
        if (logChanges)
        {
            string status = $"[WorldStateManager] Rings: {leftRings}L/{rightRings}R (Total: {TotalRings}/{maxTotalRings})";
            if (IsEclipse) status += " - ECLIPSE!";
            Debug.Log(status);
        }
        
        OnRingsChanged?.Invoke(leftRings, rightRings);
        
        // Check for eclipse
        if (IsEclipse && !eclipseTriggered)
        {
            eclipseTriggered = true;
            OnEclipseAchieved?.Invoke();
            Debug.Log("[WorldStateManager] PERFECT BALANCE ACHIEVED! Eclipse chapter unlocked.");
        }
        
        // Check for game end (10 total rings)
        if (TotalRings >= maxTotalRings)
        {
            OnGameEndReached?.Invoke(leftRings, rightRings);
            Debug.Log($"[WorldStateManager] GAME END REACHED! Final state: {leftRings}L/{rightRings}R");
        }
    }
    
    #endregion
    
    #region DEBUG INPUT
    
    void HandleDebugInput()
    {
        bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        if (!shift) return;
        
        // Left rings: Shift + 1-0 (SETS left rings, KEEPS right rings)
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
        
        // Right rings: Shift + Q,W,E,R,T,Y,U,I,O,P (SETS right rings, KEEPS left rings)
        if (Input.GetKeyDown(KeyCode.Q)) SetRings(leftRings, 1);
        if (Input.GetKeyDown(KeyCode.W)) SetRings(leftRings, 2);
        if (Input.GetKeyDown(KeyCode.E)) SetRings(leftRings, 3);
        if (Input.GetKeyDown(KeyCode.R) && !Input.GetKey(KeyCode.LeftControl)) SetRings(leftRings, 4);
        if (Input.GetKeyDown(KeyCode.T)) SetRings(leftRings, 5);
        if (Input.GetKeyDown(KeyCode.Y)) SetRings(leftRings, 6);
        if (Input.GetKeyDown(KeyCode.U)) SetRings(leftRings, 7);
        if (Input.GetKeyDown(KeyCode.I)) SetRings(leftRings, 8);
        if (Input.GetKeyDown(KeyCode.O)) SetRings(leftRings, 9);
        if (Input.GetKeyDown(KeyCode.P)) SetRings(leftRings, 10);
        
        // Mixed states for testing
        if (Input.GetKeyDown(KeyCode.F1)) SetEclipseState(); // 5+5 Eclipse
        if (Input.GetKeyDown(KeyCode.F2)) SetRings(3, 2);    // 3L+2R = 5 total
        if (Input.GetKeyDown(KeyCode.F3)) SetRings(7, 3);    // 7L+3R = 10 total (max)
        if (Input.GetKeyDown(KeyCode.F4)) SetRings(4, 4);    // 4L+4R = 8 total (near eclipse)
        
        // Reset
        if (Input.GetKeyDown(KeyCode.Backspace)) ResetRings();
    }
    
    #endregion
    
    #region CONTEXT MENU
    
    [ContextMenu("Set Eclipse (5+5)")]
    public void ContextSetEclipse() => SetEclipseState();
    
    [ContextMenu("Reset Rings")]
    public void ContextReset() => ResetRings();
    
    [ContextMenu("Test: Max Dark (10L)")]
    public void ContextMaxDark() => SetRings(10, 0);
    
    [ContextMenu("Test: Max Light (10R)")]
    public void ContextMaxLight() => SetRings(0, 10);
    
    [ContextMenu("Test: Mixed (7L + 3R)")]
    public void ContextMixed() => SetRings(7, 3);
    
    [ContextMenu("Print Status")]
    public void PrintStatus()
    {
        Debug.Log("=== WORLD STATE ===");
        Debug.Log($"Left Rings: {leftRings}");
        Debug.Log($"Right Rings: {rightRings}");
        Debug.Log($"Total: {TotalRings}/{maxTotalRings}");
        Debug.Log($"Remaining Slots: {RemainingSlots}");
        Debug.Log($"Is Eclipse: {IsEclipse}");
        Debug.Log($"Can Add Ring: {CanAddRing}");
        Debug.Log("===================");
    }
    
    #endregion
}