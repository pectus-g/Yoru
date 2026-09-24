using UnityEngine;

/// <summary>
/// ROUND 79 - camera-relative light rig for Yoru: a rim light that always sits behind her
/// silhouette as seen from the camera, and a soft fill light on the camera side.
///
/// WHY THIS EXISTS. Round 78 gave the fur its sheen and its warm backlight, but aimed them
/// with a fixed world direction (the FurKey object), so the glow was only right from one
/// camera angle and landed on her undersides from every other one. YoruRim sat straight
/// above her head. With deferred rendering (this session) YoruRim finally reaches the fur,
/// and it read as a lamp glued to her head: a white cap on top, face and front in pure
/// ambient, and a light direction that did not agree with the moon.
///
/// Character lighting in games is camera-relative: the rim lives behind the character as
/// seen from the camera, the fill lives on the camera side. This does exactly that, once per
/// frame right before the game camera culls, so it is always in sync with Cinemachine.
///
/// The rim light is also the Key Light of YoruFurLighting. This rig turns it to look at her,
/// so the strand sheen and the warm backlight follow the camera too. FurKey is deleted.
///
/// The fill light is created at runtime and is never saved into the scene.
/// Intensity, colour and range of the rim stay on the YoruRim Light component: tune them
/// there. The fill is tuned here because it does not exist outside Play.
///
/// ROUND 86 (23 Sep 2026, step 4): the phase 2 storm boost lives here now, moved from
/// StormWeather (round 80) with the same values and the same maths. A fight's weather only
/// says storm on and off through SetStorm; Storm Fill Boost and Storm Rim Multiplier are hers.
/// </summary>
[DisallowMultipleComponent]
public class YoruLightRig : MonoBehaviour
{
    #region Inspector

    [Header("=== TARGETS ===")]
    [Tooltip("Camera the rig works against. Empty = Camera.main. In this scene that is mainCamera (1), tagged MainCamera.")]
    [SerializeField] private Camera targetCamera;

    [Tooltip("Point on Yoru the lights aim at and orbit around, measured up from her root. 1.2 is her chest when standing on two legs.")]
    [SerializeField] private float targetHeight = 1.2f;

    [Header("=== RIM, behind her from the camera's view ===")]
    [Tooltip("The existing YoruRim point light. Leave empty and the rig finds a child Light named YoruRim.")]
    [SerializeField] private Light rimLight;

    [Tooltip("How far behind her (away from the camera) the rim sits, in metres.")]
    [SerializeField] private float rimBehind = 2.5f;

    [Tooltip("How high above the aim point the rim sits, in metres.")]
    [SerializeField] private float rimHeight = 2.5f;

    [Tooltip("Sideways offset as seen from the camera. 0 is dead behind her, positive is the camera's right. A little offset gives a three-quarter rim instead of a halo.")]
    [SerializeField] private float rimSide = 0.8f;

    [Header("=== FILL, camera side ===")]
    [Tooltip("Off = no fill, her front is ambient and probes only, as before this round.")]
    [SerializeField] private bool fillEnabled = true;

    [SerializeField] private Color fillColor = new Color(0.62f, 0.68f, 0.90f, 1f);

    [Tooltip("Keep it low. The fill is there so her face and front are never pure ambient, not to light her.")]
    [Range(0f, 3f)]
    [SerializeField] private float fillIntensity = 0.35f;

    [SerializeField] private float fillRange = 7f;

    [Tooltip("How far from the aim point toward the camera the fill sits, in metres.")]
    [SerializeField] private float fillTowardCamera = 2.5f;

    [SerializeField] private float fillHeight = 1.6f;

    [Tooltip("Sideways offset as seen from the camera. Opposite sign to the rim gives the classic key and rim split.")]
    [SerializeField] private float fillSide = -1.0f;

    [Header("=== WHAT THE RIG LIGHTS ===")]
    [Tooltip("On: rim and fill only light the layers below, so they never paint a light pool on the floor around her. Her body is on Player and the tails were moved to Player in this round.")]
    [SerializeField] private bool characterOnly = true;

    [SerializeField] private LayerMask characterLayers = 1 << 3;

    [Header("=== STORM, a fight's weather turns it on and off ===")]
    [Tooltip("ROUND 86 - phase 2 must not be darker. Added to the fill intensity while a fight's storm is on (StormWeather says when). Moved here from StormWeather.")]
    [Range(0f, 1f)]
    [SerializeField] private float stormFillBoost = 0.25f;

    [Tooltip("ROUND 86 - YoruRim intensity multiplier while a fight's storm is on. Her fur sheen reads YoruRim, so it brightens with it. Moved here from StormWeather.")]
    [Range(1f, 2f)]
    [SerializeField] private float stormRimMultiplier = 1.3f;

    [Header("=== DEBUG ===")]
    [Tooltip("Logs the storm boost going on and off.")]
    [SerializeField] private bool showDebugLogs = true;

    #endregion

    #region State

    private Light fill;
    private Camera cachedCamera;

    // ROUND 86 - the values the storm boost starts from and returns to, read at Start
    // (StormWeather read them at the same moment before step 4).
    private float stormBaseFill;
    private float stormBaseRim = -1f;

    #endregion

    #region Unity

    private void OnEnable()
    {
        if (rimLight == null)
        {
            rimLight = FindRim();
        }

        ApplyMasks();
        Camera.onPreCull += OnCameraPreCull;
    }

    private void Start()
    {
        // ROUND 86 - the storm boost works from the values she starts with.
        stormBaseFill = fillIntensity;
        if (rimLight != null)
        {
            stormBaseRim = rimLight.intensity;
        }
    }

    private void OnDisable()
    {
        Camera.onPreCull -= OnCameraPreCull;
        DestroyFill();
    }

    private void OnValidate()
    {
        if (Application.isPlaying)
        {
            ApplyMasks();
        }
    }

    #endregion

    #region Placement

    private void OnCameraPreCull(Camera cam)
    {
        if (cam != Target)
        {
            return;
        }

        Place(cam);
    }

    private Camera Target
    {
        get
        {
            if (targetCamera != null)
            {
                return targetCamera;
            }

            if (cachedCamera == null)
            {
                cachedCamera = Camera.main;
            }

            return cachedCamera;
        }
    }

    private void Place(Camera cam)
    {
        Vector3 aim = transform.position + Vector3.up * targetHeight;

        // Flattened camera forward: "away from the camera" on the ground plane.
        // Looking straight down makes it degenerate, then her own facing is used instead.
        Vector3 away = cam.transform.forward;
        away.y = 0f;
        if (away.sqrMagnitude < 0.0001f)
        {
            away = transform.forward;
            away.y = 0f;
            if (away.sqrMagnitude < 0.0001f)
            {
                away = Vector3.forward;
            }
        }
        away.Normalize();

        // Left-handed: up cross forward is the camera's right.
        Vector3 right = Vector3.Cross(Vector3.up, away);

        if (rimLight != null)
        {
            Vector3 rimPos = aim + away * rimBehind + Vector3.up * rimHeight + right * rimSide;
            rimLight.transform.position = rimPos;
            rimLight.transform.rotation = Quaternion.LookRotation(aim - rimPos, Vector3.up);
        }

        if (fillEnabled)
        {
            EnsureFill();
            Vector3 fillPos = aim - away * fillTowardCamera + Vector3.up * fillHeight + right * fillSide;
            fill.transform.position = fillPos;
            fill.transform.rotation = Quaternion.LookRotation(aim - fillPos, Vector3.up);
            fill.color = fillColor;
            fill.intensity = fillIntensity;
            fill.range = fillRange;
        }
        else
        {
            DestroyFill();
        }
    }

    #endregion

    #region Lights

    private Light FindRim()
    {
        Light[] lights = GetComponentsInChildren<Light>(true);
        for (int i = 0; i < lights.Length; i++)
        {
            if (lights[i].gameObject.name == "YoruRim")
            {
                return lights[i];
            }
        }

        return null;
    }

    private void EnsureFill()
    {
        if (fill != null)
        {
            return;
        }

        GameObject go = new GameObject("YoruFill (runtime)");
        go.hideFlags = HideFlags.DontSave;
        go.transform.SetParent(transform, false);

        fill = go.AddComponent<Light>();
        fill.type = LightType.Point;
        fill.shadows = LightShadows.None;
        fill.renderMode = LightRenderMode.ForcePixel;
        fill.lightmapBakeType = LightmapBakeType.Realtime;

        ApplyMasks();
    }

    private void DestroyFill()
    {
        if (fill == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(fill.gameObject);
        }
        else
        {
            DestroyImmediate(fill.gameObject);
        }

        fill = null;
    }

    private void ApplyMasks()
    {
        int mask = characterOnly ? characterLayers.value : -1;

        if (rimLight != null)
        {
            rimLight.cullingMask = mask;
        }

        if (fill != null)
        {
            fill.cullingMask = mask;
        }
    }

    #endregion

    #region Public API

    /// <summary>
    /// For the fight phases. 0 turns the fill off visually without destroying it.
    /// </summary>
    public void SetFillIntensity(float value)
    {
        fillIntensity = Mathf.Clamp(value, 0f, 3f);
    }

    /// <summary>
    /// For the fight phases. Moves the rim around her as seen from the camera, positive is the camera's right.
    /// </summary>
    public void SetRimSide(float value)
    {
        rimSide = value;
    }

    /// <summary>
    /// The fill intensity in use now: her base, or base plus Storm Fill Boost while a storm is on.
    /// </summary>
    public float FillIntensity
    {
        get { return fillIntensity; }
    }

    /// <summary>
    /// The rim light the rig drives (YoruRim).
    /// </summary>
    public Light RimLight
    {
        get { return rimLight; }
    }

    /// <summary>
    /// ROUND 86 - a fight's storm, on and off. StormWeather calls it at phase 2 and when the fight
    /// ends. On: the fill gets Storm Fill Boost on top of her base and YoruRim is multiplied by
    /// Storm Rim Multiplier. Off: both go back to the base. The maths StormWeather did in round 80.
    /// </summary>
    public void SetStorm(bool on)
    {
        SetFillIntensity(on ? stormBaseFill + stormFillBoost : stormBaseFill);

        bool hasRim = rimLight != null && stormBaseRim >= 0f;
        if (hasRim)
        {
            rimLight.intensity = on ? stormBaseRim * stormRimMultiplier : stormBaseRim;
        }

        if (!showDebugLogs)
        {
            return;
        }

        if (on)
        {
            string rimText = hasRim ? $"YoruRim {stormBaseRim:F2} -> {rimLight.intensity:F2}" : "no YoruRim";
            Debug.Log($"[YoruLightRig] lights boosted for the storm: fill {fillIntensity:F2}, rim x{stormRimMultiplier:F2} ({rimText}).");
        }
        else
        {
            string rimText = hasRim ? $"YoruRim {rimLight.intensity:F2}" : "no YoruRim";
            Debug.Log($"[YoruLightRig] lights back to base: fill {fillIntensity:F2}, {rimText}.");
        }
    }

    #endregion
}
