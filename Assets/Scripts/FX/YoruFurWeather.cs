using UnityEngine;

/// <summary>
/// ROUND 84 (23 Sep 2026): Yoru's own fur weather. It moved off StormWeather so it travels with her
/// to every scene (START-HERE rule 12: Yoru's things live on Yoru). Step 3a ran it as a shadow next
/// to StormWeather through a whole Oni fight (rain, under rock and back, phase 2): about 12,300
/// frames, 0 differences. Step 3b made it the writer. The maths are StormWeather's rounds 80, 81
/// and 83, unchanged:
///
///   ROOF CHECK: a ray straight up from above her head, 5 times a second, blended over Cover Blend
///   Seconds. Under rock the weather wind and the rain fade out; in the open they come back.
///   MOVEMENT WIND: always on, under rock too, read from her real velocity, so the coat streams
///   behind her on every move (walk, run, jump, climb). It adds to the weather wind as a vector.
///   RAIN ON THE COAT: floor wetness x Fur Rain Scale x open sky, into the XFur Weather Manager.
///   WHOLE-COAT SOAK: floor wetness x Fur Soak All x open sky, eased over Fur Soak Seconds, into
///   the global _YoruWetAll that the fur shell shader reads.
///   The total wind goes straight to XFur's two wind globals in LateUpdate, after the Weather
///   Manager's own Update, so this wins for the frame and the rain direction the manager drives
///   stays as authored.
///
/// What it reads from the scene: the fight's weather wind level, its gust speed and the floor
/// wetness from StormWeather (the fight keeps those). A scene without StormWeather gives her the
/// calm wind below and a dry floor. XFur's fur VFX module reads its rain from the scene's XFur
/// Weather Manager and defaults to full rain when there is none, so a scene without one gets a
/// runtime manager at rain 0 here: that is what soaked the day Yoru on 23 Sep.
/// </summary>
[DefaultExecutionOrder(1)]   // after StormWeather (0) has moved its wind level this frame, before YoruFurLighting (1000)
[DisallowMultipleComponent]
public class YoruFurWeather : MonoBehaviour
{
    #region Inspector

    [Header("Rain and soak (from StormWeather, rounds 80 and 81)")]
    [Tooltip("Fur rain = floor wetness x this x open sky. Under rock it is 0 and she dries over the Rain FX Fade Time on her fur objects.")]
    [SerializeField, Range(0f, 1f)] private float furRainScale = 0.8f;

    [Tooltip("Base soak for the whole coat, so chest, legs and tail undersides get wet with the back: floor wetness x this x open sky. 0 is XFur's sky-facing only rain.")]
    [SerializeField, Range(0f, 1f)] private float furSoakAll = 0.8f;

    [Tooltip("Seconds for the base soak to build up and to dry off. Keep it equal to the Rain FX Fade Time on her fur objects (12) so the underside dries with the back.")]
    [SerializeField] private float furSoakSeconds = 12f;

    [Header("Her movement wind (from StormWeather, round 83)")]
    [Tooltip("How much wind her own movement makes, on top of the weather. Always on, under cover too. 0 is off, 0.8 reads as a run, 1.5 is a sprint through a gale.")]
    [SerializeField, Range(0f, 2f)] private float moveWindMax = 0.8f;

    [Tooltip("Speed in metres per second at which her movement wind reaches the maximum. Near her run speed: a walk moves the coat a little, a run fully.")]
    [SerializeField] private float moveWindFullSpeed = 6f;

    [Tooltip("Seconds for the movement wind to catch up when she starts, stops or turns. Small values snap, large values lag.")]
    [SerializeField] private float moveWindResponse = 0.25f;

    [Tooltip("Extra gust speed at a full run, so the coat flutters faster while she moves rather than only leaning.")]
    [SerializeField, Range(0f, 16f)] private float moveWindFrequencyBoost = 3f;

    [Header("Roof check (from StormWeather, round 80)")]
    [Tooltip("Layers that count as a roof. The cave's value: Default, Water, Items, Interactable, Ground, ExamineItem, Climbable.")]
    [SerializeField] private LayerMask skyCheckMask = (1 << 0) | (1 << 4) | (1 << 6) | (1 << 7) | (1 << 8) | (1 << 11) | (1 << 13);

    [Tooltip("How far up the roof check looks for rock, in metres.")]
    [SerializeField] private float skyCheckHeight = 30f;

    [Tooltip("Metres above her root the roof check starts. 2.6 clears her ears when she stands.")]
    [SerializeField] private float skyCheckOrigin = 2.6f;

    [Tooltip("Seconds for the wind and the rain to fade out under cover and back in the open.")]
    [SerializeField] private float coverBlendSeconds = 1f;

    [Header("A scene without StormWeather")]
    [Tooltip("Weather wind on her coat when the scene has no StormWeather: the cave's calm breeze. The shader squares it: 0.4 invisible, 0.5 ripple, 1.0 real wind.")]
    [SerializeField, Range(0f, 2f)] private float calmWind = 0.5f;

    [Tooltip("Gust speed when the scene has no StormWeather: the cave's calm gusts. 2 to 3 breathes.")]
    [SerializeField, Range(0f, 32f)] private float calmWindFrequency = 2.5f;

    [Header("Debug")]
    [Tooltip("Log lines for the Weather Manager she found or made, going under cover and back into the open, and rain starting and stopping on the coat.")]
    [SerializeField] private bool showDebugLogs = true;

    #endregion

    #region Runtime

    private static readonly int WetAllId = Shader.PropertyToID("_YoruWetAll");
    private static readonly int WindDirFreqId = Shader.PropertyToID("_XFurWindDirectionFreq");
    private static readonly int WindStrengthId = Shader.PropertyToID("_XFurWindStrength");

    private const float SkyCheckInterval = 0.2f;        // 5 times a second
    private const float MaxTrustedSpeedSqr = 2500f;     // above 50 m/s it is a teleport or a respawn, not movement

    /// <summary>What one computation remembers between frames, and what it produced this frame.</summary>
    private sealed class FurState
    {
        public float openSky = 1f;          // 1 under the sky, 0 under rock, blended
        public float openSkyTarget = 1f;
        public float nextSkyCheck;
        public bool covered;
        public Vector3 lastPos;
        public bool lastPosValid;
        public Vector3 moveVelocity;        // smoothed, metres per second
        public float soak;                  // whole-coat soak 0..1

        public Vector4 windDirFreq;         // for the global _XFurWindDirectionFreq
        public float windStrength;          // for the global _XFurWindStrength
        public float zoneWind;              // for the Weather Manager's Wind Strength
        public float zoneFrequency;         // for the Weather Manager's Wind Frequency
        public float rain;                  // for the Weather Manager's Rain Intensity
    }

    private StormWeather storm;
    private XFurStudio.Utilities.XFurWeatherManager furWeather;
    private Vector3 weatherWindDir = Vector3.forward;    // the Weather Zone's authored heading, read once
    private readonly FurState state = new FurState();
    private bool wasCovered;
    private bool rainWasOn;

    #endregion

    #region Lifecycle

    private void Start()
    {
        storm = Object.FindFirstObjectByType<StormWeather>();
        furWeather = Object.FindFirstObjectByType<XFurStudio.Utilities.XFurWeatherManager>();

        if (furWeather == null)
        {
            // Safety net. The fur VFX module reads its rain amount from the manager; with no manager the
            // shader default is full rain and the coat soaks in seconds. A runtime manager at rain 0
            // under her makes that impossible in any scene.
            GameObject go = new GameObject("XFur Weather (runtime fallback)");
            go.transform.SetParent(transform, false);
            furWeather = go.AddComponent<XFurStudio.Utilities.XFurWeatherManager>();
            if (showDebugLogs)
                Debug.LogWarning("[YoruFurWeather] no XFur Weather Manager in this scene, made a runtime one at rain 0 so her fur cannot soak by default.");
        }
        XFurStudio.Core.XFurStudioInstance.WeatherManager = furWeather;

        // The heading the Weather Zone was authored with, read once. Nothing about that object is
        // changed, rotation included, so the rain and snow directions it computes stay as set.
        weatherWindDir = furWeather.transform.forward;
        if (weatherWindDir.sqrMagnitude < 0.0001f) weatherWindDir = Vector3.forward;
        weatherWindDir.Normalize();

        furWeather.RainIntensity = 0f;
        furWeather.SnowIntensity = 0f;
        furWeather.WindStrength = WeatherWind();
        furWeather.WindFrequency = WeatherFrequency();
        if (showDebugLogs)
            Debug.Log($"[YoruFurWeather] fur weather ready on '{furWeather.name}'. Rain 0, wind {WeatherWind():F2} in the open, roof check {skyCheckHeight:F0} m above her. "
                      + (storm != null ? $"Wind level and floor from StormWeather on '{storm.name}'." : "No StormWeather in this scene: calm wind and a dry floor."));
    }

    private void LateUpdate()
    {
        Compute(state, transform.position, WeatherWind(), WeatherFrequency(), FloorWetness());
        Write(state);
    }

    private void OnDisable()
    {
        // Leave the manager dry and calm and the coat dry while this is not running (the rule
        // StormWeather's OnDisable kept for rounds 80, 81 and 83).
        if (furWeather != null)
        {
            furWeather.RainIntensity = 0f;
            furWeather.WindStrength = 0f;
        }
        state.soak = 0f;
        Shader.SetGlobalFloat(WetAllId, 0f);
        Shader.SetGlobalFloat(WindStrengthId, 0f);   // stop driving the wind, the manager takes it back
    }

    #endregion

    #region Scene weather

    /// <summary>The fight's weather wind level from StormWeather, or her calm wind without one.</summary>
    private float WeatherWind()
    {
        return storm != null ? storm.FurWeatherWind : calmWind;
    }

    /// <summary>The fight's gust speed from StormWeather, or her calm gusts without one.</summary>
    private float WeatherFrequency()
    {
        return storm != null ? storm.FurWeatherWindFrequency : calmWindFrequency;
    }

    /// <summary>The floor wetness from StormWeather, or a dry floor without one.</summary>
    private float FloorWetness()
    {
        return storm != null ? storm.FloorWetness : 0f;
    }

    #endregion

    #region Fur weather

    /// <summary>
    /// One frame of her fur weather, the same maths as StormWeather.UpdateFurWeather (rounds 80, 81
    /// and 83), in the same order. Fills the state's outputs and writes nothing.
    /// </summary>
    private void Compute(FurState s, Vector3 position, float weatherWind, float weatherFreq, float wetness)
    {
        // Roof check at 5 Hz, blended every frame.
        if (Time.time >= s.nextSkyCheck)
        {
            s.nextSkyCheck = Time.time + SkyCheckInterval;
            Vector3 origin = position + Vector3.up * skyCheckOrigin;
            bool covered = Physics.Raycast(origin, Vector3.up, skyCheckHeight, skyCheckMask, QueryTriggerInteraction.Ignore);
            s.openSkyTarget = covered ? 0f : 1f;
            s.covered = covered;
        }
        float blend = coverBlendSeconds <= 0f ? 1f : Time.deltaTime / coverBlendSeconds;
        s.openSky = Mathf.MoveTowards(s.openSky, s.openSkyTarget, blend);

        // Her velocity straight from her position, so nothing here depends on PlayerMovement.
        Vector3 rawVelocity = Vector3.zero;
        if (s.lastPosValid && Time.deltaTime > 0.0001f)
            rawVelocity = (position - s.lastPos) / Time.deltaTime;
        if (rawVelocity.sqrMagnitude > MaxTrustedSpeedSqr) rawVelocity = Vector3.zero;
        s.lastPos = position;
        s.lastPosValid = true;
        float moveBlend = moveWindResponse <= 0f ? 1f : Mathf.Clamp01(Time.deltaTime / moveWindResponse);
        s.moveVelocity = Vector3.Lerp(s.moveVelocity, rawVelocity, moveBlend);

        float speed = s.moveVelocity.magnitude;
        float move01 = moveWindFullSpeed <= 0f ? 0f : Mathf.Clamp01(speed / moveWindFullSpeed);
        Vector3 moveWind = speed > 0.05f ? -(s.moveVelocity / speed) * (moveWindMax * move01) : Vector3.zero;

        // The weather wind and her movement wind add as vectors, the way two real airflows would.
        Vector3 totalWind = weatherWindDir * (weatherWind * s.openSky) + moveWind;
        float totalStrength = totalWind.magnitude;
        Vector3 totalDir = totalStrength > 0.0001f ? totalWind / totalStrength : weatherWindDir;
        totalStrength = Mathf.Clamp(totalStrength, 0f, 2f);
        float totalFreq = Mathf.Clamp(weatherFreq + moveWindFrequencyBoost * move01, 0f, 32f);

        s.windDirFreq = new Vector4(totalDir.x, totalDir.y, totalDir.z, totalFreq);
        s.windStrength = totalStrength;
        s.zoneWind = weatherWind * s.openSky;
        s.zoneFrequency = weatherFreq;

        // Rain follows the floor, gated by the same roof check, so under rock she dries out.
        s.rain = Mathf.Clamp01(wetness * furRainScale * s.openSky);

        // Whole-coat soak: slower than the rain, and it dries at the same pace as the Rain FX fade.
        float soakTarget = Mathf.Clamp01(wetness * furSoakAll * s.openSky);
        float soakStep = furSoakSeconds <= 0f ? 1f : Time.deltaTime / furSoakSeconds;
        s.soak = Mathf.MoveTowards(s.soak, soakTarget, soakStep);
    }

    /// <summary>
    /// Writes one frame of her fur weather where XFur reads it, the same writes StormWeather made,
    /// and logs going under cover and back, and rain starting and stopping on the coat.
    /// </summary>
    private void Write(FurState s)
    {
        if (s.covered != wasCovered)
        {
            wasCovered = s.covered;
            if (showDebugLogs) Debug.Log(s.covered ? "[YoruFurWeather] under cover, wind fading out." : "[YoruFurWeather] open sky, wind back.");
        }

        // The Weather Manager writes these same two globals in its own Update. This runs in
        // LateUpdate, so this wins for the frame, and the manager's Wind Strength below still drives
        // its rain and snow directions from the authored heading only.
        Shader.SetGlobalVector(WindDirFreqId, s.windDirFreq);
        Shader.SetGlobalFloat(WindStrengthId, s.windStrength);

        furWeather.WindStrength = s.zoneWind;
        furWeather.WindFrequency = s.zoneFrequency;
        furWeather.RainIntensity = s.rain;

        bool rainOn = s.rain > 0.01f;
        if (rainOn != rainWasOn)
        {
            rainWasOn = rainOn;
            if (showDebugLogs)
                Debug.Log(rainOn ? $"[YoruFurWeather] rain reaching the coat (intensity {s.rain:F2}, follows floor wetness)." : "[YoruFurWeather] rain off the coat, drying.");
        }

        Shader.SetGlobalFloat(WetAllId, s.soak);
    }

    #endregion
}
