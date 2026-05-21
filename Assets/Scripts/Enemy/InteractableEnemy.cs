using UnityEngine;

public class InteractableEnemy : MonoBehaviour
{
    public DialogueData dialogueData;
    public float interactionRange = 7f; // Larger than NPC range

    [Tooltip("Legacy 'Press E to talk' prompt UI. Hidden permanently in Phase 2 — aura visual cue (future) and the how-to-play screen handle discoverability now. Kept for scene compatibility; safe to leave assigned in Inspector.")]
    public GameObject interactionPrompt;

    private Transform player;
    private EnemyCombat enemyCombat;
    private FormController formController;
    private Camera mainCamera;
    private bool hasBeenTalkedTo = false;

    void Start()
    {
        player = GameObject.FindGameObjectWithTag("Player")?.transform;
        enemyCombat = GetComponent<EnemyCombat>();
        formController = FindObjectOfType<FormController>();
        mainCamera = Camera.main;

        // Phase 2: prompt UI is permanently hidden. Persuasion is signalled by emotional
        // aura (Tomoe-only visual, future implementation) and taught to the player on the
        // how-to-play screen. No in-world text prompt is shown.
        if (interactionPrompt != null)
        {
            interactionPrompt.SetActive(false);
        }

        if (formController == null)
        {
            Debug.LogWarning("[InteractableEnemy] FormController not found in scene — click-to-talk will be permanently disabled on this enemy.");
        }
        if (mainCamera == null)
        {
            Debug.LogWarning("[InteractableEnemy] Camera.main not found — click raycast disabled on this enemy.");
        }
    }

    void Update()
    {
        if (hasBeenTalkedTo || player == null) return;

        // LostSoul state gate kept for Phase 2. Removal deferred to Echo Walk
        // implementation phase per Phase 2 handoff doc.
        if (enemyCombat != null && enemyCombat.GetCurrentState() != EnemyCombat.EnemyState.LostSoul) return;

        // Persuasion is Tomoe-only per GDD Doc 04 §4b and Doc 09 §3. Yoru (cat) cannot
        // see emotional auras and cannot initiate Echo Walk dialogue.
        if (formController == null || !formController.IsHuman) return;

        if (mainCamera == null) return;

        // Range check — player must be close enough to start dialogue.
        float dist = Vector3.Distance(transform.position, player.position);
        if (dist > interactionRange) return;

        // LMB-click to talk. Cursor is locked to screen centre by ThirdPersonCamera, so
        // the raycast effectively goes through the centre of the view — player aims the
        // enemy with the camera and clicks. LMB is free in Tomoe form because PlayerCombat
        // early-returns from HandleInput when IsHuman (Phase 2 lockout).
        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit, 50f))
            {
                if (hit.collider.gameObject == gameObject || hit.collider.transform.IsChildOf(transform))
                {
                    StartDialogue();
                }
            }
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

        // Defensive hide — should already be inactive from Start, but ensure no leak
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