using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// KomainuGate, the door the Komainu guards. Two jobs:
///   1) While the lion is NOT yet dealt with, a solid wall collider blocks the way. Touching the
///      doorway plays a "blocked" sound + VFX. If YORU touches it, the lion wakes and the fight
///      begins. Granny touching it is also blocked (her persuade unlock is wired in a later pass).
///   2) Once the lion is dealt with (KomainuBoss.IsResolved), the wall opens and walking into the
///      doorway plays a "pass" sound + VFX and changes scene.
///
/// SETUP: put this on the doorway object. Give that object a TRIGGER collider (the detection zone)
/// AND a separate SOLID collider for the wall, dragged into 'Wall Collider'. The player must be
/// tagged 'Player' and carry a FormController (so Yoru vs Granny can be told apart).
/// </summary>
public class KomainuGate : MonoBehaviour
{
    #region Inspector
    [Header("Links")]
    [Tooltip("The Komainu this gate is tied to. The gate reads its IsResolved and wakes it for Yoru.")]
    [SerializeField] private KomainuBoss boss;
    [Tooltip("Solid collider that physically blocks the doorway (NOT a trigger). Enabled while the lion is unresolved, disabled once dealt with. This object's OTHER collider (the trigger) is the detection zone.")]
    [SerializeField] private Collider wallCollider;

    [Header("Scene Change (on a clean pass)")]
    [Tooltip("Optional SimpleSceneChanger to run the fade + load. If empty, the gate loads 'Fallback Scene Name' directly.")]
    [SerializeField] private SimpleSceneChanger sceneChanger;
    [Tooltip("Scene loaded if no SimpleSceneChanger is assigned.")]
    [SerializeField] private string fallbackSceneName = "";

    [Header("Feedback, Blocked (hit the wall)")]
    [SerializeField] private AudioClip failSfx;
    [SerializeField] private GameObject failVfxPrefab;
    [Tooltip("Minimum seconds between repeated 'blocked' thuds while pressing the wall.")]
    [SerializeField] private float failFeedbackCooldown = 0.8f;

    [Header("Feedback, Pass (gate opens)")]
    [SerializeField] private AudioClip successSfx;
    [SerializeField] private GameObject successVfxPrefab;

    [Header("Spawn / Audio")]
    [Tooltip("Where feedback VFX spawn. Defaults to this object's position.")]
    [SerializeField] private Transform feedbackPoint;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private float vfxLifetime = 3f;
    #endregion

    #region Private
    private float failReadyTime;
    private bool sceneTriggered;
    private bool wallOpen;
    #endregion

    #region Lifecycle
    private void Awake()
    {
        if (feedbackPoint == null) feedbackPoint = transform;

        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
                audioSource.spatialBlend = 1f; // 3D positional
            }
        }

        if (wallCollider != null) wallCollider.enabled = true; // start blocked
    }

    private void Update()
    {
        // Open the wall the moment the lion is dealt with.
        if (!wallOpen && boss != null && boss.IsResolved)
        {
            wallOpen = true;
            if (wallCollider != null) wallCollider.enabled = false;
        }
    }
    #endregion

    #region Triggers
    private void OnTriggerEnter(Collider other) => HandlePlayer(other);
    private void OnTriggerStay(Collider other) => HandlePlayer(other);

    private void HandlePlayer(Collider other)
    {
        FormController form = other.GetComponentInParent<FormController>();
        if (form == null) return; // only the player carries a FormController

        bool resolved = boss != null && boss.IsResolved;

        if (resolved)
        {
            // Gate is open, walking through changes scene.
            PassThrough();
            return;
        }

        // Gate is blocked.
        PlayBlocked();

        if (!form.IsHuman && boss != null)
        {
            // Yoru tried to force the door, wake the guardian. (WakeForCombat is idempotent.)
            boss.WakeForCombat();
        }
        // Granny (IsHuman): blocked too. Her unlock comes from the persuade path later,
        // which calls boss.MarkPersuadedResolved().
    }
    #endregion

    #region Feedback
    private void PlayBlocked()
    {
        if (Time.time < failReadyTime) return;
        failReadyTime = Time.time + failFeedbackCooldown;

        if (failSfx != null && audioSource != null) audioSource.PlayOneShot(failSfx);
        SpawnVfx(failVfxPrefab);
    }

    private void PassThrough()
    {
        if (sceneTriggered) return;
        sceneTriggered = true;

        if (successSfx != null && audioSource != null) audioSource.PlayOneShot(successSfx);
        SpawnVfx(successVfxPrefab);
        ChangeScene();
    }

    private void SpawnVfx(GameObject prefab)
    {
        if (prefab == null) return;
        GameObject fx = Instantiate(prefab, feedbackPoint.position, feedbackPoint.rotation);
        ParticleSystem ps = fx.GetComponent<ParticleSystem>();
        if (ps != null && !ps.isPlaying) ps.Play();
        Destroy(fx, vfxLifetime);
    }

    private void ChangeScene()
    {
        if (sceneChanger != null) { sceneChanger.StartSceneChange(); return; }
        if (!string.IsNullOrEmpty(fallbackSceneName)) { SceneManager.LoadScene(fallbackSceneName); return; }
        Debug.LogWarning("[KomainuGate] No SceneChanger and no Fallback Scene Name set, nowhere to go.");
    }
    #endregion
}
