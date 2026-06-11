using UnityEngine;

/// <summary>
/// The Stolen Face resolution at the cave. The mujina (in her noppera-bo guise) stands
/// in the cave as a REAL, ACTIVE enemy: a full Nopperabo-style prefab with EnemyCombat,
/// EnemyHealth, and an InteractableEnemy whose dialogueData is MujinaReveal_Dialogue.
///
/// Per Hazel's June 2026 ruling, form decides the encounter:
///   - Arrive as YORU: nothing scripted happens. She aggros and fights through the
///     normal combat systems, like any enemy. If she dies, the quest completes through
///     the kill path below.
///   - Arrive as TOMOE: the reveal conversation auto-opens (enemies cannot fight Tomoe).
///     On its FINAL_SUCCESS the quest completes and the cave mujina despawns. The
///     roadside noppera-bo STAYS in the world; the player simply knows her now (the
///     parchment says LIAR!). No soul drop, no return conversation.
///
/// Either path stamps the parchment with QuestData.completedStatusText (LIAR!) and the
/// strike-through, because dead or unmasked, the lie is exposed.
///
/// Setup:
///   - This GameObject: trigger Collider sized to the cave mouth.
///   - caveMujina: the ACTIVE mujina enemy standing in the cave. InteractableEnemy
///     dialogueData = MujinaReveal_Dialogue, interactionRange large (25+) so moving
///     around the cave does not range-cancel the reveal.
///   - roadsideSoul: the original Nopperabo_prefab instance on the road.
/// Cave loot is hand-placed by Hazel; this script never spawns items.
/// </summary>
[RequireComponent(typeof(Collider))]
public class MujinaRevealController : MonoBehaviour
{
    #region Inspector
    [Header("Quest")]
    [Tooltip("The Stolen Face QuestData")]
    [SerializeField] private QuestData quest;

    [Tooltip("triggerId of the find-the-cave step, advanced on entry in either form")]
    [SerializeField] private string caveTriggerId = "stolenface_cave";

    [Header("Actors")]
    [Tooltip("The ACTIVE mujina enemy in the cave (full enemy prefab). Fights Yoru normally; reveals herself to Tomoe")]
    [SerializeField] private GameObject caveMujina;

    [Tooltip("The original roadside Nopperabo spawn. Stays in the world after the reveal (the player just knows her now); despawned only if the mujina is killed")]
    [SerializeField] private GameObject roadsideSoul;
    #endregion

    #region State
    private InteractableEnemy caveMujinaInteractable;
    private EnemyCombat caveMujinaCombat;
    private EnemyHealth caveMujinaHealth;
    private FormController formController;
    private bool resolved;
    #endregion

    #region Lifecycle
    private void Awake()
    {
        Collider col = GetComponent<Collider>();
        col.isTrigger = true;
    }

    private void Start()
    {
        formController = FindObjectOfType<FormController>();

        if (caveMujina != null)
        {
            caveMujinaInteractable = caveMujina.GetComponent<InteractableEnemy>();
            caveMujinaCombat = caveMujina.GetComponent<EnemyCombat>();
            caveMujinaHealth = caveMujina.GetComponent<EnemyHealth>();
        }

        if (caveMujina == null || caveMujinaInteractable == null)
            Debug.LogError("[MujinaReveal] caveMujina is missing or has no InteractableEnemy. The reveal cannot run.");
        if (roadsideSoul == null)
            Debug.LogWarning("[MujinaReveal] roadsideSoul not assigned. The roadside spawn cannot despawn on the kill path.");
        if (quest == null)
            Debug.LogError("[MujinaReveal] quest not assigned.");
        if (formController == null)
            Debug.LogWarning("[MujinaReveal] FormController not found. The Tomoe reveal path cannot open.");

        // Tomoe path: the reveal conversation lands its lie.
        DialogueManager.OnFinalSuccess += HandleFinalSuccess;

        // Yoru path: combat as always; her death also resolves the quest.
        if (caveMujinaHealth != null)
            caveMujinaHealth.OnDied += HandleCaveMujinaDied;
    }

    private void OnDestroy()
    {
        DialogueManager.OnFinalSuccess -= HandleFinalSuccess;
        if (caveMujinaHealth != null)
            caveMujinaHealth.OnDied -= HandleCaveMujinaDied;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        OnPlayerEntered();
    }
    #endregion

    #region Entry
    /// <summary>
    /// Player crossed the cave mouth. The find-the-cave step completes in either form;
    /// the reveal conversation only opens for Tomoe. As Yoru the mujina's own combat
    /// systems take over and this controller stays silent. Self-guarded, so re-entering
    /// after a cancelled conversation restarts the reveal.
    /// </summary>
    private void OnPlayerEntered()
    {
        if (resolved) return;
        if (quest == null) return;

        QuestManager.QuestState state = QuestManager.Instance != null
            ? QuestManager.Instance.GetState(quest.questId)
            : QuestManager.QuestState.NotStarted;
        if (state != QuestManager.QuestState.Active && state != QuestManager.QuestState.ReadyToTurnIn)
            return;

        // Arrival counts in either form, tracked or not.
        QuestManager.Instance.NotifyTrigger(caveTriggerId);

        TryOpenReveal();
    }

    /// <summary>
    /// Open the reveal conversation: Tomoe only, mujina not already dead or aggroed,
    /// no other dialogue on screen. Mirrors InteractableEnemy's own talk gates, so the
    /// auto-open never does anything a click could not.
    /// </summary>
    private void TryOpenReveal()
    {
        if (caveMujinaInteractable == null) return;
        if (formController == null || !formController.IsHuman) return;
        if (caveMujinaHealth != null && caveMujinaHealth.CurrentHealth <= 0f) return;
        if (DialogueManager.Instance == null || DialogueManager.Instance.IsDialogueActive) return;

        // Mid-aggro (the player fought as Yoru, then transformed): wait for the
        // calm-down system to settle her back to LostSoul; re-entry or a click opens it.
        if (caveMujinaCombat != null && caveMujinaCombat.GetCurrentState() != EnemyCombat.EnemyState.LostSoul)
        {
            Debug.Log("[MujinaReveal] Mujina is not calm (combat state). Reveal waits for calm-down.");
            return;
        }

        if (caveMujinaCombat != null)
            caveMujinaCombat.SetState(EnemyCombat.EnemyState.Dialogue);

        Debug.Log("[MujinaReveal] Reveal conversation opening.");
        DialogueManager.Instance.ShowDialogue(caveMujinaInteractable.dialogueData, caveMujinaInteractable);
    }
    #endregion

    #region Resolution
    /// <summary>
    /// Tomoe path: the reveal conversation reached FINAL_SUCCESS. Complete the quest
    /// (parchment stamps LIAR!) and remove the mujina from the world, both spawns.
    /// </summary>
    private void HandleFinalSuccess(DialogueData dialogue)
    {
        if (resolved) return;
        if (caveMujinaInteractable == null || dialogue != caveMujinaInteractable.dialogueData) return;

        // She leaves the cave but lives on; the roadside guise stays in the world.
        Resolve(despawnCaveMujina: true, despawnRoadside: false, reason: "reveal complete, the lie is unmasked");
    }

    /// <summary>
    /// Yoru path: the cave mujina died in combat. Same quest resolution; the corpse,
    /// loot, and death effects stay with the normal combat systems, so only the
    /// roadside spawn is despawned here.
    /// </summary>
    private void HandleCaveMujinaDied(EnemyHealth health)
    {
        if (resolved) return;

        // Dead is dead: the roadside guise vanishes with her.
        Resolve(despawnCaveMujina: false, despawnRoadside: true, reason: "mujina destroyed in combat, the lie dies with her");
    }

    private void Resolve(bool despawnCaveMujina, bool despawnRoadside, string reason)
    {
        resolved = true;

        if (QuestManager.Instance != null)
            QuestManager.Instance.CompleteQuestExternally(quest);

        if (despawnCaveMujina && caveMujina != null)
            caveMujina.SetActive(false);
        if (despawnRoadside && roadsideSoul != null)
            roadsideSoul.SetActive(false);

        Debug.Log($"[MujinaReveal] Quest resolved: {reason}.");

        gameObject.SetActive(false);
    }
    #endregion
}
