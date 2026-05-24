using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Singleton manager for Tier 4 persuasion dialogue per GDD Doc 09 §4a.
///
/// Flow:
///   1. InteractableEnemy.StartDialogue() → ShowDialogue(data, enemy)
///   2. Panel opens, first N buttons populated from data.options (2-3)
///   3. Player clicks a button → OnChoiceClicked(index)
///   4. Response text shown for responseDisplayDuration seconds
///   5. ResolveOutcome — isCorrect → +1 right ring + BecomePeaceful; else BecomeHostile
///   6. CloseDialogue
///
/// Inspector wiring:
///   - dialoguePanel: parent UI GameObject (shown/hidden as a block)
///   - speakerNameText, dialogueText: panel labels
///   - choiceButtons + choiceTexts: parallel lists — button[i] paired with text[i]
///     Current scene has 3 buttons; options beyond 3 are clamped silently.
/// </summary>
public class DialogueManager : MonoBehaviour
{
    #region Inspector
    [Header("UI References")]
    [SerializeField] private GameObject dialoguePanel;
    [SerializeField] private TMP_Text speakerNameText;
    [SerializeField] private TMP_Text dialogueText;

    [Header("Choice Buttons (assign in order)")]
    [Tooltip("Drag the 3 choice buttons in the order they should fill (top to bottom or however the UI is laid out)")]
    [SerializeField] private List<Button> choiceButtons = new List<Button>();
    [Tooltip("Drag the matching button label TMP_Texts in the same order as choiceButtons")]
    [SerializeField] private List<TMP_Text> choiceTexts = new List<TMP_Text>();

    [Header("Timing")]
    [Tooltip("Seconds to display the enemy's response text before resolving outcome and closing the panel")]
    [SerializeField] private float responseDisplayDuration = 2f;
    #endregion

    #region State
    public static DialogueManager Instance { get; private set; }

    private DialogueData currentDialogue;
    private InteractableEnemy currentEnemy;
    private DialogueOption pendingOption;
    private bool isDialogueActive;
    #endregion

    #region Lifecycle
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        if (dialoguePanel != null)
            dialoguePanel.SetActive(false);

        // Wire each button to send its index to the click handler.
        // Local copy of i avoids the closure-over-loop-variable pitfall.
        for (int i = 0; i < choiceButtons.Count; i++)
        {
            if (choiceButtons[i] == null) continue;
            int capturedIndex = i;
            choiceButtons[i].onClick.RemoveAllListeners();
            choiceButtons[i].onClick.AddListener(() => OnChoiceClicked(capturedIndex));
        }
    }

    private void Update()
    {
        // Force cursor visible while dialogue is open so the player can click choices.
        // ThirdPersonCamera locks cursor during normal gameplay; CloseDialogue restores that.
        if (isDialogueActive && (!Cursor.visible || Cursor.lockState != CursorLockMode.None))
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
    #endregion

    #region Public API
    /// <summary>
    /// Open dialogue panel for the given enemy/data pair. Called by InteractableEnemy on click.
    /// </summary>
    public void ShowDialogue(DialogueData dialogue, InteractableEnemy enemy)
    {
        if (dialogue == null || enemy == null) return;

        currentDialogue = dialogue;
        currentEnemy = enemy;
        isDialogueActive = true;

        dialoguePanel.SetActive(true);
        speakerNameText.text = dialogue.enemyDisplayName;
        dialogueText.text = dialogue.initialDialogue;

        // Show only the buttons we have options for. Hide the rest.
        int optionCount = Mathf.Min(dialogue.options.Count, choiceButtons.Count);
        for (int i = 0; i < choiceButtons.Count; i++)
        {
            bool used = i < optionCount;
            if (choiceButtons[i] != null)
            {
                choiceButtons[i].gameObject.SetActive(used);
                choiceButtons[i].interactable = used;
            }
            if (used && i < choiceTexts.Count && choiceTexts[i] != null)
                choiceTexts[i].text = dialogue.options[i].choiceText;
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public bool IsDialogueActive => isDialogueActive;
    #endregion

    #region Choice Handling
    private void OnChoiceClicked(int index)
    {
        if (currentDialogue == null || currentEnemy == null) return;
        if (index < 0 || index >= currentDialogue.options.Count) return;

        pendingOption = currentDialogue.options[index];

        // Hide all buttons during the response display so player can't double-click.
        for (int i = 0; i < choiceButtons.Count; i++)
        {
            if (choiceButtons[i] != null)
                choiceButtons[i].gameObject.SetActive(false);
        }

        // Show the enemy's response text.
        dialogueText.text = pendingOption.responseText;
        speakerNameText.text = currentDialogue.enemyDisplayName + " responds:";

        // Resolve outcome after a short pause so the player can read.
        Invoke(nameof(ResolveOutcome), responseDisplayDuration);
    }

    private void ResolveOutcome()
    {
        if (currentEnemy == null || pendingOption == null)
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

        if (pendingOption.isCorrect)
        {
            // Correct response — soul accepts acknowledgement, finds peace.
            // Per GDD Doc 09 §6 and §8c, persuasion success grants a right ring (light tail).
            // AddRing(false) → false = right tail per WorldStateManager signature.
            if (WorldStateManager.Instance != null)
                WorldStateManager.Instance.AddRing(false);
            combat.BecomePeaceful();
        }
        else
        {
            // Wrong response — soul rejects acknowledgement, becomes hostile.
            // Per universal Tomoe-ignore rule (Phase 3 Option B), the enemy still won't
            // attack Tomoe directly; player must transform back to Yoru to engage.
            combat.BecomeHostile();
        }

        CloseDialogue();
    }
    #endregion

    #region Close
    private void CloseDialogue()
    {
        dialoguePanel.SetActive(false);
        isDialogueActive = false;
        pendingOption = null;

        // Re-enable buttons for next dialogue (visibility is managed per-show in ShowDialogue).
        for (int i = 0; i < choiceButtons.Count; i++)
        {
            if (choiceButtons[i] != null)
                choiceButtons[i].interactable = true;
        }

        // Return cursor to gameplay-locked state. ThirdPersonCamera takes over from here.
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        currentDialogue = null;
        currentEnemy = null;
    }
    #endregion
}