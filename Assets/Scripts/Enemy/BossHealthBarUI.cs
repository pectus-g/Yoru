using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// YORU Boss Health Bar — Zelda-style screen-top bar for Tier 1-2-3 enemies.
/// 
/// Shows enemy name + health bar at the top center of screen.
/// Only one bar visible at a time (focused on current threat).
/// Tier 4 enemies (minions like Kodoma) use the existing overhead EnemyHealthBar instead.
/// 
/// Features:
///   - Auto-creates UI on the existing screen-space Canvas
///   - Name label above the bar
///   - Two-layer chunk effect (instant drop + trailing catch-up)
///   - Fades in on combat start, fades out on enemy death
///   - Singleton — call Show(enemyHealth) from EnemyCombat.SetState
/// 
/// SETUP:
///   1. Create empty GameObject as child of your main UI Canvas
///   2. Name it "BossHealthBar"
///   3. Add this component
///   4. On Nopperabō's EnemyCombat Inspector, set Enemy Tier to 3
///   5. That's it — UI builds itself at runtime
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
    [SerializeField] private float barWidth = 500f;
    [Tooltip("Bar height in pixels")]
    [SerializeField] private float barHeight = 12f;
    [Tooltip("Distance from top of screen in pixels")]
    [SerializeField] private float topMargin = 60f;

    [Header("Chunk Effect")]
    [Tooltip("How fast the trail bar catches up")]
    [SerializeField] private float trailSpeed = 1.5f;

    [Header("Fade")]
    [SerializeField] private float fadeInDuration = 0.3f;
    [SerializeField] private float fadeOutDuration = 1.5f;
    [Tooltip("Seconds after enemy dies before bar fades out")]
    [SerializeField] private float deathFadeDelay = 2f;

    [Header("Colors")]
    [SerializeField] private Color healthColor = new Color(0.95f, 0.75f, 0.25f, 1f);
    [SerializeField] private Color trailColor = new Color(0.85f, 0.45f, 0.15f, 0.9f);
    [SerializeField] private Color backgroundColor = new Color(0.1f, 0.08f, 0.05f, 0.85f);
    [SerializeField] private Color nameColor = new Color(0.95f, 0.90f, 0.80f, 1f);

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;
    #endregion

    #region Private Fields
    // UI elements (created at runtime)
    private GameObject barRoot;
    private CanvasGroup canvasGroup;
    private TMP_Text nameLabel;
    private RectTransform healthRect;
    private RectTransform trailRect;

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

        // Trail catch-up animation
        if (trailFill > healthFill)
        {
            trailFill = Mathf.MoveTowards(trailFill, healthFill, trailSpeed * Time.deltaTime);
            SetBarFill(trailRect, trailFill);
        }

        // Death fade delay
        if (enemyDead && isShowing)
        {
            deathTimer += Time.deltaTime;
            if (deathTimer >= deathFadeDelay)
            {
                Hide("enemy dead + delay elapsed");
            }
        }

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
    /// Show the boss bar for a specific enemy. Call from EnemyCombat when tier 1-2-3 enters Alert.
    /// If already showing for a different enemy, switches to the new one.
    /// </summary>
    public void Show(EnemyHealth enemy, string enemyName)
    {
        if (enemy == null) return;

        // Already tracking this enemy
        if (trackedEnemy == enemy && isShowing) return;

        trackedEnemy = enemy;
        maxHP = enemy.MaxHealth;
        lastKnownHP = enemy.CurrentHealth;
        healthFill = (float)lastKnownHP / maxHP;
        trailFill = healthFill;
        enemyDead = false;
        deathTimer = 0f;

        // Update UI
        nameLabel.text = enemyName;
        SetBarFill(healthRect, healthFill);
        SetBarFill(trailRect, trailFill);

        // Fade in
        isShowing = true;
        targetAlpha = 1f;

        DebugLog($"Showing bar for: {enemyName} ({lastKnownHP}/{maxHP})");
    }

    /// <summary>
    /// Hide the boss bar. Called automatically on death or can be called manually.
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
        healthFill = (float)newHP / maxHP;
        SetBarFill(healthRect, healthFill);
        // Trail catches up smoothly in LateUpdate

        if (newHP <= 0)
        {
            enemyDead = true;
            deathTimer = 0f;
        }
    }
    #endregion

    #region UI Construction
    private void CreateUI()
    {
        // Root container — anchored to top center of parent Canvas
        barRoot = new GameObject("BossBar_Root");
        barRoot.transform.SetParent(transform, false);

        RectTransform rootRect = barRoot.AddComponent<RectTransform>();
        rootRect.anchorMin = new Vector2(0.5f, 1f); // Top center
        rootRect.anchorMax = new Vector2(0.5f, 1f);
        rootRect.pivot = new Vector2(0.5f, 1f);
        rootRect.anchoredPosition = new Vector2(0f, -topMargin);
        rootRect.sizeDelta = new Vector2(barWidth, barHeight + 30f); // Extra height for name

        canvasGroup = barRoot.AddComponent<CanvasGroup>();
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        // === NAME LABEL ===
        GameObject nameObj = new GameObject("EnemyName");
        nameObj.transform.SetParent(barRoot.transform, false);

        RectTransform nameRect = nameObj.AddComponent<RectTransform>();
        nameRect.anchorMin = new Vector2(0f, 1f);
        nameRect.anchorMax = new Vector2(1f, 1f);
        nameRect.pivot = new Vector2(0.5f, 1f);
        nameRect.anchoredPosition = new Vector2(0f, 0f);
        nameRect.sizeDelta = new Vector2(0f, 24f);

        nameLabel = nameObj.AddComponent<TextMeshProUGUI>();
        nameLabel.text = "Enemy Name";
        nameLabel.fontSize = 18f;
        nameLabel.fontStyle = FontStyles.SmallCaps;
        nameLabel.color = nameColor;
        nameLabel.alignment = TextAlignmentOptions.Center;
        nameLabel.enableWordWrapping = false;

        // === BAR CONTAINER ===
        GameObject barContainer = new GameObject("BarContainer");
        barContainer.transform.SetParent(barRoot.transform, false);

        RectTransform containerRect = barContainer.AddComponent<RectTransform>();
        containerRect.anchorMin = new Vector2(0f, 0f);
        containerRect.anchorMax = new Vector2(1f, 0f);
        containerRect.pivot = new Vector2(0.5f, 0f);
        containerRect.anchoredPosition = new Vector2(0f, 0f);
        containerRect.sizeDelta = new Vector2(0f, barHeight);

        // Background
        GameObject bgObj = CreateBarLayer("Background", barContainer.transform, backgroundColor);

        // Trail (amber catch-up)
        GameObject trailObj = CreateBarLayer("Trail", barContainer.transform, trailColor);
        trailRect = trailObj.GetComponent<RectTransform>();

        // Health (gold instant)
        GameObject healthObj = CreateBarLayer("Health", barContainer.transform, healthColor);
        healthRect = healthObj.GetComponent<RectTransform>();

        // Border outline (thin frame around the bar)
        CreateBorderOutline(barContainer.transform, barHeight);
    }

    private GameObject CreateBarLayer(string name, Transform parent, Color color)
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

        return obj;
    }

    private void CreateBorderOutline(Transform parent, float height)
    {
        // Simple 1px border using 4 stretched images
        float borderWidth = 1f;
        Color borderColor = new Color(0.6f, 0.5f, 0.3f, 0.6f);

        // Top
        CreateBorderEdge("Border_Top", parent, borderColor,
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1f),
            Vector2.zero, new Vector2(0, borderWidth));
        // Bottom
        CreateBorderEdge("Border_Bottom", parent, borderColor,
            new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0f),
            Vector2.zero, new Vector2(0, borderWidth));
        // Left
        CreateBorderEdge("Border_Left", parent, borderColor,
            new Vector2(0, 0), new Vector2(0, 1), new Vector2(0f, 0.5f),
            Vector2.zero, new Vector2(borderWidth, 0));
        // Right
        CreateBorderEdge("Border_Right", parent, borderColor,
            new Vector2(1, 0), new Vector2(1, 1), new Vector2(1f, 0.5f),
            Vector2.zero, new Vector2(borderWidth, 0));
    }

    private void CreateBorderEdge(string name, Transform parent, Color color,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
        Vector2 offsetMin, Vector2 sizeDelta)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);

        RectTransform rect = obj.AddComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.sizeDelta = sizeDelta;

        Image img = obj.AddComponent<Image>();
        img.color = color;
    }

    private void SetBarFill(RectTransform rect, float percent)
    {
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
