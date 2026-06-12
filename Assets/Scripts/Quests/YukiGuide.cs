using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Yuki as the quest guide spirit. She NEVER appears on her own: the Call Yuki button
/// on the bag's Quest page summons her (grayed out when she cannot come). She fades in
/// beside Tomoe, checks around with a giggle, then leads the way to the marked quest's
/// current glow ring in short hops: run ahead, wait, giggle when Tomoe gets close, run
/// again. When the ring is near enough for the player to find it themselves, she gives
/// a last giggle and dissolves.
///
/// HARDCODED rules (no behavior knobs, by design):
///   - One universal guide for EVERY marked quest; the trail she reads is whichever
///     GlowTrail is lit, and single-tracking guarantees at most one ever is.
///   - Tomoe form only, same gate as the trail itself. Transforming to Yoru, unmarking
///     the quest, or the step advancing mid-walk all make her dissolve quietly.
///
/// Setup: duplicate the intro Yuki prefab, REMOVE YukiHideAndSeek, add this. Same
/// pieces: Animator (IsRunning / IsAlert / IsLookAround), GhostEffect3D, NavMeshAgent,
/// AudioSource, fairy particles, laugh clip. One instance lives in the scene, hidden.
/// </summary>
public class YukiGuide : MonoBehaviour
{
    #region Inspector
    [Header("Character References (same pieces as YukiHideAndSeek)")]
    [SerializeField] private Animator animator;
    [SerializeField] private GhostEffect3D ghostEffect;
    [SerializeField] private NavMeshAgent navMeshAgent;
    [SerializeField] private ParticleSystem fairyParticles;
    [SerializeField] private AudioSource audioSource;

    [Header("Audio")]
    [Tooltip("Her giggle. Played on appear, on every new hop, and as her goodbye")]
    [SerializeField] private AudioClip laughingSound;

    [Header("Guiding Distances (metres)")]
    [Tooltip("The Call Yuki button only lights up when the marked quest's current ring is farther than this. Closer than this, the player can find the ring alone")]
    [SerializeField] private float callDistance = 30f;

    [Tooltip("How far ahead she runs in one hop before waiting for Tomoe")]
    [SerializeField] private float hopDistance = 15f;

    [Tooltip("When Tomoe gets within this range of a waiting Yuki, she giggles and runs the next hop")]
    [SerializeField] private float resumeDistance = 6f;

    [Tooltip("Mid-run, if Tomoe falls farther behind than this, Yuki stops and waits")]
    [SerializeField] private float maxLeadDistance = 22f;

    [Tooltip("Handoff: when the RING is within this range of the player, Yuki giggles goodbye and dissolves")]
    [SerializeField] private float handoffDistance = 15f;

    [Header("Movement")]
    [SerializeField] private float runSpeed = 5f;

    [Tooltip("How close to her hop point counts as arrived")]
    [SerializeField] private float arriveTolerance = 0.6f;

    [Header("Timing")]
    [Tooltip("The look-around beat after she appears, before the first hop")]
    [SerializeField] private float lookAroundTime = 1.2f;

    [Tooltip("Seconds her dissolve takes before she is fully hidden")]
    [SerializeField] private float fadeOutTime = 2f;
    #endregion

    #region State
    public static YukiGuide Instance { get; private set; }

    private enum GuideState
    {
        Hidden,     // Not in the world. The only state Call Yuki works from
        Appearing,  // Fading in beside Tomoe, look-around beat
        Running,    // Hopping toward the ring
        Waiting,    // At a hop point (or paused mid-run), waiting for Tomoe
        Dissolving  // Fading out (handoff or abort), then Hidden
    }

    private GuideState state = GuideState.Hidden;
    private FormController formController;
    private Transform player;
    private GlowTrail[] trails;
    private bool reachedHopPoint;

    private static readonly int HashIsRunning = Animator.StringToHash("IsRunning");
    private static readonly int HashIsAlert = Animator.StringToHash("IsAlert");
    private static readonly int HashIsLookAround = Animator.StringToHash("IsLookAround");
    #endregion

    #region Lifecycle
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[YukiGuide] Second YukiGuide in scene, destroying it. One guide only.");
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (animator == null) animator = GetComponent<Animator>();
        if (ghostEffect == null) ghostEffect = GetComponent<GhostEffect3D>();
        if (navMeshAgent == null) navMeshAgent = GetComponent<NavMeshAgent>();
        if (audioSource == null) audioSource = GetComponent<AudioSource>();
    }

    private void Start()
    {
        formController = FindObjectOfType<FormController>();
        if (formController == null)
            Debug.LogWarning("[YukiGuide] FormController not found. Yuki can never be called (Tomoe gate cannot pass).");
        else
            player = formController.transform;

        // Trails are placed in the editor, never spawned at runtime: cache once.
        trails = FindObjectsOfType<GlowTrail>(true);

        if (fairyParticles != null) fairyParticles.Stop();
        HideImmediate();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        if (state != GuideState.Running && state != GuideState.Waiting) return;

        // The world she navigates by: the one lit trail's current ring. If it cannot
        // be resolved (quest unmarked, step advanced, Tomoe became Yoru), she leaves.
        if (formController == null || !formController.IsHuman || !TryResolveTarget(out Vector3 ring))
        {
            StartCoroutine(DissolveRoutine(false));
            return;
        }

        // Handoff: the ring is close enough for the player to spot. Her job is done.
        if (HorizontalDistance(player.position, ring) <= handoffDistance)
        {
            StartCoroutine(DissolveRoutine(true));
            return;
        }

        if (state == GuideState.Running)
            UpdateRunning(ring);
        else
            UpdateWaiting(ring);
    }
    #endregion

    #region Public API
    /// <summary>
    /// True when the Call Yuki button should light up: she is hidden, a quest is
    /// marked, its trail resolves to a ring, the player is Tomoe, and the ring is
    /// farther away than callDistance.
    /// </summary>
    public bool CanBeCalled()
    {
        if (state != GuideState.Hidden) return false;
        if (formController == null || !formController.IsHuman) return false;
        if (QuestManager.Instance == null || string.IsNullOrEmpty(QuestManager.Instance.TrackedQuestId)) return false;
        if (!TryResolveTarget(out Vector3 ring)) return false;
        return HorizontalDistance(player.position, ring) > callDistance;
    }

    /// <summary>
    /// Summon her. Called by the bag's Call Yuki button AFTER the bag closes (so
    /// timeScale is back to 1 and her coroutines run).
    /// </summary>
    public void CallYuki()
    {
        if (!CanBeCalled()) return;
        StartCoroutine(AppearRoutine());
    }
    #endregion

    #region Appear / Dissolve
    /// <summary>
    /// Fade in beside Tomoe, check around with a giggle, then start the first hop.
    /// </summary>
    private IEnumerator AppearRoutine()
    {
        state = GuideState.Appearing;

        // Beside Tomoe, snapped to the NavMesh so the agent is happy.
        Vector3 spawn = player.position + player.right * 2f;
        if (NavMesh.SamplePosition(spawn, out NavMeshHit hit, 4f, NavMesh.AllAreas))
            spawn = hit.position;

        navMeshAgent.enabled = true;
        navMeshAgent.Warp(spawn);
        navMeshAgent.speed = runSpeed;
        navMeshAgent.isStopped = true;

        FacePlayer();

        if (ghostEffect != null)
        {
            ghostEffect.SetAlphaImmediate(0f);
            ghostEffect.BecomeGhost();
        }
        if (fairyParticles != null) fairyParticles.Play();

        // She checks the marked quest: a little look around, a giggle, and off she goes.
        SetAnimation(running: false, alert: false, lookAround: true);
        Giggle();
        yield return new WaitForSeconds(lookAroundTime);

        if (TryResolveTarget(out Vector3 ring))
        {
            NextHop(ring);
        }
        else
        {
            yield return DissolveRoutine(false);
        }
    }

    /// <summary>
    /// Her exit, both endings: goodbye (handoff, with a last giggle) and quiet abort
    /// (quest unmarked, form changed, ring gone).
    /// </summary>
    private IEnumerator DissolveRoutine(bool goodbye)
    {
        if (state == GuideState.Dissolving) yield break;
        state = GuideState.Dissolving;

        if (navMeshAgent.enabled) navMeshAgent.isStopped = true;
        SetAnimation(running: false, alert: false, lookAround: goodbye);

        if (goodbye)
        {
            FacePlayer();
            Giggle();
            yield return new WaitForSeconds(0.6f);
        }

        if (ghostEffect != null) ghostEffect.FadeOut();
        yield return new WaitForSeconds(fadeOutTime);

        HideImmediate();
    }

    /// <summary>Instantly to the hidden resting state, ready for the next call.</summary>
    private void HideImmediate()
    {
        if (ghostEffect != null) ghostEffect.SetAlphaImmediate(0f);
        if (fairyParticles != null) fairyParticles.Stop();
        if (navMeshAgent != null) navMeshAgent.enabled = false;
        SetAnimation(running: false, alert: false, lookAround: false);
        state = GuideState.Hidden;
    }
    #endregion

    #region Guiding
    private void UpdateRunning(Vector3 ring)
    {
        // Tomoe fell behind: pause mid-run and wait for her.
        if (HorizontalDistance(player.position, transform.position) > maxLeadDistance)
        {
            navMeshAgent.isStopped = true;
            reachedHopPoint = false;
            SetAnimation(running: false, alert: true, lookAround: false);
            state = GuideState.Waiting;
            return;
        }

        // Arrived at the hop point: wait there for Tomoe to catch up.
        if (!navMeshAgent.pathPending && navMeshAgent.remainingDistance <= arriveTolerance)
        {
            navMeshAgent.isStopped = true;
            reachedHopPoint = true;
            SetAnimation(running: false, alert: false, lookAround: true);
            state = GuideState.Waiting;
        }
    }

    private void UpdateWaiting(Vector3 ring)
    {
        if (HorizontalDistance(player.position, transform.position) > resumeDistance) return;

        // Tomoe is close again.
        if (reachedHopPoint)
        {
            Giggle();
            NextHop(ring);
        }
        else
        {
            // She only paused mid-run: continue the same path.
            navMeshAgent.isStopped = false;
            SetAnimation(running: true, alert: false, lookAround: false);
            state = GuideState.Running;
        }
    }

    /// <summary>
    /// Run the next stretch toward the ring: up to hopDistance ahead, snapped to the
    /// NavMesh so she paths around trees and rocks instead of through them.
    /// </summary>
    private void NextHop(Vector3 ring)
    {
        Vector3 toRing = ring - transform.position;
        toRing.y = 0f;
        float distance = Mathf.Min(hopDistance, toRing.magnitude);

        Vector3 point = transform.position + toRing.normalized * distance;
        if (NavMesh.SamplePosition(point, out NavMeshHit hit, 6f, NavMesh.AllAreas))
            point = hit.position;

        reachedHopPoint = false;
        navMeshAgent.isStopped = false;
        navMeshAgent.SetDestination(point);
        SetAnimation(running: true, alert: false, lookAround: false);
        state = GuideState.Running;
    }
    #endregion

    #region Helpers
    /// <summary>
    /// The ring Yuki guides to: the current ring of the one lit trail. Single-tracking
    /// guarantees at most one trail is ever lit, so first hit wins.
    /// </summary>
    private bool TryResolveTarget(out Vector3 ringPosition)
    {
        ringPosition = Vector3.zero;
        if (trails == null) return false;

        for (int i = 0; i < trails.Length; i++)
        {
            GlowTrail trail = trails[i];
            if (trail == null || !trail.IsLit) continue;
            return trail.TryGetCurrentRingPosition(out ringPosition);
        }
        return false;
    }

    private static float HorizontalDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }

    private void FacePlayer()
    {
        if (player == null) return;
        Vector3 look = player.position - transform.position;
        look.y = 0f;
        if (look.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.LookRotation(look);
    }

    private void Giggle()
    {
        if (audioSource != null && laughingSound != null)
            audioSource.PlayOneShot(laughingSound);
    }

    private void SetAnimation(bool running, bool alert, bool lookAround)
    {
        if (animator == null) return;
        animator.SetBool(HashIsRunning, running);
        animator.SetBool(HashIsAlert, alert);
        animator.SetBool(HashIsLookAround, lookAround);
    }
    #endregion
}
