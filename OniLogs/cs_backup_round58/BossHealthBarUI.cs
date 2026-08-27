using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// YORU Boss Health Bar — Zelda-style screen-top bar for Tier 1-2-3 enemies.
/// 
/// Big glowing health bar pinned to the top center of the screen, with the enemy name.
/// Separate from the small overhead EnemyHealthBar that Tier 4 minions (Kodama) use.
/// Only one boss bar is visible at a time (the current threat).
/// 
/// Look (YORU), glow-based, no art assets:
///   - Rounded corners, a soft glassy sheen on the fill, and a glowing halo behind the bar.
///   - Phase 1: glowing blue fill. Phase 2 (call SetPhase2): fill and glow shift to crimson,
///     a bright flash fires on the switch, and the glow pulses faster with a shimmer.
///   - Damage: HP drops instantly, the lost chunk reads as a hot trail that fades as it catches up.
///   - Optional red flash over the bar as it fades out on death (redFlashOnDeath).
/// 
/// Features:
///   - Auto-creates UI on the existing screen-space Canvas
///   - Name title above the bar, with its own glow
///   - Two-layer chunk effect (instant drop + trailing catch-up)
///   - Fades in on combat start, fades out on death or disengage
///   - Singleton — call Show(enemyHealth) from EnemyCombat.SetState
/// 
/// SETUP:
///   1. The BossHealthBar object already lives on HUDCanvas. This component sits on it.
///   2. EnemyCombat drives it: Show on Alert, SetPhase2 at the phase threshold, Hide on death.
///   3. Set the boss's display name in EnemyCombat (Boss Bar Name) to switch it on for that enemy.
///   4. That's it — the bar builds itself at runtime.
/// </summary>
public class BossHealthBarUI : MonoBehaviour
{
    #region Singleton
    public static BossHealthBarUI Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }
    #endregion

    #region Serialized Fields
    [Header("Bar Dimensions")]
    [Tooltip("Bar width in pixels")]
    [SerializeField] private float barWidth = 620f;
    [Tooltip("Bar height in pixels")]
    [SerializeField] private float barHeight = 30f;
    [Tooltip("Distance from top of screen in pixels")]
    [SerializeField] private float topMargin = 60f;

    [Header("Title")]
    [Tooltip("Enemy-name font size")]
    [SerializeField] private float titleFontSize = 36f;
    [Tooltip("Optional TMP font for the title. Leave empty to use the project default TMP font.")]
    [SerializeField] private TMP_FontAsset titleFont;
    [Tooltip("Main colour of the enemy-name title")]
    [SerializeField] private Color titleColor = new Color(0.92f, 0.95f, 1f, 1f);
    [Tooltip("Soft glow drawn behind the title (a slightly larger copy). Alpha used as-is.")]
    [SerializeField] private Color titleGlowColor = new Color(0.30f, 0.55f, 1f, 0.55f);

    [Header("Chunk Effect")]
    [Tooltip("How fast the lost-health trail catches up to the new HP")]
    [SerializeField] private float trailSpeed = 1.5f;

    [Header("Phase 1 Colors (blue)")]
    [Tooltip("Phase 1 health fill")]
    [SerializeField] private Color healthColorP1 = new Color(0.22f, 0.52f, 1f, 1f);
    [Tooltip("Phase 1 glow halo (RGB; alpha driven by pulse settings)")]
    [SerializeField] private Color glowColorP1 = new Color(0.25f, 0.55f, 1f, 1f);
    [Tooltip("Phase 1 lost-health chunk colour")]
    [SerializeField] private Color trailColorP1 = new Color(0.95f, 0.30f, 0.20f, 0.95f);

    [Header("Phase 2 Colors (crimson)")]
    [Tooltip("Phase 2 health fill")]
    [SerializeField] private Color healthColorP2 = new Color(0.95f, 0.18f, 0.22f, 1f);
    [Tooltip("Phase 2 glow halo (RGB; alpha driven by pulse settings)")]
    [SerializeField] private Color glowColorP2 = new Color(1f, 0.25f, 0.20f, 1f);
    [Tooltip("Phase 2 lost-health chunk colour (contrasts the crimson fill)")]
    [SerializeField] private Color trailColorP2 = new Color(1f, 0.85f, 0.35f, 0.95f);
    [Tooltip("Phase 2 glow pulse speed (faster than phase 1)")]
    [SerializeField] private float phase2PulseSpeed = 4.5f;
    [Tooltip("Phase 2 shimmer speed layered on top of the pulse")]
    [SerializeField] private float phase2ShimmerSpeed = 11f;
    [Tooltip("Phase 2 shimmer strength")]
    [SerializeField] private float phase2ShimmerAmount = 0.10f;

    [Header("Phase Transition Flash")]
    [Tooltip("Bright flash that fires the moment phase 2 begins")]
    [SerializeField] private Color phaseFlashColor = new Color(1f, 1f, 1f, 0.9f);
    [Tooltip("How long the phase-2 flash takes to fade")]
    [SerializeField] private float phaseFlashDuration = 0.45f;

    [Header("Glow")]
    [Tooltip("How far the glow halo extends past the bar, in pixels")]
    [SerializeField] private float glowPadding = 30f;
    [Tooltip("Base glow opacity")]
    [SerializeField] private float glowBaseAlpha = 0.6f;
    [Tooltip("Phase 1 glow pulse speed")]
    [SerializeField] private float glowPulseSpeed = 2f;
    [Tooltip("How much the pulse adds to / removes from the base opacity")]
    [SerializeField] private float glowPulseAmount = 0.08f;
    [Tooltip("Extra glow opacity added as HP approaches zero")]
    [SerializeField] private float glowLowHealthBoost = 0.18f;

    [Header("Fade")]
    [SerializeField] private float fadeInDuration = 0.3f;
    [SerializeField] private float fadeOutDuration = 1.5f;
    [Tooltip("Seconds after enemy dies before bar fades out")]
    [SerializeField] private float deathFadeDelay = 2f;

    [Header("Frame")]
    [Tooltip("Dark bar background")]
    [SerializeField] private Color backgroundColor = new Color(0.05f, 0.07f, 0.16f, 0.9f);
    [Tooltip("Frame around the bar")]
    [SerializeField] private Color borderColor = new Color(0.55f, 0.72f, 1f, 0.7f);
    [Tooltip("Corner roundness in pixels")]
    [SerializeField] private float cornerRadius = 9f;
    [Tooltip("Border (frame) thickness in pixels")]
    [SerializeField] private float borderThickness = 2.5f;
    [Tooltip("Strength of the glassy highlight sheen on the fill (0 = off)")]
    [SerializeField] private float sheenStrength = 0.30f;

    [Header("Damage Flash")]
    [Tooltip("Colour the glow flares toward when HP drops, then settles back")]
    [SerializeField] private Color damageGlowColor = new Color(1f, 0.35f, 0.20f, 1f);
    [Tooltip("How long the damage glow flare takes to settle after a hit")]
    [SerializeField] private float damageFlashDuration = 0.6f;

    [Header("Death Flash")]
    [Tooltip("Flash the bar red as it fades out on death")]
    [SerializeField] private bool redFlashOnDeath = true;
    [Tooltip("Colour of the death flash over the bar")]
    [SerializeField] private Color deathFlashColor = new Color(0.90f, 0.12f, 0.10f, 0.9f);
    [Tooltip("How fast the death flash pulses")]
    [SerializeField] private float deathFlashSpeed = 8f;

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;
    #endregion

    #region Private Fields
    // UI elements (created at runtime)
    private GameObject barRoot;
    private CanvasGroup canvasGroup;
    private TMP_Text nameLabel;
    private TMP_Text nameGlowLabel;
    private RectTransform healthRect;
    private RectTransform trailRect;
    private Image glowImage;
    private Image backgroundImage;
    private Image healthImage;
    private Image trailImage;
    private Image flashImage;

    // State
    private EnemyHealth trackedEnemy;
    private int lastKnownHP;
    private int maxHP;
    private float healthFill = 1f;
    private float trailFill = 1f;
    private float currentAlpha = 0f;
    private float targetAlpha = 0f;
    private bool isShowing = false;
    private float deathTimer = 0f;
    private bool enemyDead = false;
    private float damageFlashT = 0f; // 1 right after a hit, decays to 0
    private float phaseFlashT = 0f;  // 1 the moment phase 2 starts, decays to 0
    private int phase = 1;
    #endregion

    #region Unity Lifecycle
    private void Start()
    {
        CreateUI();
        canvasGroup.alpha = 0f;
        DebugLog("BossHealthBarUI initialized");
    }

    private void LateUpdate()
    {
        // Track enemy HP changes
        if (trackedEnemy != null && isShowing)
        {
            int currentHP = trackedEnemy.CurrentHealth;
            if (currentHP != lastKnownHP)
            {
                OnHealthChanged(currentHP);
                lastKnownHP = currentHP;
            }
        }

        // Trail catch-up animation (the lost chunk shrinking back to current HP)
        if (trailFill > healthFill)
        {
            trailFill = Mathf.MoveTowards(trailFill, healthFill, trailSpeed * Time.deltaTime);
            SetBarFill(trailRect, trailFill);
        }

        // Flares decay back toward rest
        if (damageFlashT > 0f)
            damageFlashT = Mathf.MoveTowards(damageFlashT, 0f, Time.deltaTime / Mathf.Max(0.01f, damageFlashDuration));
        if (phaseFlashT > 0f)
            phaseFlashT = Mathf.MoveTowards(phaseFlashT, 0f, Time.deltaTime / Mathf.Max(0.01f, phaseFlashDuration));

        // Death fade delay
        if (enemyDead && isShowing)
        {
            deathTimer += Time.deltaTime;
            if (deathTimer >= deathFadeDelay)
            {
                Hide("enemy dead + delay elapsed");
            }
        }

        UpdateGlow();
        UpdateFlashOverlay();
        UpdateDeathFlash();

        // Smooth alpha fade
        float fadeSpeed = targetAlpha > currentAlpha
            ? 1f / fadeInDuration
            : 1f / fadeOutDuration;
        currentAlpha = Mathf.MoveTowards(currentAlpha, targetAlpha, fadeSpeed * Time.deltaTime);
        canvasGroup.alpha = currentAlpha;
    }
    #endregion

    #region Public API

    /// <summary>
    /// Show the boss bar for a specific enemy. Call from EnemyCombat when a boss enters Alert.
    /// Resets to phase 1; if the enemy is already in phase 2, call SetPhase2 right after.
    /// </summary>
    public void Show(EnemyHealth enemy, string enemyName)
    {
        if (enemy == null) return;

        // Already tracking this enemy
        if (trackedEnemy == enemy && isShowing) return;

        trackedEnemy = enemy;
        maxHP = enemy.MaxHealth;
        lastKnownHP = enemy.CurrentHealth;
        healthFill = maxHP > 0 ? (float)lastKnownHP / maxHP : 0f;
        trailFill = healthFill;
        enemyDead = false;
        deathTimer = 0f;
        damageFlashT = 0f;
        phaseFlashT = 0f;
        phase = 1;
        ApplyPhaseColors();

        // Update UI
        if (nameLabel != null) nameLabel.text = enemyName;
        if (nameGlowLabel != null) nameGlowLabel.text = enemyName;
        SetBarFill(healthRect, healthFill);
        SetBarFill(trailRect, trailFill);
        if (backgroundImage != null) backgroundImage.color = backgroundColor;

        // Fade in
        isShowing = true;
        targetAlpha = 1f;

        DebugLog($"Showing bar for: {enemyName} ({lastKnownHP}/{maxHP})");
    }

    /// <summary>
    /// Switch the bar to its phase-2 look: crimson fill and glow, a bright transition flash,
    /// and a faster shimmering pulse. Call from EnemyCombat when the enemy enters phase 2.
    /// </summary>
    public void SetPhase2()
    {
        if (phase == 2) return;
        phase = 2;
        phaseFlashT = 1f;
        ApplyPhaseColors();
        DebugLog("Bar switched to phase 2");
    }

    /// <summary>
    /// Hide the boss bar. Called on death or disengage, or manually.
    /// </summary>
    public void Hide(string reason = "")
    {
        isShowing = false;
        targetAlpha = 0f;
        DebugLog($"Hiding bar{(reason != "" ? $" ({reason})" : "")}");
    }

    /// <summary>
    /// Notify that the tracked enemy has died. Starts the fade-out delay.
    /// </summary>
    public void NotifyEnemyDead(EnemyHealth enemy)
    {
        if (enemy != trackedEnemy) return;
        enemyDead = true;
        deathTimer = 0f;
        DebugLog("Tracked enemy died — bar will fade");
    }
    #endregion

    #region Health Tracking
    private void OnHealthChanged(int newHP)
    {
        // Health drops instantly
        healthFill = maxHP > 0 ? (float)newHP / maxHP : 0f;
        SetBarFill(healthRect, healthFill);
        // Trail catches up smoothly in LateUpdate, showing the lost chunk

        // Flare the glow on the hit, then it settles back
        damageFlashT = 1f;

        if (newHP <= 0)
        {
            enemyDead = true;
            deathTimer = 0f;
        }
    }
    #endregion

    #region Phase + Glow + Flash
    /// <summary>Active fill/trail/glow colours for the current phase.</summary>
    private Color ActiveHealthColor => phase == 2 ? healthColorP2 : healthColorP1;
    private Color ActiveTrailColor => phase == 2 ? trailColorP2 : trailColorP1;
    private Color ActiveGlowColor => phase == 2 ? glowColorP2 : glowColorP1;

    private void ApplyPhaseColors()
    {
        if (healthImage != null) healthImage.color = ActiveHealthColor;
        if (trailImage != null) trailImage.color = ActiveTrailColor;
        // glow is recoloured every frame in UpdateGlow
    }

    private void UpdateGlow()
    {
        if (glowImage == null) return;

        // Active phase glow, flaring toward the damage colour right after a hit
        Color rgb = Color.Lerp(ActiveGlowColor, damageGlowColor, damageFlashT * 0.7f);

        float pulseSpeed = phase == 2 ? phase2PulseSpeed : glowPulseSpeed;
        float pulse = glowBaseAlpha + Mathf.Sin(Time.time * pulseSpeed) * glowPulseAmount;
        if (phase == 2)
            pulse += Mathf.Sin(Time.time * phase2ShimmerSpeed) * phase2ShimmerAmount;

        float lowBoost = Mathf.Lerp(glowLowHealthBoost, 0f, healthFill);
        float flareBoost = damageFlashT * 0.35f + phaseFlashT * 0.5f;

        rgb.a = Mathf.Clamp01(pulse + lowBoost + flareBoost);
        glowImage.color = rgb;
    }

    private void UpdateFlashOverlay()
    {
        if (flashImage == null) return;
        Color f = phaseFlashColor;
        f.a = phaseFlashColor.a * phaseFlashT; // fades out after phase 2 starts
        flashImage.color = f;
    }

    private void UpdateDeathFlash()
    {
        if (backgroundImage == null) return;
        if (!(enemyDead && redFlashOnDeath)) return;

        // Pulse the bar background red while it lingers, before the alpha fade carries it away
        float t = (Mathf.Sin(Time.time * deathFlashSpeed) * 0.5f) + 0.5f;
        backgroundImage.color = Color.Lerp(backgroundColor, deathFlashColor, t);
    }
    #endregion

    #region UI Construction
    private void CreateUI()
    {
        // Stretch this object to fill the Canvas so the bar lands at the true top of the
        // screen, regardless of how the BossHealthBar object was anchored in the scene.
        RectTransform selfRect = GetComponent<RectTransform>();
        if (selfRect != null)
        {
            selfRect.anchorMin = Vector2.zero;
            selfRect.anchorMax = Vector2.one;
            selfRect.offsetMin = Vector2.zero;
            selfRect.offsetMax = Vector2.zero;
        }

        // Root container — anchored to top center of parent Canvas
        barRoot = new GameObject("BossBar_Root");
        barRoot.transform.SetParent(transform, false);

        float titleBlock = titleFontSize + 14f;

        RectTransform rootRect = barRoot.AddComponent<RectTransform>();
        rootRect.anchorMin = new Vector2(0.5f, 1f); // Top center
        rootRect.anchorMax = new Vector2(0.5f, 1f);
        rootRect.pivot = new Vector2(0.5f, 1f);
        rootRect.anchoredPosition = new Vector2(0f, -topMargin);
        rootRect.sizeDelta = new Vector2(barWidth, barHeight + titleBlock); // Extra height for name

        canvasGroup = barRoot.AddComponent<CanvasGroup>();
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        // === NAME TITLE (glow copy first so it sits behind the main text) ===
        nameGlowLabel = CreateTitleLabel("EnemyName_Glow", barRoot.transform, titleBlock,
            titleFontSize * 1.08f, titleGlowColor, FontStyles.Bold | FontStyles.SmallCaps);
        nameLabel = CreateTitleLabel("EnemyName", barRoot.transform, titleBlock,
            titleFontSize, titleColor, FontStyles.Bold | FontStyles.SmallCaps);

        // === BAR CONTAINER ===
        GameObject barContainer = new GameObject("BarContainer");
        barContainer.transform.SetParent(barRoot.transform, false);

        RectTransform containerRect = barContainer.AddComponent<RectTransform>();
        containerRect.anchorMin = new Vector2(0f, 0f);
        containerRect.anchorMax = new Vector2(1f, 0f);
        containerRect.pivot = new Vector2(0.5f, 0f);
        containerRect.anchoredPosition = new Vector2(0f, 0f);
        containerRect.sizeDelta = new Vector2(0f, barHeight);

        // Rounded + sheen sprites, generated in code so no art asset is needed
        Sprite rounded = CreateRoundedSprite(cornerRadius);
        Sprite sheen = CreateSheenSprite();

        // Soft glow halo (behind everything, larger than the bar)
        GameObject glowObj = new GameObject("Glow");
        glowObj.transform.SetParent(barContainer.transform, false);
        RectTransform glowRect = glowObj.AddComponent<RectTransform>();
        glowRect.anchorMin = new Vector2(0.5f, 0.5f);
        glowRect.anchorMax = new Vector2(0.5f, 0.5f);
        glowRect.pivot = new Vector2(0.5f, 0.5f);
        glowRect.anchoredPosition = Vector2.zero;
        glowRect.sizeDelta = new Vector2(barWidth + glowPadding * 2f, barHeight + glowPadding * 2f);
        glowImage = glowObj.AddComponent<Image>();
        glowImage.sprite = CreateSoftSprite(64);
        glowImage.type = Image.Type.Simple;
        glowImage.raycastTarget = false;
        Color glowStart = ActiveGlowColor;
        glowStart.a = glowBaseAlpha;
        glowImage.color = glowStart;

        // Border rim (full-size rounded layer, the inner area sits on top inset by the thickness)
        CreateFilledImage("Border", barContainer.transform, borderColor, rounded);

        // Inner area, inset by the border thickness so the rim shows around it
        GameObject inner = new GameObject("Inner");
        inner.transform.SetParent(barContainer.transform, false);
        RectTransform innerRect = inner.AddComponent<RectTransform>();
        innerRect.anchorMin = Vector2.zero;
        innerRect.anchorMax = Vector2.one;
        innerRect.offsetMin = new Vector2(borderThickness, borderThickness);
        innerRect.offsetMax = new Vector2(-borderThickness, -borderThickness);

        // Background
        GameObject bgObj = CreateFilledImage("Background", inner.transform, backgroundColor, rounded);
        backgroundImage = bgObj.GetComponent<Image>();

        // Trail (lost chunk that catches up)
        GameObject trailObj = CreateFilledImage("Trail", inner.transform, ActiveTrailColor, rounded);
        trailRect = trailObj.GetComponent<RectTransform>();
        trailImage = trailObj.GetComponent<Image>();

        // Health fill (instant)
        GameObject healthObj = CreateFilledImage("Health", inner.transform, ActiveHealthColor, rounded);
        healthRect = healthObj.GetComponent<RectTransform>();
        healthImage = healthObj.GetComponent<Image>();

        // Glassy sheen over the fill (child of health so it matches the fill width)
        GameObject sheenObj = new GameObject("Sheen");
        sheenObj.transform.SetParent(healthObj.transform, false);
        RectTransform sheenRect = sheenObj.AddComponent<RectTransform>();
        sheenRect.anchorMin = Vector2.zero;
        sheenRect.anchorMax = Vector2.one;
        sheenRect.offsetMin = Vector2.zero;
        sheenRect.offsetMax = Vector2.zero;
        Image sheenImg = sheenObj.AddComponent<Image>();
        sheenImg.sprite = sheen;
        sheenImg.type = Image.Type.Simple;
        sheenImg.color = new Color(1f, 1f, 1f, sheenStrength);
        sheenImg.raycastTarget = false;

        // Phase-transition flash overlay (on top, transparent until phase 2 fires)
        GameObject flashObj = CreateFilledImage("PhaseFlash", barContainer.transform, new Color(1f, 1f, 1f, 0f), rounded);
        flashImage = flashObj.GetComponent<Image>();
    }

    private TMP_Text CreateTitleLabel(string name, Transform parent, float blockHeight,
        float fontSize, Color color, FontStyles style)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);

        RectTransform rect = obj.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, 0f);
        rect.sizeDelta = new Vector2(0f, blockHeight);

        TMP_Text label = obj.AddComponent<TextMeshProUGUI>();
        if (titleFont != null) label.font = titleFont;
        label.text = "Enemy Name";
        label.fontSize = fontSize;
        label.fontStyle = style;
        label.color = color;
        label.alignment = TextAlignmentOptions.Center;
        label.enableWordWrapping = false;
        label.raycastTarget = false;
        return label;
    }

    /// <summary>
    /// Creates a full-stretch Image filling its parent. If a sprite is given it renders Sliced
    /// so rounded corners stay crisp at any width.
    /// </summary>
    private GameObject CreateFilledImage(string name, Transform parent, Color color, Sprite sprite)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);

        RectTransform rect = obj.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Image img = obj.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        if (sprite != null)
        {
            img.sprite = sprite;
            img.type = Image.Type.Sliced;
        }

        return obj;
    }

    /// <summary>
    /// Builds a soft-edged sprite in code so the glow halo reads as a blur with no art asset.
    /// </summary>
    private Sprite CreateSoftSprite(int size)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;

        float center = (size - 1) * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = Mathf.Abs(x - center) / center;
                float dy = Mathf.Abs(y - center) / center;
                float d = Mathf.Max(dx, dy);          // box distance, 0 center to 1 edge
                float a = Mathf.Clamp01(1f - d);
                a = a * a * (3f - 2f * a);             // smoothstep falloff
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }

    /// <summary>
    /// Builds a rounded-rectangle sprite with a 9-slice border so corners stay round at any size.
    /// </summary>
    private Sprite CreateRoundedSprite(float radius)
    {
        int r = Mathf.Max(1, Mathf.CeilToInt(radius));
        int size = r * 2 + 4;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float nx = Mathf.Clamp(x, r, size - 1 - r);
                float ny = Mathf.Clamp(y, r, size - 1 - r);
                float dx = x - nx;
                float dy = y - ny;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01(r - d + 0.5f); // 1 inside, soft 1px edge at the corners
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f),
            100f, 0, SpriteMeshType.FullRect, new Vector4(r, r, r, r));
    }

    /// <summary>
    /// Builds a vertical white gradient (bright at the top, fading down) for the glassy sheen.
    /// </summary>
    private Sprite CreateSheenSprite()
    {
        int h = 32;
        Texture2D tex = new Texture2D(4, h, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;

        for (int y = 0; y < h; y++)
        {
            float t = (float)y / (h - 1);                  // 0 bottom, 1 top
            float a = Mathf.Clamp01((t - 0.45f) / 0.55f);  // 0 over the lower 45%, ramps to the top
            a = a * a;                                      // ease so the sheen sits near the top
            for (int x = 0; x < 4; x++)
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, 4, h), new Vector2(0.5f, 0.5f));
    }

    private void SetBarFill(RectTransform rect, float percent)
    {
        if (rect == null) return;
        percent = Mathf.Clamp01(percent);
        rect.anchorMax = new Vector2(percent, rect.anchorMax.y);
    }
    #endregion

    #region Debug
    private void DebugLog(string message)
    {
        if (showDebugLogs)
            Debug.Log($"[BossBar] {message}");
    }
    #endregion
}