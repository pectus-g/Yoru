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
    [Tooltip("Particle/glow effect shown on this item while it sits in the world, to draw the player's eye. It vanishes when the item is collected. A REGULAR item loses this glow when dropped back; a QUEST item keeps glowing even on the ground. Assign ANY prefab (a purchased effect, a halo, a light, or your own particle system). Use a looping effect, not a one-shot. Leave empty for no glow")]
    public GameObject attractParticle;

    [Tooltip("Scale multiplier for the attract particle, for when a purchased effect is too big or small for this item. 1 keeps the effect's own size")]
    public float attractParticleScale = 1f;

    [Tooltip("Nudge for the attract particle. With 'Attract Particle Cover Item' on, this shifts it from the item's center (raise Y to lift it, X and Z to slide it). With that off, it is measured from the item's pivot")]
    public Vector3 attractParticleOffset = Vector3.zero;

    [Tooltip("When on, the glow is CENTERED on the item so it can cover the whole body. Then raise Attract Particle Scale until the effect wraps the item the way you want. When off, the glow sits at the item's pivot instead")]
    public bool attractParticleCoverItem = true;

    [Tooltip("When on, the glow turns to face the camera every frame, so it reads correctly from any viewing angle. Recommended on for flat or sprite-style effects")]
    public bool attractParticleFaceCamera = true;
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