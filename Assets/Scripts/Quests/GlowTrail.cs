using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Tomoe-only quest wayfinding: a hand-placed chain of soft golden glows on the ground.
///
/// Setup: create an empty GameObject, add this component, set questId (and stepId when
/// the trail belongs to one step), then add empty children along the route. Each child
/// becomes a pulsing ground glow; the LAST child becomes the larger arrival glow.
///
/// Visibility, all three at once:
///   1. The quest is the TRACKED quest (chosen on the Memory Parchment), and
///   2. that step is the quest's current step (stepId empty = whole quest), and
///   3. the player is in Tomoe form. Yoru sees nothing. (GDD: the world only speaks
///      to the grandmother.)
///
/// Visuals are fully procedural (generated soft-dot texture on additive quads), so the
/// trail works with zero asset dependencies. Swapping in a VFX prefab later only touches
/// BuildPoint.
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

    [Header("Look (procedural fallback, and arrival scaling)")]
    [Tooltip("Diameter in metres of a path glow")]
    [SerializeField] private float pointSize = 0.9f;

    [Tooltip("The last child is the arrival glow: pointSize times this")]
    [SerializeField] private float arrivalSizeMultiplier = 2.2f;

    [Tooltip("Warm gold by default. Alpha is part of the look on the additive material")]
    [SerializeField] private Color glowColor = new Color(1f, 0.82f, 0.45f, 0.55f);

    [Tooltip("Pulse cycles per second-ish. Points pulse out of phase down the chain")]
    [SerializeField] private float pulseSpeed = 2.2f;

    [Tooltip("Metres above each point's position, so the quad never z-fights the ground")]
    [SerializeField] private float groundOffset = 0.06f;
    #endregion

    #region State
    private readonly List<GameObject> glowInstances = new List<GameObject>();
    private readonly List<Transform> billboardQuads = new List<Transform>(); // procedural fallback only
    private FormController formController;
    private Transform cameraTransform;
    private bool usingPrefabs;
    private bool currentlyVisible;

    private static Texture2D sharedDotTexture;
    private Material trailMaterial;
    #endregion

    #region Lifecycle
    private void Start()
    {
        formController = FindObjectOfType<FormController>();
        if (formController == null)
            Debug.LogWarning($"[GlowTrail] {name}: FormController not found. Trail will never show (Tomoe gate cannot pass).");

        BuildPoints();
        SetVisible(false);
    }

    private void Update()
    {
        bool visible = QuestManager.Instance != null
                    && QuestManager.Instance.IsTrailVisible(questId, stepId)
                    && formController != null
                    && formController.IsHuman;

        if (visible != currentlyVisible)
            SetVisible(visible);

        if (!currentlyVisible || usingPrefabs) return;

        // Procedural fallback only: billboard each quad toward the camera (a flat
        // ground disc reads as a thin line from a standing camera) and breathe a soft
        // pulse, phase-shifted along the chain so the trail flows toward the destination.
        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;

        for (int i = 0; i < billboardQuads.Count; i++)
        {
            Transform quad = billboardQuads[i];
            if (quad == null) continue;

            if (cameraTransform != null)
                quad.rotation = Quaternion.LookRotation(quad.position - cameraTransform.position);

            float baseSize = i == billboardQuads.Count - 1 ? pointSize * arrivalSizeMultiplier : pointSize;
            float pulse = 1f + 0.18f * Mathf.Sin(Time.time * pulseSpeed - i * 0.8f);
            quad.localScale = Vector3.one * baseSize * pulse;
        }
    }
    #endregion

    #region Build
    /// <summary>
    /// One visual per child point. With prefabs assigned: YOUR effect at every point,
    /// the arrival effect (or a scaled point effect) at the last. Without prefabs: a
    /// procedural camera-facing additive glow as a working placeholder.
    /// </summary>
    private void BuildPoints()
    {
        usingPrefabs = pointEffectPrefab != null;
        if (!usingPrefabs)
            EnsureSharedVisuals();

        int count = transform.childCount;
        for (int i = 0; i < count; i++)
        {
            Transform point = transform.GetChild(i);
            bool isArrival = i == count - 1;

            if (usingPrefabs)
            {
                GameObject prefab = isArrival && arrivalEffectPrefab != null
                    ? arrivalEffectPrefab
                    : pointEffectPrefab;

                GameObject instance = Instantiate(prefab, point);
                instance.transform.localPosition = new Vector3(0f, groundOffset, 0f);

                // No dedicated arrival effect: same effect, scaled up.
                if (isArrival && arrivalEffectPrefab == null)
                    instance.transform.localScale *= arrivalSizeMultiplier;

                glowInstances.Add(instance);
            }
            else
            {
                GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                quad.name = "Glow";
                Object.Destroy(quad.GetComponent<Collider>());

                quad.transform.SetParent(point, false);
                quad.transform.localPosition = new Vector3(0f, groundOffset + pointSize * 0.4f, 0f);

                Renderer rend = quad.GetComponent<Renderer>();
                rend.sharedMaterial = trailMaterial;
                rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                rend.receiveShadows = false;

                glowInstances.Add(quad);
                billboardQuads.Add(quad.transform);
            }
        }

        if (glowInstances.Count == 0)
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

    private void SetVisible(bool visible)
    {
        currentlyVisible = visible;
        Debug.Log($"[GlowTrail] {name}: {(visible ? "ON (quest tracked, step current, Tomoe form)" : "off")}");
        for (int i = 0; i < glowInstances.Count; i++)
        {
            if (glowInstances[i] != null && glowInstances[i].activeSelf != visible)
                glowInstances[i].SetActive(visible);
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
            float r = (i == transform.childCount - 1 ? pointSize * arrivalSizeMultiplier : pointSize) * 0.5f;
            Gizmos.DrawWireSphere(point.position, r);
            if (previous != null)
                Gizmos.DrawLine(previous.position, point.position);
            previous = point;
        }
    }
    #endregion
}
