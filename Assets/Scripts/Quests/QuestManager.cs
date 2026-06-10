using UnityEngine;

/// <summary>
/// Stub quest manager for Dialogue System v2. Replaced by the real implementation in a
/// future sub-chat. DialogueManager calls GiveQuest on FINAL_SUCCESS when the dialogue's
/// endBehavior is GIVE_QUEST. DialogueManager is null-safe around Instance, so this
/// component is optional in the scene during dialogue testing.
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

    #region Public API
    /// <summary>
    /// Hand a quest to the player. Stub: logs only.
    /// </summary>
    public void GiveQuest(QuestData data)
    {
        Debug.Log($"[STUB] Quest given: {(data != null ? data.displayName : "null")}");
    }
    #endregion
}
