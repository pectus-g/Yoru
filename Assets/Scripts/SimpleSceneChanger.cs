using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class SimpleSceneChanger : MonoBehaviour 
{
    [Header("Scene to Load")]
    [SerializeField] private string nextSceneName = "NextScene";
    
    [Header("Trigger Settings")]
    [SerializeField] private bool useKeyPress = true;
    [SerializeField] private KeyCode interactKey = KeyCode.E;
    
    [Header("Fade Settings")]
    [SerializeField] private float fadeSpeed = 1f;
    [SerializeField] private Color fadeColor = Color.black;
    
    [Header("UI")]
    [SerializeField] private GameObject promptUI; // Optional UI prompt
    
    private bool playerInRange = false;
    private bool isChangingScene = false;
    private Image fadeImage;
    private Canvas fadeCanvas;
    
    void Start() 
    {
        CreateFadeOverlay();
        
        // Hide prompt at start
        if (promptUI != null) 
            promptUI.SetActive(false);
    }
    
    void CreateFadeOverlay() 
    {
        // Create a canvas for fade effect
        GameObject canvasObj = new GameObject("FadeCanvas");
        fadeCanvas = canvasObj.AddComponent<Canvas>();
        fadeCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        fadeCanvas.sortingOrder = 1000; // Always on top
        
        // Create black image for fade
        GameObject imageObj = new GameObject("FadeImage");
        imageObj.transform.SetParent(fadeCanvas.transform);
        fadeImage = imageObj.AddComponent<Image>();
        fadeImage.color = new Color(fadeColor.r, fadeColor.g, fadeColor.b, 0f); // Start transparent
        
        // Make image cover full screen
        RectTransform rect = fadeImage.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
    
    void Update() 
    {
        if (playerInRange && !isChangingScene) 
        {
            if (useKeyPress) 
            {
                // Show prompt
                if (promptUI != null) 
                    promptUI.SetActive(true);
                
                // Check for key press
                if (Input.GetKeyDown(interactKey)) 
                {
                    StartSceneChange();
                }
            }
            else 
            {
                // Automatic trigger
                StartSceneChange();
            }
        }
        else if (promptUI != null) 
        {
            promptUI.SetActive(false);
        }
    }
    
    void OnTriggerEnter(Collider other) 
    {
        if (other.CompareTag("Player")) 
        {
            playerInRange = true;
            Debug.Log("Player can change scene now");
        }
    }
    
    void OnTriggerExit(Collider other) 
    {
        if (other.CompareTag("Player")) 
        {
            playerInRange = false;
        }
    }
    
    public void StartSceneChange() 
    {
        if (!isChangingScene) 
        {
            isChangingScene = true;
            StartCoroutine(ChangeSceneWithFade());
        }
    }
    
    IEnumerator ChangeSceneWithFade() 
    {
        Debug.Log($"Changing to scene: {nextSceneName}");
        
        // Fade to black
        yield return StartCoroutine(FadeToBlack());
        
        // Load new scene
        SceneManager.LoadScene(nextSceneName);
        
        // Fade from black (this will happen in the new scene if this script exists there)
        yield return StartCoroutine(FadeFromBlack());
    }
    
    IEnumerator FadeToBlack() 
    {
        float timer = 0f;
        Color startColor = fadeImage.color;
        Color targetColor = fadeColor; // Full opacity
        
        while (timer < fadeSpeed) 
        {
            timer += Time.deltaTime;
            float progress = timer / fadeSpeed;
            
            fadeImage.color = Color.Lerp(startColor, targetColor, progress);
            yield return null;
        }
        
        fadeImage.color = targetColor;
    }
    
    IEnumerator FadeFromBlack() 
    {
        float timer = 0f;
        Color startColor = fadeColor; // Full opacity
        Color targetColor = new Color(fadeColor.r, fadeColor.g, fadeColor.b, 0f); // Transparent
        
        while (timer < fadeSpeed) 
        {
            timer += Time.deltaTime;
            float progress = timer / fadeSpeed;
            
            fadeImage.color = Color.Lerp(startColor, targetColor, progress);
            yield return null;
        }
        
        fadeImage.color = targetColor;
    }
}