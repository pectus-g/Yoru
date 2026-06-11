using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The Memory Parchments (J): four parchment pages, one per enemy tier, ordered
/// Tier 4 -> Tier 1 (page 2 = Tier 3 = Nopperabo). Each page lists every soul of that
/// tier the player has encountered: name, their short story, their quest, and a status
/// stamp (Found the Peace / Destroyed / LIAR!). The mujina's lie strikes the name
/// through.
///
/// Clicking a quest's track button makes it the TRACKED quest: its glow trail lights
/// up in the world (Tomoe only). One tracked quest at a time, any number active.
///
/// The hierarchy is built by YORU > Build Memory Parchments UI (editor menu); this
/// component only drives it. Cursor handling mirrors DialogueManager: visible while
/// open, locked again on close.
/// </summary>
public class MemoryParchmentUI : MonoBehaviour
{
    #region Types
    /// <summary>
    /// One soul entry slot on the parchment. Wired by the builder.
    /// </summary>
    [System.Serializable]
    public class EntrySlot
    {
        public GameObject root;
        public TextMeshProUGUI nameText;
        public Image strikeLine;
        public TextMeshProUGUI storyText;
        public TextMeshProUGUI questNameText;
        public TextMeshProUGUI questHintText;
        public TextMeshProUGUI statusText;
        public Button trackButton;
        public TextMeshProUGUI trackLabel;
    }
    #endregion

    #region Inspector
    [Header("Wired By Builder")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private Image parchmentImage;
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI pageLabelText;
    [SerializeField] private List<EntrySlot> entrySlots = new List<EntrySlot>();
    [SerializeField] private Button previousPageButton;
    [SerializeField] private Button nextPageButton;

    [Tooltip("Optional per-tier parchment art, index 0 = Tier 1 ... index 3 = Tier 4. Empty entries keep the default sprite")]
    [SerializeField] private List<Sprite> tierParchmentSprites = new List<Sprite>();

    [Header("Behaviour")]
    [Tooltip("Key that opens and closes the parchments. J per GDD Doc 04")]
    [SerializeField] private KeyCode toggleKey = KeyCode.J;

    [Tooltip("Status colour for Found the Peace")]
    [SerializeField] private Color peaceColor = new Color(0.62f, 0.78f, 0.55f, 1f);

    [Tooltip("Status colour for Destroyed and LIAR!")]
    [SerializeField] private Color destroyedColor = new Color(0.72f, 0.25f, 0.2f, 1f);
    #endregion

    #region State
    private int pageIndex; // 0..3, tier = 4 - pageIndex
    private bool isOpen;
    private CursorLockMode previousLockState;
    private bool previousCursorVisible;

    private static readonly string[] RomanPages = { "I", "II", "III", "IV" };
    #endregion

    #region Lifecycle
    private void Start()
    {
        if (panelRoot != null)
            panelRoot.SetActive(false);

        SoulJournal.OnJournalChanged += RefreshIfOpen;
        if (QuestManager.Instance != null)
            QuestManager.Instance.OnQuestsChanged += RefreshIfOpen;
    }

    private void OnDestroy()
    {
        SoulJournal.OnJournalChanged -= RefreshIfOpen;
        if (QuestManager.Instance != null)
            QuestManager.Instance.OnQuestsChanged -= RefreshIfOpen;
    }

    private void Update()
    {
        if (Input.GetKeyDown(toggleKey))
        {
            if (isOpen) Close();
            else Open();
        }

        if (isOpen && Input.GetKeyDown(KeyCode.Escape))
            Close();
    }
    #endregion

    #region Open / Close
    /// <summary>
    /// Open the parchments. Refuses while a conversation is on screen; the dialogue
    /// owns the cursor and the player's attention.
    /// </summary>
    public void Open()
    {
        if (isOpen) return;
        if (panelRoot == null) return;
        if (DialogueManager.Instance != null && DialogueManager.Instance.IsDialogueActive) return;

        isOpen = true;
        panelRoot.SetActive(true);

        previousLockState = Cursor.lockState;
        previousCursorVisible = Cursor.visible;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        Refresh();
    }

    /// <summary>
    /// Close and hand the cursor back to gameplay.
    /// </summary>
    public void Close()
    {
        if (!isOpen) return;

        isOpen = false;
        if (panelRoot != null)
            panelRoot.SetActive(false);

        Cursor.lockState = previousLockState;
        Cursor.visible = previousCursorVisible;
    }
    #endregion

    #region Paging
    /// <summary>
    /// Builder wires the side arrows here. direction -1 = previous, +1 = next.
    /// </summary>
    public void ChangePage(int direction)
    {
        pageIndex = Mathf.Clamp(pageIndex + direction, 0, 3);
        Refresh();
    }

    private int CurrentTier => 4 - pageIndex;
    #endregion

    #region Rendering
    private void RefreshIfOpen()
    {
        if (isOpen) Refresh();
    }

    private void Refresh()
    {
        int tier = CurrentTier;

        if (titleText != null)
            titleText.text = "Memories of the Lost";
        if (pageLabelText != null)
            pageLabelText.text = $"Parchment {RomanPages[pageIndex]}   .   Tier {tier} Souls";

        ApplyParchmentArt(tier);

        if (previousPageButton != null)
            previousPageButton.interactable = pageIndex > 0;
        if (nextPageButton != null)
            nextPageButton.interactable = pageIndex < 3;

        List<SoulJournal.SoulRecord> records = SoulJournal.GetRecordsForTier(tier);

        for (int i = 0; i < entrySlots.Count; i++)
        {
            if (i < records.Count)
                FillSlot(entrySlots[i], records[i]);
            else if (entrySlots[i].root != null)
                entrySlots[i].root.SetActive(false);
        }
    }

    private void ApplyParchmentArt(int tier)
    {
        if (parchmentImage == null) return;

        int index = tier - 1;
        if (index >= 0 && index < tierParchmentSprites.Count && tierParchmentSprites[index] != null)
            parchmentImage.sprite = tierParchmentSprites[index];
    }

    private void FillSlot(EntrySlot slot, SoulJournal.SoulRecord record)
    {
        if (slot.root == null) return;
        slot.root.SetActive(true);

        if (slot.nameText != null)
            slot.nameText.text = record.soulName;
        if (slot.strikeLine != null)
            slot.strikeLine.enabled = record.strikeThrough;
        if (slot.storyText != null)
            slot.storyText.text = record.story;

        FillQuestBlock(slot, record);
        FillStatus(slot, record);
    }

    /// <summary>
    /// The quest block beside the story: quest name, current objective hint, and the
    /// track button. Hidden entirely when the soul never gave a quest.
    /// </summary>
    private void FillQuestBlock(EntrySlot slot, SoulJournal.SoulRecord record)
    {
        QuestData quest = QuestManager.Instance != null
            ? QuestManager.Instance.GetQuestForGiver(record.dialogueId)
            : null;

        bool hasQuest = quest != null;
        if (slot.questNameText != null)
        {
            slot.questNameText.gameObject.SetActive(hasQuest);
            if (hasQuest) slot.questNameText.text = quest.displayName;
        }

        QuestManager.QuestState state = hasQuest
            ? QuestManager.Instance.GetState(quest.questId)
            : QuestManager.QuestState.NotStarted;

        bool showHint = hasQuest && state != QuestManager.QuestState.Completed;
        if (slot.questHintText != null)
        {
            slot.questHintText.gameObject.SetActive(showHint);
            if (showHint) slot.questHintText.text = QuestManager.Instance.GetCurrentHint(quest.questId);
        }

        bool trackable = hasQuest
            && (state == QuestManager.QuestState.Active || state == QuestManager.QuestState.ReadyToTurnIn);
        if (slot.trackButton != null)
        {
            slot.trackButton.gameObject.SetActive(trackable);
            if (trackable)
            {
                bool tracked = QuestManager.Instance.TrackedQuestId == quest.questId;
                if (slot.trackLabel != null)
                    slot.trackLabel.text = tracked ? "Following" : "Follow the Glow";

                slot.trackButton.onClick.RemoveAllListeners();
                string id = quest.questId;
                slot.trackButton.onClick.AddListener(() => QuestManager.Instance.TrackQuest(id));
            }
        }
    }

    /// <summary>
    /// The status stamp: quest completion text takes priority (Found the Peace, LIAR!),
    /// a combat death stamps Destroyed, an unresolved soul shows nothing.
    /// </summary>
    private void FillStatus(EntrySlot slot, SoulJournal.SoulRecord record)
    {
        if (slot.statusText == null) return;

        bool hasStatus = record.status != SoulJournal.SoulStatus.Met
                      && !string.IsNullOrEmpty(record.statusText);
        slot.statusText.gameObject.SetActive(hasStatus);
        if (!hasStatus) return;

        slot.statusText.text = record.statusText;
        bool grim = record.status == SoulJournal.SoulStatus.Destroyed || record.strikeThrough;
        slot.statusText.color = grim ? destroyedColor : peaceColor;
    }
    #endregion
}
