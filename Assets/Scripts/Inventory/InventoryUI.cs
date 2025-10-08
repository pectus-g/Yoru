using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;
using TMPro;
using Cinemachine;

public class InventoryUI : MonoBehaviour
{
    public static InventoryUI Instance { get; private set; }
    
    [Header("UI References")]
    [SerializeField] private GameObject inventoryPanel;
    [SerializeField] private GameObject dimBackground;
    [SerializeField] private Transform slotsParent;
    [SerializeField] private GameObject slotPrefab;
    
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
    private RectTransform inventoryRect;
    
    private NavMeshAgent playerNavAgent;
    private CinemachineBrain cinemachineBrain;
    
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
        
        cinemachineBrain = Camera.main?.GetComponent<CinemachineBrain>();
    }
    
    private void Start()
    {
        // Make sure everything starts closed and game is running
        if (inventoryPanel != null) inventoryPanel.SetActive(false);
        if (dimBackground != null) dimBackground.SetActive(false);
        if (previewPanel != null) previewPanel.SetActive(false);
        
        isInventoryOpen = false;
        Time.timeScale = 1f;
        
        // Subscribe to inventory changes
        if (InventoryManager.Instance != null)
        {
            InventoryManager.Instance.OnInventoryChanged += RefreshInventoryDisplay;
            RefreshInventoryDisplay();
        }
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
        if (Input.GetKeyDown(KeyCode.I))
        {
            ToggleInventory();
        }
        
        // Drop with X key when inventory is open and hovering over a slot
    if (isInventoryOpen && Input.GetKeyDown(KeyCode.X))
    {
        // Find which slot the mouse is over
        foreach (InventorySlotUI slotUI in activeSlots)
        {
            if (slotUI != null && RectTransformUtility.RectangleContainsScreenPoint(
                slotUI.GetComponent<RectTransform>(), 
                Input.mousePosition, 
                GetComponentInParent<Canvas>().worldCamera))
            {
                int slotIndex = activeSlots.IndexOf(slotUI);
                InventoryManager.Instance.DropItem(slotIndex);
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
        
        for (int i = 0; i < items.Count; i++)
        {
            CreateSlotUI(i, items[i]);
        }
        
        UpdateItemCount();
        
        if (emptyInventoryMessage != null)
        {
            emptyInventoryMessage.SetActive(items.Count == 0);
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
        isInventoryOpen = true;
        if (inventoryPanel != null) inventoryPanel.SetActive(true);
        if (dimBackground != null) dimBackground.SetActive(true);

        HideItemPreview();
        //  RefreshInventoryDisplay();

        if (pauseGameWhenOpen)
        {
            Time.timeScale = 0f;
            if (playerNavAgent != null) playerNavAgent.enabled = false;
            if (freezeCameraWhenOpen && cinemachineBrain != null) cinemachineBrain.enabled = false;

        }
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void CloseInventory()
    {
        isInventoryOpen = false;
        if (inventoryPanel != null) inventoryPanel.SetActive(false);
        if (dimBackground != null) dimBackground.SetActive(false);

        HideItemPreview();

        if (pauseGameWhenOpen)
        {
            Time.timeScale = 1f;
            if (playerNavAgent != null) playerNavAgent.enabled = true;
            if (freezeCameraWhenOpen && cinemachineBrain != null) cinemachineBrain.enabled = true;

        }
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
    
    public RectTransform GetInventoryRect()
    {
        return inventoryRect;
    }
    
    public bool IsInventoryOpen()
    {
        return isInventoryOpen;
    }
}