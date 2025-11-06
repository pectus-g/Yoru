using UnityEngine;

/// <summary>
/// Base class for all NPCs that can be interacted with
/// Inherit from this for specific NPC behaviors (give items, take items, dialogue, etc.)
/// </summary>
[RequireComponent(typeof(Collider))]
public abstract class NPCInteraction : MonoBehaviour
{
    [Header("NPC Settings")]
    [SerializeField] protected string npcName = "NPC";
    [SerializeField] protected float interactionRadius = 3f;
    
    [Header("Interaction Settings")]
    [SerializeField] protected bool isRepeatable = true; // Can interact multiple times?
    
    protected Collider triggerCollider;
    protected bool hasInteracted = false;
    protected bool playerInRange = false;
    
    protected virtual void Awake()
    {
        // Setup trigger collider
        triggerCollider = GetComponent<Collider>();
        triggerCollider.isTrigger = true;
        
        // If it's a sphere collider, set the radius
        if (triggerCollider is SphereCollider sphereCollider)
        {
            sphereCollider.radius = interactionRadius;
        }
    }
    
    /// <summary>
    /// When player enters trigger zone
    /// </summary>
    protected virtual void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerInRange = true;
            
            // Check if can still interact
            if (CanInteract())
            {
                // Notify PlayerInteraction
                PlayerInteraction playerInteraction = other.GetComponent<PlayerInteraction>();
                if (playerInteraction != null)
                {
                    playerInteraction.SetNearbyNPC(this);
                }
            }
        }
    }
    
    /// <summary>
    /// When player exits trigger zone
    /// </summary>
    protected virtual void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerInRange = false;
            
            // Notify PlayerInteraction
            PlayerInteraction playerInteraction = other.GetComponent<PlayerInteraction>();
            if (playerInteraction != null)
            {
                playerInteraction.ClearNearbyNPC(this);
            }
        }
    }
    
    /// <summary>
    /// Check if this NPC can be interacted with
    /// </summary>
    public virtual bool CanInteract()
    {
        return isRepeatable || !hasInteracted;
    }
    
    /// <summary>
    /// Called when player presses E near this NPC
    /// Override this in child classes for specific behavior
    /// </summary>
    public virtual void Interact()
    {
        if (!CanInteract()) return;
        
        hasInteracted = true;
        Debug.Log($"Interacted with {npcName}");
    }
    
    /// <summary>
    /// Get the prompt text to display to player
    /// Override this in child classes for custom prompts
    /// </summary>
    public virtual string GetPromptText()
    {
        return $"Press E to talk to {npcName}";
    }
    
    /// <summary>
    /// Reset interaction state (useful for repeatable NPCs)
    /// </summary>
    public virtual void ResetInteraction()
    {
        hasInteracted = false;
    }
    
    /// <summary>
    /// Visualize interaction range in editor
    /// </summary>
    protected virtual void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, interactionRadius);
    }
}