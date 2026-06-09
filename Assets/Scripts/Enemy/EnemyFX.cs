using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Per-animation VFX + SFX library for one enemy. Every animation state gets its own entry
/// with a drag-and-drop VFX prefab slot and an AudioClip slot. EnemyCombat calls Play(animName)
/// at the moment it plays an animation; this script does the rest. One generic script for every
/// enemy regardless of how many animations it has: right-click the component header and choose
/// "Sync From Animator" to auto-build one entry per animation state on that enemy's Animator.
/// Re-running Sync only adds missing entries, it never deletes or overwrites existing ones.
/// </summary>
public class EnemyFX : MonoBehaviour
{
    #region FX Entry
    [System.Serializable]
    public class FXEntry
    {
        [Tooltip("Animator state name this entry belongs to. Filled automatically by Sync From Animator. Must match the state name exactly.")]
        public string animationName;

        [Tooltip("Prefab spawned at the enemy's position when this animation plays. Drag prefab from Project window. Leave empty for no VFX.")]
        public GameObject vfx;

        [Tooltip("Clip played when this animation plays. Drag clip from Project window. Leave empty for no SFX.")]
        public AudioClip sfx;

        [Tooltip("Volume for this clip only (multiplied with the AudioSource volume).")]
        [Range(0f, 1f)]
        public float sfxVolume = 1f;
    }
    #endregion

    #region Inspector
    [Header("Setup")]
    [Tooltip("Seconds before a spawned VFX is auto-destroyed. Set higher than your longest particle lifetime.")]
    [SerializeField] private float vfxLifetime = 3f;

    [Tooltip("AudioSource all clips play through. Leave empty: one is found on this object or added automatically at runtime (3D positional, no play-on-awake).")]
    [SerializeField] private AudioSource audioSource;

    [Header("Per-Animation Effects")]
    [Tooltip("One entry per animation. Use Sync From Animator (right-click the component header) to build this list automatically. Empty slots simply do nothing.")]
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
            audioSource.spatialBlend = 1f; // 3D positional so enemy sounds sit in the world
        }

        BuildLookup();
    }
    #endregion

    #region Public API
    /// <summary>
    /// Plays the VFX and SFX assigned to the given animation state name.
    /// Safe to call with any name: unknown names, empty names, and empty slots all no-op silently.
    /// VFX spawn mirrors the old EnemyCombat.PlayVFX (instantiate at enemy position/rotation,
    /// play the ParticleSystem, auto-destroy after vfxLifetime).
    /// </summary>
    public void Play(string animationName)
    {
        if (string.IsNullOrEmpty(animationName) || lookup == null)
            return;

        if (!lookup.TryGetValue(animationName.Trim(), out FXEntry entry))
            return;

        if (entry.vfx != null)
        {
            GameObject effect = Instantiate(entry.vfx, transform.position, transform.rotation);

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
            if (entry == null || string.IsNullOrEmpty(entry.animationName))
                continue;

            string key = entry.animationName.Trim();
            if (!lookup.ContainsKey(key))
                lookup.Add(key, entry);
        }
    }
    #endregion

#if UNITY_EDITOR
    #region Editor Sync
    /// <summary>
    /// Reads this enemy's Animator Controller and adds one entry for every animation state that
    /// does not already have one. Never deletes or overwrites existing entries, so assigned
    /// effects always survive a re-sync. Right-click the EnemyFX component header to run it.
    /// </summary>
    [ContextMenu("Sync From Animator")]
    private void SyncFromAnimator()
    {
        Animator anim = GetComponent<Animator>();
        if (anim == null)
            anim = GetComponentInChildren<Animator>();

        if (anim == null || anim.runtimeAnimatorController == null)
        {
            Debug.LogWarning($"[EnemyFX] {gameObject.name}: no Animator with a controller found. Nothing synced.");
            return;
        }

        List<string> stateNames = new List<string>();

        var controller = anim.runtimeAnimatorController as UnityEditor.Animations.AnimatorController;
        if (controller != null)
        {
            // Normal case: walk every layer and sub-state machine, collect state names.
            foreach (var layer in controller.layers)
                CollectStates(layer.stateMachine, stateNames);
        }
        else
        {
            // Override-controller fallback: state names are unreachable, use clip names instead.
            foreach (AnimationClip clip in anim.runtimeAnimatorController.animationClips)
            {
                if (clip != null && !stateNames.Contains(clip.name))
                    stateNames.Add(clip.name);
            }
        }

        int added = 0;
        foreach (string name in stateNames)
        {
            bool exists = entries.Exists(e => e != null && e.animationName == name);
            if (!exists)
            {
                entries.Add(new FXEntry { animationName = name });
                added++;
            }
        }

        UnityEditor.EditorUtility.SetDirty(this);
        Debug.Log($"[EnemyFX] {gameObject.name}: sync complete. {added} new entries added, {entries.Count} total.");
    }

    /// <summary>
    /// Recursively collects state names from a state machine and all its sub-state machines.
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
