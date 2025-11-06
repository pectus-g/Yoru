using UnityEngine;

public class InteractableEnemy : MonoBehaviour
{
    public DialogueData dialogueData;
    public float interactionRange = 7f; // Larger than NPC range
    public GameObject interactionPrompt;
    
    private Transform player;
    private EnemyCombat enemyCombat;
    private bool hasBeenTalkedTo = false;
    
    void Start()
    {
        player = GameObject.FindGameObjectWithTag("Player")?.transform;
        enemyCombat = GetComponent<EnemyCombat>();
        
        if (interactionPrompt != null)
        {
            interactionPrompt.SetActive(false);
            Debug.Log("✅ Enemy interaction prompt ready");
        }
        else
        {
            Debug.LogError("❌ InteractionPrompt NOT assigned to enemy!");
        }
    }
    
    void Update()
    {
        if (hasBeenTalkedTo || player == null || interactionPrompt == null) return;
        
        // Only show prompt when in LostSoul state
        if (enemyCombat != null && enemyCombat.GetCurrentState() != EnemyCombat.EnemyState.LostSoul)
        {
            if (interactionPrompt.activeSelf)
            {
                interactionPrompt.SetActive(false);
            }
            return;
        }
        
        // Check distance
        float dist = Vector3.Distance(transform.position, player.position);
        bool inRange = dist <= interactionRange;
        
        // Show/hide prompt
        if (inRange && !interactionPrompt.activeSelf)
        {
            interactionPrompt.SetActive(true);
            Debug.Log("✅ Showing enemy prompt!");
        }
        else if (!inRange && interactionPrompt.activeSelf)
        {
            interactionPrompt.SetActive(false);
        }
        
        // Check E key
        if (inRange && Input.GetKeyDown(KeyCode.E))
        {
            Debug.Log("🔑🔑🔑 E PRESSED ON ENEMY! 🔑🔑🔑");
            StartDialogue();
        }
    }
    
    void StartDialogue()
    {
        if (dialogueData == null)
        {
            Debug.LogError("❌ No DialogueData assigned!");
            return;
        }
        
        if (DialogueManager.Instance == null)
        {
            Debug.LogError("❌ DialogueManager.Instance is NULL!");
            return;
        }
        
        Debug.Log("=== STARTING ENEMY DIALOGUE ===");
        
        // Hide prompt
        if (interactionPrompt != null)
        {
            interactionPrompt.SetActive(false);
        }
        
        // Set to dialogue state
        if (enemyCombat != null)
        {
            enemyCombat.SetState(EnemyCombat.EnemyState.Dialogue);
        }
        
        // Show dialogue
        DialogueManager.Instance.ShowDialogue(dialogueData, this);
        hasBeenTalkedTo = true;
        
        Debug.Log("✅ Enemy dialogue started!");
    }
    
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, interactionRange);
    }
}