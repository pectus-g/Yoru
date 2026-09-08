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

    private static readonly int DirId = Shader.PropertyToID("_XFurMainStandardLightDir");
    private static readonly int ColId = Shader.PropertyToID("_XFurMainStandardLightColor");
    private static readonly int TransId = Shader.PropertyToID("_XFurSelfTransmission");

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

    private void OnDisable()
    {
        // Leave the direction and colour alone so anything else reading them is not
        // surprised, but drop the backlight, since nothing else in the project sets it.
        Shader.SetGlobalFloat(TransId, 0f);
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
