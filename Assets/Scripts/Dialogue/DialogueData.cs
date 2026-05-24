using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A single choice presented in a Tier 4 persuasion dialogue.
/// Per GDD Doc 09 §4a, the player picks one option from 2-3 — exactly one is emotionally correct.
/// Correct choice → BecomePeaceful + right ring grant. Wrong choice → BecomeHostile.
/// </summary>
[System.Serializable]
public class DialogueOption
{
    [Tooltip("The text shown on the choice button (e.g. \"You're safe now. What happened?\")")]
    public string choiceText;

    [Tooltip("What the enemy says in response after the player picks this option")]
    [TextArea(3, 6)]
    public string responseText;

    [Tooltip("TRUE = emotionally correct response. Triggers peaceful resolution + right ring grant. FALSE = wrong response, enemy becomes hostile. Exactly one option per DialogueData should be marked correct.")]
    public bool isCorrect;
}

/// <summary>
/// Tier 4 persuasion dialogue data per GDD Doc 09 §4a.
/// One ScriptableObject per persuadable enemy. Holds the soul's individual name, backstory intro,
/// and the 2-3 choice options the player picks from.
/// </summary>
[CreateAssetMenu(fileName = "NewDialogue", menuName = "Yoru/Dialogue Data")]
public class DialogueData : ScriptableObject
{
    #region Enemy Identity
    [Header("Enemy Identity")]
    [Tooltip("The individual soul's name shown to the player (e.g. \"Hitotsume of the Lantern Festival\" — not just the species \"Hitotsume\"). Per GDD Doc 09 §4a step 1.")]
    public string enemyDisplayName;
    #endregion

    #region Opening Dialogue
    [Header("Opening Dialogue")]
    [Tooltip("2-3 lines of personal backstory the soul speaks when Tomoe approaches. Per GDD Doc 09 §4a step 1.")]
    [TextArea(4, 8)]
    public string initialDialogue;
    #endregion

    #region Player Choices
    [Header("Player Choices")]
    [Tooltip("2-3 emotional response options. Exactly one should be marked isCorrect. The dialogue UI currently has 3 button slots — extras beyond 3 are ignored silently at runtime.")]
    public List<DialogueOption> options = new List<DialogueOption>();
    #endregion
}