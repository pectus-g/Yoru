using UnityEngine;

/// <summary>
/// NPC that takes a specific item from the player when interacted with
/// Example: Enemy takes sake and becomes weaker
/// Attach this to your NPC/Enemy GameObject
/// </summary>
public class NPCTakeItem : NPCInteraction
{
    [Header("Item to Take")]
    [SerializeField] private InventoryItem requiredItem;
    [SerializeField] private int quantityRequired = 1;
    
    [Header("Dialogue")]
    [SerializeField] private string needItemText = "I need something...";
    [SerializeField] private string acceptItemText = "Ahh, thank you...";
    [SerializeField] private string noItemText = "You don't have what I need.";
    [SerializeField] private string alreadyReceivedText = "I already received it.";
    
    [Header("Effects After Receiving Item")]
    [SerializeField] private bool destroyAfterReceiving = false;
    [SerializeField] private bool disableAfterReceiving = true;
    
    // Optional: Reference to enemy script for weakening effects
    // [SerializeField] private EnemyController enemyController;
    
    public override void Interact()
    {
        if (!CanInteract())
        {
            Debug.Log($"{npcName}: {alreadyReceivedText}");
            return;
        }
        
        if (requiredItem == null)
        {
            Debug.LogWarning($"{npcName} has no required item set!");
            return;
        }
        
        // Check if player has the required item
        int playerItemCount = InventoryManager.Instance.GetItemQuantity(requiredItem);
        
        if (playerItemCount >= quantityRequired)
        {
            // Player has the item - take it
            bool success = InventoryManager.Instance.RemoveItem(requiredItem, quantityRequired);
            
            if (success)
            {
                Debug.Log($"{npcName}: {acceptItemText}");
                Debug.Log($"You gave {quantityRequired}x {requiredItem.itemName} to {npcName}");
                
                hasInteracted = true;
                
                // Apply effects (for now just log, later implement actual effects)
                OnItemReceived();
                
                // Handle post-receive behavior
                if (destroyAfterReceiving)
                {
                    Destroy(gameObject, 1f); // Delay to allow dialogue/effects
                }
                else if (disableAfterReceiving)
                {
                    gameObject.SetActive(false);
                }
            }
        }
        else
        {
            // Player doesn't have the item
            Debug.Log($"{npcName}: {noItemText}");
        }
    }
    
    /// <summary>
    /// Called when item is successfully received
    /// Override or extend this for specific effects
    /// </summary>
    protected virtual void OnItemReceived()
    {
        Debug.Log($"{npcName} received {requiredItem.itemName} - applying effects...");
        
        // TODO: Implement actual effects here later
        // Example for sake effect on enemy:
        // if (requiredItem.effectType == ItemEffectType.TimidEffect)
        // {
        //     if (enemyController != null)
        //     {
        //         enemyController.BecomeWeak();
        //     }
        // }
        
        // For now, just log what would happen
        if (requiredItem.effectType == ItemEffectType.TimidEffect)
        {
            Debug.Log($"{npcName} becomes timid and weak!");
        }
    }
    
    public override string GetPromptText()
    {
        if (!CanInteract())
        {
            return ""; // Don't show prompt if already received
        }
        
        // Check if player has the item
        bool hasItem = InventoryManager.Instance.HasItem(requiredItem);
        
        if (hasItem)
        {
            return $"Press E to give {requiredItem.itemName} to {npcName}";
        }
        else
        {
            // Optional: Show that NPC needs something, or hide prompt
            return ""; // Empty = no prompt shown
            // Or: return $"{npcName} needs {requiredItem.itemName}";
        }
    }
}