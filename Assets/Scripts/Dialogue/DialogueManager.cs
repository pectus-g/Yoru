using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Dialogue System v2 singleton.
///
/// Replaces the v1 single-shot persuasion (correct = peace + ring, wrong = hostile) with a
/// branched multi-beat conversation. Souls are never hostile and never pass on from
/// dialogue. The conversation's only job is to reach FINAL_SUCCESS and fire the dialogue's
/// end behavior (quest hand-over or mini-game). The soul stays alive afterward.
///
/// Flow:
///   1. InteractableEnemy click -> ShowDialogue(data, enemy)
///   2. Beats display one at a time, routed by the picked option's branchType:
///        CORRECT / SOFT_WRONG -> advance to nextBeatId
///        HARD_WRONG           -> goodbye line, conversation ends, strike added
///        FINAL_SUCCESS        -> end behavior fires, soul enters permanent recap mode
///   3. 3 strikes -> soul wanders to its wander point and a cooldown starts.
///      Cooldown expiry -> soul walks home, strikes reset, interaction restored.
///   4. After the quest is given, re-approaching opens a one-beat recap instead:
///      a waiting line, a leave option, and a repeat-the-quest option. The strike and
///      cooldown machinery is permanently retired for that soul.
///
/// Per-soul runtime state (strikes, cooldown, quest-given) is keyed by
/// DialogueData.dialogueId and survives soul object disable/enable.
///
/// This manager never calls BecomePeaceful or BecomeHostile. The only combat state it
/// touches is restoring LostSoul on conversation end so the soul stays talkable.
///
/// Inspector wiring (unchanged from v1, scene component survives):
///   - dialoguePanel: parent UI GameObject (shown/hidden as a block)
///   - speakerNameText, dialogueText: panel labels
///   - choiceButtons + choiceTexts: parallel lists, button[i] paired with text[i]
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
    [Tooltip("Seconds a goodbye line or quest recap stays on screen before the panel closes")]
    [SerializeField] private float responseDisplayDuration = 2f;

    [Header("Cancel")]
    [Tooltip("Extra metres beyond the enemy's interactionRange before walking away cancels the conversation (no strike)")]
    [SerializeField] private float rangeCancelBuffer = 1.5f;

    [Header("Choice Hover (v2 styled buttons only)")]
    [Tooltip("Resting transparency of a choice button's glass fill")]
    [SerializeField] private float fillNormalAlpha = 0.22f;
    [Tooltip("Fill transparency while hovered: near-opaque so the background becomes visible")]
    [SerializeField] private float fillHoverAlpha = 0.9f;
    [Tooltip("Resting brightness of the neon outline")]
    [SerializeField] private float frameNormalAlpha = 0.95f;
    [Tooltip("Outline brightness while hovered: the light dims as the glass fills")]
    [SerializeField] private float frameHoverAlpha = 0.3f;
    [Tooltip("Higher = snappier fade. 12 is a quick, soft response")]
    [SerializeField] private float hoverFadeSpeed = 12f;
    #endregion

    #region Per-Soul State
    /// <summary>
    /// Runtime state for one soul, keyed by DialogueData.dialogueId.
    /// Lives on the manager so it survives soul object disable/enable.
    /// </summary>
    private class SoulState
    {
        public int strikeCount;
        public float cooldownEndTime;      // 0 = no cooldown; compared against Time.time
        public bool hasGivenQuest;
        public InteractableEnemy enemy;    // last enemy seen for this id; used for the walk home
    }

    private readonly Dictionary<string, SoulState> soulStates = new Dictionary<string, SoulState>();

    private SoulState GetOrCreateState(string dialogueId)
    {
        if (!soulStates.TryGetValue(dialogueId, out SoulState state))
        {
            state = new SoulState();
            soulStates[dialogueId] = state;
        }
        return state;
    }
    #endregion

    #region State
    public static DialogueManager Instance { get; private set; }

    private DialogueData currentDialogue;
    private InteractableEnemy currentEnemy;
    private Image[] choiceFills;
    private Image[] choiceFrames;
    private DialogueBeat currentBeat;
    private bool isDialogueActive;
    private bool isRecapMode;
    private bool isTurnInMode;
    private QuestData turnInQuest;
    private GameObject pendingDisappear;
    private Transform player;

    // Movement lock for the duration of a conversation, same pattern as YoruSleepIntro:
    // disable the PlayerMovement component, remember it ONLY if we disabled it, restore
    // on close. Never re-enables a script some other system (sleep intro) turned off.
    private PlayerMovement lockedMovement;

    private const int MaxStrikes = 3;

    /// <summary>
    /// Fired when any conversation reaches FINAL_SUCCESS, after its end behavior ran.
    /// Scripted sequences (the mujina cave reveal) listen here; endBehavior NONE
    /// conversations exist exactly for this hook.
    /// </summary>
    public static event System.Action<DialogueData> OnFinalSuccess;
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

        player = GameObject.FindGameObjectWithTag("Player")?.transform;

        // Wire each button to send its index to the click handler.
        // Local copy of i avoids the closure-over-loop-variable pitfall.
        for (int i = 0; i < choiceButtons.Count; i++)
        {
            if (choiceButtons[i] == null) continue;
            int capturedIndex = i;
            choiceButtons[i].onClick.RemoveAllListeners();
            choiceButtons[i].onClick.AddListener(() => OnChoiceClicked(capturedIndex));
        }

        CacheChoiceVisuals();
    }

    /// <summary>
    /// Cache the glass fill and neon frame of each v2-styled choice button. A button
    /// without a child named "Frame" is not v2-styled and is left untouched by the
    /// hover effect, so legacy or hand-built buttons keep their own look.
    /// </summary>
    private void CacheChoiceVisuals()
    {
        choiceFills = new Image[choiceButtons.Count];
        choiceFrames = new Image[choiceButtons.Count];
        for (int i = 0; i < choiceButtons.Count; i++)
        {
            if (choiceButtons[i] == null) continue;
            Transform frame = choiceButtons[i].transform.Find("Frame");
            if (frame == null) continue;
            choiceFrames[i] = frame.GetComponent<Image>();
            choiceFills[i] = choiceButtons[i].GetComponent<Image>();
        }
    }

    private void Update()
    {
        TickCooldowns();

        if (!isDialogueActive) return;

        UpdateChoiceHover();

        // Force cursor visible while dialogue is open so the player can click choices.
        // ThirdPersonCamera locks cursor during normal gameplay; CloseDialogue restores that.
        if (!Cursor.visible || Cursor.lockState != CursorLockMode.None)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        // Cancel paths: Esc, or walking out of range. No strike, no penalty.
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            CancelDialogue("Esc pressed");
            return;
        }

        if (player != null && currentEnemy != null)
        {
            float maxDist = currentEnemy.interactionRange + rangeCancelBuffer;
            if (Vector3.Distance(player.position, currentEnemy.transform.position) > maxDist)
            {
                CancelDialogue("player left range");
            }
        }
    }

    /// <summary>
    /// Walks every soul on cooldown. When a cooldown expires, the soul walks home,
    /// strikes reset, and (on arrival) interaction is restored by InteractableEnemy.
    /// Touching souls the manager is not actively talking to is intended and cheap.
    /// </summary>
    private void TickCooldowns()
    {
        foreach (KeyValuePair<string, SoulState> pair in soulStates)
        {
            SoulState state = pair.Value;
            if (state.cooldownEndTime > 0f && Time.time >= state.cooldownEndTime)
            {
                state.cooldownEndTime = 0f;
                state.strikeCount = 0;
                Debug.Log($"[Dialogue] Cooldown complete for '{pair.Key}'. strikeCount = 0");

                if (state.enemy != null)
                    state.enemy.ReturnFromWander();
            }
        }
    }
    #endregion

    #region Public API
    /// <summary>
    /// Open the conversation for the given enemy/data pair. Called by InteractableEnemy on click.
    /// Routes to the post-quest recap when the soul has already given its quest.
    /// </summary>
    public void ShowDialogue(DialogueData dialogue, InteractableEnemy enemy)
    {
        if (dialogue == null || enemy == null) return;

        if (string.IsNullOrEmpty(dialogue.dialogueId))
        {
            Debug.LogError($"[Dialogue] '{dialogue.name}' has an empty dialogueId. Aborting.");
            RestoreLostSoulOn(enemy);
            return;
        }

        SoulState state = GetOrCreateState(dialogue.dialogueId);
        state.enemy = enemy;

        // First contact (or refresh) on the Memory Parchments. Idempotent.
        SoulJournal.RegisterMet(dialogue);

        // Cooldown re-entry is suppressed at the InteractableEnemy level; this is a belt-and-braces guard.
        // The early-outs restore LostSoul because InteractableEnemy set Dialogue state before calling here.
        if (state.cooldownEndTime > 0f && Time.time < state.cooldownEndTime)
        {
            RestoreLostSoulOn(enemy);
            return;
        }

        currentDialogue = dialogue;
        currentEnemy = enemy;
        isDialogueActive = true;

        dialoguePanel.SetActive(true);
        LockPlayerMovement();
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // One-time mistaken identity interstitial: the soul aggroed on Yoru, no fight
        // started, and the player came back as Tomoe. The soul excuses itself first,
        // then the normal conversation (or recap) opens.
        bool mistaken = enemy.ConsumeMistakenIdentity();
        if (mistaken && !string.IsNullOrEmpty(dialogue.mistakenIdentityLine))
        {
            isRecapMode = false;
            currentBeat = null;
            speakerNameText.text = dialogue.soulName;
            dialogueText.text = dialogue.mistakenIdentityLine;
            HideAllButtons();
            Debug.Log($"[Dialogue] Mistaken identity line for '{dialogue.dialogueId}'");
            Invoke(nameof(OpenConversation), responseDisplayDuration);
        }
        else
        {
            OpenConversation();
        }
    }

    /// <summary>
    /// Open the conversation body: the recap beat when the quest has been given,
    /// otherwise the start beat of the full conversation.
    /// </summary>
    private void OpenConversation()
    {
        if (currentDialogue == null)
        {
            CloseDialogue();
            return;
        }

        SoulState state = GetOrCreateState(currentDialogue.dialogueId);

        // Quest finished and waiting on this soul: the turn-in beat outranks the recap.
        if (QuestManager.Instance != null && QuestManager.Instance.IsReadyToTurnIn(currentDialogue.dialogueId))
        {
            ShowTurnInBeat();
        }
        else if (state.hasGivenQuest)
        {
            ShowRecapBeat();
        }
        else
        {
            DialogueBeat startBeat = currentDialogue.GetBeat(currentDialogue.startBeatId);
            if (startBeat == null)
            {
                Debug.LogError($"[Dialogue] Start beat '{currentDialogue.startBeatId}' not found in '{currentDialogue.dialogueId}'. Aborting.");
                EndConversationNoStrike();
                return;
            }
            Debug.Log($"[Dialogue] Opened '{currentDialogue.dialogueId}' at beat {startBeat.beatId}");
            DisplayBeat(startBeat);
        }
    }

    public bool IsDialogueActive => isDialogueActive;
    #endregion

    #region Beat Display
    /// <summary>
    /// Render one beat: soul line, options on buttons, optional VO and animation trigger.
    /// </summary>
    private void DisplayBeat(DialogueBeat beat)
    {
        currentBeat = beat;
        isRecapMode = false;
        ResetChoiceVisuals();

        speakerNameText.text = currentDialogue.soulName;
        dialogueText.text = beat.soulLine;

        int optionCount = Mathf.Min(beat.options.Count, choiceButtons.Count);
        for (int i = 0; i < choiceButtons.Count; i++)
        {
            bool used = i < optionCount;
            if (choiceButtons[i] != null)
            {
                choiceButtons[i].gameObject.SetActive(used);
                choiceButtons[i].interactable = used;
            }
            if (used && i < choiceTexts.Count && choiceTexts[i] != null)
                choiceTexts[i].text = beat.options[i].text;
        }

        PlayBeatPresentation(beat);
    }

    /// <summary>
    /// Optional VO clip and Animator trigger for a beat. cameraFramingId is reserved for
    /// the future camera framing system and is ignored at runtime for now.
    /// </summary>
    private void PlayBeatPresentation(DialogueBeat beat)
    {
        if (currentEnemy == null) return;

        if (beat.soulAudioClip != null)
            AudioSource.PlayClipAtPoint(beat.soulAudioClip, currentEnemy.transform.position);

        if (!string.IsNullOrEmpty(beat.soulAnimationTrigger))
        {
            EnemyCombat combat = currentEnemy.GetComponent<EnemyCombat>();
            Animator animator = combat != null ? combat.GetAnimator() : null;
            if (animator != null)
                animator.SetTrigger(Animator.StringToHash(beat.soulAnimationTrigger));
        }
    }

    /// <summary>
    /// Neon hover for v2-styled choice buttons: while the pointer is over a button its
    /// glass fill rises to near-opaque and its outline light dims, both gliding back on
    /// exit. Two opposing fades, which is why Unity's single-target ColorTint is not used.
    /// </summary>
    private void UpdateChoiceHover()
    {
        if (choiceFills == null) return;

        float t = 1f - Mathf.Exp(-hoverFadeSpeed * Time.unscaledDeltaTime);
        for (int i = 0; i < choiceButtons.Count; i++)
        {
            if (choiceFills[i] == null || choiceFrames[i] == null) continue;
            Button button = choiceButtons[i];
            if (button == null || !button.gameObject.activeSelf) continue;

            // Overlay canvas: no camera needed for the containment test.
            bool hovered = RectTransformUtility.RectangleContainsScreenPoint(
                (RectTransform)button.transform, Input.mousePosition, null);

            LerpImageAlpha(choiceFills[i], hovered ? fillHoverAlpha : fillNormalAlpha, t);
            LerpImageAlpha(choiceFrames[i], hovered ? frameHoverAlpha : frameNormalAlpha, t);
        }
    }

    /// <summary>
    /// Snap every styled button back to its resting look. Called whenever choices are
    /// (re)shown so a button never opens mid-hover from the previous beat.
    /// </summary>
    private void ResetChoiceVisuals()
    {
        if (choiceFills == null) return;
        for (int i = 0; i < choiceFills.Length; i++)
        {
            SetImageAlpha(choiceFills[i], fillNormalAlpha);
            SetImageAlpha(choiceFrames[i], frameNormalAlpha);
        }
    }

    private static void LerpImageAlpha(Image image, float target, float t)
    {
        if (image == null) return;
        Color c = image.color;
        c.a = Mathf.Lerp(c.a, target, t);
        image.color = c;
    }

    private static void SetImageAlpha(Image image, float alpha)
    {
        if (image == null) return;
        Color c = image.color;
        c.a = alpha;
        image.color = c;
    }

    /// <summary>
    /// The one-beat post-quest exchange: a waiting line, a leave option, and a
    /// repeat-the-quest option. Re-approachable indefinitely. No strikes here.
    /// </summary>
    private void ShowRecapBeat()
    {
        currentBeat = null;
        isRecapMode = true;
        ResetChoiceVisuals();

        Debug.Log($"[Dialogue] Recap opened for '{currentDialogue.dialogueId}'");

        speakerNameText.text = currentDialogue.soulName;
        dialogueText.text = currentDialogue.postQuestWaitingLine;

        // Button 0 = leave, button 1 = ask again. Hide the rest.
        for (int i = 0; i < choiceButtons.Count; i++)
        {
            bool used = i < 2;
            if (choiceButtons[i] != null)
            {
                choiceButtons[i].gameObject.SetActive(used);
                choiceButtons[i].interactable = used;
            }
        }
        if (choiceTexts.Count > 0 && choiceTexts[0] != null)
            choiceTexts[0].text = currentDialogue.postQuestLeaveText;
        if (choiceTexts.Count > 1 && choiceTexts[1] != null)
            choiceTexts[1].text = currentDialogue.postQuestAskAgainText;
    }

    /// <summary>
    /// The turn-in beat for a RETURN_TO_GIVER quest: the soul acknowledges the finished
    /// business (turnInLine), one button (turnInButtonText), then the farewell line and
    /// completion. No strikes here; cancelling just postpones the turn-in.
    /// </summary>
    private void ShowTurnInBeat()
    {
        currentBeat = null;
        isRecapMode = false;
        isTurnInMode = true;
        turnInQuest = QuestManager.Instance.GetTurnInQuest(currentDialogue.dialogueId);
        ResetChoiceVisuals();

        Debug.Log($"[Dialogue] Turn-in opened for '{currentDialogue.dialogueId}'");

        speakerNameText.text = currentDialogue.soulName;
        dialogueText.text = turnInQuest != null && !string.IsNullOrEmpty(turnInQuest.turnInLine)
            ? turnInQuest.turnInLine
            : "...";

        // One button only.
        for (int i = 0; i < choiceButtons.Count; i++)
        {
            bool used = i == 0;
            if (choiceButtons[i] != null)
            {
                choiceButtons[i].gameObject.SetActive(used);
                choiceButtons[i].interactable = used;
            }
        }
        if (choiceTexts.Count > 0 && choiceTexts[0] != null)
            choiceTexts[0].text = turnInQuest != null && !string.IsNullOrEmpty(turnInQuest.turnInButtonText)
                ? turnInQuest.turnInButtonText
                : "...";
    }
    #endregion

    #region Choice Handling
    private void OnChoiceClicked(int index)
    {
        if (!isDialogueActive || currentDialogue == null || currentEnemy == null) return;

        if (isTurnInMode)
        {
            HandleTurnInChoice();
            return;
        }

        if (isRecapMode)
        {
            HandleRecapChoice(index);
            return;
        }

        if (currentBeat == null || index < 0 || index >= currentBeat.options.Count) return;

        DialogueOption option = currentBeat.options[index];

        switch (option.branchType)
        {
            case DialogueBranchType.CORRECT:
            case DialogueBranchType.SOFT_WRONG:
                AdvanceToBeat(option);
                break;

            case DialogueBranchType.HARD_WRONG:
                ShowGoodbye(option);
                break;

            case DialogueBranchType.FINAL_SUCCESS:
                ResolveFinalSuccess();
                break;
        }
    }

    private void AdvanceToBeat(DialogueOption option)
    {
        DialogueBeat next = currentDialogue.GetBeat(option.nextBeatId);
        if (next == null)
        {
            Debug.LogError($"[Dialogue] nextBeatId '{option.nextBeatId}' not found in '{currentDialogue.dialogueId}'. Ending without strike.");
            EndConversationNoStrike();
            return;
        }

        Debug.Log($"[Dialogue] Beat {currentBeat.beatId} -> {next.beatId} ({option.branchType})");
        DisplayBeat(next);
    }

    /// <summary>
    /// HARD_WRONG: show the resolved goodbye line and apply the strike immediately
    /// (so cancelling during the goodbye cannot dodge it), then close after a pause.
    /// Goodbye resolution: option-specific, then beat fallback, then soul default.
    /// </summary>
    private void ShowGoodbye(DialogueOption option)
    {
        string line = !string.IsNullOrEmpty(option.optionGoodbyeLine) ? option.optionGoodbyeLine
                    : !string.IsNullOrEmpty(currentBeat.beatGoodbyeLine) ? currentBeat.beatGoodbyeLine
                    : !string.IsNullOrEmpty(currentDialogue.soulDefaultGoodbye) ? currentDialogue.soulDefaultGoodbye
                    : "...";

        HideAllButtons();
        dialogueText.text = line;

        ApplyStrike();

        Invoke(nameof(EndConversationNoStrike), responseDisplayDuration);
    }

    private void ApplyStrike()
    {
        SoulState state = GetOrCreateState(currentDialogue.dialogueId);
        state.strikeCount++;
        Debug.Log($"[Dialogue] HARD_WRONG at beat {(currentBeat != null ? currentBeat.beatId : "?")}. strikeCount = {state.strikeCount}");

        if (state.strikeCount >= MaxStrikes)
        {
            state.cooldownEndTime = Time.time + currentDialogue.cooldownSeconds;
            Debug.Log($"[Dialogue] strikeCount = {state.strikeCount}. '{currentDialogue.dialogueId}' begins wander + cooldown ({currentDialogue.cooldownSeconds}s)");

            // BeginWander restores LostSoul underneath, suppresses interaction, and
            // drives the NavMeshAgent to the wander point. It starts now, while the
            // goodbye line is still on screen: the soul turning away mid-line is
            // intended flavor.
            currentEnemy.BeginWander();
        }
    }

    /// <summary>
    /// FINAL_SUCCESS: fire the end behavior, retire the strike machinery for this soul,
    /// and leave it alive and talkable so the recap path works. Never destroys the soul,
    /// never calls BecomePeaceful.
    /// </summary>
    private void ResolveFinalSuccess()
    {
        SoulState state = GetOrCreateState(currentDialogue.dialogueId);
        state.hasGivenQuest = true;
        state.strikeCount = 0;
        state.cooldownEndTime = 0f;

        if (currentDialogue.endBehavior == DialogueEndBehavior.GIVE_QUEST)
        {
            if (QuestManager.Instance != null)
                QuestManager.Instance.GiveQuest(currentDialogue.questToGive);
            else
                Debug.Log($"[STUB] Quest given: {(currentDialogue.questToGive != null ? currentDialogue.questToGive.displayName : "null")} (no QuestManager in scene)");
        }
        else if (currentDialogue.endBehavior == DialogueEndBehavior.TRIGGER_MINIGAME)
        {
            if (MiniGameManager.Instance != null)
                MiniGameManager.Instance.TriggerMinigame(currentDialogue.miniGameToTrigger);
            else
                Debug.Log($"[STUB] Minigame triggered: {(currentDialogue.miniGameToTrigger != null ? currentDialogue.miniGameToTrigger.displayName : "null")} (no MiniGameManager in scene)");
        }
        // DialogueEndBehavior.NONE: scripted conversation, listeners act on the event below.

        OnFinalSuccess?.Invoke(currentDialogue);

        EndConversationNoStrike();
    }

    /// <summary>
    /// Turn-in click: complete the quest through QuestManager (rewards, parchment
    /// stamp), show the farewell line, then close. The soul passing on (deactivation)
    /// honours QuestData.giverDisappearsOnCompletion; a dissolve VFX is a later pass.
    /// </summary>
    private void HandleTurnInChoice()
    {
        QuestData quest = turnInQuest;
        InteractableEnemy giver = currentEnemy;

        if (QuestManager.Instance != null)
            QuestManager.Instance.CompleteTurnIn(currentDialogue.dialogueId);

        HideAllButtons();
        dialogueText.text = quest != null && !string.IsNullOrEmpty(quest.farewellLine)
            ? quest.farewellLine
            : "...";

        bool disappear = quest != null && quest.giverDisappearsOnCompletion;
        pendingDisappear = disappear && giver != null ? giver.gameObject : null;

        Invoke(nameof(FinishTurnIn), responseDisplayDuration);
    }

    private void FinishTurnIn()
    {
        GameObject soul = pendingDisappear;
        pendingDisappear = null;

        EndConversationNoStrike();

        if (soul != null)
        {
            Debug.Log($"[Dialogue] {soul.name} passes on.");
            soul.SetActive(false);
        }
    }

    private void HandleRecapChoice(int index)
    {
        if (index == 0)
        {
            // Leave. No strike, no state change.
            EndConversationNoStrike();
        }
        else if (index == 1)
        {
            // Repeat the quest: the soul re-reads the quest description, then the panel closes.
            string recap = currentDialogue.questToGive != null && !string.IsNullOrEmpty(currentDialogue.questToGive.description)
                ? currentDialogue.questToGive.description
                : "...";

            HideAllButtons();
            dialogueText.text = recap;
            Invoke(nameof(EndConversationNoStrike), responseDisplayDuration);
        }
    }
    #endregion

    #region End And Close
    /// <summary>
    /// Player cancelled (Esc or walked away). Ends mid-conversation with no strike.
    /// </summary>
    private void CancelDialogue(string reason)
    {
        Debug.Log($"[Dialogue] Cancelled ({reason}). No strike.");
        CancelInvoke();
        pendingDisappear = null;
        EndConversationNoStrike();
    }

    /// <summary>
    /// End the conversation without penalty and return the soul to LostSoul so re-entry works.
    /// </summary>
    private void EndConversationNoStrike()
    {
        RestoreLostSoul();
        CloseDialogue();
    }

    /// <summary>
    /// Return the soul to LostSoul after a non-wander end so InteractableEnemy's state gate
    /// lets the player talk again. Uses only EnemyCombat's existing public SetState.
    /// </summary>
    private void RestoreLostSoul()
    {
        RestoreLostSoulOn(currentEnemy);
    }

    private void RestoreLostSoulOn(InteractableEnemy enemy)
    {
        if (enemy == null) return;
        EnemyCombat combat = enemy.GetComponent<EnemyCombat>();
        if (combat != null)
            combat.SetState(EnemyCombat.EnemyState.LostSoul);
    }

    private void HideAllButtons()
    {
        for (int i = 0; i < choiceButtons.Count; i++)
        {
            if (choiceButtons[i] != null)
                choiceButtons[i].gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// The player stands still while talking, like any conversation in any game.
    /// Disables the PlayerMovement component for the conversation; PlayerMovement.cs
    /// itself is untouched. Camera look stays free.
    /// </summary>
    private void LockPlayerMovement()
    {
        if (lockedMovement != null) return;
        if (player == null) return;

        PlayerMovement movement = player.GetComponent<PlayerMovement>();
        if (movement != null && movement.enabled)
        {
            movement.enabled = false;
            lockedMovement = movement;
        }
    }

    private void UnlockPlayerMovement()
    {
        if (lockedMovement == null) return;

        lockedMovement.enabled = true;
        lockedMovement = null;
    }

    private void CloseDialogue()
    {
        dialoguePanel.SetActive(false);
        isDialogueActive = false;
        isRecapMode = false;
        isTurnInMode = false;
        turnInQuest = null;
        currentBeat = null;

        // Re-enable buttons for the next dialogue (visibility is managed per-show).
        for (int i = 0; i < choiceButtons.Count; i++)
        {
            if (choiceButtons[i] != null)
                choiceButtons[i].interactable = true;
        }

        UnlockPlayerMovement();

        // Return cursor to gameplay-locked state. ThirdPersonCamera takes over from here.
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        currentDialogue = null;
        currentEnemy = null;
    }
    #endregion
}