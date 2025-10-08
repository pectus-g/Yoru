using UnityEngine;

/// <summary>
/// Attach this to any item object in the world that can be picked up
/// Requires a Collider with "Is Trigger" enabled
/// </summary>
[RequireComponent(typeof(Collider))]
public class ItemPickup : MonoBehaviour
{
    [Header("Item Data")]
    [SerializeField] private InventoryItem item; // Reference to the item ScriptableObject
    [SerializeField] private int quantity = 1;
    
    [Header("Pickup Settings")]
    [SerializeField] private float pickupRadius = 2f; // Trigger size
    
    private Collider triggerCollider;
    private bool playerInRange = false;
    
    private void Awake()
    {
        // Setup trigger collider
        triggerCollider = GetComponent<Collider>();
        triggerCollider.isTrigger = true;
        
        // If it's a sphere collider, set the radius
        if (triggerCollider is SphereCollider sphereCollider)
        {
            sphereCollider.radius = pickupRadius;
        }
        // If it's a box collider, you might want to adjust size manually in inspector
    }
    
    /// <summary>
    /// Set item data (useful when dropping items)
    /// </summary>
    public void SetItem(InventoryItem newItem, int newQuantity = 1)
    {
        item = newItem;
        quantity = newQuantity;
    }
    
    /// <summary>
    /// Get the item reference
    /// </summary>
    public InventoryItem GetItem()
    {
        return item;
    }
    
    /// <summary>
    /// Get the quantity
    /// </summary>
    public int GetQuantity()
    {
        return quantity;
    }
    
    /// <summary>
    /// When player enters trigger zone
    /// </summary>
    private void OnTriggerEnter(Collider other)
    {
        // Check if it's the player
        if (other.CompareTag("Player"))
        {
            playerInRange = true;
            
            // Notify PlayerInteraction that this item is nearby
            PlayerInteraction playerInteraction = other.GetComponent<PlayerInteraction>();
            if (playerInteraction != null)
            {
                playerInteraction.SetNearbyItem(this);
            }
        }
    }
    
    /// <summary>
    /// When player exits trigger zone
    /// </summary>
    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerInRange = false;
            
            // Notify PlayerInteraction that player left
            PlayerInteraction playerInteraction = other.GetComponent<PlayerInteraction>();
            if (playerInteraction != null)
            {
                playerInteraction.ClearNearbyItem(this);
            }
        }
    }
    
    /// <summary>
    /// Attempt to pick up this item
    /// </summary>
    public bool TryPickup()
    {
        if (item == null)
        {
            Debug.LogWarning("ItemPickup has no item assigned!");
            return false;
        }
        
        // Try to add to inventory
        bool success = InventoryManager.Instance.AddItem(item, quantity);
        
        if (success)
        {
            Debug.Log($"Picked up {quantity}x {item.itemName}");
            Destroy(gameObject); // Remove from world
            return true;
        }
        else
        {
            Debug.Log("Inventory is full!");
            return false;
        }
    }
    
    /// <summary>
    /// Visualize pickup range in editor
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, pickupRadius);
    }
}