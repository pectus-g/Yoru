using UnityEngine;

/// <summary>
/// ScriptableObject that defines an item type
/// Create items via: Right-click in Project > Create > Inventory > InventoryItem
/// </summary>
[CreateAssetMenu(fileName = "New Item", menuName = "YORU/Inventory Item")]
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

    [Header("Category")]
    [Tooltip("Items: food, sake, consumables (everyday page). Quest: important things souls need; shown on the bag's Quest page with a glowing frame, cannot be dropped or consumed")]
    public ItemCategory category = ItemCategory.Items;
    
    [Header("Item Effects (for future use)")]
    public ItemEffectType effectType = ItemEffectType.None;
    public float effectValue = 0f; // Heal amount, stamina restore, duration, etc.
    
    [Header("World Representation")]
    public GameObject worldPrefab; // The 3D model that appears in the world when dropped

    [Header("World Particle")]
    [Tooltip("Looping particle effect shown on this item while it sits in the world, to draw the player's eye. It vanishes when the item is collected. A REGULAR item loses this glow when dropped back; a QUEST item keeps glowing even on the ground. Assign a ParticleSystem prefab; leave empty for no glow")]
    public GameObject attractParticle;
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

/// <summary>
/// Which page of the bag an item lives on.
/// Quest items are protected: glowing slot frame, cannot be dropped or consumed;
/// they only leave the bag when a quest hands them over at turn-in.
/// </summary>
public enum ItemCategory
{
    Items, // Food, sake, consumables - the everyday page
    Quest  // Important things souls need - the protected page
}