using System.Collections;
using UnityEngine;

/// <summary>
/// The Oni arena's weather profile.
///
/// Phase 1: steady rain, floor slowly accumulates dampness (darkens + turns
/// reflective). Phase 2 (polled from EnemyCombat.IsPhase2 - it has no events):
/// rain doubles, fog thickens, the floor soaks to fully wet, and lightning
/// strikes frequently with a real light flash.
///
/// Floor wetness drives the TERRAIN's layer properties (smoothness + diffuse
/// remap) - no particles, no decals, no extra draw calls. Terrain layers are
/// ASSETS: in the editor, runtime changes would stick, so originals are cached
/// on Start and restored on exit. If Unity hard-crashes mid-play the layer may
/// stay wet - the original values are logged at Start for manual restore.
///
/// Perf notes: terrain properties update at 5 Hz, not per frame. Rain emission
/// changes only during the 2.5 s phase transition. Lightning allocates one
/// prefab instance per strike (destroyed after 5 s). Everything else is flat.
/// </summary>
[DisallowMultipleComponent]
public class StormWeather : MonoBehaviour
{
    [Header("References (all optional - auto-found if empty)")]
    [Tooltip("The Oni's EnemyCombat. Auto-found.")]
    [SerializeField] private EnemyCombat oniCombat;

    [Tooltip("Root of the Rain_Cave prefab instance. Its child particle systems are ramped at Phase 2.")]
    [SerializeField] private Transform rainRoot;

    [Tooltip("Arena terrain. Auto-found from Terrain.activeTerrain.")]
    [SerializeField] private Terrain terrain;

    [Tooltip("One-shot lightning VFX prefab (thundershock).")]
    [SerializeField] private GameObject lightningPrefab;

    [Header("Floor wetness")]
    [Tooltip("How wet the floor gets during Phase 1 (0..1).")]
    [SerializeField, Range(0f, 1f)] private float calmMaxWetness = 0.35f;

    [Tooltip("Seconds of Phase 1 rain to reach the calm wetness above.")]
    [SerializeField] private float calmSoakTime = 60f;

    [Tooltip("Seconds after Phase 2 starts to reach fully wet.")]
    [SerializeField] private float stormSoakTime = 30f;

    [Tooltip("Terrain layer smoothness when fully wet. Dry value is whatever the layer already has.")]
    [SerializeField, Range(0f, 1f)] private float wetSmoothness = 0.75f;

    [Tooltip("Albedo multiplier when fully wet. 0.6 = noticeably darker, rained-on rock.")]
    [SerializeField, Range(0.3f, 1f)] private float wetDarkening = 0.65f;

    [Header("Phase 2 storm")]
    [Tooltip("Rain emission multiplier at Phase 2 (1 = no change).")]
    [SerializeField, Range(1f, 4f)] private float stormRainMultiplier = 2f;

    [Tooltip("Kronnect fog density: calm and storm.")]
    [SerializeField] private float calmFogDensity = 0.45f;
    [SerializeField] private float stormFogDensity = 0.7f;

    [Tooltip("Seconds for rain/fog to ramp when Phase 2 hits.")]
    [SerializeField] private float transitionDuration = 2.5f;

    [Header("Lightning")]
    [Tooltip("Seconds between strikes: calm (x=min, y=max).")]
    [SerializeField] private Vector2 calmStrikeInterval = new Vector2(14f, 26f);
    [SerializeField] private Vector2 stormStrikeInterval = new Vector2(3f, 8f);

    [Tooltip("World position strikes spawn at - above the roof opening.")]
    [SerializeField] private Vector3 strikeOrigin = new Vector3(481f, 45f, 447f);
    [SerializeField] private float strikeSpread = 10f;
    [SerializeField] private float flashPeakIntensity = 2.5f;
    [SerializeField] private float flashDuration = 0.22f;
    [SerializeField] private Color flashColor = new Color(0.8f, 0.88f, 1f);

    [SerializeField] private bool debugLog = true;

    // ---- runtime ----
    private float wetness;                 // 0..1 accumulated
    private float storm;                   // 0 calm, 1 full storm
    private bool  phase2;
    private Light flashLight;

    private ParticleSystem[] rainSystems;
    private float[] rainBaseRates;

    private TerrainLayer[] layers;
    private float[] drySmoothness;
    private Vector4[] dryRemapMax;

    private MonoBehaviour kronnectFog;
    private System.Reflection.PropertyInfo fogDensity;

    private void Start()
    {
        if (oniCombat == null)
#if UNITY_2023_1_OR_NEWER
            oniCombat = Object.FindFirstObjectByType<EnemyCombat>();
#else
            oniCombat = Object.FindObjectOfType<EnemyCombat>();
#endif
        if (terrain == null) terrain = Terrain.activeTerrain;
        FindFog();
        CacheRain();
        CacheTerrain();
        MakeFlashLight();

        StartCoroutine(WetnessLoop());
        StartCoroutine(LightningLoop());

        if (debugLog) Debug.Log("[StormWeather] Ready. Rain on, floor drying... wait, soaking.");
    }

    // ROUND 58 (Oni cinematic): while held, the storm does NOT break even though phase 2 is on —
    // the boss layer holds it through the roar and releases it at the LIGHTNING-ON-THE-CLUB beat,
    // so the heavy rain and the sky bolt land together with her strike. Never serialized; if the
    // boss object dies the release is called from its OnDisable, and with no boss the hold is
    // simply never set.
    private bool breakHold;

    public void SetBreakHold(bool held)
    {
        breakHold = held;
        if (debugLog) Debug.Log($"[StormWeather] storm break {(held ? "HELD (waiting for the lightning beat)" : "released")}");
    }

    private void Update()
    {
        if (phase2 || oniCombat == null) return;
        if (breakHold) return;   // ROUND 58: the cinematic decides the moment
        if (oniCombat.IsPhase2())
        {
            phase2 = true;
            StartCoroutine(RampStorm());
            Strike();
            if (debugLog) Debug.Log("[StormWeather] PHASE 2 - storm breaking.");
        }
    }

    // ---- wetness: the floor itself gets wet ----

    private IEnumerator WetnessLoop()
    {
        var wait = new WaitForSeconds(0.2f);   // 5 Hz is plenty for a soak
        while (true)
        {
            float target = phase2 ? 1f : calmMaxWetness;
            float time   = phase2 ? stormSoakTime : calmSoakTime;
            wetness = Mathf.MoveTowards(wetness, target, 0.2f / Mathf.Max(1f, time));
            ApplyWetness(wetness);
            yield return wait;
        }
    }

    private void ApplyWetness(float w)
    {
        if (layers == null) return;
        for (int i = 0; i < layers.Length; i++)
        {
            if (layers[i] == null) continue;
            layers[i].smoothness = Mathf.Lerp(drySmoothness[i], wetSmoothness, w);
            float d = Mathf.Lerp(1f, wetDarkening, w);
            Vector4 r = dryRemapMax[i];
            layers[i].diffuseRemapMax = new Vector4(r.x * d, r.y * d, r.z * d, r.w);
        }
    }

    // ---- phase 2 ramp: rain + fog ----

    private IEnumerator RampStorm()
    {
        float t = 0f;
        while (t < transitionDuration)
        {
            t += Time.deltaTime;
            storm = Mathf.SmoothStep(0f, 1f, t / transitionDuration);
            ApplyRain(Mathf.Lerp(1f, stormRainMultiplier, storm));
            ApplyFog(Mathf.Lerp(calmFogDensity, stormFogDensity, storm));
            yield return null;
        }
        storm = 1f;
    }

    private void ApplyRain(float factor)
    {
        if (rainSystems == null) return;
        for (int i = 0; i < rainSystems.Length; i++)
        {
            if (rainSystems[i] == null) continue;
            var em = rainSystems[i].emission;
            em.rateOverTimeMultiplier = rainBaseRates[i] * factor;
        }
    }

    private void ApplyFog(float density)
    {
        if (kronnectFog != null && fogDensity != null)
            fogDensity.SetValue(kronnectFog, density);
    }

    // ---- lightning ----

    private IEnumerator LightningLoop()
    {
        yield return new WaitForSeconds(Random.Range(5f, 9f));
        while (true)
        {
            Vector2 win = Vector2.Lerp(calmStrikeInterval, stormStrikeInterval, storm);
            yield return new WaitForSeconds(Random.Range(win.x, win.y));
            Strike();
        }
    }

    private void Strike()
    {
        Vector3 pos = strikeOrigin + new Vector3(Random.Range(-strikeSpread, strikeSpread), 0f,
                                                 Random.Range(-strikeSpread, strikeSpread));
        StrikeAt(pos);
    }

    /// <summary>ROUND 61 (Oni cinematic) — a strike at a CHOSEN point: the converging burst of
    /// the lightning-pull moment places its bolts by hand (far → near → almost on him) instead
    /// of the random spread. Same prefab, same light flash as every storm strike.
    /// ROUND 62: the caller chooses WHERE on the ground — the bolt itself always spawns at the
    /// storm's SKY height (round 61 spawned placed bolts at floor level: log showed y=0.14 while
    /// the storm's own strikes fly at y=45 — that was Hazel's empty sky).</summary>
    public void StrikeAt(Vector3 pos)
    {
        pos.y = strikeOrigin.y;   // the sky is up there

        if (lightningPrefab != null)
            Destroy(Instantiate(lightningPrefab, pos, Quaternion.identity), 5f);

        if (flashLight != null) StartCoroutine(Flash());
        if (debugLog) Debug.Log($"[StormWeather] ⚡ strike at {pos}");
    }

    /// Shaped falloff (from LightingController) - the sine gives the strobing
    /// double-flicker of a real strike. A flat fade reads as a lamp turning off.
    private IEnumerator Flash()
    {
        float t = 1f;
        float rate = 1f / Mathf.Max(0.01f, flashDuration);
        while (t > 0f)
        {
            t -= Time.deltaTime * rate;
            float shaped = Mathf.Max(0f, t) * (0.6f + 0.4f * Mathf.Abs(Mathf.Sin(t * 28f)));
            flashLight.intensity = shaped * flashPeakIntensity;
            yield return null;
        }
        flashLight.intensity = 0f;
    }

    // ---- caching / cleanup ----

    private void CacheRain()
    {
        if (rainRoot == null) return;
        rainSystems = rainRoot.GetComponentsInChildren<ParticleSystem>(true);
        rainBaseRates = new float[rainSystems.Length];
        for (int i = 0; i < rainSystems.Length; i++)
            rainBaseRates[i] = rainSystems[i].emission.rateOverTimeMultiplier;
    }

    private void CacheTerrain()
    {
        if (terrain == null || terrain.terrainData == null)
        {
            if (debugLog) Debug.LogWarning("[StormWeather] No terrain found - floor wetness disabled.");
            return;
        }
        layers = terrain.terrainData.terrainLayers;
        drySmoothness = new float[layers.Length];
        dryRemapMax   = new Vector4[layers.Length];
        for (int i = 0; i < layers.Length; i++)
        {
            if (layers[i] == null) continue;
            drySmoothness[i] = layers[i].smoothness;
            dryRemapMax[i]   = layers[i].diffuseRemapMax;
            if (dryRemapMax[i] == Vector4.zero) dryRemapMax[i] = Vector4.one;  // unset remap means 1
            if (debugLog)
                Debug.Log($"[StormWeather] layer '{layers[i].name}' dry smoothness={drySmoothness[i]:F2} " +
                          $"(restore to this if a crash ever leaves it wet)");
        }
    }

    private void MakeFlashLight()
    {
        var go = new GameObject("StormFlashLight");
        go.transform.SetParent(transform, false);
        go.transform.rotation = Quaternion.Euler(60f, 30f, 0f);
        flashLight = go.AddComponent<Light>();
        flashLight.type = LightType.Directional;
        flashLight.shadows = LightShadows.None;
        flashLight.color = flashColor;
        flashLight.intensity = 0f;
    }

    private void FindFog()
    {
#if UNITY_2023_1_OR_NEWER
        var all = Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
        var all = Object.FindObjectsOfType<MonoBehaviour>(true);
#endif
        foreach (var mb in all)
            if (mb != null && mb.GetType().Name == "VolumetricFog")
            { kronnectFog = mb; break; }
        if (kronnectFog != null)
            fogDensity = kronnectFog.GetType().GetProperty("density");
    }

    private void OnDisable()  { RestoreTerrain(); if (flashLight) flashLight.intensity = 0f; }
    private void OnApplicationQuit() { RestoreTerrain(); }

    private void RestoreTerrain()
    {
        if (layers == null) return;
        for (int i = 0; i < layers.Length; i++)
        {
            if (layers[i] == null) continue;
            layers[i].smoothness = drySmoothness[i];
            layers[i].diffuseRemapMax = dryRemapMax[i];
        }
        if (debugLog) Debug.Log("[StormWeather] terrain layers restored to dry values.");
    }

    // ---- tests: right-click the component header ----

    [ContextMenu("Test: Strike lightning")]
    private void TestStrike() { if (Application.isPlaying) Strike(); else Debug.Log("Enter Play mode first."); }

    [ContextMenu("Test: Force Phase 2")]
    private void TestPhase2()
    {
        if (!Application.isPlaying) { Debug.Log("Enter Play mode first."); return; }
        if (phase2) return;
        phase2 = true; StartCoroutine(RampStorm()); Strike();
        Debug.Log("[StormWeather] PHASE 2 forced.");
    }

    [ContextMenu("Test: Instant full wetness")]
    private void TestWet()
    {
        if (!Application.isPlaying) { Debug.Log("Enter Play mode first."); return; }
        wetness = 1f; ApplyWetness(1f);
        Debug.Log("[StormWeather] floor set to fully wet - is it visibly darker and shinier?");
    }
}
