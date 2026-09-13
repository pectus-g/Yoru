using UnityEngine;

/// <summary>
/// NIGHT VISION. Yoru's reveal ability, toggled with a key (default L).
///
/// The problem it solves: in the dark parts of the cave she reads as a lit character floating in a
/// black void, because the environment around her has nothing lighting it.
///
/// How shipped games solve this (researched 13 Sep 2026): the reveal is a real light that travels
/// with the player and lights the WORLD, never the hero. Hollow Knight's Lumafly Lantern "brightens
/// up the area around the Knight"; Bloodborne's hip lantern and Alan Wake's torch do the same.
/// Unreal exposes this as Lighting Channels, "dynamic lights only affect objects when their lighting
/// channels overlap", so a character can be lit independently of the set. Unity's equivalent is the
/// light culling mask, which is what this component uses. Full-screen night vision (Splinter Cell,
/// Metro) is a post-process filter and is deliberately NOT what this is, because post is off limits.
///
/// The rule that protects everything already tuned: BOTH lights here exclude the Player layer, so
/// YoruLightRig stays the only thing lighting Yoru. Her fur, collar and eyes render identically
/// whether this is on or off. Nothing here touches post processing, ambient, RenderSettings, the
/// weather, or any existing light.
///
/// Both lights are created at runtime and never saved into a scene, the same pattern YoruLightRig
/// uses for its fill light. The prefab gains this component and no child objects.
/// </summary>
[DisallowMultipleComponent]
public class NightVision : MonoBehaviour
{
    #region Inspector

    [Header("=== NIGHT VISION VFX / SFX ===")]
    [Tooltip("Spawned once when night vision switches ON. Empty = no effect. Destroyed after Activate VFX Lifetime seconds.")]
    [SerializeField] private GameObject activateVFX;
    [Tooltip("Spawned once when night vision switches OFF. Empty = no effect.")]
    [SerializeField] private GameObject deactivateVFX;
    [Tooltip("Where the two effects above are born, and what they are parented to so they ride her. Empty = this object's own transform, at Glow Height.")]
    [SerializeField] private Transform vfxAnchor;
    [Tooltip("Seconds before a spawned effect is destroyed.")]
    [SerializeField] private float vfxLifetime = 3f;
    [Tooltip("Played when night vision switches ON. Empty = silent.")]
    [SerializeField] private AudioClip activateSFX;
    [Tooltip("Played when night vision switches OFF. Empty = silent.")]
    [SerializeField] private AudioClip deactivateSFX;
    [Range(0f, 1f)]
    [Tooltip("Volume for both clips above.")]
    [SerializeField] private float sfxVolume = 0.7f;

    [Header("=== INPUT ===")]
    [Tooltip("Key that toggles night vision. L by default; A, C, E, I, Q, R, T, U, Tab, Space and Shift are already taken elsewhere.")]
    [SerializeField] private KeyCode toggleKey = KeyCode.L;
    [Tooltip("Untick to take the key away from the player and drive this only from code (Toggle / SetOn), for a cutscene or an ability gate.")]
    [SerializeField] private bool keyEnabled = true;

    [Header("=== GLOW, the pool of light around her ===")]
    [Tooltip("This is what puts the floor and the rocks back under her feet, so she stops reading as floating in a void.")]
    [SerializeField] private bool glowEnabled = true;
    [Tooltip("Colour of the pool. Taken from her eye glow so the ability reads as HER seeing, not as a lamp.")]
    [SerializeField] private Color glowColor = new Color(0.62f, 0.55f, 1f, 1f);
    [Tooltip("Brightness at full fade-in. Keep it low: this is meant to reveal shapes, not to flatten the cave.")]
    [SerializeField] private float glowIntensity = 1.4f;
    [Tooltip("Metres the pool reaches. Roughly the radius of ground you want to read around her.")]
    [SerializeField] private float glowRange = 9f;
    [Tooltip("Height above her feet the pool sits at, metres. About chest height reads best.")]
    [SerializeField] private float glowHeight = 1f;

    [Header("=== BEAM, what the camera is looking at ===")]
    [Tooltip("A soft cone from the camera along the view direction, so looking at something far reveals it.")]
    [SerializeField] private bool beamEnabled = true;
    [Tooltip("Colour of the cone. Cooler than the pool so the two read as different things.")]
    [SerializeField] private Color beamColor = new Color(0.72f, 0.78f, 1f, 1f);
    [Tooltip("Brightness at full fade-in.")]
    [SerializeField] private float beamIntensity = 1.6f;
    [Tooltip("Metres the cone reaches.")]
    [SerializeField] private float beamRange = 20f;
    [Range(10f, 120f)]
    [Tooltip("Cone width in degrees. Wide reads as vision, narrow reads as a torch.")]
    [SerializeField] private float beamAngle = 55f;
    [Tooltip("Metres the cone starts in front of the camera, so the near edge is not clipped by it.")]
    [SerializeField] private float beamForwardOffset = 0.5f;

    [Header("=== FEEL ===")]
    [Tooltip("Seconds to reach full brightness after the key is pressed.")]
    [SerializeField] private float fadeInSeconds = 0.35f;
    [Tooltip("Seconds to fade back to nothing. Slower than the fade in, so switching it off feels like her eyes adjusting.")]
    [SerializeField] private float fadeOutSeconds = 0.6f;
    [Range(0f, 0.5f)]
    [Tooltip("Slow breathe on the brightness while it is on, as a fraction. 0.08 = plus or minus 8 percent. 0 = dead steady.")]
    [SerializeField] private float breatheAmount = 0.08f;
    [Tooltip("Breathe cycles per second.")]
    [SerializeField] private float breatheSpeed = 0.5f;

    [Header("=== WHAT IT IS ALLOWED TO LIGHT ===")]
    [Tooltip("Layers these lights are NOT allowed to touch. Player is ticked on purpose: it keeps Yoru's own look entirely under YoruLightRig, so her fur and materials render the same whether night vision is on or off. Untick Player only if you want her to catch some of it.")]
    [SerializeField] private LayerMask excludedLayers = 1 << 3;

    #endregion

    #region State

    private Light glow;
    private Light beam;
    private Camera cam;
    private AudioSource audioSource;

    private bool on;
    private float level;            // 0 = fully off, 1 = fully on
    private float breathePhase;

    #endregion

    #region Unity

    private void OnEnable()
    {
        level = 0f;
        ApplyLevel();
    }

    private void OnDisable()
    {
        on = false;
        level = 0f;
        DestroyLight(ref glow);
        DestroyLight(ref beam);
    }

    private void Update()
    {
        if (keyEnabled && Input.GetKeyDown(toggleKey))
        {
            Toggle();
        }

        float target = on ? 1f : 0f;
        float seconds = on ? fadeInSeconds : fadeOutSeconds;

        if (seconds > 0.0001f)
        {
            level = Mathf.MoveTowards(level, target, Time.unscaledDeltaTime / seconds);
        }
        else
        {
            level = target;
        }

        if (on)
        {
            breathePhase += Time.unscaledDeltaTime * breatheSpeed;
        }

        ApplyLevel();
    }

    private void LateUpdate()
    {
        // After the camera has moved for this frame, so the beam never lags a frame behind the view.
        PlaceLights();
    }

    #endregion

    #region Public API

    /// <summary>True while night vision is switched on, including during its fade.</summary>
    public bool IsOn
    {
        get { return on; }
    }

    /// <summary>Flip it. Same thing the key does, for an ability button or a cutscene.</summary>
    public void Toggle()
    {
        SetOn(!on);
    }

    /// <summary>Switch it on or off. Silent and effect-free if it is already in that state.</summary>
    public void SetOn(bool value)
    {
        if (on == value)
        {
            return;
        }

        on = value;
        PlayFeedback(value);
    }

    #endregion

    #region Lights

    private void ApplyLevel()
    {
        float shaped = level * level * (3f - 2f * level);          // smoothstep, so the fade has no hard start or stop
        float breathe = 1f + Mathf.Sin(breathePhase * Mathf.PI * 2f) * breatheAmount * level;
        int mask = ~excludedLayers.value;

        UpdateLight(ref glow, glowEnabled && shaped > 0.001f, "YoruNightVision Glow (runtime)", LightType.Point, mask,
                    glowColor, glowIntensity * shaped * breathe, glowRange, 0f);

        UpdateLight(ref beam, beamEnabled && shaped > 0.001f, "YoruNightVision Beam (runtime)", LightType.Spot, mask,
                    beamColor, beamIntensity * shaped * breathe, beamRange, beamAngle);
    }

    private void UpdateLight(ref Light light, bool wanted, string name, LightType type, int mask,
                             Color color, float intensity, float range, float angle)
    {
        if (!wanted)
        {
            DestroyLight(ref light);
            return;
        }

        if (light == null)
        {
            GameObject go = new GameObject(name);
            go.hideFlags = HideFlags.DontSave;                      // never written into a scene or a prefab

            light = go.AddComponent<Light>();
            light.type = type;
            light.shadows = LightShadows.None;                      // a second shadow direction on top of the scene's reads artificial
            light.renderMode = LightRenderMode.ForcePixel;
            light.lightmapBakeType = LightmapBakeType.Realtime;
        }

        light.cullingMask = mask;
        light.color = color;
        light.intensity = intensity;
        light.range = range;

        if (type == LightType.Spot)
        {
            light.spotAngle = angle;
        }

        PlaceLight(light);
    }

    private void PlaceLights()
    {
        PlaceLight(glow);
        PlaceLight(beam);
    }

    private void PlaceLight(Light light)
    {
        if (light == null)
        {
            return;
        }

        if (light == glow)
        {
            light.transform.position = transform.position + Vector3.up * glowHeight;
            return;
        }

        Camera c = ResolveCamera();

        if (c == null)
        {
            // No camera to ride: park the cone on her, facing where she faces, rather than at the origin.
            light.transform.SetPositionAndRotation(transform.position + Vector3.up * glowHeight, transform.rotation);
            return;
        }

        Transform ct = c.transform;
        light.transform.SetPositionAndRotation(ct.position + ct.forward * beamForwardOffset, ct.rotation);
    }

    private Camera ResolveCamera()
    {
        if (cam != null && cam.isActiveAndEnabled)
        {
            return cam;
        }

        cam = Camera.main;
        return cam;
    }

    private void DestroyLight(ref Light light)
    {
        if (light == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(light.gameObject);
        }
        else
        {
            DestroyImmediate(light.gameObject);
        }

        light = null;
    }

    #endregion

    #region Feedback

    private void PlayFeedback(bool turningOn)
    {
        GameObject prefab = turningOn ? activateVFX : deactivateVFX;

        if (prefab != null)
        {
            Transform anchor = vfxAnchor != null ? vfxAnchor : transform;
            Vector3 at = vfxAnchor != null ? anchor.position : transform.position + Vector3.up * glowHeight;

            GameObject fx = Instantiate(prefab, at, anchor.rotation);
            fx.transform.SetParent(anchor, true);                   // worldPositionStays, so the prefab keeps its authored size

            if (vfxLifetime > 0f)
            {
                Destroy(fx, vfxLifetime);
            }
        }

        AudioClip clip = turningOn ? activateSFX : deactivateSFX;

        if (clip != null)
        {
            EnsureAudioSource();
            audioSource.PlayOneShot(clip, sfxVolume);
        }
    }

    private void EnsureAudioSource()
    {
        if (audioSource != null)
        {
            return;
        }

        audioSource = GetComponent<AudioSource>();

        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;                          // her own ability, so it reads as hers, not as a point in the world
        }
    }

    #endregion
}
