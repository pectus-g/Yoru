using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;
using TMPro;

public class InventoryUI : MonoBehaviour
{
    public static InventoryUI Instance { get; private set; }
    
    [Header("UI References")]
    [SerializeField] private GameObject inventoryPanel;
    [SerializeField] private GameObject dimBackground;
    [SerializeField] private Transform slotsParent;
    [SerializeField] private GameObject slotPrefab;

    [Header("Pages (Items / Quest)")]
    [SerializeField] private Button itemsTabButton;
    [SerializeField] private Button questTabButton;
    
    [Header("Preview Panel (Right Side)")]
    [SerializeField] private GameObject previewPanel;
    [SerializeField] private Image previewItemIcon;
    [SerializeField] private TextMeshProUGUI previewItemName;
    [SerializeField] private TextMeshProUGUI previewItemDescription;
    [SerializeField] private TextMeshProUGUI previewItemStats;
    
    [Header("Empty State (Optional)")]
    [SerializeField] private GameObject emptyInventoryMessage;
    [SerializeField] private TextMeshProUGUI itemCountText;
    
    [Header("Pause Settings")]
    [SerializeField] private bool pauseGameWhenOpen = true;
    [SerializeField] private bool freezeCameraWhenOpen = true;
    
    private List<InventorySlotUI> activeSlots = new List<InventorySlotUI>();
    private bool isInventoryOpen = false;
    private ItemCategory currentPage = ItemCategory.Items;
    private RectTransform inventoryRect;
    
    private NavMeshAgent playerNavAgent;
    private bool navAgentWasEnabled;
    private ThirdPersonCamera cameraController;
    
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }
        
        if (inventoryPanel != null)
        {
            inventoryRect = inventoryPanel.GetComponent<RectTransform>();
        }
        
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            playerNavAgent = player.GetComponent<NavMeshAgent>();
        }
        
        // Find camera controller
        cameraController = FindObjectOfType<ThirdPersonCamera>();
        if (cameraController == null)
        {
            Debug.LogWarning("ThirdPersonCamera not found in scene!");
        }
    }
    
    private void Start()
    {
        if (inventoryPanel != null) inventoryPanel.SetActive(false);
        if (dimBackground != null) dimBackground.SetActive(false);
        if (previewPanel != null) previewPanel.SetActive(false);
        
        isInventoryOpen = false;
        Time.timeScale = 1f;
        
        if (itemsTabButton != null) itemsTabButton.onClick.AddListener(() => SwitchPage(ItemCategory.Items));
        if (questTabButton != null) questTabButton.onClick.AddListener(() => SwitchPage(ItemCategory.Quest));
        SetTabLabel(itemsTabButton, "Items");
        SetTabLabel(questTabButton, "Quest");
        StyleTab(itemsTabButton);
        StyleTab(questTabButton);

        if (InventoryManager.Instance != null)
        {
            InventoryManager.Instance.OnInventoryChanged += RefreshInventoryDisplay;
            RefreshInventoryDisplay();
        }

        RefreshTabVisuals();
    }
    
    private void OnDestroy()
    {
        if (InventoryManager.Instance != null)
        {
            InventoryManager.Instance.OnInventoryChanged -= RefreshInventoryDisplay;
        }
    }
    
    private void Update()
    {
        // While the 3D item view is open it owns all input (drag to rotate, scroll to
        // zoom, double-click or I to close). The bag stays paused underneath, so do not
        // toggle or drop here until the inspect view hands control back.
        if (ItemExamineController.IsExamining) return;

        // The inspector just closed THIS frame (I or double-click). Swallow this frame's
        // input so the same press does not also toggle the bag. Update order between the
        // two is not guaranteed; this is what makes I reliably go back to the grid.
        if (ItemExamineController.ConsumedCloseThisFrame) return;

        if (Input.GetKeyDown(KeyCode.I))
        {
            ToggleInventory();
        }

        if (isInventoryOpen)
        {
            // Tab cycles between the Items and Quest pages.
            if (Input.GetKeyDown(KeyCode.Tab))
                SwitchPage(currentPage == ItemCategory.Items ? ItemCategory.Quest : ItemCategory.Items);
        }

        if (isInventoryOpen && Input.GetKeyDown(KeyCode.X))
        {
            foreach (InventorySlotUI slotUI in activeSlots)
            {
                if (slotUI != null && RectTransformUtility.RectangleContainsScreenPoint(
                    slotUI.GetComponent<RectTransform>(), 
                    Input.mousePosition, 
                    GetComponentInParent<Canvas>().worldCamera))
                {
                    // Pages filter the display: ask the slot for its index in the
                    // manager's FULL list, never its position on screen.
                    InventoryManager.Instance.DropItem(slotUI.SlotIndex);
                    break;
                }
            }
        }
    }
    
    private void RefreshInventoryDisplay()
    {
        foreach (InventorySlotUI slotUI in activeSlots)
        {
            if (slotUI != null) Destroy(slotUI.gameObject);
        }
        activeSlots.Clear();
        
        List<InventorySlot> items = InventoryManager.Instance.GetInventorySlots();
        
        // Only the current page's category is shown; the slot keeps its index into
        // the manager's FULL list so drop/use still target the right item.
        for (int i = 0; i < items.Count; i++)
        {
            if (items[i].item == null || items[i].item.category != currentPage) continue;
            CreateSlotUI(i, items[i]);
        }
        
        UpdateItemCount();
        
        if (emptyInventoryMessage != null)
        {
            emptyInventoryMessage.SetActive(activeSlots.Count == 0);
        }
    }
    
    private void CreateSlotUI(int index, InventorySlot slot)
    {
        if (slotPrefab == null || slotsParent == null)
        {
            Debug.LogError("Slot prefab or slots parent not assigned!");
            return;
        }
        
        GameObject slotObj = Instantiate(slotPrefab, slotsParent);
        InventorySlotUI slotUI = slotObj.GetComponent<InventorySlotUI>();
        
        if (slotUI != null)
        {
            slotUI.Setup(index, slot);
            activeSlots.Add(slotUI);
        }
    }
    
    private void UpdateItemCount()
    {
        if (itemCountText != null)
        {
            int current = InventoryManager.Instance.GetItemCount();
            itemCountText.text = $"Items: {current}/20";
        }
    }
    
    public void ShowItemPreview(InventoryItem item)
    {
        if (item == null || previewPanel == null)
        {
            HideItemPreview();
            return;
        }
        
        previewPanel.SetActive(true);
        
        if (previewItemIcon != null)
        {
            previewItemIcon.sprite = item.icon;
            previewItemIcon.enabled = item.icon != null;
        }
        
        if (previewItemName != null)
        {
            previewItemName.text = item.itemName;
        }
        
        if (previewItemDescription != null)
        {
            previewItemDescription.text = item.description;
        }
        
        if (previewItemStats != null)
        {
            string statsText = "";
            if (item.isConsumable) statsText += "<color=green>Consumable</color>\n";
            if (item.effectType != ItemEffectType.None) statsText += $"Effect: {item.effectType}\n";
            if (item.effectValue > 0) statsText += $"Value: {item.effectValue}\n";
            statsText += $"Max Stack: {item.maxStackSize}";
            previewItemStats.text = statsText;
        }
    }
    
    public void HideItemPreview()
    {
        if (previewPanel != null) previewPanel.SetActive(false);
    }
    
    public void ToggleInventory()
    {
        if (isInventoryOpen) CloseInventory();
        else OpenInventory();
    }

    public void OpenInventory()
    {
        if (isInventoryOpen) return;
        // The inventory follows the same menu rules as the Memory Parchments: never
        // mid-combat, never mid-dialogue, never over another menu. While open, the
        // player is frozen and damage-immune (MenuGuard).
        if (!MenuGuard.CanOpenMenu())
        {
            Debug.Log("[Inventory] Open blocked (combat, dialogue, or another menu).");
            return;
        }

        isInventoryOpen = true;
        MenuGuard.Register();
        if (inventoryPanel != null) inventoryPanel.SetActive(true);
        if (dimBackground != null) dimBackground.SetActive(true);

        HideItemPreview();
        RefreshTabVisuals();

        if (pauseGameWhenOpen)
        {
            Time.timeScale = 0f;
            // Remember the agent's real state, then switch it off while paused. It is
            // restored to THIS value on close, never blindly forced on. PlayerMovement
            // disables the agent at startup on purpose (the player is CharacterController
            // driven), so a hardcoded "enabled = true" on close would wake an agent that
            // must stay asleep and let it fight the controller for the transform.
            if (playerNavAgent != null)
            {
                navAgentWasEnabled = playerNavAgent.enabled;
                playerNavAgent.enabled = false;
            }
            
            if (freezeCameraWhenOpen && cameraController != null)
            {
                cameraController.SetCameraEnabled(false);
            }
        }
        
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void CloseInventory()
    {
        if (!isInventoryOpen) return;
        isInventoryOpen = false;
        MenuGuard.Unregister();
        if (inventoryPanel != null) inventoryPanel.SetActive(false);
        if (dimBackground != null) dimBackground.SetActive(false);

        HideItemPreview();

        if (pauseGameWhenOpen)
        {
            Time.timeScale = 1f;
            // Restore the agent to whatever it was before we opened (disabled during
            // normal play), never force it on.
            if (playerNavAgent != null) playerNavAgent.enabled = navAgentWasEnabled;
            
            if (freezeCameraWhenOpen && cameraController != null)
            {
                cameraController.SetCameraEnabled(true);
            }
        }
        
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
    
    #region Pages + Call Yuki
    /// <summary>
    /// Show one page of the bag: Items (everyday) or Quest (protected, glowing frames).
    /// </summary>
    private void SwitchPage(ItemCategory page)
    {
        if (currentPage == page) return;
        currentPage = page;
        HideItemPreview();
        RefreshInventoryDisplay();
        RefreshTabVisuals();
    }

    /// <summary>
    /// Tabs style themselves: dark translucent fill, thin gold outline, soft white
    /// label. The ACTIVE tab is the non-interactable one, so the disabled tint is set
    /// to gold: selected reads as lit, unselected as dark. No manual styling needed.
    /// </summary>
    private static void StyleTab(Button button)
    {
        if (button == null) return;

        TextMeshProUGUI text = button.GetComponentInChildren<TextMeshProUGUI>(true);
        if (text != null) text.color = new Color(0.95f, 0.93f, 0.88f, 1f);

        Image fill = button.image;
        if (fill != null)
        {
            fill.color = new Color(0.08f, 0.08f, 0.1f, 0.75f);

            Outline outline = fill.GetComponent<Outline>();
            if (outline == null) outline = fill.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.95f, 0.85f, 0.55f, 0.5f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);
            outline.useGraphicAlpha = false;
        }

        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.3f, 1.3f, 1.3f, 1f);
        colors.disabledColor = new Color(2.2f, 1.9f, 1.1f, 1f); // selected tab = gold lit
        button.colors = colors;
    }

    /// <summary>
    /// Tabs write their own labels so the default "Button" text can never ship.
    /// </summary>
    private static void SetTabLabel(Button button, string label)
    {
        if (button == null) return;
        TextMeshProUGUI text = button.GetComponentInChildren<TextMeshProUGUI>(true);
        if (text != null) text.text = label;
    }

    /// <summary>
    /// The active tab reads as pressed (non-interactable).
    /// </summary>
    private void RefreshTabVisuals()
    {
        if (itemsTabButton != null) itemsTabButton.interactable = currentPage != ItemCategory.Items;
        if (questTabButton != null) questTabButton.interactable = currentPage != ItemCategory.Quest;
    }
    #endregion

    public RectTransform GetInventoryRect()
    {
        return inventoryRect;
    }
    
    public bool IsInventoryOpen()
    {
        return isInventoryOpen;
    }
}