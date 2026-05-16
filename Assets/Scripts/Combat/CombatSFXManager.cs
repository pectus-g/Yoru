using UnityEngine;

/// <summary>
/// YORU Combat SFX Manager — Phase 3B
/// Singleton for all combat sound effects. Hooks are ready — assign AudioClips in Inspector.
/// Uses AudioSource pooling (3 sources) so overlapping sounds don't cut each other off.
/// 
/// Attach to the same "CombatManagers" GameObject as CombatFeedbackManager.
/// </summary>
public class CombatSFXManager : MonoBehaviour
{
    #region Singleton
    public static CombatSFXManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }
    #endregion

    #region Serialized Fields — Assign clips in Inspector when audio is ready
    [Header("Swing / Whoosh")]
    [SerializeField] private AudioClip swingLight;
    [SerializeField] private AudioClip swingHeavy;
    [SerializeField] private AudioClip swingCombo3;

    [Header("Impact")]
    [SerializeField] private AudioClip impactLight;
    [SerializeField] private AudioClip impactHeavy;
    [SerializeField] private AudioClip impactCombo3;

    [Header("Dodge")]
    [SerializeField] private AudioClip dodgeWhoosh;

    [Header("Parry")]
    [SerializeField] private AudioClip parryClang;
    [SerializeField] private AudioClip guardBlock;

    [Header("Player Hit")]
    [SerializeField] private AudioClip playerHitLight;
    [SerializeField] private AudioClip playerHitHeavy;

    [Header("Enemy")]
    [SerializeField] private AudioClip enemyHitVocal;
    [SerializeField] private AudioClip enemyDeathVocal;
    [SerializeField] private AudioClip enemyAttackVocal;

    [Header("Heavy Charge")]
    [Tooltip("One-shot played when LMB hold passes the 0.3s gate and the charge begins.")]
    [SerializeField] private AudioClip heavyChargeStart;
    [Tooltip("Looping rumble/hum played for the duration of the charge. Stops on release, cancel, or hit.")]
    [SerializeField] private AudioClip heavyChargeLoop;
    [Tooltip("One-shot played when chargePercent crosses 100% — pairs with the UI ring glow flash.")]
    [SerializeField] private AudioClip heavyChargeReady;
    [Tooltip("One-shot played at the moment of release (when the release animation triggers). The impact/strike sound.")]
    [SerializeField] private AudioClip heavyChargeRelease;
    [SerializeField] [Range(0f, 1f)] private float heavyChargeLoopVolume = 0.6f;

    [Header("Volume")]
    [SerializeField] [Range(0f, 1f)] private float sfxVolume = 0.8f;
    [SerializeField] [Range(0f, 1f)] private float swingVolume = 0.5f;
    [SerializeField] [Range(0f, 1f)] private float impactVolume = 0.9f;

    [Header("Pitch Variation")]
    [Tooltip("Random pitch variation ± this value for organic feel")]
    [SerializeField] private float pitchVariation = 0.1f;

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = false;
    #endregion

    #region Private Fields
    private AudioSource[] audioSources;
    private int currentSourceIndex;
    private const int POOL_SIZE = 4;
    private AudioSource heavyChargeLoopSource; // Dedicated source for the looping charge rumble — separate from the pool so it doesn't get stolen by round-robin.
    #endregion

    #region Unity Lifecycle
    private void Start()
    {
        // Create audio source pool
        audioSources = new AudioSource[POOL_SIZE];
        for (int i = 0; i < POOL_SIZE; i++)
        {
            audioSources[i] = gameObject.AddComponent<AudioSource>();
            audioSources[i].playOnAwake = false;
            audioSources[i].spatialBlend = 0f; // 2D for combat SFX — always audible
        }

        // Dedicated source for the heavy charge loop (kept out of the pool so round-robin
        // can't steal it mid-charge). Configured for sustained looping playback.
        heavyChargeLoopSource = gameObject.AddComponent<AudioSource>();
        heavyChargeLoopSource.playOnAwake = false;
        heavyChargeLoopSource.spatialBlend = 0f;
        heavyChargeLoopSource.loop = true;

        DebugLog("CombatSFXManager initialized");
    }
    #endregion

    #region Public API — Called by PlayerCombat, CombatFeedbackManager, EnemyCombat

    /// <summary>
    /// Play swing whoosh at start of attack animation.
    /// Call from Animation Events on combo clips.
    /// </summary>
    public void PlaySwing(int comboStep)
    {
        AudioClip clip;
        switch (comboStep)
        {
            case 3: clip = swingCombo3; break;
            case 0: clip = swingHeavy; break; // Heavy uses step 0
            default: clip = swingLight; break;
        }
        PlayClip(clip, swingVolume);
        DebugLog($"Swing: combo {comboStep}");
    }

    /// <summary>
    /// Play impact thud when attack connects.
    /// Call from PlayerCombat.DealDamageInRange() on hit confirm.
    /// </summary>
    public void PlayImpact(bool isHeavy, bool isCombo3 = false)
    {
        AudioClip clip;
        if (isCombo3)
            clip = impactCombo3 ?? impactHeavy;
        else
            clip = isHeavy ? impactHeavy : impactLight;

        PlayClip(clip, impactVolume);
        DebugLog($"Impact: {(isHeavy ? "heavy" : "light")}");
    }

    /// <summary>
    /// Play dodge whoosh.
    /// Call from PlayerCombat.PerformDodge().
    /// </summary>
    public void PlayDodge()
    {
        PlayClip(dodgeWhoosh, swingVolume);
        DebugLog("Dodge whoosh");
    }

    /// <summary>
    /// Play parry clang on perfect parry.
    /// </summary>
    public void PlayParryClang()
    {
        PlayClip(parryClang, impactVolume);
        DebugLog("Parry clang");
    }

    /// <summary>
    /// Play guard block sound (not perfect parry).
    /// </summary>
    public void PlayGuardBlock()
    {
        PlayClip(guardBlock, sfxVolume * 0.7f);
        DebugLog("Guard block");
    }

    /// <summary>
    /// Play player taking damage sound.
    /// Call from PlayerHealth.TakeDamage().
    /// </summary>
    public void PlayPlayerHit(bool isHeavy)
    {
        AudioClip clip = isHeavy ? playerHitHeavy : playerHitLight;
        PlayClip(clip, sfxVolume);
        DebugLog($"Player hit: {(isHeavy ? "heavy" : "light")}");
    }

    /// <summary>
    /// Play enemy vocalization on hit.
    /// Call from EnemyHealth.TakeDamage().
    /// </summary>
    public void PlayEnemyHitVocal()
    {
        PlayClip(enemyHitVocal, sfxVolume * 0.6f);
    }

    /// <summary>
    /// Play enemy death vocalization.
    /// </summary>
    public void PlayEnemyDeathVocal()
    {
        PlayClip(enemyDeathVocal, sfxVolume);
    }

    /// <summary>
    /// Play enemy attack vocalization (telegraph).
    /// </summary>
    public void PlayEnemyAttackVocal()
    {
        PlayClip(enemyAttackVocal, sfxVolume * 0.7f);
    }

    /// <summary>
    /// One-shot played when heavy charge begins (LMB hold passes the 0.3s gate).
    /// Pairs with PlayHeavyChargeLoop which starts at the same moment.
    /// </summary>
    public void PlayHeavyChargeStart()
    {
        PlayClip(heavyChargeStart, sfxVolume);
        DebugLog("Heavy charge start");
    }

    /// <summary>
    /// Start the looping charge rumble. Plays on dedicated AudioSource so the pool
    /// can't steal it. Idempotent — calling twice in a row is safe.
    /// </summary>
    public void PlayHeavyChargeLoop()
    {
        if (heavyChargeLoop == null || heavyChargeLoopSource == null) return;
        if (heavyChargeLoopSource.isPlaying) return;
        heavyChargeLoopSource.clip = heavyChargeLoop;
        heavyChargeLoopSource.volume = heavyChargeLoopVolume * sfxVolume;
        heavyChargeLoopSource.Play();
        DebugLog("Heavy charge loop START");
    }

    /// <summary>
    /// Stop the looping charge rumble. Called on release, cancel, hit, or any safety reset.
    /// Idempotent — calling when already stopped is safe.
    /// </summary>
    public void StopHeavyChargeLoop()
    {
        if (heavyChargeLoopSource == null) return;
        if (!heavyChargeLoopSource.isPlaying) return;
        heavyChargeLoopSource.Stop();
        DebugLog("Heavy charge loop STOP");
    }

    /// <summary>
    /// One-shot played when chargePercent crosses 100%.
    /// Pairs with the HeavyChargeUI ring glow flash.
    /// </summary>
    public void PlayHeavyChargeReady()
    {
        PlayClip(heavyChargeReady, sfxVolume);
        DebugLog("Heavy charge ready (100%)");
    }

    /// <summary>
    /// One-shot played at the moment of release — the strike/impact sound.
    /// Called by PlayerCombat.ReleaseHeavyAttack alongside StopHeavyChargeLoop.
    /// </summary>
    public void PlayHeavyChargeRelease()
    {
        PlayClip(heavyChargeRelease, sfxVolume);
        DebugLog("Heavy charge release");
    }

    /// <summary>
    /// Set global SFX volume from Settings.
    /// </summary>
    public void SetVolume(float volume)
    {
        sfxVolume = Mathf.Clamp01(volume);
    }
    #endregion

    #region Audio Source Pooling
    private void PlayClip(AudioClip clip, float volume)
    {
        if (clip == null) return;

        AudioSource source = GetNextSource();
        source.clip = clip;
        source.volume = volume;
        source.pitch = 1f + Random.Range(-pitchVariation, pitchVariation);
        source.Play();
    }

    private AudioSource GetNextSource()
    {
        // Round-robin through pool — find one that's not playing, or use next in line
        for (int i = 0; i < POOL_SIZE; i++)
        {
            int index = (currentSourceIndex + i) % POOL_SIZE;
            if (!audioSources[index].isPlaying)
            {
                currentSourceIndex = (index + 1) % POOL_SIZE;
                return audioSources[index];
            }
        }

        // All playing — steal the next one
        currentSourceIndex = (currentSourceIndex + 1) % POOL_SIZE;
        return audioSources[currentSourceIndex];
    }
    #endregion

    #region Debug
    private void DebugLog(string message)
    {
        if (showDebugLogs)
            Debug.Log($"[SFX] {message}");
    }
    #endregion
}