using UnityEngine;

public class InteractableEnemy : MonoBehaviour
{
    [Header("Dialogue")]
    [SerializeField] private DialogueData dialogueData;
    
    [Header("Interaction Settings")]
    [SerializeField] private float interactionRange = 3f;
    [SerializeField] private bool canInteract = true;
    [SerializeField] private bool hasBeenTalkedTo = false;
    
    private Transform player;
    private GameObject interactionPrompt;
    private EnemyCombat enemyCombat;
    
    void Start()
    {
        // Find player
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            player = playerObj.transform;
        }
        
        // Find interaction prompt in HUD
        Canvas hudCanvas = FindObjectOfType<Canvas>();
        if (hudCanvas != null)
        {
            Transform prompt = hudCanvas.transform.Find("InteractionPrompt");
            if (prompt != null)
            {
                interactionPrompt = prompt.gameObject;
            }
        }
        
        enemyCombat = GetComponent<EnemyCombat>();
        
        // Hide prompt by default
        if (interactionPrompt != null)
        {
            interactionPrompt.SetActive(false);
        }
    }
    
    void Update()
    {
        if (!canInteract || hasBeenTalkedTo) return;
        if (player == null) return;
        
        // Check if enemy is in LostSoul state (can only talk when confused)
        if (enemyCombat != null && enemyCombat.GetCurrentState() != EnemyCombat.EnemyState.LostSoul)
        {
            if (interactionPrompt != null && interactionPrompt.activeSelf)
            {
                interactionPrompt.SetActive(false);
            }
            return;
        }
        
        // Check distance to player
        float distance = Vector3.Distance(transform.position, player.position);
        
        if (distance <= interactionRange)
        {
            // Show prompt
            if (interactionPrompt != null)
            {
                interactionPrompt.SetActive(true);
            }
            
            // Check for E key
            if (Input.GetKeyDown(KeyCode.E))
            {
                StartDialogue();
            }
        }
        else
        {
            // Hide prompt
            if (interactionPrompt != null && interactionPrompt.activeSelf)
            {
                interactionPrompt.SetActive(false);
            }
        }
    }
    
    private void StartDialogue()
    {
        if (dialogueData == null)
        {
            Debug.LogError($"{gameObject.name} has no DialogueData assigned!");
            return;
        }
        
        // Hide prompt
        if (interactionPrompt != null)
        {
            interactionPrompt.SetActive(false);
        }
        
        // Set enemy to dialogue state
        if (enemyCombat != null)
        {
            enemyCombat.SetState(EnemyCombat.EnemyState.Dialogue);
        }
        
        // Show dialogue
        if (DialogueManager.Instance != null)
        {
            DialogueManager.Instance.ShowDialogue(dialogueData, this);
            hasBeenTalkedTo = true; // Can only talk once
        }
        
        Debug.Log($"Started dialogue with {gameObject.name}");
    }
    
    // Debug visualization
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, interactionRange);
    }
}