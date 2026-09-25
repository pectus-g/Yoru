using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// ROUND 96 (25 Sep 2026, step 10b): Yoru's eye glow follows the light that falls on her.
///
/// Her words: in the cave the glow is right as it is, a small glow shows in the dark. In bright
/// scenes the light swallows the glow, so the eyes need to glow brighter there. She sets how the eyes
/// look in her darkest place and in her brightest place; everything in between follows the light.
///
/// THE LIGHT ON HER, read four times a second:
///   the scene's sun (Window > Rendering > Lighting > Environment > Sun Source, which COZY drives):
///     its colour x intensity x how high it stands, counted only when a ray from above her head
///     towards it hits nothing, so a cave roof, an overhang or a wall takes it away;
///   plus the sky light (the scene's ambient: the sky, horizon and ground colours for Gradient,
///     the one colour for Color, the ambient probe for Skybox), counted only when the ray straight
///     up from above her head hits nothing within Sky Check Height.
/// COZY's time of day and weather, the balance system's light states, night and caves all change it.
/// The rays use the same origin, height and layers as Yoru Fur Weather's roof check (her rain).
///
/// THE GLOW: at Darkest Light and below the eyes use Glow In Darkest (1 = Eye.mat's own glow, the
/// cave look), at Brightest Light and above Glow In Brightest, smoothly in between. The middle of the
/// last five readings is used and eased over Adapt Seconds, so a flash or a passing branch cannot
/// flicker it. Only the emission of her eye slot changes, through a property override on that one
/// slot: Eye.mat itself, the fur, Night Vision, her lights and the post look are not touched, and at
/// exactly x1 the renderer is not touched at all.
///
/// ROUND 95 read the brightness of the whole screen instead; her cave and day tests on 25 Sep showed
/// the two screens are about equally bright (the cave's post look brightens it), so the screen could
/// not tell the cave from the day.
/// </summary>
[DisallowMultipleComponent]
public class YoruEyeGlow : MonoBehaviour
{
    [Header("=== THE EYES ===")]
    [Tooltip("The renderer that draws her eyes: model:Cat_BODY. Empty = found by the eye material among her renderers.")]
    [SerializeField] private Renderer eyeRenderer;

    [Tooltip("Her eye material, Assets/YORU/Eye.mat. Its own Emission colour is the glow at x1. The material file is never changed.")]
    [SerializeField] private Material eyeMaterial;

    [Header("=== YOUR DARKEST PLACE ===")]
    [Tooltip("The light on her that counts as your darkest place (watch Light Now in Play to see what a place reads). At this light and below, the eyes use Glow In Darkest.")]
    [Min(0f)]
    [SerializeField] private float darkestLight = 1.2f;

    [Tooltip("How strong the eye glow is in the darkest place. 1 = Eye.mat's own glow, the cave look.")]
    [Range(0.2f, 10f)]
    [SerializeField] private float glowInDarkest = 1f;

    [Header("=== YOUR BRIGHTEST PLACE ===")]
    [Tooltip("The light on her that counts as your brightest place. At this light and above, the eyes use Glow In Brightest.")]
    [Min(0f)]
    [SerializeField] private float brightestLight = 2.4f;

    [Tooltip("How strong the eye glow is in the brightest place (times Eye.mat's own glow).")]
    [Range(0.2f, 10f)]
    [SerializeField] private float glowInBrightest = 1f;

    [Header("=== HOW THE LIGHT IS READ ===")]
    [Tooltip("How many times a second the light on her is read.")]
    [Range(1f, 10f)]
    [SerializeField] private float readingsPerSecond = 4f;

    [Tooltip("Seconds the eyes take to follow a change in light (real time, so slow motion does not slow it).")]
    [Range(0.05f, 5f)]
    [SerializeField] private float adaptSeconds = 1f;

    [Tooltip("Metres above her feet the rays start, above her own body (Yoru Fur Weather's roof check uses 2.6).")]
    [SerializeField] private float lightCheckOrigin = 2.6f;

    [Tooltip("How far the ray towards the sun looks for something in the way, metres.")]
    [SerializeField] private float sunCheckDistance = 200f;

    [Tooltip("How far the ray straight up looks for a roof, metres (Yoru Fur Weather's roof check uses 30).")]
    [SerializeField] private float skyCheckHeight = 30f;

    [Tooltip("What can stand between her and the sun or the sky: the same layers as Yoru Fur Weather's roof check.")]
    [SerializeField] private LayerMask lightCheckMask = (1 << 0) | (1 << 4) | (1 << 6) | (1 << 7) | (1 << 8) | (1 << 11) | (1 << 13);

    [Header("=== DEBUG ===")]
    [Tooltip("Console lines: ready, the first reading, every change of 0.1 or more in the glow, going under cover and back, and a summary every 10 seconds.")]
    [SerializeField] private bool showDebugLogs = true;

    [Tooltip("Shown live in Play: the light on her now. Typing here does nothing.")]
    [SerializeField] private float lightNow;

    [Tooltip("Shown live in Play: how many times her own eye glow is used now. Typing here does nothing.")]
    [SerializeField] private float glowNow = 1f;

    private const int MedianCount = 5;
    private const float SummarySeconds = 10f;
    private const float LogStep = 0.1f;
    private const float FirstReadingDelay = 0.3f;

    private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
    private static readonly Vector3[] SkyDirections =
    {
        Vector3.up, Vector3.forward, Vector3.back, Vector3.left, Vector3.right,
    };

    private readonly Color[] skySamples = new Color[5];
    private readonly List<Material> materialsBuffer = new List<Material>();
    private readonly float[] readings = new float[MedianCount];
    private readonly float[] sorted = new float[MedianCount];

    private int eyeIndex = -1;
    private Color ownGlow;
    private MaterialPropertyBlock block;
    private bool blockSet;
    private bool ready;

    private float nextReading;
    private int readingCount;
    private int readingNext;
    private float targetGlow = 1f;
    private bool firstReading = true;
    private float loggedGlow = 1f;
    private bool lastSunClear;
    private bool lastSkyOpen;
    private Light lastSun;

    private float summaryUntil;
    private float summaryLow = float.MaxValue;
    private float summaryHigh = float.MinValue;
    private float summarySun;
    private float summarySky;
    private int summarySunClear;
    private int summarySkyOpen;
    private float summaryGlowLow = float.MaxValue;
    private float summaryGlowHigh = float.MinValue;
    private int summaryCount;

    private void Awake()
    {
        block = new MaterialPropertyBlock();
        ready = FindEyes();
        glowNow = 1f;
        targetGlow = 1f;
        if (ready && showDebugLogs)
        {
            Light sun = RenderSettings.sun;
            Debug.Log("[EyeGlow] ready: eyes = '" + eyeRenderer.name + "' slot " + eyeIndex + " '" + eyeMaterial.name + "', own glow "
                + ownGlow.r.ToString("F2") + " / " + ownGlow.g.ToString("F2") + " / " + ownGlow.b.ToString("F2")
                + "; darkest light " + darkestLight.ToString("F2") + " -> x" + glowInDarkest.ToString("F2")
                + ", brightest light " + brightestLight.ToString("F2") + " -> x" + glowInBrightest.ToString("F2")
                + "; sun '" + (sun != null ? sun.name : "none") + "', sky light from " + RenderSettings.ambientMode
                + "; " + readingsPerSecond.ToString("0.#") + " readings a second, middle of the last " + MedianCount
                + ", adapt " + adaptSeconds.ToString("F1") + " s.");
        }
    }

    private void OnEnable()
    {
        nextReading = Time.unscaledTime + FirstReadingDelay;
        summaryUntil = Time.unscaledTime + SummarySeconds;
    }

    private void OnDisable()
    {
        ClearEyes();
    }

    private void LateUpdate()
    {
        if (!ready)
        {
            return;
        }

        if (Time.unscaledTime >= nextReading)
        {
            nextReading = Time.unscaledTime + 1f / Mathf.Max(1f, readingsPerSecond);
            float sunPart;
            float skyPart;
            bool sunClear;
            bool skyOpen;
            float light = ReadLight(out sunPart, out sunClear, out skyPart, out skyOpen);
            Take(light, sunPart, sunClear, skyPart, skyOpen);
        }

        // Ease towards the glow the last readings asked for, in real time.
        float k = 1f - Mathf.Exp(-Time.unscaledDeltaTime / Mathf.Max(0.05f, adaptSeconds));
        glowNow = Mathf.Lerp(glowNow, targetGlow, k);
        if (Mathf.Abs(glowNow - targetGlow) < 0.0005f)
        {
            glowNow = targetGlow;
        }

        ApplyEyes();
    }

    // ---- the light on her ----

    private float ReadLight(out float sunPart, out bool sunClear, out float skyPart, out bool skyOpen)
    {
        Vector3 origin = transform.position + Vector3.up * lightCheckOrigin;

        sunPart = 0f;
        sunClear = false;
        Light sun = RenderSettings.sun;
        if (sun != null && sun.isActiveAndEnabled && sun.intensity > 0f)
        {
            Vector3 toSun = -sun.transform.forward;
            float height = Mathf.Clamp01(toSun.y);
            if (height > 0f)
            {
                sunClear = !Physics.Raycast(origin, toSun, sunCheckDistance, lightCheckMask, QueryTriggerInteraction.Ignore);
                if (sunClear)
                {
                    sunPart = Luminance(sun.color.linear) * sun.intensity * height;
                }
            }
        }

        skyOpen = !Physics.Raycast(origin, Vector3.up, skyCheckHeight, lightCheckMask, QueryTriggerInteraction.Ignore);
        skyPart = skyOpen ? SkyLight() : 0f;
        return sunPart + skyPart;
    }

    // The scene's ambient as Unity lights her with it, averaged over up and the four sides.
    private float SkyLight()
    {
        switch (RenderSettings.ambientMode)
        {
            case AmbientMode.Trilight:
                return (Luminance(RenderSettings.ambientSkyColor.linear) + Luminance(RenderSettings.ambientEquatorColor.linear)
                    + Luminance(RenderSettings.ambientGroundColor.linear)) / 3f;
            case AmbientMode.Skybox:
                SphericalHarmonicsL2 probe = RenderSettings.ambientProbe;
                probe.Evaluate(SkyDirections, skySamples);
                float sum = 0f;
                for (int i = 0; i < skySamples.Length; i++)
                {
                    sum += Luminance(skySamples[i]);
                }

                return sum / skySamples.Length;
            default:
                return Luminance(RenderSettings.ambientLight.linear);
        }
    }

    private static float Luminance(Color c)
    {
        return Mathf.Max(0f, 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b);
    }

    private void Take(float light, float sunPart, bool sunClear, float skyPart, bool skyOpen)
    {
        readings[readingNext] = light;
        readingNext = (readingNext + 1) % MedianCount;
        readingCount = Mathf.Min(readingCount + 1, MedianCount);
        for (int i = 0; i < readingCount; i++)
        {
            sorted[i] = readings[i];
        }

        System.Array.Sort(sorted, 0, readingCount);
        lightNow = sorted[readingCount / 2];

        float t = brightestLight > darkestLight ? Mathf.InverseLerp(darkestLight, brightestLight, lightNow) : (lightNow > darkestLight ? 1f : 0f);
        targetGlow = Mathf.Lerp(glowInDarkest, glowInBrightest, t);

        string parts = "sun " + sunPart.ToString("F2") + (sunClear ? " clear" : " blocked") + ", sky " + skyPart.ToString("F2") + (skyOpen ? " open" : " covered");
        if (firstReading)
        {
            // The first reading sets the eyes at once, so they never visibly ramp up as a scene starts.
            firstReading = false;
            glowNow = targetGlow;
            loggedGlow = targetGlow;
            lastSunClear = sunClear;
            lastSkyOpen = skyOpen;
            lastSun = RenderSettings.sun;
            if (showDebugLogs)
            {
                Debug.Log("[EyeGlow] first reading: light on her " + light.ToString("F2") + " (" + parts + ", the scene's sun '"
                    + (lastSun != null ? lastSun.name : "none") + "') -> eyes x" + targetGlow.ToString("F2"));
            }
        }
        else if (showDebugLogs)
        {
            if (RenderSettings.sun != lastSun)
            {
                lastSun = RenderSettings.sun;
                Debug.Log("[EyeGlow] the scene's sun is now '" + (lastSun != null ? lastSun.name : "none") + "'");
            }

            if (sunClear != lastSunClear || skyOpen != lastSkyOpen)
            {
                Debug.Log("[EyeGlow] " + (skyOpen ? (sunClear ? "in the open" : "in shadow, sky open") : "under cover") + ": light on her "
                    + light.ToString("F2") + " (" + parts + ")");
            }

            if (Mathf.Abs(targetGlow - loggedGlow) >= LogStep)
            {
                loggedGlow = targetGlow;
                Debug.Log("[EyeGlow] light on her " + lightNow.ToString("F2") + " (" + parts + ") -> eyes x" + targetGlow.ToString("F2"));
            }
        }

        lastSunClear = sunClear;
        lastSkyOpen = skyOpen;

        summaryLow = Mathf.Min(summaryLow, light);
        summaryHigh = Mathf.Max(summaryHigh, light);
        summarySun += sunPart;
        summarySky += skyPart;
        summarySunClear += sunClear ? 1 : 0;
        summarySkyOpen += skyOpen ? 1 : 0;
        summaryGlowLow = Mathf.Min(summaryGlowLow, targetGlow);
        summaryGlowHigh = Mathf.Max(summaryGlowHigh, targetGlow);
        summaryCount++;
        if (Time.unscaledTime >= summaryUntil)
        {
            if (showDebugLogs && summaryCount > 0)
            {
                Debug.Log("[EyeGlow] last " + SummarySeconds.ToString("0") + " s: " + summaryCount + " readings, light on her "
                    + summaryLow.ToString("F2") + " to " + summaryHigh.ToString("F2") + " (sun " + (summarySun / summaryCount).ToString("F2")
                    + " clear " + (100 * summarySunClear / summaryCount) + "%, sky " + (summarySky / summaryCount).ToString("F2")
                    + " open " + (100 * summarySkyOpen / summaryCount) + "%), eyes x" + summaryGlowLow.ToString("F2")
                    + (summaryGlowHigh - summaryGlowLow >= 0.005f ? " to x" + summaryGlowHigh.ToString("F2") : string.Empty));
            }

            summaryUntil = Time.unscaledTime + SummarySeconds;
            summaryLow = float.MaxValue;
            summaryHigh = float.MinValue;
            summarySun = 0f;
            summarySky = 0f;
            summarySunClear = 0;
            summarySkyOpen = 0;
            summaryGlowLow = float.MaxValue;
            summaryGlowHigh = float.MinValue;
            summaryCount = 0;
        }
    }

    // ---- the eyes ----

    private bool FindEyes()
    {
        if (eyeMaterial == null)
        {
            Debug.LogWarning("[EyeGlow] no eye material set (Yoru Eye Glow > Eye Material, Assets/YORU/Eye.mat): the eyes keep their own glow.");
            return false;
        }

        if (eyeRenderer == null)
        {
            foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
            {
                if (SlotOf(r) >= 0)
                {
                    eyeRenderer = r;
                    break;
                }
            }
        }

        eyeIndex = eyeRenderer != null ? SlotOf(eyeRenderer) : -1;
        if (eyeIndex < 0)
        {
            Debug.LogWarning("[EyeGlow] '" + eyeMaterial.name + "' is not on " + (eyeRenderer != null ? "'" + eyeRenderer.name + "'" : "any of her renderers")
                + ": the eyes keep their own glow.");
            return false;
        }

        if (!eyeMaterial.HasProperty(EmissionId) || !eyeMaterial.IsKeywordEnabled("_EMISSION"))
        {
            Debug.LogWarning("[EyeGlow] '" + eyeMaterial.name + "' has no Emission switched on, so there is no glow to change.");
            return false;
        }

        ownGlow = eyeMaterial.GetColor(EmissionId);
        return true;
    }

    private int SlotOf(Renderer r)
    {
        r.GetSharedMaterials(materialsBuffer);
        for (int i = 0; i < materialsBuffer.Count; i++)
        {
            if (materialsBuffer[i] == eyeMaterial)
            {
                return i;
            }
        }

        return -1;
    }

    private void ApplyEyes()
    {
        if (eyeRenderer == null)
        {
            return;
        }

        // At x1 the renderer is left exactly as it is without this component.
        if (Mathf.Abs(glowNow - 1f) < 0.001f)
        {
            ClearEyes();
            return;
        }

        block.Clear();
        block.SetColor(EmissionId, new Color(ownGlow.r * glowNow, ownGlow.g * glowNow, ownGlow.b * glowNow, ownGlow.a));
        eyeRenderer.SetPropertyBlock(block, eyeIndex);
        blockSet = true;
    }

    private void ClearEyes()
    {
        if (blockSet && eyeRenderer != null && eyeIndex >= 0)
        {
            eyeRenderer.SetPropertyBlock(null, eyeIndex);
        }

        blockSet = false;
    }
}
