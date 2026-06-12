using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Yuki as the quest guide spirit, version 3 (Hazel's spec, June 12 2026).
///
/// THE LOOP:
///   - Press C (Tomoe/granny form only, no menu open) or the parchment button.
///   - She appears in the distance, runs to granny, giggles her greeting.
///   - Then she runs ahead toward the CURRENT glow ring, stopping midway to wait
///     whenever granny falls behind (granny is slow).
///   - She arrives at the ring first and WAITS THERE for granny.
///   - Once granny reaches the ring, Yuki's guiding is done. From then on she is shy:
///     she follows granny from FAR, keeping her distance. No more hide and seek.
///   - If granny walks TOWARD her and gets close, she fades away. Want her back?
///     Call her again with C.
///   - Press C while she is out: she runs far away and disappears out there.
///   - If the player turns into Yoru (the cat) while she is out: she runs away and
///     disappears. She only ever answers to granny.
///
/// HARDCODED rules:
///   - No distance requirement to call her. She comes EVERY time C is pressed.
///   - She guides to the FIRST ring after each call; the remaining rings (one at a
///     time) guide the player onward while she shyly tags along behind.
///   - C in cat form stays dodge, untouched; she simply never hears the cat.
///
/// Setup: duplicate the intro Yuki prefab, REMOVE YukiHideAndSeek, add this. Same
/// pieces: Animator (IsRunning / IsAlert / IsLookAround), GhostEffect3D, NavMeshAgent,
/// laugh clip. She builds her own AudioSource if the prefab lacks one, finds her own
/// sparkles, hides her own renderers, and keeps her colliders permanently off (she is
/// a spirit, nothing stands on her). One instance lives in the scene, hidden.
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
    [Tooltip("Her giggle. Played on appear, greeting, each hop resume, and goodbye")]
    [SerializeField] private AudioClip laughingSound;

    [Header("Input")]
    [Tooltip("Summons her when hidden, dismisses her when out. Granny form only, ignored while any menu or dialogue is open")]
    [SerializeField] private KeyCode callKey = KeyCode.C;

    [Header("Distances (metres)")]
    [Tooltip("How far away she appears before running to granny")]
    [SerializeField] private float appearDistance = 25f;

    [Tooltip("How close she gets to granny before the greeting")]
    [SerializeField] private float greetDistance = 3.5f;

    [Tooltip("How far ahead she runs in one stretch before waiting for slow granny")]
    [SerializeField] private float hopDistance = 15f;

    [Tooltip("When granny gets within this range of a waiting Yuki, she giggles and runs on")]
    [SerializeField] private float resumeDistance = 6f;

    [Tooltip("Mid-stretch, if granny falls farther behind than this, Yuki stops and waits")]
    [SerializeField] private float maxLeadDistance = 22f;

    [Tooltip("How close SHE must get to the ring to count as arrived (she waits there)")]
    [SerializeField] private float ringArriveDistance = 2.5f;

    [Tooltip("Granny reaching within this range of the ring ends the guiding: Yuki backs off and turns shy")]
    [SerializeField] private float ringHandoffDistance = 6f;

    [Header("Shy Following (after the ring)")]
    [Tooltip("The distance she keeps from granny while shyly following from far")]
    [SerializeField] private float shyDistance = 14f;

    [Tooltip("If granny comes toward her and gets within this range, she fades away")]
    [SerializeField] private float scareDistance = 7f;

    [Header("Departing")]
    [Tooltip("On dismissal: how far she runs away before fading out there")]
    [SerializeField] private float departDistance = 20f;

    [Header("Movement")]
    [SerializeField] private float runSpeed = 5f;

    [Tooltip("How close to a destination counts as arrived")]
    [SerializeField] private float arriveTolerance = 0.6f;

    [Header("Timing")]
    [Tooltip("The look-around greeting beat after she reaches granny")]
    [SerializeField] private float lookAroundTime = 1.2f;

    [Tooltip("Seconds her fade-out takes")]
    [SerializeField] private float fadeOutTime = 2f;
    #endregion

    #region State
    public static YukiGuide Instance { get; private set; }

    private enum GuideState
    {
        Hidden,        // Not in the world. The only state she can be called from
        Approaching,   // Appeared in the distance, running to granny
        Greeting,      // Reached granny: look-around + giggle beat
        GuideRunning,  // Running a stretch toward the current ring
        GuideWaiting,  // Paused on the way, waiting for slow granny
        AtRing,        // Arrived at the ring, waiting there for granny
        ShyFollowing,  // Guiding done: tagging along from far, easily scared off
        Departing      // Running far away, fading out there
    }

    private GuideState state = GuideState.Hidden;
    private FormController formController;
    private Transform player;
    private GlowTrail[] trails;
    private Renderer[] renderers;
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

        // The prefab ships without a speaker: build one. 3D so her giggle comes from
        // WHERE she is; the laugh is the breadcrumb the player follows through trees.
        if (audioSource == null) audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 1f;
            audioSource.minDistance = 2f;
            audioSource.maxDistance = 35f;
        }

        // Hiding is handled HERE, not by GhostEffect3D alone: the prefab's effect only
        // fades the renderers in its list and misses the skinned body. Renderers off =
        // truly gone, whatever the model is made of.
        renderers = GetComponentsInChildren<Renderer>(true);

        // She is a spirit: nothing should ever stand on her. The prefab root carries a
        // solid capsule that once turned hidden Yuki into an invisible platform.
        // Colliders stay off permanently; the NavMeshAgent does not need them.
        Collider[] solids = GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < solids.Length; i++)
            solids[i].enabled = false;
    }

    private void Start()
    {
        formController = FindObjectOfType<FormController>();
        if (formController == null)
            Debug.LogWarning("[YukiGuide] FormController not found. Yuki can never be called (granny gate cannot pass).");
        else
            player = formController.transform;

        // Trails are placed in the editor, never spawned at runtime: cache once.
        trails = FindObjectsOfType<GlowTrail>(true);

        // Guard against the easy inspector mistake of assigning the particle PREFAB
        // (an asset cannot play in the scene): fall back to her own child sparkles.
        if (fairyParticles == null || !fairyParticles.gameObject.scene.IsValid())
            fairyParticles = GetComponentInChildren<ParticleSystem>(true);

        if (fairyParticles != null) fairyParticles.Stop();
        HideImmediate();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        HandleCallKey();

        // She only ever answers to granny: the moment the player is the cat, she
        // runs away and disappears. Call her again (as granny) to bring her back.
        if (state != GuideState.Hidden && state != GuideState.Departing &&
            (formController == null || !formController.IsHuman))
        {
            DismissYuki();
            return;
        }

        switch (state)
        {
            case GuideState.Approaching:  UpdateApproaching(); break;
            case GuideState.GuideRunning: UpdateGuideRunning(); break;
            case GuideState.GuideWaiting: UpdateGuideWaiting(); break;
            case GuideState.AtRing:       UpdateAtRing(); break;
            case GuideState.ShyFollowing: UpdateShyFollowing(); break;
            // Hidden, Greeting, Departing are driven by coroutines / do nothing.
        }
    }
    #endregion

    #region Input
    /// <summary>
    /// C summons her when hidden, dismisses her when out. Granny only, and ignored
    /// while any menu, dialogue, or combat lock is active. Cat C stays dodge.
    /// </summary>
    private void HandleCallKey()
    {
        if (!Input.GetKeyDown(callKey)) return;
        if (formController == null || !formController.IsHuman) return;
        if (!MenuGuard.CanOpenMenu()) return;

        if (state == GuideState.Hidden)
        {
            if (CanBeCalled()) CallYuki();
        }
        else if (state != GuideState.Departing)
        {
            DismissYuki();
        }
    }
    #endregion

    #region Public API
    /// <summary>
    /// True when calling her would work: she is hidden, the player is granny, and a
    /// quest is marked. No distance requirement: she comes every time.
    /// </summary>
    public bool CanBeCalled()
    {
        if (state != GuideState.Hidden) return false;
        if (formController == null || !formController.IsHuman) return false;
        if (QuestManager.Instance == null || string.IsNullOrEmpty(QuestManager.Instance.TrackedQuestId)) return false;
        return true;
    }

    /// <summary>
    /// Summon her: she appears in the distance and runs to granny. Used by the C key
    /// and by the parchment's Call Yuki button (which closes the parchment first).
    /// </summary>
    public void CallYuki()
    {
        if (!CanBeCalled()) return;

        // She appears off in the direction of the goal when there is one (so her
        // approach already points the way), otherwise simply ahead of granny.
        Vector3 direction = player.forward;
        if (TryResolveTarget(out Vector3 ring))
        {
            Vector3 toRing = ring - player.position;
            toRing.y = 0f;
            if (toRing.sqrMagnitude > 0.01f) direction = toRing.normalized;
        }

        Vector3 spawn = player.position + direction * appearDistance;
        if (NavMesh.SamplePosition(spawn, out NavMeshHit hit, 12f, NavMesh.AllAreas))
            spawn = hit.position;
        else if (NavMesh.SamplePosition(player.position + direction * 6f, out hit, 6f, NavMesh.AllAreas))
            spawn = hit.position; // fallback: closer, but she still comes

        navMeshAgent.enabled = true;
        navMeshAgent.Warp(spawn);
        navMeshAgent.speed = runSpeed;
        navMeshAgent.stoppingDistance = 0f;

        SetRenderersVisible(true);
        if (ghostEffect != null)
        {
            ghostEffect.SetAlphaImmediate(0f);
            ghostEffect.BecomeGhost();
        }
        if (fairyParticles != null) fairyParticles.Play();

        Giggle();
        navMeshAgent.isStopped = false;
        navMeshAgent.SetDestination(player.position);
        SetAnimation(running: true, alert: false, lookAround: false);
        state = GuideState.Approaching;
    }

    /// <summary>
    /// Dismiss her: goodbye giggle, she runs far away and fades out there. Triggered
    /// by C while she is out, and automatically when the player turns into the cat.
    /// </summary>
    public void DismissYuki()
    {
        if (state == GuideState.Hidden || state == GuideState.Departing) return;
        StopAllCoroutines();
        StartCoroutine(DepartRoutine());
    }
    #endregion

    #region Approach + Greeting
    private void UpdateApproaching()
    {
        // Granny moves; keep running at her.
        navMeshAgent.SetDestination(player.position);

        if (HorizontalDistance(transform.position, player.position) <= greetDistance)
            StartCoroutine(GreetRoutine());
    }

    /// <summary>Reached granny: stop, look around, giggle, then head for the ring.</summary>
    private IEnumerator GreetRoutine()
    {
        state = GuideState.Greeting;
        navMeshAgent.isStopped = true;
        FacePlayer();
        SetAnimation(running: false, alert: false, lookAround: true);
        Giggle();
        yield return new WaitForSeconds(lookAroundTime);

        // Guide when there is a ring to guide to, otherwise she is simply around,
        // shy as always.
        if (TryResolveTarget(out Vector3 ring))
            NextHop(ring);
        else
            EnterShyFollowing(retreat: false);
    }
    #endregion

    #region Guiding (to the FIRST ring, with waits for slow granny)
    private void UpdateGuideRunning()
    {
        // Target gone (quest unmarked, all rings passed): nothing to guide to,
        // she turns shy instead of vanishing.
        if (!TryResolveTarget(out Vector3 ring)) { EnterShyFollowing(retreat: false); return; }

        // SHE reached the ring: wait there for granny.
        if (HorizontalDistance(transform.position, ring) <= ringArriveDistance)
        {
            EnterAtRing();
            return;
        }

        // Granny fell behind: stop midway and wait for her. Granny is slow.
        if (HorizontalDistance(player.position, transform.position) > maxLeadDistance)
        {
            navMeshAgent.isStopped = true;
            reachedHopPoint = false;
            SetAnimation(running: false, alert: true, lookAround: false);
            state = GuideState.GuideWaiting;
            return;
        }

        // Finished this stretch: wait for granny to catch up before the next one.
        if (!navMeshAgent.pathPending && navMeshAgent.remainingDistance <= arriveTolerance)
        {
            navMeshAgent.isStopped = true;
            reachedHopPoint = true;
            SetAnimation(running: false, alert: false, lookAround: true);
            state = GuideState.GuideWaiting;
        }
    }

    private void UpdateGuideWaiting()
    {
        if (!TryResolveTarget(out Vector3 ring)) { EnterShyFollowing(retreat: false); return; }

        if (HorizontalDistance(player.position, transform.position) > resumeDistance) return;

        // Granny is close again.
        if (reachedHopPoint)
        {
            Giggle();
            NextHop(ring);
        }
        else
        {
            // She only paused mid-stretch: continue the same path.
            navMeshAgent.isStopped = false;
            SetAnimation(running: true, alert: false, lookAround: false);
            state = GuideState.GuideRunning;
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
        navMeshAgent.stoppingDistance = 0f;
        navMeshAgent.isStopped = false;
        navMeshAgent.SetDestination(point);
        SetAnimation(running: true, alert: false, lookAround: false);
        state = GuideState.GuideRunning;
    }
    #endregion

    #region At the Ring
    private void EnterAtRing()
    {
        navMeshAgent.isStopped = true;
        FacePlayer();
        SetAnimation(running: false, alert: false, lookAround: true);
        Giggle();
        state = GuideState.AtRing;
    }

    private void UpdateAtRing()
    {
        // Ring consumed some other way (player took a shortcut past it)?
        // Her job here is done either way.
        if (!TryResolveTarget(out Vector3 ring)) { EnterShyFollowing(retreat: true); return; }

        // Granny arrived: guiding is over. Yuki backs off and turns shy.
        if (HorizontalDistance(player.position, ring) <= ringHandoffDistance ||
            HorizontalDistance(player.position, transform.position) <= ringHandoffDistance)
        {
            Giggle();
            EnterShyFollowing(retreat: true);
        }
    }
    #endregion

    #region Shy Following (from far, after the ring)
    /// <summary>
    /// Guiding is done: from now on she keeps her distance. With retreat, she first
    /// runs away from granny out to her shy distance.
    /// </summary>
    private void EnterShyFollowing(bool retreat)
    {
        navMeshAgent.stoppingDistance = shyDistance;

        if (retreat)
        {
            Vector3 away = transform.position - player.position;
            away.y = 0f;
            if (away.sqrMagnitude < 0.01f) away = -player.forward;
            away.Normalize();

            Vector3 point = player.position + away * shyDistance;
            if (NavMesh.SamplePosition(point, out NavMeshHit hit, 8f, NavMesh.AllAreas))
                point = hit.position;

            navMeshAgent.stoppingDistance = 0f; // the retreat point itself is exact
            navMeshAgent.isStopped = false;
            navMeshAgent.SetDestination(point);
            SetAnimation(running: true, alert: false, lookAround: false);
        }
        else
        {
            navMeshAgent.isStopped = true;
            SetAnimation(running: false, alert: false, lookAround: false);
        }

        state = GuideState.ShyFollowing;
    }

    private void UpdateShyFollowing()
    {
        float gap = HorizontalDistance(transform.position, player.position);

        // Granny came toward her: too close, she fades away. Call her again with C.
        if (gap <= scareDistance)
        {
            StopAllCoroutines();
            StartCoroutine(FadeAwayShyRoutine());
            return;
        }

        // Tag along from far: when granny drifts off, follow, but always stop at
        // shy distance. The NavMeshAgent's stoppingDistance does the keeping-away.
        if (gap > shyDistance + 5f)
        {
            navMeshAgent.stoppingDistance = shyDistance;
            navMeshAgent.isStopped = false;
            navMeshAgent.SetDestination(player.position);
        }

        // Animation from actual motion: running while moving, settled when not.
        bool moving = !navMeshAgent.isStopped && navMeshAgent.velocity.sqrMagnitude > 0.05f;
        SetAnimation(running: moving, alert: false, lookAround: false);
        if (!moving) FacePlayer();
    }

    /// <summary>Scared off: a quiet fade right where she stands. No run, no goodbye.</summary>
    private IEnumerator FadeAwayShyRoutine()
    {
        state = GuideState.Departing; // blocks re-trigger; C is ignored during it
        navMeshAgent.isStopped = true;
        SetAnimation(running: false, alert: false, lookAround: true);

        if (ghostEffect != null) ghostEffect.FadeOut();
        yield return new WaitForSeconds(fadeOutTime);

        HideImmediate();
    }
    #endregion

    #region Departing + Hiding
    /// <summary>
    /// Her exit on C or cat form: goodbye giggle, run far from granny, fade out in
    /// the distance.
    /// </summary>
    private IEnumerator DepartRoutine()
    {
        state = GuideState.Departing;

        FacePlayer();
        SetAnimation(running: false, alert: false, lookAround: true);
        Giggle();
        yield return new WaitForSeconds(0.6f);

        // Away from granny; if she is standing on top of her, just run forward.
        Vector3 away = transform.position - player.position;
        away.y = 0f;
        if (away.sqrMagnitude < 0.01f) away = transform.forward;
        away.Normalize();

        Vector3 farPoint = transform.position + away * departDistance;
        if (NavMesh.SamplePosition(farPoint, out NavMeshHit hit, 10f, NavMesh.AllAreas))
            farPoint = hit.position;

        navMeshAgent.stoppingDistance = 0f;
        navMeshAgent.isStopped = false;
        navMeshAgent.SetDestination(farPoint);
        SetAnimation(running: true, alert: false, lookAround: false);

        // Run until she gets there, with a timeout so blocked paths never trap her.
        float timer = 0f;
        while (timer < 6f)
        {
            if (!navMeshAgent.pathPending && navMeshAgent.remainingDistance <= 1f) break;
            timer += Time.deltaTime;
            yield return null;
        }

        navMeshAgent.isStopped = true;
        SetAnimation(running: false, alert: false, lookAround: false);
        if (ghostEffect != null) ghostEffect.FadeOut();
        yield return new WaitForSeconds(fadeOutTime);

        HideImmediate();
    }

    /// <summary>Instantly to the hidden resting state, ready for the next call.</summary>
    private void HideImmediate()
    {
        SetRenderersVisible(false);
        if (ghostEffect != null) ghostEffect.SetAlphaImmediate(0f);
        if (fairyParticles != null) fairyParticles.Stop();
        if (navMeshAgent != null) navMeshAgent.enabled = false;
        SetAnimation(running: false, alert: false, lookAround: false);
        state = GuideState.Hidden;
    }

    /// <summary>
    /// The real hide switch. GhostEffect3D only fades the renderers it knows about and
    /// misses the skinned body, so visibility is enforced at the renderer level.
    /// </summary>
    private void SetRenderersVisible(bool visible)
    {
        if (renderers == null) return;
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
                renderers[i].enabled = visible;
        }
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
