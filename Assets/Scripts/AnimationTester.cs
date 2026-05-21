using UnityEngine;

public class AnimationTester : MonoBehaviour
{
    [Header("Animation Testing for Trailer")]
    [SerializeField] private Animator animator;
    [SerializeField] private float layerTransitionTime = 0.2f;
    
    [Header("Combat Layer Settings")]
    [SerializeField] private KeyCode combatLayerToggle = KeyCode.C;
    [SerializeField] private KeyCode attackPawKey = KeyCode.Q;
    [SerializeField] private KeyCode leftTailCastKey = KeyCode.E;
    [SerializeField] private KeyCode rightTailCastKey = KeyCode.R;
    [SerializeField] private KeyCode towPawKey = KeyCode.G;
    
    [Header("Cinematic Layer Settings")]
    [SerializeField] private KeyCode cinematicLayerToggle = KeyCode.V;
    [SerializeField] private KeyCode freeingSoulKey = KeyCode.Alpha1;
    [SerializeField] private KeyCode wakeupKey = KeyCode.Alpha2;
    [SerializeField] private KeyCode circleActivationKey = KeyCode.Alpha3;
    [SerializeField] private KeyCode sleepKey = KeyCode.Alpha4;
    [SerializeField] private KeyCode scaredKey = KeyCode.Alpha5;
    [SerializeField] private KeyCode absorbingKey = KeyCode.Alpha6;
    [SerializeField] private KeyCode heartKey = KeyCode.Alpha7;
    
    // Layer weights
    private float combatTargetWeight = 0f;
    private float cinematicTargetWeight = 0f;
    private float combatCurrentWeight = 0f;
    private float cinematicCurrentWeight = 0f;
    
    // State tracking
    private bool combatLayerActive = false;
    private bool cinematicLayerActive = false;
    private string lastTriggeredAnimation = "";

    // Phase 2 lockout — AnimationTester's E/R/V/G/1-7 keys are Yoru-form only
    private FormController formController;
    
    void Start()
    {
        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }
        
        // Ensure layers start at 0 weight
        animator.SetLayerWeight(1, 0f); // Combat Layer
        animator.SetLayerWeight(2, 0f); // Cinematic Layer

        formController = FindObjectOfType<FormController>();

        ShowControls();
    }
    
    void Update()
    {
        // Phase 2 lockout: in Tomoe (human) form, all AnimationTester input disabled.
        // E/R tail abilities, V/G/1-7 cinematic triggers are Yoru-only combat actions.
        if (formController != null && formController.IsHuman) return;

        // Toggle layer weights
        if (Input.GetKeyDown(combatLayerToggle))
        {
            ToggleCombatLayer();
        }
        
        if (Input.GetKeyDown(cinematicLayerToggle))
        {
            ToggleCinematicLayer();
        }
        
        // COMBAT ANIMATIONS (Layer 1)
        if (combatLayerActive)
        {
            if (Input.GetKeyDown(attackPawKey))
            {
                TriggerCombatAnimation("AttackPaw");
                Debug.Log("🗡️ Attack Paw!");
            }
            else if (Input.GetKeyDown(leftTailCastKey))
            {
                TriggerCombatAnimation("LeftTailCast");
                Debug.Log("⚡ Left Tail Cast!");
            }
            else if (Input.GetKeyDown(rightTailCastKey))
            {
                TriggerCombatAnimation("RightTailCast");
                Debug.Log("⚡ Right Tail Cast!");
            }
            else if (Input.GetKeyDown(towPawKey))
            {
                TriggerCombatAnimation("TowPaw");
                Debug.Log("🐾 Tow Paw!");
            }
        }
        
        // CINEMATIC ANIMATIONS (Layer 2)
        if (cinematicLayerActive)
        {
            if (Input.GetKeyDown(freeingSoulKey))
            {
                TriggerCinematicAnimation("FreeingSoul");
                Debug.Log("👻 Freeing Soul!");
            }
            else if (Input.GetKeyDown(wakeupKey))
            {
                TriggerCinematicAnimation("Wakeup");
                Debug.Log("😴 Wakeup!");
            }
            else if (Input.GetKeyDown(circleActivationKey))
            {
                TriggerCinematicAnimation("CircleActivation");
                Debug.Log("⭕ Circle Activation!");
            }
            else if (Input.GetKeyDown(sleepKey))
            {
                TriggerCinematicAnimation("Sleep");
                Debug.Log("💤 Sleep!");
            }
            else if (Input.GetKeyDown(scaredKey))
            {
                TriggerCinematicAnimation("Scared");
                Debug.Log("😱 Scared!");
            }
            else if (Input.GetKeyDown(absorbingKey))
            {
                TriggerCinematicAnimation("Absorbing");
                Debug.Log("🌀 Absorbing!");
            }
            else if (Input.GetKeyDown(heartKey))
            {
                TriggerCinematicAnimation("Heart");
                Debug.Log("❤️ Heart!");
            }
        }
        
        // Smooth layer weight transitions
        UpdateLayerWeights();
        
        // Quick reset - hold both Shift keys
        if (Input.GetKey(KeyCode.LeftShift) && Input.GetKey(KeyCode.RightShift))
        {
            ResetAllLayers();
        }
    }
    
    private void ToggleCombatLayer()
    {
        combatLayerActive = !combatLayerActive;
        combatTargetWeight = combatLayerActive ? 1f : 0f;
        
        // Disable cinematic if enabling combat
        if (combatLayerActive && cinematicLayerActive)
        {
            cinematicLayerActive = false;
            cinematicTargetWeight = 0f;
            Debug.Log("📌 Switching to Combat Layer");
        }
        
        Debug.Log($"⚔️ Combat Layer: {(combatLayerActive ? "ACTIVE" : "INACTIVE")}");
        
        if (combatLayerActive)
        {
            ShowCombatControls();
        }
    }
    
    private void ToggleCinematicLayer()
    {
        cinematicLayerActive = !cinematicLayerActive;
        cinematicTargetWeight = cinematicLayerActive ? 1f : 0f;
        
        // Disable combat if enabling cinematic
        if (cinematicLayerActive && combatLayerActive)
        {
            combatLayerActive = false;
            combatTargetWeight = 0f;
            Debug.Log("📌 Switching to Cinematic Layer");
        }
        
        Debug.Log($"🎬 Cinematic Layer: {(cinematicLayerActive ? "ACTIVE" : "INACTIVE")}");
        
        if (cinematicLayerActive)
        {
            ShowCinematicControls();
        }
    }
    
    private void UpdateLayerWeights()
    {
        // Smoothly transition layer weights
        combatCurrentWeight = Mathf.Lerp(combatCurrentWeight, combatTargetWeight, Time.deltaTime / layerTransitionTime);
        cinematicCurrentWeight = Mathf.Lerp(cinematicCurrentWeight, cinematicTargetWeight, Time.deltaTime / layerTransitionTime);
        
        animator.SetLayerWeight(1, combatCurrentWeight);
        animator.SetLayerWeight(2, cinematicCurrentWeight);
    }
    
    private void TriggerCombatAnimation(string triggerName)
    {
        // Reset previous trigger
        if (!string.IsNullOrEmpty(lastTriggeredAnimation))
        {
            animator.ResetTrigger(lastTriggeredAnimation);
        }
        
        animator.SetTrigger(triggerName);
        lastTriggeredAnimation = triggerName;
    }
    
    private void TriggerCinematicAnimation(string triggerName)
    {
        // Reset previous trigger
        if (!string.IsNullOrEmpty(lastTriggeredAnimation))
        {
            animator.ResetTrigger(lastTriggeredAnimation);
        }
        
        animator.SetTrigger(triggerName);
        lastTriggeredAnimation = triggerName;
    }
    
    private void ResetAllLayers()
    {
        combatLayerActive = false;
        cinematicLayerActive = false;
        combatTargetWeight = 0f;
        cinematicTargetWeight = 0f;
        Debug.Log("🔄 All layers reset!");
    }
    
    private void ShowControls()
    {
        Debug.Log("=== ANIMATION TESTER CONTROLS ===");
        Debug.Log("C - Toggle Combat Layer");
        Debug.Log("V - Toggle Cinematic Layer");
        Debug.Log("Hold Both Shifts - Reset all layers");
        Debug.Log("================================");
    }
    
    private void ShowCombatControls()
    {
        Debug.Log("=== COMBAT CONTROLS ===");
        Debug.Log("Q - Attack Paw");
        Debug.Log("E - Left Tail Cast");
        Debug.Log("R - Right Tail Cast");
        Debug.Log("T - Tow Paw");
        Debug.Log("=======================");
    }
    
    private void ShowCinematicControls()
    {
        Debug.Log("=== CINEMATIC CONTROLS ===");
        Debug.Log("1 - Freeing Soul");
        Debug.Log("2 - Wakeup");
        Debug.Log("3 - Circle Activation");
        Debug.Log("4 - Sleep");
        Debug.Log("5 - Scared");
        Debug.Log("6 - Absorbing");
        Debug.Log("7 - Heart");
        Debug.Log("==========================");
    }
    
    void OnGUI()
    {
        // Display current layer status
        GUI.Box(new Rect(10, 10, 200, 90), "Animation Layers");
        GUI.Label(new Rect(20, 30, 180, 20), $"Base: Always Active");
        GUI.Label(new Rect(20, 50, 180, 20), $"Combat: {(combatLayerActive ? "ON" : "OFF")} (Weight: {combatCurrentWeight:F2})");
        GUI.Label(new Rect(20, 70, 180, 20), $"Cinematic: {(cinematicLayerActive ? "ON" : "OFF")} (Weight: {cinematicCurrentWeight:F2})");
    }
}