using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Click-to-talk entry point for persuadable souls, Dialogue System v2.
///
/// v2 changes from v1:
///   - The permanent hasBeenTalkedTo latch is removed: souls are talked to repeatedly
///     across attempts (strikes), and after the quest is given the recap path opens.
///   - Re-entry is blocked only while a dialogue is active and during the 3-strike
///     cooldown (interaction suppression toggled by DialogueManager via BeginWander /
///     ReturnFromWander).
///   - Owns the wander round trip: on 3 strikes the soul walks to wanderPoint, and on
///     cooldown expiry it walks back to its cached original position.
///
/// EnemyCombat is never modified. During the wander round trip this component disables
/// EnemyCombat (its LostSoul/Dialogue handlers call StopNav every frame and would freeze
/// the NavMeshAgent) and re-enables it on arrival home. The combat state remains LostSoul
/// underneath the whole time, so the talk gate works again immediately on restore.
/// </summary>
public class InteractableEnemy : MonoBehaviour
{
    #region Inspector
    [Header("Dialogue")]
    [Tooltip("Per-soul conversation asset. Field name kept from v1 so scene wiring survives")]
    public DialogueData dialogueData;

    [Tooltip("Metres within which the player can start dialogue. Larger than NPC range")]
    public float interactionRange = 7f;

    [Tooltip("Legacy 'Press E to talk' prompt UI. Hidden permanently in Phase 2. Kept for scene compatibility; safe to leave assigned in Inspector")]
    public GameObject interactionPrompt;

    [Header("Wander (3-Strike Cooldown)")]
    [Tooltip("Scene Transform the soul walks to after 3 strikes. Place it 5 to 8 metres away on flat NavMesh-baked ground")]
    [SerializeField] private Transform wanderPoint;

    [Tooltip("NavMeshAgent speed used for the wander out and the walk home")]
    [SerializeField] private float wanderSpeed = 2f;

    [Tooltip("Optional Animator state name cross-faded during the wander walk (EnemyCombat is disabled then, so its own animation driving is paused). Empty = soul glides in its idle pose")]
    [SerializeField] private string wanderAnimationState = "";

    [Tooltip("Safety timeout in seconds for each wander leg in case the NavMeshAgent can't reach its target")]
    [SerializeField] private float wanderLegTimeout = 20f;
    [Header("Mistaken Identity Calm-Down")]
    [Tooltip("Settle buffer, not a gate: once the soul disengaged (player is Tomoe, no fight started) and stands still, it becomes talkable after this brief beat. The natural turn-back-and-stop time is the real wait. 0 = the instant it stops moving")]
    [SerializeField] private float calmDownSeconds = 1f;

    [Header("Debug")]
    [Tooltip("Logs why a talk attempt was blocked, gate by gate, every time LMB is pressed near this soul. Turn off once dialogue is confirmed working")]
    [SerializeField] private bool debugTalkGates = true;
    #endregion

    #region State
    private Transform player;
    private EnemyCombat enemyCombat;
    private FormController formController;
    private Camera mainCamera;
    private NavMeshAgent navAgent;

    private Vector3 originalPosition;
    private bool interactionSuppressed;
    private bool agentValuesSaved;
    private bool previousUpdateRotation;
    private float previousAgentSpeed;

    private EnemyHealth enemyHealth;
    private float calmTimer;
    private bool pendingMistakenIdentity;
    #endregion

    #region Lifecycle
    private void Start()
    {
        player = GameObject.FindGameObjectWithTag("Player")?.transform;
        enemyCombat = GetComponent<EnemyCombat>();
        formController = FindObjectOfType<FormController>();
        mainCamera = Camera.main;
        navAgent = GetComponent<NavMeshAgent>();
        enemyHealth = GetComponent<EnemyHealth>();

        // Cached for the walk home after a wander.
        originalPosition = transform.position;

        // Phase 2: prompt UI is permanently hidden. Persuasion is signalled by emotional
        // aura (Tomoe-only visual, future implementation) and taught to the player on the
        // how-to-play screen. No in-world text prompt is shown.
        if (interactionPrompt != null)
            interactionPrompt.SetActive(false);

        if (formController == null)
            Debug.LogWarning("[InteractableEnemy] FormController not found in scene. Click-to-talk will be permanently disabled on this enemy.");
        if (mainCamera == null)
            Debug.LogWarning("[InteractableEnemy] Camera.main not found. Click raycast disabled on this enemy.");
    }

    private void Update()
    {
        if (player == null) return;

        MonitorCalmDown();

        // Everything below only matters on the click frame. Gate checks are evaluated on
        // click so debugTalkGates can report exactly which one blocked the attempt.
        if (!Input.GetMouseButtonDown(0)) return;

        // Only diagnose clicks that happen near this soul, so the console is not spammed
        // by ordinary combat clicks elsewhere in the level.
        float dist = Vector3.Distance(transform.position, player.position);
        bool nearby = dist <= interactionRange * 2f;

        if (interactionSuppressed)
        {
            if (debugTalkGates && nearby) Debug.Log($"[TalkGate] {name}: blocked, interaction suppressed (wander/cooldown in progress).");
            return;
        }

        if (DialogueManager.Instance != null && DialogueManager.Instance.IsDialogueActive)
            return;

        // LostSoul state gate kept for Phase 2. Removal deferred to Echo Walk
        // implementation phase per Phase 2 handoff doc.
        if (enemyCombat != null && enemyCombat.GetCurrentState() != EnemyCombat.EnemyState.LostSoul)
        {
            if (debugTalkGates && nearby) Debug.Log($"[TalkGate] {name}: blocked, combat state is {enemyCombat.GetCurrentState()} (needs LostSoul).");
            return;
        }

        // Persuasion is Tomoe-only per GDD Doc 04 4b and Doc 09 3. Yoru (cat) cannot
        // see emotional auras and cannot initiate dialogue.
        if (formController == null || !formController.IsHuman)
        {
            if (debugTalkGates && nearby) Debug.Log($"[TalkGate] {name}: blocked, player is not in Tomoe form (press T).");
            return;
        }

        if (mainCamera == null) return;

        // Range check: player must be close enough to start dialogue.
        if (dist > interactionRange)
        {
            if (debugTalkGates && nearby) Debug.Log($"[TalkGate] {name}: blocked, distance {dist:F1}m exceeds interactionRange {interactionRange}m.");
            return;
        }

        // Aim ray. When ThirdPersonCamera has the cursor locked, Input.mousePosition is
        // not reliable, so aim straight through the viewport centre instead: the player
        // points the camera at the soul and clicks.
        Ray ray = Cursor.lockState == CursorLockMode.Locked
            ? mainCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f))
            : mainCamera.ScreenPointToRay(Input.mousePosition);

        // RaycastAll instead of a single Raycast so Tomoe's own body, grass, or any
        // stray collider between the camera and the soul cannot silently eat the click.
        RaycastHit[] hits = Physics.RaycastAll(ray, 50f);
        bool hitThisSoul = false;
        for (int i = 0; i < hits.Length; i++)
        {
            Transform t = hits[i].collider.transform;
            if (hits[i].collider.gameObject == gameObject || t.IsChildOf(transform))
            {
                hitThisSoul = true;
                break;
            }
        }

        if (hitThisSoul)
        {
            StartDialogue();
        }
        else if (debugTalkGates && nearby)
        {
            string firstHit = hits.Length > 0 ? hits[0].collider.name : "nothing";
            Debug.Log($"[TalkGate] {name}: all gates passed but the aim ray did not hit this soul. Ray hit '{firstHit}' first of {hits.Length} hit(s). Point the centre of the screen at the soul and click. If this persists, the soul may have no collider.");
        }
    }
    #endregion

    #region Mistaken Identity Calm-Down
    /// <summary>
    /// The soul spotted Yoru and aggroed (LostSoul became Alert/Chase) but no fight ever
    /// started: the soul is still at full health and the player is now Tomoe. After
    /// calmDownSeconds of that, the soul returns to LostSoul where it stands, becomes
    /// talkable again, and the next conversation opens with its mistakenIdentityLine.
    /// Any damage taken means the fight DID start, and this path never fires.
    /// Runs every frame; uses only EnemyCombat's existing public surface.
    /// </summary>
    private void MonitorCalmDown()
    {
        if (interactionSuppressed || enemyCombat == null)
        {
            calmTimer = 0f;
            return;
        }

        // Freeze (do not reset) while a dialogue is open anywhere.
        if (DialogueManager.Instance != null && DialogueManager.Instance.IsDialogueActive)
            return;

        EnemyCombat.EnemyState st = enemyCombat.GetCurrentState();
        bool aggroChain = st == EnemyCombat.EnemyState.Alert
                       || st == EnemyCombat.EnemyState.Chase
                       || st == EnemyCombat.EnemyState.Returning
                       || st == EnemyCombat.EnemyState.Idle;
        bool playerIsTomoe = formController != null && formController.IsHuman;
        bool fightNeverStarted = enemyHealth == null || enemyHealth.CurrentHealth >= enemyHealth.MaxHealth;

        if (!aggroChain || !playerIsTomoe || !fightNeverStarted)
        {
            calmTimer = 0f;
            return;
        }

        calmTimer += Time.deltaTime;

        // Only complete the calm-down while standing still (Alert or Idle). During
        // Chase/Returning the timer keeps accumulating and fires as soon as the soul
        // settles, so a soul walking home calms the moment it stops.
        bool stationary = st == EnemyCombat.EnemyState.Alert || st == EnemyCombat.EnemyState.Idle;
        if (calmTimer >= calmDownSeconds && stationary)
        {
            calmTimer = 0f;
            pendingMistakenIdentity = true;
            enemyCombat.SetState(EnemyCombat.EnemyState.LostSoul);
            Debug.Log($"[InteractableEnemy] {name} calmed down (no fight started). Talkable again, mistaken identity line queued.");
        }
    }

    /// <summary>
    /// Called by DialogueManager when a conversation opens. Returns true exactly once
    /// after a calm-down, so the mistaken identity line plays a single time.
    /// </summary>
    public bool ConsumeMistakenIdentity()
    {
        bool value = pendingMistakenIdentity;
        pendingMistakenIdentity = false;
        return value;
    }
    #endregion

    #region Dialogue Entry
    private void StartDialogue()
    {
        if (dialogueData == null)
        {
            Debug.LogError("[InteractableEnemy] No DialogueData assigned!");
            return;
        }

        if (DialogueManager.Instance == null)
        {
            Debug.LogError("[InteractableEnemy] DialogueManager.Instance is NULL!");
            return;
        }

        // Defensive hide. Should already be inactive from Start, but ensure no leak.
        if (interactionPrompt != null)
            interactionPrompt.SetActive(false);

        if (enemyCombat != null)
            enemyCombat.SetState(EnemyCombat.EnemyState.Dialogue);

        DialogueManager.Instance.ShowDialogue(dialogueData, this);
    }
    #endregion

    #region Wander Round Trip
    /// <summary>
    /// Called by DialogueManager when the soul hits 3 strikes. Suppresses interaction,
    /// restores LostSoul underneath, disables EnemyCombat (its state handlers fight the
    /// NavMeshAgent), and walks the soul to its wander point.
    /// </summary>
    public void BeginWander()
    {
        interactionSuppressed = true;
        StopAllCoroutines();

        if (enemyCombat != null)
        {
            enemyCombat.SetState(EnemyCombat.EnemyState.LostSoul);
            enemyCombat.enabled = false;
        }

        // Save the combat-configured agent values exactly once per round trip, before
        // the wander overwrites them. Restored in RestoreAfterWander.
        if (navAgent != null && navAgent.isOnNavMesh && !agentValuesSaved)
        {
            previousUpdateRotation = navAgent.updateRotation;
            previousAgentSpeed = navAgent.speed;
            agentValuesSaved = true;
        }

        if (wanderPoint == null)
        {
            Debug.LogWarning($"[InteractableEnemy] {name} has no wanderPoint assigned. Cooldown applies but the soul stays in place.");
            return;
        }

        if (!DriveAgentTo(wanderPoint.position))
            return;

        Debug.Log($"[InteractableEnemy] {name} wandering to {wanderPoint.name}.");
        StartCoroutine(WaitForArrival(stopOnArrival: true, restoreOnArrival: false));
    }

    /// <summary>
    /// Called by DialogueManager when the cooldown expires. Walks the soul back to its
    /// original position, then restores EnemyCombat and interaction.
    /// </summary>
    public void ReturnFromWander()
    {
        StopAllCoroutines();

        if (wanderPoint == null || navAgent == null || !navAgent.isOnNavMesh)
        {
            // Nothing moved (or can't move): restore immediately.
            RestoreAfterWander();
            return;
        }

        if (!DriveAgentTo(originalPosition))
        {
            RestoreAfterWander();
            return;
        }

        Debug.Log($"[InteractableEnemy] {name} walking home.");
        StartCoroutine(WaitForArrival(stopOnArrival: true, restoreOnArrival: true));
    }

    /// <summary>
    /// Point the NavMeshAgent at a destination with wander settings. Returns false when
    /// the agent is missing or off the NavMesh (caller decides the fallback).
    /// </summary>
    private bool DriveAgentTo(Vector3 destination)
    {
        if (navAgent == null || !navAgent.isOnNavMesh)
        {
            Debug.LogWarning($"[InteractableEnemy] {name} has no usable NavMeshAgent. Skipping wander movement.");
            return false;
        }

        // EnemyCombat sets updateRotation = false and rotates manually; while it is
        // disabled nothing rotates, so let the agent face its walk direction. The
        // combat-configured values were saved once in BeginWander and are restored
        // in RestoreAfterWander.
        navAgent.updateRotation = true;
        navAgent.speed = wanderSpeed;
        navAgent.isStopped = false;
        navAgent.SetDestination(destination);

        if (!string.IsNullOrEmpty(wanderAnimationState) && enemyCombat != null)
        {
            Animator animator = enemyCombat.GetAnimator();
            if (animator != null)
                animator.CrossFadeInFixedTime(wanderAnimationState, 0.15f);
        }

        return true;
    }

    private IEnumerator WaitForArrival(bool stopOnArrival, bool restoreOnArrival)
    {
        float deadline = Time.time + wanderLegTimeout;

        while (Time.time < deadline)
        {
            if (navAgent == null || !navAgent.isOnNavMesh) break;
            if (!navAgent.pathPending && navAgent.remainingDistance <= navAgent.stoppingDistance + 0.1f) break;
            yield return null;
        }

        if (stopOnArrival && navAgent != null && navAgent.isOnNavMesh)
        {
            navAgent.isStopped = true;
            navAgent.velocity = Vector3.zero;
        }

        if (restoreOnArrival)
            RestoreAfterWander();
    }

    /// <summary>
    /// Re-enable EnemyCombat (still in LostSoul underneath, so its idle handling and the
    /// talk gate resume immediately) and lift interaction suppression.
    /// </summary>
    private void RestoreAfterWander()
    {
        if (agentValuesSaved && navAgent != null && navAgent.isOnNavMesh)
        {
            navAgent.updateRotation = previousUpdateRotation;
            navAgent.speed = previousAgentSpeed;
        }
        agentValuesSaved = false;

        if (enemyCombat != null)
            enemyCombat.enabled = true;

        interactionSuppressed = false;
        Debug.Log($"[InteractableEnemy] {name} returned home. Interaction restored.");
    }
    #endregion

    #region Gizmos
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, interactionRange);

        if (wanderPoint != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(transform.position, wanderPoint.position);
            Gizmos.DrawWireSphere(wanderPoint.position, 0.4f);
        }
    }
    #endregion
}