using UnityEngine;

public class RotateSkybox : MonoBehaviour
{
    [Header("Day/Night Cycle")]
    [SerializeField] private float dayLengthInSeconds = 120f;  // 2 minutes = 1 full day
    [SerializeField] private bool pauseAtNight = false;
    
    void Update()
    {
        // Calculate rotation speed for full 360° in dayLengthInSeconds
        float rotationSpeed = 360f / dayLengthInSeconds;
        
        // Apply rotation
        float rotation = Time.time * rotationSpeed;
        RenderSettings.skybox.SetFloat("_Rotation", rotation);
    }
}