using UnityEngine;

/// <summary>
/// ROUND 84 (23 Sep 2026): Yoru's own fur weather, moving off StormWeather so it travels with her
/// to every scene (START-HERE rule 12: Yoru's things live on Yoru). Same maths as StormWeather's
/// UpdateFurWeather (rounds 80, 81 and 83):
///
///   ROOF CHECK: a ray straight up from above her head, 5 times a second, blended over Cover Blend
///   Seconds. Under rock the weather wind and the rain fade out; in the open they come back.
///   MOVEMENT WIND: always on, under rock too, read from her real velocity, so the coat streams
///   behind her on every move (walk, run, jump, climb). It adds to the weather wind as a vector.
///   RAIN ON THE COAT: floor wetness x Fur Rain Scale x open sky, for the XFur Weather Manager.
///   WHOLE-COAT SOAK: floor wetness x Fur Soak All x open sky, eased over Fur Soak Seconds, for the
///   global _YoruWetAll that the fur shell shader reads.
///
/// The fight keeps what belongs to the fight: its weather wind level, its gust speed and the floor
/// wetness stay on StormWeather, and this reads them from there.
///
/// STEP 3a, SHADOW. This version WRITES NOTHING. Every frame, after StormWeather has written the
/// fur weather, it runs the same computation twice and compares both with what StormWeather wrote:
///   EXACT COPY: fed the position StormWeather read this frame. It must match within 0.001 on every
///   frame; that is the proof that the maths moved over unchanged.
///   HER OWN: fed her own position at the end of her frame, the way step 3b will read it. Any
///   difference here comes only from the moment in the frame the position is read (her attack
///   position lock can move her in between), measured so step 3b is decided on real numbers.
/// Logs the first differences, a summary every 10 seconds and a total when the scene ends.
/// </summary>
[DefaultExecutionOrder(1)]   // after StormWeather (0) has written this frame, before YoruFurLighting (1000)
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

    [Header("Debug")]
    [Tooltip("Shadow log lines: the first differences, a summary every 10 seconds and a total when the scene ends.")]
    [SerializeField] private bool showDebugLogs = true;

    #endregion

    #region Runtime

    private static readonly int WetAllId = Shader.PropertyToID("_YoruWetAll");
    private static readonly int WindDirFreqId = Shader.PropertyToID("_XFurWindDirectionFreq");
    private static readonly int WindStrengthId = Shader.PropertyToID("_XFurWindStrength");

    private const float SkyCheckInterval = 0.2f;        // 5 times a second
    private const float MaxTrustedSpeedSqr = 2500f;     // above 50 m/s it is a teleport or a respawn, not movement
    private const float ShadowTolerance = 0.001f;
    private const float ShadowSummarySeconds = 10f;
    private const int ShadowDetailLines = 10;

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

    /// <summary>Differences counted for one shadow report.</summary>
    private sealed class ShadowStats
    {
        public int frames;
        public int copyFrames;
        public float copyLargest;
        public string copyWhat = "nothing";
        public int ownFrames;
        public float ownLargest;
        public string ownWhat = "nothing";
        public float positionLargest;

        public void Add(float copyDiff, string copyName, float ownDiff, string ownName, float positionDiff)
        {
            frames++;
            if (copyDiff > ShadowTolerance) copyFrames++;
            if (copyDiff > copyLargest) { copyLargest = copyDiff; copyWhat = copyName; }
            if (ownDiff > ShadowTolerance) ownFrames++;
            if (ownDiff > ownLargest) { ownLargest = ownDiff; ownWhat = ownName; }
            if (positionDiff > positionLargest) positionLargest = positionDiff;
        }

        public void Reset()
        {
            frames = 0;
            copyFrames = 0;
            copyLargest = 0f;
            copyWhat = "nothing";
            ownFrames = 0;
            ownLargest = 0f;
            ownWhat = "nothing";
            positionLargest = 0f;
        }
    }

    private StormWeather storm;
    private XFurStudio.Utilities.XFurWeatherManager furWeather;
    private Vector3 weatherWindDir = Vector3.forward;    // the Weather Zone's authored heading, read once
    private readonly FurState copy = new FurState();     // fed the position StormWeather read
    private readonly FurState own = new FurState();      // fed her own end-of-frame position
    private readonly ShadowStats window = new ShadowStats();
    private readonly ShadowStats session = new ShadowStats();
    private bool shadowReady;
    private int detailLinesLogged;
    private float nextSummaryRealTime;

    #endregion

    #region Lifecycle

    private void Start()
    {
        storm = Object.FindFirstObjectByType<StormWeather>();
        furWeather = Object.FindFirstObjectByType<XFurStudio.Utilities.XFurWeatherManager>();

        if (storm == null || furWeather == null)
        {
            if (showDebugLogs)
                Debug.Log("[YoruFurWeather] SHADOW idle: " + (storm == null ? "no StormWeather" : "no XFur Weather Manager")
                          + " in this scene, nothing to compare with. Writes nothing.");
            return;
        }

        // The same heading StormWeather reads once from the Weather Zone.
        weatherWindDir = furWeather.transform.forward;
        if (weatherWindDir.sqrMagnitude < 0.0001f) weatherWindDir = Vector3.forward;
        weatherWindDir.Normalize();

        shadowReady = true;
        nextSummaryRealTime = Time.unscaledTime + ShadowSummarySeconds;
        if (showDebugLogs)
            Debug.Log($"[YoruFurWeather] SHADOW ON (step 3a): computing her fur wind, rain and soak next to StormWeather on '{storm.name}' "
                      + $"with the Weather Manager on '{furWeather.name}'. Writes nothing. Tolerance {ShadowTolerance}, summary every {ShadowSummarySeconds:F0} s.");
    }

    private void LateUpdate()
    {
        if (!shadowReady) return;

        float weatherWind = storm.FurWeatherWind;
        float weatherFreq = storm.FurWeatherWindFrequency;
        float wetness = storm.FloorWetness;
        Vector3 stormSample = storm.ShadowFurSamplePosition;
        Vector3 ownSample = transform.position;

        Compute(copy, stormSample, weatherWind, weatherFreq, wetness);
        Compute(own, ownSample, weatherWind, weatherFreq, wetness);
        CompareWithStorm(Vector3.Distance(stormSample, ownSample));
    }

    private void OnDisable()
    {
        if (!shadowReady || !showDebugLogs) return;
        Debug.Log(Summary("SHADOW TOTAL for this session", session));
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

    #endregion

    #region Shadow

    /// <summary>Compares both computations with what StormWeather wrote this frame and logs the result.</summary>
    private void CompareWithStorm(float positionDiff)
    {
        Vector4 stormDirFreq = Shader.GetGlobalVector(WindDirFreqId);
        float stormStrength = Shader.GetGlobalFloat(WindStrengthId);
        float stormSoak = Shader.GetGlobalFloat(WetAllId);

        float copyDiff = LargestDifference(copy, stormDirFreq, stormStrength, stormSoak, out string copyWhat);
        float ownDiff = LargestDifference(own, stormDirFreq, stormStrength, stormSoak, out string ownWhat);
        window.Add(copyDiff, copyWhat, ownDiff, ownWhat, positionDiff);
        session.Add(copyDiff, copyWhat, ownDiff, ownWhat, positionDiff);

        if (showDebugLogs && copyDiff > ShadowTolerance && detailLinesLogged < ShadowDetailLines)
        {
            detailLinesLogged++;
            Debug.LogWarning($"[YoruFurWeather] SHADOW DIFF {detailLinesLogged}/{ShadowDetailLines} (exact copy): largest on {copyWhat} by {copyDiff:F6}. "
                + $"StormWeather wrote wind {stormStrength:F4} dir ({stormDirFreq.x:F3}, {stormDirFreq.y:F3}, {stormDirFreq.z:F3}) gust {stormDirFreq.w:F3}, "
                + $"zone wind {furWeather.WindStrength:F4} gust {furWeather.WindFrequency:F3}, rain {furWeather.RainIntensity:F4}, soak {stormSoak:F4}. "
                + $"Copy has wind {copy.windStrength:F4} dir ({copy.windDirFreq.x:F3}, {copy.windDirFreq.y:F3}, {copy.windDirFreq.z:F3}) gust {copy.windDirFreq.w:F3}, "
                + $"zone wind {copy.zoneWind:F4} gust {copy.zoneFrequency:F3}, rain {copy.rain:F4}, soak {copy.soak:F4}.");
        }

        if (Time.unscaledTime >= nextSummaryRealTime)
        {
            nextSummaryRealTime = Time.unscaledTime + ShadowSummarySeconds;
            if (showDebugLogs) Debug.Log(Summary("SHADOW last 10 s", window));
            window.Reset();
        }
    }

    /// <summary>Largest absolute difference between one computation and what StormWeather wrote.</summary>
    private float LargestDifference(FurState s, Vector4 stormDirFreq, float stormStrength, float stormSoak, out string what)
    {
        float largest = 0f;
        what = "nothing";
        Keep(ref largest, ref what, Mathf.Abs(s.windDirFreq.x - stormDirFreq.x), "wind direction x");
        Keep(ref largest, ref what, Mathf.Abs(s.windDirFreq.y - stormDirFreq.y), "wind direction y");
        Keep(ref largest, ref what, Mathf.Abs(s.windDirFreq.z - stormDirFreq.z), "wind direction z");
        Keep(ref largest, ref what, Mathf.Abs(s.windDirFreq.w - stormDirFreq.w), "gust speed");
        Keep(ref largest, ref what, Mathf.Abs(s.windStrength - stormStrength), "wind strength");
        Keep(ref largest, ref what, Mathf.Abs(s.zoneWind - furWeather.WindStrength), "Weather Zone wind");
        Keep(ref largest, ref what, Mathf.Abs(s.zoneFrequency - furWeather.WindFrequency), "Weather Zone gust speed");
        Keep(ref largest, ref what, Mathf.Abs(s.rain - furWeather.RainIntensity), "rain");
        Keep(ref largest, ref what, Mathf.Abs(s.soak - stormSoak), "soak");
        return largest;
    }

    private static void Keep(ref float largest, ref string what, float difference, string name)
    {
        if (difference > largest)
        {
            largest = difference;
            what = name;
        }
    }

    private static string Summary(string title, ShadowStats s)
    {
        return $"[YoruFurWeather] {title}: {s.frames} frames. Exact copy: {s.copyFrames} frames differ "
             + $"(largest {s.copyLargest:F6} on {s.copyWhat}). Her own position: {s.ownFrames} frames differ "
             + $"(largest {s.ownLargest:F4} on {s.ownWhat}; she was up to {s.positionLargest:F3} m from where StormWeather read her).";
    }

    #endregion
}
