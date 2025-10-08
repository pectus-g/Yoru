using UnityEngine;

/// <summary>
/// ScriptableObject that defines an item type
/// Create items via: Right-click in Project > Create > Inventory > InventoryItem
/// </summary>
[CreateAssetMenu(fileName = "New Item", menuName = "Inventory/InventoryItem")]
public class InventoryItem : ScriptableObject
{
    [Header("Basic Info")]
    public string itemName = "New Item";
    public string itemID = ""; // Unique identifier (e.g., "potion_health", "sake")
    
    [TextArea(3, 5)]
    public string description = "Item description here";
    
    public Sprite icon; // Item icon for inventory display
    
    [Header("Stack Settings")]
    public int maxStackSize = 99; // How many can stack in one slot
    
    [Header("Item Properties")]
    public bool isConsumable = false; // Can it be consumed/used?
    
    [Header("Item Effects (for future use)")]
    public ItemEffectType effectType = ItemEffectType.None;
    public float effectValue = 0f; // Heal amount, stamina restore, duration, etc.
    
    [Header("World Representation")]
    public GameObject worldPrefab; // The 3D model that appears in the world when dropped
}

/// <summary>
/// Types of effects items can have
/// Extend this enum as you add more item types
/// </summary>
public enum ItemEffectType
{
    None,           // No effect (just for storage)
    Heal,           // Restores health
    RestoreStamina, // Restores stamina
    TimidEffect,    // Makes enemy timid (sake effect)
    // Add more as needed
}