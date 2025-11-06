using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// Represents a single UI slot in the inventory grid
/// Handles display, drag and drop, hover preview, and user interaction
/// Attach this to each slot prefab
/// </summary>
public class InventorySlotUI : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, 
                                IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    [Header("UI References")]
    [SerializeField] private Image itemIcon;
    [SerializeField] private TextMeshProUGUI quantityText;
    [SerializeField] private Image slotBackground;
    
    [Header("Visual Settings")]
    [SerializeField] private Color emptySlotColor = new Color(0.2f, 0.2f, 0.2f, 0.5f);
    [SerializeField] private Color filledSlotColor = new Color(0.3f, 0.3f, 0.3f, 0.8f);
    [SerializeField] private Color hoverSlotColor = new Color(0.4f, 0.4f, 0.5f, 1f);
    
    private int slotIndex;
    private InventorySlot inventorySlot;
    private Canvas canvas;
    private CanvasGroup canvasGroup;
    private RectTransform rectTransform;
    private Vector2 originalPosition;
    private Transform originalParent;
    private bool isHovering = false;
    
    private void Awake()
    {
        canvas = GetComponentInParent<Canvas>();
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }
        rectTransform = GetComponent<RectTransform>();
    }
    
    /// <summary>
    /// Setup this slot with its data
    /// </summary>
    public void Setup(int index, InventorySlot slot)
    {
        slotIndex = index;
        inventorySlot = slot;
        UpdateDisplay();
    }
    
    /// <summary>
    /// Update the visual display based on slot data
    /// </summary>
    public void UpdateDisplay()
    {
        if (inventorySlot == null || inventorySlot.IsEmpty())
        {
            // Empty slot
            itemIcon.enabled = false;
            quantityText.text = "";
            slotBackground.color = isHovering ? hoverSlotColor : emptySlotColor;
        }
        else
        {
            // Filled slot
            itemIcon.enabled = true;
            itemIcon.sprite = inventorySlot.item.icon;
            
            // Show quantity only if more than 1
            quantityText.text = inventorySlot.quantity > 1 ? inventorySlot.quantity.ToString() : "";
            
            slotBackground.color = isHovering ? hoverSlotColor : filledSlotColor;
        }
    }
    
    #region Hover Events (for Preview Panel)
    
    public void OnPointerEnter(PointerEventData eventData)
    {
        isHovering = true;
        
        // Show preview panel on right side
        if (!inventorySlot.IsEmpty() && InventoryUI.Instance != null)
        {
            InventoryUI.Instance.ShowItemPreview(inventorySlot.item);
        }
        
        // Highlight this slot
        UpdateDisplay();
    }
    
    public void OnPointerExit(PointerEventData eventData)
    {
        isHovering = false;
        
        // Hide preview panel
        if (InventoryUI.Instance != null)
        {
            InventoryUI.Instance.HideItemPreview();
        }
        
        // Remove highlight
        UpdateDisplay();
    }
    
    #endregion
    
    #region Drag and Drop
    
    public void OnBeginDrag(PointerEventData eventData)
    {
        if (inventorySlot.IsEmpty()) return;
        
        // Hide preview when dragging
        if (InventoryUI.Instance != null)
        {
            InventoryUI.Instance.HideItemPreview();
        }
        
        // Store original position and parent
        originalPosition = rectTransform.anchoredPosition;
        originalParent = transform.parent;
        
        // Make draggable
        canvasGroup.alpha = 0.6f;
        canvasGroup.blocksRaycasts = false;
        
        // Move to root canvas for proper rendering
        transform.SetParent(canvas.transform);
    }
    
    public void OnDrag(PointerEventData eventData)
    {
        if (inventorySlot.IsEmpty()) return;
        
        // Follow mouse/touch
        rectTransform.anchoredPosition += eventData.delta / canvas.scaleFactor;
    }
    
    public void OnEndDrag(PointerEventData eventData)
    {
        if (inventorySlot.IsEmpty()) return;
        
        // Reset visual state
        canvasGroup.alpha = 1f;
        canvasGroup.blocksRaycasts = true;
        
        // Check if dropped outside inventory (drop item to world)
        if (!RectTransformUtility.RectangleContainsScreenPoint(
            InventoryUI.Instance.GetInventoryRect(), 
            Input.mousePosition, 
            canvas.worldCamera))
        {
            // Dropped outside inventory - drop to world
            InventoryManager.Instance.DropItem(slotIndex);
        }
        
        // Return to original position
        transform.SetParent(originalParent);
        rectTransform.anchoredPosition = originalPosition;
        
        // Restore hover state if mouse still over slot
        if (RectTransformUtility.RectangleContainsScreenPoint(rectTransform, Input.mousePosition, canvas.worldCamera))
        {
            isHovering = true;
            if (!inventorySlot.IsEmpty())
            {
                InventoryUI.Instance.ShowItemPreview(inventorySlot.item);
            }
        }
        
        UpdateDisplay();
    }
    
    #endregion
    
    #region Click Interaction
    
    public void OnPointerClick(PointerEventData eventData)
    {
        if (inventorySlot.IsEmpty()) return;
        
        // Right click or double click to use item
        if (eventData.button == PointerEventData.InputButton.Right || eventData.clickCount == 2)
        {
            if (inventorySlot.item.isConsumable)
            {
                InventoryManager.Instance.UseItem(slotIndex);
                
                // Update preview if slot becomes empty
                if (inventorySlot.IsEmpty())
                {
                    InventoryUI.Instance.HideItemPreview();
                }
                else
                {
                    // Update preview to show new quantity
                    InventoryUI.Instance.ShowItemPreview(inventorySlot.item);
                }
            }
            else
            {
                Debug.Log($"{inventorySlot.item.itemName} is not consumable!");
            }
        }
    }
    
    #endregion
    
    /// <summary>
    /// Called when 'D' key is pressed while this slot is selected
    /// </summary>
    public void DropItemByKey()
    {
        if (!inventorySlot.IsEmpty())
        {
            InventoryManager.Instance.DropItem(slotIndex);
            InventoryUI.Instance.HideItemPreview();
        }
    }
}