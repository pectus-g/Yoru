using UnityEngine;

[System.Serializable]
public class DialogueChoice
{
    public string choiceText;
    public DialogueResponse response;
    public ChoiceType type;
}

[System.Serializable]
public class DialogueResponse
{
    [TextArea(3, 6)]
    public string responseText;
    public EnemyOutcome outcome;
}

public enum ChoiceType
{
    Light,      // Empathize, help, kindness
    Dark,       // Confront, harsh, aggressive
    Neutral     // Leave, avoid, no commitment
}

public enum EnemyOutcome
{
    BecomePeaceful,   // Soul finds peace and passes on
    BecomeHostile,    // Soul becomes angry and attacks
    StayConfused,     // Soul remains in limbo
    AskMoreQuestions  // Continues dialogue (future feature)
}

[CreateAssetMenu(fileName = "NewDialogue", menuName = "Yoru/Dialogue Data")]
public class DialogueData : ScriptableObject
{
    [Header("Enemy Info")]
    public string enemyName;
    
    [Header("Initial Dialogue")]
    [TextArea(4, 8)]
    public string initialDialogue;
    
    [Header("Player Choices")]
    public DialogueChoice lightChoice;
    public DialogueChoice darkChoice;
    public DialogueChoice neutralChoice;
    
    [Header("Karma Rewards")]
    public int lightKarmaReward = 1;
    public int darkKarmaReward = 1;
}