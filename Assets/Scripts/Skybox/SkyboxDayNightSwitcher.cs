using UnityEngine;

public class SkyboxDayNightSwitcher : MonoBehaviour
{
    [Header("Skybox Materials")]
    [SerializeField] private Material daySkybox;
    [SerializeField] private Material nightSkybox;
    
    [Header("Time Settings")]
    [SerializeField] private float dayDuration = 60f;  // How long day lasts (seconds)
    [SerializeField] private float nightDuration = 60f;  // How long night lasts (seconds)
    [SerializeField] private float transitionDuration = 5f;  // Fade time between day/night
    
    [Header("Rotation Settings")]
    [SerializeField] private bool rotateSkybox = true;
    [SerializeField] private float rotationSpeed = 1f;  // Degrees per second
    
    [Header("Lighting")]
    [SerializeField] private Light directionalLight;
    [SerializeField] private float dayLightIntensity = 1.0f;
    [SerializeField] private float nightLightIntensity = 0.2f;
    [SerializeField] private Color dayLightColor = new Color(1f, 0.95f, 0.8f);
    [SerializeField] private Color nightLightColor = new Color(0.5f, 0.5f, 0.8f);
    
    [Header("Debug")]
    [SerializeField] private bool autoTransition = true;
    [SerializeField] private bool isNight = false;
    [SerializeField] private bool debugMode = true;
    
    private float currentPhaseTime = 0f;
    private float transitionProgress = 0f;
    private bool isTransitioning = false;
    private float currentRotation = 0f;
    
    void Start()
    {
        // Start with day skybox
        RenderSettings.skybox = daySkybox;
        
        // Initialize rotation if both skyboxes support it
        if (rotateSkybox)
        {
            InitializeRotation();
        }
        
        DynamicGI.UpdateEnvironment();
        
        if (debugMode)
        {
            Debug.Log("=== DAY/NIGHT SKYBOX INITIALIZED ===");
            Debug.Log($"Day Skybox: {(daySkybox != null ? daySkybox.name : "NONE")}");
            Debug.Log($"Night Skybox: {(nightSkybox != null ? nightSkybox.name : "NONE")}");
            Debug.Log($"Rotation Enabled: {rotateSkybox}");
            Debug.Log($"Auto Transition: {autoTransition}");
        }
    }
    
    void InitializeRotation()
    {
        // Check if both skyboxes support rotation
        bool dayHasRotation = daySkybox != null && daySkybox.HasProperty("_Rotation");
        bool nightHasRotation = nightSkybox != null && nightSkybox.HasProperty("_Rotation");
        
        if (!dayHasRotation || !nightHasRotation)
        {
            Debug.LogWarning("⚠️ One or both skyboxes don't have _Rotation property! Rotation disabled.");
            rotateSkybox = false;
        }
    }
    
    void Update()
    {
        // ROTATION - Always running if enabled
        if (rotateSkybox)
        {
            UpdateRotation();
        }
        
        // DAY/NIGHT CYCLE
        if (autoTransition)
        {
            AutoDayNightCycle();
        }
        
        if (isTransitioning)
        {
            UpdateTransition();
        }
    }
    
    void AutoDayNightCycle()
    {
        currentPhaseTime += Time.deltaTime;
        
        float currentPhaseDuration = isNight ? nightDuration : dayDuration;
        
        if (currentPhaseTime >= currentPhaseDuration && !isTransitioning)
        {
            StartTransition();
            currentPhaseTime = 0f;
        }
    }
    
    void UpdateRotation()
    {
        // Rotate the skybox
        currentRotation += rotationSpeed * Time.deltaTime;
        
        if (currentRotation >= 360f)
        {
            currentRotation -= 360f;
        }
        
        // Apply rotation to current skybox
        if (RenderSettings.skybox != null && RenderSettings.skybox.HasProperty("_Rotation"))
        {
            RenderSettings.skybox.SetFloat("_Rotation", currentRotation);
        }
    }
    
    void StartTransition()
    {
        isTransitioning = true;
        transitionProgress = 0f;
        
        if (debugMode)
        {
            Debug.Log(isNight ? "☀️ Transitioning to DAY" : "🌙 Transitioning to NIGHT");
        }
    }
    
    void UpdateTransition()
    {
        transitionProgress += Time.deltaTime / transitionDuration;
        
        if (transitionProgress >= 1f)
        {
            transitionProgress = 1f;
            isTransitioning = false;
            isNight = !isNight;
            
            // Switch skybox
            Material newSkybox = isNight ? nightSkybox : daySkybox;
            RenderSettings.skybox = newSkybox;
            
            // IMPORTANT: Apply current rotation to new skybox!
            if (rotateSkybox && newSkybox != null && newSkybox.HasProperty("_Rotation"))
            {
                newSkybox.SetFloat("_Rotation", currentRotation);
            }
            
            DynamicGI.UpdateEnvironment();
            
            if (debugMode)
            {
                Debug.Log($"✅ Switched to {(isNight ? "NIGHT" : "DAY")} skybox");
            }
        }
        
        // Smooth transition curve
        float blend = Mathf.SmoothStep(0f, 1f, transitionProgress);
        
        // Blend lighting
        if (directionalLight != null)
        {
            if (isNight)
            {
                // Transitioning to night
                directionalLight.intensity = Mathf.Lerp(dayLightIntensity, nightLightIntensity, blend);
                directionalLight.color = Color.Lerp(dayLightColor, nightLightColor, blend);
            }
            else
            {
                // Transitioning to day
                directionalLight.intensity = Mathf.Lerp(nightLightIntensity, dayLightIntensity, blend);
                directionalLight.color = Color.Lerp(nightLightColor, dayLightColor, blend);
            }
        }
        
        // Fade skybox exposure for smoother visual transition
        Material currentSkybox = RenderSettings.skybox;
        if (currentSkybox != null && currentSkybox.HasProperty("_Exposure"))
        {
            float targetExposure = isNight ? 0.3f : 1.0f;
            float startExposure = isNight ? 1.0f : 0.3f;
            currentSkybox.SetFloat("_Exposure", Mathf.Lerp(startExposure, targetExposure, blend));
        }
    }
    
    // ========== PUBLIC METHODS (Call from other scripts) ==========
    
    public void ForceNight()
    {
        if (!isNight && !isTransitioning)
        {
            StartTransition();
        }
    }
    
    public void ForceDay()
    {
        if (isNight && !isTransitioning)
        {
            StartTransition();
        }
    }
    
    public void ToggleDayNight()
    {
        if (!isTransitioning)
        {
            StartTransition();
        }
    }
    
    public void SetRotationSpeed(float speed)
    {
        rotationSpeed = speed;
    }
    
    public void SetAutoTransition(bool enabled)
    {
        autoTransition = enabled;
    }
    
    public bool IsNight()
    {
        return isNight;
    }
    
    public bool IsDay()
    {
        return !isNight;
    }
}