using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Yoru's game over screen. Lives on the GameOverScreen prefab (its own overlay canvas) and draws
/// itself at runtime, so the prefab holds nothing but the font, the red edge sprite and the dials.
///
/// The order (Hazel, 20 Sep 2026, re-timed the same night after her first test):
///   1. The killing hit. PlayerCombat slows the world and the peach row holds its red bleed at full.
///      This screen does nothing until her DEATH ANIMATION HAS FULLY FINISHED (PlayerCombat.
///      IsDeathClipFinished), then waits Delay After Death Clip more on her still body. The first
///      version went black 1.5 s after the hit and hid the whole death animation.
///   2. Black fades in SLOWLY over the whole screen, with the red edges on top (black and red together).
///   3. GAME OVER fades in, slowly.
///   4. Replay and Main Menu fade in. Replay reloads the scene she died in. Main Menu stays greyed
///      out until a scene name is typed in Main Menu Scene.
///
/// Player data: players cut dead time after a death whenever they can (Elden Ring's Faster Respawn
/// mod removes 3 to 4 s and has about 2,000 endorsements; Bloodborne's reload was patched from 45 s
/// to about 12 s). The slow, full sequence is the director's cut; for the player who has died ten
/// times, any key jumps straight to the buttons from Skip Allowed After on, and Replay is already
/// selected for Enter or Space.
///
/// Everything runs on real time: the death slow motion never stretches it.
/// It watches PlayerCombat.IsDead(), so no other script has to call it.
/// </summary>
public class GameOverController : MonoBehaviour
{
    [Header("Timing (real seconds, the death slow motion does not stretch them)")]
    [Tooltip("The screen first waits until her death animation has FULLY finished, then this long again on her still body, and only then the black starts. The world runs at Death Slow End Scale (0.5) meanwhile, so a 3 s death clip takes about 7 s real.")]
    [SerializeField] private float delayAfterDeathClip = 1f;
    [Tooltip("Safety net: if the death animation never reports its end, the black starts this long after the killing hit anyway.")]
    [SerializeField] private float maxWaitForDeathClip = 12f;
    [Tooltip("Black and the red edges fade in together over this long. Slow on purpose.")]
    [SerializeField] private float blackFadeSeconds = 3f;
    [Tooltip("GAME OVER fades in over this long, right after the black is full.")]
    [SerializeField] private float titleFadeSeconds = 2f;
    [Tooltip("Pause between the title being fully in and the buttons starting to appear.")]
    [SerializeField] private float buttonsDelay = 0.5f;
    [Tooltip("Replay and Main Menu fade in over this long. They can be used as soon as they start to appear.")]
    [SerializeField] private float buttonsFadeSeconds = 1.5f;
    [Tooltip("ON = any key or click jumps straight to the finished screen with the buttons ready. For the player who has died many times and only wants Replay.")]
    [SerializeField] private bool skipWithAnyKey = true;
    [Tooltip("Real seconds after the killing hit before the skip above starts to listen, so the last attack mash of a dying player skips nothing.")]
    [SerializeField] private float skipAllowedAfter = 1.5f;
    [Tooltip("Buttons ignore presses for this long after they appear, so the key that skipped the fade cannot also press Replay by accident.")]
    [SerializeField] private float buttonsGuardSeconds = 0.25f;

    [Header("Black and red")]
    [Tooltip("How dark the screen gets. 1 = fully black.")]
    [Range(0f, 1f)] [SerializeField] private float blackAlpha = 1f;
    [Tooltip("Full screen sprite for the red edges (white with a transparent middle). The same sprite the peach row uses for its low health bleed: Assets/Peaches/peach_low_vignette.")]
    [SerializeField] private Sprite redEdgeSprite;
    [Tooltip("Colour of the red edges. Keep it the same as PeachHealthHUD > Peach Health UI > Low Health > Vignette Color so the bleed hands over to this screen without a jump.")]
    [SerializeField] private Color redEdgeColor = new Color(0.85f, 0.05f, 0.08f, 1f);
    [Tooltip("Strength of the red edges on top of the black (0 to 1).")]
    [Range(0f, 1f)] [SerializeField] private float redEdgeAlpha = 0.9f;

    [Header("Title")]
    [Tooltip("The font for every word on this screen. Assets/Fonts/YujiSyuku-Regular SDF.")]
    [SerializeField] private TMP_FontAsset font;
    [SerializeField] private string titleText = "GAME OVER";
    [Tooltip("Title size in pixels at the canvas reference resolution (1920 x 1080).")]
    [SerializeField] private float titleSize = 130f;
    [SerializeField] private Color titleColor = new Color(0.80f, 0.08f, 0.09f, 1f);
    [Tooltip("Space between the letters of the title (TextMeshPro character spacing).")]
    [SerializeField] private float titleLetterSpacing = 12f;
    [Tooltip("Height of the title above the middle of the screen, in pixels.")]
    [SerializeField] private float titleY = 110f;

    [Header("Buttons")]
    [SerializeField] private string replayText = "Replay";
    [SerializeField] private string mainMenuText = "Main Menu";
    [Tooltip("Scene the Main Menu button loads. EMPTY = the button shows greyed out and cannot be pressed. Type the scene's name here once a main menu scene exists (it must be in File > Build Profiles > Scene List).")]
    [SerializeField] private string mainMenuScene = "";
    [Tooltip("Button text size in pixels at 1920 x 1080.")]
    [SerializeField] private float buttonSize = 46f;
    [SerializeField] private Color buttonColor = new Color(0.93f, 0.89f, 0.82f, 1f);
    [Tooltip("Colour of the button under the mouse or picked with the keys.")]
    [SerializeField] private Color buttonSelectedColor = new Color(0.95f, 0.16f, 0.14f, 1f);
    [Tooltip("How faint a button that cannot be pressed looks (0 to 1).")]
    [Range(0f, 1f)] [SerializeField] private float disabledAlpha = 0.35f;
    [Tooltip("Height of the first button relative to the middle of the screen, in pixels (negative = below).")]
    [SerializeField] private float buttonsY = -90f;
    [Tooltip("Distance between the two buttons, in pixels.")]
    [SerializeField] private float buttonGap = 78f;

    [Header("After Replay")]
    [Tooltip("ON = after Replay the new scene starts under black, so the second of bald fur while XFur builds is never seen. The full loading screen replaces this later.")]
    [SerializeField] private bool coverAfterReplay = true;
    [Tooltip("Seconds the black stays fully closed after the scene has loaded. XFur needs about a second.")]
    [SerializeField] private float coverHoldSeconds = 1.2f;
    [Tooltip("Seconds the black takes to open onto the scene.")]
    [SerializeField] private float coverFadeSeconds = 0.6f;

    // Survive the reload: tell the new scene's screen that it was reached by Replay, and what the
    // real physics step is, so the clock can be put back exactly.
    private static bool arrivedByReplay;
    private static float baseFixedDeltaTime;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewSession()
    {
        arrivedByReplay = false;
        baseFixedDeltaTime = 0f;
    }

    private PlayerCombat playerCombat;
    private GameObject screenRoot;
    private Image black;
    private Image redEdges;
    private TextMeshProUGUI title;
    private CanvasGroup buttonsGroup;
    private Button replayButton;
    private Button mainMenuButton;
    private Image startCover;

    private bool sequenceStarted;
    private bool screenComplete;
    private bool skipRequested;
    private float deathRealTime;
    private bool menuRegistered;
    private bool leaving;

    private void Awake()
    {
        if (baseFixedDeltaTime <= 0f)
            baseFixedDeltaTime = Time.fixedDeltaTime / Mathf.Max(0.01f, Time.timeScale);

        if (arrivedByReplay)
        {
            // Belt and braces: Replay already reset the clock, but nothing may carry a slow world into the new attempt.
            Time.timeScale = 1f;
            Time.fixedDeltaTime = baseFixedDeltaTime;
        }

        BuildScreen();

        if (arrivedByReplay && coverAfterReplay)
            StartCoroutine(OpenStartCover());
        arrivedByReplay = false;
    }

    private void Start()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        playerCombat = player != null ? player.GetComponent<PlayerCombat>() : null;
        Debug.Log($"[GameOver] ready: PlayerCombat {(playerCombat != null ? "found" : "NOT FOUND, this screen will never show")}, font {(font != null ? font.name : "MISSING")}, red edge sprite {(redEdgeSprite != null ? "set" : "MISSING")}, Main Menu {(string.IsNullOrEmpty(mainMenuScene) ? "greyed out (no scene set)" : "-> " + mainMenuScene)}");
    }

    private void Update()
    {
        if (!sequenceStarted)
        {
            if (playerCombat != null && playerCombat.IsDead())
            {
                sequenceStarted = true;
                deathRealTime = Time.unscaledTime;
                StartCoroutine(Sequence());
            }
            return;
        }

        if (skipWithAnyKey && !skipRequested && !screenComplete
            && Time.unscaledTime - deathRealTime >= skipAllowedAfter && Input.anyKeyDown)
        {
            skipRequested = true;
            Debug.Log($"[GameOver] skipped by a key {Time.unscaledTime - deathRealTime:F1}s after the hit: straight to the buttons");
        }
    }

    // ---------- the sequence ----------

    private IEnumerator Sequence()
    {
        Debug.Log($"[GameOver] Yoru died: waiting for the death animation to finish, then {delayAfterDeathClip:F1}s, then black {blackFadeSeconds:F1}s, title {titleFadeSeconds:F1}s, buttons {buttonsFadeSeconds:F1}s");

        // The camera stops taking the mouse the moment she dies (it keeps following her).
        ThirdPersonCamera cam = FindFirstObjectByType<ThirdPersonCamera>();
        if (cam != null) cam.SetCameraEnabled(false);

        // 1. The killing hit and her whole death animation own the screen: slow motion, the red bleed,
        //    the fall. Nothing of this screen shows until the clip has played to its last frame.
        float waited = 0f;
        while (!playerCombat.IsDeathClipFinished() && waited < maxWaitForDeathClip && !skipRequested)
        {
            waited += Time.unscaledDeltaTime;
            yield return null;
        }
        Debug.Log(waited >= maxWaitForDeathClip
            ? $"[GameOver] the death animation never reported its end: going on after {waited:F1}s (Max Wait For Death Clip)"
            : $"[GameOver] death animation finished {waited:F1}s after the hit (world at x{Time.timeScale:F2}); black starts in {delayAfterDeathClip:F1}s");

        //    Then a quiet beat on her still body.
        yield return WaitReal(delayAfterDeathClip);

        // 2. Black and red together, slowly.
        screenRoot.SetActive(true);
        black.raycastTarget = true;          // nothing under this screen can be clicked any more
        yield return Fade(blackFadeSeconds, k =>
        {
            SetAlpha(black, blackAlpha * k);
            SetAlpha(redEdges, redEdgeAlpha * k);
        });

        // The world is hidden now: close the other menus' doors (inventory, parchments) the same way every menu does.
        if (!menuRegistered) { MenuGuard.Register(); menuRegistered = true; }

        // 3. The title.
        yield return Fade(titleFadeSeconds, k => title.alpha = k);

        // 4. The buttons.
        yield return WaitReal(buttonsDelay);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        StartCoroutine(ArmButtons());
        yield return Fade(buttonsFadeSeconds, k => buttonsGroup.alpha = k);
        screenComplete = true;
        Debug.Log($"[GameOver] screen complete {Time.unscaledTime - deathRealTime:F1}s after the hit, waiting for Replay or Main Menu");
    }

    private IEnumerator ArmButtons()
    {
        float t = 0f;
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);   // nothing left selected from another menu
        while (t < buttonsGuardSeconds) { t += Time.unscaledDeltaTime; yield return null; }
        buttonsGroup.blocksRaycasts = true;
        if (EventSystem.current != null && replayButton != null)
            EventSystem.current.SetSelectedGameObject(replayButton.gameObject);
    }

    private IEnumerator WaitReal(float seconds)
    {
        float t = 0f;
        while (t < seconds && !skipRequested) { t += Time.unscaledDeltaTime; yield return null; }
    }

    private IEnumerator Fade(float seconds, System.Action<float> apply)
    {
        float t = 0f;
        float length = Mathf.Max(0.01f, seconds);
        while (t < length && !skipRequested)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / length);
            apply(k * k * (3f - 2f * k));   // smooth ease
            yield return null;
        }
        apply(1f);
    }

    // ---------- the buttons ----------

    private void Replay()
    {
        if (leaving) return;
        leaving = true;
        Scene scene = SceneManager.GetActiveScene();
        Debug.Log($"[GameOver] Replay: reloading '{scene.name}'");
        if (OniDebugLogFile.IsOpen) OniDebugLogFile.Marker("REPLAY: scene reloaded");
        PutTheWorldBack();
        arrivedByReplay = true;
#if UNITY_EDITOR
        // In the editor the scene is loaded by its path, so Replay works even while the scene is not
        // in File > Build Profiles > Scene List.
        UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(scene.path, new LoadSceneParameters(LoadSceneMode.Single));
#else
        SceneManager.LoadScene(scene.buildIndex);
#endif
    }

    private void GoToMainMenu()
    {
        if (leaving || string.IsNullOrEmpty(mainMenuScene)) return;
        if (!Application.CanStreamedLevelBeLoaded(mainMenuScene))
        {
            Debug.LogError($"[GameOver] Main Menu: the scene '{mainMenuScene}' is not in File > Build Profiles > Scene List, so it cannot be loaded. Add it there, or fix the name in GameOverScreen > Game Over Controller > Buttons > Main Menu Scene.");
            return;
        }
        leaving = true;
        Debug.Log($"[GameOver] Main Menu: loading '{mainMenuScene}'");
        PutTheWorldBack();
        SceneManager.LoadScene(mainMenuScene);
    }

    /// <summary>The clock, the menu lock and the death slow motion are all handed back before a scene is left.</summary>
    private void PutTheWorldBack()
    {
        if (playerCombat != null) playerCombat.ReleaseDeathClock();
        if (menuRegistered) { MenuGuard.Unregister(); menuRegistered = false; }
        Time.timeScale = 1f;
        Time.fixedDeltaTime = baseFixedDeltaTime;
    }

    private void OnDestroy()
    {
        // Play stopped or the scene changed some other way while this screen held the menu lock.
        if (menuRegistered) { MenuGuard.Unregister(); menuRegistered = false; }
    }

    // ---------- after Replay: the scene opens from black ----------

    private IEnumerator OpenStartCover()
    {
        startCover.gameObject.SetActive(true);
        SetAlpha(startCover, 1f);
        float t = 0f;
        while (t < coverHoldSeconds) { t += Time.unscaledDeltaTime; yield return null; }
        t = 0f;
        float length = Mathf.Max(0.01f, coverFadeSeconds);
        while (t < length)
        {
            t += Time.unscaledDeltaTime;
            SetAlpha(startCover, 1f - Mathf.Clamp01(t / length));
            yield return null;
        }
        startCover.gameObject.SetActive(false);
    }

    // ---------- building the screen ----------

    private void BuildScreen()
    {
        if (screenRoot != null) return;

        if (EventSystem.current == null && FindFirstObjectByType<EventSystem>() == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

        screenRoot = new GameObject("Screen", typeof(RectTransform));
        screenRoot.transform.SetParent(transform, false);
        Stretch(screenRoot.GetComponent<RectTransform>());

        black = NewImage("Black", screenRoot.transform, null, Color.black);
        redEdges = NewImage("RedEdges", screenRoot.transform, redEdgeSprite, redEdgeColor);
        redEdges.enabled = redEdgeSprite != null;

        title = NewText("Title", screenRoot.transform, titleText, titleSize, titleColor);
        title.characterSpacing = titleLetterSpacing;
        RectTransform tr = title.rectTransform;
        tr.anchorMin = tr.anchorMax = tr.pivot = new Vector2(0.5f, 0.5f);
        tr.sizeDelta = new Vector2(1800f, titleSize * 1.5f);
        tr.anchoredPosition = new Vector2(0f, titleY);
        title.alpha = 0f;

        GameObject group = new GameObject("Buttons", typeof(RectTransform), typeof(CanvasGroup));
        group.transform.SetParent(screenRoot.transform, false);
        Stretch(group.GetComponent<RectTransform>());
        buttonsGroup = group.GetComponent<CanvasGroup>();
        buttonsGroup.alpha = 0f;
        // Interactable from the start, so the words fade in with their real colours (a group that is
        // not interactable paints every button in its greyed out colour). Until ArmButtons runs the
        // mouse passes through and nothing is selected, so no press can land.
        buttonsGroup.interactable = true;
        buttonsGroup.blocksRaycasts = false;

        replayButton = NewButton("Replay", replayText, buttonsY, Replay);
        mainMenuButton = NewButton("MainMenu", mainMenuText, buttonsY - buttonGap, GoToMainMenu);
        mainMenuButton.interactable = !string.IsNullOrEmpty(mainMenuScene);

        // Up and down move between the two; a greyed out Main Menu is skipped.
        Navigation nav = new Navigation { mode = Navigation.Mode.Explicit };
        if (mainMenuButton.interactable) { nav.selectOnDown = mainMenuButton; nav.selectOnUp = mainMenuButton; }
        replayButton.navigation = nav;
        mainMenuButton.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnUp = replayButton, selectOnDown = replayButton };

        SetAlpha(black, 0f);
        SetAlpha(redEdges, 0f);
        screenRoot.SetActive(false);

        startCover = NewImage("StartCover", transform, null, Color.black);
        startCover.gameObject.SetActive(false);
    }

    private Image NewImage(string name, Transform parent, Sprite sprite, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        Stretch(go.GetComponent<RectTransform>());
        Image image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private TextMeshProUGUI NewText(string name, Transform parent, string text, float size, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        TextMeshProUGUI tmp = go.GetComponent<TextMeshProUGUI>();
        if (font != null) tmp.font = font;
        tmp.text = text;
        tmp.fontSize = size;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.raycastTarget = false;
        return tmp;
    }

    private Button NewButton(string name, string label, float y, UnityEngine.Events.UnityAction onClick)
    {
        // An invisible box takes the mouse; the word itself carries the colour.
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(SelectOnHover));
        go.transform.SetParent(buttonsGroup.transform, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(560f, buttonSize * 1.5f);
        rt.anchoredPosition = new Vector2(0f, y);

        Image hit = go.GetComponent<Image>();
        hit.color = new Color(1f, 1f, 1f, 0f);
        hit.raycastTarget = true;

        TextMeshProUGUI text = NewText("Label", go.transform, label, buttonSize, Color.white);
        Stretch(text.rectTransform);

        Button button = go.GetComponent<Button>();
        button.targetGraphic = text;
        ColorBlock colors = button.colors;
        colors.normalColor = buttonColor;
        colors.highlightedColor = buttonSelectedColor;
        colors.selectedColor = buttonSelectedColor;
        colors.pressedColor = Color.Lerp(buttonSelectedColor, Color.black, 0.25f);
        colors.disabledColor = new Color(buttonColor.r, buttonColor.g, buttonColor.b, disabledAlpha);
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.08f;
        button.colors = colors;
        button.onClick.AddListener(onClick);
        return button;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }

    private static void SetAlpha(Graphic graphic, float alpha)
    {
        if (graphic == null) return;
        Color c = graphic.color;
        c.a = alpha;
        graphic.color = c;
    }

    /// <summary>The mouse and the keys share one highlight: pointing at a button selects it.</summary>
    private class SelectOnHover : MonoBehaviour, IPointerEnterHandler
    {
        public void OnPointerEnter(PointerEventData eventData)
        {
            Selectable s = GetComponent<Selectable>();
            if (s != null && s.IsInteractable() && EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(gameObject);
        }
    }
}
