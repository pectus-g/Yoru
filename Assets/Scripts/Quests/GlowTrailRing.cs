using UnityEngine;

/// <summary>
/// Marks one ring of a GlowTrail. Add this to each point object under a GlowTrail
/// (or use the GlowTrail component's right-click menu "Setup Rings From Children"
/// to add and number them all in one click).
///
/// The ring's place in the sequence comes from Order, NOT from hierarchy position
/// and NOT from creation time, so points can be added, deleted, or reordered freely.
/// The ring's size comes from Size, NOT from the transform's scale, so resizing can
/// never move the ring. The transform is used for POSITION only.
/// </summary>
public class GlowTrailRing : MonoBehaviour
{
    [Tooltip("Place in the sequence. Lower = earlier. Leave gaps (10, 20, 30...) so a ring can be inserted later (e.g. 15 goes between 10 and 20). The HIGHEST order ring is the arrival")]
    public int order = 0;

    [Tooltip("Size multiplier for this ring only. 1 = the effect prefab's normal size, 2 = double, 0.5 = half. Live, tweakable during Play")]
    public float size = 1f;
}
