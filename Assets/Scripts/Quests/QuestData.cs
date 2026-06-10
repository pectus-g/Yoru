using UnityEngine;

/// <summary>
/// Minimal quest data stub for Dialogue System v2.
/// The real quest system (objectives, tracking, resolution, rewards) is a future sub-chat.
/// description is read aloud by the post-quest recap path ("What was it you asked for?"),
/// so write it in a voice the soul can plausibly repeat.
/// </summary>
[CreateAssetMenu(fileName = "NewQuest", menuName = "YORU/Quest Data")]
public class QuestData : ScriptableObject
{
    [Header("Quest")]
    [Tooltip("Quest name shown to the player and logged on hand-over")]
    public string displayName;

    [Tooltip("The unfinished business. Also the text the soul repeats in the post-quest recap")]
    [TextArea(2, 5)]
    public string description;
}
