using UnityEngine;

/// <summary>
/// Stub mini-game manager for Dialogue System v2. Replaced by the real Tier 1 mini-game
/// system in a future sub-chat. DialogueManager calls TriggerMinigame on FINAL_SUCCESS
/// when the dialogue's endBehavior is TRIGGER_MINIGAME. DialogueManager is null-safe
/// around Instance, so this component is optional in the scene during dialogue testing.
/// </summary>
public class MiniGameManager : MonoBehaviour
{
    #region Singleton
    public static MiniGameManager Instance { get; private set; }

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
    /// Trigger a Tier 1 persuasion mini-game. Stub: logs only.
    /// </summary>
    public void TriggerMinigame(MiniGameData data)
    {
        Debug.Log($"[STUB] Minigame triggered: {(data != null ? data.displayName : "null")}");
    }
    #endregion
}
