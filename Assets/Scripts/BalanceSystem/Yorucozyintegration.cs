using UnityEngine;
using DistantLands.Cozy;

/// <summary>
/// YORU: Connects karma rings to COZY Weather system.
/// 
/// SIMPLE APPROACH:
/// - COZY "Pause Time" should be CHECKED (time paused)
/// - We directly SET the time when rings change
/// - Uses Coroutines for smooth transitions (not Update loop)
/// 
/// SETUP:
/// - Time Module: CHECK "Pause Time" 
/// - Weather Module: "Manual" mode
/// - Eclipse Module: "Manual" mode
/// </summary>
public class YoruCozyIntegration : MonoBehaviour
{
    #region Serialized Fields
    
    [Header("=== TIME MAPPING ===")]
    [Tooltip("Time at 0 rings (neutral). 12 = Noon")]
    [SerializeField] private float timeAt0Rings = 12f;
    
    [Tooltip("Time at 5 LEFT rings (sunset). 18 = 6PM")]
    [SerializeField] private float timeAt5Left = 18f;
    
    [Tooltip("Time at 10 LEFT rings (midnight). 0 or 24 = Midnight")]
    [SerializeField] private float timeAt10Left = 0f;
    
    [Tooltip("Time at 5 RIGHT rings (morning). 9 = 9AM")]
    [SerializeField] private float timeAt5Right = 9f;
    
    [Tooltip("Time at 10 RIGHT rings (bright noon). 11 = 11AM")]
    [SerializeField] private float timeAt10Right = 11f;
    
    [Header("=== ECLIPSE ===")]
    [SerializeField, Range(0f, 1f)] private float eclipseIntensity = 1f;
    [SerializeField] private bool showPartialEclipse = true;
    
    [Header("=== SMOOTH TRANSITION ===")]
    [Tooltip("Use coroutine for smooth time change")]
    [SerializeField] private bool useSmoothTransition = true;
    [SerializeField] private float transitionDuration = 2f;
    
    [Header("=== DEBUG ===")]
    [SerializeField] private bool showDebugInfo = true;
    
    #endregion
    
    #region Private
    
    private CozyWeather cozy;
    private EclipseModule eclipseModule;
    private Coroutine timeTransitionCoroutine;
    private Coroutine eclipseTransitionCoroutine;
    
    #endregion
    
    #region Unity Lifecycle
    
    private void Start()
    {
        // Find COZY
        cozy = CozyWeather.instance;
        if (cozy == null)
        {
            Debug.LogError("[YoruCozy] CozyWeather not found!");
            enabled = false;
            return;
        }
        
        // Find Eclipse
        eclipseModule = cozy.GetModule<EclipseModule>();
        if (eclipseModule == null)
        {
            Debug.LogWarning("[YoruCozy] Eclipse module not found.");
        }
        
        // Subscribe to ring changes
        if (WorldStateManager.Instance != null)
        {
            WorldStateManager.Instance.OnRingsChanged.AddListener(OnRingsChanged);
            // Apply initial state
            OnRingsChanged(WorldStateManager.Instance.LeftRings, WorldStateManager.Instance.RightRings);
        }
        
        Log("Initialized. Make sure COZY 'Pause Time' is CHECKED!");
    }
    
    private void OnDestroy()
    {
        if (WorldStateManager.Instance != null)
        {
            WorldStateManager.Instance.OnRingsChanged.RemoveListener(OnRingsChanged);
        }
    }
    
    #endregion
    
    #region Ring Handler
    
    private void OnRingsChanged(int leftRings, int rightRings)
    {
        Log($"Rings changed: {leftRings}L / {rightRings}R");
        
        // Calculate target time
        float targetTime = CalculateTime(leftRings, rightRings);
        Log($"Target time: {targetTime:F1}h ({FormatTime(targetTime)})");
        
        // Calculate eclipse
        float targetEclipse = CalculateEclipse(leftRings, rightRings);
        Log($"Target eclipse: {targetEclipse:F2}");
        
        // Apply
        if (useSmoothTransition)
        {
            StartTimeTransition(targetTime);
            StartEclipseTransition(targetEclipse);
        }
        else
        {
            SetTimeImmediate(targetTime);
            SetEclipseImmediate(targetEclipse);
        }
    }
    
    #endregion
    
    #region Time Calculation
    
    private float CalculateTime(int leftRings, int rightRings)
    {
        int total = leftRings + rightRings;
        
        // No rings = neutral
        if (total == 0)
        {
            return timeAt0Rings;
        }
        
        // Eclipse (5+5) = noon
        if (leftRings == 5 && rightRings == 5)
        {
            return timeAt0Rings;
        }
        
        // Calculate dark path time (left rings)
        float darkTime = CalculateDarkTime(leftRings);
        
        // Calculate light path time (right rings)
        float lightTime = CalculateLightTime(rightRings);
        
        // Weighted average
        float leftWeight = (float)leftRings / total;
        float rightWeight = (float)rightRings / total;
        
        // Simple weighted blend
        float result = (darkTime * leftWeight) + (lightTime * rightWeight);
        
        return result;
    }
    
    private float CalculateDarkTime(int leftRings)
    {
        if (leftRings == 0) return timeAt0Rings;
        
        if (leftRings <= 5)
        {
            // 0-5: Noon → Sunset
            float t = leftRings / 5f;
            return Mathf.Lerp(timeAt0Rings, timeAt5Left, t);
        }
        else
        {
            // 5-10: Sunset → Midnight
            float t = (leftRings - 5) / 5f;
            
            // Handle midnight wraparound
            float target = timeAt10Left;
            if (target < timeAt5Left)
            {
                target = 24f; // Treat 0 as 24 for lerp
            }
            
            float result = Mathf.Lerp(timeAt5Left, target, t);
            if (result >= 24f) result = 0f;
            
            return result;
        }
    }
    
    private float CalculateLightTime(int rightRings)
    {
        if (rightRings == 0) return timeAt0Rings;
        
        if (rightRings <= 5)
        {
            // 0-5: Noon → Morning (going backward in time)
            float t = rightRings / 5f;
            return Mathf.Lerp(timeAt0Rings, timeAt5Right, t);
        }
        else
        {
            // 5-10: Morning → Late morning
            float t = (rightRings - 5) / 5f;
            return Mathf.Lerp(timeAt5Right, timeAt10Right, t);
        }
    }
    
    #endregion
    
    #region Eclipse Calculation
    
    private float CalculateEclipse(int leftRings, int rightRings)
    {
        // Perfect balance
        if (leftRings == 5 && rightRings == 5)
        {
            return eclipseIntensity;
        }
        
        if (!showPartialEclipse) return 0f;
        if (leftRings == 0 || rightRings == 0) return 0f;
        
        // Partial eclipse when close to balance
        int smaller = Mathf.Min(leftRings, rightRings);
        int larger = Mathf.Max(leftRings, rightRings);
        
        float balance = (float)smaller / larger;
        float leftDist = Mathf.Abs(leftRings - 5) / 5f;
        float rightDist = Mathf.Abs(rightRings - 5) / 5f;
        float center = 1f - Mathf.Max(leftDist, rightDist);
        
        float eclipse = balance * center * eclipseIntensity;
        return eclipse > 0.15f ? eclipse : 0f;
    }
    
    #endregion
    
    #region Apply to COZY
    
    private void SetTimeImmediate(float hours)
    {
        if (cozy?.timeModule != null)
        {
            cozy.timeModule.currentTime = hours;
            Log($"Time set to: {FormatTime(hours)}");
        }
    }
    
    private void SetEclipseImmediate(float intensity)
    {
        if (eclipseModule != null)
        {
            eclipseModule.eclipseRatio = intensity;
        }
    }
    
    #endregion
    
    #region Smooth Transitions (Coroutines)
    
    private void StartTimeTransition(float targetTime)
    {
        if (timeTransitionCoroutine != null)
        {
            StopCoroutine(timeTransitionCoroutine);
        }
        timeTransitionCoroutine = StartCoroutine(TimeTransitionRoutine(targetTime));
    }
    
    private System.Collections.IEnumerator TimeTransitionRoutine(float targetTime)
    {
        if (cozy?.timeModule == null) yield break;
        
        float startTime = cozy.timeModule.currentTime;
        float elapsed = 0f;
        
        // Handle wraparound (e.g., going from 22 to 2)
        float diff = targetTime - startTime;
        if (diff > 12f) 
        {
            startTime += 24f;
        }
        else if (diff < -12f)
        {
            targetTime += 24f;
        }
        
        while (elapsed < transitionDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / transitionDuration;
            t = t * t * (3f - 2f * t); // Smoothstep
            
            float current = Mathf.Lerp(startTime, targetTime, t);
            
            // Normalize to 0-24
            while (current >= 24f) current -= 24f;
            while (current < 0f) current += 24f;
            
            cozy.timeModule.currentTime = current;
            
            yield return null;
        }
        
        // Ensure we hit exact target
        float final = targetTime;
        while (final >= 24f) final -= 24f;
        cozy.timeModule.currentTime = final;
        
        timeTransitionCoroutine = null;
    }
    
    private void StartEclipseTransition(float targetEclipse)
    {
        if (eclipseTransitionCoroutine != null)
        {
            StopCoroutine(eclipseTransitionCoroutine);
        }
        eclipseTransitionCoroutine = StartCoroutine(EclipseTransitionRoutine(targetEclipse));
    }
    
    private System.Collections.IEnumerator EclipseTransitionRoutine(float targetEclipse)
    {
        if (eclipseModule == null) yield break;
        
        float startEclipse = eclipseModule.eclipseRatio;
        float elapsed = 0f;
        
        while (elapsed < transitionDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / transitionDuration;
            t = t * t * (3f - 2f * t); // Smoothstep
            
            eclipseModule.eclipseRatio = Mathf.Lerp(startEclipse, targetEclipse, t);
            
            yield return null;
        }
        
        eclipseModule.eclipseRatio = targetEclipse;
        eclipseTransitionCoroutine = null;
    }
    
    #endregion
    
    #region Helpers
    
    private void Log(string msg)
    {
        if (showDebugInfo)
        {
            Debug.Log($"[YoruCozy] {msg}");
        }
    }
    
    private string FormatTime(float hours)
    {
        int h = Mathf.FloorToInt(hours);
        int m = Mathf.FloorToInt((hours - h) * 60f);
        string ampm = h >= 12 ? "PM" : "AM";
        int display = h > 12 ? h - 12 : (h == 0 ? 12 : h);
        return $"{display}:{m:D2} {ampm}";
    }
    
    #endregion
    
    #region Public API
    
    /// <summary>
    /// Immediately snap to current ring state (no transition).
    /// </summary>
    public void SnapToCurrentState()
    {
        if (WorldStateManager.Instance == null) return;
        
        int left = WorldStateManager.Instance.LeftRings;
        int right = WorldStateManager.Instance.RightRings;
        
        SetTimeImmediate(CalculateTime(left, right));
        SetEclipseImmediate(CalculateEclipse(left, right));
    }
    
    #endregion
}