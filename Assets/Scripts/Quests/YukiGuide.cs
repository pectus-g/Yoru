using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Yuki as the quest guide spirit, version 4 (Hazel's spec, June 12 2026).
///
/// THE LOOP:
///   - Press C (granny form only, no menu open) or the parchment button. Only works
///     while a quest is marked; no marked quest, no Yuki.
///   - She appears in the distance, runs to granny, giggles her greeting.
///   - She leads to the CURRENT glow ring, stopping midway to wait whenever granny
///     falls behind (granny is slow).
///   - She arrives at the ring first and WAITS THERE, every time.
///   - The moment granny PASSES that ring, Yuki's job is done: a happy giggle and she
///     fades away on the spot. The remaining rings (one at a time) guide the player on.
///   - Want her again, for the next ring or the way back? Press C again. Fresh call,
///     same loop, she always leads to whatever ring is current.
///   - Press C while she is out: she runs far away and disappears out there.
///   - Turning into Yoru (the cat) while she is out: same, she runs away and
///     disappears. She only ever answers to granny.
///
/// HARDCODED rules:
///   - No distance requirement to call her. She comes EVERY time C is pressed.
///   - One job per call: lead to the current ring, vanish once it is passed.
///   - C in cat form stays dodge, untouched; she simply never hears the cat.
///   - She carries her own soft glow light so she reads through rain, fog, and night.
///
/// Setup: duplicate the intro Yuki prefab, REMOVE YukiHideAndSeek, add this. Same
/// pieces: Animator (IsRunning / IsAlert / IsLookAround), GhostEffect3D, NavMeshAgent,
/// laugh clip. She builds her own AudioSource and glow light if missing, finds her own
/// sparkles, hides her own renderers, forces her Animator to always animate (so she
/// runs properly even off-screen), and keeps her colliders permanently off (she is a
/// spirit, nothing stands on her). One instance lives in the scene, hidden.
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
    [Tooltip("Her giggle. Played on appear, greeting, resume, success, and goodbye, never more than once per cooldown")]
    [SerializeField] private AudioClip laughingSound;

    [Tooltip("Minimum seconds between giggles, so call-spamming never machine-guns the laugh")]
    [SerializeField] private float giggleCooldown = 1.2f;

    [Header("Input")]
    [Tooltip("Summons her when hidden, dismisses her when out. Granny form only, ignored while any menu or dialogue is open")]
    [SerializeField] private KeyCode callKey = KeyCode.C;

    [Header("Glow (so she reads through rain, fog, night)")]
    [Tooltip("Created automatically: a soft warm light that travels with her while she is visible")]
    [SerializeField] private Color glowColor = new Color(1f, 0.92f, 0.72f, 1f);
    [SerializeField] private float glowIntensity = 2.4f;
    [SerializeField] private float glowRange = 7f;

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
        AtRing,        // Waiting at the ring until granny passes it
        Departing      // Fading out (success, dismissal, or cat form)
    }

    private GuideState state = GuideState.Hidden;
    private FormController formController;
    private Transform player;
    private GlowTrail[] trails;
    private Renderer[] renderers;
    private Light glowLight;
    private bool reachedHopPoint;
    private Vector3 guidedRing;       // the ring she is waiting at
    private float lastGiggleTime = -10f;
    private float lastCallKeyTime = -10f;

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

        // Off-screen or freshly unhidden, a culled Animator simply stops playing,
        // which looked like "her animations do not work". She always animates.
        if (animator != null)
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

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

        // Her own glow, so rain, fog, and night can never swallow her.
        GameObject glow = new GameObject("YukiGlow");
        glow.transform.SetParent(transform, false);
        glow.transform.localPosition = new Vector3(0f, 1.1f, 0f);
        glowLight = glow.AddComponent<Light>();
        glowLight.type = LightType.Point;
        glowLight.color = glowColor;
        glowLight.intensity = glowIntensity;
        glowLight.range = glowRange;
        glowLight.shadows = LightShadows.None;
        glowLight.enabled = false;

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
            // Hidden, Greeting, Departing are driven by coroutines / do nothing.
        }
    }
    #endregion

    #region Input
    /// <summary>
    /// C summons her when hidden, dismisses her when out. Granny only, ignored while
    /// any menu, dialogue, or combat lock is active, and debounced so hammering the
    /// key cannot spam-toggle her. Cat C stays dodge.
    /// </summary>
    private void HandleCallKey()
    {
        if (!Input.GetKeyDown(callKey)) return;
        if (Time.time - lastCallKeyTime < 0.75f) return;
        if (formController == null || !formController.IsHuman) return;
        if (!MenuGuard.CanOpenMenu()) return;

        lastCallKeyTime = Time.time;

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
        if (glowLight != null) glowLight.enabled = true;
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
        StartCoroutine(DepartRoutine(runAway: true));
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

        if (TryResolveTarget(out Vector3 ring))
        {
            NextHop(ring);
        }
        else
        {
            // Nothing to guide to (all rings already passed): she fades back out.
            StartCoroutine(DepartRoutine(runAway: false));
        }
    }
    #endregion

    #region Guiding (to the current ring, with waits for slow granny)
    private void UpdateGuideRunning()
    {
        if (!TryResolveTarget(out Vector3 ring)) { StartCoroutine(DepartRoutine(runAway: false)); return; }

        // SHE reached the ring: wait there until granny passes it.
        if (HorizontalDistance(transform.position, ring) <= ringArriveDistance)
        {
            EnterAtRing(ring);
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
        if (!TryResolveTarget(out Vector3 ring)) { StartCoroutine(DepartRoutine(runAway: false)); return; }

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
        navMeshAgent.isStopped = false;
        navMeshAgent.SetDestination(point);
        SetAnimation(running: true, alert: false, lookAround: false);
        state = GuideState.GuideRunning;
    }
    #endregion

    #region At the Ring (until granny passes it)
    private void EnterAtRing(Vector3 ring)
    {
        guidedRing = ring;
        navMeshAgent.isStopped = true;
        FacePlayer();
        SetAnimation(running: false, alert: false, lookAround: true);
        Giggle();
        state = GuideState.AtRing;
    }

    private void UpdateAtRing()
    {
        // Granny passed the ring: the trail consumed it, so the current ring is now a
        // DIFFERENT one (or none). Her job is done: happy giggle, fade on the spot.
        bool ringStillCurrent = TryResolveTarget(out Vector3 current) &&
                                Vector3.Distance(current, guidedRing) < 0.5f;
        if (!ringStillCurrent)
        {
            Giggle();
            StartCoroutine(DepartRoutine(runAway: false));
        }
    }
    #endregion

    #region Departing + Hiding
    /// <summary>
    /// Her exit. With runAway (C press, cat form): goodbye giggle, run far from
    /// granny, fade in the distance. Without (job done, nothing to guide to): a
    /// gentle fade right where she stands.
    /// </summary>
    private IEnumerator DepartRoutine(bool runAway)
    {
        state = GuideState.Departing;

        if (runAway)
        {
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
        }

        if (navMeshAgent.enabled) navMeshAgent.isStopped = true;
        SetAnimation(running: false, alert: false, lookAround: !runAway);
        if (ghostEffect != null) ghostEffect.FadeOut();
        yield return new WaitForSeconds(fadeOutTime);

        HideImmediate();
    }

    /// <summary>Instantly to the hidden resting state, ready for the next call.</summary>
    private void HideImmediate()
    {
        SetRenderersVisible(false);
        if (glowLight != null) glowLight.enabled = false;
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

    /// <summary>Cooldown-guarded: hammering C can never machine-gun the laugh.</summary>
    private void Giggle()
    {
        if (audioSource == null || laughingSound == null) return;
        if (Time.time - lastGiggleTime < giggleCooldown) return;
        lastGiggleTime = Time.time;
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
