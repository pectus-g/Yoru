using UnityEngine;

public class SkyboxSwitcher : MonoBehaviour
{
    [Header("Skybox Materials")]
    [SerializeField] private Material daySkybox;
    [SerializeField] private Material nightSkybox;
    
    [Header("Time Settings")]
    [SerializeField] private float dayDuration = 60f;
    [SerializeField] private float nightDuration = 60f;
    [SerializeField] private float transitionDuration = 5f;
    
    [Header("Lighting")]
    [SerializeField] private Light directionalLight;
    [SerializeField] private float dayLightIntensity = 1.0f;
    [SerializeField] private float nightLightIntensity = 0.2f;
    [SerializeField] private Color dayLightColor = new Color(1f, 0.95f, 0.8f);
    [SerializeField] private Color nightLightColor = new Color(0.5f, 0.5f, 0.8f);
    
    [Header("Debug")]
    [SerializeField] private bool autoTransition = true;
    [SerializeField] private bool isNight = false;
    
    private float currentPhaseTime = 0f;
    private float transitionProgress = 0f;
    private bool isTransitioning = false;
    
    void Start()
    {
        // Start with day skybox
        RenderSettings.skybox = daySkybox;
        DynamicGI.UpdateEnvironment();
    }
    
    void Update()
    {
        if (autoTransition)
        {
            currentPhaseTime += Time.deltaTime;
            
            float currentPhaseDuration = isNight ? nightDuration : dayDuration;
            
            if (currentPhaseTime >= currentPhaseDuration && !isTransitioning)
            {
                StartTransition();
                currentPhaseTime = 0f;
            }
        }
        
        if (isTransitioning)
        {
            UpdateTransition();
        }
    }
    
    void StartTransition()
    {
        isTransitioning = true;
        transitionProgress = 0f;
        
        Debug.Log(isNight ? "☀️ Transitioning to DAY" : "🌙 Transitioning to NIGHT");
    }
    
    void UpdateTransition()
    {
        transitionProgress += Time.deltaTime / transitionDuration;
        
        if (transitionProgress >= 1f)
        {
            transitionProgress = 1f;
            isTransitioning = false;
            isNight = !isNight;
            
            // Switch skybox completely
            RenderSettings.skybox = isNight ? nightSkybox : daySkybox;
            DynamicGI.UpdateEnvironment();
        }
        
        // Calculate blend (0 = current, 1 = target)
        float blend = Mathf.SmoothStep(0f, 1f, transitionProgress);
        
        // Blend lighting only (skybox switches at end)
        if (directionalLight != null)
        {
            if (isNight)
            {
                // Going to night
                directionalLight.intensity = Mathf.Lerp(dayLightIntensity, nightLightIntensity, blend);
                directionalLight.color = Color.Lerp(dayLightColor, nightLightColor, blend);
            }
            else
            {
                // Going to day
                directionalLight.intensity = Mathf.Lerp(nightLightIntensity, dayLightIntensity, blend);
                directionalLight.color = Color.Lerp(nightLightColor, dayLightColor, blend);
            }
        }
        
        // Fade skybox exposure for smooth transition
        Material currentSkybox = RenderSettings.skybox;
        if (currentSkybox != null && currentSkybox.HasProperty("_Exposure"))
        {
            float targetExposure = isNight ? 0.3f : 1.0f;
            float startExposure = isNight ? 1.0f : 0.3f;
            currentSkybox.SetFloat("_Exposure", Mathf.Lerp(startExposure, targetExposure, blend));
        }
    }
    
    public void ForceNight()
    {
        if (!isNight && !isTransitioning) StartTransition();
    }
    
    public void ForceDay()
    {
        if (isNight && !isTransitioning) StartTransition();
    }
}
