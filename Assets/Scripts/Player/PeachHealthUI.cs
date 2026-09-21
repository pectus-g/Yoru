using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Yoru's peach row. Draws itself from sprites at runtime under this object, so the prefab holds
/// nothing but the sprites and the dials. PlayerHealth pushes the numbers in through SetHealth().
///
/// Reading the row: real peaches first, left to right, eaten from the RIGHT end (like Zelda's
/// hearts). Full, three quarter, half, quarter, then the pit stays on screen. Gold peaches sit
/// after the real ones, glow, are eaten first and vanish when gone. The row wraps after
/// Peaches Per Row.
///
/// Low health (only the last real peach left, no gold): the peach beats like a heart (two beats,
/// lub dub), a red glow flares behind it on every beat, and the screen edges bleed red in time.
/// The heartbeat SOUND is fired from here, on the beat itself, through CombatSFXManager (the clips
/// live in its Low Health Heartbeat slots), so sound and picture cannot drift apart.
///
/// Death (0 bites): the heart stops, the pit does not pulse, and the red bleed goes to full and
/// HOLDS. That red edge is what marks the killing hit on screen; the game over screen fades its
/// black in underneath it.
/// </summary>
public class PeachHealthUI : MonoBehaviour
{
    public enum Corner { TopLeft, TopRight, BottomLeft, BottomRight }

    [Header("Peach Sprites")]
    public Sprite peachFull;
    public Sprite peach3_4;
    public Sprite peachHalf;
    public Sprite peach1_4;
    [Tooltip("Shown where a real peach has been eaten completely. Stays on screen so the number of pits still says how many peaches she owns.")]
    public Sprite peachPit;

    [Header("Gold Sprites (temporary peaches from items)")]
    public Sprite goldFull;
    public Sprite gold3_4;
    public Sprite goldHalf;
    public Sprite gold1_4;

    [Header("Glow (halo drawn behind a gold peach, and tinted red behind the last peach)")]
    public Sprite glowFull;
    public Sprite glow3_4;
    public Sprite glowHalf;
    public Sprite glow1_4;

    [Header("Layout")]
    [Tooltip("Peach size in pixels at the canvas reference resolution (1920 x 1080). 64 is about 6 percent of the screen height.")]
    public float peachSize = 64f;
    [Tooltip("Space between two peaches in the row, in pixels.")]
    public float gap = 6f;
    [Tooltip("Which screen corner the row hangs from.")]
    public Corner corner = Corner.TopLeft;
    [Tooltip("Distance from that corner, in pixels (x = from the side, y = from the top or bottom).")]
    public Vector2 margin = new Vector2(40f, 40f);
    [Tooltip("Peaches on one row before the next row starts. 6 keeps the row clear of the Oni's ink stroke at the top centre.")]
    public int peachesPerRow = 6;
    [Tooltip("Space between two rows, in pixels.")]
    public float rowGap = 6f;

    [Header("Motion")]
    [Tooltip("The peach that just lost a bite pops to this scale, then settles.")]
    public float bitePunchScale = 1.3f;
    [Tooltip("The peach that just gained a bite pops to this scale, then settles.")]
    public float healPunchScale = 1.15f;
    [Tooltip("Seconds for a pop to settle back to normal size.")]
    public float punchSeconds = 0.2f;
    [Tooltip("Gold halo brightness at the bottom of its breath (0 to 1).")]
    [Range(0f, 1f)] public float glowBreathMin = 0.55f;
    [Tooltip("Gold halo brightness at the top of its breath (0 to 1).")]
    [Range(0f, 1f)] public float glowBreathMax = 1f;
    [Tooltip("Seconds for one full breath of the gold halo.")]
    public float glowBreathSeconds = 1.6f;

    [Header("Low Health (the last peach)")]
    [Tooltip("ON = heartbeat, red glow and screen bleed when only the last real peach is left and no gold remains.")]
    public bool lowHealthCue = true;
    [Tooltip("Low health starts when her real bites are at or below this. 4 = the last peach (whatever is left of it).")]
    public int lowHealthBites = 4;
    [Tooltip("Seconds per heartbeat (two beats: lub, dub). 1.0 = 60 beats a minute; lower = faster panic.")]
    public float heartbeatSeconds = 1f;
    [Tooltip("How big the peach gets at the top of a beat. 1.4 = 40 percent bigger.")]
    public float heartbeatScale = 1.4f;
    [Tooltip("Colour of the glow that flares behind the last peach on each beat.")]
    public Color lowGlowColor = new Color(1f, 0.16f, 0.12f, 1f);
    [Tooltip("Glow strength at the top of a beat (0 to 1).")]
    [Range(0f, 1f)] public float lowGlowMax = 1f;
    [Tooltip("Full screen sprite for the red bleed at the screen edges (transparent middle). Empty = no bleed.")]
    public Sprite lowVignette;
    [Tooltip("Colour of the bleed.")]
    public Color vignetteColor = new Color(0.85f, 0.05f, 0.08f, 1f);
    [Tooltip("Bleed strength between beats (0 to 1). The cave is dark, so this needs to be high to read.")]
    [Range(0f, 1f)] public float vignetteFloor = 0.35f;
    [Tooltip("Bleed strength at the top of a beat (0 to 1).")]
    [Range(0f, 1f)] public float vignetteMax = 0.85f;
    [Tooltip("Seconds for the bleed to fade in when low health starts and out when she heals past it.")]
    public float vignetteFade = 0.6f;

    private class Slot
    {
        public RectTransform rect;
        public Image glow;
        public Image peach;
        public int value = -1;     // quarters shown, 0..4
        public bool gold;
        public float punchFrom = 1f;
        public float punchT = 1f;  // 0 = just popped, 1 = settled
    }

    private RectTransform row;
    private Image vignette;
    private readonly List<Slot> slots = new List<Slot>();
    private int quarters, maxQuarters, goldQuarters;
    private bool built;
    private float lowBlend;        // 0 = normal, 1 = low health visuals fully in
    private bool lowLogged;
    private bool deadLogged;
    private float lastBeatPhase;   // where in the heartbeat the last frame was, to catch the lub and the dub once each

    /// <summary>The second beat sits here in the cycle, for the picture AND the sound.</summary>
    private const float DubPhase = 0.30f;

    private void Awake()
    {
        BuildRow();
    }

    private void Start()
    {
        PlayerHealth ph = FindFirstObjectByType<PlayerHealth>();
        if (ph != null) ph.RegisterUI(this);
        Debug.Log($"[PeachUI] row ready: {slots.Count} peaches, {peachSize:F0} px, {corner}, bleed sprite {(lowVignette != null ? "set" : "MISSING")}, PlayerHealth {(ph != null ? "found" : "NOT FOUND")}");
    }

    private void BuildRow()
    {
        if (row != null) return;

        // The bleed goes first so everything else draws over it.
        GameObject vg = new GameObject("LowHealthBleed", typeof(RectTransform), typeof(Image));
        vg.transform.SetParent(transform, false);
        RectTransform vr = vg.GetComponent<RectTransform>();
        vr.anchorMin = Vector2.zero; vr.anchorMax = Vector2.one;
        vr.offsetMin = Vector2.zero; vr.offsetMax = Vector2.zero;
        vignette = vg.GetComponent<Image>();
        vignette.raycastTarget = false;
        vignette.sprite = lowVignette;
        vignette.color = new Color(vignetteColor.r, vignetteColor.g, vignetteColor.b, 0f);
        vignette.enabled = false;

        GameObject go = new GameObject("PeachRow", typeof(RectTransform));
        row = go.GetComponent<RectTransform>();
        row.SetParent(transform, false);
        Vector2 a = CornerAnchor();
        row.anchorMin = a; row.anchorMax = a; row.pivot = a;
        row.anchoredPosition = Vector2.zero;
        row.sizeDelta = Vector2.zero;
        built = true;
    }

    private Vector2 CornerAnchor()
    {
        switch (corner)
        {
            case Corner.TopRight: return new Vector2(1f, 1f);
            case Corner.BottomLeft: return new Vector2(0f, 0f);
            case Corner.BottomRight: return new Vector2(1f, 0f);
            default: return new Vector2(0f, 1f);
        }
    }

    /// <summary>Called by PlayerHealth on every change. deltaQuarters < 0 = bites lost, > 0 = gained, 0 = just a refresh.</summary>
    public void SetHealth(int quarters, int maxQuarters, int goldQuarters, int deltaQuarters)
    {
        if (!built) BuildRow();
        this.quarters = Mathf.Max(0, quarters);
        this.maxQuarters = Mathf.Max(0, maxQuarters);
        this.goldQuarters = Mathf.Max(0, goldQuarters);

        int realPeaches = Mathf.CeilToInt(this.maxQuarters / 4f);
        int goldPeaches = Mathf.CeilToInt(this.goldQuarters / 4f);
        EnsureSlots(realPeaches + goldPeaches);

        for (int i = 0; i < slots.Count; i++)
        {
            Slot s = slots[i];
            bool isGold = i >= realPeaches;
            int value = isGold ? Mathf.Clamp(this.goldQuarters - 4 * (i - realPeaches), 0, 4)
                               : Mathf.Clamp(this.quarters - 4 * i, 0, 4);
            bool changed = s.value != value || s.gold != isGold;
            s.gold = isGold;
            s.value = value;
            s.peach.sprite = isGold ? GoldSprite(value) : RealSprite(value);
            s.peach.enabled = s.peach.sprite != null;
            if (isGold)
            {
                s.glow.sprite = GlowSprite(value);
                s.glow.enabled = s.glow.sprite != null;
                s.glow.color = Color.white;
            }
            else if (i != 0)
            {
                s.glow.enabled = false;   // slot 0 is handled every frame by the low health cue
            }
            if (changed && deltaQuarters != 0 && s.value >= 0)
            {
                s.punchFrom = deltaQuarters < 0 ? bitePunchScale : healPunchScale;
                s.punchT = 0f;
            }
            Place(s, i);
        }
    }

    private void EnsureSlots(int count)
    {
        while (slots.Count < count)
        {
            Slot s = new Slot();
            GameObject go = new GameObject($"Peach_{slots.Count}", typeof(RectTransform));
            s.rect = go.GetComponent<RectTransform>();
            s.rect.SetParent(row, false);

            GameObject glowGo = new GameObject("Glow", typeof(RectTransform), typeof(Image));
            glowGo.transform.SetParent(go.transform, false);
            s.glow = glowGo.GetComponent<Image>();
            s.glow.raycastTarget = false;
            s.glow.enabled = false;
            Stretch(glowGo.GetComponent<RectTransform>());

            GameObject peachGo = new GameObject("Peach", typeof(RectTransform), typeof(Image));
            peachGo.transform.SetParent(go.transform, false);
            s.peach = peachGo.GetComponent<Image>();
            s.peach.raycastTarget = false;
            s.peach.preserveAspect = true;
            Stretch(peachGo.GetComponent<RectTransform>());

            slots.Add(s);
        }
        while (slots.Count > count)
        {
            Slot s = slots[slots.Count - 1];
            slots.RemoveAt(slots.Count - 1);
            if (s.rect != null) Destroy(s.rect.gameObject);
        }
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }

    private void Place(Slot s, int index)
    {
        int perRow = Mathf.Max(1, peachesPerRow);
        int col = index % perRow;
        int rowIndex = index / perRow;
        Vector2 a = CornerAnchor();
        float sx = a.x > 0.5f ? -1f : 1f;   // right corners grow leftwards
        float sy = a.y > 0.5f ? -1f : 1f;   // top corners grow downwards
        float x = sx * (margin.x + col * (peachSize + gap));
        float y = sy * (margin.y + rowIndex * (peachSize + rowGap));
        s.rect.anchorMin = a; s.rect.anchorMax = a; s.rect.pivot = a;
        s.rect.sizeDelta = new Vector2(peachSize, peachSize);
        s.rect.anchoredPosition = new Vector2(x, y);
    }

    private bool IsLow()
    {
        return lowHealthCue && goldQuarters == 0 && quarters > 0 && quarters <= Mathf.Max(1, lowHealthBites);
    }

    /// <summary>Two beats per cycle: a strong one at the start, a softer one a quarter cycle later. 0 = rest, 1 = top of the first beat.</summary>
    private float Heartbeat(float t01)
    {
        float lub = Mathf.Exp(-Mathf.Pow(t01 / 0.12f, 2f));
        float dub = 0.7f * Mathf.Exp(-Mathf.Pow((t01 - DubPhase) / 0.12f, 2f));
        return Mathf.Clamp01(lub + dub);
    }

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;   // hitstop must not freeze the HUD
        bool low = IsLow();
        // Dead = she owns peaches and has no bite left (Cannot Die floors her at 1, so 0 is only ever death).
        bool dead = lowHealthCue && maxQuarters > 0 && quarters <= 0;
        bool lowChanged = low != lowLogged;
        if (lowChanged)
        {
            lowLogged = low;
            bool sound = CombatSFXManager.Instance != null && CombatSFXManager.Instance.HasHeartbeatClip;
            Debug.Log(low ? $"[PeachUI] LOW HEALTH cue ON: {quarters} bites left, heartbeat {heartbeatSeconds:F2}s, bleed {(lowVignette != null ? "on" : "no sprite")}, sound {(sound ? "set" : "EMPTY")}"
                          : $"[PeachUI] LOW HEALTH cue OFF{(dead ? " (she died: heartbeat stops, bleed holds at full)" : "")}");
        }
        if (dead != deadLogged)
        {
            deadLogged = dead;
            if (dead && !lowChanged) Debug.Log("[PeachUI] DEATH: heartbeat off, bleed held at full");   // she died without ever being low (one big hit)
        }
        // At death the red comes in fast (0.15 s): it is the mark of the killing hit, not a slow warning.
        float blendSeconds = dead ? Mathf.Min(vignetteFade, 0.15f) : vignetteFade;
        lowBlend = Mathf.MoveTowards(lowBlend, (low || dead) ? 1f : 0f, dt / Mathf.Max(0.05f, blendSeconds));

        float breath = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * Mathf.PI * 2f / Mathf.Max(0.05f, glowBreathSeconds));
        float goldAlpha = Mathf.Lerp(glowBreathMin, glowBreathMax, breath);
        float period = Mathf.Max(0.2f, heartbeatSeconds);
        float phase = (Time.unscaledTime % period) / period;
        float beat = dead ? 0f : Heartbeat(phase);          // the heart stops with her: no pulse on the pit
        float bleedBeat = dead ? 1f : beat;                 // the bleed goes to full at death and holds

        // The heartbeat sound, fired on the beat itself: lub when the cycle wraps, dub when it passes
        // Dub Phase. It fades in with the bleed (lowBlend) and stops the frame she heals or dies.
        if (low && CombatSFXManager.Instance != null)
        {
            if (phase < lastBeatPhase) CombatSFXManager.Instance.PlayHeartbeat(false, lowBlend);
            else if (lastBeatPhase < DubPhase && phase >= DubPhase) CombatSFXManager.Instance.PlayHeartbeat(true, lowBlend);
        }
        lastBeatPhase = phase;

        for (int i = 0; i < slots.Count; i++)
        {
            Slot s = slots[i];
            if (s.punchT < 1f)
                s.punchT = Mathf.Min(1f, s.punchT + dt / Mathf.Max(0.01f, punchSeconds));
            float ease = 1f - (1f - s.punchT) * (1f - s.punchT);          // fast out, soft landing
            float scale = Mathf.Lerp(s.punchFrom, 1f, ease);

            if (i == 0 && !s.gold)
            {
                // The last peach: heartbeat and red flare while low, nothing otherwise.
                if (lowBlend > 0.001f)
                {
                    scale *= 1f + (heartbeatScale - 1f) * beat * lowBlend;
                    Sprite g = GlowSprite(s.value);
                    s.glow.sprite = g;
                    s.glow.enabled = g != null;
                    Color c = lowGlowColor;
                    c.a = lowGlowMax * (0.25f + 0.75f * beat) * lowBlend;
                    s.glow.color = c;
                }
                else
                {
                    s.glow.enabled = false;
                }
            }
            else if (s.gold && s.glow.enabled)
            {
                Color c = Color.white; c.a = goldAlpha; s.glow.color = c;
            }

            // Scale the images, not the slot: the slot's pivot is the screen corner, the images pivot on their centre.
            Vector3 sc = new Vector3(scale, scale, 1f);
            s.peach.rectTransform.localScale = sc;
            s.glow.rectTransform.localScale = sc;
        }

        if (vignette != null)
        {
            bool show = lowVignette != null && lowBlend > 0.001f;
            vignette.enabled = show;
            if (show)
            {
                if (vignette.sprite != lowVignette) vignette.sprite = lowVignette;
                Color c = vignetteColor;
                c.a = Mathf.Lerp(vignetteFloor, vignetteMax, bleedBeat) * lowBlend;
                vignette.color = c;
            }
        }
    }

    private Sprite RealSprite(int v)
    {
        switch (v)
        {
            case 4: return peachFull;
            case 3: return peach3_4;
            case 2: return peachHalf;
            case 1: return peach1_4;
            default: return peachPit;
        }
    }

    private Sprite GoldSprite(int v)
    {
        switch (v)
        {
            case 4: return goldFull;
            case 3: return gold3_4;
            case 2: return goldHalf;
            case 1: return gold1_4;
            default: return null;
        }
    }

    private Sprite GlowSprite(int v)
    {
        switch (v)
        {
            case 4: return glowFull;
            case 3: return glow3_4;
            case 2: return glowHalf;
            case 1: return glow1_4;
            default: return null;
        }
    }

#if UNITY_EDITOR
    // Layout dials edited in Play Mode take effect at once.
    private void OnValidate()
    {
        if (!Application.isPlaying || row == null) return;
        Vector2 a = CornerAnchor();
        row.anchorMin = a; row.anchorMax = a; row.pivot = a;
        for (int i = 0; i < slots.Count; i++) Place(slots[i], i);
    }
#endif
}
