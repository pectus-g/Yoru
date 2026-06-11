using System.Collections.Generic;
using UnityEngine;

#region Enums
/// <summary>
/// How picking an option routes the conversation.
/// CORRECT and SOFT_WRONG both advance via nextBeatId (SOFT_WRONG is a design/analytics
/// distinction, mechanically identical). HARD_WRONG ends the conversation with a goodbye
/// line and adds a strike. FINAL_SUCCESS ends the conversation and fires endBehavior.
/// </summary>
public enum DialogueBranchType
{
    CORRECT,
    SOFT_WRONG,
    HARD_WRONG,
    FINAL_SUCCESS
}

/// <summary>
/// What happens when the player reaches FINAL_SUCCESS.
/// Tier 4-2 souls give a quest. Tier 1 bosses trigger a mini-game. NONE is for scripted
/// conversations (the mujina cave reveal) where a controller listens to
/// DialogueManager.OnFinalSuccess instead. NONE is appended last so existing assets
/// keep their serialized values.
/// </summary>
public enum DialogueEndBehavior
{
    GIVE_QUEST,
    TRIGGER_MINIGAME,
    NONE
}
#endregion

#region Option
/// <summary>
/// A single player response option inside a beat. 2 or 3 per beat.
/// </summary>
[System.Serializable]
public class DialogueOption
{
    [Tooltip("The text shown on the choice button")]
    [TextArea(1, 3)]
    public string text;

    [Tooltip("How this option routes: CORRECT / SOFT_WRONG advance to nextBeatId, HARD_WRONG ends with a strike, FINAL_SUCCESS ends and fires the end behavior")]
    public DialogueBranchType branchType = DialogueBranchType.CORRECT;

    [Tooltip("Beat to advance to. Used by CORRECT and SOFT_WRONG only; ignored for HARD_WRONG and FINAL_SUCCESS")]
    public string nextBeatId;

    [Tooltip("Optional goodbye line for HARD_WRONG only. Empty = fall back to the beat goodbye, then the soul default goodbye")]
    [TextArea(1, 3)]
    public string optionGoodbyeLine;
}
#endregion

#region Beat
/// <summary>
/// One exchange in the conversation: the soul speaks one line, the player picks one option.
/// </summary>
[System.Serializable]
public class DialogueBeat
{
    [Tooltip("Unique id within this dialogue, e.g. B1, B1A, B2")]
    public string beatId;

    [Tooltip("What the soul says at this beat. Short, one breath")]
    [TextArea(2, 5)]
    public string soulLine;

    [Tooltip("2 or 3 player response options. The dialogue UI has 3 button slots; extras beyond 3 are ignored silently")]
    public List<DialogueOption> options = new List<DialogueOption>();

    [Tooltip("Fallback goodbye for HARD_WRONG options at this beat that have no optionGoodbyeLine of their own")]
    [TextArea(1, 3)]
    public string beatGoodbyeLine;

    [Tooltip("Optional VO clip played at the enemy position when this beat opens. Null = silent")]
    public AudioClip soulAudioClip;

    [Tooltip("Optional Animator trigger fired on the soul when this beat opens. Empty = no trigger")]
    public string soulAnimationTrigger;

    [Tooltip("Reserved for the future camera framing system. Stored but ignored at runtime for now")]
    public string cameraFramingId;
}
#endregion

/// <summary>
/// Dialogue System v2 per-soul conversation asset.
/// One ScriptableObject per persuadable soul. The code in DialogueManager is fully general;
/// everything unique to a soul (beats, branches, goodbyes, quest, recap lines) lives here.
/// Souls are never hostile and never pass on from dialogue: the conversation's only job is
/// to reach FINAL_SUCCESS and hand over the quest (or trigger the mini-game). The soul
/// stays alive in the world afterward and re-approaching it opens the post-quest recap.
/// </summary>
[CreateAssetMenu(fileName = "NewDialogue", menuName = "Yoru/Dialogue Data")]
public class DialogueData : ScriptableObject
{
    #region Identity
    [Header("Identity")]
    [Tooltip("Stable ASCII key for runtime state tracking, e.g. \"nopperabo\". Must be unique per soul")]
    public string dialogueId;

    [Tooltip("Display name shown to the player. Unicode is fine here, e.g. \"Nopperab\u014d\"")]
    public string soulName;

    [Tooltip("Enemy tier (1 to 4). Decides which Memory Parchment this soul appears on")]
    [Range(1, 4)]
    public int tier = 4;

    [Tooltip("The soul's short story as written on the Memory Parchment, next to their name")]
    [TextArea(2, 4)]
    public string soulStory;

    [Tooltip("Never create a Memory Parchment entry for this dialogue. For scripted conversations (the mujina cave reveal) that are not souls of their own")]
    public bool excludeFromJournal = false;
    #endregion

    #region Conversation
    [Header("Conversation")]
    [Tooltip("Beat id the full conversation opens at, e.g. \"B1\"")]
    public string startBeatId = "B1";

    [Tooltip("All beats of this conversation, main path and sub-arcs alike. Routed by beatId, order in this list does not matter")]
    public List<DialogueBeat> beats = new List<DialogueBeat>();

    [Tooltip("Soul-level fallback goodbye when a HARD_WRONG option and its beat both have empty goodbye lines")]
    [TextArea(1, 3)]
    public string soulDefaultGoodbye;
    #endregion

    #region Strikes
    [Header("Strikes And Cooldown")]
    [Tooltip("Seconds the soul stays unavailable after 3 strikes. Test value 10, production 300")]
    public float cooldownSeconds = 300f;
    #endregion

    #region End Behavior
    [Header("End Behavior")]
    [Tooltip("What FINAL_SUCCESS fires: a quest hand-over (Tier 4-2) or a mini-game (Tier 1)")]
    public DialogueEndBehavior endBehavior = DialogueEndBehavior.GIVE_QUEST;

    [Tooltip("Quest handed to the player on FINAL_SUCCESS when endBehavior is GIVE_QUEST")]
    public QuestData questToGive;

    [Tooltip("Mini-game triggered on FINAL_SUCCESS when endBehavior is TRIGGER_MINIGAME")]
    public MiniGameData miniGameToTrigger;
    #endregion

    #region Mistaken Identity
    [Header("Mistaken Identity")]
    [Tooltip("One-time line spoken first when the soul aggroed on Yoru but no fight started and the player returned as Tomoe, e.g. \"I took you for a nekomata...\". Empty = skip straight to the normal conversation")]
    [TextArea(1, 3)]
    public string mistakenIdentityLine;
    #endregion

    #region Post-Quest Recap
    [Header("Post-Quest Recap")]
    [Tooltip("What the soul says when re-approached after the quest has been given")]
    [TextArea(1, 3)]
    public string postQuestWaitingLine;

    [Tooltip("Option A label: the leave / later line that just closes the panel")]
    public string postQuestLeaveText;

    [Tooltip("Option B label: asking the soul to repeat the quest, e.g. \"What was it you asked for?\". The repeat text is questToGive.description")]
    public string postQuestAskAgainText;
    #endregion

    #region Lookup
    /// <summary>
    /// Find a beat by id. Returns null if not found; callers must handle null.
    /// </summary>
    public DialogueBeat GetBeat(string beatId)
    {
        if (string.IsNullOrEmpty(beatId)) return null;
        for (int i = 0; i < beats.Count; i++)
        {
            if (beats[i] != null && beats[i].beatId == beatId)
                return beats[i];
        }
        return null;
    }
    #endregion
}