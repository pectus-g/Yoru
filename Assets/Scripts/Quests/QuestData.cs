using System.Collections.Generic;
using UnityEngine;

#region Enums
/// <summary>
/// What completes a quest step.
/// ENTER_LOCATION and INTERACT are matched by triggerId (QuestTrigger components in
/// the scene, ENTER_AREA and PRESS_E modes). OBTAIN_ITEM watches the inventory.
/// TALK_TO_GIVER marks the quest ready for turn-in at the giver soul (DialogueManager
/// runs the turn-in beat). DEFEAT_ENEMY is matched by the defeated soul's dialogueId.
/// EXTERNAL is completed only by code (scripted sequences).
/// </summary>
public enum QuestStepTrigger
{
    ENTER_LOCATION,
    OBTAIN_ITEM,
    INTERACT,
    TALK_TO_GIVER,
    DEFEAT_ENEMY,
    EXTERNAL
}

/// <summary>
/// How the quest resolves after its steps are done.
/// RETURN_TO_GIVER: the final TALK_TO_GIVER step opens a short turn-in conversation at
/// the giver soul, rewards are handed out there. WORLD_EVENT: a scripted moment in the
/// world completes the quest via QuestManager.CompleteQuestExternally (the Stolen Face
/// cave reveal is the first of these).
/// </summary>
public enum QuestResolutionType
{
    RETURN_TO_GIVER,
    WORLD_EVENT
}
#endregion

#region Step
/// <summary>
/// One objective inside a quest. Steps complete strictly in list order.
/// </summary>
[System.Serializable]
public class QuestStep
{
    [Tooltip("Unique id within this quest, e.g. S1, S2")]
    public string stepId;

    [Tooltip("Objective text shown on the Memory Parchment while this step is current. TMP color tags allowed")]
    [TextArea(1, 3)]
    public string hintText;

    [Tooltip("What completes this step")]
    public QuestStepTrigger trigger = QuestStepTrigger.ENTER_LOCATION;

    [Tooltip("Match key. ENTER_LOCATION / INTERACT: the triggerId of the scene component. DEFEAT_ENEMY: the soul's dialogueId. Unused for other types")]
    public string triggerId;

    [Tooltip("OBTAIN_ITEM only: the inventory item the player must hold")]
    public InventoryItem requiredItem;

    [Tooltip("OBTAIN_ITEM only: how many of the item the player must hold")]
    public int requiredQuantity = 1;
}
#endregion

#region Reward
/// <summary>
/// One reward line: an item handed straight into the inventory on completion.
/// Per the June 2026 ruling, tier 2-4 quests reward items directly at resolution;
/// the Ancient Tree is not involved.
/// </summary>
[System.Serializable]
public class QuestReward
{
    [Tooltip("Item added to the inventory when the quest completes")]
    public InventoryItem item;

    [Tooltip("How many to add")]
    public int quantity = 1;
}
#endregion

/// <summary>
/// Per-quest data asset for the quest system.
/// description doubles as the post-quest recap the giver soul re-reads verbatim
/// ("What was it you asked for?"), so write it in a voice the soul can plausibly repeat.
/// That contract is unchanged from the Dialogue System v2 stub.
/// </summary>
[CreateAssetMenu(fileName = "NewQuest", menuName = "YORU/Quest Data")]
public class QuestData : ScriptableObject
{
    #region Identity
    [Header("Quest")]
    [Tooltip("Stable ASCII key for runtime state tracking, e.g. \"stolen_face\". Must be unique")]
    public string questId;

    [Tooltip("Quest name shown to the player and logged on hand-over")]
    public string displayName;

    [Tooltip("The unfinished business. Also the text the soul repeats in the post-quest recap")]
    [TextArea(2, 5)]
    public string description;

    [Tooltip("Enemy tier of the giver soul (1 to 4). Decides which Memory Parchment the quest appears on")]
    [Range(1, 4)]
    public int tier = 4;

    [Tooltip("dialogueId of the giver soul, e.g. \"nopperabo\". Links the quest to its parchment entry and its turn-in conversation")]
    public string giverDialogueId;
    #endregion

    #region Steps
    [Header("Steps (completed in order)")]
    [Tooltip("Ordered objectives. The current step's hintText shows on the parchment and decides which glow trail is lit")]
    public List<QuestStep> steps = new List<QuestStep>();
    #endregion

    #region Resolution
    [Header("Resolution")]
    [Tooltip("RETURN_TO_GIVER: turn-in conversation at the giver. WORLD_EVENT: a scripted moment completes the quest")]
    public QuestResolutionType resolutionType = QuestResolutionType.RETURN_TO_GIVER;

    [Tooltip("Items handed straight into the inventory on completion. Leave empty for quests with hand-placed loot")]
    public List<QuestReward> rewards = new List<QuestReward>();
    #endregion

    #region Parchment Status
    [Header("Memory Parchment Status")]
    [Tooltip("Status stamped on the parchment entry when this quest completes, e.g. \"Found the Peace\" or \"LIAR!\"")]
    public string completedStatusText = "Found the Peace";

    [Tooltip("Strike the soul's name through on the parchment when the quest completes (the mujina's lie)")]
    public bool strikeThroughOnComplete = false;
    #endregion

    #region Turn-In (RETURN_TO_GIVER only)
    [Header("Turn-In Conversation (RETURN_TO_GIVER only)")]
    [Tooltip("What the soul says when the player returns with the business finished")]
    [TextArea(1, 3)]
    public string turnInLine;

    [Tooltip("Label of the single response button during turn-in")]
    public string turnInButtonText = "...";

    [Tooltip("The soul's final words, shown after the turn-in click, before it passes on")]
    [TextArea(1, 3)]
    public string farewellLine;

    [Tooltip("Deactivate the giver soul after the farewell line (the soul passes on). Visual dissolve is a later polish pass")]
    public bool giverDisappearsOnCompletion = true;
    #endregion

    #region Lookup
    /// <summary>
    /// Step by list index. Returns null when out of range; callers must handle null.
    /// </summary>
    public QuestStep GetStep(int index)
    {
        if (index < 0 || index >= steps.Count) return null;
        return steps[index];
    }
    #endregion
}
