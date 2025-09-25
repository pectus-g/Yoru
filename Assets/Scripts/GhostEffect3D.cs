using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GhostEffect3D : MonoBehaviour 
{
    [Header("Ghost Settings")]
    [SerializeField] [Range(0f, 1f)] private float ghostAlpha = 0.4f; // Transparent when hiding
    [SerializeField] [Range(0f, 1f)] private float hiddenAlpha = 0.1f; // Almost invisible when hiding
    [SerializeField] private float fadeSpeed = 3f; // Fast fade for vanish effect
    [SerializeField] private bool startAsGhost = true; // Start transparent
    
    [Header("References")]
    [SerializeField] private Renderer[] characterRenderers; // Assign all renderers of your character
    
    // Private variables
    private Material[] originalMaterials;
    private Material[] ghostMaterials;
    private Color[] originalColors;
    private bool isGhost = false;
    private bool isFading = false;
    
    void Start() 
    {
        SetupMaterials();
        
        if (startAsGhost) 
        {
            SetAlphaImmediate(ghostAlpha);
            isGhost = true;
        }
    }
    
    void SetupMaterials() 
    {
        // Get all renderers if not assigned
        if (characterRenderers == null || characterRenderers.Length == 0) 
        {
            characterRenderers = GetComponentsInChildren<Renderer>();
        }
        
        List<Material> origMats = new List<Material>();
        List<Material> ghostMats = new List<Material>();
        List<Color> origColors = new List<Color>();
        
        // Process all renderers and their materials
        foreach (Renderer renderer in characterRenderers) 
        {
            foreach (Material mat in renderer.materials) 
            {
                origMats.Add(mat);
                origColors.Add(mat.color);
                
                // Create ghost material copy
                Material ghostMat = new Material(mat);
                SetupTransparentMaterial(ghostMat);
                ghostMats.Add(ghostMat);
            }
        }
        
        originalMaterials = origMats.ToArray();
        ghostMaterials = ghostMats.ToArray();
        originalColors = origColors.ToArray();
    }
    
    void SetupTransparentMaterial(Material material) 
    {
        // Setup material for transparency
        if (material.HasProperty("_Mode")) 
        {
            material.SetFloat("_Mode", 3); // Transparent mode
        }
        
        if (material.HasProperty("_SrcBlend")) 
        {
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        }
        
        if (material.HasProperty("_DstBlend")) 
        {
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        }
        
        if (material.HasProperty("_ZWrite")) 
        {
            material.SetFloat("_ZWrite", 0);
        }
        
        material.DisableKeyword("_ALPHATEST_ON");
        material.EnableKeyword("_ALPHABLEND_ON");
        material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        material.renderQueue = 3000;
    }
    
    // Public methods to control ghost effect
    public void BecomeGhost() 
    {
        if (!isFading) 
        {
            StartCoroutine(FadeToAlpha(ghostAlpha, () => isGhost = true));
        }
    }
    
    public void BecomeVisible() 
    {
        if (!isFading) 
        {
            StartCoroutine(FadeToAlpha(1f, () => isGhost = false));
        }
    }
    
    public void BecomeHidden() 
    {
        if (!isFading) 
        {
            StartCoroutine(FadeToAlpha(hiddenAlpha, () => isGhost = true));
        }
    }
    
    public void FadeOut() 
    {
        if (!isFading) 
        {
            StartCoroutine(FadeToAlpha(0f, () => isGhost = true));
        }
    }
    
    // Instant alpha change without animation
    public void SetAlphaImmediate(float alpha) 
    {
        SwitchToGhostMaterials();
        SetAlpha(alpha);
    }
    
    // Coroutine for smooth fading
    IEnumerator FadeToAlpha(float targetAlpha, System.Action onComplete = null) 
    {
        isFading = true;
        SwitchToGhostMaterials();
        
        float startAlpha = GetCurrentAlpha();
        float elapsedTime = 0f;
        float fadeDuration = 1f / fadeSpeed;
        
        while (elapsedTime < fadeDuration) 
        {
            elapsedTime += Time.deltaTime;
            float currentAlpha = Mathf.Lerp(startAlpha, targetAlpha, elapsedTime / fadeDuration);
            SetAlpha(currentAlpha);
            yield return null;
        }
        
        SetAlpha(targetAlpha);
        onComplete?.Invoke();
        isFading = false;
        
        // Switch back to original materials if fully opaque
        if (targetAlpha >= 1f) 
        {
            SwitchToOriginalMaterials();
        }
    }
    
    void SwitchToGhostMaterials() 
    {
        int materialIndex = 0;
        foreach (Renderer renderer in characterRenderers) 
        {
            Material[] newMaterials = new Material[renderer.materials.Length];
            for (int i = 0; i < renderer.materials.Length; i++) 
            {
                newMaterials[i] = ghostMaterials[materialIndex];
                materialIndex++;
            }
            renderer.materials = newMaterials;
        }
    }
    
    void SwitchToOriginalMaterials() 
    {
        int materialIndex = 0;
        foreach (Renderer renderer in characterRenderers) 
        {
            Material[] newMaterials = new Material[renderer.materials.Length];
            for (int i = 0; i < renderer.materials.Length; i++) 
            {
                newMaterials[i] = originalMaterials[materialIndex];
                materialIndex++;
            }
            renderer.materials = newMaterials;
        }
    }
    
    void SetAlpha(float alpha) 
    {
        int materialIndex = 0;
        foreach (Renderer renderer in characterRenderers) 
        {
            foreach (Material mat in renderer.materials) 
            {
                if (mat != null && originalColors != null && materialIndex < originalColors.Length) 
                {
                    Color newColor = originalColors[materialIndex];
                    newColor.a = alpha;
                    mat.color = newColor;
                }
                materialIndex++;
            }
        }
    }
    
    float GetCurrentAlpha() 
    {
        if (characterRenderers.Length > 0 && characterRenderers[0].materials.Length > 0) 
        {
            return characterRenderers[0].materials[0].color.a;
        }
        return 1f;
    }
    
    // Public getters
    public bool IsGhost => isGhost;
    public bool IsFading => isFading;
    public float CurrentAlpha => GetCurrentAlpha();
    
    // Test method for debugging
    [ContextMenu("Test Fade Out")]
    public void TestFadeOut() 
    {
        FadeOut();
    }
    
    [ContextMenu("Test Become Ghost")]
    public void TestBecomeGhost() 
    {
        BecomeGhost();
    }
    
    [ContextMenu("Test Become Visible")]
    public void TestBecomeVisible() 
    {
        BecomeVisible();
    }
    
    void OnDestroy() 
    {
        // Clean up created materials
        if (ghostMaterials != null) 
        {
            foreach (Material mat in ghostMaterials) 
            {
                if (mat != null) 
                {
                    DestroyImmediate(mat);
                }
            }
        }
    }
}