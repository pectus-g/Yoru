using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class DialogueManager : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject dialoguePanel;
    [SerializeField] private TMP_Text speakerNameText;
    [SerializeField] private TMP_Text dialogueText;
    [SerializeField] private Button lightChoiceButton;
    [SerializeField] private Button darkChoiceButton;
    [SerializeField] private Button neutralChoiceButton;
    
    [Header("Button Text References")]
    [SerializeField] private TMP_Text lightChoiceText;
    [SerializeField] private TMP_Text darkChoiceText;
    [SerializeField] private TMP_Text neutralChoiceText;
    
    // Current dialogue state
    private DialogueData currentDialogue;
    private InteractableEnemy currentEnemy;
    private KarmaManager karmaManager;
    private bool isDialogueActive = false;
    
    // Singleton pattern
    public static DialogueManager Instance { get; private set; }
    
    void Awake()
    {
        // Singleton setup
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }
        
        karmaManager = FindObjectOfType<KarmaManager>();
    }
    
    void Start()
    {
        // Hide dialogue by default
        HideDialogue();
        
        // Setup button listeners
        lightChoiceButton.onClick.AddListener(() => OnChoiceSelected(ChoiceType.Light));
        darkChoiceButton.onClick.AddListener(() => OnChoiceSelected(ChoiceType.Dark));
        neutralChoiceButton.onClick.AddListener(() => OnChoiceSelected(ChoiceType.Neutral));
    }
    
    void Update()
    {
        // Close dialogue with ESC
        if (isDialogueActive && Input.GetKeyDown(KeyCode.Escape))
        {
            HideDialogue();
        }
    }
    
    public void ShowDialogue(DialogueData dialogue, InteractableEnemy enemy)
    {
        if (dialogue == null)
        {
            Debug.LogError("DialogueData is null!");
            return;
        }
        
        currentDialogue = dialogue;
        currentEnemy = enemy;
        isDialogueActive = true;
        
        // Show panel
        dialoguePanel.SetActive(true);
        
        // Set speaker name
        speakerNameText.text = dialogue.enemyName;
        
        // Set initial dialogue text
        dialogueText.text = dialogue.initialDialogue;
        
        // Set choice button texts
        lightChoiceText.text = dialogue.lightChoice.choiceText;
        darkChoiceText.text = dialogue.darkChoice.choiceText;
        neutralChoiceText.text = dialogue.neutralChoice.choiceText;
        
        // Pause game
        Time.timeScale = 0f;
        
        // Lock player movement
        LockPlayerInput(true);
        
        Debug.Log($"Dialogue started with {dialogue.enemyName}");
    }
    
    public void HideDialogue()
    {
        dialoguePanel.SetActive(false);
        isDialogueActive = false;
        
        // Unpause game
        Time.timeScale = 1f;
        
        // Unlock player
        LockPlayerInput(false);
        
        currentDialogue = null;
        currentEnemy = null;
        
        Debug.Log("Dialogue closed");
    }
    
    private void OnChoiceSelected(ChoiceType choiceType)
    {
        if (currentDialogue == null || currentEnemy == null)
        {
            Debug.LogError("No active dialogue!");
            return;
        }
        
        DialogueChoice selectedChoice = null;
        
        // Get the selected choice
        switch (choiceType)
        {
            case ChoiceType.Light:
                selectedChoice = currentDialogue.lightChoice;
                // Add light karma
                if (karmaManager != null)
                {
                    karmaManager.AddLightKarma(currentDialogue.lightKarmaReward);
                }
                Debug.Log("💚 Player chose LIGHT path - Empathy");
                break;
                
            case ChoiceType.Dark:
                selectedChoice = currentDialogue.darkChoice;
                // Add dark karma
                if (karmaManager != null)
                {
                    karmaManager.AddDarkKarma(currentDialogue.darkKarmaReward);
                }
                Debug.Log("💔 Player chose DARK path - Confrontation");
                break;
                
            case ChoiceType.Neutral:
                selectedChoice = currentDialogue.neutralChoice;
                Debug.Log("😶 Player chose NEUTRAL path - Silence");
                break;
        }
        
        if (selectedChoice != null)
        {
            // Show response (for now, just log it)
            Debug.Log($"Enemy responds: {selectedChoice.response.responseText}");
            
            // Apply outcome
            ApplyOutcome(selectedChoice.response.outcome);
        }
        
        // Close dialogue
        HideDialogue();
    }
    
    private void ApplyOutcome(EnemyOutcome outcome)
    {
        if (currentEnemy == null) return;
        
        EnemyCombat combat = currentEnemy.GetComponent<EnemyCombat>();
        if (combat == null) return;
        
        switch (outcome)
        {
            case EnemyOutcome.BecomePeaceful:
                Debug.Log("✨ Soul finds peace and passes on...");
                combat.BecomePeaceful();
                break;
                
            case EnemyOutcome.BecomeHostile:
                Debug.Log("⚔️ Soul becomes enraged and attacks!");
                combat.BecomeHostile();
                break;
                
            case EnemyOutcome.StayConfused:
                Debug.Log("❓ Soul remains confused and wanders...");
                combat.SetState(EnemyCombat.EnemyState.LostSoul);
                break;
                
            case EnemyOutcome.AskMoreQuestions:
                Debug.Log("💬 Conversation continues... (not implemented yet)");
                break;
        }
    }
    
    private void LockPlayerInput(bool locked)
    {
        // Find player controller and disable/enable it
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            // Disable movement scripts during dialogue
            var playerController = player.GetComponent<MonoBehaviour>();
            // We'll implement this better later
        }
    }
    
    public bool IsDialogueActive() => isDialogueActive;
}