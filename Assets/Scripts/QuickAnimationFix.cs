using UnityEngine;

public class QuickAnimationFix : MonoBehaviour
{
    [Header("Quick Animation Setup")]
    [Space(10)]
    [Button("Setup Player Now")]
    public bool setupPlayerButton;
    
    [Button("Fix Animation Issues")]
    public bool fixAnimationButton;
    
    void Start()
    {
        // This runs automatically when you add this component
        AutoSetupPlayer();
    }
    
    public void AutoSetupPlayer()
    {
        Debug.Log("=== Auto Setting Up Cat Player ===");
        
        GameObject player = this.gameObject;
        
        // Add Character Controller
        if (player.GetComponent<CharacterController>() == null)
        {
            var cc = player.AddComponent<CharacterController>();
            cc.radius = 0.3f;
            cc.height = 1.2f;
            cc.center = new Vector3(0, 0.6f, 0);
            Debug.Log("✓ Added CharacterController");
        }
        
        // Add Cat Player Controller
        if (player.GetComponent<CatPlayerController>() == null)
        {
            var catController = player.AddComponent<CatPlayerController>();
            catController.walkSpeed = 3f;
            catController.runSpeed = 6f;
            catController.jumpHeight = 2f;
            catController.rotationSpeed = 720f;
            Debug.Log("✓ Added CatPlayerController");
        }
        
        // Add Simple Cat Animator
        if (player.GetComponent<SimpleCatAnimator>() == null)
        {
            var simpleAnimator = player.AddComponent<SimpleCatAnimator>();
            
            // Find and assign the animator
            Animator catAnimator = player.GetComponentInChildren<Animator>();
            if (catAnimator != null)
            {
                simpleAnimator.catAnimator = catAnimator;
                catAnimator.applyRootMotion = false; // Fix backwards walking
                Debug.Log("✓ Added SimpleCatAnimator and connected to: " + catAnimator.name);
            }
        }
        
        // Set player tag
        player.tag = "Player";
        
        Debug.Log("=== Cat Player Setup Complete! ===");
        Debug.Log("Now you can move with WASD, run with Shift, and jump with Space!");
        
        // Remove this component after setup
        Destroy(this);
    }
}

// Custom property attribute for buttons (visual only)
public class ButtonAttribute : PropertyAttribute
{
    public string text;
    public ButtonAttribute(string text) { this.text = text; }
}