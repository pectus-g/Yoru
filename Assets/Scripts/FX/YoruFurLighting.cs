using UnityEngine;

/// <summary>
/// ROUND 78 - feeds XFur Studio 4 the three global shader values it never sets by itself.
/// Put this on the Directional Light in the Oni cave. One per scene.
///
/// WHY THIS EXISTS. The XFur shell shader has two terms that give fur its form:
///
///   1. The anisotropic strand specular, the sheen that runs along the fur and reads as
///      direction and volume. XFurStudio_StandardPasses.cginc line 392 ends with
///      "specFinal *= _XFurMainStandardLightColor * NdotL * occlusion".
///
///   2. The warm backlight that bleeds through the fur on the side turned away from the
///      key light. Line 411 builds it from a hardcoded warm tint, a tight pow(rim,5)
///      silhouette falloff, the key light colour, and _XFurSelfTransmission.
///
/// Both are multiplied by globals. XFur ships a component called XFurLightManager that
/// sets _XFurMainStandardLightDir and _XFurMainStandardLightColor, but that component was
/// never placed in any scene in this project, so the colour global stayed at zero and the
/// specular was multiplied by black. _XFurSelfTransmission is worse: it is declared in
/// XFurStudio_Core.cginc line 141 and nothing in the entire XFur package ever writes to
/// it, so it was zero everywhere and the backlight never existed at all.
///
/// The result was fur lit by diffuse only. No sheen, no backlight, no rim from any real
/// light. That is what "Yoru is flat" was. Every earlier fix in this scene, the light
/// probes, the occlusion, the ambient gradient, the colour grade, was adjusting the
/// diffuse term while the two form-giving terms were multiplied by zero.
///
/// This does the job of XFurLightManager and adds the transmission value on top, so one
/// component covers both instead of two.
///
/// The values are also drivable from script, so the fight phases can push the sheen and
/// the backlight harder as the music gets faster.
///
/// ROUND 81 - WET LOOK. XFur's wet fur (the VFX module's rain, vfxMap.b in the shader) is
/// four constants baked into XFurStudio_StandardPasses.cginc: wet shells colour x0.55,
/// metallic 0.5, rim light x0, shells pulled down by 0.35. Together they read as a short,
/// burnt, chrome coat. Those constants are now behind a switch, _YoruWetOverride, and the
/// sliders below replace them while this component is enabled. With the component off or
/// absent the global reads 0 and XFur renders exactly as shipped, in this scene and in
/// every other one. A fifth term is new: strands thin out toward the tips inside a low
/// frequency noise so wet fur groups into pointed clumps instead of a smooth sheet.
/// Rain Roughness, Penetration, the mask and Fade Time stay on the XFur Studio Instance,
/// VFX and Weather module, Rain FX.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public class YoruFurLighting : MonoBehaviour
{
    [Header("=== KEY LIGHT ===")]
    [Tooltip("The directional light the fur takes its sheen and its backlight from. " +
             "Leave this empty and it uses the scene's Sun Source, set in " +
             "Window > Rendering > Lighting > Environment > Sun Source. In this scene " +
             "that is already the object named Directional Light, so empty is correct.")]
    [SerializeField] private Light keyLight;

    [Header("=== FUR KEY DIRECTION ===")]
    [Tooltip("Optional, and this is the fix for the top light. Leave it empty and the " +
             "fur is aimed by the key light above, which in this scene points down at 55 " +
             "degrees. A light that steep gives a rounded shape almost no form, which is " +
             "why the sheen and the backlight sit on top of her head instead of raking " +
             "across her. Make an empty GameObject, rotate it low and behind her so it " +
             "points back towards the camera, around X 18, and drop it here. Only the " +
             "fur reads it. It carries no Light component and it changes nothing else " +
             "in the cave.")]
    [SerializeField] private Transform furKeyDirection;

    [Header("=== FUR SHEEN ===")]
    [Tooltip("Multiplies the key light before XFur uses it for the anisotropic strand " +
             "specular. 1 is the light exactly as it is, which is what XFur intends. " +
             "Raise it to make the strands catch more light along their length. Go past " +
             "about 1.4 and the sheen is added into albedo faster than albedo can hold, " +
             "so she clips to white on the lit side and goes flat again. Start at 1.")]
    [Range(0f, 4f)]
    [SerializeField] private float sheenBoost = 1f;

    [Header("=== BACKLIGHT ===")]
    [Tooltip("Warm light bleeding through the fur on the side turned away from the key " +
             "light. This is the Ori rim. It only appears on the silhouette and only on " +
             "the shadowed side, so it separates her from the rock without flattening " +
             "her. 0 is XFur's broken default. Start at 1.2.")]
    [Range(0f, 4f)]
    [SerializeField] private float transmission = 1.2f;

    [Header("=== WET LOOK (rain on the fur, round 81) ===")]
    [Tooltip("How much of the colour a soaked shell keeps. XFur's fixed value is 0.55, " +
             "which is the burnt look. 0.75 is a wet cat: clearly darker, not dirty. " +
             "Only the outer shells darken (Penetration on the Rain FX decides how deep), " +
             "so the roots stay light and the depth survives.")]
    [Range(0.3f, 1f)]
    [SerializeField] private float wetDarken = 0.75f;

    [Tooltip("Colour the wet darkening is tinted with. White is a neutral darkening. A " +
             "slightly warm grey pulls the moonlit blue-white coat toward the dirty " +
             "yellowish white of a real wet cat.")]
    [SerializeField] private Color wetTint = new Color(1f, 0.94f, 0.84f, 1f);

    [Tooltip("Metallic of a soaked shell. XFur's fixed value is 0.5, which halves the " +
             "diffuse light and turns the sheen into chrome. 0.3 keeps a wet gloss along " +
             "the strands, 0.1 is nearly matte. Highlight tightness lives on the Rain FX as " +
             "Roughness: lower Roughness, tighter and more directional highlights.")]
    [Range(0f, 1f)]
    [SerializeField] private float wetMetallic = 0.3f;

    [Tooltip("How much of the rim light a soaked shell keeps. XFur kills it completely " +
             "(0), which is why the wet silhouette went flat. 0.4 keeps her readable " +
             "against the rock while wet still reads darker than dry.")]
    [Range(0f, 1f)]
    [SerializeField] private float wetRim = 0.4f;

    [Tooltip("Downward pull on soaked shells. XFur's fixed value is 0.35. 0 keeps the " +
             "dry length, 0.6 flattens the coat to the skin. 0.12 bends the tips down " +
             "and slims the silhouette while the clumps still stand out as spikes.")]
    [Range(0f, 0.6f)]
    [SerializeField] private float wetSag = 0.12f;

    [Tooltip("Clumping. Wet strands thin out toward the tips wherever a low frequency " +
             "noise is low, so the coat separates into pointed groups. 0 is XFur's " +
             "smooth sheet, 0.4 is barely visible at gameplay distance, 0.85 reads as a " +
             "wet cat with bald valleys between the clumps.")]
    [Range(0f, 1f)]
    [SerializeField] private float wetClump = 0.85f;

    [Tooltip("Size of the clumps, as a tiling of the noise over the UV space. Lower is " +
             "bigger clumps. The tails have a coarser UV layout than the body, so at the " +
             "same value their clumps come out about three times larger; judge on the body.")]
    [Range(4f, 128f)]
    [SerializeField] private float wetClumpScale = 24f;

    private static readonly int DirId = Shader.PropertyToID("_XFurMainStandardLightDir");
    private static readonly int ColId = Shader.PropertyToID("_XFurMainStandardLightColor");
    private static readonly int TransId = Shader.PropertyToID("_XFurSelfTransmission");

    private static readonly int WetOverrideId = Shader.PropertyToID("_YoruWetOverride");
    private static readonly int WetDarkenId = Shader.PropertyToID("_YoruWetDarken");
    private static readonly int WetTintId = Shader.PropertyToID("_YoruWetTint");
    private static readonly int WetMetallicId = Shader.PropertyToID("_YoruWetMetallic");
    private static readonly int WetRimId = Shader.PropertyToID("_YoruWetRim");
    private static readonly int WetSagId = Shader.PropertyToID("_YoruWetSag");
    private static readonly int WetClumpId = Shader.PropertyToID("_YoruWetClump");
    private static readonly int WetClumpScaleId = Shader.PropertyToID("_YoruWetClumpScale");

    private Light Key
    {
        get { return keyLight != null ? keyLight : RenderSettings.sun; }
    }

    private void OnEnable()
    {
        Apply();
    }

    private void Update()
    {
        Apply();
    }

    private void Apply()
    {
        ApplyWetLook();

        Light key = Key;

        // A missing or switched off key light means no sheen and no backlight, the same
        // way XFur's own manager handles it. Without this the fur would keep a sheen
        // from a light that is not shining.
        if (key == null || !key.isActiveAndEnabled)
        {
            Shader.SetGlobalVector(DirId, Vector3.forward);
            Shader.SetGlobalColor(ColId, Color.black);
            Shader.SetGlobalFloat(TransId, 0f);
            return;
        }

        // forward is the direction the light travels, from the light towards the surface.
        // The shader negates it for NdotL and uses it unnegated for the backlight gate,
        // so it wants exactly this, not the direction towards the light. That second use
        // is why the aim matters: the backlight only appears where the surface faces away
        // from this direction, so the glow lands on the camera side only when the aim
        // points from behind her towards the camera.
        Vector3 dir = furKeyDirection != null
            ? furKeyDirection.forward
            : key.transform.forward;

        Shader.SetGlobalVector(DirId, dir);
        Shader.SetGlobalColor(ColId, key.color * key.intensity * sheenBoost);
        Shader.SetGlobalFloat(TransId, transmission);
    }

    /// <summary>
    /// Pushes the wet look sliders into the shell shader and switches the override on.
    /// Runs every frame (edit mode too, the class is ExecuteAlways) so the sliders are
    /// live while tuning in Play.
    /// </summary>
    private void ApplyWetLook()
    {
        Shader.SetGlobalFloat(WetOverrideId, 1f);
        Shader.SetGlobalFloat(WetDarkenId, wetDarken);
        Shader.SetGlobalColor(WetTintId, wetTint);
        Shader.SetGlobalFloat(WetMetallicId, wetMetallic);
        Shader.SetGlobalFloat(WetRimId, wetRim);
        Shader.SetGlobalFloat(WetSagId, wetSag);
        Shader.SetGlobalFloat(WetClumpId, wetClump);
        Shader.SetGlobalFloat(WetClumpScaleId, wetClumpScale);
    }

    private void OnDisable()
    {
        // Leave the direction and colour alone so anything else reading them is not
        // surprised, but drop the backlight, since nothing else in the project sets it.
        Shader.SetGlobalFloat(TransId, 0f);

        // Back to XFur's shipped wet constants when this component is not running.
        Shader.SetGlobalFloat(WetOverrideId, 0f);
    }

    /// <summary>
    /// For the fight phases. Higher sheen reads as more energy in the fur.
    /// </summary>
    public void SetSheen(float value)
    {
        sheenBoost = Mathf.Clamp(value, 0f, 4f);
    }

    /// <summary>
    /// For the fight phases. Higher backlight reads as a hotter, more separated silhouette.
    /// </summary>
    public void SetTransmission(float value)
    {
        transmission = Mathf.Clamp(value, 0f, 4f);
    }
}
