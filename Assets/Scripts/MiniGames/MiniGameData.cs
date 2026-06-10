using UnityEngine;

/// <summary>
/// Minimal mini-game data stub for Dialogue System v2.
/// The real Tier 1 mini-game system is a future sub-chat.
/// </summary>
[CreateAssetMenu(fileName = "NewMiniGame", menuName = "YORU/Mini Game Data")]
public class MiniGameData : ScriptableObject
{
    [Header("Mini Game")]
    [Tooltip("Mini-game name logged on trigger")]
    public string displayName;
}
