using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// YORU Boss Health Bar. Sekiro-style ink bar for Tier 1-2-3 bosses.
///
/// Look: one thin brush stroke of ink laid across the screen. The track is the dry stroke in
/// near black, the health is the same stroke still wet in ember with a low glow under it, and the
/// lost chunk reads as ash burning back. A kanji seal glows at the head of the stroke and the
/// boss name sits under it in the same brush hand.
///
/// Two-stage fill: with Two Stage Fill on, phase 1 spends the whole stroke getting him down to the
/// phase threshold, the stroke empties, and phase 2 refills it in blood red. The phase marks under
/// the head of the stroke say up front how many stages there are, so an empty bar that refills
/// never reads as the game cheating. That is Sekiro's deathblow marks in ink.
///
/// Phase 2 does not change hue family. It gets hotter and glowier, the seal ignites, the name
/// glows, and the breathing speeds up. Escalation reads as tempo, not as a new colour.
///
/// Every stroke sprite is generated in code at the bar's real pixel size, so the ink grain never
/// stretches. The seal and its glow are sprites, so no font is needed for the kanji.
///
/// SETUP:
///   1. The BossHealthBar object lives on the HUD canvas. This component sits on it.
///   2. OniBoss drives it: Show on any hostile state, SetPhase2 at the phase beat, Hide on leash.
///   3. Set the boss's display name in OniBoss (Boss Bar Display Name).
///   4. Phase Split must match EnemyCombat's Phase Threshold, or the two stages will not line up.
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
    [Header("Layout")]
    [Tooltip("Stroke length in pixels at the canvas reference resolution. Sekiro and Elden Ring sit near 40 percent of the screen width, which is 760 at 1920.")]
    [SerializeField] private float barWidth = 760f;
    [Tooltip("Stroke thickness in pixels. Above about 16 it stops reading as ink.")]
    [SerializeField] private float barHeight = 13f;
    [Tooltip("Put the bar at the bottom of the screen (Sekiro) instead of the top (Nioh).")]
    [SerializeField] private bool anchorToBottom = false;
    [Tooltip("Distance from the screen edge the bar is anchored to, in pixels.")]
    [SerializeField] private float screenMargin = 62f;

    [Header("Name")]
    [Tooltip("Font for the boss name. Use the brush font, not the default sans.")]
    [SerializeField] private TMP_FontAsset nameFont;
    [Tooltip("Name size in pixels. Small on purpose: the seal carries the identity.")]
    [SerializeField] private float nameFontSize = 18f;
    [Tooltip("Extra spacing between letters. Wide tracking is what makes a small name read as a title.")]
    [SerializeField] private float nameLetterSpacing = 18f;
    [Tooltip("Draw the name in capitals whatever the boss sends.")]
    [SerializeField] private bool forceUppercase = true;
    [Tooltip("Name colour. Warm off white, never pure white.")]
    [SerializeField] private Color nameColor = new Color(0.89f, 0.84f, 0.77f, 0.95f);
    [Tooltip("Glow around the name in phase 1.")]
    [SerializeField] private Color nameGlowP1 = new Color(0.95f, 0.45f, 0.20f, 0.55f);
    [Tooltip("Glow around the name in phase 2.")]
    [SerializeField] private Color nameGlowP2 = new Color(1f, 0.22f, 0.14f, 0.95f);
    [Tooltip("How far the halo spreads around the letters in pixels, phase 1.")]
    [SerializeField] private float nameGlowRadiusP1 = 2.5f;
    [Tooltip("How far the halo spreads around the letters in pixels, phase 2.")]
    [SerializeField] private float nameGlowRadiusP2 = 4.5f;
    [Tooltip("Opacity of the outer ring as a fraction of the inner one. Lower is a tighter glow.")]
    [Range(0f, 1f)]
    [SerializeField] private float nameGlowFalloff = 0.45f;

    [Header("Name Heartbeat (phase 2 only)")]
    [Tooltip("Beat the name glow like a pulse once phase 2 starts. Phase 1 keeps the slow breath.")]
    [SerializeField] private bool nameHeartbeat = true;
    [Tooltip("Seconds per beat at the top of phase 2.")]
    [SerializeField] private float heartbeatPeriodFull = 1f;
    [Tooltip("Seconds per beat as he approaches death. Shorter means his heart is racing.")]
    [SerializeField] private float heartbeatPeriodLow = 0.45f;
    [Tooltip("Glow multiplier between beats. 1 rests exactly on the phase 2 level, below 1 dips under it.")]
    [SerializeField] private float heartbeatMin = 0.7f;
    [Tooltip("Glow multiplier at the top of the first thump.")]
    [SerializeField] private float heartbeatMax = 1.5f;

    [Header("Kanji Seal")]
    [Tooltip("The brush kanji stamped at the head of the stroke. Empty = no seal is drawn.")]
    [SerializeField] private Sprite sealSprite;
    [Tooltip("Blurred copy of the same kanji, drawn behind it as the glow. Empty = no seal glow.")]
    [SerializeField] private Sprite sealGlowSprite;
    [Tooltip("Seal size in pixels, square.")]
    [SerializeField] private float sealSize = 50f;
    [Tooltip("How far left of the stroke the seal sits, in pixels.")]
    [SerializeField] private float sealOffsetX = 32f;
    [Tooltip("Nudge the seal up or down against the stroke, in pixels.")]
    [SerializeField] private float sealOffsetY = 0f;
    [Tooltip("How much larger the glow is than the seal.")]
    [SerializeField] private float sealGlowScale = 2.1f;
    [Tooltip("Seal colour in phase 1.")]
    [SerializeField] private Color sealColorP1 = new Color(0.78f, 0.33f, 0.18f, 0.85f);
    [Tooltip("Seal colour once phase 2 begins.")]
    [SerializeField] private Color sealColorP2 = new Color(1f, 0.30f, 0.16f, 1f);
    [Tooltip("Seal glow colour and strength in phase 1.")]
    [SerializeField] private Color sealGlowColorP1 = new Color(0.95f, 0.40f, 0.18f, 0.30f);
    [Tooltip("Seal glow colour and strength in phase 2.")]
    [SerializeField] private Color sealGlowColorP2 = new Color(1f, 0.18f, 0.10f, 0.65f);

    [Header("Ink")]
    [Tooltip("The dry stroke behind the health.")]
    [SerializeField] private Color trackColor = new Color(0.055f, 0.045f, 0.04f, 0.92f);
    [Tooltip("Hairline under the stroke. Alpha low or it turns into a frame.")]
    [SerializeField] private Color edgeColor = new Color(0.34f, 0.27f, 0.22f, 0.45f);
    [Tooltip("How rough the ink reads, 0 = flat paint, 1 = heavy dry brush.")]
    [Range(0f, 1f)]
    [SerializeField] private float grainStrength = 0.34f;
    [Tooltip("Length of the tapered brush end at each tip, in pixels.")]
    [SerializeField] private float brushEndWidth = 32f;
    [Tooltip("Seed for the ink grain. Change it for a different stroke, same character.")]
    [SerializeField] private int inkSeed = 7;

    [Header("Glow Under The Ink")]
    [Tooltip("Strength of the glow behind the filled part of the stroke in phase 1.")]
    [Range(0f, 1f)]
    [SerializeField] private float underGlowP1 = 0.22f;
    [Tooltip("Strength of that glow in phase 2.")]
    [Range(0f, 1f)]
    [SerializeField] private float underGlowP2 = 0.42f;
    [Tooltip("How far the glow spreads above and below the stroke, as a multiple of its thickness.")]
    [SerializeField] private float underGlowSpread = 5f;

    [Header("Phase 1")]
    [Tooltip("Health colour while he is in phase 1. Ember, tied to his horns, never the moon's blue.")]
    [SerializeField] private Color fillColorP1 = new Color(0.66f, 0.19f, 0.10f, 1f);
    [Tooltip("Seconds per breath of the ink in phase 1.")]
    [SerializeField] private float breathPeriodP1 = 3.2f;

    [Header("Phase 2")]
    [Tooltip("Health colour once phase 2 begins. Blood red, hotter and glowier than phase 1.")]
    [SerializeField] private Color fillColorP2 = new Color(0.86f, 0.10f, 0.07f, 1f);
    [Tooltip("Seconds per breath of the ink in phase 2. Shorter means more urgent.")]
    [SerializeField] private float breathPeriodP2 = 1.1f;
    [Tooltip("How much the breath lightens and darkens the ink.")]
    [Range(0f, 0.4f)]
    [SerializeField] private float breathAmount = 0.10f;

    [Header("Two Stage Fill")]
    [Tooltip("Phase 1 spends the whole stroke, it empties at the phase threshold, then phase 2 refills it. Off = one continuous bar.")]
    [SerializeField] private bool twoStageFill = true;
    [Tooltip("Health fraction where phase 2 starts. MUST match EnemyCombat's Phase Threshold.")]
    [Range(0.05f, 0.95f)]
    [SerializeField] private float phaseSplit = 0.5f;
    [Tooltip("Seconds the stroke takes to paint itself back in when phase 2 begins.")]
    [SerializeField] private float refillDuration = 0.9f;

    [Header("Phase Marks")]
    [Tooltip("How many stages this boss has. Drawn as ink dots under the head of the stroke so an empty bar that refills is never a surprise. 0 hides them.")]
    [SerializeField] private int phaseCount = 2;
    [Tooltip("Dot size in pixels.")]
    [SerializeField] private float phaseMarkSize = 9f;
    [Tooltip("Gap between dots, centre to centre, in pixels.")]
    [SerializeField] private float phaseMarkSpacing = 15f;
    [Tooltip("A stage still to be fought.")]
    [SerializeField] private Color phaseMarkLive = new Color(0.92f, 0.38f, 0.18f, 0.90f);
    [Tooltip("A stage already taken from him.")]
    [SerializeField] private Color phaseMarkSpent = new Color(0.30f, 0.24f, 0.20f, 0.55f);

    [Header("Phase Transition")]
    [Tooltip("Heat that runs once down the stroke when phase 2 starts.")]
    [SerializeField] private Color sweepColor = new Color(1f, 0.78f, 0.45f, 0.85f);
    [Tooltip("Seconds the heat takes to travel the whole stroke.")]
    [SerializeField] private float sweepDuration = 0.55f;
    [Tooltip("Width of the travelling heat, as a fraction of the stroke.")]
    [Range(0.05f, 0.6f)]
    [SerializeField] private float sweepWidth = 0.22f;

    [Header("Damage")]
    [Tooltip("The chunk just lost, burning back toward the new health.")]
    [SerializeField] private Color trailColor = new Color(0.84f, 0.76f, 0.64f, 0.80f);
    [Tooltip("Seconds the lost chunk holds still before it starts burning back.")]
    [SerializeField] private float trailHoldTime = 0.35f;
    [Tooltip("How fast the lost chunk burns back, in bar fractions per second.")]
    [SerializeField] private float trailSpeed = 0.9f;
    [Tooltip("Hot mark that sits where the health currently ends, brightest right after a hit.")]
    [SerializeField] private Color edgeMarkColor = new Color(1f, 0.62f, 0.28f, 1f);
    [Tooltip("Width of that mark in pixels.")]
    [SerializeField] private float edgeMarkWidth = 18f;
    [Tooltip("Seconds for the mark to settle back after a hit.")]
    [SerializeField] private float damageFlashDuration = 0.5f;
    [Tooltip("Mark opacity between hits. 0 hides it until he is hurt.")]
    [Range(0f, 1f)]
    [SerializeField] private float edgeMarkRestAlpha = 0.35f;

    [Header("Fade")]
    [SerializeField] private float fadeInDuration = 0.35f;
    [SerializeField] private float fadeOutDuration = 1.5f;
    [Tooltip("Seconds after he dies before the bar leaves.")]
    [SerializeField] private float deathFadeDelay = 2f;

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;
    #endregion

    #region Private Fields
    private GameObject barRoot;
    private CanvasGroup canvasGroup;
    private TMP_Text nameLabel;
    private TMP_Text[] glowLabels;
    private Vector2[] glowDirs;
    private float[] glowRings;
    private Vector4 nameRectBase;
    private Image sealImage;
    private Image sealGlowImage;
    private Image underGlowImage;
    private Image trailImage;
    private Image healthImage;
    private Image sweepImage;
    private Image edgeMarkImage;
    private Image[] phaseMarkImages;
    private RectTransform sweepRect;
    private RectTransform edgeMarkRect;
    private RectTransform underGlowRect;

    private EnemyHealth trackedEnemy;
    private int lastKnownHP;
    private int maxHP;
    private float rawFill = 1f;      // straight HP fraction
    private float healthFill = 1f;   // what the stroke actually shows
    private float trailFill = 1f;
    private float trailHoldT;
    private float refillT = 1f;      // 1 = not refilling
    private float currentAlpha;
    private float targetAlpha;
    private bool isShowing;
    private float deathTimer;
    private bool enemyDead;
    private float damageFlashT;
    private float sweepT = -1f;
    private float heartPhase;
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
        if (trackedEnemy != null && isShowing)
        {
            int currentHP = trackedEnemy.CurrentHealth;
            if (currentHP != lastKnownHP)
            {
                OnHealthChanged(currentHP);
                lastKnownHP = currentHP;
            }
        }

        UpdateRefill();
        UpdateTrail();
        UpdateDecays();
        UpdateInk();
        UpdateSeal();
        UpdateName();
        UpdatePhaseMarks();
        UpdateSweep();
        UpdateEdgeMark();

        if (enemyDead && isShowing)
        {
            deathTimer += Time.unscaledDeltaTime;
            if (deathTimer >= deathFadeDelay) Hide("enemy dead + delay elapsed");
        }

        float fadeSpeed = targetAlpha > currentAlpha
            ? 1f / Mathf.Max(0.01f, fadeInDuration)
            : 1f / Mathf.Max(0.01f, fadeOutDuration);
        currentAlpha = Mathf.MoveTowards(currentAlpha, targetAlpha, fadeSpeed * Time.unscaledDeltaTime);
        canvasGroup.alpha = currentAlpha;
    }
    #endregion

    #region Public API
    /// <summary>
    /// Show the boss bar for a specific enemy. Resets to phase 1; if the enemy is already in
    /// phase 2, call SetPhase2 right after.
    /// </summary>
    public void Show(EnemyHealth enemy, string enemyName)
    {
        if (enemy == null) return;
        if (trackedEnemy == enemy && isShowing) return;

        trackedEnemy = enemy;
        maxHP = enemy.MaxHealth;
        lastKnownHP = enemy.CurrentHealth;
        rawFill = maxHP > 0 ? (float)lastKnownHP / maxHP : 0f;
        phase = 1;
        refillT = 1f;
        healthFill = StageFill(rawFill);
        trailFill = healthFill;
        trailHoldT = 0f;
        enemyDead = false;
        deathTimer = 0f;
        damageFlashT = 0f;
        sweepT = -1f;

        string shown = forceUppercase ? enemyName.ToUpperInvariant() : enemyName;
        if (nameLabel != null) nameLabel.text = shown;
        if (glowLabels != null)
            foreach (TMP_Text g in glowLabels)
                if (g != null) g.text = shown;

        SetFill(healthImage, healthFill);
        SetFill(trailImage, trailFill);

        isShowing = true;
        targetAlpha = 1f;

        DebugLog($"Showing bar for: {enemyName} ({lastKnownHP}/{maxHP})");
    }

    /// <summary>
    /// Switch the bar to its phase-2 look: blood red ink, the seal and the name ignite, one sweep
    /// of heat runs down the stroke, and with Two Stage Fill on the stroke paints itself back in.
    /// </summary>
    public void SetPhase2()
    {
        if (phase == 2) return;
        phase = 2;
        sweepT = 0f;
        heartPhase = 0f;
        if (twoStageFill)
        {
            refillT = 0f;
            healthFill = 0f;
            trailFill = 0f;
            SetFill(healthImage, 0f);
            SetFill(trailImage, 0f);
        }
        DebugLog("Bar switched to phase 2");
    }

    /// <summary>Hide the boss bar with a fade. Called on death or disengage, or manually.</summary>
    public void Hide(string reason = "")
    {
        isShowing = false;
        targetAlpha = 0f;
        DebugLog($"Hiding bar{(reason != "" ? $" ({reason})" : "")}");
    }

    /// <summary>
    /// Hide NOW, no fade. The Oni's cinematic needs the HUD gone on the cut frame.
    /// </summary>
    public void HideInstant(string reason = "")
    {
        isShowing = false;
        targetAlpha = 0f;
        currentAlpha = 0f;
        if (canvasGroup != null) canvasGroup.alpha = 0f;
        DebugLog($"Hiding bar INSTANTLY{(reason != "" ? $" ({reason})" : "")}");
    }

    /// <summary>Notify that the tracked enemy has died. Starts the fade-out delay.</summary>
    public void NotifyEnemyDead(EnemyHealth enemy)
    {
        if (enemy != trackedEnemy) return;
        enemyDead = true;
        deathTimer = 0f;
        DebugLog("Tracked enemy died, bar will fade");
    }
    #endregion

    #region Health Tracking
    /// <summary>
    /// Maps the raw HP fraction onto the stroke. With Two Stage Fill on, phase 1 uses the whole
    /// stroke to cover 100 percent down to the split, and phase 2 uses the whole stroke again to
    /// cover the split down to zero.
    /// </summary>
    private float StageFill(float raw)
    {
        if (!twoStageFill) return Mathf.Clamp01(raw);
        float split = Mathf.Clamp(phaseSplit, 0.05f, 0.95f);
        return phase == 2
            ? Mathf.Clamp01(raw / split)
            : Mathf.Clamp01((raw - split) / (1f - split));
    }

    private void OnHealthChanged(int newHP)
    {
        float previous = healthFill;
        rawFill = maxHP > 0 ? (float)newHP / maxHP : 0f;
        healthFill = StageFill(rawFill);
        SetFill(healthImage, healthFill);

        if (healthFill < previous)
        {
            trailHoldT = trailHoldTime;
            damageFlashT = 1f;
        }
        else
        {
            trailFill = healthFill;
            SetFill(trailImage, trailFill);
        }

        if (newHP <= 0)
        {
            enemyDead = true;
            deathTimer = 0f;
        }
    }

    /// <summary>The stroke painting itself back in at the start of phase 2.</summary>
    private void UpdateRefill()
    {
        if (refillT >= 1f) return;

        refillT += Time.unscaledDeltaTime / Mathf.Max(0.01f, refillDuration);
        float t = Mathf.Clamp01(refillT);
        float eased = 1f - (1f - t) * (1f - t);

        healthFill = StageFill(rawFill) * eased;
        trailFill = healthFill;
        SetFill(healthImage, healthFill);
        SetFill(trailImage, trailFill);
    }

    private void UpdateTrail()
    {
        if (refillT < 1f) return;
        if (trailFill <= healthFill) return;

        if (trailHoldT > 0f)
        {
            trailHoldT -= Time.unscaledDeltaTime;
            return;
        }

        trailFill = Mathf.MoveTowards(trailFill, healthFill, trailSpeed * Time.unscaledDeltaTime);
        SetFill(trailImage, trailFill);
    }

    private void UpdateDecays()
    {
        if (damageFlashT > 0f)
            damageFlashT = Mathf.MoveTowards(damageFlashT, 0f,
                Time.unscaledDeltaTime / Mathf.Max(0.01f, damageFlashDuration));
    }
    #endregion

    #region Look
    private Color ActiveFillColor => phase == 2 ? fillColorP2 : fillColorP1;
    private float ActiveBreathPeriod => phase == 2 ? breathPeriodP2 : breathPeriodP1;
    private float ActiveUnderGlow => phase == 2 ? underGlowP2 : underGlowP1;

    private float Breath()
    {
        float period = Mathf.Max(0.05f, ActiveBreathPeriod);
        return Mathf.Sin(Time.unscaledTime * (Mathf.PI * 2f / period)) * breathAmount;
    }

    /// <summary>
    /// The ink breathes: the health colour lightens and darkens a little, faster in phase 2, and
    /// flares toward the hit colour for a moment after damage. The track never changes.
    /// </summary>
    private void UpdateInk()
    {
        if (healthImage == null) return;

        float breath = Breath();
        Color c = ActiveFillColor;
        c.r = Mathf.Clamp01(c.r * (1f + breath));
        c.g = Mathf.Clamp01(c.g * (1f + breath));
        c.b = Mathf.Clamp01(c.b * (1f + breath));
        c = Color.Lerp(c, edgeMarkColor, damageFlashT * 0.25f);
        c.a = ActiveFillColor.a;
        healthImage.color = c;

        if (trailImage != null)
        {
            Color t = trailColor;
            if (enemyDead) t.a = trailColor.a * 0.5f;
            trailImage.color = t;
        }

        if (underGlowImage != null)
        {
            // The glow only covers the part of the stroke that is still inked.
            float w = Mathf.Max(1f, barWidth * healthFill);
            underGlowRect.sizeDelta = new Vector2(w, barHeight * Mathf.Max(1f, underGlowSpread));
            underGlowRect.anchoredPosition = new Vector2(w * 0.5f, 0f);

            Color g = ActiveFillColor;
            g.a = Mathf.Clamp01(ActiveUnderGlow * (1f + breath) + damageFlashT * 0.18f);
            underGlowImage.color = g;
            underGlowImage.enabled = healthFill > 0.001f;
        }
    }

    /// <summary>The seal and its halo are the identity, and they ignite at phase 2.</summary>
    private void UpdateSeal()
    {
        float k = 1f - Mathf.Exp(-6f * Time.unscaledDeltaTime);
        float breath = Breath();

        if (sealImage != null)
        {
            Color target = phase == 2 ? sealColorP2 : sealColorP1;
            sealImage.color = Color.Lerp(sealImage.color, target, k);
        }

        if (sealGlowImage != null)
        {
            Color target = phase == 2 ? sealGlowColorP2 : sealGlowColorP1;
            target.a = Mathf.Clamp01(target.a * (1f + breath * 2f));
            sealGlowImage.color = Color.Lerp(sealGlowImage.color, target, k);
        }
    }

    /// <summary>
    /// The name carries a glow of its own, wider and hotter once phase 2 starts, and from that
    /// point it beats: two thumps and a rest, faster the closer he gets to death.
    /// </summary>
    private void UpdateName()
    {
        if (glowLabels == null) return;

        Color glow = phase == 2 ? nameGlowP2 : nameGlowP1;
        float radius = phase == 2 ? nameGlowRadiusP2 : nameGlowRadiusP1;

        if (phase == 2 && nameHeartbeat)
        {
            float stage = StageFill(rawFill);
            float period = Mathf.Lerp(heartbeatPeriodLow, heartbeatPeriodFull, Mathf.Clamp01(stage));
            heartPhase += Time.unscaledDeltaTime / Mathf.Max(0.05f, period);
            heartPhase -= Mathf.Floor(heartPhase);

            float beat = Heartbeat(heartPhase);
            glow.a = Mathf.Clamp01(glow.a * Mathf.Lerp(heartbeatMin, heartbeatMax, beat));
            radius *= Mathf.Lerp(0.85f, 1.35f, beat);
        }
        else
        {
            glow.a = Mathf.Clamp01(glow.a * (1f + Breath() * 2f));
        }

        // Each copy carries a slice of the glow. Stacked, they build the falloff.
        float per = glow.a / (glowLabels.Length * 0.42f);
        for (int i = 0; i < glowLabels.Length; i++)
        {
            TMP_Text g = glowLabels[i];
            if (g == null) continue;

            Color c = glow;
            c.a = Mathf.Clamp01(per * glowRings[i] * Mathf.Lerp(1f, nameGlowFalloff, i / (float)glowLabels.Length));
            g.color = c;

            Vector2 d = glowDirs[i] * radius;
            RectTransform r = g.rectTransform;
            r.offsetMin = new Vector2(nameRectBase.x + d.x, nameRectBase.y + d.y);
            r.offsetMax = new Vector2(nameRectBase.z + d.x, nameRectBase.w + d.y);
        }
    }

    /// <summary>
    /// One cycle of a lub-dub: a strong first thump, a softer one right behind it, then a rest
    /// over roughly two thirds of the cycle. 0 at rest, 1 at the top of the first thump.
    /// </summary>
    private static float Heartbeat(float t)
    {
        float lub = Thump(t, 0f, 0.035f, 0.17f);
        float dub = Thump(t, 0.26f, 0.03f, 0.21f) * 0.72f;
        return Mathf.Clamp01(Mathf.Max(lub, dub));
    }

    /// <summary>One thump: a fast rise then a softer fall. 0 outside its own window.</summary>
    private static float Thump(float t, float start, float rise, float fall)
    {
        float d = t - start;
        if (d < 0f || d > rise + fall) return 0f;
        if (d < rise) return d / rise;
        float f = (d - rise) / fall;
        return (1f - f) * (1f - f);
    }

    /// <summary>
    /// One dot per stage, so the player knows before the first one empties that there is another
    /// coming. The dot for a stage already taken from him burns out.
    /// </summary>
    private void UpdatePhaseMarks()
    {
        if (phaseMarkImages == null) return;

        for (int i = 0; i < phaseMarkImages.Length; i++)
        {
            if (phaseMarkImages[i] == null) continue;
            bool spent = i < phase - 1;
            Color target = spent ? phaseMarkSpent : phaseMarkLive;
            if (!spent) target.a = Mathf.Clamp01(target.a * (1f + Breath()));
            phaseMarkImages[i].color = Color.Lerp(phaseMarkImages[i].color, target,
                1f - Mathf.Exp(-8f * Time.unscaledDeltaTime));
        }
    }

    /// <summary>One pass of heat down the stroke at the phase-2 beat, then it is gone.</summary>
    private void UpdateSweep()
    {
        if (sweepImage == null) return;

        if (sweepT < 0f)
        {
            if (sweepImage.enabled) sweepImage.enabled = false;
            return;
        }

        if (!sweepImage.enabled) sweepImage.enabled = true;

        sweepT += Time.unscaledDeltaTime / Mathf.Max(0.01f, sweepDuration);
        if (sweepT >= 1f)
        {
            sweepT = -1f;
            sweepImage.enabled = false;
            return;
        }

        float w = Mathf.Clamp01(sweepWidth) * barWidth;
        float travel = barWidth + w;
        sweepRect.sizeDelta = new Vector2(w, barHeight * 2f);
        sweepRect.anchoredPosition = new Vector2(-w * 0.5f + travel * sweepT, 0f);

        Color c = sweepColor;
        c.a = sweepColor.a * Mathf.Sin(sweepT * Mathf.PI);
        sweepImage.color = c;
    }

    /// <summary>The hot mark that sits where the health ends, flaring on each hit.</summary>
    private void UpdateEdgeMark()
    {
        if (edgeMarkImage == null) return;

        edgeMarkRect.anchoredPosition = new Vector2(barWidth * healthFill, 0f);

        Color c = edgeMarkColor;
        float a = Mathf.Lerp(edgeMarkRestAlpha, 1f, damageFlashT);
        if (healthFill <= 0.0001f) a = 0f;
        c.a = edgeMarkColor.a * a;
        edgeMarkImage.color = c;
    }
    #endregion

    #region UI Construction
    private void CreateUI()
    {
        RectTransform selfRect = GetComponent<RectTransform>();
        if (selfRect != null)
        {
            selfRect.anchorMin = Vector2.zero;
            selfRect.anchorMax = Vector2.one;
            selfRect.offsetMin = Vector2.zero;
            selfRect.offsetMax = Vector2.zero;
        }

        barRoot = new GameObject("BossBar_Root");
        barRoot.transform.SetParent(transform, false);

        RectTransform rootRect = barRoot.AddComponent<RectTransform>();
        float edgeY = anchorToBottom ? 0f : 1f;
        rootRect.anchorMin = new Vector2(0.5f, edgeY);
        rootRect.anchorMax = new Vector2(0.5f, edgeY);
        rootRect.pivot = new Vector2(0.5f, edgeY);
        rootRect.anchoredPosition = new Vector2(0f, anchorToBottom ? screenMargin : -screenMargin);
        rootRect.sizeDelta = new Vector2(barWidth, barHeight);

        canvasGroup = barRoot.AddComponent<CanvasGroup>();
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        Sprite stroke = CreateBrushStrokeSprite();
        Sprite soft = CreateSoftFalloffSprite(64);

        // Glow under the ink, drawn first so everything sits on top of it.
        underGlowImage = CreateChildImage("UnderGlow", barRoot.transform, soft, ActiveFillColor);
        underGlowRect = underGlowImage.rectTransform;
        underGlowRect.anchorMin = new Vector2(0f, 0.5f);
        underGlowRect.anchorMax = new Vector2(0f, 0.5f);
        underGlowRect.pivot = new Vector2(0.5f, 0.5f);
        underGlowRect.sizeDelta = new Vector2(barWidth, barHeight * underGlowSpread);

        CreateStrokeLayer("Track", barRoot.transform, stroke, trackColor, Image.Type.Simple);

        trailImage = CreateStrokeLayer("Trail", barRoot.transform, stroke, trailColor, Image.Type.Filled);
        trailImage.fillMethod = Image.FillMethod.Horizontal;
        trailImage.fillOrigin = (int)Image.OriginHorizontal.Left;
        trailImage.fillAmount = 1f;

        healthImage = CreateStrokeLayer("Health", barRoot.transform, stroke, fillColorP1, Image.Type.Filled);
        healthImage.fillMethod = Image.FillMethod.Horizontal;
        healthImage.fillOrigin = (int)Image.OriginHorizontal.Left;
        healthImage.fillAmount = 1f;

        sweepImage = CreateChildImage("Sweep", barRoot.transform, soft, sweepColor);
        sweepRect = sweepImage.rectTransform;
        sweepRect.anchorMin = new Vector2(0f, 0.5f);
        sweepRect.anchorMax = new Vector2(0f, 0.5f);
        sweepRect.pivot = new Vector2(0.5f, 0.5f);
        sweepRect.sizeDelta = new Vector2(barWidth * sweepWidth, barHeight * 2f);
        sweepImage.enabled = false;

        edgeMarkImage = CreateChildImage("EdgeMark", barRoot.transform, soft, edgeMarkColor);
        edgeMarkRect = edgeMarkImage.rectTransform;
        edgeMarkRect.anchorMin = new Vector2(0f, 0.5f);
        edgeMarkRect.anchorMax = new Vector2(0f, 0.5f);
        edgeMarkRect.pivot = new Vector2(0.5f, 0.5f);
        edgeMarkRect.sizeDelta = new Vector2(edgeMarkWidth, barHeight * 2.2f);

        // The hairline under the stroke uses the stroke itself, squashed flat, so it tapers with
        // the brush instead of reading as a straight mechanical rule.
        Image underline = CreateChildImage("Underline", barRoot.transform, stroke, edgeColor);
        RectTransform ul = underline.rectTransform;
        ul.anchorMin = new Vector2(0f, 0f);
        ul.anchorMax = new Vector2(1f, 0f);
        ul.pivot = new Vector2(0.5f, 1f);
        ul.sizeDelta = new Vector2(0f, 2f);
        ul.anchoredPosition = new Vector2(0f, -3f);

        CreatePhaseMarks(soft);

        float nameIndent = phaseCount > 0 ? phaseCount * phaseMarkSpacing + 10f : 0f;
        float nameTop = -8f;
        float nameHeight = nameFontSize + 8f;
        nameRectBase = new Vector4(nameIndent, nameTop - nameHeight, 0f, nameTop);

        // The halo is built from offset copies of the name rather than TextMesh Pro's own glow.
        // TMP can only draw glow inside the font atlas padding, and this atlas was baked with 6 px
        // of padding at point size 69, so at an 18 px name there is about 1.5 screen pixels of room
        // and no glow setting can ever show. Offset copies have no such ceiling.
        CreateNameGlowLabels();

        nameLabel = CreateLabel("EnemyName", barRoot.transform, nameFont, nameFontSize, nameColor,
            TextAlignmentOptions.Left);
        nameLabel.characterSpacing = nameLetterSpacing;
        RectTransform nr = nameLabel.rectTransform;
        nr.anchorMin = new Vector2(0f, 0f);
        nr.anchorMax = new Vector2(1f, 0f);
        nr.pivot = new Vector2(0.5f, 1f);
        nr.offsetMin = new Vector2(nameRectBase.x, nameRectBase.y);
        nr.offsetMax = new Vector2(nameRectBase.z, nameRectBase.w);

        if (sealGlowSprite != null)
        {
            sealGlowImage = CreateChildImage("KanjiSealGlow", barRoot.transform, sealGlowSprite, sealGlowColorP1);
            RectTransform gr = sealGlowImage.rectTransform;
            float gs = sealSize * Mathf.Max(1f, sealGlowScale);
            gr.anchorMin = new Vector2(0f, 0.5f);
            gr.anchorMax = new Vector2(0f, 0.5f);
            gr.pivot = new Vector2(0.5f, 0.5f);
            gr.sizeDelta = new Vector2(gs, gs);
            gr.anchoredPosition = new Vector2(-sealOffsetX - sealSize * 0.5f, sealOffsetY);
        }

        if (sealSprite != null)
        {
            sealImage = CreateChildImage("KanjiSeal", barRoot.transform, sealSprite, sealColorP1);
            RectTransform kr = sealImage.rectTransform;
            kr.anchorMin = new Vector2(0f, 0.5f);
            kr.anchorMax = new Vector2(0f, 0.5f);
            kr.pivot = new Vector2(1f, 0.5f);
            kr.sizeDelta = new Vector2(sealSize, sealSize);
            kr.anchoredPosition = new Vector2(-sealOffsetX, sealOffsetY);
        }
    }

    /// <summary>
    /// Copies of the name laid in three rings around it, each one faint. Stacked, they build a
    /// smooth halo at any font size, which TMP's own glow cannot do with this font's padding.
    /// Sixteen directions per ring and a half-step stagger between rings is what stops the copies
    /// reading as separate ghosts.
    /// </summary>
    private void CreateNameGlowLabels()
    {
        const int Directions = 16;
        float[] ringRadius = { 0.40f, 0.75f, 1.15f };
        float[] ringWeight = { 1.00f, 0.80f, 0.45f };

        glowLabels = new TMP_Text[Directions * ringRadius.Length];
        glowDirs = new Vector2[glowLabels.Length];
        glowRings = new float[glowLabels.Length];

        int i = 0;
        for (int ring = 0; ring < ringRadius.Length; ring++)
        {
            for (int d = 0; d < Directions; d++)
            {
                float ang = (Mathf.PI * 2f / Directions) * (d + 0.5f * ring);
                glowDirs[i] = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * ringRadius[ring];
                glowRings[i] = ringWeight[ring];

                TMP_Text g = CreateLabel($"NameGlow_{ring}_{d}", barRoot.transform, nameFont,
                    nameFontSize, Color.clear, TextAlignmentOptions.Left);
                g.characterSpacing = nameLetterSpacing;
                RectTransform r = g.rectTransform;
                r.anchorMin = new Vector2(0f, 0f);
                r.anchorMax = new Vector2(1f, 0f);
                r.pivot = new Vector2(0.5f, 1f);
                glowLabels[i] = g;
                i++;
            }
        }
    }

    private void CreatePhaseMarks(Sprite soft)
    {
        if (phaseCount <= 0)
        {
            phaseMarkImages = null;
            return;
        }

        phaseMarkImages = new Image[phaseCount];
        for (int i = 0; i < phaseCount; i++)
        {
            Image dot = CreateChildImage($"PhaseMark_{i + 1}", barRoot.transform, soft, phaseMarkLive);
            RectTransform r = dot.rectTransform;
            r.anchorMin = new Vector2(0f, 0f);
            r.anchorMax = new Vector2(0f, 0f);
            r.pivot = new Vector2(0.5f, 0.5f);
            r.sizeDelta = new Vector2(phaseMarkSize, phaseMarkSize);
            // Centred on the name row, so the dots and the name read as one line.
            r.anchoredPosition = new Vector2(phaseMarkSize * 0.5f + i * phaseMarkSpacing,
                -8f - (nameFontSize + 8f) * 0.5f);
            phaseMarkImages[i] = dot;
        }
    }

    private Image CreateStrokeLayer(string name, Transform parent, Sprite sprite, Color color, Image.Type type)
    {
        Image img = CreateChildImage(name, parent, sprite, color);
        RectTransform r = img.rectTransform;
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.offsetMin = Vector2.zero;
        r.offsetMax = Vector2.zero;
        img.type = type;
        return img;
    }

    private Image CreateChildImage(string name, Transform parent, Sprite sprite, Color color)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        obj.AddComponent<RectTransform>();

        Image img = obj.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        if (sprite != null)
        {
            img.sprite = sprite;
            img.type = Image.Type.Simple;
        }
        return img;
    }

    private TMP_Text CreateLabel(string name, Transform parent, TMP_FontAsset font, float size,
        Color color, TextAlignmentOptions align)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        obj.AddComponent<RectTransform>();

        TMP_Text label = obj.AddComponent<TextMeshProUGUI>();
        if (font != null) label.font = font;
        label.text = "";
        label.fontSize = size;
        label.color = color;
        label.alignment = align;
        label.enableWordWrapping = false;
        label.raycastTarget = false;
        return label;
    }

    /// <summary>
    /// The stroke itself, generated at the bar's real pixel size so the grain never stretches:
    /// solid through the middle, tapered at both tips like a brush lifting off the paper, with
    /// dry-brush breakup along the length and a slightly uneven top and bottom edge.
    /// </summary>
    private Sprite CreateBrushStrokeSprite()
    {
        int w = Mathf.Max(16, Mathf.RoundToInt(barWidth));
        int h = Mathf.Max(4, Mathf.RoundToInt(barHeight * 3f));
        int end = Mathf.Max(1, Mathf.RoundToInt(brushEndWidth));

        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        System.Random rng = new System.Random(inkSeed);
        float phaseA = (float)rng.NextDouble() * 100f;
        float phaseB = (float)rng.NextDouble() * 100f;
        float phaseC = (float)rng.NextDouble() * 100f;

        Color[] pixels = new Color[w * h];

        for (int x = 0; x < w; x++)
        {
            float u = (float)x / (w - 1);

            // Tapered tips: the brush lands and lifts.
            float head = Mathf.Clamp01((float)x / end);
            float tail = Mathf.Clamp01((float)(w - 1 - x) / end);
            float taper = Mathf.Min(head * head * (3f - 2f * head), tail * tail * (3f - 2f * tail));

            // Uneven top and bottom, so the stroke is never a rectangle.
            float wobble = Mathf.Sin(u * 17f + phaseA) * 0.05f + Mathf.Sin(u * 41f + phaseB) * 0.025f;
            float halfBand = 0.5f * (0.86f + wobble);

            // Dry brush: the ink skips here and there along the length.
            float dry = Mathf.PerlinNoise(u * 24f + phaseC, 0.5f);
            float drySkip = 1f - grainStrength * Mathf.Clamp01((0.55f - dry) * 2.2f);

            for (int y = 0; y < h; y++)
            {
                float v = (float)y / (h - 1) - 0.5f;
                float d = Mathf.Abs(v) / halfBand;

                float a = Mathf.Clamp01(1f - d);
                a = a * a * (3f - 2f * a);
                a *= taper * drySkip;

                // A little more bite along the very edge of the stroke.
                float edgeNoise = Mathf.PerlinNoise(u * 60f + phaseA, v * 8f + phaseB);
                a *= Mathf.Lerp(1f, edgeNoise, grainStrength * 0.45f * Mathf.Clamp01(d));

                pixels[y * w + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(a));
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f));
    }

    /// <summary>Soft round falloff, used for the glows, the travelling heat and the marks.</summary>
    private Sprite CreateSoftFalloffSprite(int size)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;

        float center = (size - 1) * 0.5f;
        Color[] pixels = new Color[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x - center) / center;
                float dy = (y - center) / center;
                float d = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy));
                float a = 1f - d;
                a = a * a * a;
                pixels[y * size + x] = new Color(1f, 1f, 1f, a);
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }

    private void SetFill(Image image, float percent)
    {
        if (image == null) return;
        image.fillAmount = Mathf.Clamp01(percent);
    }
    #endregion

    #region Debug
    private void DebugLog(string message)
    {
        if (showDebugLogs) Debug.Log($"[BossBar] {message}");
    }
    #endregion
}
