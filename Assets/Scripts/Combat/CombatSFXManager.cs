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