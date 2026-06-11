using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Tomoe-only quest wayfinding: a hand-placed chain of glowing rings the player passes
/// through one by one, like flying through rings. Reusable for any quest: add a GlowTrail
/// object per quest, set questId/stepId, add as many P point children as the route needs.
///
/// Setup: create an empty GameObject, add this component, set questId (and stepId when
/// the trail belongs to one step), then add empty children along the route (P01, P02...).
/// Any number of children works; the LAST child is always the arrival ring.
///
/// Per-ring control, live, from the inspector, via the P point children:
///   MOVE a ring   = move its P point.
///   RESIZE a ring = scale its P point (uniform; the X value is used as the multiplier).
/// P point ROTATION is ignored. All rings use the Ring Rotation field below, applied
/// from code LIVE every frame (default 90 on X stands flat effects upright), so tweaks
/// show instantly, including during Play.
/// Never edit the runtime "<name>_Effects" container at the scene root; it is rebuilt
/// every Play. The P points are the only editing surface.
///
/// Pass detection is HORIZONTAL (XZ) within triggerRadius, with a separate vertical
/// tolerance (triggerHeight), so walking under or through a raised ring still counts.
///
/// Sequential reveal: only the current ring and the next revealAhead ring(s) are lit.
/// Passing a ring plays the pass sound + burst and that ring is gone for the session,
/// while the next one lights up ahead. The FINAL ring uses its own special sound and
/// burst when set, falling back to the normal ones when empty.
///
/// Visibility, all three at once:
///   1. The quest is the TRACKED quest (chosen on the Memory Parchment), and
///   2. that step is the quest's current step (stepId empty = whole quest), and
///   3. the player is in Tomoe form. Yoru sees nothing. (GDD: the world only speaks
///      to the grandmother.)
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
    [Tooltip("Your particle effect, spawned at every trail point. Use a LOOPING effect. Leave empty to use the built-in procedural glow")]
    [SerializeField] private GameObject pointEffectPrefab;

    [Tooltip("A different effect for the FINAL point only, the you-have-arrived marker. Empty = the point effect scaled up by the arrival multiplier")]
    [SerializeField] private GameObject arrivalEffectPrefab;

    [Tooltip("Rotation in degrees applied to EVERY ring from code, LIVE (changes show instantly, even in Play). Default (90,0,0) stands a flat effect upright. P point rotation is ignored")]
    [SerializeField] private Vector3 ringRotation = new Vector3(90f, 0f, 0f);

    [Header("Pass Through (walk through a ring: sound plays, burst spawns, ring disappears)")]
    [Tooltip("Played at the ring's position when the player passes through it")]
    [SerializeField] private AudioClip passSound;

    [Tooltip("SPECIAL sound for the FINAL ring only. Empty = the normal pass sound")]
    [SerializeField] private AudioClip arrivalPassSound;

    [Tooltip("Volume for pass sounds")]
    [Range(0f, 1f)]
    [SerializeField] private float passSoundVolume = 0.9f;

    [Tooltip("One-shot burst spawned where the ring was (a little poof). Use a NON-looping effect. Empty = none")]
    [SerializeField] private GameObject passEffectPrefab;

    [Tooltip("SPECIAL burst for the FINAL ring only. Empty = the normal pass effect")]
    [SerializeField] private GameObject arrivalPassEffectPrefab;

    [Tooltip("Horizontal distance from the player to a ring's P point, in metres, to pass it")]
    [SerializeField] private float triggerRadius = 1.5f;

    [Tooltip("Vertical tolerance in metres: a ring up to this far above/below the player still counts as passed")]
    [SerializeField] private float triggerHeight = 3f;

    [Header("Reveal (rings light up one ahead as you progress)")]
    [Tooltip("ON: only the current ring plus the next Reveal Ahead ring(s) are lit. OFF: the whole trail is lit at once")]
    [SerializeField] private bool sequentialReveal = true;

    [Tooltip("How many rings beyond the current one are lit. 1 = the next ring appears before the current one is passed")]
    [SerializeField] private int revealAhead = 1;

    [Header("Look (PLACEHOLDER ONLY except where noted; these do NOT change your prefab)")]
    [Tooltip("PLACEHOLDER ONLY: diameter in metres of a built-in path glow")]
    [SerializeField] private float pointSize = 0.9f;

    [Tooltip("BOTH MODES: the last ring is scaled up by this when no dedicated arrival prefab is set")]
    [SerializeField] private float arrivalSizeMultiplier = 2.2f;

    [Tooltip("PLACEHOLDER ONLY: warm gold by default")]
    [SerializeField] private Color glowColor = new Color(1f, 0.82f, 0.45f, 0.55f);

    [Tooltip("PLACEHOLDER ONLY: pulse cycles per second-ish")]
    [SerializeField] private float pulseSpeed = 2.2f;

    [Tooltip("BOTH MODES: metres above each point's position, so effects never z-fight the ground")]
    [SerializeField] private float groundOffset = 0.06f;
    #endregion

    #region State
    /// <summary>One ring along the route.</summary>
    private class TrailPoint
    {
        public Transform anchor;          // the P-point child; position + scale read live every frame
        public GameObject instance;       // the spawned visual (prefab instance or placeholder quad)
        public Transform quad;            // placeholder mode only, for billboard + pulse
        public Vector3 baseScale;         // authored prefab scale (incl. arrival multiplier), prefab mode
        public Quaternion prefabRotation; // authored prefab rotation, combined live with ringRotation
        public bool isArrival;
        public bool consumed;             // player passed through it, gone for the session
    }

    private readonly List<TrailPoint> points = new List<TrailPoint>();
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

        BuildPoints();
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

        for (int i = 0; i < points.Count; i++)
        {
            TrailPoint point = points[i];
            if (point.quad == null || point.consumed || !point.instance.activeSelf) continue;

            if (cameraTransform != null)
                point.quad.rotation = Quaternion.LookRotation(point.quad.position - cameraTransform.position);

            float baseSize = point.isArrival ? pointSize * arrivalSizeMultiplier : pointSize;
            float pulse = 1f + 0.18f * Mathf.Sin(Time.time * pulseSpeed - i * 0.8f);
            point.quad.localScale = Vector3.one * baseSize * pulse * AnchorScale(point.anchor);
        }
    }
    #endregion

    #region Build
    /// <summary>
    /// One visual per child point, spawned into a clean scale-1 scene-root container.
    /// Point transforms mark position and per-ring size; rotation comes from ringRotation
    /// in code; the scene hierarchy never touches the effects.
    /// </summary>
    private void BuildPoints()
    {
        usingPrefabs = pointEffectPrefab != null;
        if (!usingPrefabs)
            EnsureSharedVisuals();

        effectsRoot = new GameObject($"{name}_Effects").transform;

        int count = transform.childCount;
        for (int i = 0; i < count; i++)
        {
            Transform anchor = transform.GetChild(i);
            bool isArrival = i == count - 1;

            TrailPoint trailPoint = new TrailPoint { anchor = anchor, isArrival = isArrival };

            if (usingPrefabs)
            {
                GameObject prefab = isArrival && arrivalEffectPrefab != null
                    ? arrivalEffectPrefab
                    : pointEffectPrefab;

                trailPoint.prefabRotation = prefab.transform.rotation;

                Vector3 spawnPosition = anchor.position + Vector3.up * groundOffset;
                GameObject instance = Instantiate(
                    prefab, spawnPosition, Quaternion.Euler(ringRotation) * trailPoint.prefabRotation, effectsRoot);

                float sizeMultiplier = isArrival && arrivalEffectPrefab == null ? arrivalSizeMultiplier : 1f;
                trailPoint.baseScale = prefab.transform.localScale * sizeMultiplier;
                instance.transform.localScale = trailPoint.baseScale * AnchorScale(anchor);

                trailPoint.instance = instance;
            }
            else
            {
                GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                quad.name = "Glow";
                Object.Destroy(quad.GetComponent<Collider>());

                quad.transform.SetParent(effectsRoot, false);
                quad.transform.position = anchor.position + Vector3.up * (groundOffset + pointSize * 0.4f);

                Renderer rend = quad.GetComponent<Renderer>();
                rend.sharedMaterial = trailMaterial;
                rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                rend.receiveShadows = false;

                trailPoint.instance = quad;
                trailPoint.quad = quad.transform;
            }

            points.Add(trailPoint);
        }

        if (points.Count == 0)
            Debug.LogWarning($"[GlowTrail] {name}: no child points. Add empty children along the route.");
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
    /// Keeps each ring glued to its P point (position + per-ring size) and re-applies
    /// ringRotation, all live every frame, then applies the sequential reveal window:
    /// the first unpassed ring plus revealAhead more.
    /// </summary>
    private void SyncAndReveal()
    {
        Quaternion codeRotation = Quaternion.Euler(ringRotation);

        int firstUnconsumed = -1;
        for (int i = 0; i < points.Count; i++)
        {
            if (!points[i].consumed) { firstUnconsumed = i; break; }
        }

        for (int i = 0; i < points.Count; i++)
        {
            TrailPoint point = points[i];
            if (point.instance == null) continue;

            bool lit = !point.consumed
                    && (!sequentialReveal || firstUnconsumed < 0 || i <= firstUnconsumed + revealAhead);

            if (point.instance.activeSelf != lit)
                point.instance.SetActive(lit);

            if (!lit || point.anchor == null) continue;

            if (usingPrefabs)
            {
                point.instance.transform.SetPositionAndRotation(
                    point.anchor.position + Vector3.up * groundOffset,
                    codeRotation * point.prefabRotation);
                point.instance.transform.localScale = point.baseScale * AnchorScale(point.anchor);
            }
            else
            {
                point.quad.position = point.anchor.position + Vector3.up * (groundOffset + pointSize * 0.4f);
            }
        }
    }

    /// <summary>
    /// Any LIT ring the player walks into is passed: sound + burst + gone for the session.
    /// Distance is horizontal (XZ) within triggerRadius, with triggerHeight of vertical
    /// tolerance, so raised rings still count. The final ring uses its special sound and
    /// burst when assigned.
    /// </summary>
    private void CheckPassThrough()
    {
        if (playerTransform == null) return;

        float sqrRadius = triggerRadius * triggerRadius;
        for (int i = 0; i < points.Count; i++)
        {
            TrailPoint point = points[i];
            if (point.consumed || point.instance == null || !point.instance.activeSelf || point.anchor == null)
                continue;

            Vector3 delta = playerTransform.position - point.anchor.position;
            float verticalDistance = Mathf.Abs(delta.y);
            delta.y = 0f;

            if (delta.sqrMagnitude <= sqrRadius && verticalDistance <= triggerHeight)
                ConsumePoint(point, i);
        }
    }

    private void ConsumePoint(TrailPoint point, int index)
    {
        point.consumed = true;

        if (point.instance != null)
            point.instance.SetActive(false);

        AudioClip clip = point.isArrival && arrivalPassSound != null ? arrivalPassSound : passSound;
        if (clip != null)
            AudioSource.PlayClipAtPoint(clip, point.anchor.position, passSoundVolume);

        GameObject burstPrefab = point.isArrival && arrivalPassEffectPrefab != null
            ? arrivalPassEffectPrefab
            : passEffectPrefab;

        if (burstPrefab != null)
        {
            GameObject burst = Instantiate(
                burstPrefab,
                point.anchor.position + Vector3.up * groundOffset,
                burstPrefab.transform.rotation,
                effectsRoot);
            burst.transform.localScale = burstPrefab.transform.localScale;
            Destroy(burst, passBurstLifetime);
        }

        Debug.Log($"[GlowTrail] {name}: ring {index + 1}/{points.Count} passed{(point.isArrival ? " (ARRIVAL)" : "")}");
    }

    /// <summary>Per-ring size multiplier read from the P point's scale (X component).</summary>
    private static float AnchorScale(Transform anchor)
    {
        return anchor == null ? 1f : Mathf.Max(0.01f, anchor.localScale.x);
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

        for (int i = 0; i < points.Count; i++)
        {
            TrailPoint point = points[i];
            if (point.instance != null && point.instance.activeSelf)
                point.instance.SetActive(false);
        }
    }
    #endregion

    #region Gizmos
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.82f, 0.45f, 0.9f);
        Transform previous = null;
        for (int i = 0; i < transform.childCount; i++)
        {
            Transform point = transform.GetChild(i);
            float baseR = (i == transform.childCount - 1 ? pointSize * arrivalSizeMultiplier : pointSize) * 0.5f;
            Gizmos.DrawWireSphere(point.position, baseR * Mathf.Max(0.01f, point.localScale.x));
            if (previous != null)
                Gizmos.DrawLine(previous.position, point.position);
            previous = point;
        }
    }
    #endregion
}