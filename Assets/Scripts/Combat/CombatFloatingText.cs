using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Flying combat text: the combo counter over Yoru (x1, x2, x3) and the damage numbers over
/// whatever she hits. One component, on PlayerYoru_1.1. Builds its text objects at runtime from
/// code, no prefab, no canvas, and pools them so a long fight allocates nothing.
///
/// Two entry points, both safe to call when no instance exists:
///   CombatFloatingText.ShowCombo(step, worldPosition)   from PlayerCombat, every swing
///   damage numbers subscribe themselves to EnemyHealth.AnyDamaged and need no caller
/// The momentum rank, when it comes, reuses ShowCombo with its own label.
/// </summary>
public class CombatFloatingText : MonoBehaviour
{
    #region Inspector
    [Header("Font")]
    [Tooltip("Font for every flying text. Empty = the TextMesh Pro default. YujiSyuku-Regular SDF in Assets/Fonts is the brush face already in the project.")]
    [SerializeField] private TMP_FontAsset font;

    [Header("Combo Counter (screen corner)")]
    [Tooltip("Show the plain combo step (x1 x2 x3) when no Combat Momentum component is running. With momentum running, the count takes the corner instead.")]
    [SerializeField] private bool showComboText = true;
    [Tooltip("{0} is the number. x{0} gives x1, x2, x3.")]
    [SerializeField] private string comboFormat = "x{0}";
    [SerializeField] private Color comboColor = new Color(0.35f, 0.75f, 1f, 1f);
    [Tooltip("Size of the combo text. It floats a fixed distance in front of the camera, so this reads the same at any range.")]
    [SerializeField] private float comboSize = 0.03f;
    [Tooltip("The finisher (step 3), or a momentum milestone, is drawn this many times bigger.")]
    [SerializeField] private float comboFinisherScale = 1.5f;
    [Tooltip("Where on the screen the combo text is born. 0,0 = bottom left, 1,1 = top right. 0.86, 0.68 is the upper right, clear of her and the boss bar. Swap x to 0.14 for the left side.")]
    [SerializeField] private Vector2 comboScreenAnchor = new Vector2(0.86f, 0.68f);
    [Tooltip("Metres in front of the camera the combo text floats. Close, so it can never sit inside a wall.")]
    [SerializeField] private float comboScreenDepth = 1.5f;
    [Tooltip("How far up the screen it drifts over its life, as a fraction of the screen height.")]
    [SerializeField] private float comboScreenRise = 0.05f;

    [Header("Combo Camera Kick")]
    [Tooltip("Camera shake on every combo step, through Camera Game Feel. 0 = none. Hits land at 0.18 to 0.5 for scale; keep this well under.")]
    [SerializeField] private float comboShakeIntensity = 0.06f;
    [SerializeField] private float comboShakeDuration = 0.08f;
    [Tooltip("Shake on the finisher or a momentum milestone instead of the value above.")]
    [SerializeField] private float comboFinisherShakeIntensity = 0.14f;

    [Header("Damage Numbers (over the enemy)")]
    [SerializeField] private bool showDamageNumbers = true;
    [SerializeField] private Color damageColor = new Color(1f, 1f, 1f, 1f);
    [SerializeField] private Color heavyDamageColor = new Color(0.55f, 0.85f, 1f, 1f);
    [SerializeField] private float damageSize = 0.35f;
    [Tooltip("Heavy hits are drawn this many times bigger.")]
    [SerializeField] private float heavyDamageScale = 1.4f;
    [Tooltip("Metres above the enemy's feet the number is born. The Oni is tall; 1.8 puts it at his chest.")]
    [SerializeField] private float damageHeight = 1.8f;
    [Tooltip("Random sideways spread in metres so rapid hits do not stack on one spot.")]
    [SerializeField] private float damageJitter = 0.5f;

    [Header("Motion")]
    [Tooltip("Seconds a text lives before it is fully faded and returned to the pool.")]
    [SerializeField] private float lifetime = 0.7f;
    [Tooltip("Metres per second the text drifts upward.")]
    [SerializeField] private float riseSpeed = 1.2f;
    [Tooltip("Starting scale of the pop. 1.5 = born half again too big and settling to size in Pop Time.")]
    [SerializeField] private float popScale = 2.2f;
    [SerializeField] private float popTime = 0.09f;
    [Tooltip("Fraction of Lifetime spent fully visible before the fade starts.")]
    [Range(0f, 1f)]
    [SerializeField] private float holdFraction = 0.45f;

    [Header("Look")]
    [Tooltip("Outline thickness, 0 to 1. 0.2 reads on any background.")]
    [Range(0f, 1f)]
    [SerializeField] private float outlineWidth = 0.2f;
    [SerializeField] private Color outlineColor = new Color(0.02f, 0.02f, 0.08f, 1f);
    [Tooltip("Glow strength, 0 = none. Uses the text's own colour.")]
    [Range(0f, 1f)]
    [SerializeField] private float glowPower = 0.35f;
    [Tooltip("How far the glow reaches, 0 to 1.")]
    [Range(0f, 1f)]
    [SerializeField] private float glowOuter = 0.4f;
    #endregion

    #region State
    private static CombatFloatingText instance;
    private readonly List<TextMeshPro> pool = new List<TextMeshPro>();
    private Transform texts;
    private Camera cam;
    #endregion

    #region Public API
    /// <summary>Combo step text over Yoru. Safe with no instance in the scene.</summary>
    public static void ShowCombo(int step, Vector3 yoruPosition)
    {
        if (instance == null || !instance.showComboText) return;
        if (CombatMomentum.Active) return;   // the momentum count owns the corner when it is running
        instance.Corner(string.Format(instance.comboFormat, step), step >= 3);
    }

    /// <summary>The momentum count, x1, x2, x3 and on. Milestones pop bigger with a bigger kick.</summary>
    public static void ShowMomentum(int count, bool milestone)
    {
        if (instance == null) return;
        instance.Corner(string.Format(instance.comboFormat, count), milestone);
    }

    /// <summary>One corner text with its camera kick. 'big' = the finisher / milestone treatment.</summary>
    private void Corner(string text, bool big)
    {
        Spawn(text, Vector3.zero, comboColor, comboSize * (big ? comboFinisherScale : 1f), comboScreenAnchor);
        float kick = big ? comboFinisherShakeIntensity : comboShakeIntensity;
        if (kick > 0.001f && CameraGameFeel.Instance != null)
            CameraGameFeel.Instance.Shake(kick, comboShakeDuration);
    }

    /// <summary>Any text at a world position. For the momentum rank later, or anything else.</summary>
    public static void Show(string text, Vector3 worldPosition, Color color, float size)
    {
        if (instance == null) return;
        instance.Spawn(text, worldPosition, color, size, null);
    }

    /// <summary>Any text pinned to a screen position (0..1, bottom left to top right).</summary>
    public static void ShowOnScreen(string text, Vector2 screenAnchor, Color color, float size)
    {
        if (instance == null) return;
        instance.Spawn(text, Vector3.zero, color, size, screenAnchor);
    }
    #endregion

    #region Lifecycle
    private void Awake()
    {
        instance = this;
        cam = Camera.main;
        texts = new GameObject("FloatingTexts").transform;   // scene level, so nothing inherits her scale or her spin
    }

    private void OnEnable()  { EnemyHealth.AnyDamaged += OnEnemyDamaged; }
    private void OnDisable() { EnemyHealth.AnyDamaged -= OnEnemyDamaged; }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
        if (texts != null) Destroy(texts.gameObject);
    }

    private void OnEnemyDamaged(EnemyHealth enemy, int damage, bool isHeavy)
    {
        if (!showDamageNumbers || enemy == null || damage <= 0) return;
        Vector2 j = Random.insideUnitCircle * damageJitter;
        Vector3 at = enemy.transform.position + new Vector3(j.x, damageHeight, j.y);
        Spawn(damage.ToString(),
              at,
              isHeavy ? heavyDamageColor : damageColor,
              damageSize * (isHeavy ? heavyDamageScale : 1f),
              null);
    }
    #endregion

    #region Spawn and animate
    /// <summary>screenAnchor null = world text at 'at' (damage numbers). Set = pinned to that
    /// screen spot in front of the camera, 'at' ignored (combo text).</summary>
    private void Spawn(string text, Vector3 at, Color color, float size, Vector2? screenAnchor)
    {
        TextMeshPro tmp = Take();
        tmp.text = text;
        tmp.color = color;
        tmp.fontSize = 10f;                      // fixed; Size scales the object, so the fields are linear
        tmp.transform.position = screenAnchor.HasValue ? ScreenPoint(screenAnchor.Value, 0f) : at;
        tmp.transform.localScale = Vector3.one * (size * popScale);
        StopCoroutineFor(tmp);
        ApplyLook(tmp, color);
        tmp.gameObject.SetActive(true);
        running[tmp] = StartCoroutine(Animate(tmp, size, screenAnchor));
    }

    private Vector3 ScreenPoint(Vector2 anchor, float rise)
    {
        if (cam == null) cam = Camera.main;
        if (cam == null) return Vector3.zero;
        return cam.ViewportToWorldPoint(new Vector3(anchor.x, anchor.y + rise, comboScreenDepth));
    }

    private readonly Dictionary<TextMeshPro, Coroutine> running = new Dictionary<TextMeshPro, Coroutine>();

    private void StopCoroutineFor(TextMeshPro tmp)
    {
        if (running.TryGetValue(tmp, out Coroutine c) && c != null) StopCoroutine(c);
        running.Remove(tmp);
    }

    private IEnumerator Animate(TextMeshPro tmp, float size, Vector2? screenAnchor)
    {
        Transform t = tmp.transform;
        Color c = tmp.color;
        float born = Time.time;
        float holdUntil = born + lifetime * holdFraction;
        float end = born + lifetime;

        while (Time.time < end && tmp.gameObject.activeSelf)
        {
            float age = Time.time - born;

            float pop = popTime > 0.001f ? Mathf.Clamp01(age / popTime) : 1f;
            float scale = Mathf.Lerp(popScale, 1f, 1f - (1f - pop) * (1f - pop));   // ease out
            t.localScale = Vector3.one * (size * scale);

            if (screenAnchor.HasValue)
                t.position = ScreenPoint(screenAnchor.Value, comboScreenRise * Mathf.Clamp01(age / lifetime));   // rides the camera
            else
                t.position += Vector3.up * (riseSpeed * Time.deltaTime);
            if (cam != null) t.rotation = cam.transform.rotation;                 // billboard

            float fade = Time.time <= holdUntil ? 1f : Mathf.Clamp01((end - Time.time) / Mathf.Max(0.01f, end - holdUntil));
            tmp.color = new Color(c.r, c.g, c.b, c.a * fade);

            yield return null;
        }

        tmp.gameObject.SetActive(false);
        running.Remove(tmp);
    }
    #endregion

    #region Pool and look
    private TextMeshPro Take()
    {
        foreach (TextMeshPro p in pool)
            if (!p.gameObject.activeSelf) return p;

        GameObject go = new GameObject("FloatingText");
        go.transform.SetParent(texts, false);
        TextMeshPro tmp = go.AddComponent<TextMeshPro>();
        if (font != null) tmp.font = font;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.sortingOrder = 100;
        go.SetActive(false);
        pool.Add(tmp);
        return tmp;
    }

    private static readonly int OutlineWidthId = Shader.PropertyToID("_OutlineWidth");
    private static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");
    private static readonly int GlowColorId    = Shader.PropertyToID("_GlowColor");
    private static readonly int GlowPowerId    = Shader.PropertyToID("_GlowPower");
    private static readonly int GlowOuterId    = Shader.PropertyToID("_GlowOuter");

    /// <summary>Outline and glow on the text's own material instance. TMP's SDF shader needs the
    /// keywords on for the properties to do anything.</summary>
    private void ApplyLook(TextMeshPro tmp, Color color)
    {
        Material m = tmp.fontMaterial;        // per-object instance, never the shared asset
        if (m == null) return;

        if (outlineWidth > 0.001f)
        {
            m.EnableKeyword("OUTLINE_ON");
            m.SetFloat(OutlineWidthId, outlineWidth);
            m.SetColor(OutlineColorId, outlineColor);
        }
        else m.DisableKeyword("OUTLINE_ON");

        if (glowPower > 0.001f)
        {
            m.EnableKeyword("GLOW_ON");
            m.SetColor(GlowColorId, color);
            m.SetFloat(GlowPowerId, glowPower);
            m.SetFloat(GlowOuterId, glowOuter);
        }
        else m.DisableKeyword("GLOW_ON");
    }
    #endregion
}
