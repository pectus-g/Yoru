using UnityEngine;
using TMPro;

/// <summary>
/// Handles player interaction with items and NPCs
/// Attach this to your Player GameObject
/// </summary>
public class PlayerInteraction : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject interactionPromptUI; // Panel with prompt text
    [SerializeField] private TextMeshProUGUI promptText; // "Press E to pick up..."
    
    [Header("Interaction Settings")]
    [SerializeField] private KeyCode interactionKey = KeyCode.E;
    
    // Track what's nearby (only one at a time for priority)
    private ItemPickup nearbyItem = null;
    private NPCInteraction nearbyNPC = null;
    private QuestTrigger nearbyQuestTrigger = null;
    
    private void Start()
    {
        // Hide prompt at start
        if (interactionPromptUI != null)
        {
            interactionPromptUI.SetActive(false);
        }
    }
    
    private void Update()
    {
        // Press E to interact
        if (Input.GetKeyDown(interactionKey))
        {
            TryInteract();
        }
        
        // Update prompt display
        UpdatePrompt();
    }
    
    /// <summary>
    /// Attempt to interact with whatever is nearby
    /// Priority: NPC > Item
    /// </summary>
    private void TryInteract()
    {
        // Don't interact if inventory is open
        if (InventoryUI.Instance != null && InventoryUI.Instance.IsInventoryOpen())
        {
            return;
        }
        
        // Priority: NPC first, then quest interactables, then items
        if (nearbyNPC != null)
        {
            nearbyNPC.Interact();
        }
        else if (nearbyQuestTrigger != null)
        {
            nearbyQuestTrigger.Use();
        }
        else if (nearbyItem != null)
        {
            nearbyItem.TryPickup();
        }
    }
    
    /// <summary>
    /// Update the interaction prompt text
    /// </summary>
    private void UpdatePrompt()
    {
        if (interactionPromptUI == null || promptText == null) return;
        
        // Don't show prompts if inventory is open
        if (InventoryUI.Instance != null && InventoryUI.Instance.IsInventoryOpen())
        {
            interactionPromptUI.SetActive(false);
            return;
        }
        
        // Show prompt based on what's nearby
        if (nearbyNPC != null)
        {
            interactionPromptUI.SetActive(true);
            promptText.text = nearbyNPC.GetPromptText();
        }
        else if (nearbyQuestTrigger != null)
        {
            interactionPromptUI.SetActive(true);
            promptText.text = nearbyQuestTrigger.GetPromptText();
        }
        else if (nearbyItem != null)
        {
            interactionPromptUI.SetActive(true);
            promptText.text = nearbyItem.BagHasRoom()
                ? $"Press E to pick up {nearbyItem.GetItem().itemName}"
                : "Bag is full!";
        }
        else
        {
            interactionPromptUI.SetActive(false);
        }
    }
    
    #region Item Detection
    
    /// <summary>
    /// Called by ItemPickup when player enters its trigger
    /// </summary>
    public void SetNearbyItem(ItemPickup item)
    {
        nearbyItem = item;
    }
    
    /// <summary>
    /// Called by ItemPickup when player exits its trigger
    /// </summary>
    public void ClearNearbyItem(ItemPickup item)
    {
        if (nearbyItem == item)
        {
            nearbyItem = null;
        }
    }
    
    #endregion
    
    #region Quest Trigger Detection
    
    /// <summary>
    /// Called by QuestTrigger (PRESS_E mode) when the player is in range AND the
    /// matching quest step is current. Same registration pattern as ItemPickup.
    /// </summary>
    public void SetNearbyQuestTrigger(QuestTrigger trigger)
    {
        nearbyQuestTrigger = trigger;
    }
    
    /// <summary>
    /// Called by QuestTrigger when the player leaves range or the step passes.
    /// </summary>
    public void ClearNearbyQuestTrigger(QuestTrigger trigger)
    {
        if (nearbyQuestTrigger == trigger)
        {
            nearbyQuestTrigger = null;
        }
    }
    
    #endregion
    
    #region NPC Detection
    
    /// <summary>
    /// Called by NPCInteraction when player enters its trigger
    /// </summary>
    public void SetNearbyNPC(NPCInteraction npc)
    {
        nearbyNPC = npc;
    }
    
    /// <summary>
    /// Called by NPCInteraction when player exits its trigger
    /// </summary>
    public void ClearNearbyNPC(NPCInteraction npc)
    {
        if (nearbyNPC == npc)
        {
            nearbyNPC = null;
        }
    }
    
    #endregion
}