using UnityEngine;

/// <summary>
/// ROUND 71 - makes a fire light look alive, and keeps it off the light budget.
///
/// Put this on the Point Light INSIDE a brazier or lantern prefab (the child called
/// "Point Light"), not on the brazier root. One component per light.
///
/// It does three things:
///   1. FLICKER. Wobbles the light's brightness with smooth noise, not random jitter.
///      Random jitter reads as a broken lamp; smooth noise reads as fire.
///   2. SWAY. Moves the light a few centimetres so the shadows and the highlights on
///      nearby stone breathe. This is what actually sells it, more than the brightness.
///   3. DISTANCE CUT. Turns the Light component off when the camera is far away, and
///      fades it out first so it never pops. Built-in RP only draws a few real lights
///      per object (Quality > Pixel Light Count, currently 4), so every torch that
///      switches itself off out of shot gives that slot back to the fight.
///
/// Costs nothing per frame: no allocations, no GetComponent, no Find. The distance
/// check runs on a timer, not every frame.
///
/// Runtime only on purpose. It does NOT run in the Scene view, so it can never write
/// a flicker offset back into your prefab.
/// </summary>
[RequireComponent(typeof(Light))]
[DisallowMultipleComponent]
public class TorchLight : MonoBehaviour
{
    #region Inspector

    [Header("Flicker - the fast wobble")]
    [Tooltip("Turn the brightness wobble on or off.")]
    [SerializeField] private bool flicker = true;
    [Tooltip("How deep the wobble goes. 0 = steady lamp, 1 = almost out. 0.25 to 0.35 looks like a real fire.")]
    [Range(0f, 1f)]
    [SerializeField] private float flickerDepth = 0.28f;
    [Tooltip("How fast the wobble runs. Higher = twitchier. 5 to 7 for an open flame, 2 to 3 for a lantern behind paper.")]
    [Range(0.2f, 20f)]
    [SerializeField] private float flickerSpeed = 5.5f;

    [Header("Gust - the slow breath underneath")]
    [Tooltip("A second, much slower wobble on top of the first. Without it the flicker gets repetitive after a few seconds.")]
    [SerializeField] private bool gusts = true;
    [Range(0f, 1f)]
    [SerializeField] private float gustDepth = 0.30f;
    [Range(0.05f, 3f)]
    [SerializeField] private float gustSpeed = 0.55f;

    [Header("Sway - the light moves, not just dims")]
    [Tooltip("Metres the light drifts around its resting spot. Small. 0.05 to 0.10. Set 0 to switch sway off.")]
    [Range(0f, 0.5f)]
    [SerializeField] private float swayRadius = 0.06f;
    [Range(0.1f, 10f)]
    [SerializeField] private float swaySpeed = 2.2f;

    [Header("Distance cut - this is the performance part")]
    [Tooltip("Switch the Light off when the camera is further away than Cull Distance. The fire particles keep playing, only the light stops.")]
    [SerializeField] private bool cullByDistance = true;
    [Tooltip("Metres. Past this the Light component is off. Keep it a bit bigger than the light's Range so you never see it happen.")]
    [SerializeField] private float cullDistance = 45f;
    [Tooltip("Metres of fade before the cut, so the light dims out instead of blinking off.")]
    [SerializeField] private float fadeDistance = 12f;
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
    private Vector3 basePos;

    private float seed;        // per instance, so two braziers never flicker in step
    private float clock;       // the one clock the noise is read from
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
        basePos = tf.localPosition;

        // A stable per instance offset. Position based, so it survives a reload and two
        // braziers in the same spot still differ by their tiny placement differences.
        seed = (Mathf.Abs(tf.position.x) * 3.17f + Mathf.Abs(tf.position.z) * 7.31f) % 100f;
        clock = seed;
    }

    private void OnEnable()
    {
        AcquireCamera();
        checkTimer = 0f;
        distanceScale = 1f;
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

        // ---- brightness
        clock += dt;
        float mult = 1f;

        if (flicker && flickerDepth > 0f)
        {
            // PerlinNoise returns 0..1 around 0.5. Centre it so the average brightness
            // stays at baseIntensity instead of drifting darker.
            float n = Mathf.PerlinNoise(seed + clock * flickerSpeed, 0.37f) - 0.5f;
            mult += n * 2f * flickerDepth;
        }

        if (gusts && gustDepth > 0f)
        {
            float g = Mathf.PerlinNoise(seed * 0.5f + clock * gustSpeed, 8.21f) - 0.5f;
            mult += g * 2f * gustDepth;
        }

        if (mult < 0.05f) mult = 0.05f;   // never fully out, a dead frame reads as a bug

        lightRef.intensity = baseIntensity * mult * distanceScale;

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

    /// <summary>Snuff the fire out, or light it again. Turns the whole object off, particles included.</summary>
    public void SetLit(bool lit)
    {
        Restore();
        if (tf != null && tf.parent != null) tf.parent.gameObject.SetActive(lit);
        else gameObject.SetActive(lit);
    }

    #endregion
}
