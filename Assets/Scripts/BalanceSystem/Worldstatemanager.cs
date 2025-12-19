using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Central manager for YORU's karma ring system.
/// Tracks left (dark/condemn) and right (light/forgive) rings.
/// Fires events when rings change so other systems can react.
/// </summary>
public class WorldStateManager : MonoBehaviour
{
    // ═══════════════════════════════════════════════════════════
    // SINGLETON
    // ═══════════════════════════════════════════════════════════
    
    public static WorldStateManager Instance { get; private set; }
    
    // ═══════════════════════════════════════════════════════════
    // RING STATE
    // ═══════════════════════════════════════════════════════════
    
    [Header("Current Ring State")]
    [SerializeField, Range(0, 5)] private int leftRings = 0;
    [SerializeField, Range(0, 5)] private int rightRings = 0;
    
    public const int MAX_RINGS_PER_TAIL = 5;
    
    // Public read-only access
    public int LeftRings => leftRings;
    public int RightRings => rightRings;
    public int TotalRings => leftRings + rightRings;
    
    /// <summary>
    /// Balance Score: Right - Left
    /// Range: -5 (pure dark) to +5 (pure light)
    /// 0 = balanced (but not necessarily eclipse)
    /// </summary>
    public int BalanceScore => rightRings - leftRings;
    
    /// <summary>
    /// True only when BOTH tails are completely full (5L + 5R)
    /// This is the special "Perfect Balance" / "True Ending" state
    /// </summary>
    public bool IsPerfectBalance => leftRings == MAX_RINGS_PER_TAIL && rightRings == MAX_RINGS_PER_TAIL;
    
    // ═══════════════════════════════════════════════════════════
    // EVENTS
    // ═══════════════════════════════════════════════════════════
    
    [Header("Events")]
    [Tooltip("Fired when rings change. Parameters: (leftRings, rightRings)")]
    public UnityEvent<int, int> OnRingsChanged;
    
    [Tooltip("Fired when perfect balance (5L + 5R) is achieved")]
    public UnityEvent OnPerfectBalanceAchieved;
    
    // ═══════════════════════════════════════════════════════════
    // LIFECYCLE
    // ═══════════════════════════════════════════════════════════
    
    private void Awake()
    {
        // Singleton setup
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[WorldStateManager] Duplicate instance destroyed.");
            Destroy(gameObject);
            return;
        }
        Instance = this;
        
        // Initialize events if null
        OnRingsChanged ??= new UnityEvent<int, int>();
        OnPerfectBalanceAchieved ??= new UnityEvent();
    }
    
    private void Start()
    {
        // Fire initial state so listeners can initialize
        NotifyRingsChanged();
    }
    
    private void Update()
    {
        // Debug controls (only in editor or development builds)
        #if UNITY_EDITOR || DEVELOPMENT_BUILD
        HandleDebugInput();
        #endif
    }
    
    // ═══════════════════════════════════════════════════════════
    // PUBLIC METHODS
    // ═══════════════════════════════════════════════════════════
    
    /// <summary>
    /// Add a ring to the LEFT tail (dark/condemn).
    /// Returns false if tail is already full.
    /// </summary>
    public bool AddLeftRing()
    {
        if (leftRings >= MAX_RINGS_PER_TAIL)
        {
            Debug.LogWarning("[WorldStateManager] Left tail is full (5 rings max).");
            return false;
        }
        
        leftRings++;
        Debug.Log($"[WorldStateManager] Left ring added. Now: {leftRings}L / {rightRings}R (Balance: {BalanceScore})");
        NotifyRingsChanged();
        return true;
    }
    
    /// <summary>
    /// Add a ring to the RIGHT tail (light/forgive).
    /// Returns false if tail is already full.
    /// </summary>
    public bool AddRightRing()
    {
        if (rightRings >= MAX_RINGS_PER_TAIL)
        {
            Debug.LogWarning("[WorldStateManager] Right tail is full (5 rings max).");
            return false;
        }
        
        rightRings++;
        Debug.Log($"[WorldStateManager] Right ring added. Now: {leftRings}L / {rightRings}R (Balance: {BalanceScore})");
        NotifyRingsChanged();
        return true;
    }
    
    /// <summary>
    /// Set rings to specific values. Useful for testing or save/load.
    /// Values are clamped to valid range (0-5).
    /// </summary>
    public void SetRings(int left, int right)
    {
        leftRings = Mathf.Clamp(left, 0, MAX_RINGS_PER_TAIL);
        rightRings = Mathf.Clamp(right, 0, MAX_RINGS_PER_TAIL);
        Debug.Log($"[WorldStateManager] Rings set to: {leftRings}L / {rightRings}R (Balance: {BalanceScore})");
        NotifyRingsChanged();
    }
    
    /// <summary>
    /// Reset all rings to zero (game start state).
    /// </summary>
    public void ResetRings()
    {
        leftRings = 0;
        rightRings = 0;
        Debug.Log("[WorldStateManager] Rings reset to 0L / 0R");
        NotifyRingsChanged();
    }
    
    // ═══════════════════════════════════════════════════════════
    // PRIVATE METHODS
    // ═══════════════════════════════════════════════════════════
    
    private void NotifyRingsChanged()
    {
        OnRingsChanged?.Invoke(leftRings, rightRings);
        
        if (IsPerfectBalance)
        {
            Debug.Log("[WorldStateManager] ★ PERFECT BALANCE ACHIEVED! (5L + 5R) ★");
            OnPerfectBalanceAchieved?.Invoke();
        }
    }
    
    // ═══════════════════════════════════════════════════════════
    // DEBUG CONTROLS
    // ═══════════════════════════════════════════════════════════
    
    private void HandleDebugInput()
    {
        // Require Shift to be held
        if (!Input.GetKey(KeyCode.LeftShift) && !Input.GetKey(KeyCode.RightShift))
            return;
        
        // LEFT tail: Shift + 1-5
        if (Input.GetKeyDown(KeyCode.Alpha1)) SetRings(1, rightRings);
        if (Input.GetKeyDown(KeyCode.Alpha2)) SetRings(2, rightRings);
        if (Input.GetKeyDown(KeyCode.Alpha3)) SetRings(3, rightRings);
        if (Input.GetKeyDown(KeyCode.Alpha4)) SetRings(4, rightRings);
        if (Input.GetKeyDown(KeyCode.Alpha5)) SetRings(5, rightRings);
        
        // RIGHT tail: Shift + 6-0
        if (Input.GetKeyDown(KeyCode.Alpha6)) SetRings(leftRings, 1);
        if (Input.GetKeyDown(KeyCode.Alpha7)) SetRings(leftRings, 2);
        if (Input.GetKeyDown(KeyCode.Alpha8)) SetRings(leftRings, 3);
        if (Input.GetKeyDown(KeyCode.Alpha9)) SetRings(leftRings, 4);
        if (Input.GetKeyDown(KeyCode.Alpha0)) SetRings(leftRings, 5);
        
        // Reset: Shift + R
        if (Input.GetKeyDown(KeyCode.R)) ResetRings();
    }
}