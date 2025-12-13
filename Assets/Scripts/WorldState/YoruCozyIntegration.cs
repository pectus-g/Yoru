using UnityEngine;

namespace Yoru.BalanceSystem
{
    /// <summary>
    /// Connects YORU's WorldStateManager to COZY Eclipse.
    /// Simply controls the Eclipse Ratio value based on karma balance.
    /// 
    /// SETUP:
    /// 1. In COZY Time module: Check "Pause Time" to freeze day/night
    /// 2. Add this script to YORU_BalanceSystem
    /// 3. It will auto-find COZY and control Eclipse Ratio
    /// </summary>
    public class YoruCozyIntegration : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField, Range(0.1f, 5f)] private float transitionSpeed = 1.5f;
        [SerializeField] private bool debugMode = true;
        
        [Header("Runtime (Read Only)")]
        [SerializeField, Range(0f, 1f)] private float targetEclipseRatio;
        [SerializeField, Range(0f, 1f)] private float currentEclipseRatio;
        
        // COZY references - found at runtime
        private MonoBehaviour cozyWeather;
        private Component eclipseModule;
        private System.Reflection.FieldInfo eclipseRatioField;
        private bool isReady = false;
        
        void Start()
        {
            FindCozyReferences();
            
            if (WorldStateManager.Instance != null)
            {
                WorldStateManager.Instance.OnRingsChanged.AddListener(OnRingsChanged);
                OnRingsChanged(WorldStateManager.Instance.LeftRings, WorldStateManager.Instance.RightRings);
            }
        }
        
        void FindCozyReferences()
        {
            // Find Cozy Weather Sphere
            var allObjects = FindObjectsOfType<MonoBehaviour>();
            foreach (var mb in allObjects)
            {
                if (mb.GetType().Name == "CozyWeather")
                {
                    cozyWeather = mb;
                    Log("Found CozyWeather");
                    break;
                }
            }
            
            if (cozyWeather == null)
            {
                Debug.LogError("[YORU] CozyWeather not found!");
                return;
            }
            
            // Find Eclipse module - it's a component on the same GameObject or child
            var components = cozyWeather.GetComponentsInChildren<Component>();
            foreach (var comp in components)
            {
                string typeName = comp.GetType().Name;
                if (typeName.Contains("Eclipse"))
                {
                    eclipseModule = comp;
                    Log("Found Eclipse module: " + typeName);
                    
                    // Find the eclipseRatio field
                    var type = comp.GetType();
                    eclipseRatioField = type.GetField("eclipseRatio", 
                        System.Reflection.BindingFlags.Public | 
                        System.Reflection.BindingFlags.NonPublic | 
                        System.Reflection.BindingFlags.Instance);
                    
                    if (eclipseRatioField != null)
                    {
                        Log("Found eclipseRatio field");
                        isReady = true;
                    }
                    else
                    {
                        // Try property
                        var prop = type.GetProperty("eclipseRatio");
                        if (prop != null)
                        {
                            Log("Found eclipseRatio property");
                            isReady = true;
                        }
                    }
                    break;
                }
            }
            
            if (!isReady)
            {
                Debug.LogWarning("[YORU] Eclipse module not fully configured. Make sure Eclipse module is added to COZY.");
            }
        }
        
        void Update()
        {
            if (!isReady) return;
            
            // Smooth transition
            currentEclipseRatio = Mathf.MoveTowards(currentEclipseRatio, targetEclipseRatio, 
                Time.deltaTime * transitionSpeed);
            
            // Apply to COZY
            SetEclipseRatio(currentEclipseRatio);
        }
        
        void SetEclipseRatio(float value)
        {
            if (eclipseModule == null) return;
            
            try
            {
                if (eclipseRatioField != null)
                {
                    eclipseRatioField.SetValue(eclipseModule, value);
                }
            }
            catch (System.Exception e)
            {
                if (debugMode) Debug.LogWarning("[YORU] Error setting eclipse: " + e.Message);
            }
        }
        
        void OnDestroy()
        {
            if (WorldStateManager.Instance != null)
            {
                WorldStateManager.Instance.OnRingsChanged.RemoveListener(OnRingsChanged);
            }
        }
        
        void OnRingsChanged(int left, int right)
        {
            // Calculate eclipse ratio based on balance
            CalculateEclipse(left, right);
            Log($"Rings: L={left} R={right} → Eclipse: {targetEclipseRatio:P0}");
        }
        
        void CalculateEclipse(int left, int right)
        {
            // PERFECT BALANCE (5L + 5R) = FULL ECLIPSE
            if (left == 5 && right == 5)
            {
                targetEclipseRatio = 1f;
                return;
            }
            
            // APPROACHING BALANCE = PARTIAL ECLIPSE
            // Eclipse starts showing when both are at least 3
            if (left >= 3 && right >= 3)
            {
                float leftProgress = left / 5f;
                float rightProgress = right / 5f;
                float minProgress = Mathf.Min(leftProgress, rightProgress);
                
                // Scale from 0 to 0.7 (save full eclipse for perfect balance)
                targetEclipseRatio = minProgress * 0.7f;
            }
            else
            {
                targetEclipseRatio = 0f;
            }
        }
        
        void Log(string msg)
        {
            if (debugMode) Debug.Log($"[YORU-COZY] {msg}");
        }
        
        // === PUBLIC API ===
        
        public void ForceEclipse(float ratio)
        {
            targetEclipseRatio = Mathf.Clamp01(ratio);
            currentEclipseRatio = targetEclipseRatio;
            SetEclipseRatio(currentEclipseRatio);
        }
        
        public void ClearEclipse()
        {
            targetEclipseRatio = 0f;
            currentEclipseRatio = 0f;
            SetEclipseRatio(0f);
        }
        
#if UNITY_EDITOR
        [ContextMenu("Force Full Eclipse")]
        void DebugFullEclipse() => ForceEclipse(1f);
        
        [ContextMenu("Force 50% Eclipse")]  
        void Debug50Eclipse() => ForceEclipse(0.5f);
        
        [ContextMenu("Clear Eclipse")]
        void DebugClearEclipse() => ClearEclipse();
#endif
    }
}