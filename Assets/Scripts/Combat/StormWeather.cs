using System.Collections;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;   // ROUND 77 - storm grade + strike flash
using DistantLands.Cozy;        // ROUND 63 — the COZY bridge (package installed; same usings as YoruCozyIntegration)
using DistantLands.Cozy.Data;

/// <summary>
/// The Oni arena's weather profile.
///
/// ROUND 77: post processing rides the same arc. The scene volume keeps the magical night
/// base. A second global volume (priority 10, created at runtime, weight 0) carries the
/// storm grade and fades in at fight start and fully at phase 2, and a third (priority 11)
/// flashes the screen with every lightning strike. Same pattern as CombatPostProcessPulse.
///
/// ROUND 73: before the fight there is NO weather. COZY snaps to the pre-fight
/// profile (Clear) at scene start; the floor is dry and there is no lightning.
/// The storm arrives the instant the Oni engages (Alert/Chase/Telegraph/Attack,
/// the same rule OniBoss uses to start the boss music).
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

    [Header("COZY bridge - round 63 (Hazel's weather arc)")]
    [Tooltip("ROUND 63 - drive the COZY weather system through the fight: pre-fight = your open-sky profile from scene start (round 73), phase 1 = your storm profile the instant he engages, and from the lightning beat on = your storm-chaos profile. NEEDS the COZY rig in THIS scene (like DemoScene_Day has); without it, this does nothing and the cave behaves exactly as before. When COZY runs, the old Rain_Cave particles are retired - COZY owns the rain (Hazel's pick). Untick to go back to the pre-COZY cave.")]
    [SerializeField] private bool driveCozy = true;
    [Tooltip("YOUR pick - the phase-1 mood. Try 'Imminent Storm', 'Approaching Storm' or 'Overcast' from the COZY package's Weather Profiles folder.")]
    [SerializeField] private WeatherProfile phase1Weather;
    [Tooltip("YOUR pick - from the lightning beat to the end of the fight. Try 'Thunder Storm' (or 'Storm Eye' / 'Electric Fog' for weirder chaos).")]
    [SerializeField] private WeatherProfile phase2Weather;
    [Tooltip("ROUND 73 - YOUR pick - the sky BEFORE the fight. COZY snaps to it instantly at scene start, no blend. Use 'Clear' for an open night sky. Leave empty to keep whatever weather the scene already has.")]
    [SerializeField] private WeatherProfile preFightWeather;

    [Header("Post processing - round 77 (magical base, storm on top)")]
    [Tooltip("ROUND 77 - drag Cave_Oni_Storm here. A global volume at priority 10 is created at runtime and its weight is faded: 0 before the fight, Phase 1 Post Weight when he engages, 1 at phase 2. Your normal scene volume (priority 0) is never touched. The hit and hallucination pulses (priority 100 and 101) still sit on top of everything.")]
    [SerializeField] private PostProcessProfile stormProfile;
    [Tooltip("How much of the storm grade shows during phase 1. 0.3 to 0.5 keeps the arena readable and saves the full look for phase 2.")]
    [SerializeField, Range(0f, 1f)] private float phase1PostWeight = 0.4f;
    [Tooltip("Seconds the storm grade takes to fade in at fight start. Phase 2 uses Transition Duration above.")]
    [SerializeField] private float postFadeSeconds = 0.8f;
    [Tooltip("Every lightning strike also flashes the screen: exposure jumps by about this many EV and decays with the same double flicker as the flash light. 0 turns the screen flash off.")]
    [SerializeField, Range(0f, 2f)] private float strikeFlashExposure = 0.5f;
    [Tooltip("Chromatic aberration at the peak of a strike flash.")]
    [SerializeField, Range(0f, 1f)] private float strikeFlashChromatic = 0.2f;
    [Tooltip("Seconds a strike flash lasts on screen. Shorter than the light flash reads sharper.")]
    [SerializeField] private float strikeFlashSeconds = 0.12f;

    [SerializeField] private bool debugLog = true;

    // ---- runtime ----
    private float wetness;                 // 0..1 accumulated
    private float storm;                   // 0 calm, 1 full storm
    private bool  phase2;
    private bool  fightStarted;            // ROUND 73 - set the moment the Oni engages
    private Light flashLight;

    // ROUND 77 - post processing
    private PostProcessVolume stormVolume;
    private PostProcessVolume flashVolume;
    private ColorGrading flashGrading;
    private ChromaticAberration flashChromatic;
    private Coroutine postFade;
    private Coroutine postFlash;
    private const float FlashExposureCeiling = 3f;   // the flash volume's exposure; weight does the dimming

    private ParticleSystem[] rainSystems;
    private float[] rainBaseRates;

    private TerrainLayer[] layers;
    private float[] drySmoothness;
    private Vector4[] dryRemapMax;

    private MonoBehaviour kronnectFog;
    private System.Reflection.PropertyInfo fogDensity;

    // ROUND 63 — COZY runtime.
    private CozyWeather cozy;

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
        MakePostVolumes();   // ROUND 77

        StartCoroutine(WetnessLoop());
        // ROUND 73 - LightningLoop no longer starts here; it starts the moment the fight starts (OnFightStarted).
        StartCoroutine(CozyBootstrap());   // ROUND 63 — no-op if no COZY rig in the scene

        if (debugLog) Debug.Log("[StormWeather] Ready. Open sky, dry floor. Waiting for the fight.");
    }

    // ---- ROUND 63: the COZY bridge — Hazel's weather arc -------------------------------------
    // ROUND 73: pre-fight = open sky (her profile, snapped once COZY is up). Phase 1 storm = the moment he engages (Update).
    // The lightning beat / phase 2: storm chaos (set in Update's phase-2 block below).
    // COZY owns the rain: the old Rain_Cave is retired while the bridge runs. Everything is
    // null-safe — a cave without the COZY rig behaves exactly as before this round.

    private IEnumerator CozyBootstrap()
    {
        if (!driveCozy) yield break;

        float until = Time.unscaledTime + 5f;
        while (CozyWeather.instance == null && Time.unscaledTime < until) yield return null;
        cozy = CozyWeather.instance;
        if (cozy == null)
        {
            if (debugLog) Debug.Log("[StormWeather] COZY bridge: no COZY rig in this scene — cave weather stays as before.");
            yield break;
        }

        if (rainRoot != null)
        {
            rainRoot.gameObject.SetActive(false);
            if (debugLog) Debug.Log("[StormWeather] COZY bridge: Rain_Cave retired — COZY owns the rain now.");
        }

        // ROUND 73 - open sky until the fight. Snap to it (0 s), no 15 s blend from the scene's default weather.
        SetCozyWeather(preFightWeather, "pre-fight - open sky", 0f);
    }

    private void SetCozyWeather(WeatherProfile profile, string why)
    {
        SetCozyWeather(profile, why, -1f);
    }

    // ROUND 73 - transitionTime: negative = COZY's own default blend (weatherTransitionTime, 15 s in this scene),
    // 0 = instant snap. Same null-safety as before.
    private void SetCozyWeather(WeatherProfile profile, string why, float transitionTime)
    {
        if (cozy == null || profile == null) return;
        var eco = cozy.weatherModule != null ? cozy.weatherModule.ecosystem : null;
        if (eco == null) return;
        if (transitionTime < 0f) eco.SetWeather(profile);
        else                     eco.SetWeather(profile, transitionTime);
        if (debugLog) Debug.Log($"[StormWeather] COZY → '{profile.name}' ({why}"
                              + (transitionTime < 0f ? "" : $", {transitionTime:F0} s") + ")");
    }

    // ROUND 73 - the moment the fight starts: storm on (instant), lightning loop on, floor may start to soak.
    // Called from Update on engage, and as a safety from the phase-2 block so the storm can never be skipped.
    private void OnFightStarted(string why)
    {
        if (fightStarted) return;
        fightStarted = true;
        if (!phase2) SetCozyWeather(phase1Weather, "fight engaged - storm rolls in", 0f);
        StartCoroutine(LightningLoop());
        FadePostTo(phase1PostWeight, postFadeSeconds);   // ROUND 77
        if (debugLog) Debug.Log($"[StormWeather] FIGHT STARTED ({why}) - storm on, floor starts to soak.");
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
        if (oniCombat == null) return;

        // ROUND 73 - the storm arrives the moment the fight starts. Same rule OniBoss uses to start the
        // boss music (Alert / Chase / Telegraph / Attack), so sky and music turn together. Instant, Hazel's pick.
        if (!fightStarted && !phase2)
        {
            var s = oniCombat.GetCurrentState();
            if (s == EnemyCombat.EnemyState.Alert || s == EnemyCombat.EnemyState.Chase
             || s == EnemyCombat.EnemyState.Telegraph || s == EnemyCombat.EnemyState.Attack)
                OnFightStarted("he engaged");
        }

        if (phase2) return;
        if (breakHold) return;   // ROUND 58: the cinematic decides the moment
        if (oniCombat.IsPhase2())
        {
            phase2 = true;
            OnFightStarted("phase 2 reached");   // ROUND 73 - no-op if the fight already started
            StartCoroutine(RampStorm());
            FadePostTo(1f, transitionDuration);   // ROUND 77
            Strike();
            SetCozyWeather(phase2Weather, "the sky answers — storm chaos");   // ROUND 63
            if (debugLog) Debug.Log("[StormWeather] PHASE 2 - storm breaking.");
        }
    }

    // ---- wetness: the floor itself gets wet ----

    private IEnumerator WetnessLoop()
    {
        var wait = new WaitForSeconds(0.2f);   // 5 Hz is plenty for a soak
        while (true)
        {
            float target = !fightStarted ? 0f : (phase2 ? 1f : calmMaxWetness);   // ROUND 73 - dry until the fight
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
        StrikeAt(pos, 1f);
    }

    /// <summary>ROUND 65 — same, with a size multiplier for fatter cinematic bolts.</summary>
    public void StrikeAt(Vector3 pos, float scale)
    {
        pos.y = strikeOrigin.y;   // the sky is up there

        if (lightningPrefab != null)
        {
            GameObject bolt = Instantiate(lightningPrefab, pos, Quaternion.identity);
            if (!Mathf.Approximately(scale, 1f))
                bolt.transform.localScale *= Mathf.Clamp(scale, 0.3f, 4f);
            Destroy(bolt, 5f);
        }

        if (flashLight != null) StartCoroutine(Flash());
        if (flashVolume != null && strikeFlashExposure > 0f)   // ROUND 77
        {
            if (postFlash != null) StopCoroutine(postFlash);
            postFlash = StartCoroutine(PostFlash());
        }
        if (debugLog) Debug.Log($"[StormWeather] ⚡ strike at {pos}"
                              + (Mathf.Approximately(scale, 1f) ? "" : $" x{scale:F1}"));
    }

    // ROUND 65 — COZY's own thunder on demand: its full screen flash + bolt + rumble SOUND, from
    // the weather system already in the project. Null-safe: without the COZY rig (or before its
    // thunder FX is live) it does nothing. The manager is cached after the first find.
    private CozyThunderManager cozyThunderMgr;

    public void ThunderNow()
    {
        if (cozyThunderMgr == null) cozyThunderMgr = FindObjectOfType<CozyThunderManager>();
        if (cozyThunderMgr == null || cozyThunderMgr.thunderFX == null || cozyThunderMgr.weatherSphere == null)
        {
            if (debugLog) Debug.Log("[StormWeather] COZY thunder skipped — no live thunder manager in the scene.");
            return;
        }
        try
        {
            cozyThunderMgr.Strike();
            if (debugLog) Debug.Log("[StormWeather] COZY thunder → Strike()");
        }
        catch (System.Exception e)
        {
            if (debugLog) Debug.Log($"[StormWeather] COZY thunder failed safely: {e.Message}");
        }
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

    // ---- ROUND 77: post processing, storm grade + strike flash ----
    // Same pattern as CombatPostProcessPulse: separate global volumes created at runtime, weight 0 until
    // needed, so the base profile on the scene volume is never modified. Storm sits at priority 10, the
    // strike flash at 11, both under the hit pulse (100) and hallucination (101) volumes.

    private void MakePostVolumes()
    {
        // The PostProcessLayer on the camera only listens to some layers. Put the volumes on the first one it wants.
        PostProcessLayer ppLayer = Object.FindFirstObjectByType<PostProcessLayer>();
        int layer = gameObject.layer;
        if (ppLayer != null && ppLayer.volumeLayer.value != 0)
        {
            for (int i = 0; i < 32; i++)
                if ((ppLayer.volumeLayer.value & (1 << i)) != 0) { layer = i; break; }
        }
        else if (debugLog) Debug.Log("[StormWeather] no PostProcessLayer found - post volumes use this object's layer and may be ignored.");

        if (stormProfile != null)
        {
            GameObject go = new GameObject("StormPost (runtime)");
            go.transform.SetParent(transform, false);
            go.layer = layer;
            stormVolume = go.AddComponent<PostProcessVolume>();
            stormVolume.isGlobal = true;
            stormVolume.priority = 10f;
            stormVolume.weight = 0f;
            stormVolume.sharedProfile = stormProfile;
        }
        else if (debugLog) Debug.Log("[StormWeather] Storm Profile slot is empty - no storm grade, only the strike flash.");

        GameObject fgo = new GameObject("StormFlash (runtime)");
        fgo.transform.SetParent(transform, false);
        fgo.layer = layer;
        flashVolume = fgo.AddComponent<PostProcessVolume>();
        flashVolume.isGlobal = true;
        flashVolume.priority = 11f;
        flashVolume.weight = 0f;
        PostProcessProfile p = ScriptableObject.CreateInstance<PostProcessProfile>();
        flashGrading = ScriptableObject.CreateInstance<ColorGrading>();
        flashGrading.enabled.Override(true);
        flashGrading.postExposure.Override(FlashExposureCeiling);
        p.AddSettings(flashGrading);
        flashChromatic = ScriptableObject.CreateInstance<ChromaticAberration>();
        flashChromatic.enabled.Override(true);
        flashChromatic.intensity.Override(0f);
        p.AddSettings(flashChromatic);
        flashVolume.profile = p;

        if (debugLog) Debug.Log("[StormWeather] post volumes ready on layer " + layer + " (storm " + (stormVolume != null ? "priority 10" : "none") + ", flash priority 11).");
    }

    private void FadePostTo(float target, float seconds)
    {
        if (stormVolume == null) return;
        if (postFade != null) StopCoroutine(postFade);
        postFade = StartCoroutine(FadePost(target, seconds));
    }

    private IEnumerator FadePost(float target, float seconds)
    {
        float start = stormVolume.weight;
        float t = 0f;
        seconds = Mathf.Max(0.01f, seconds);
        while (t < seconds)
        {
            t += Time.deltaTime;
            stormVolume.weight = Mathf.Lerp(start, target, Mathf.SmoothStep(0f, 1f, t / seconds));
            yield return null;
        }
        stormVolume.weight = target;
        postFade = null;
        if (debugLog) Debug.Log($"[StormWeather] storm post weight -> {target:F2}");
    }

    /// Screen flash for a strike. The flash volume holds exposure at FlashExposureCeiling and the WEIGHT
    /// does the work, so the result rides on whatever the base and storm grades are doing underneath.
    /// Weight w lifts exposure by about w x (ceiling - 1) EV. Same double flicker shape as Flash().
    private IEnumerator PostFlash()
    {
        float peak = Mathf.Clamp01(strikeFlashExposure / (FlashExposureCeiling - 1f));
        flashChromatic.intensity.Override(Mathf.Clamp01(strikeFlashChromatic / Mathf.Max(0.05f, peak)));
        float t = 1f;
        float rate = 1f / Mathf.Max(0.01f, strikeFlashSeconds);
        while (t > 0f)
        {
            t -= Time.deltaTime * rate;
            float shaped = Mathf.Max(0f, t) * (0.6f + 0.4f * Mathf.Abs(Mathf.Sin(t * 28f)));
            flashVolume.weight = peak * shaped;
            yield return null;
        }
        flashVolume.weight = 0f;
        postFlash = null;
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

    private void OnDisable()
    {
        RestoreTerrain();
        if (flashLight) flashLight.intensity = 0f;
        if (stormVolume) stormVolume.weight = 0f;   // ROUND 77
        if (flashVolume) flashVolume.weight = 0f;
    }
    private void OnApplicationQuit() { RestoreTerrain(); }
    private void OnDestroy()   // ROUND 77 - the flash profile and its settings are runtime objects, free them
    {
        if (flashVolume != null && flashVolume.profile != null)
            RuntimeUtilities.DestroyProfile(flashVolume.profile, true);
    }

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
        OnFightStarted("test"); FadePostTo(1f, transitionDuration);   // ROUND 77
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
