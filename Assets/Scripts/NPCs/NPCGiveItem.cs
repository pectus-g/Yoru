using UnityEngine;

/// <summary>
/// NPC that gives an item to the player when interacted with
/// Example: Rabbit gives potion
/// Attach this to your NPC GameObject
/// </summary>
public class NPCGiveItem : NPCInteraction
{
    [Header("Item to Give")]
    [SerializeField] private InventoryItem itemToGive;
    [SerializeField] private int quantityToGive = 1;
    
    [Header("Dialogue (Optional)")]
    [SerializeField] private string beforeGiveText = "Here, take this!";
    [SerializeField] private string afterGiveText = "I hope it helps!";
    [SerializeField] private string alreadyGaveText = "I already gave you something!";
    
    public override void Interact()
    {
        if (!CanInteract())
        {
            // Already gave item (if not repeatable)
            Debug.Log($"{npcName}: {alreadyGaveText}");
            return;
        }
        
        if (itemToGive == null)
        {
            Debug.LogWarning($"{npcName} has no item to give!");
            return;
        }
        
        // Show dialogue before giving
        Debug.Log($"{npcName}: {beforeGiveText}");
        
        // Give item to player
        bool success = InventoryManager.Instance.AddItem(itemToGive, quantityToGive);
        
        if (success)
        {
            Debug.Log($"{npcName} gave you {quantityToGive}x {itemToGive.itemName}!");
            Debug.Log($"{npcName}: {afterGiveText}");
            
            hasInteracted = true;
            
            // TODO: Add dialogue UI, animations, sound effects here later
        }
        else
        {
            Debug.Log($"{npcName}: Your inventory is full!");
        }
    }
    
    public override string GetPromptText()
    {
        if (CanInteract())
        {
            return $"Press E to talk to {npcName}";
        }
        else
        {
            return $"{npcName} (already helped you)";
        }
    }
}