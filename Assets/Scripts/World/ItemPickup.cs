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

    [Header("Feedback")]
    [Tooltip("Played at the item's position the moment it is picked up")]
    [SerializeField] private AudioClip pickupSound;
    [Range(0f, 1f)]
    [SerializeField] private float pickupVolume = 0.9f;

    [Header("Attract Particle")]
    [Tooltip("When on, the item's attractParticle (from its InventoryItem) glows while this sits in the world. Hand-placed pickups leave this ON. A dropped REGULAR item turns it OFF; a dropped QUEST item leaves it ON (set automatically when the bag drops an item)")]
    [SerializeField] private bool showAttractParticle = true;
    
    private Collider triggerCollider;
    private bool playerInRange = false;
    private GameObject activeAttractParticle;
    private bool particleBuilt = false;
    private Camera faceCamera;
    
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

    private void Start()
    {
        // Built in Start, not Awake: dropped items get their item assigned via SetItem
        // right after Instantiate (after Awake, before Start), so the glow reflects the
        // final item and the dropped-vs-quest flag.
        RefreshAttractParticle();
    }

    /// <summary>
    /// Set item data (useful when dropping items)
    /// </summary>
    public void SetItem(InventoryItem newItem, int newQuantity = 1)
    {
        item = newItem;
        quantity = newQuantity;
        if (particleBuilt) RefreshAttractParticle();
    }

    /// <summary>
    /// Turn the in-world attract glow on or off. The bag calls this when dropping:
    /// regular items drop without the glow, quest items keep it.
    /// </summary>
    public void SetAttractParticle(bool on)
    {
        showAttractParticle = on;
        if (particleBuilt) RefreshAttractParticle();
    }

    /// <summary>
    /// (Re)build the looping attract particle as a child of this pickup. Destroyed with
    /// the object on pickup, so the glow disappears the moment the item is collected.
    /// </summary>
    private void RefreshAttractParticle()
    {
        particleBuilt = true;

        if (activeAttractParticle != null)
        {
            Destroy(activeAttractParticle);
            activeAttractParticle = null;
        }

        if (!showAttractParticle || item == null || item.attractParticle == null) return;

        activeAttractParticle = Instantiate(item.attractParticle, transform);

        // Center it on the item's visual so the effect can cover the whole body (scale it
        // up in the inspector to wrap the item). Falls back to the pivot if the item has no
        // measurable mesh. The per-item offset then nudges from there.
        if (item.attractParticleCoverItem && TryGetVisualBounds(out Bounds vis))
        {
            activeAttractParticle.transform.position = vis.center;
            activeAttractParticle.transform.localPosition += item.attractParticleOffset;
        }
        else
        {
            activeAttractParticle.transform.localPosition = item.attractParticleOffset;
        }

        activeAttractParticle.transform.localRotation = Quaternion.identity;
        if (Mathf.Abs(item.attractParticleScale - 1f) > 0.0001f)
            activeAttractParticle.transform.localScale *= item.attractParticleScale;
    }

    /// <summary>
    /// Combined world-space bounds of this pickup's solid mesh renderers, ignoring any
    /// particle renderers (so the glow itself is never measured). Used to float the
    /// attract glow above the item.
    /// </summary>
    private bool TryGetVisualBounds(out Bounds bounds)
    {
        bounds = new Bounds(transform.position, Vector3.zero);
        bool found = false;
        foreach (Renderer r in GetComponentsInChildren<Renderer>())
        {
            if (r is ParticleSystemRenderer) continue;
            if (!found) { bounds = r.bounds; found = true; }
            else bounds.Encapsulate(r.bounds);
        }
        return found;
    }

    /// <summary>
    /// Keep the attract glow turned toward the camera so it reads from any angle.
    /// </summary>
    private void LateUpdate()
    {
        if (activeAttractParticle == null || item == null || !item.attractParticleFaceCamera) return;
        if (faceCamera == null) faceCamera = Camera.main;
        if (faceCamera == null) return;
        activeAttractParticle.transform.rotation = faceCamera.transform.rotation;
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
    /// True when the bag can take the FULL quantity right now. PlayerInteraction
    /// uses this for the prompt ("Bag is full!" instead of "Press E").
    /// </summary>
    public bool BagHasRoom()
    {
        return item != null
            && InventoryManager.Instance != null
            && InventoryManager.Instance.CanAccept(item, quantity);
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

        // Refuse BEFORE taking: if the full quantity does not fit, the item stays
        // in the world instead of partially vanishing into a full bag.
        if (InventoryManager.Instance == null || !InventoryManager.Instance.CanAccept(item, quantity))
        {
            Debug.Log("Bag is full! The item stays where it is.");
            return false;
        }

        if (pickupSound != null)
            AudioSource.PlayClipAtPoint(pickupSound, transform.position, pickupVolume);

        // Try to add to inventory
        bool success = InventoryManager.Instance.AddItem(item, quantity);
        
        if (success)
        {
            Debug.Log($"Picked up {quantity}x {item.itemName}");
            Destroy(gameObject); // Remove from world (the attract particle child goes with it)
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