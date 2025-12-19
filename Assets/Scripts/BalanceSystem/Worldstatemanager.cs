using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Central manager for YORU's karma ring system.
/// Tracks left (dark/condemn) and right (light/forgive) rings.
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
    [SerializeField, Range(0, 5)] private int leftRings;
    [SerializeField, Range(0, 5)] private int rightRings;
    
    [Header("Events")]
    [Tooltip("Fired when rings change. Parameters: (leftRings, rightRings)")]
    public UnityEvent<int, int> OnRingsChanged;
    
    [Tooltip("Fired when perfect balance (5L + 5R) is achieved")]
    public UnityEvent OnPerfectBalanceAchieved;
    
    #endregion
    
    #region Constants
    
    public const int MAX_RINGS = 5;
    
    #endregion
    
    #region Properties
    
    public int LeftRings => leftRings;
    public int RightRings => rightRings;
    public int TotalRings => leftRings + rightRings;
    public int BalanceScore => rightRings - leftRings;
    public bool IsPerfectBalance => leftRings == MAX_RINGS && rightRings == MAX_RINGS;
    
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
    
    public bool AddLeftRing()
    {
        if (leftRings >= MAX_RINGS)
            return false;
        
        leftRings++;
        NotifyRingsChanged();
        return true;
    }
    
    public bool AddRightRing()
    {
        if (rightRings >= MAX_RINGS)
            return false;
        
        rightRings++;
        NotifyRingsChanged();
        return true;
    }
    
    public void SetRings(int left, int right)
    {
        left = Mathf.Clamp(left, 0, MAX_RINGS);
        right = Mathf.Clamp(right, 0, MAX_RINGS);
        
        if (left == leftRings && right == rightRings)
            return; // No change, skip event
        
        leftRings = left;
        rightRings = right;
        NotifyRingsChanged();
    }
    
    public void ResetRings()
    {
        if (leftRings == 0 && rightRings == 0)
            return; // Already reset
        
        leftRings = 0;
        rightRings = 0;
        NotifyRingsChanged();
    }
    
    #endregion
    
    #region Private Methods
    
    private void NotifyRingsChanged()
    {
        OnRingsChanged?.Invoke(leftRings, rightRings);
        
        if (IsPerfectBalance)
            OnPerfectBalanceAchieved?.Invoke();
    }
    
    #endregion
    
    #region Debug (Editor Only)
    
#if UNITY_EDITOR
    private void Update()
    {
        if (!Input.GetKey(KeyCode.LeftShift) && !Input.GetKey(KeyCode.RightShift))
            return;
        
        // LEFT tail: Shift + 1-5
        if (Input.GetKeyDown(KeyCode.Alpha1)) SetRings(1, rightRings);
        else if (Input.GetKeyDown(KeyCode.Alpha2)) SetRings(2, rightRings);
        else if (Input.GetKeyDown(KeyCode.Alpha3)) SetRings(3, rightRings);
        else if (Input.GetKeyDown(KeyCode.Alpha4)) SetRings(4, rightRings);
        else if (Input.GetKeyDown(KeyCode.Alpha5)) SetRings(5, rightRings);
        // RIGHT tail: Shift + 6-0
        else if (Input.GetKeyDown(KeyCode.Alpha6)) SetRings(leftRings, 1);
        else if (Input.GetKeyDown(KeyCode.Alpha7)) SetRings(leftRings, 2);
        else if (Input.GetKeyDown(KeyCode.Alpha8)) SetRings(leftRings, 3);
        else if (Input.GetKeyDown(KeyCode.Alpha9)) SetRings(leftRings, 4);
        else if (Input.GetKeyDown(KeyCode.Alpha0)) SetRings(leftRings, 5);
        // Reset: Shift + R
        else if (Input.GetKeyDown(KeyCode.R)) ResetRings();
    }
#endif
    
    #endregion
}