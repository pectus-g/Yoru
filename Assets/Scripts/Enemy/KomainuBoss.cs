using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// KomainuBoss, the narrative "brain" that sits on the stone guardian (Komainu) alongside
/// EnemyCombat, EnemyHealth, EnemyFX, an Animator and a NavMeshAgent. It owns the statue
/// life-cycle; EnemyCombat is the fight engine, switched ON only while the lion is fighting.
///
/// FLOW
///   Dormant  , stone pose, eyes lit, invulnerable. Waits for the gate to wake it.
///               (KomainuGate calls WakeForCombat() when YORU tries to force the closed door.)
///   Awakening, plays "Awaken", then enables EnemyCombat and goes hostile at full health.
///   Fighting , EnemyCombat drives every attack / combo. KomainuBoss watches for two things:
///                 * EnemyHealth.OnYield (worn down)  -> bow + turn to stone FOR GOOD
///                   (gate opens, never wakes again).
///                 * EnemyCombat entering Returning (Yoru fled past the leash) -> scanning-walk
///                   patrol near the post, then back to stone (full-health reset, re-armable:
///                   forcing the door again starts a fresh fight).
///
/// GRANNY is never attacked. The gate only calls WakeForCombat() for Yoru. The peaceful persuade
/// path (talk + quest) is set up in a later pass; it ends by calling MarkPersuadedResolved(),
/// which runs the same bow + turn-to-stone-for-good ending without a fight.
///
/// The lion is NEVER destroyed and never plays the Death state, both endings are the bow and
/// ReturnToStone. EnemyHealth runs in non-lethal mode (it yields instead of dying).
/// </summary>
[RequireComponent(typeof(EnemyCombat))]
[RequireComponent(typeof(EnemyHealth))]
public class KomainuBoss : MonoBehaviour
{
    #region Phase
    private enum Phase
    {
        Dormant,    // stone, armed, can wake (invulnerable)
        Awakening,  // playing Awaken
        Fighting,   // EnemyCombat active (vulnerable)
        Patrolling, // scanning-walk after a flee (invulnerable)
        Yielding,   // defeated: bow -> stone (invulnerable)
        Resolved    // stone for good, gate open, never wakes (invulnerable)
    }

    [Header("Current Phase (Debug)")]
    [SerializeField] private Phase phase = Phase.Dormant;
    #endregion

    #region Animation State Names
    [Header("Animation State Names (must match the Lion Animator exactly)")]
    [SerializeField] private string stoneStatueAnim = "StoneStatuePose";
    [SerializeField] private string awakenAnim = "Awaken";
    [SerializeField] private string scanningWalkAnim = "ScanningWalk";
    [SerializeField] private string guardianStanceAnim = "GuardianStance";
    [Tooltip("Pose held while standing and scanning during the post-flee patrol. GuardianStance or an alert Idle both read well.")]
    [SerializeField] private string scanIdleAnim = "GuardianStance";
    [SerializeField] private string returnToStoneAnim = "ReturnToStone";
    [Tooltip("Animator layer the states above live on. Base Layer = 0.")]
    [SerializeField] private int animatorLayer = 0;
    #endregion

    #region Sequence Timing
    [Header("Sequence Timing (fallbacks used only if a clip length can't be read)")]
    [SerializeField] private float awakenFallback = 1.2f;
    [SerializeField] private float guardianStanceFallback = 2f;
    [SerializeField] private float returnToStoneFallback = 1.5f;
    [Tooltip("Crossfade time between KomainuBoss-driven states.")]
    [SerializeField] private float crossFade = 0.12f;
    [Tooltip("Seconds to turn and face Yoru before the bow (defeat) ending.")]
    [SerializeField] private float faceBeforeBow = 0.3f;
    #endregion

    #region Scanning-Walk Patrol
    [Header("Scanning-Walk Patrol (runs after Yoru flees, before going back to stone)")]
    [Tooltip("How many idle-scan + walk cycles the lion does before it is sure Yoru is gone and returns to stone. Higher = waits longer. Each cycle is: stand and scan, then walk to a new spot.")]
    [SerializeField] private int scanCycles = 4;
    [Tooltip("Seconds the lion stands and scans (looking around) at each spot before walking to the next.")]
    [SerializeField] private float idleHoldSeconds = 3f;
    [Tooltip("How far from its post (start position) the lion wanders while scanning.")]
    [SerializeField] private float patrolRadius = 5f;
    [Tooltip("Move speed while scanning / walking home.")]
    [SerializeField] private float patrolSpeed = 2f;
    [Tooltip("Planar distance that counts as 'reached' a scan point or the post.")]
    [SerializeField] private float patrolArrive = 0.6f;
    [Tooltip("Safety cap (seconds) on a single walk leg, so a blocked path can never hang the patrol.")]
    [SerializeField] private float maxWalkSecondsPerLeg = 6f;
    [Tooltip("Safety cap (seconds) on the final walk back to the post.")]
    [SerializeField] private float maxWalkSecondsHome = 10f;
    [SerializeField] private float rotationSpeed = 6f;

    [Header("Scan Look-Around Sweep")]
    [Tooltip("How far (degrees) the lion turns left/right while standing and scanning. 0 = faces straight ahead, no sweep.")]
    [SerializeField] private float scanSweepAngle = 45f;
    [Tooltip("How fast the look-around sweep oscillates while scanning.")]
    [SerializeField] private float scanSweepSpeed = 1.5f;
    #endregion

    #region Re-Engage On Approach
    [Header("Re-Engage On Approach (hear Yoru come back)")]
    [Tooltip("While the lion is patrolling / walking back to stone, if Yoru comes within this distance the lion hears the footsteps and drops straight into a fresh fight. Keep it well under the combat leash distance so it does not instantly re-trigger the moment Yoru flees.")]
    [SerializeField] private float reAggroRadius = 10f;
    #endregion

    #region Eye Glow
    [Header("Eye Glow (optional, assign the lion's eye renderer)")]
    [Tooltip("Renderer whose emissive material is the green eye glow. Leave empty to skip the glow entirely.")]
    [SerializeField] private Renderer eyeGlowRenderer;
    [Tooltip("Material index to drive on that renderer, if it has more than one material.")]
    [SerializeField] private int eyeMaterialIndex = 0;
    [SerializeField] private Color eyeGlowColor = new Color(0.2f, 1f, 0.35f);
    [SerializeField] private float eyeGlowIntensity = 3f;
    [Tooltip("Seconds the green glow takes to fade out for good once the lion is resolved.")]
    [SerializeField] private float eyeFadeDuration = 2f;
    #endregion

    #region Fight Music
    [Header("Fight Music")]
    [Tooltip("Combat song for this fight. Fades in when the fight starts, fades out when the lion is defeated, persuaded, or gives up and walks back. Leave empty to skip music.")]
    [SerializeField] private AudioClip fightMusic;
    [Tooltip("Peak music volume (0-1).")]
    [SerializeField] [Range(0f, 1f)] private float musicVolume = 0.7f;
    [Tooltip("Seconds to fade the song in when the fight starts.")]
    [SerializeField] private float musicFadeIn = 1f;
    [Tooltip("Seconds to fade the song out when the fight ends.")]
    [SerializeField] private float musicFadeOut = 2f;
    #endregion

    #region Debug
    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;
    #endregion

    #region Private
    private EnemyCombat enemyCombat;
    private EnemyHealth enemyHealth;
    private Animator animator;
    private NavMeshAgent navAgent;
    private Transform player;

    private Vector3 postPosition;   // where the statue stands
    private Quaternion postRotation;
    private bool combatStartedOnce; // first BecomeHostile needs EnemyCombat.Start() to run first
    private string currentAnim = "";
    private Material eyeMat;         // instanced eye material (null-safe)

    private AudioSource musicSource; // fight song player (2D, looping), created at Awake
    private float musicTarget = 0f;  // volume the song is fading toward
    private float musicFadeRate = 1f;// volume units per second for the current fade

    private static readonly int EmissionColorID = Shader.PropertyToID("_EmissionColor");

    /// <summary>True once the lion has been dealt with (defeated or persuaded). The gate reads this to open.</summary>
    public bool IsResolved => phase == Phase.Resolved;
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        enemyCombat = GetComponent<EnemyCombat>();
        enemyHealth = GetComponent<EnemyHealth>();
        animator = GetComponent<Animator>();
        navAgent = GetComponent<NavMeshAgent>();

        // The fight engine stays OFF until the gate wakes the lion, this is what stops it
        // aggroing on sight. EnemyCombat.Start() runs the first time we enable it.
        if (enemyCombat != null) enemyCombat.enabled = false;

        // Dedicated 2D looping source for the fight song (faded in/out by Update).
        musicSource = gameObject.AddComponent<AudioSource>();
        musicSource.loop = true;
        musicSource.playOnAwake = false;
        musicSource.volume = 0f;
        musicSource.spatialBlend = 0f;
        if (fightMusic != null) musicSource.clip = fightMusic;
    }

    private void Start()
    {
        player = GameObject.FindGameObjectWithTag("Player")?.transform;

        postPosition = transform.position;
        postRotation = transform.rotation;

        if (navAgent != null)
        {
            navAgent.updateRotation = false; // we rotate manually
            navAgent.stoppingDistance = 0.4f;
        }

        if (eyeGlowRenderer != null)
        {
            Material[] mats = eyeGlowRenderer.materials; // instances the materials
            if (mats != null && mats.Length > 0)
                eyeMat = mats[Mathf.Clamp(eyeMaterialIndex, 0, mats.Length - 1)];
        }

        if (enemyHealth != null) enemyHealth.OnYield += OnLionYielded;

        EnterDormant();
        Log("Dormant, waiting for the gate.");
    }

    private void Update()
    {
        // Fade the fight song every frame, regardless of phase, so it can fade out after the fight.
        if (musicSource != null)
        {
            musicSource.volume = Mathf.MoveTowards(musicSource.volume, musicTarget, musicFadeRate * Time.deltaTime);
            if (musicTarget <= 0.001f && musicSource.volume <= 0.001f && musicSource.isPlaying)
                musicSource.Stop();
        }

        if (phase != Phase.Fighting) return;

        // EnemyCombat signals a disengage (Yoru fled past the leash, or turned into Granny) by
        // entering its Returning state. We take over from there: scan-walk patrol, then stone.
        if (enemyCombat.GetCurrentState() == EnemyCombat.EnemyState.Returning)
        {
            StopAllCoroutines();
            StartCoroutine(DisengageToPatrol());
        }
    }

    private void OnDestroy()
    {
        if (enemyHealth != null) enemyHealth.OnYield -= OnLionYielded;
    }
    #endregion

    #region Public API
    /// <summary>
    /// Called by KomainuGate when Yoru tries to force the closed door. Wakes the lion into combat.
    /// Works from Dormant (plays the Awaken animation first) or Patrolling (already on its feet, so
    /// it re-engages straight away). Ignored while awakening / fighting / yielding / resolved.
    /// </summary>
    public void WakeForCombat()
    {
        if (phase == Phase.Dormant)
        {
            StopAllCoroutines();
            StartCoroutine(AwakenAndFight(playAwaken: true));
        }
        else if (phase == Phase.Patrolling)
        {
            StopAllCoroutines();
            StartCoroutine(AwakenAndFight(playAwaken: false));
        }
    }

    /// <summary>
    /// Called by the (later) persuade system when Granny talks the lion down. Runs the same
    /// bow + turn-to-stone-for-good ending as a combat defeat, without a fight, and opens the gate.
    /// </summary>
    public void MarkPersuadedResolved()
    {
        if (phase == Phase.Resolved) return;
        StopAllCoroutines();
        StartCoroutine(YieldSequence());
    }
    #endregion

    #region Sequences
    private IEnumerator AwakenAndFight(bool playAwaken)
    {
        phase = Phase.Awakening;
        SetInvulnerable(true); // still stone-tough through the wind-up
        StopNav();
        SetEyeGlow(true);

        if (playAwaken)
            yield return PlayAndWait(awakenAnim, awakenFallback);

        // Hand the fight to EnemyCombat.
        enemyCombat.enabled = true;
        if (!combatStartedOnce)
        {
            combatStartedOnce = true;
            yield return null; // let EnemyCombat.Start() run before we drive it
        }

        enemyCombat.ResetCombatState();
        if (enemyHealth != null) enemyHealth.ResetHealth(); // every engagement is a fresh, full fight
        SetInvulnerable(false);
        enemyCombat.BecomeHostile();

        phase = Phase.Fighting;
        StartFightMusic();
        Log("Awake, fighting Yoru.");
    }

    private IEnumerator DisengageToPatrol()
    {
        phase = Phase.Patrolling;
        SetInvulnerable(true);
        HideBossBar();
        StopFightMusic();
        enemyCombat.ResetCombatState();
        enemyCombat.enabled = false; // stop its per-frame StopNav so we can drive the patrol
        Log("Yoru fled, scanning-walk patrol.");

        if (navAgent != null) navAgent.speed = patrolSpeed;

        // Alternate stand-and-scan with a walk to a new vantage point. The whole time, if Yoru
        // comes back within earshot the lion drops straight back into a fresh fight.
        for (int cycle = 0; cycle < scanCycles; cycle++)
        {
            // Stand and scan: alert pose with a slow left/right look-around sweep.
            StopNav();
            float idle = 0f;
            float baseYaw = transform.eulerAngles.y;
            while (idle < idleHoldSeconds)
            {
                PlayState(scanIdleAnim);
                if (scanSweepAngle > 0.01f)
                {
                    float sweep = Mathf.Sin(idle * scanSweepSpeed) * scanSweepAngle;
                    transform.rotation = Quaternion.Euler(0f, baseYaw + sweep, 0f);
                }
                if (PlayerInEarshot()) { ReEngageFromPatrol(); yield break; }
                idle += Time.deltaTime;
                yield return null;
            }

            // Walk to a new scan point (safety-capped so a blocked path can't hang the loop).
            Vector3 point = RandomPatrolPoint();
            if (DriveNavTo(point))
            {
                float legTime = 0f;
                while (!ArrivedAt(point) && legTime < maxWalkSecondsPerLeg)
                {
                    PlayState(scanningWalkAnim);
                    RotateTowardsVelocity();
                    if (PlayerInEarshot()) { ReEngageFromPatrol(); yield break; }
                    legTime += Time.deltaTime;
                    yield return null;
                }
            }
        }

        // No one came back, walk home and settle back to stone (re-armable, full health).
        if (DriveNavTo(postPosition))
        {
            float homeTime = 0f;
            while (!ArrivedAt(postPosition) && homeTime < maxWalkSecondsHome)
            {
                PlayState(scanningWalkAnim);
                RotateTowardsVelocity();
                if (PlayerInEarshot()) { ReEngageFromPatrol(); yield break; }
                homeTime += Time.deltaTime;
                yield return null;
            }
        }
        StopNav();
        yield return SettleFacing(postRotation, faceBeforeBow);
        yield return PlayAndWait(returnToStoneAnim, returnToStoneFallback);

        if (enemyHealth != null) enemyHealth.ResetHealth();
        EnterDormant();
        Log("Settled back to stone, armed again.");
    }

    private IEnumerator YieldSequence()
    {
        phase = Phase.Yielding;
        SetInvulnerable(true);
        HideBossBar();
        StopFightMusic();
        enemyCombat.ResetCombatState();
        enemyCombat.enabled = false;
        StopNav();
        Log("Defeated/persuaded, bow, then stone for good.");

        yield return SettleFacing(FacePlayerRotation(), faceBeforeBow);
        yield return PlayAndWait(guardianStanceAnim, guardianStanceFallback);
        yield return PlayAndWait(returnToStoneAnim, returnToStoneFallback);
        yield return FadeEyeGlowOut();

        PlayState(stoneStatueAnim);
        phase = Phase.Resolved; // gate now reads IsResolved == true; lion never wakes again
        Log("Resolved, stone for good, gate open.");
    }

    private void EnterDormant()
    {
        SetInvulnerable(true);
        StopNav();
        SetEyeGlow(true);
        PlayState(stoneStatueAnim);
        phase = Phase.Dormant;
    }

    private void OnLionYielded(EnemyHealth h)
    {
        if (phase != Phase.Fighting) return;
        StopAllCoroutines();
        StartCoroutine(YieldSequence());
    }
    #endregion

    #region Animation Helpers
    /// <summary>
    /// Drives the animator while EnemyCombat is OFF. Skips if the state is already playing so it
    /// can be called safely every frame inside the patrol loop without re-crossfading.
    /// </summary>
    private void PlayState(string stateName)
    {
        if (animator == null || string.IsNullOrEmpty(stateName)) return;
        if (stateName == currentAnim) return;

        int hash = Animator.StringToHash(stateName);
        if (!animator.HasState(animatorLayer, hash))
        {
            Debug.LogError($"[Komainu] Animator state not found: '{stateName}' on layer {animatorLayer}. Check the state-name fields.");
            return;
        }

        currentAnim = stateName;
        animator.CrossFadeInFixedTime(stateName, crossFade, animatorLayer);
    }

    /// <summary>Plays a state and waits for it to finish (real clip length, with a fallback cap).</summary>
    private IEnumerator PlayAndWait(string stateName, float fallback)
    {
        PlayState(stateName);

        // Let the crossfade settle so the state's real length is readable.
        yield return new WaitForSeconds(crossFade + 0.02f);

        float length = fallback;
        if (animator != null)
        {
            AnimatorStateInfo s = animator.GetCurrentAnimatorStateInfo(animatorLayer);
            if (s.IsName(stateName) && s.length > 0.01f) length = s.length;
        }

        float elapsed = crossFade + 0.02f;
        while (elapsed < length)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }
    }
    #endregion

    #region Eye Glow
    private void SetEyeGlow(bool on)
    {
        if (eyeMat == null) return;
        eyeMat.EnableKeyword("_EMISSION");
        eyeMat.SetColor(EmissionColorID, on ? eyeGlowColor * eyeGlowIntensity : Color.black);
    }

    private IEnumerator FadeEyeGlowOut()
    {
        if (eyeMat == null) yield break;

        Color start = eyeMat.GetColor(EmissionColorID);
        float t = 0f;
        while (t < eyeFadeDuration)
        {
            eyeMat.SetColor(EmissionColorID, Color.Lerp(start, Color.black, t / eyeFadeDuration));
            t += Time.deltaTime;
            yield return null;
        }
        eyeMat.SetColor(EmissionColorID, Color.black);
    }
    #endregion

    #region Navigation Helpers
    private void StopNav()
    {
        if (navAgent != null && navAgent.isOnNavMesh)
        {
            navAgent.isStopped = true;
            navAgent.velocity = Vector3.zero;
        }
    }

    private bool DriveNavTo(Vector3 destination)
    {
        if (navAgent == null || !navAgent.isOnNavMesh) return false;
        navAgent.isStopped = false;
        navAgent.speed = patrolSpeed;
        navAgent.SetDestination(destination);
        return true;
    }

    private bool ArrivedAt(Vector3 destination)
    {
        Vector3 d = destination - transform.position;
        d.y = 0f;
        return d.sqrMagnitude <= patrolArrive * patrolArrive;
    }

    private Vector3 RandomPatrolPoint()
    {
        Vector2 r = Random.insideUnitCircle * patrolRadius;
        Vector3 candidate = postPosition + new Vector3(r.x, 0f, r.y);
        if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, patrolRadius + 1f, NavMesh.AllAreas))
            return hit.position;
        return postPosition;
    }

    private void RotateTowardsVelocity()
    {
        if (navAgent == null) return;
        Vector3 v = navAgent.velocity;
        v.y = 0f;
        if (v.sqrMagnitude < 0.01f) return;
        Quaternion target = Quaternion.LookRotation(v.normalized);
        transform.rotation = Quaternion.Slerp(transform.rotation, target, Time.deltaTime * rotationSpeed);
    }

    private IEnumerator SettleFacing(Quaternion target, float duration)
    {
        if (duration <= 0f) { transform.rotation = target; yield break; }
        Quaternion start = transform.rotation;
        float t = 0f;
        while (t < duration)
        {
            transform.rotation = Quaternion.Slerp(start, target, t / duration);
            t += Time.deltaTime;
            yield return null;
        }
        transform.rotation = target;
    }

    private Quaternion FacePlayerRotation()
    {
        if (player == null) return transform.rotation;
        Vector3 d = player.position - transform.position;
        d.y = 0f;
        return d.sqrMagnitude > 0.001f ? Quaternion.LookRotation(d.normalized) : transform.rotation;
    }

    /// <summary>
    /// True if Yoru is within reAggroRadius (planar). Polled during the patrol / walk-home so the
    /// lion can "hear" Yoru come back and drop into a fresh fight instead of finishing the patrol.
    /// </summary>
    private bool PlayerInEarshot()
    {
        if (player == null) return false;
        Vector3 d = player.position - transform.position;
        d.y = 0f;
        return d.sqrMagnitude <= reAggroRadius * reAggroRadius;
    }

    /// <summary>
    /// Re-enter combat from the patrol (Yoru came back). Skips the Awaken wind-up since the lion is
    /// already on its feet, and runs a fresh, full-health fight, same as the gate's re-trigger.
    /// Sets phase up front so a same-frame gate WakeForCombat can't start a second fight.
    /// </summary>
    private void ReEngageFromPatrol()
    {
        phase = Phase.Awakening;
        Log("Heard Yoru return, back into the fight.");
        StartCoroutine(AwakenAndFight(playAwaken: false));
    }
    #endregion

    #region Misc Helpers
    private void SetInvulnerable(bool value)
    {
        if (enemyHealth != null) enemyHealth.SetInvulnerable(value);
    }

    private void HideBossBar()
    {
        if (BossHealthBarUI.Instance != null) BossHealthBarUI.Instance.Hide("komainu disengage/yield");
    }

    /// <summary>Fade the fight song up to musicVolume over musicFadeIn seconds (starts playback if needed).</summary>
    private void StartFightMusic()
    {
        if (musicSource == null || musicSource.clip == null) return;
        if (!musicSource.isPlaying)
        {
            musicSource.volume = 0f;
            musicSource.Play();
        }
        musicTarget = musicVolume;
        musicFadeRate = musicVolume / Mathf.Max(0.01f, musicFadeIn);
    }

    /// <summary>Fade the fight song down to silence over musicFadeOut seconds (Update stops it at the end).</summary>
    private void StopFightMusic()
    {
        if (musicSource == null) return;
        musicTarget = 0f;
        musicFadeRate = musicVolume / Mathf.Max(0.01f, musicFadeOut);
    }

    private void Log(string msg)
    {
        if (showDebugLogs) Debug.Log($"[Komainu] {msg}");
    }
    #endregion
}