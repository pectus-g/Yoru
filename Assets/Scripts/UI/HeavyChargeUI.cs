using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// YORU Heavy Charge UI — Phase 3D
/// Zelda BotW stamina-wheel style donut ring shown around Yoru while LMB is held.
/// Reads PlayerCombat.GetHeavyChargePercent() and visualises it as a radial-fill donut
/// with a soft halo glow that intensifies as the charge ramps up.
///
/// Matches the BossHealthBarUI pattern: the whole UI hierarchy + donut texture are
/// built in code at Start. No manual Canvas setup, no sprite assets, no Inspector
/// wiring beyond the optional tuning fields below.
///
/// SETUP:
///   1. Create an empty GameObject as a child of PlayerYoru_Def
///   2. Name it "HeavyChargeRing"
///   3. Position at chest height (try Local Y = 1.0)
///   4. Add this component
///   5. That's it — Canvas, CanvasGroup, all Images, and the donut texture all
///      build themselves at runtime. PlayerCombat is auto-found via FindObjectOfType.
///
/// COLOR PROGRESSION (mirrors the BossHealthBar trail/health two-tone idea):
///   0–100% : ring lerps from dim cyan → bright warm-cyan as charge fills (subtle)
///   100%   : warm gold-white glow halo pulses softly on top of the ring
///
/// This script does NOT modify combat state — it only reads PlayerCombat. Safe to
/// disable or remove without breaking anything.
/// </summary>
public class HeavyChargeUI : MonoBehaviour
{
    #region Serialized Fields
    [Header("Reference")]
    [Tooltip("PlayerCombat to read charge percent from. Auto-finds in scene if left empty.")]
    [SerializeField] private PlayerCombat playerCombat;

    [Header("Ring Shape")]
    [Tooltip("World-space scale of the canvas. Default 0.005 → ring reads as ~50cm across at default canvas size.")]
    [SerializeField] private float worldScale = 0.005f;
    [Tooltip("Canvas size in pixels. Higher = sharper donut at the cost of fillrate. 256 is plenty.")]
    [SerializeField] private int textureResolution = 256;
    [Tooltip("Outer radius of the donut as a fraction of the texture size. 0.48 fills most of the canvas.")]
    [Range(0.2f, 0.5f)]
    [SerializeField] private float ringOuterRadius = 0.48f;
    [Tooltip("Inner radius of the donut as a fraction of the texture size. Smaller value = thicker ring. 0.36 = chunky readable ring.")]
    [Range(0.05f, 0.45f)]
    [SerializeField] private float ringInnerRadius = 0.36f;

    [Header("Colors")]
    [Tooltip("Base ring color at 0% charge. Pale cyan default — replace with your palette color.")]
    [SerializeField] private Color ringColorLow = new Color(0.55f, 0.85f, 1.0f, 1.0f);
    [Tooltip("Ring color reached at full charge — warmer to signal payoff.")]
    [SerializeField] private Color ringColorHigh = new Color(0.85f, 0.95f, 1.0f, 1.0f);
    [Tooltip("Glow halo color at 100% charge (the ready-to-fire flash).")]
    [SerializeField] private Color glowFullColor = new Color(1.0f, 0.92f, 0.65f, 1.0f);
    [Tooltip("Background ring color (the empty track behind the fill). Alpha controls visibility.")]
    [SerializeField] private Color backgroundColor = new Color(0.2f, 0.3f, 0.4f, 0.35f);

    [Header("Glow")]
    [Tooltip("Peak alpha of the glow halo at 100% charge.")]
    [Range(0f, 1f)]
    [SerializeField] private float glowPeakAlpha = 0.85f;
    [Tooltip("Pulse speed of the glow once 100% is reached.")]
    [SerializeField] private float glowPulseSpeed = 4f;
    [Tooltip("How much larger the glow halo is than the fill ring (1.0 = same size, 1.2 = 20% bigger halo).")]
    [Range(1.0f, 1.5f)]
    [SerializeField] private float glowSizeMultiplier = 1.15f;

    [Header("Fade")]
    [SerializeField] private float fadeInDuration = 0.15f;
    [SerializeField] private float fadeOutDuration = 0.25f;

    [Header("Billboard")]
    [Tooltip("Face the main camera each LateUpdate so the ring is always readable.")]
    [SerializeField] private bool billboardToCamera = true;

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = false;
    #endregion

    #region Private Fields
    private Camera mainCamera;
    private Canvas canvas;
    private CanvasGroup canvasGroup;
    private Image fillImage;
    private Image glowImage;
    private Image backgroundImage;
    private Sprite ringSprite;
    private float currentAlpha;
    private bool hasFiredReadyFlash;
    #endregion

    #region Unity Lifecycle
    private void Start()
    {
        if (playerCombat == null)
            playerCombat = FindObjectOfType<PlayerCombat>();

        mainCamera = Camera.main;

        BuildUI();
        canvasGroup.alpha = 0f;
        SetGlowAlpha(0f);

        DebugLog("HeavyChargeUI initialized (procedural build complete)");
    }

    private void Update()
    {
        if (playerCombat == null || fillImage == null) return;

        bool isCharging = playerCombat.IsChargingHeavy();
        float percent = playerCombat.GetHeavyChargePercent();

        UpdateFade(isCharging);
        UpdateFill(isCharging, percent);
        UpdateRingColor(percent);
        UpdateGlow(isCharging, percent);
    }

    private void LateUpdate()
    {
        if (!billboardToCamera || mainCamera == null) return;
        if (currentAlpha <= 0.01f) return; // skip rotation when fully hidden

        Vector3 toCamera = mainCamera.transform.position - transform.position;
        if (toCamera.sqrMagnitude < 0.0001f) return;
        transform.rotation = Quaternion.LookRotation(-toCamera, Vector3.up);
    }
    #endregion

    #region Per-Frame Update Steps
    private void UpdateFade(bool isCharging)
    {
        float targetAlpha = isCharging ? 1f : 0f;
        float duration = targetAlpha > currentAlpha ? fadeInDuration : fadeOutDuration;
        float rate = duration > 0f ? 1f / duration : 999f;
        currentAlpha = Mathf.MoveTowards(currentAlpha, targetAlpha, rate * Time.deltaTime);
        canvasGroup.alpha = currentAlpha;
    }

    private void UpdateFill(bool isCharging, float percent)
    {
        if (isCharging)
            fillImage.fillAmount = percent;
        else if (currentAlpha <= 0.01f)
            fillImage.fillAmount = 0f; // reset to empty once hidden, ready for next charge
    }

    private void UpdateRingColor(float percent)
    {
        // Lerp the visible ring color from dim cyan → bright warm-cyan as charge fills.
        // Brightness ramp is intentionally subtle so the eye reads FILL AMOUNT as the
        // primary signal; color is the secondary cue. The big payoff cue is the glow.
        fillImage.color = Color.Lerp(ringColorLow, ringColorHigh, percent);
    }

    private void UpdateGlow(bool isCharging, float percent)
    {
        // Glow only matters at 100%. Soft pulse while held at full charge.
        float glowAlpha = 0f;

        if (isCharging && percent >= 1f)
        {
            // 0..1 sine pulse → lerp between half-peak and full-peak for a heartbeat feel
            float pulse = (Mathf.Sin(Time.time * glowPulseSpeed) + 1f) * 0.5f;
            glowAlpha = Mathf.Lerp(glowPeakAlpha * 0.5f, glowPeakAlpha, pulse);

            if (!hasFiredReadyFlash)
            {
                hasFiredReadyFlash = true;
                DebugLog("Charge ready — glow flash engaged");
            }
        }
        else
        {
            hasFiredReadyFlash = false;
        }

        // Multiplied by currentAlpha so the glow fades with the rest of the UI on cancel/release
        SetGlowAlpha(glowAlpha * currentAlpha);
    }

    private void SetGlowAlpha(float a)
    {
        if (glowImage == null) return;
        Color c = glowFullColor;
        c.a = Mathf.Clamp01(a);
        glowImage.color = c;
    }
    #endregion

    #region UI Construction
    /// <summary>
    /// Build the entire Canvas hierarchy and generate the donut sprite at runtime.
    /// Same pattern as BossHealthBarUI.CreateUI() — no scene setup needed beyond
    /// placing an empty GameObject with this script.
    /// </summary>
    private void BuildUI()
    {
        // === ROOT CANVAS ===
        GameObject canvasObj = new GameObject("Canvas_HeavyCharge");
        canvasObj.transform.SetParent(transform, false);
        canvasObj.transform.localPosition = Vector3.zero;
        canvasObj.transform.localRotation = Quaternion.identity;
        canvasObj.transform.localScale = Vector3.one * worldScale;

        canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvasObj.AddComponent<CanvasScaler>();
        canvasObj.AddComponent<GraphicRaycaster>(); // harmless, kept for consistency

        RectTransform canvasRect = canvas.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(textureResolution, textureResolution);

        canvasGroup = canvasObj.AddComponent<CanvasGroup>();
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        // === GENERATE DONUT SPRITE ===
        ringSprite = GenerateRingSprite(textureResolution, ringOuterRadius, ringInnerRadius);

        // === BACKGROUND RING (the empty track behind the fill) ===
        backgroundImage = CreateRingImage("BackgroundRing", canvasObj.transform, backgroundColor, false, 1f);

        // === FILL RING (the radial fill that grows as charge fills) ===
        fillImage = CreateRingImage("FillRing", canvasObj.transform, ringColorLow, true, 1f);

        // === GLOW HALO (slightly bigger, sits on top, alpha animated by UpdateGlow) ===
        glowImage = CreateRingImage("GlowHalo", canvasObj.transform, glowFullColor, false, glowSizeMultiplier);
    }

    private Image CreateRingImage(string name, Transform parent, Color color, bool filled, float sizeMultiplier)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);

        RectTransform rect = obj.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(textureResolution * sizeMultiplier, textureResolution * sizeMultiplier);

        Image img = obj.AddComponent<Image>();
        img.sprite = ringSprite;
        img.color = color;
        img.raycastTarget = false;

        if (filled)
        {
            img.type = Image.Type.Filled;
            img.fillMethod = Image.FillMethod.Radial360;
            img.fillOrigin = (int)Image.Origin360.Top;
            img.fillClockwise = true;
            img.fillAmount = 0f;
        }

        return img;
    }

    /// <summary>
    /// Generate an anti-aliased donut sprite at runtime using a Texture2D.
    /// outerR and innerR are radii as fractions of texture size (0..0.5).
    /// Avoids needing any sprite asset in the project.
    /// </summary>
    private Sprite GenerateRingSprite(int size, float outerR, float innerR)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;

        Color[] pixels = new Color[size * size];
        Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
        float outerPx = size * outerR;
        float innerPx = size * innerR;
        const float edgeSoftness = 1.5f; // pixels of anti-alias falloff per edge

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center);

                // Soft falloff on both the outer and inner boundary
                float outerAlpha = Mathf.SmoothStep(1f, 0f, (dist - (outerPx - edgeSoftness)) / edgeSoftness);
                float innerAlpha = Mathf.SmoothStep(0f, 1f, (dist - innerPx) / edgeSoftness);
                float ringAlpha = Mathf.Clamp01(Mathf.Min(outerAlpha, innerAlpha));

                pixels[y * size + x] = new Color(1f, 1f, 1f, ringAlpha);
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();

        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }
    #endregion

    #region Debug
    private void DebugLog(string message)
    {
        if (showDebugLogs)
            Debug.Log($"[HeavyChargeUI] {message}");
    }
    #endregion
}