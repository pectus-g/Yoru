using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Quest system singleton. Replaces the Dialogue System v2 stub; the GiveQuest entry
/// point keeps the exact signature DialogueManager already calls, so the shipped
/// dialogue flow is untouched.
///
/// Responsibilities:
///   - Active / completed quest tracking, keyed by QuestData.questId.
///   - Strictly ordered step progression, fed by scene QuestTriggers (both modes),
///     the inventory (OBTAIN_ITEM steps), enemy deaths
///     (DEFEAT_ENEMY steps), and scripted code (EXTERNAL steps, WORLD_EVENT quests).
///   - The tracked quest: the one quest whose glow trail is lit. Set from the
///     Memory Parchment UI. One tracked quest at a time, any number active.
///   - Reward delivery straight into the inventory at resolution (June 2026 ruling:
///     tier 2-4 rewards are direct, no tree involvement).
///   - Stamping the SoulJournal when a quest completes (Found the Peace / LIAR!).
///
/// State is session-only by design; persistence across sessions is a future pass.
/// The SoulJournal (the Memory Parchments' soul registry) lives at the bottom of this
/// file as a static class: no scene object, no Instance null checks, one file.
/// </summary>
public class QuestManager : MonoBehaviour
{
    #region Singleton
    public static QuestManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }
    #endregion

    #region Types
    public enum QuestState
    {
        NotStarted,
        Active,
        ReadyToTurnIn,
        Completed
    }

    /// <summary>
    /// Runtime record of one given quest.
    /// </summary>
    private class QuestEntry
    {
        public QuestData data;
        public int stepIndex;
        public QuestState state;
    }
    #endregion

    #region State
    private readonly Dictionary<string, QuestEntry> quests = new Dictionary<string, QuestEntry>();
    private string trackedQuestId = "";

    /// <summary>Fired whenever any quest is given, advances, or completes. UI refresh hook.</summary>
    public event System.Action OnQuestsChanged;

    /// <summary>Fired once when a quest is handed to the player. The parchment HUD icon glows on this.</summary>
    public event System.Action<QuestData> OnQuestGiven;

    /// <summary>Fired when the tracked quest changes. Glow trails listen via polling, UI via this.</summary>
    public event System.Action OnTrackedQuestChanged;
    #endregion

    #region Lifecycle
    private void Start()
    {
        if (InventoryManager.Instance != null)
            InventoryManager.Instance.OnInventoryChanged += CheckObtainItemSteps;
    }

    private void OnDestroy()
    {
        if (InventoryManager.Instance != null)
            InventoryManager.Instance.OnInventoryChanged -= CheckObtainItemSteps;
    }
    #endregion

    #region Public API: Give
    /// <summary>
    /// Hand a quest to the player. Called by DialogueManager on FINAL_SUCCESS.
    /// Auto-tracks the quest when nothing else is tracked, so the glow trail lights
    /// up immediately after the conversation that handed it over.
    /// </summary>
    public void GiveQuest(QuestData data)
    {
        if (data == null)
        {
            Debug.LogWarning("[Quest] GiveQuest called with null data.");
            return;
        }
        if (string.IsNullOrEmpty(data.questId))
        {
            Debug.LogError($"[Quest] '{data.name}' has an empty questId. Not given.");
            return;
        }
        if (quests.ContainsKey(data.questId))
        {
            Debug.Log($"[Quest] '{data.questId}' already given. Ignored.");
            return;
        }

        QuestEntry entry = new QuestEntry { data = data, stepIndex = 0, state = QuestState.Active };
        quests[data.questId] = entry;
        Debug.Log($"[Quest] Given: {data.displayName} ({data.questId}), {data.steps.Count} step(s)");

        OnQuestGiven?.Invoke(data);

        EvaluateCurrentStep(entry);

        if (string.IsNullOrEmpty(trackedQuestId))
            TrackQuest(data.questId);

        OnQuestsChanged?.Invoke();
    }
    #endregion

    #region Public API: Triggers
    /// <summary>
    /// A scene QuestTrigger fired (area entry or E press).
    /// Advances the first active quest whose current step matches the id.
    /// Returns true when a step advanced.
    /// </summary>
    public bool NotifyTrigger(string triggerId)
    {
        if (string.IsNullOrEmpty(triggerId)) return false;

        foreach (KeyValuePair<string, QuestEntry> pair in quests)
        {
            QuestEntry entry = pair.Value;
            if (entry.state != QuestState.Active) continue;

            QuestStep step = entry.data.GetStep(entry.stepIndex);
            if (step == null) continue;

            bool matches = (step.trigger == QuestStepTrigger.ENTER_LOCATION
                         || step.trigger == QuestStepTrigger.INTERACT)
                         && step.triggerId == triggerId;
            if (!matches) continue;

            AdvanceStep(entry);
            return true;
        }
        return false;
    }

    /// <summary>
    /// True when some active quest's CURRENT step is waiting on this triggerId.
    /// QuestTrigger (PRESS_E mode) uses this to decide whether to show its prompt at all.
    /// </summary>
    public bool IsTriggerRelevant(string triggerId)
    {
        if (string.IsNullOrEmpty(triggerId)) return false;

        foreach (KeyValuePair<string, QuestEntry> pair in quests)
        {
            QuestEntry entry = pair.Value;
            if (entry.state != QuestState.Active) continue;

            QuestStep step = entry.data.GetStep(entry.stepIndex);
            if (step == null) continue;

            if ((step.trigger == QuestStepTrigger.ENTER_LOCATION
              || step.trigger == QuestStepTrigger.INTERACT)
              && step.triggerId == triggerId)
                return true;
        }
        return false;
    }

    /// <summary>
    /// A soul died. Advances DEFEAT_ENEMY steps matching its dialogueId.
    /// Wired from InteractableEnemy via EnemyHealth.OnDied.
    /// </summary>
    public void NotifyEnemyDefeated(string dialogueId)
    {
        if (string.IsNullOrEmpty(dialogueId)) return;

        foreach (KeyValuePair<string, QuestEntry> pair in quests)
        {
            QuestEntry entry = pair.Value;
            if (entry.state != QuestState.Active) continue;

            QuestStep step = entry.data.GetStep(entry.stepIndex);
            if (step == null) continue;

            if (step.trigger == QuestStepTrigger.DEFEAT_ENEMY && step.triggerId == dialogueId)
            {
                AdvanceStep(entry);
                return;
            }
        }
    }

    /// <summary>
    /// Complete an EXTERNAL step of the given quest by code (scripted sequences).
    /// </summary>
    public void CompleteExternalStep(string questId, string stepId)
    {
        if (!quests.TryGetValue(questId, out QuestEntry entry) || entry.state != QuestState.Active) return;

        QuestStep step = entry.data.GetStep(entry.stepIndex);
        if (step == null || step.trigger != QuestStepTrigger.EXTERNAL || step.stepId != stepId) return;

        AdvanceStep(entry);
    }
    #endregion

    #region Public API: Turn-In And External Completion
    /// <summary>
    /// True when a quest given by this soul is waiting for its turn-in conversation.
    /// DialogueManager checks this before opening the recap.
    /// </summary>
    public bool IsReadyToTurnIn(string giverDialogueId)
    {
        return FindTurnInQuest(giverDialogueId) != null;
    }

    /// <summary>
    /// The QuestData waiting for turn-in at this soul, or null.
    /// </summary>
    public QuestData GetTurnInQuest(string giverDialogueId)
    {
        QuestEntry entry = FindTurnInQuest(giverDialogueId);
        return entry != null ? entry.data : null;
    }

    /// <summary>
    /// Finish a RETURN_TO_GIVER quest at its giver: rewards into the inventory,
    /// parchment stamped, quest closed. Called by DialogueManager during the turn-in beat.
    /// </summary>
    public void CompleteTurnIn(string giverDialogueId)
    {
        QuestEntry entry = FindTurnInQuest(giverDialogueId);
        if (entry == null)
        {
            Debug.LogWarning($"[Quest] CompleteTurnIn: nothing ready to turn in at '{giverDialogueId}'.");
            return;
        }
        CompleteQuest(entry);
    }

    /// <summary>
    /// Finish a WORLD_EVENT quest from scripted code (the Stolen Face cave reveal).
    /// Works regardless of remaining steps: the world event IS the resolution.
    /// </summary>
    public void CompleteQuestExternally(QuestData data)
    {
        if (data == null || !quests.TryGetValue(data.questId, out QuestEntry entry))
        {
            Debug.LogWarning("[Quest] CompleteQuestExternally: quest not active.");
            return;
        }
        if (entry.state == QuestState.Completed) return;

        CompleteQuest(entry);
    }
    #endregion

    #region Public API: Tracking And Queries
    /// <summary>
    /// Make this quest the tracked one (its glow trail lights up). Pass the id of an
    /// already-tracked quest to untrack it.
    /// </summary>
    public void TrackQuest(string questId)
    {
        string next = trackedQuestId == questId ? "" : questId;
        if (trackedQuestId == next) return;

        trackedQuestId = next;
        Debug.Log($"[Quest] Tracked: '{(string.IsNullOrEmpty(trackedQuestId) ? "none" : trackedQuestId)}'");
        OnTrackedQuestChanged?.Invoke();
        OnQuestsChanged?.Invoke();
    }

    public string TrackedQuestId => trackedQuestId;

    public QuestState GetState(string questId)
    {
        return quests.TryGetValue(questId, out QuestEntry entry) ? entry.state : QuestState.NotStarted;
    }

    /// <summary>
    /// The current objective hint for the parchment. Empty when the quest is unknown
    /// or completed.
    /// </summary>
    public string GetCurrentHint(string questId)
    {
        if (!quests.TryGetValue(questId, out QuestEntry entry)) return "";
        if (entry.state == QuestState.Completed) return "";

        QuestStep step = entry.data.GetStep(entry.stepIndex);
        if (step != null) return step.hintText;

        // All steps done but the quest is waiting on its world event.
        QuestStep last = entry.data.GetStep(entry.data.steps.Count - 1);
        return last != null ? last.hintText : "";
    }

    /// <summary>
    /// The quest this soul gave, if any. The Memory Parchment uses this to render the
    /// quest block beside the soul's story.
    /// </summary>
    public QuestData GetQuestForGiver(string giverDialogueId)
    {
        foreach (KeyValuePair<string, QuestEntry> pair in quests)
        {
            if (pair.Value.data.giverDialogueId == giverDialogueId)
                return pair.Value.data;
        }
        return null;
    }

    /// <summary>
    /// True when this quest step's glow trail should be lit: quest tracked, not
    /// completed, and (when stepId is given) the step is the current one.
    /// GlowTrail polls this; the Tomoe-form gate lives in GlowTrail itself.
    /// </summary>
    public bool IsTrailVisible(string questId, string stepId)
    {
        if (!SameId(questId, trackedQuestId)) return false;
        if (!quests.TryGetValue(trackedQuestId, out QuestEntry entry)) return false;
        if (entry.state == QuestState.Completed) return false;
        if (string.IsNullOrEmpty(stepId)) return true;

        QuestStep step = entry.data.GetStep(entry.stepIndex);
        if (step != null) return SameId(step.stepId, stepId);

        // Steps exhausted, WORLD_EVENT pending: keep the last step's trail lit.
        QuestStep last = entry.data.GetStep(entry.data.steps.Count - 1);
        return last != null && SameId(last.stepId, stepId);
    }

    /// <summary>
    /// Forgiving id comparison for inspector-typed fields: trims whitespace, ignores
    /// case, so "S1 " still matches "s1". Empty never matches.
    /// </summary>
    private static bool SameId(string a, string b)
    {
        if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b)) return false;
        return string.Equals(a.Trim(), b.Trim(), System.StringComparison.OrdinalIgnoreCase);
    }
    #endregion

    #region Internals
    private QuestEntry FindTurnInQuest(string giverDialogueId)
    {
        if (string.IsNullOrEmpty(giverDialogueId)) return null;

        foreach (KeyValuePair<string, QuestEntry> pair in quests)
        {
            QuestEntry entry = pair.Value;
            if (entry.state == QuestState.ReadyToTurnIn && entry.data.giverDialogueId == giverDialogueId)
                return entry;
        }
        return null;
    }

    private void AdvanceStep(QuestEntry entry)
    {
        QuestStep done = entry.data.GetStep(entry.stepIndex);
        entry.stepIndex++;
        Debug.Log($"[Quest] '{entry.data.questId}' step {(done != null ? done.stepId : "?")} complete -> index {entry.stepIndex}");

        EvaluateCurrentStep(entry);
        OnQuestsChanged?.Invoke();
    }

    /// <summary>
    /// Settle whatever the new current step implies: TALK_TO_GIVER flips the quest to
    /// ReadyToTurnIn, OBTAIN_ITEM may already be satisfied by items in hand, and
    /// running out of steps either waits for the world event or flips to turn-in.
    /// </summary>
    private void EvaluateCurrentStep(QuestEntry entry)
    {
        if (entry.state != QuestState.Active) return;

        QuestStep step = entry.data.GetStep(entry.stepIndex);

        if (step == null)
        {
            // No steps left. WORLD_EVENT quests wait for CompleteQuestExternally;
            // RETURN_TO_GIVER quests without an explicit TALK_TO_GIVER step flip here.
            if (entry.data.resolutionType == QuestResolutionType.RETURN_TO_GIVER)
            {
                entry.state = QuestState.ReadyToTurnIn;
                Debug.Log($"[Quest] '{entry.data.questId}' ready to turn in at '{entry.data.giverDialogueId}'");
            }
            return;
        }

        if (step.trigger == QuestStepTrigger.TALK_TO_GIVER)
        {
            entry.state = QuestState.ReadyToTurnIn;
            Debug.Log($"[Quest] '{entry.data.questId}' ready to turn in at '{entry.data.giverDialogueId}'");
            return;
        }

        if (step.trigger == QuestStepTrigger.OBTAIN_ITEM)
            CheckObtainItemStep(entry, step);
    }

    /// <summary>
    /// Inventory changed: re-check every active OBTAIN_ITEM current step.
    /// </summary>
    private void CheckObtainItemSteps()
    {
        // Snapshot keys: AdvanceStep mutates entries (never the dictionary), but a
        // chain of instant completions is possible, so iterate defensively.
        List<QuestEntry> snapshot = new List<QuestEntry>(quests.Values);
        for (int i = 0; i < snapshot.Count; i++)
        {
            QuestEntry entry = snapshot[i];
            if (entry.state != QuestState.Active) continue;

            QuestStep step = entry.data.GetStep(entry.stepIndex);
            if (step != null && step.trigger == QuestStepTrigger.OBTAIN_ITEM)
                CheckObtainItemStep(entry, step);
        }
    }

    private void CheckObtainItemStep(QuestEntry entry, QuestStep step)
    {
        if (step.requiredItem == null)
        {
            Debug.LogError($"[Quest] '{entry.data.questId}' step {step.stepId} is OBTAIN_ITEM with no requiredItem. Skipping step.");
            AdvanceStep(entry);
            return;
        }
        if (InventoryManager.Instance == null) return;

        if (InventoryManager.Instance.GetItemQuantity(step.requiredItem) >= Mathf.Max(1, step.requiredQuantity))
            AdvanceStep(entry);
    }

    private void CompleteQuest(QuestEntry entry)
    {
        entry.state = QuestState.Completed;
        Debug.Log($"[Quest] Completed: {entry.data.displayName} ({entry.data.questId})");

        DeliverRewards(entry.data);

        SoulJournal.MarkQuestCompleted(
            entry.data.giverDialogueId,
            entry.data.completedStatusText,
            entry.data.strikeThroughOnComplete);

        if (trackedQuestId == entry.data.questId)
            TrackQuest(entry.data.questId); // toggles off

        OnQuestsChanged?.Invoke();
    }

    /// <summary>
    /// Rewards go straight into the inventory at resolution. Overflow is logged, not
    /// dropped silently; a drop-at-feet fallback is a future polish item.
    /// </summary>
    private void DeliverRewards(QuestData data)
    {
        if (data.rewards == null || data.rewards.Count == 0) return;

        if (InventoryManager.Instance == null)
        {
            Debug.LogWarning($"[Quest] '{data.questId}' has rewards but no InventoryManager is in the scene.");
            return;
        }

        for (int i = 0; i < data.rewards.Count; i++)
        {
            QuestReward reward = data.rewards[i];
            if (reward == null || reward.item == null) continue;

            bool added = InventoryManager.Instance.AddItem(reward.item, Mathf.Max(1, reward.quantity));
            if (!added)
                Debug.LogWarning($"[Quest] Inventory full: reward '{reward.item.itemName}' x{reward.quantity} was not delivered.");
            else
                Debug.Log($"[Quest] Reward delivered: {reward.item.itemName} x{reward.quantity}");
        }
    }
    #endregion
}

/// <summary>
/// Registry of every soul the player has encountered, feeding the Memory Parchments (J).
/// One record per soul, keyed by DialogueData.dialogueId.
///
/// A soul enters the journal the first time the player talks to it (DialogueManager
/// calls RegisterMet) or the moment it dies in combat (InteractableEnemy calls
/// MarkDestroyed via EnemyHealth.OnDied). Quest completion stamps the record through
/// QuestManager (Found the Peace by default, per-quest overrides like LIAR!).
///
/// Static on purpose: pure session data, no Unity lifecycle, nothing to place in the
/// scene. The SubsystemRegistration reset keeps it correct when Enter Play Mode runs
/// with domain reload disabled.
/// </summary>
public static class SoulJournal
{
    #region Types
    public enum SoulStatus
    {
        Met,        // talked to, business unfinished
        FoundPeace, // quest completed (default stamp; statusText may override the wording)
        Destroyed   // killed in combat
    }

    /// <summary>
    /// One parchment entry.
    /// </summary>
    public class SoulRecord
    {
        public string dialogueId;
        public string soulName;
        public int tier;
        public string story;
        public SoulStatus status;
        public string statusText;     // what the parchment stamps; "" while Met
        public bool strikeThrough;    // the mujina's lie: name struck out
    }
    #endregion

    #region State
    private static readonly Dictionary<string, SoulRecord> records = new Dictionary<string, SoulRecord>();

    /// <summary>Fired whenever a record is added or changed. Parchment UI refresh hook.</summary>
    public static event System.Action OnJournalChanged;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewSession()
    {
        records.Clear();
        OnJournalChanged = null;
    }
    #endregion

    #region Public API
    /// <summary>
    /// Register a soul as met. Idempotent; never downgrades an existing status.
    /// Called by DialogueManager when a conversation opens.
    /// </summary>
    public static void RegisterMet(DialogueData dialogue)
    {
        if (dialogue == null || string.IsNullOrEmpty(dialogue.dialogueId)) return;
        if (dialogue.excludeFromJournal) return;

        if (records.TryGetValue(dialogue.dialogueId, out SoulRecord existing))
        {
            // Refresh display fields in case the asset changed; status untouched.
            existing.soulName = dialogue.soulName;
            existing.tier = dialogue.tier;
            existing.story = dialogue.soulStory;
            return;
        }

        records[dialogue.dialogueId] = new SoulRecord
        {
            dialogueId = dialogue.dialogueId,
            soulName = dialogue.soulName,
            tier = Mathf.Clamp(dialogue.tier, 1, 4),
            story = dialogue.soulStory,
            status = SoulStatus.Met,
            statusText = "",
            strikeThrough = false
        };

        Debug.Log($"[Journal] Met: '{dialogue.dialogueId}' (tier {dialogue.tier})");
        OnJournalChanged?.Invoke();
    }

    /// <summary>
    /// Stamp a soul's quest as completed. statusText is the parchment word
    /// ("Found the Peace", "LIAR!"). Called by QuestManager.
    /// </summary>
    public static void MarkQuestCompleted(string dialogueId, string statusText, bool strikeThrough)
    {
        if (string.IsNullOrEmpty(dialogueId)) return;
        if (!records.TryGetValue(dialogueId, out SoulRecord record))
        {
            Debug.LogWarning($"[Journal] MarkQuestCompleted for unknown soul '{dialogueId}'.");
            return;
        }

        record.status = SoulStatus.FoundPeace;
        record.statusText = string.IsNullOrEmpty(statusText) ? "Found the Peace" : statusText;
        record.strikeThrough = strikeThrough;

        Debug.Log($"[Journal] '{dialogueId}' stamped: {record.statusText}");
        OnJournalChanged?.Invoke();
    }

    /// <summary>
    /// A soul died in combat. Registers it first if it was never talked to, so killed
    /// souls still appear on the parchment. Never overwrites a completed quest stamp.
    /// </summary>
    public static void MarkDestroyed(DialogueData dialogue)
    {
        if (dialogue == null || string.IsNullOrEmpty(dialogue.dialogueId)) return;
        if (dialogue.excludeFromJournal) return;

        RegisterMet(dialogue);
        SoulRecord record = records[dialogue.dialogueId];

        if (record.status == SoulStatus.FoundPeace) return;

        record.status = SoulStatus.Destroyed;
        record.statusText = "Destroyed";

        Debug.Log($"[Journal] '{dialogue.dialogueId}' stamped: Destroyed");
        OnJournalChanged?.Invoke();
    }

    /// <summary>
    /// All records for one tier, in encounter order. The parchment for that tier
    /// renders exactly this list.
    /// </summary>
    public static List<SoulRecord> GetRecordsForTier(int tier)
    {
        List<SoulRecord> result = new List<SoulRecord>();
        foreach (KeyValuePair<string, SoulRecord> pair in records)
        {
            if (pair.Value.tier == tier)
                result.Add(pair.Value);
        }
        return result;
    }
    #endregion
}
