using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class DialogueManager : MonoBehaviour
{
    [Header("UI References")]
    public GameObject dialoguePanel;
    public TMP_Text speakerNameText;
    public TMP_Text dialogueText;
    public Button lightChoiceButton;
    public Button darkChoiceButton;
    public Button neutralChoiceButton;
    
    [Header("Button Text References")]
    public TMP_Text lightChoiceText;
    public TMP_Text darkChoiceText;
    public TMP_Text neutralChoiceText;
    
    private DialogueData currentDialogue;
    private InteractableEnemy currentEnemy;
    private KarmaManager karmaManager;
    
    // Store the chosen outcome to apply after delay
    private EnemyOutcome outcomeToApply;
    private bool waitingToApplyOutcome = false;
    
    public static DialogueManager Instance;
    
    void Awake()
    {
        Instance = this;
        karmaManager = FindObjectOfType<KarmaManager>();
    }
    
    void Start()
    {
        if (dialoguePanel != null)
        {
            dialoguePanel.SetActive(false);
        }
        
        // Setup buttons
        if (lightChoiceButton != null)
        {
            lightChoiceButton.onClick.RemoveAllListeners();
            lightChoiceButton.onClick.AddListener(ClickedLight);
            Debug.Log("✅ Light button setup");
        }
        
        if (darkChoiceButton != null)
        {
            darkChoiceButton.onClick.RemoveAllListeners();
            darkChoiceButton.onClick.AddListener(ClickedDark);
            Debug.Log("✅ Dark button setup");
        }
        
        if (neutralChoiceButton != null)
        {
            neutralChoiceButton.onClick.RemoveAllListeners();
            neutralChoiceButton.onClick.AddListener(ClickedNeutral);
            Debug.Log("✅ Neutral button setup");
        }
        
        Debug.Log("✅ DialogueManager fully initialized!");
    }
    
    public void ClickedLight()
    {
        Debug.Log("🟢🟢🟢 LIGHT BUTTON CLICKED! 🟢🟢🟢");
        ProcessChoice(ChoiceType.Light);
    }
    
    public void ClickedDark()
    {
        Debug.Log("🔴🔴🔴 DARK BUTTON CLICKED! 🔴🔴🔴");
        ProcessChoice(ChoiceType.Dark);
    }
    
    public void ClickedNeutral()
    {
        Debug.Log("⚪⚪⚪ NEUTRAL BUTTON CLICKED! ⚪⚪⚪");
        ProcessChoice(ChoiceType.Neutral);
    }
    
    public void ShowDialogue(DialogueData dialogue, InteractableEnemy enemy)
    {
        Debug.Log("=== SHOWING DIALOGUE ===");
        
        if (dialogue == null)
        {
            Debug.LogError("❌ DialogueData is NULL!");
            return;
        }
        
        if (enemy == null)
        {
            Debug.LogError("❌ Enemy is NULL!");
            return;
        }
        
        currentDialogue = dialogue;
        currentEnemy = enemy;
        waitingToApplyOutcome = false;
        
        // Show panel
        dialoguePanel.SetActive(true);
        
        // Set texts
        speakerNameText.text = dialogue.enemyName;
        dialogueText.text = dialogue.initialDialogue;
        
        lightChoiceText.text = dialogue.lightChoice.choiceText;
        darkChoiceText.text = dialogue.darkChoice.choiceText;
        neutralChoiceText.text = dialogue.neutralChoice.choiceText;
        
        // Show buttons
        lightChoiceButton.gameObject.SetActive(true);
        darkChoiceButton.gameObject.SetActive(true);
        neutralChoiceButton.gameObject.SetActive(true);
        
        lightChoiceButton.interactable = true;
        darkChoiceButton.interactable = true;
        neutralChoiceButton.interactable = true;
        
        // Unlock cursor
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        
        Debug.Log($"✅ Dialogue showing: {dialogue.enemyName}");
        Debug.Log($"✅ currentDialogue set: {currentDialogue != null}");
        Debug.Log($"✅ currentEnemy set: {currentEnemy != null}");
    }
    
    private void ProcessChoice(ChoiceType type)
    {
        Debug.Log($"▶▶▶ PROCESSING CHOICE: {type} ◀◀◀");
        
        if (currentDialogue == null)
        {
            Debug.LogError("❌ currentDialogue is NULL in ProcessChoice!");
            return;
        }
        
        if (currentEnemy == null)
        {
            Debug.LogError("❌ currentEnemy is NULL in ProcessChoice!");
            return;
        }
        
        Debug.Log("✅ Both currentDialogue and currentEnemy are valid!");
        
        DialogueChoice choice = null;
        
        // Get the choice based on type
        if (type == ChoiceType.Light)
        {
            choice = currentDialogue.lightChoice;
            if (karmaManager != null)
            {
                karmaManager.AddLightKarma(currentDialogue.lightKarmaReward);
                Debug.Log($"💚 Added {currentDialogue.lightKarmaReward} light karma");
            }
        }
        else if (type == ChoiceType.Dark)
        {
            choice = currentDialogue.darkChoice;
            if (karmaManager != null)
            {
                karmaManager.AddDarkKarma(currentDialogue.darkKarmaReward);
                Debug.Log($"💔 Added {currentDialogue.darkKarmaReward} dark karma");
            }
        }
        else
        {
            choice = currentDialogue.neutralChoice;
            Debug.Log("😶 Neutral choice - no karma");
        }
        
        if (choice == null)
        {
            Debug.LogError("❌ Selected choice is NULL!");
            return;
        }
        
        Debug.Log($"✅ Choice response: {choice.response.responseText}");
        Debug.Log($"✅ Choice outcome: {choice.response.outcome}");
        
        // Hide buttons immediately
        lightChoiceButton.gameObject.SetActive(false);
        darkChoiceButton.gameObject.SetActive(false);
        neutralChoiceButton.gameObject.SetActive(false);
        
        // Show response text
        dialogueText.text = choice.response.responseText;
        speakerNameText.text = currentDialogue.enemyName + " responds:";
        
        // Store outcome to apply later
        outcomeToApply = choice.response.outcome;
        waitingToApplyOutcome = true;
        
        Debug.Log($"⏰ Waiting 2 seconds before applying outcome: {outcomeToApply}");
        
        // Wait 2 seconds then apply outcome
        Invoke("ApplyOutcomeNow", 2f);
    }
    
    private void ApplyOutcomeNow()
    {
        Debug.Log("⏰⏰⏰ 2 SECONDS PASSED - APPLYING OUTCOME NOW! ⏰⏰⏰");
        
        if (!waitingToApplyOutcome)
        {
            Debug.LogWarning("⚠️ Not waiting for outcome anymore!");
            return;
        }
        
        if (currentEnemy == null)
        {
            Debug.LogError("❌ currentEnemy is NULL when trying to apply outcome!");
            CloseDialogue();
            return;
        }
        
        EnemyCombat combat = currentEnemy.GetComponent<EnemyCombat>();
        if (combat == null)
        {
            Debug.LogError("❌ EnemyCombat component not found!");
            CloseDialogue();
            return;
        }
        
        Debug.Log($"🎭 Applying outcome: {outcomeToApply}");
        
        // Apply the outcome
        if (outcomeToApply == EnemyOutcome.BecomePeaceful)
        {
            Debug.Log("✨✨✨ CALLING BecomePeaceful() ✨✨✨");
            combat.BecomePeaceful();
        }
        else if (outcomeToApply == EnemyOutcome.BecomeHostile)
        {
            Debug.Log("⚔️⚔️⚔️ CALLING BecomeHostile() ⚔️⚔️⚔️");
            combat.BecomeHostile();
        }
        else if (outcomeToApply == EnemyOutcome.StayConfused)
        {
            Debug.Log("❓❓❓ Keeping LostSoul state ❓❓❓");
            combat.SetState(EnemyCombat.EnemyState.LostSoul);
        }
        
        Debug.Log("✅ Outcome applied successfully!");
        
        // Close dialogue
        CloseDialogue();
    }
    
    private void CloseDialogue()
    {
        Debug.Log("=== CLOSING DIALOGUE ===");
        
        dialoguePanel.SetActive(false);
        waitingToApplyOutcome = false;
        
        // Re-enable buttons for next time
        lightChoiceButton.gameObject.SetActive(true);
        darkChoiceButton.gameObject.SetActive(true);
        neutralChoiceButton.gameObject.SetActive(true);
        
        lightChoiceButton.interactable = true;
        darkChoiceButton.interactable = true;
        neutralChoiceButton.interactable = true;
        
        // Lock cursor
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        
        currentDialogue = null;
        currentEnemy = null;
        
        Debug.Log("✅ Dialogue closed, cursor locked");
    }
}