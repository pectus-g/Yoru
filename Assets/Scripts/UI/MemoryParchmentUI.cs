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
        public Image highlight;
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

    [Header("Call Yuki (top right)")]
    [Tooltip("Hand-made Button - TextMeshPro under the panel root, top right corner. Shown while the parchments are open; grayed out when Yuki cannot come (no quest marked, already near the ring, or not Tomoe)")]
    [SerializeField] private Button callYukiButton;

    [Tooltip("Optional per-tier parchment art, index 0 = Tier 1 ... index 3 = Tier 4. Empty entries keep the default sprite")]
    [SerializeField] private List<Sprite> tierParchmentSprites = new List<Sprite>();

    [Header("HUD Icon (Wired By Builder)")]
    [Tooltip("The small parchment icon pinned bottom-left. Hidden while the parchments are open")]
    [SerializeField] private GameObject hudIconRoot;

    [Tooltip("Soft gold glow behind the icon. Pulses while a new quest has not been looked at yet")]
    [SerializeField] private Image hudGlowImage;

    [Header("Audio (assign your own clips)")]
    [Tooltip("Wired by the builder. Plays the two clips below")]
    [SerializeField] private AudioSource audioSource;

    [Tooltip("Played every time the parchments open")]
    [SerializeField] private AudioClip openSound;

    [Tooltip("Played when the parchments open showing a freshly taken quest, layered with the entry shine")]
    [SerializeField] private AudioClip newQuestSound;

    [Header("New Quest Shine")]
    [Tooltip("How long the new quest's entry frame shines before fading out")]
    [SerializeField] private float shineDuration = 2.5f;

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
    private bool hasUnseenQuest;
    private QuestData pendingNewQuest;   // the quest the parchment opens onto, with shine
    private QuestData shineQuest;        // whose entry frame is currently shining
    private Image shineHighlight;        // rebound every Refresh by FillQuestBlock
    private float shineTimer;
    private CursorLockMode previousLockState;
    private bool previousCursorVisible;
    private TextMeshProUGUI callYukiLabel; // cached by StyleCallYukiButton

    private static readonly string[] RomanPages = { "I", "II", "III", "IV" };
    #endregion

    #region Lifecycle
    private void Start()
    {
        if (panelRoot != null)
            panelRoot.SetActive(false);

        SoulJournal.OnJournalChanged += RefreshIfOpen;
        if (QuestManager.Instance != null)
        {
            QuestManager.Instance.OnQuestsChanged += RefreshIfOpen;
            QuestManager.Instance.OnQuestGiven += HandleQuestGiven;
        }

        if (callYukiButton != null)
        {
            callYukiButton.onClick.AddListener(OnCallYukiClicked);
            StyleCallYukiButton();
        }

        UpdateHudGlow();
    }

    private void OnDestroy()
    {
        SoulJournal.OnJournalChanged -= RefreshIfOpen;
        if (QuestManager.Instance != null)
        {
            QuestManager.Instance.OnQuestsChanged -= RefreshIfOpen;
            QuestManager.Instance.OnQuestGiven -= HandleQuestGiven;
        }
    }

    private void HandleQuestGiven(QuestData quest)
    {
        // The icon glows until the player looks; the next open jumps to this quest.
        hasUnseenQuest = true;
        pendingNewQuest = quest;
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

        // Lit only when she can actually come; the gray button doubles as the hint
        // that the marked quest's ring is already close by.
        if (isOpen && callYukiButton != null)
        {
            bool canCall = YukiGuide.Instance != null && YukiGuide.Instance.CanBeCalled();
            callYukiButton.interactable = canCall;
            if (callYukiLabel != null)
                callYukiLabel.alpha = canCall ? 1f : 0.35f;
        }

        UpdateHudGlow();
    }

    /// <summary>
    /// The bottom-left parchment icon: hidden while the parchments are open, and its
    /// glow breathes gold while a quest is waiting to be looked at.
    /// </summary>
    private void UpdateHudGlow()
    {
        if (hudIconRoot != null && hudIconRoot.activeSelf == isOpen)
            hudIconRoot.SetActive(!isOpen);

        if (hudGlowImage != null)
        {
            float alpha = hasUnseenQuest
                ? 0.4f + 0.3f * Mathf.Sin(Time.time * 3.5f)
                : 0f;
            Color c = hudGlowImage.color;
            if (!Mathf.Approximately(c.a, alpha))
            {
                c.a = alpha;
                hudGlowImage.color = c;
            }
        }

        UpdateEntryShine();
    }

    /// <summary>
    /// The freshly taken quest's entry frame shines, then fades over shineDuration.
    /// FillQuestBlock rebinds shineHighlight every Refresh, so paging away and back
    /// keeps the remaining shine on the right entry.
    /// </summary>
    private void UpdateEntryShine()
    {
        if (shineQuest == null) return;

        shineTimer -= Time.deltaTime;
        if (shineTimer <= 0f)
        {
            if (shineHighlight != null)
                SetImageAlpha(shineHighlight, 0f);
            shineQuest = null;
            shineHighlight = null;
            return;
        }

        if (shineHighlight != null)
            SetImageAlpha(shineHighlight, 0.85f * (shineTimer / Mathf.Max(0.01f, shineDuration)));
    }

    private static void SetImageAlpha(Image image, float alpha)
    {
        Color c = image.color;
        c.a = alpha;
        image.color = c;
    }

    private void PlaySound(AudioClip clip)
    {
        if (audioSource != null && clip != null)
            audioSource.PlayOneShot(clip);
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
        if (!MenuGuard.CanOpenMenu())
        {
            Debug.Log("[Parchment] Open blocked (combat, dialogue, or another menu).");
            return;
        }

        isOpen = true;
        hasUnseenQuest = false; // looked at: the icon stops glowing
        MenuGuard.Register();   // freezes the player, grants menu damage immunity

        // A freshly taken quest pulls the parchment straight to its page.
        QuestData newQuest = pendingNewQuest;
        pendingNewQuest = null;
        if (newQuest != null)
            pageIndex = Mathf.Clamp(4 - newQuest.tier, 0, 3);

        panelRoot.SetActive(true);

        previousLockState = Cursor.lockState;
        previousCursorVisible = Cursor.visible;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (newQuest != null)
        {
            shineQuest = newQuest;
            shineTimer = shineDuration;
        }

        Refresh();

        PlaySound(openSound);
        if (newQuest != null)
            PlaySound(newQuestSound);
    }

    /// <summary>
    /// Close and hand the cursor back to gameplay.
    /// </summary>
    public void Close()
    {
        if (!isOpen) return;

        isOpen = false;
        MenuGuard.Unregister();
        if (panelRoot != null)
            panelRoot.SetActive(false);

        Cursor.lockState = previousLockState;
        Cursor.visible = previousCursorVisible;
    }
    #endregion

    #region Call Yuki
    /// <summary>
    /// Make the hand-made button look like it belongs on the parchment: ink-coloured
    /// label in the parchment's own font, near-transparent fill, thin ink frame. Runs
    /// once at Start so no manual styling is ever needed.
    /// </summary>
    private void StyleCallYukiButton()
    {
        // Ink and font are borrowed from the page label the builder already styled,
        // so the button always matches whatever the parchment looks like.
        Color ink = pageLabelText != null ? pageLabelText.color : new Color(0.24f, 0.17f, 0.10f, 1f);

        callYukiLabel = callYukiButton.GetComponentInChildren<TextMeshProUGUI>(true);
        if (callYukiLabel != null)
        {
            callYukiLabel.text = "Call Yuki";
            callYukiLabel.color = ink;
            if (pageLabelText != null)
            {
                callYukiLabel.font = pageLabelText.font;
                callYukiLabel.fontSize = pageLabelText.fontSize;
            }
        }

        Image frame = callYukiButton.image;
        if (frame != null)
        {
            // Near-invisible fill: the parchment shows through, only the ink remains.
            frame.color = new Color(ink.r, ink.g, ink.b, 0.06f);

            Outline outline = frame.GetComponent<Outline>();
            if (outline == null)
                outline = frame.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(ink.r, ink.g, ink.b, 0.6f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);
            outline.useGraphicAlpha = false; // the frame stays visible over the faint fill
        }

        // Disabled = faded, like dried ink.
        ColorBlock colors = callYukiButton.colors;
        colors.disabledColor = new Color(1f, 1f, 1f, 0.35f);
        callYukiButton.colors = colors;
    }

    /// <summary>
    /// Close the parchments first (releases MenuGuard and the cursor), then summon
    /// Yuki beside Tomoe to lead the way to the marked quest's current ring.
    /// </summary>
    private void OnCallYukiClicked()
    {
        if (YukiGuide.Instance == null || !YukiGuide.Instance.CanBeCalled()) return;
        Close();
        YukiGuide.Instance.CallYuki();
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

        if (slot.highlight != null)
            SetImageAlpha(slot.highlight, 0f);
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
        if (hasQuest && quest == shineQuest)
            shineHighlight = slot.highlight;
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

/// <summary>
/// The soft-pause for menus (Memory Parchments, Inventory, future screens), the way
/// living-world action games handle it: the world keeps breathing (COZY weather, leaves,
/// ambience all run), but the player is frozen and damage-immune while any menu is open,
/// and menus refuse to open mid-combat or mid-dialogue.
///
/// Static on purpose: every menu calls CanOpenMenu before opening, Register on open,
/// Unregister on close. Register freezes PlayerMovement (component disabled, sleep-intro
/// pattern); attack input is gated inside PlayerCombat by IsAnyMenuOpen; PlayerHealth
/// checks IsAnyMenuOpen as a damage gate. PlayerMovement.cs itself is untouched.
/// </summary>
public static class MenuGuard
{
    private static int openMenus;
    private static PlayerMovement frozenMovement;

    /// <summary>True while any registered menu is on screen. PlayerHealth and PlayerCombat gate on this.</summary>
    public static bool IsAnyMenuOpen => openMenus > 0;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewSession()
    {
        openMenus = 0;
        frozenMovement = null;
    }

    /// <summary>
    /// May a menu open right now? No while another menu is open, a conversation is on
    /// screen, or an enemy is engaged with the player (same check as the transform lock).
    /// </summary>
    public static bool CanOpenMenu()
    {
        if (IsAnyMenuOpen) return false;
        if (DialogueManager.Instance != null && DialogueManager.Instance.IsDialogueActive) return false;

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        PlayerCombat combat = player != null ? player.GetComponent<PlayerCombat>() : null;
        if (combat != null && combat.IsEngagedInCombat()) return false;

        return true;
    }

    /// <summary>Menu opened: freeze the player on the first open.</summary>
    public static void Register()
    {
        openMenus++;
        if (openMenus != 1) return;

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null) return;

        PlayerMovement movement = player.GetComponent<PlayerMovement>();
        if (movement != null && movement.enabled)
        {
            movement.enabled = false;
            frozenMovement = movement;
        }
    }

    /// <summary>Menu closed: unfreeze when the last one closes. Only restores what Register froze.</summary>
    public static void Unregister()
    {
        openMenus = Mathf.Max(0, openMenus - 1);
        if (openMenus != 0) return;

        if (frozenMovement != null)
        {
            frozenMovement.enabled = true;
            frozenMovement = null;
        }
    }
}
