using UnityEngine;

/// <summary>
/// ROUND 76 - fire light for a torch, brazier or lantern. Put this on the Point Light
/// inside the prefab, not on the root. One component per light.
///
/// WHY IT IS BUILT THIS WAY. Two earlier tries missed:
///
///   Round 71 wobbled the brightness with one smooth Perlin wave. One smooth wave reads
///   as a lamp on a dimmer, because real firelight is never a single frequency.
///
///   Round 75 read the live particles and copied their total brightness. That was a dead
///   end and the numbers say why: PF_FireSmall's "Fire Glow" spawns 20 particles a second
///   with a 0.5 s life, so about ten are alive at once, and its size curve only runs from
///   0.74 to 1.0. Ten particles at nearly constant size average each other out, so the
///   fire's TOTAL brightness moves about ten percent, smoothly. What makes that fire look
///   alive is the sprites sliding over each other and the noise module pushing them
///   around. That is spatial, not brightness. There is nothing there for a light to copy.
///
/// So the flicker is authored, in three layers, which is how firelight actually behaves:
///
///   1. JITTER. Fast, small. The surface chop of the flame. On its own it reads as a
///      faulty bulb, so it stays shallow.
///   2. SWELL. Slow, wider. The flame leaning and recovering. This carries the mood.
///   3. GULP. The part both earlier versions were missing, and the part that sells it.
///      Every second or two the fire dips hard, catches, and flares slightly past its
///      resting brightness before settling. That is fire pulling in air. Without it no
///      amount of noise reads as a flame.
///
/// The colour rides the brightness: dim goes toward ember red, bright goes toward yellow
/// white. A light that only changes brightness reads as a bulb no matter how good the
/// curve is.
///
/// Costs nothing per frame: no allocations, no GetComponent, no Find. The distance check
/// runs on a timer, not every frame, and switches the Light off past Cull Distance so it
/// gives its slot back to the fight (Built-in RP only draws Pixel Light Count real lights
/// per object).
///
/// RUNTIME ONLY, on purpose. Press Play to see it. It does not animate in the Scene view,
/// so it can never write a flicker value back into your prefab.
/// </summary>
[RequireComponent(typeof(Light))]
[DisallowMultipleComponent]
public class TorchLight : MonoBehaviour
{
    #region Inspector

    [Header("1. Jitter - fast surface chop")]
    [Tooltip("Depth of the fast wobble, as a fraction of the light's authored Intensity. Keep it small, this layer is texture not drama. 0.08 to 0.14.")]
    [Range(0f, 0.5f)]
    [SerializeField] private float jitterAmount = 0.10f;
    [Tooltip("Speed of the fast wobble. 9 to 14 for an open flame, 4 to 6 for a flame behind paper.")]
    [Range(0.5f, 25f)]
    [SerializeField] private float jitterSpeed = 11f;

    [Header("2. Swell - slow lean and recover")]
    [Tooltip("Depth of the slow wave. This is the layer you feel rather than see. 0.12 to 0.22.")]
    [Range(0f, 0.6f)]
    [SerializeField] private float swellAmount = 0.16f;
    [Tooltip("Speed of the slow wave. Around 1 to 2.")]
    [Range(0.05f, 6f)]
    [SerializeField] private float swellSpeed = 1.4f;

    [Header("3. Gulp - the dip and flare that makes it fire")]
    [Tooltip("How deep the dip goes. 0.3 means it drops to about 70 percent before catching again. Turn this to 0 and it stops reading as fire.")]
    [Range(0f, 0.8f)]
    [SerializeField] private float gulpDepth = 0.30f;
    [Tooltip("How far past normal it flares when it catches, as a fraction of the dip. 0.2 to 0.3.")]
    [Range(0f, 1f)]
    [SerializeField] private float gulpFlare = 0.22f;
    [Tooltip("Seconds a single dip and recovery takes.")]
    [Range(0.1f, 2f)]
    [SerializeField] private float gulpDuration = 0.45f;
    [Tooltip("Shortest and longest gap between dips, in seconds. Randomised each time so it never gets a rhythm.")]
    [SerializeField] private Vector2 gulpInterval = new Vector2(0.7f, 2.6f);

    [Header("Colour - dim goes red, bright goes yellow")]
    [Tooltip("How far the colour travels. 0 keeps the colour you authored on the Light at all times. 0.5 to 0.8 reads as fire without leaving your palette.")]
    [Range(0f, 1f)]
    [SerializeField] private float colorShift = 0.6f;

    [Header("Limits")]
    [Tooltip("Darkest the light may go, as a fraction of its authored Intensity.")]
    [Range(0f, 1f)]
    [SerializeField] private float minBrightness = 0.45f;
    [Tooltip("Brightest the light may go, as a fraction of its authored Intensity.")]
    [Range(1f, 4f)]
    [SerializeField] private float maxBrightness = 1.45f;

    [Header("Sway - the light moves, not just dims")]
    [Tooltip("Metres the light drifts around its resting spot. Small. 0.02 to 0.06. Set 0 to switch sway off. Moving the light is what makes the shadows on nearby stone breathe.")]
    [Range(0f, 0.5f)]
    [SerializeField] private float swayRadius = 0.03f;
    [Range(0.1f, 10f)]
    [SerializeField] private float swaySpeed = 2.2f;

    [Header("Distance cut - this is the performance part")]
    [Tooltip("Switch the Light off when the camera is further away than Cull Distance. The fire particles keep playing, only the light stops.")]
    [SerializeField] private bool cullByDistance = true;
    [Tooltip("Metres. Past this the Light component is off. Keep it a bit bigger than the light's Range so you never see it happen.")]
    [SerializeField] private float cullDistance = 35f;
    [Tooltip("Metres of fade before the cut, so the light dims out instead of blinking off.")]
    [SerializeField] private float fadeDistance = 10f;
    [Tooltip("Seconds between distance checks. 0.15 is invisible and costs nothing.")]
    [Range(0.02f, 1f)]
    [SerializeField] private float checkInterval = 0.15f;

    [Header("Slow motion")]
    [Tooltip("OFF (default) means the fire slows down with the world during the phase-2 cinematic, which matches the fire particles. ON means it keeps flickering at normal speed through the slow motion.")]
    [SerializeField] private bool ignoreSlowMotion = false;

    #endregion

    #region State

    private Light lightRef;
    private Transform tf;
    private Camera cam;

    private float baseIntensity;
    private Color baseColor;
    private Color emberColor;   // where the colour goes when it dips
    private Color flareColor;   // where the colour goes when it peaks
    private Vector3 basePos;

    private float seed;         // per instance, so two fires never move in step
    private float clock;

    private float gulpWait;     // seconds until the next dip starts
    private float gulpPhase;    // 0 to 1 while a dip is running, negative when idle

    private float checkTimer;
    private float camRetryTimer;

    private float distanceScale = 1f;   // 1 near, 0 past the cull line
    private bool applied;               // true while we are holding modified values

    #endregion

    #region Unity

    private void Awake()
    {
        tf = transform;
        lightRef = GetComponent<Light>();

        baseIntensity = lightRef.intensity;
        baseColor = lightRef.color;
        basePos = tf.localPosition;

        // Ember and flare are derived from the colour SHE authored, so the light never
        // leaves her palette. Ember pulls toward deep orange red, flare toward yellow white.
        emberColor = Color.Lerp(baseColor, new Color(0.85f, 0.20f, 0.04f), 0.65f);
        flareColor = Color.Lerp(baseColor, new Color(1f, 0.93f, 0.72f), 0.55f);

        // A stable per instance offset. Position based, so two lanterns side by side differ
        // and a reload does not reshuffle them.
        seed = (Mathf.Abs(tf.position.x) * 3.17f + Mathf.Abs(tf.position.z) * 7.31f) % 100f;
        clock = seed;
    }

    private void OnEnable()
    {
        AcquireCamera();
        checkTimer = 0f;
        distanceScale = 1f;
        gulpPhase = -1f;
        gulpWait = Random.Range(gulpInterval.x, gulpInterval.y) * 0.5f;
        applied = false;
    }

    private void OnDisable()
    {
        Restore();
    }

    private void Update()
    {
        float dt = ignoreSlowMotion ? Time.unscaledDeltaTime : Time.deltaTime;

        // ---- distance, on a timer, never every frame
        if (cullByDistance)
        {
            checkTimer -= Time.unscaledDeltaTime;
            if (checkTimer <= 0f)
            {
                checkTimer = checkInterval;
                UpdateDistanceScale();
            }
        }
        else if (!lightRef.enabled)
        {
            lightRef.enabled = true;
            distanceScale = 1f;
        }

        if (!lightRef.enabled) return;   // culled: nothing left to animate

        clock += dt;

        // ---- 1. jitter, fast and shallow. Centred so the average stays at 1.
        float mult = 1f;
        if (jitterAmount > 0f)
            mult += (Mathf.PerlinNoise(seed + clock * jitterSpeed, 0.37f) - 0.5f) * 2f * jitterAmount;

        // ---- 2. swell, slow and wider
        if (swellAmount > 0f)
            mult += (Mathf.PerlinNoise(seed * 0.5f + clock * swellSpeed, 8.21f) - 0.5f) * 2f * swellAmount;

        // ---- 3. gulp, the dip and flare
        if (gulpDepth > 0f)
        {
            if (gulpPhase >= 0f)
            {
                gulpPhase += dt / Mathf.Max(0.05f, gulpDuration);
                if (gulpPhase >= 1f)
                {
                    gulpPhase = -1f;
                    gulpWait = Random.Range(gulpInterval.x, gulpInterval.y);
                }
                else
                {
                    mult += gulpDepth * GulpEnvelope(gulpPhase, gulpFlare);
                }
            }
            else
            {
                gulpWait -= dt;
                if (gulpWait <= 0f) gulpPhase = 0f;
            }
        }

        if (mult < minBrightness) mult = minBrightness;
        if (mult > maxBrightness) mult = maxBrightness;

        lightRef.intensity = baseIntensity * mult * distanceScale;

        // ---- colour rides the brightness
        if (colorShift > 0f)
        {
            Color target;
            if (mult >= 1f)
            {
                float t = maxBrightness > 1f ? Mathf.Clamp01((mult - 1f) / (maxBrightness - 1f)) : 0f;
                target = Color.Lerp(baseColor, flareColor, t);
            }
            else
            {
                float t = minBrightness < 1f ? Mathf.Clamp01((1f - mult) / (1f - minBrightness)) : 0f;
                target = Color.Lerp(baseColor, emberColor, t);
            }
            lightRef.color = Color.Lerp(baseColor, target, colorShift);
        }

        // ---- sway
        if (swayRadius > 0f)
        {
            float sx = Mathf.PerlinNoise(seed + clock * swaySpeed, 1.13f) - 0.5f;
            float sz = Mathf.PerlinNoise(seed + clock * swaySpeed, 5.79f) - 0.5f;
            float sy = Mathf.PerlinNoise(seed + clock * swaySpeed, 9.41f) - 0.5f;
            tf.localPosition = new Vector3(
                basePos.x + sx * 2f * swayRadius,
                basePos.y + sy * swayRadius,          // half as much up and down
                basePos.z + sz * 2f * swayRadius);
        }

        applied = true;
    }

    #endregion

    #region The gulp

    /// <summary>
    /// One dip and recovery, x from 0 to 1. Returns about -1 at the bottom, then climbs
    /// past zero to +flare as the fire catches, then settles back to 0.
    /// The fall is quick and the recovery is slower, which is the asymmetry that makes it
    /// read as fire rather than as a sine wave.
    /// </summary>
    private static float GulpEnvelope(float x, float flare)
    {
        if (x <= 0f || x >= 1f) return 0f;

        if (x < 0.25f)                                    // fall, fast
            return -Mathf.SmoothStep(0f, 1f, x / 0.25f);

        if (x < 0.60f)                                    // catch, climbs past normal
        {
            float u = (x - 0.25f) / 0.35f;
            return Mathf.Lerp(-1f, flare, Mathf.SmoothStep(0f, 1f, u));
        }

        float v = (x - 0.60f) / 0.40f;                    // settle
        return Mathf.Lerp(flare, 0f, Mathf.SmoothStep(0f, 1f, v));
    }

    #endregion

    #region Internals

    private void UpdateDistanceScale()
    {
        if (cam == null)
        {
            camRetryTimer -= checkInterval;
            if (camRetryTimer <= 0f)
            {
                camRetryTimer = 1f;
                AcquireCamera();
            }
            if (cam == null)
            {
                // No camera found yet. Fail open: stay lit rather than go dark.
                distanceScale = 1f;
                if (!lightRef.enabled) lightRef.enabled = true;
                return;
            }
        }

        float cut = Mathf.Max(1f, cullDistance);
        float fade = Mathf.Clamp(fadeDistance, 0f, cut - 0.5f);
        float fadeStart = cut - fade;

        // Squared compare first so the common far case never takes a square root.
        Vector3 d = cam.transform.position - tf.position;
        float sq = d.sqrMagnitude;

        if (sq >= cut * cut)
        {
            distanceScale = 0f;
            if (lightRef.enabled)
            {
                lightRef.intensity = baseIntensity;   // leave it clean for the next enable
                lightRef.color = baseColor;
                tf.localPosition = basePos;
                lightRef.enabled = false;
            }
            return;
        }

        if (!lightRef.enabled) lightRef.enabled = true;

        if (fade <= 0f || sq <= fadeStart * fadeStart)
        {
            distanceScale = 1f;
            return;
        }

        float dist = Mathf.Sqrt(sq);
        distanceScale = Mathf.Clamp01((cut - dist) / fade);
    }

    private void AcquireCamera()
    {
        // Camera.main is a tagged search, so it happens here and nowhere near Update.
        cam = Camera.main;
        if (cam == null) cam = Camera.current;
    }

    private void Restore()
    {
        if (lightRef == null) return;
        if (applied)
        {
            lightRef.intensity = baseIntensity;
            lightRef.color = baseColor;
            if (tf != null) tf.localPosition = basePos;
            applied = false;
        }
        lightRef.enabled = true;
    }

    #endregion

    #region Public

    /// <summary>The brightness this light returns to. Set it if you change the light at runtime.</summary>
    public float BaseIntensity
    {
        get { return baseIntensity; }
        set { baseIntensity = Mathf.Max(0f, value); }
    }

    /// <summary>Force a dip and flare right now, for example on a thunder beat.</summary>
    public void Gust()
    {
        gulpPhase = 0f;
    }

    #endregion
}
