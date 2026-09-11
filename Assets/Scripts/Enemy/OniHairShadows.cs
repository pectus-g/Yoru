using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Stops the Oni's hair strands from casting shadows, so the bangs and the mane
/// no longer shadow his face from ONI_KEY. Runs once at Awake and touches nothing else.
/// Disable the component to get the hair shadows back.
/// </summary>
public class OniHairShadows : MonoBehaviour
{
    [Tooltip("Renderers whose shared material name starts with this stop casting shadows.")]
    [SerializeField] private string hairMaterialPrefix = "demon warior8_hair";

    private void Awake()
    {
        int count = 0;
        foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
        {
            Material m = r.sharedMaterial;
            if (m == null || !m.name.StartsWith(hairMaterialPrefix)) continue;
            r.shadowCastingMode = ShadowCastingMode.Off;
            count++;
        }
        Debug.Log($"[OniHairShadows] shadow casting off on {count} hair renderers");
    }
}
