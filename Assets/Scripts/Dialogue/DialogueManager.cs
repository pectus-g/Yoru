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
    private EnemyOutcome outcomeToApply;
    private bool isDialogueActive = false;  // NEW FLAG
    
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
        
        if (lightChoiceButton != null)
        {
            lightChoiceButton.onClick.RemoveAllListeners();
            lightChoiceButton.onClick.AddListener(ClickedLight);
        }
        
        if (darkChoiceButton != null)
        {
            darkChoiceButton.onClick.RemoveAllListeners();
            darkChoiceButton.onClick.AddListener(ClickedDark);
        }
        
        if (neutralChoiceButton != null)
        {
            neutralChoiceButton.onClick.RemoveAllListeners();
            neutralChoiceButton.onClick.AddListener(ClickedNeutral);
        }
        
        Debug.Log("✅ DialogueManager initialized!");
    }
    
    void Update()
    {
        // FORCE CURSOR VISIBLE WHILE DIALOGUE IS ACTIVE
        if (isDialogueActive)
        {
            if (!Cursor.visible || Cursor.lockState != CursorLockMode.None)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }
    }
    
    public void ClickedLight()
    {
        Debug.Log("🟢 LIGHT CLICKED!");
        ProcessChoice(ChoiceType.Light);
    }
    
    public void ClickedDark()
    {
        Debug.Log("🔴 DARK CLICKED!");
        ProcessChoice(ChoiceType.Dark);
    }
    
    public void ClickedNeutral()
    {
        Debug.Log("⚪ NEUTRAL CLICKED!");
        ProcessChoice(ChoiceType.Neutral);
    }
    
    public void ShowDialogue(DialogueData dialogue, InteractableEnemy enemy)
    {
        if (dialogue == null || enemy == null) return;
        
        currentDialogue = dialogue;
        currentEnemy = enemy;
        isDialogueActive = true;  // SET FLAG
        
        dialoguePanel.SetActive(true);
        
        speakerNameText.text = dialogue.enemyName;
        dialogueText.text = dialogue.initialDialogue;
        
        lightChoiceText.text = dialogue.lightChoice.choiceText;
        darkChoiceText.text = dialogue.darkChoice.choiceText;
        neutralChoiceText.text = dialogue.neutralChoice.choiceText;
        
        lightChoiceButton.gameObject.SetActive(true);
        darkChoiceButton.gameObject.SetActive(true);
        neutralChoiceButton.gameObject.SetActive(true);
        
        lightChoiceButton.interactable = true;
        darkChoiceButton.interactable = true;
        neutralChoiceButton.interactable = true;
        
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        
        Debug.Log("✅ Dialogue opened!");
    }
    
    private void ProcessChoice(ChoiceType type)
    {
        if (currentDialogue == null || currentEnemy == null) return;
        
        DialogueChoice choice = null;
        
        if (type == ChoiceType.Light)
        {
            choice = currentDialogue.lightChoice;
            if (karmaManager != null)
                karmaManager.AddLightKarma(currentDialogue.lightKarmaReward);
        }
        else if (type == ChoiceType.Dark)
        {
            choice = currentDialogue.darkChoice;
            if (karmaManager != null)
                karmaManager.AddDarkKarma(currentDialogue.darkKarmaReward);
        }
        else
        {
            choice = currentDialogue.neutralChoice;
        }
        
        if (choice == null) return;
        
        // Hide buttons
        lightChoiceButton.gameObject.SetActive(false);
        darkChoiceButton.gameObject.SetActive(false);
        neutralChoiceButton.gameObject.SetActive(false);
        
        // Show response
        dialogueText.text = choice.response.responseText;
        speakerNameText.text = currentDialogue.enemyName + " responds:";
        
        // Store outcome
        outcomeToApply = choice.response.outcome;
        
        // CURSOR STAYS VISIBLE - Update() will enforce this
        
        Debug.Log("Waiting 2 seconds...");
        Invoke("ApplyOutcomeNow", 2f);
    }
    
    private void ApplyOutcomeNow()
    {
        Debug.Log("Applying outcome!");
        
        if (currentEnemy == null)
        {
            CloseDialogue();
            return;
        }
        
        EnemyCombat combat = currentEnemy.GetComponent<EnemyCombat>();
        if (combat == null)
        {
            CloseDialogue();
            return;
        }
        
        Debug.Log($"Outcome: {outcomeToApply}");
        
        if (outcomeToApply == EnemyOutcome.BecomePeaceful)
        {
            Debug.Log("✨ Peaceful");
            combat.BecomePeaceful();
        }
        else if (outcomeToApply == EnemyOutcome.BecomeHostile)
        {
            Debug.Log("⚔️ Hostile");
            combat.BecomeHostile();
        }
        else
        {
            combat.SetState(EnemyCombat.EnemyState.LostSoul);
        }
        
        Debug.Log($"New state: {combat.GetCurrentState()}");
        
        CloseDialogue();
    }
    
    private void CloseDialogue()
    {
        dialoguePanel.SetActive(false);
        isDialogueActive = false;  // CLEAR FLAG
        
        // Re-enable buttons
        lightChoiceButton.gameObject.SetActive(true);
        darkChoiceButton.gameObject.SetActive(true);
        neutralChoiceButton.gameObject.SetActive(true);
        
        lightChoiceButton.interactable = true;
        darkChoiceButton.interactable = true;
        neutralChoiceButton.interactable = true;
        
        // NOW lock cursor
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        
        currentDialogue = null;
        currentEnemy = null;
        
        Debug.Log("✅ Dialogue closed!");
    }
}