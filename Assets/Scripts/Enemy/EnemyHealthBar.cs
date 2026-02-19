using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// World-space health bar for Tier 3-4 enemies.
/// Attach to any enemy alongside EnemyHealth.
/// Creates its own UI automatically — no prefab needed.
/// 
/// Features:
/// - Billboard (always faces camera)
/// - Appears on first hit, fades after combat ends
/// - Two-layer chunk effect (instant drop + trailing catch-up)
/// - Golden/warm glow aesthetic
/// - Tier-configurable thickness
/// - Uses anchor-based resizing (no sprite required)
/// 
/// Setup: Add this component to any enemy that has EnemyHealth.
/// That's it. Everything else is automatic.
/// </summary>
public class EnemyHealthBar : MonoBehaviour
{
    [Header("Bar Settings")]
    [SerializeField] private float barWidth = 1.2f;
    [SerializeField] private float barHeight = 0.06f;
    [SerializeField] private float heightAboveEnemy = 2.2f;
    [SerializeField] private float canvasScale = 0.01f;

    [Header("Chunk Effect")]
    [SerializeField] private float trailSpeed = 1.5f;

    [Header("Fade Settings")]
    [SerializeField] private float fadeDelay = 3f;
    [SerializeField] private float fadeDuration = 0.8f;

    [Header("Colors")]
    [SerializeField] private Color healthColor = new Color(0.95f, 0.75f, 0.25f, 1f);
    [SerializeField] private Color trailColor = new Color(0.85f, 0.45f, 0.15f, 0.9f);
    [SerializeField] private Color backgroundColor = new Color(0.1f, 0.08f, 0.05f, 0.75f);
    [SerializeField] private Color glowColor = new Color(0.95f, 0.75f, 0.25f, 0.25f);

    // Internal references
    private Canvas canvas;
    private CanvasGroup canvasGroup;
    private Image backgroundImage;
    private Image trailImage;
    private Image healthImage;
    private Image glowImage;

    // RectTransforms for anchor-based resizing
    private RectTransform trailRect;
    private RectTransform healthRect;

    // State tracking
    private EnemyHealth enemyHealth;
    private Camera mainCamera;
    private int lastKnownHealth;
    private int maxHealth;
    private float trailFillAmount = 1f;
    private float healthFillAmount = 1f;
    private bool hasBeenHit = false;
    private bool isVisible = false;
    private float timeSinceLastHit = 0f;
    private float currentAlpha = 0f;
    private bool isFading = false;
    private bool isDead = false;

    void Start()
    {
        mainCamera = Camera.main;
        enemyHealth = GetComponent<EnemyHealth>();

        if (enemyHealth == null)
        {
            Debug.LogWarning($"[HealthBar] {gameObject.name} has no EnemyHealth component!");
            enabled = false;
            return;
        }

        maxHealth = enemyHealth.MaxHealth;
        lastKnownHealth = maxHealth;

        CreateHealthBarUI();

        // Start hidden
        canvasGroup.alpha = 0f;
        currentAlpha = 0f;

        Debug.Log($"[HealthBar] {gameObject.name} health bar ready (max: {maxHealth})");
    }

    void LateUpdate()
    {
        if (isDead && currentAlpha <= 0f) return;

        // Billboard — always face camera
        if (mainCamera != null && canvas != null)
        {
            canvas.transform.forward = mainCamera.transform.forward;
        }

        // Check for health changes
        int currentHealth = enemyHealth.CurrentHealth;

        if (currentHealth != lastKnownHealth)
        {
            OnHealthChanged(currentHealth);
            lastKnownHealth = currentHealth;
        }

        // Animate the trail bar (chunk effect — the slow catch-up)
        if (trailFillAmount > healthFillAmount)
        {
            trailFillAmount = Mathf.MoveTowards(trailFillAmount, healthFillAmount, trailSpeed * Time.deltaTime);
            SetBarPercent(trailRect, trailFillAmount);
        }

        // Update glow intensity
        UpdateGlow();

        // Handle fade in/out
        HandleFading();
    }

    private void OnHealthChanged(int newHealth)
    {
        if (!hasBeenHit)
        {
            hasBeenHit = true;
        }

        // Always show on any hit (including after fade)
        isVisible = true;
        isFading = false;

        // Health bar drops INSTANTLY
        healthFillAmount = (float)newHealth / maxHealth;
        SetBarPercent(healthRect, healthFillAmount);

        // Trail bar stays where it was — catches up smoothly in LateUpdate

        // Reset fade timer
        timeSinceLastHit = 0f;

        // Snap to full alpha if we were fading
        if (currentAlpha < 1f && isVisible)
        {
            currentAlpha = 1f;
        }

        // Death
        if (newHealth <= 0)
        {
            isDead = true;
            isFading = true;
            timeSinceLastHit = fadeDelay;
        }
    }

    private void HandleFading()
    {
        if (!hasBeenHit) return;

        if (isVisible && !isDead)
        {
            timeSinceLastHit += Time.deltaTime;

            if (timeSinceLastHit >= fadeDelay)
            {
                isFading = true;
            }
        }

        // Fade in (quick)
        if (isVisible && !isFading && currentAlpha < 1f)
        {
            currentAlpha = Mathf.MoveTowards(currentAlpha, 1f, Time.deltaTime / 0.2f);
        }

        // Fade out (smooth)
        if (isFading)
        {
            currentAlpha = Mathf.MoveTowards(currentAlpha, 0f, Time.deltaTime / fadeDuration);
            if (currentAlpha <= 0f && !isDead)
            {
                isVisible = false;
                isFading = false;
            }
        }

        if (canvasGroup != null)
        {
            canvasGroup.alpha = currentAlpha;
        }
    }

    private void UpdateGlow()
    {
        if (glowImage == null) return;

        float healthPercent = healthFillAmount;
        float basePulse = 0.15f + (Mathf.Sin(Time.time * 2f) * 0.05f);
        float lowHealthBoost = Mathf.Lerp(0.2f, 0f, healthPercent);

        Color glow = glowColor;
        glow.a = basePulse + lowHealthBoost;
        glowImage.color = glow;
    }

    // ====================================================
    // BAR WIDTH CONTROL — anchor-based (no sprite needed)
    // ====================================================

    /// <summary>
    /// Sets a bar's visible width by adjusting its right anchor.
    /// percent: 1.0 = full bar, 0.0 = empty bar.
    /// </summary>
    private void SetBarPercent(RectTransform rect, float percent)
    {
        percent = Mathf.Clamp01(percent);
        rect.anchorMax = new Vector2(percent, rect.anchorMax.y);
    }

    // ====================================================
    // UI CONSTRUCTION — all built in code, no prefab needed
    // ====================================================

    private void CreateHealthBarUI()
    {
        float barWidthPixels = barWidth / canvasScale;
        float barHeightPixels = barHeight / canvasScale;

        // ====== CANVAS ======
        GameObject canvasObj = new GameObject("EnemyHealthBar_Canvas");
        canvasObj.transform.SetParent(transform);
        canvasObj.transform.localPosition = new Vector3(0f, heightAboveEnemy, 0f);
        canvasObj.transform.localScale = Vector3.one * canvasScale;

        canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 100;

        canvasGroup = canvasObj.AddComponent<CanvasGroup>();
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        RectTransform canvasRect = canvas.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(barWidthPixels, barHeightPixels);

        // ====== CONTAINER (parent for all bar layers) ======
        GameObject containerObj = new GameObject("BarContainer");
        containerObj.transform.SetParent(canvasObj.transform, false);
        RectTransform containerRect = containerObj.AddComponent<RectTransform>();
        containerRect.anchorMin = Vector2.zero;
        containerRect.anchorMax = Vector2.one;
        containerRect.sizeDelta = Vector2.zero;
        containerRect.offsetMin = Vector2.zero;
        containerRect.offsetMax = Vector2.zero;

        // ====== GLOW (behind everything, slightly larger) ======
        float glowPadding = barHeightPixels * 0.8f;
        glowImage = CreateFixedImage("Glow", containerObj.transform,
            barWidthPixels + glowPadding * 2f,
            barHeightPixels + glowPadding * 2f,
            glowColor);

        // ====== BACKGROUND ======
        backgroundImage = CreateFixedImage("Background", containerObj.transform,
            barWidthPixels, barHeightPixels, backgroundColor);

        // ====== TRAIL FILL (amber bar that catches up slowly) ======
        trailImage = CreateStretchedBar("Trail", containerObj.transform, trailColor);
        trailRect = trailImage.rectTransform;

        // ====== HEALTH FILL (golden bar that drops instantly) ======
        healthImage = CreateStretchedBar("Health", containerObj.transform, healthColor);
        healthRect = healthImage.rectTransform;
    }

    /// <summary>
    /// Creates a fixed-size image (used for background and glow).
    /// </summary>
    private Image CreateFixedImage(string name, Transform parent, float width, float height, Color color)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);

        RectTransform rect = obj.AddComponent<RectTransform>();
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(width, height);

        Image img = obj.AddComponent<Image>();
        img.color = color;

        return img;
    }

    /// <summary>
    /// Creates an anchor-stretched bar (used for health and trail).
    /// Anchored left-to-right inside the container so we can
    /// control width by changing anchorMax.x (1=full, 0=empty).
    /// </summary>
    private Image CreateStretchedBar(string name, Transform parent, Color color)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);

        RectTransform rect = obj.AddComponent<RectTransform>();
        // Stretch to fill parent: anchors from (0,0) to (1,1)
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(1f, 1f);
        // Zero offset = exactly matches parent size
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Image img = obj.AddComponent<Image>();
        img.color = color;

        return img;
    }
}