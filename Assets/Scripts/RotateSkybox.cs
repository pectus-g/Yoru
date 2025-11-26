using UnityEngine;

public class RotateSkybox : MonoBehaviour
{
    [Header("Rotation Settings")]
    [SerializeField] private float rotationSpeed = 0.5f;  // Degrees per second
    
    void Update()
    {
        // Rotate the skybox slowly
        RenderSettings.skybox.SetFloat("_Rotation", Time.time * rotationSpeed);
    }
}