using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Tomoe-only quest wayfinding: a chain of glowing rings the player passes through one
/// by one. Fully reusable: one GlowTrail object per quest, any number of rings, rings
/// can be added, deleted, moved, resized, and reordered at any time.
///
/// SETUP (per trail):
///   1. Empty GameObject + this component. Set questId (and stepId if step-specific).
///      Assign effect prefabs and sounds.
///   2. Add empty children where rings should be (any names, any hierarchy order).
///   3. Right-click this component's header in the Inspector and choose
///      "Setup Rings From Children". Every child gets a GlowTrailRing component
///      numbered 10, 20, 30... by current hierarchy order. Save the scene.
///
/// HOW RINGS WORK (all on the GlowTrailRing component, nothing else matters):
///   SEQUENCE  = the Order number. Lower = earlier. The HIGHEST order is the arrival.
///               Insert a ring between 10 and 20 by giving it order 15. Hierarchy
///               position and creation time are IGNORED.
///   SIZE      = the Size field (1 = normal). Transform scale is IGNORED, and the ring
///               is pinned to its point by content-offset compensation, so resizing
///               NEVER moves it.
///   POSITION  = the point object's position. That is the only thing the transform does.
///   ROTATION  = the trail-wide Ring Rotation field below, applied live from code.
///               Point rotation is IGNORED.
///
/// Effects live in a clean scale-1 container at the scene root; the scene hierarchy
/// never touches them. Never edit the runtime "<name>_Effects" container.
///
/// Pass detection is HORIZONTAL (XZ) within triggerRadius, with triggerHeight of
/// vertical tolerance. Exactly ONE ring is visible at a time (hardcoded): passing it
/// plays the pass sound + burst, it vanishes, and the next ring appears in the same
/// instant. The FINAL ring uses its special sound and burst when set.
///
/// Visibility, all three at once: quest tracked + step current + Tomoe form.
/// (GDD: the world only speaks to the grandmother.)
/// </summary>
public class GlowTrail : MonoBehaviour
{
    #region Inspector
    [Header("Quest Link")]
    [Tooltip("questId this trail belongs to, e.g. \"stolen_face\"")]
    [SerializeField] private string questId;

    [Tooltip("stepId this trail belongs to, e.g. \"S1\". Empty = lit for the whole quest")]
    [SerializeField] private string stepId;

    [Header("Visual Prefabs (assign your own VFX)")]
    [Tooltip("Your particle effect, spawned at every ring. Use a LOOPING effect. Leave empty to use the built-in procedural glow")]
    [SerializeField] private GameObject pointEffectPrefab;

    [Tooltip("A different effect for the ARRIVAL ring only. Empty = the point effect scaled up by the arrival multiplier")]
    [SerializeField] private GameObject arrivalEffectPrefab;

    [Tooltip("Rotation in degrees applied to EVERY ring from code, LIVE. Default (90,0,0) stands a flat effect upright. Point rotation is ignored")]
    [SerializeField] private Vector3 ringRotation = new Vector3(90f, 0f, 0f);

    [Header("Pass Through (walk through a ring: sound plays, burst spawns, ring disappears)")]
    [Tooltip("Played at the ring's position when the player passes through it")]
    [SerializeField] private AudioClip passSound;

    [Tooltip("SPECIAL sound for the ARRIVAL ring only. Empty = the normal pass sound")]
    [SerializeField] private AudioClip arrivalPassSound;

    [Tooltip("Volume for pass sounds")]
    [Range(0f, 1f)]
    [SerializeField] private float passSoundVolume = 0.9f;

    [Tooltip("One-shot burst spawned where the ring was (a little poof). Use a NON-looping effect. Empty = none")]
    [SerializeField] private GameObject passEffectPrefab;

    [Tooltip("SPECIAL burst for the ARRIVAL ring only. Empty = the normal pass effect")]
    [SerializeField] private GameObject arrivalPassEffectPrefab;

    [Tooltip("Horizontal distance from the player to a ring, in metres, to pass it")]
    [SerializeField] private float triggerRadius = 1.5f;

    [Tooltip("Vertical tolerance in metres: a ring up to this far above/below the player still counts as passed")]
    [SerializeField] private float triggerHeight = 3f;

    [Header("Look (PLACEHOLDER ONLY except where noted; these do NOT change your prefab)")]
    [Tooltip("PLACEHOLDER ONLY: diameter in metres of a built-in path glow")]
    [SerializeField] private float pointSize = 0.9f;

    [Tooltip("BOTH MODES: the arrival ring is scaled up by this when no dedicated arrival prefab is set")]
    [SerializeField] private float arrivalSizeMultiplier = 2.2f;

    [Tooltip("PLACEHOLDER ONLY: warm gold by default")]
    [SerializeField] private Color glowColor = new Color(1f, 0.82f, 0.45f, 0.55f);

    [Tooltip("PLACEHOLDER ONLY: pulse cycles per second-ish")]
    [SerializeField] private float pulseSpeed = 2.2f;

    [Tooltip("BOTH MODES: metres above each ring's position, so effects never z-fight the ground")]
    [SerializeField] private float groundOffset = 0.06f;
    #endregion

    #region State
    /// <summary>One ring along the route, driven by its GlowTrailRing component.</summary>
    private class TrailEntry
    {
        public GlowTrailRing ring;          // order + size, read live
        public GameObject instance;         // the spawned visual (prefab instance or placeholder quad)
        public Transform quad;              // placeholder mode only, for billboard + pulse
        public Vector3 baseScale;           // authored prefab scale (incl. arrival multiplier), prefab mode
        public Quaternion prefabRotation;   // authored prefab rotation, combined live with ringRotation
        public Vector3 contentOffsetPerUnit;// prefab content offset from its root, per 1.0 of Size,
                                            // in the ring's local frame; used to pin the visuals to
                                            // the point so resizing never moves the ring
        public bool isArrival;
        public bool consumed;               // player passed through it, gone for the session
    }

    private readonly List<TrailEntry> entries = new List<TrailEntry>();
    private Transform effectsRoot;   // scene-root container, scale (1,1,1), owns every spawned visual
    private FormController formController;
    private Transform playerTransform;
    private Transform cameraTransform;
    private bool usingPrefabs;
    private bool currentlyVisible;

    private const float passBurstLifetime = 4f;

    private static Texture2D sharedDotTexture;
    private Material trailMaterial;
    #endregion

    #region Lifecycle
    private void Start()
    {
        formController = FindObjectOfType<FormController>();
        if (formController == null)
            Debug.LogWarning($"[GlowTrail] {name}: FormController not found. Trail will never show (Tomoe gate cannot pass).");
        else
            playerTransform = formController.transform;

        BuildEntries();
        SetVisible(false);
    }

    private void OnDestroy()
    {
        if (effectsRoot != null)
            Destroy(effectsRoot.gameObject);
    }

    private void Update()
    {
        bool visible = QuestManager.Instance != null
                    && QuestManager.Instance.IsTrailVisible(questId, stepId)
                    && formController != null
                    && formController.IsHuman;

        if (visible != currentlyVisible)
            SetVisible(visible);

        if (!currentlyVisible) return;

        SyncAndReveal();
        CheckPassThrough();

        if (usingPrefabs) return;

        // Placeholder mode only: billboard each lit quad toward the camera and breathe a
        // soft pulse, phase-shifted along the chain so the trail flows toward the goal.
        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;

        for (int i = 0; i < entries.Count; i++)
        {
            TrailEntry entry = entries[i];
            if (entry.quad == null || entry.consumed || !entry.instance.activeSelf) continue;

            if (cameraTransform != null)
                entry.quad.rotation = Quaternion.LookRotation(entry.quad.position - cameraTransform.position);

            float baseSize = entry.isArrival ? pointSize * arrivalSizeMultiplier : pointSize;
            float pulse = 1f + 0.18f * Mathf.Sin(Time.time * pulseSpeed - i * 0.8f);
            entry.quad.localScale = Vector3.one * baseSize * pulse * RingSize(entry.ring);
        }
    }
    #endregion

    #region Public Lookup (Yuki guide)
    /// <summary>
    /// True while this trail is lit in the world (quest tracked + step current +
    /// Tomoe form). YukiGuide scans for the one lit trail.
    /// </summary>
    public bool IsLit => currentlyVisible;

    /// <summary>
    /// Position of the CURRENT ring: the single lit one, the first unpassed in the
    /// sequence. False when every ring has been passed. Yuki leads the player here.
    /// </summary>
    public bool TryGetCurrentRingPosition(out Vector3 position)
    {
        for (int i = 0; i < entries.Count; i++)
        {
            TrailEntry entry = entries[i];
            if (entry.consumed || entry.ring == null) continue;
            position = entry.ring.transform.position;
            return true;
        }
        position = Vector3.zero;
        return false;
    }
    #endregion

    #region Editor Setup
    /// <summary>
    /// One-click setup: adds a GlowTrailRing to every direct child that lacks one and
    /// numbers ALL of them 10, 20, 30... by current hierarchy order (existing sizes are
    /// kept). Run it again any time after dragging children around to renumber. To
    /// insert a ring between two others without renumbering, just give it an in-between
    /// order (e.g. 15 between 10 and 20).
    /// </summary>
    [ContextMenu("Setup Rings From Children")]
    private void SetupRingsFromChildren()
    {
        int order = 10;
        for (int i = 0; i < transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);
            GlowTrailRing ring = child.GetComponent<GlowTrailRing>();
            if (ring == null)
                ring = child.gameObject.AddComponent<GlowTrailRing>();

            ring.order = order;
            order += 10;
        }
        Debug.Log($"[GlowTrail] {name}: {transform.childCount} ring(s) set up and numbered by hierarchy order. Save the scene.");
    }
    #endregion

    #region Build
    /// <summary>
    /// Collects all GlowTrailRing children, sorts them by Order (ties broken by
    /// hierarchy position), and spawns one visual per ring into a clean scale-1
    /// scene-root container. The highest Order is the arrival.
    /// </summary>
    private void BuildEntries()
    {
        GlowTrailRing[] rings = GetComponentsInChildren<GlowTrailRing>(true)
            .OrderBy(r => r.order)
            .ThenBy(r => r.transform.GetSiblingIndex())
            .ToArray();

        if (rings.Length == 0)
        {
            Debug.LogWarning($"[GlowTrail] {name}: no GlowTrailRing children found. Right-click the GlowTrail component and run \"Setup Rings From Children\".");
            return;
        }

        usingPrefabs = pointEffectPrefab != null;
        if (!usingPrefabs)
            EnsureSharedVisuals();

        effectsRoot = new GameObject($"{name}_Effects").transform;
        Quaternion codeRotation = Quaternion.Euler(ringRotation);

        for (int i = 0; i < rings.Length; i++)
        {
            GlowTrailRing ring = rings[i];
            bool isArrival = i == rings.Length - 1;

            TrailEntry entry = new TrailEntry { ring = ring, isArrival = isArrival };

            if (usingPrefabs)
            {
                GameObject prefab = isArrival && arrivalEffectPrefab != null
                    ? arrivalEffectPrefab
                    : pointEffectPrefab;

                entry.prefabRotation = prefab.transform.rotation;

                float sizeMultiplier = isArrival && arrivalEffectPrefab == null ? arrivalSizeMultiplier : 1f;
                entry.baseScale = prefab.transform.localScale * sizeMultiplier;

                Quaternion spawnRotation = codeRotation * entry.prefabRotation;
                GameObject instance = Instantiate(prefab, ring.transform.position, spawnRotation, effectsRoot);
                instance.transform.localScale = entry.baseScale * RingSize(ring);

                // Measure how far the prefab's particle content sits from its root, per
                // 1.0 of Size, in the ring's local frame. SyncAndReveal uses this to pin
                // the CONTENT (not the root) to the point, so resizing never moves it.
                entry.contentOffsetPerUnit = MeasureContentOffsetPerUnit(instance, spawnRotation, RingSize(ring));

                entry.instance = instance;
            }
            else
            {
                GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                quad.name = "Glow";
                Object.Destroy(quad.GetComponent<Collider>());

                quad.transform.SetParent(effectsRoot, false);
                quad.transform.position = ring.transform.position + Vector3.up * (groundOffset + pointSize * 0.4f);

                Renderer rend = quad.GetComponent<Renderer>();
                rend.sharedMaterial = trailMaterial;
                rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                rend.receiveShadows = false;

                entry.instance = quad;
                entry.quad = quad.transform;
            }

            entries.Add(entry);
        }

        string sequence = string.Join(" -> ", entries.Select(e => $"{e.ring.name}({e.ring.order})"));
        Debug.Log($"[GlowTrail] {name}: {entries.Count} ring(s), sequence: {sequence}, one ring shown at a time");
    }

    /// <summary>
    /// Average position of the spawned effect's ParticleSystems relative to its root,
    /// converted to the ring's local frame and normalised to Size = 1. Zero when the
    /// effect has no particle systems or its content is already centred on the root.
    /// </summary>
    private static Vector3 MeasureContentOffsetPerUnit(GameObject instance, Quaternion rotation, float sizeAtBuild)
    {
        ParticleSystem[] systems = instance.GetComponentsInChildren<ParticleSystem>(true);
        if (systems.Length == 0 || sizeAtBuild <= 0f)
            return Vector3.zero;

        Vector3 sum = Vector3.zero;
        for (int i = 0; i < systems.Length; i++)
            sum += systems[i].transform.position;

        Vector3 worldOffset = sum / systems.Length - instance.transform.position;
        return Quaternion.Inverse(rotation) * worldOffset / sizeAtBuild;
    }

    /// <summary>
    /// One soft-dot texture shared by every trail, one material per trail so glowColor
    /// applies per trail. Additive particle shader, BRP-safe with a sprite fallback.
    /// </summary>
    private void EnsureSharedVisuals()
    {
        if (sharedDotTexture == null)
            BuildSharedDotTexture();

        Shader shader = Shader.Find("Legacy Shaders/Particles/Additive");
        if (shader == null) shader = Shader.Find("Particles/Standard Unlit");
        if (shader == null) shader = Shader.Find("Sprites/Default");

        trailMaterial = new Material(shader);
        trailMaterial.mainTexture = sharedDotTexture;
        if (trailMaterial.HasProperty("_TintColor"))
            trailMaterial.SetColor("_TintColor", glowColor);
        else
            trailMaterial.color = glowColor;
    }

    private static void BuildSharedDotTexture()
    {
        const int size = 128;
        sharedDotTexture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        sharedDotTexture.wrapMode = TextureWrapMode.Clamp;

        Vector2 centre = new Vector2(size * 0.5f, size * 0.5f);
        float maxDist = size * 0.5f;
        Color32[] pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), centre) / maxDist;
                // Bright core, long soft falloff.
                float a = Mathf.Clamp01(1f - d);
                a = a * a * (3f - 2f * a); // smoothstep
                a *= a;
                pixels[y * size + x] = new Color(1f, 1f, 1f, a);
            }
        }
        sharedDotTexture.SetPixels32(pixels);
        sharedDotTexture.Apply();
    }
    #endregion

    #region Reveal + Pass Through
    /// <summary>
    /// Keeps each ring pinned to its point (content-centred, so Size changes never move
    /// it), re-applies ringRotation and Size live every frame, then applies the
    /// sequencing rule: exactly one ring lit, the first unpassed one.
    /// </summary>
    private void SyncAndReveal()
    {
        Quaternion codeRotation = Quaternion.Euler(ringRotation);

        int firstUnconsumed = -1;
        for (int i = 0; i < entries.Count; i++)
        {
            if (!entries[i].consumed) { firstUnconsumed = i; break; }
        }

        for (int i = 0; i < entries.Count; i++)
        {
            TrailEntry entry = entries[i];
            if (entry.instance == null || entry.ring == null) continue;

            // HARDCODED: exactly ONE ring at a time. Only the first unpassed ring is
            // lit; the moment it is passed, the next one lights in the same instant.
            bool lit = !entry.consumed && i == firstUnconsumed;

            if (entry.instance.activeSelf != lit)
                entry.instance.SetActive(lit);

            if (!lit) continue;

            if (usingPrefabs)
            {
                float ringSize = RingSize(entry.ring);
                Quaternion rotation = codeRotation * entry.prefabRotation;
                Vector3 target = entry.ring.transform.position + Vector3.up * groundOffset;

                // Pin the CONTENT centre to the point: shift the root by the scaled,
                // rotated content offset so resizing grows the ring in place.
                Vector3 rootPosition = target - rotation * (entry.contentOffsetPerUnit * ringSize);

                entry.instance.transform.SetPositionAndRotation(rootPosition, rotation);
                entry.instance.transform.localScale = entry.baseScale * ringSize;
            }
            else
            {
                entry.quad.position = entry.ring.transform.position + Vector3.up * (groundOffset + pointSize * 0.4f);
            }
        }
    }

    /// <summary>
    /// Any LIT ring the player walks into is passed: sound + burst + gone for the session.
    /// Distance is horizontal (XZ) within triggerRadius, with triggerHeight of vertical
    /// tolerance, so raised rings still count. The arrival ring uses its special sound
    /// and burst when assigned.
    /// </summary>
    private void CheckPassThrough()
    {
        if (playerTransform == null) return;

        float sqrRadius = triggerRadius * triggerRadius;
        for (int i = 0; i < entries.Count; i++)
        {
            TrailEntry entry = entries[i];
            if (entry.consumed || entry.instance == null || !entry.instance.activeSelf || entry.ring == null)
                continue;

            Vector3 delta = playerTransform.position - entry.ring.transform.position;
            float verticalDistance = Mathf.Abs(delta.y);
            delta.y = 0f;

            if (delta.sqrMagnitude <= sqrRadius && verticalDistance <= triggerHeight)
                ConsumeEntry(entry, i);
        }
    }

    private void ConsumeEntry(TrailEntry entry, int index)
    {
        entry.consumed = true;

        if (entry.instance != null)
            entry.instance.SetActive(false);

        AudioClip clip = entry.isArrival && arrivalPassSound != null ? arrivalPassSound : passSound;
        if (clip != null)
            AudioSource.PlayClipAtPoint(clip, entry.ring.transform.position, passSoundVolume);

        GameObject burstPrefab = entry.isArrival && arrivalPassEffectPrefab != null
            ? arrivalPassEffectPrefab
            : passEffectPrefab;

        if (burstPrefab != null)
        {
            GameObject burst = Instantiate(
                burstPrefab,
                entry.ring.transform.position + Vector3.up * groundOffset,
                burstPrefab.transform.rotation,
                effectsRoot);
            burst.transform.localScale = burstPrefab.transform.localScale;
            Destroy(burst, passBurstLifetime);
        }

        Debug.Log($"[GlowTrail] {name}: ring {index + 1}/{entries.Count} passed{(entry.isArrival ? " (ARRIVAL)" : "")}");
    }

    /// <summary>Per-ring size multiplier from the GlowTrailRing component, guarded.</summary>
    private static float RingSize(GlowTrailRing ring)
    {
        return ring == null ? 1f : Mathf.Max(0.01f, ring.size);
    }
    #endregion

    #region Visibility
    private void SetVisible(bool visible)
    {
        currentlyVisible = visible;
        Debug.Log($"[GlowTrail] {name}: {(visible ? "ON (quest tracked, step current, Tomoe form)" : "off")}");

        if (visible)
        {
            // SyncAndReveal lights the correct window on the next Update.
            SyncAndReveal();
            return;
        }

        for (int i = 0; i < entries.Count; i++)
        {
            TrailEntry entry = entries[i];
            if (entry.instance != null && entry.instance.activeSelf)
                entry.instance.SetActive(false);
        }
    }
    #endregion

    #region Gizmos
    private void OnDrawGizmosSelected()
    {
        GlowTrailRing[] rings = GetComponentsInChildren<GlowTrailRing>(true)
            .OrderBy(r => r.order)
            .ThenBy(r => r.transform.GetSiblingIndex())
            .ToArray();

        Gizmos.color = new Color(1f, 0.82f, 0.45f, 0.9f);
        Transform previous = null;
        for (int i = 0; i < rings.Length; i++)
        {
            Transform point = rings[i].transform;
            float baseR = (i == rings.Length - 1 ? pointSize * arrivalSizeMultiplier : pointSize) * 0.5f;
            Gizmos.DrawWireSphere(point.position, baseR * Mathf.Max(0.01f, rings[i].size));
            if (previous != null)
                Gizmos.DrawLine(previous.position, point.position);
            previous = point;
        }
    }
    #endregion
}