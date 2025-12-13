using UnityEngine;
using UnityEngine.Events;

namespace Yoru.BalanceSystem
{
    /// <summary>
    /// WorldStateManager - The Central Brain of YORU's Balance System
    /// 
    /// CORRECT LOGIC:
    /// - Game STARTS with 0 rings = Normal day, sun and moon far apart
    /// - Each boss defeated = 1 ring (LEFT for combat, RIGHT for persuasion)
    /// - Eclipse ONLY happens when ALL 10 rings collected AND balanced (5L + 5R)
    /// 
    /// POSITION LOGIC:
    /// - Sun moves toward center as RIGHT rings increase
    /// - Moon moves toward center as LEFT rings increase
    /// - They OVERLAP only when both have 5 rings = ECLIPSE!
    /// 
    /// BRIGHTNESS LOGIC:
    /// - More RIGHT rings = brighter sun, dimmer moon
    /// - More LEFT rings = brighter moon, dimmer sun
    /// - At Eclipse = BOTH fully bright!
    /// </summary>
    public class WorldStateManager : MonoBehaviour
    {
        #region Singleton
        public static WorldStateManager Instance { get; private set; }
        
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            
            RecalculateState();
        }
        #endregion

        #region Enums
        public enum WorldState
        {
            NormalDay,      // Starting state, 0 rings
            LeaningLight,   // More right than left rings
            LeaningDark,    // More left than right rings
            Eclipse,        // 5L + 5R = perfect balance with all rings
            PureLight,      // 10R + 0L = all light choices
            PureDark        // 10L + 0R = all dark choices
        }

        public enum Ending
        {
            NotYetDetermined,  // Less than 10 rings
            TheDevourer,       // 10L + 0R (or 9L + 1R, 8L + 2R)
            VengefulSpirit,    // 7L + 3R, 6L + 4R
            TrueBalance,       // 5L + 5R - THE ECLIPSE ENDING
            HarmonyPath,       // 4L + 6R, 3L + 7R
            Enlightenment      // 2L + 8R, 1L + 9R, 0L + 10R
        }
        #endregion

        #region Inspector Fields
        [Header("=== RING COUNTS ===")]
        [SerializeField, Range(0, 10)] 
        private int leftRings = 0;
        
        [SerializeField, Range(0, 10)] 
        private int rightRings = 0;

        [Header("=== CALCULATED VALUES (Read Only) ===")]
        [SerializeField] 
        private int totalRings = 0;
        
        [SerializeField] 
        private int balanceScore = 0;
        
        [SerializeField] 
        private WorldState currentWorldState = WorldState.NormalDay;
        
        [SerializeField] 
        private Ending currentEnding = Ending.NotYetDetermined;

        [Header("=== ECLIPSE STATUS ===")]
        [SerializeField]
        private bool isEclipseActive = false;
        
        [SerializeField, Range(0f, 1f)]
        private float eclipseProgress = 0f;

        [Header("=== EVENTS ===")]
        public UnityEvent<int, int> OnRingsChanged;           // leftRings, rightRings
        public UnityEvent<int> OnBalanceScoreChanged;         // balanceScore
        public UnityEvent<WorldState> OnWorldStateChanged;    // new state
        public UnityEvent OnEclipseAchieved;                  // Special event for eclipse!

        [Header("=== DEBUG ===")]
        [SerializeField] 
        private bool enableDebugLogs = true;
        #endregion

        #region Properties
        public int LeftRings => leftRings;
        public int RightRings => rightRings;
        public int TotalRings => totalRings;
        public int BalanceScore => balanceScore;
        public WorldState CurrentWorldState => currentWorldState;
        public Ending CurrentEnding => currentEnding;
        public bool IsEclipseActive => isEclipseActive;
        public float EclipseProgress => eclipseProgress;
        
        /// <summary>
        /// How much the sun has moved toward center (0 = far, 1 = center)
        /// Based on RIGHT rings
        /// </summary>
        public float SunProgress => Mathf.Min(rightRings / 5f, 1f);
        
        /// <summary>
        /// How much the moon has moved toward center (0 = far, 1 = center)
        /// Based on LEFT rings
        /// </summary>
        public float MoonProgress => Mathf.Min(leftRings / 5f, 1f);
        
        /// <summary>
        /// How close to eclipse (0 = far apart, 1 = overlapping)
        /// </summary>
        public float EclipseProximity => Mathf.Min(SunProgress, MoonProgress);
        #endregion

        #region Public Methods - Ring Management
        /// <summary>
        /// Add a LEFT ring (dark/combat choice) - called after boss defeat
        /// </summary>
        public void AddLeftRing()
        {
            if (totalRings >= 10)
            {
                Debug.LogWarning("[WorldStateManager] All 10 bosses already defeated!");
                return;
            }
            
            leftRings++;
            
            if (enableDebugLogs)
                Debug.Log($"[WorldStateManager] ◀ LEFT ring added! Total LEFT: {leftRings}");
            
            RecalculateState();
        }

        /// <summary>
        /// Add a RIGHT ring (light/persuasion choice) - called after boss defeat
        /// </summary>
        public void AddRightRing()
        {
            if (totalRings >= 10)
            {
                Debug.LogWarning("[WorldStateManager] All 10 bosses already defeated!");
                return;
            }
            
            rightRings++;
            
            if (enableDebugLogs)
                Debug.Log($"[WorldStateManager] ▶ RIGHT ring added! Total RIGHT: {rightRings}");
            
            RecalculateState();
        }

        /// <summary>
        /// Set exact ring counts (for loading saved games or testing)
        /// </summary>
        public void SetRings(int left, int right)
        {
            left = Mathf.Clamp(left, 0, 10);
            right = Mathf.Clamp(right, 0, 10);
            
            if (left + right > 10)
            {
                Debug.LogError("[WorldStateManager] Total rings cannot exceed 10!");
                return;
            }
            
            leftRings = left;
            rightRings = right;
            
            if (enableDebugLogs)
                Debug.Log($"[WorldStateManager] Rings set to LEFT:{leftRings} RIGHT:{rightRings}");
            
            RecalculateState();
        }

        /// <summary>
        /// Reset all rings to zero (new game)
        /// </summary>
        public void ResetRings()
        {
            leftRings = 0;
            rightRings = 0;
            
            if (enableDebugLogs)
                Debug.Log("[WorldStateManager] All rings reset to 0 - New Game!");
            
            RecalculateState();
        }
        #endregion

        #region Private Methods - Calculation
        private void RecalculateState()
        {
            int previousBalance = balanceScore;
            WorldState previousState = currentWorldState;
            bool wasEclipse = isEclipseActive;
            
            // Calculate totals
            totalRings = leftRings + rightRings;
            balanceScore = rightRings - leftRings;
            
            // Calculate eclipse progress (how close sun and moon are)
            eclipseProgress = EclipseProximity;
            
            // Check for TRUE Eclipse (5L + 5R)
            isEclipseActive = (leftRings == 5 && rightRings == 5);
            
            // Determine world state
            currentWorldState = DetermineWorldState();
            
            // Determine ending (only meaningful when all 10 rings collected)
            currentEnding = DetermineEnding();
            
            // Fire events
            OnRingsChanged?.Invoke(leftRings, rightRings);
            
            if (balanceScore != previousBalance)
            {
                OnBalanceScoreChanged?.Invoke(balanceScore);
            }
            
            if (currentWorldState != previousState)
            {
                OnWorldStateChanged?.Invoke(currentWorldState);
                
                if (enableDebugLogs)
                    Debug.Log($"[WorldStateManager] ★ WORLD STATE: {currentWorldState}");
            }
            
            // Special eclipse event!
            if (isEclipseActive && !wasEclipse)
            {
                OnEclipseAchieved?.Invoke();
                
                if (enableDebugLogs)
                    Debug.Log("[WorldStateManager] ☀🌙 ECLIPSE ACHIEVED! The sun and moon align! ☀🌙");
            }
            
            if (enableDebugLogs)
                LogCurrentState();
        }

        private WorldState DetermineWorldState()
        {
            // Check for eclipse first
            if (leftRings == 5 && rightRings == 5)
                return WorldState.Eclipse;
            
            // No rings yet
            if (totalRings == 0)
                return WorldState.NormalDay;
            
            // All rings, check extremes
            if (totalRings == 10)
            {
                if (leftRings >= 8) return WorldState.PureDark;
                if (rightRings >= 8) return WorldState.PureLight;
            }
            
            // Leaning based on balance
            if (balanceScore > 0)
                return WorldState.LeaningLight;
            else if (balanceScore < 0)
                return WorldState.LeaningDark;
            
            return WorldState.NormalDay;
        }

        private Ending DetermineEnding()
        {
            // Ending only determined when all 10 rings collected
            if (totalRings < 10)
                return Ending.NotYetDetermined;
            
            // Check balance for ending
            if (balanceScore == 0) return Ending.TrueBalance;        // 5L + 5R
            if (balanceScore <= -6) return Ending.TheDevourer;       // 8L+2R, 9L+1R, 10L+0R
            if (balanceScore <= -3) return Ending.VengefulSpirit;    // 6L+4R, 7L+3R
            if (balanceScore >= 6) return Ending.Enlightenment;      // 2L+8R, 1L+9R, 0L+10R
            if (balanceScore >= 3) return Ending.HarmonyPath;        // 4L+6R, 3L+7R
            
            // Close to balance but not perfect
            return Ending.TrueBalance; // 4L+6R or 6L+4R close enough
        }

        private void LogCurrentState()
        {
            string eclipseStatus = isEclipseActive ? "☀🌙 ECLIPSE!" : $"Progress: {eclipseProgress:P0}";
            Debug.Log($"[WorldStateManager] L:{leftRings} R:{rightRings} | Balance:{balanceScore:+#;-#;0} | " +
                     $"State:{currentWorldState} | {eclipseStatus}");
        }
        #endregion

        #region Utility Methods
        /// <summary>
        /// Get remaining bosses
        /// </summary>
        public int GetRemainingBosses() => 10 - totalRings;

        /// <summary>
        /// Check if can still achieve eclipse (need equal rings remaining for both sides)
        /// </summary>
        public bool CanStillAchieveEclipse()
        {
            int remaining = GetRemainingBosses();
            int leftNeeded = 5 - leftRings;
            int rightNeeded = 5 - rightRings;
            
            return leftNeeded >= 0 && rightNeeded >= 0 && 
                   leftNeeded + rightNeeded <= remaining;
        }

        /// <summary>
        /// Get display name for current world state
        /// </summary>
        public string GetWorldStateDisplayName()
        {
            return currentWorldState switch
            {
                WorldState.NormalDay => "Normal Day",
                WorldState.LeaningLight => "Leaning Light",
                WorldState.LeaningDark => "Leaning Dark",
                WorldState.Eclipse => "TRUE ECLIPSE",
                WorldState.PureLight => "Pure Light",
                WorldState.PureDark => "Pure Dark",
                _ => "Unknown"
            };
        }
        #endregion

        #region Editor Testing
        #if UNITY_EDITOR
        private void Update()
        {
            // Use SHIFT + Number keys for testing (avoids conflict with animation tester)
            if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
            {
                // Shift+0: Reset (0L + 0R)
                if (Input.GetKeyDown(KeyCode.Alpha0)) SetRings(0, 0);
                
                // Shift+1: 1 Left ring
                if (Input.GetKeyDown(KeyCode.Alpha1)) SetRings(1, 0);
                
                // Shift+2: 3 Left rings (leaning dark)
                if (Input.GetKeyDown(KeyCode.Alpha2)) SetRings(3, 1);
                
                // Shift+3: 5 Left rings (more dark)
                if (Input.GetKeyDown(KeyCode.Alpha3)) SetRings(5, 2);
                
                // Shift+4: Close to eclipse (4L + 4R)
                if (Input.GetKeyDown(KeyCode.Alpha4)) SetRings(4, 4);
                
                // Shift+5: ECLIPSE! (5L + 5R)
                if (Input.GetKeyDown(KeyCode.Alpha5)) SetRings(5, 5);
                
                // Shift+6: Close to eclipse other side (4L + 4R) - same as 4
                if (Input.GetKeyDown(KeyCode.Alpha6)) SetRings(4, 5);
                
                // Shift+7: 2 Left + 5 Right (leaning light)
                if (Input.GetKeyDown(KeyCode.Alpha7)) SetRings(2, 5);
                
                // Shift+8: 1 Left + 7 Right (more light)
                if (Input.GetKeyDown(KeyCode.Alpha8)) SetRings(1, 7);
                
                // Shift+9: Pure Light (0L + 10R)
                if (Input.GetKeyDown(KeyCode.Alpha9)) SetRings(0, 10);
                
                // Shift+L: Add LEFT ring
                if (Input.GetKeyDown(KeyCode.L)) AddLeftRing();
                
                // Shift+R: Add RIGHT ring
                if (Input.GetKeyDown(KeyCode.R)) AddRightRing();
            }
        }
        
        private void OnGUI()
        {
            // Display current state in corner
            GUIStyle style = new GUIStyle(GUI.skin.box);
            style.fontSize = 14;
            
            string info = $"YORU Balance System\n" +
                         $"LEFT: {leftRings}  RIGHT: {rightRings}\n" +
                         $"Balance: {balanceScore:+#;-#;0}\n" +
                         $"State: {currentWorldState}\n" +
                         $"Eclipse: {(isEclipseActive ? "ACTIVE!" : $"{eclipseProgress:P0}")}";
            
            GUI.Box(new Rect(Screen.width - 220, 10, 210, 110), info);
        }
        #endif
        #endregion
    }
}