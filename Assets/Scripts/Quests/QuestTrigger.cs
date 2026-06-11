using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// The one scene component for quest steps. Drop on a GameObject with a trigger
/// Collider, pick a mode, set the triggerId to match the quest step:
///
///   ENTER_AREA: completes ENTER_LOCATION steps the moment the player walks in.
///   PRESS_E:    completes INTERACT steps on E. Registers with PlayerInteraction the
///               same way ItemPickup does, and only while some active quest's CURRENT
///               step is waiting on this triggerId, so the prompt never shows early.
///
/// onEntered fires on EVERY player entry in ENTER_AREA mode (scripted sequences hook
/// here and self-guard). onAccepted fires in both modes when a quest step actually
/// advanced; hook visuals there (sprout the seed, light the offering board).
/// </summary>
[RequireComponent(typeof(Collider))]
public class QuestTrigger : MonoBehaviour
{
    #region Types
    public enum TriggerMode
    {
        ENTER_AREA,
        PRESS_E
    }
    #endregion

    #region Inspector
    [Header("Quest")]
    [Tooltip("How the player fires this trigger")]
    [SerializeField] private TriggerMode mode = TriggerMode.ENTER_AREA;

    [Tooltip("Match key for the quest step, e.g. \"stolenface_cave\"")]
    [SerializeField] private string triggerId;

    [Header("Enter Area Mode")]
    [Tooltip("Keep listening after a step was advanced. Off = the trigger disables itself once accepted")]
    [SerializeField] private bool stayArmedAfterAccept = false;

    [Header("Press E Mode")]
    [Tooltip("Prompt shown when in range, e.g. \"Press E to plant the seed\"")]
    [SerializeField] private string promptText = "Press E to interact";

    [Tooltip("Deactivate this object after a successful use")]
    [SerializeField] private bool consumeOnUse = true;

    [Header("Events")]
    [Tooltip("ENTER_AREA only: every player entry, accepted or not. Scripted sequences hook here")]
    public UnityEvent onEntered;

    [Tooltip("Both modes: only when a quest step actually advanced")]
    public UnityEvent onAccepted;
    #endregion

    #region State
    private PlayerInteraction playerInside;
    private bool registered;
    #endregion

    #region Lifecycle
    private void Awake()
    {
        Collider col = GetComponent<Collider>();
        col.isTrigger = true;
    }

    private void Update()
    {
        if (mode != TriggerMode.PRESS_E || playerInside == null) return;

        // Relevance can change while the player stands here (the quest step arrives or
        // passes), so registration follows relevance every frame while in range.
        bool relevant = QuestManager.Instance != null && QuestManager.Instance.IsTriggerRelevant(triggerId);
        if (relevant && !registered)
        {
            playerInside.SetNearbyQuestTrigger(this);
            registered = true;
        }
        else if (!relevant && registered)
        {
            playerInside.ClearNearbyQuestTrigger(this);
            registered = false;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        if (mode == TriggerMode.ENTER_AREA)
        {
            HandleAreaEntry();
        }
        else
        {
            playerInside = other.GetComponent<PlayerInteraction>();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (mode != TriggerMode.PRESS_E) return;
        if (!other.CompareTag("Player")) return;

        Unregister();
        playerInside = null;
    }

    private void OnDisable()
    {
        Unregister();
        playerInside = null;
    }
    #endregion

    #region Enter Area Mode
    private void HandleAreaEntry()
    {
        onEntered?.Invoke();

        if (QuestManager.Instance == null) return;

        bool accepted = QuestManager.Instance.NotifyTrigger(triggerId);
        if (accepted)
        {
            onAccepted?.Invoke();
            if (!stayArmedAfterAccept)
                gameObject.SetActive(false);
        }
    }
    #endregion

    #region Press E Mode
    /// <summary>
    /// Prompt for PlayerInteraction's UI.
    /// </summary>
    public string GetPromptText()
    {
        return promptText;
    }

    /// <summary>
    /// Player pressed E. Advances the matching quest step; on success fires onAccepted
    /// and optionally consumes this object.
    /// </summary>
    public void Use()
    {
        if (QuestManager.Instance == null) return;

        bool accepted = QuestManager.Instance.NotifyTrigger(triggerId);
        if (!accepted) return;

        onAccepted?.Invoke();

        if (consumeOnUse)
            gameObject.SetActive(false);
    }

    private void Unregister()
    {
        if (playerInside != null && registered)
            playerInside.ClearNearbyQuestTrigger(this);
        registered = false;
    }
    #endregion

    #region Gizmos
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.85f, 0.3f, 0.6f);
        Collider col = GetComponent<Collider>();
        if (col is BoxCollider box)
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(box.center, box.size);
        }
        else if (col is SphereCollider sphere)
        {
            Gizmos.DrawWireSphere(transform.position + sphere.center, sphere.radius);
        }
    }
    #endregion
}
