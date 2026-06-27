using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Per animation VFX and SFX library for Yoru's climbing, modeled on EnemyFX. Every effect is a
/// named slot with a drag and drop VFX prefab and an AudioClip. Anything can fire a slot by calling
/// Play(name): ClimbController fires the five climb moments (Grab, Hop, LetGo, MantleStart,
/// MantleLand), and animation events on the climb clips fire the per step hand and foot effects.
///
/// HOW EFFECTS ARE FIRED
///   - ClimbController calls Play with the moment constants below.
///   - You add animation events on the looping clips (up, down, sideways, wall run) at the exact
///     frames a hand or foot plants, calling the function Play with the string Climb_HandL,
///     Climb_HandR, Climb_FootL, Climb_FootR, or Climb_WallRunStep. The frame timing stays yours.
///
/// SETUP
///   1. Put this on the player root, the same GameObject as the Animator, so animation events can
///      reach Play. ClimbController finds it automatically.
///   2. Right click the component header and choose Add Climb Event Slots to create the moment and
///      per step slots. Optionally choose Sync From Animator to also add one slot per climb state.
///      Both only add missing slots, they never overwrite what you have set.
///   3. Fill the slots you want, leave the rest empty (empty slots do nothing).
///
/// Player sounds default to 2D so they stay consistent when the camera zooms. Assign your own
/// AudioSource, or change Spatial Blend, if you want them positional like the enemies.
/// </summary>
public class ClimbFX : MonoBehaviour
{
    #region Event Names

    // Fired by ClimbController.
    public const string Grab = "Climb_Grab";
    public const string Hop = "Climb_Hop";
    public const string LetGo = "Climb_LetGo";
    public const string MantleStart = "Climb_MantleStart";
    public const string MantleLand = "Climb_MantleLand";

    // Fired by animation events you place on the clips.
    public const string HandL = "Climb_HandL";
    public const string HandR = "Climb_HandR";
    public const string FootL = "Climb_FootL";
    public const string FootR = "Climb_FootR";
    public const string WallRunStep = "Climb_WallRunStep";

    #endregion

    #region FX Entry

    [System.Serializable]
    public class FXEntry
    {
        [Tooltip("Effect name this entry belongs to. Use a moment constant (Climb_Grab and so on) or a per step name (Climb_HandL and so on). Must match the name Play is called with.")]
        public string effectName;

        [Tooltip("Prefab spawned at the player when this effect fires. Drag prefab from Project window. Leave empty for no VFX.")]
        public GameObject vfx;

        [Tooltip("Clip played when this effect fires. Drag clip from Project window. Leave empty for no SFX.")]
        public AudioClip sfx;

        [Tooltip("Volume for this clip only (multiplied with the AudioSource volume).")]
        [Range(0f, 1f)]
        public float sfxVolume = 1f;

        [Header("Spawn Shaping (optional)")]
        [Tooltip("Local offset from the player. Y raises the effect, Z pushes it toward Yoru's facing (into the wall while climbing). Leave at zero to spawn at the feet.")]
        public Vector3 positionOffset = Vector3.zero;

        [Tooltip("Extra rotation in degrees applied on top of Yoru's facing. Leave at zero for no extra rotation.")]
        public Vector3 rotationOffset = Vector3.zero;

        [Tooltip("Uniform size multiplier for the spawned effect. 2 = twice as big. 0 and 1 both mean unchanged.")]
        public float scale = 1f;
    }

    #endregion

    #region Inspector

    [Header("Setup")]
    [Tooltip("Seconds before a spawned VFX is auto destroyed. Set higher than your longest particle lifetime.")]
    [SerializeField] private float vfxLifetime = 3f;

    [Tooltip("AudioSource all clips play through. Leave empty: one is found on this object or added automatically at runtime.")]
    [SerializeField] private AudioSource audioSource;

    [Tooltip("Spatial blend for the auto created AudioSource. 0 = 2D (always clear, recommended for the player), 1 = 3D positional. Ignored if you assign your own AudioSource.")]
    [Range(0f, 1f)]
    [SerializeField] private float spatialBlend = 0f;

    [Header("Per-Effect Slots")]
    [Tooltip("One entry per effect. Use Add Climb Event Slots (right click the component header) to build this list. Empty slots simply do nothing.")]
    [SerializeField] private List<FXEntry> entries = new List<FXEntry>();

    #endregion

    #region Private State

    // Fast name -> entry lookup, built once at Awake from the serialized list.
    private Dictionary<string, FXEntry> lookup;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = spatialBlend;
        }

        BuildLookup();
    }

    #endregion

    #region Public API

    /// <summary>
    /// Plays the VFX and SFX assigned to the given effect name. Safe to call with any name:
    /// unknown names, empty names, and empty slots all no op silently. Public so animation events
    /// can call it directly with a string argument.
    /// </summary>
    public void Play(string effectName)
    {
        if (string.IsNullOrEmpty(effectName) || lookup == null)
            return;

        if (!lookup.TryGetValue(effectName.Trim(), out FXEntry entry))
            return;

        if (entry.vfx != null)
        {
            // Local offset so Y is always up the body and Z is always toward Yoru's facing,
            // whatever way he is facing.
            Vector3 spawnPos = transform.TransformPoint(entry.positionOffset);
            Quaternion spawnRot = transform.rotation * Quaternion.Euler(entry.rotationOffset);

            GameObject effect = Instantiate(entry.vfx, spawnPos, spawnRot);

            float sizeMultiplier = entry.scale <= 0f ? 1f : entry.scale;
            if (!Mathf.Approximately(sizeMultiplier, 1f))
                effect.transform.localScale *= sizeMultiplier;

            ParticleSystem ps = effect.GetComponent<ParticleSystem>();
            if (ps != null && !ps.isPlaying)
                ps.Play();

            Destroy(effect, vfxLifetime);
        }

        if (entry.sfx != null && audioSource != null)
            audioSource.PlayOneShot(entry.sfx, entry.sfxVolume);
    }

    #endregion

    #region Lookup

    /// <summary>
    /// Builds the name -> entry dictionary from the serialized list. Blank names are skipped;
    /// if the same name appears twice, the first entry wins.
    /// </summary>
    private void BuildLookup()
    {
        lookup = new Dictionary<string, FXEntry>();

        foreach (FXEntry entry in entries)
        {
            if (entry == null || string.IsNullOrEmpty(entry.effectName))
                continue;

            string key = entry.effectName.Trim();
            if (!lookup.ContainsKey(key))
                lookup.Add(key, entry);
        }
    }

    #endregion

#if UNITY_EDITOR
    #region Editor Sync

    private static readonly string[] ClimbEventNames =
    {
        Grab, Hop, LetGo, MantleStart, MantleLand,
        HandL, HandR, FootL, FootR, WallRunStep
    };

    /// <summary>
    /// Adds a slot for every climb moment and per step effect that does not already have one.
    /// Never deletes or overwrites existing slots. Right click the ClimbFX component header to run.
    /// </summary>
    [ContextMenu("Add Climb Event Slots")]
    private void AddClimbEventSlots()
    {
        int added = 0;
        foreach (string name in ClimbEventNames)
        {
            bool exists = entries.Exists(e => e != null && e.effectName == name);
            if (!exists)
            {
                entries.Add(new FXEntry { effectName = name });
                added++;
            }
        }

        UnityEditor.EditorUtility.SetDirty(this);
        Debug.Log($"[ClimbFX] {gameObject.name}: added {added} climb event slot(s), {entries.Count} total.");
    }

    /// <summary>
    /// Optional. Reads the Animator and adds one slot per climb state that does not already have
    /// one, in case you want a one shot effect on entering a state, fired by an animation event
    /// using the state name. Never deletes or overwrites existing slots.
    /// </summary>
    [ContextMenu("Sync From Animator")]
    private void SyncFromAnimator()
    {
        Animator anim = GetComponent<Animator>();
        if (anim == null)
            anim = GetComponentInChildren<Animator>();

        if (anim == null || anim.runtimeAnimatorController == null)
        {
            Debug.LogWarning($"[ClimbFX] {gameObject.name}: no Animator with a controller found. Nothing synced.");
            return;
        }

        List<string> stateNames = new List<string>();

        var controller = anim.runtimeAnimatorController as UnityEditor.Animations.AnimatorController;
        if (controller != null)
        {
            foreach (var layer in controller.layers)
                CollectStates(layer.stateMachine, stateNames);
        }
        else
        {
            foreach (AnimationClip clip in anim.runtimeAnimatorController.animationClips)
            {
                if (clip != null && !stateNames.Contains(clip.name))
                    stateNames.Add(clip.name);
            }
        }

        int added = 0;
        foreach (string name in stateNames)
        {
            bool exists = entries.Exists(e => e != null && e.effectName == name);
            if (!exists)
            {
                entries.Add(new FXEntry { effectName = name });
                added++;
            }
        }

        UnityEditor.EditorUtility.SetDirty(this);
        Debug.Log($"[ClimbFX] {gameObject.name}: sync complete. {added} new entries added, {entries.Count} total.");
    }

    /// <summary>
    /// Recursively collects state names from a state machine and all its sub state machines.
    /// </summary>
    private void CollectStates(UnityEditor.Animations.AnimatorStateMachine stateMachine, List<string> names)
    {
        foreach (var child in stateMachine.states)
        {
            if (!names.Contains(child.state.name))
                names.Add(child.state.name);
        }

        foreach (var sub in stateMachine.stateMachines)
            CollectStates(sub.stateMachine, names);
    }

    #endregion
#endif
}
