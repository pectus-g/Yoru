using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Yoru's game over screen, SUMI-E look (Hazel's pick, 20 Sep 2026: "dramatic, very modern, transparent").
/// Lives on the GameOverScreen prefab (its own overlay canvas) and draws itself at runtime, so the prefab
/// holds nothing but the sprites, the font, the shader and the dials.
///
/// The order, all in real time (the death slow motion never stretches it):
///   1. The killing hit. PlayerCombat slows the world, the peach row holds its red bleed at full, and her
///      DEATH ANIMATION PLAYS TO ITS LAST FRAME (PlayerCombat.IsDeathClipFinished). Then one quiet beat.
///   2. The world freezes: one frame of the game camera is kept (the HUD is not in it) and takes the
///      place of the live picture, with the same red edges laid over it, so nothing seems to change.
///   3. NO BLACK. The frozen world turns into an ink painting on paper (shader "YORU/UI/SumiE World")
///      while the red edges let go: grey washes, the picture fading out toward the paper's edge, a band
///      of paper mist where the words will sit.
///   4. The red kanji (the end), the brush line and GAME OVER appear.
///   5. REPLAY and MAIN MENU appear on one line. Two red dots mark the picked one (the dots of the Oni's
///      bar). Replay reloads the scene she died in. Main Menu stays faint and dead until a scene name is
///      typed in Main Menu Scene.
///
/// Player data: players cut dead time after a death whenever they can (Elden Ring's Faster Respawn mod
/// removes 3 to 4 s and has about 2,000 endorsements; Bloodborne's reload was patched from 45 s to about
/// 12 s). The slow full sequence is the director's cut; any key from Skip Allowed After on jumps straight
/// to the finished screen, and Replay is already picked for Enter or Space.
///
/// It watches PlayerCombat.IsDead(), so no other script has to call it.
/// </summary>
public class GameOverController : MonoBehaviour
{
    public enum CaptureMode
    {
        [Tooltip("The game camera draws one extra frame into a texture: the world only, no HUD in it.")]
        CameraRender,
        [Tooltip("Copies the finished screen, HUD and red edges included. Use only if Camera Render looks wrong.")]
        ScreenGrab
    }

    [Header("Timing (real seconds, the death slow motion does not stretch them)")]
    [Tooltip("The screen first waits until her death animation has FULLY finished, then this long again on her still body.")]
    [SerializeField] private float delayAfterDeathClip = 1f;
    [Tooltip("Safety net: if the death animation never reports its end, the screen goes on this long after the killing hit anyway.")]
    [SerializeField] private float maxWaitForDeathClip = 12f;
    [Tooltip("The frozen frame takes over from the live picture over this long. Short: nothing should seem to happen.")]
    [SerializeField] private float freezeFadeSeconds = 0.35f;
    [Tooltip("The frozen world turns into the ink painting over this long, and the red edges fade away with it.")]
    [SerializeField] private float inkFadeSeconds = 3f;
    [Tooltip("The kanji, the brush line and GAME OVER fade in over this long.")]
    [SerializeField] private float titleFadeSeconds = 2f;
    [Tooltip("Pause between the title being fully in and the buttons starting to appear.")]
    [SerializeField] private float buttonsDelay = 0.5f;
    [Tooltip("REPLAY and MAIN MENU fade in over this long.")]
    [SerializeField] private float buttonsFadeSeconds = 1.5f;
    [Tooltip("ON = any key or click jumps straight to the finished screen with the buttons ready. For the player who has died many times and only wants Replay.")]
    [SerializeField] private bool skipWithAnyKey = true;
    [Tooltip("Real seconds after the killing hit before the skip above starts to listen, so the last attack mash of a dying player skips nothing.")]
    [SerializeField] private float skipAllowedAfter = 1.5f;
    [Tooltip("Buttons ignore presses for this long after they appear, so the key that skipped the fades cannot also press Replay by accident.")]
    [SerializeField] private float buttonsGuardSeconds = 0.25f;

    [Header("The ink painting")]
    [Tooltip("Assets/UI/GameOver/SumiEWorld (shader YORU/UI/SumiE World). Empty or broken = plain paper instead of the painted world.")]
    [SerializeField] private Shader inkShader;
    [Tooltip("The paper the world is painted on. Assets/UI/GameOver/gameover_washi.")]
    [SerializeField] private Texture2D paperTexture;
    [SerializeField] private Color inkColor = new Color(0.085f, 0.08f, 0.085f, 1f);
    [Tooltip("How dark the painted world gets (0 to 1). Lower = paler, more paper.")]
    [Range(0f, 1f)] [SerializeField] private float inkStrength = 0.62f;
    [Tooltip("How soft the washes are. 2 is the designed look; 3 = mistier, 1 = more detail of the world survives.")]
    [Range(0f, 6f)] [SerializeField] private float washSoftness = 2f;
    [Tooltip("ON = the darkest and brightest parts of the frozen frame are measured and stretched to full ink and bare paper, so a dark cave and a bright field both paint well. OFF = use the two points below.")]
    [SerializeField] private bool autoLevels = true;
    [Tooltip("Auto Levels OFF only: frame brightness that becomes full ink (0 to 1).")]
    [Range(0f, 1f)] [SerializeField] private float blackPoint = 0.04f;
    [Tooltip("Auto Levels OFF only: frame brightness that becomes bare paper (0 to 1).")]
    [Range(0f, 1f)] [SerializeField] private float whitePoint = 0.6f;
    [Tooltip("The painting starts to fade toward the paper this far from the middle (0 = centre, 1 = the screen edge).")]
    [Range(0f, 1f)] [SerializeField] private float edgeFadeStart = 0.25f;
    [Tooltip("How much of the painting is gone at the corners (0 to 1).")]
    [Range(0f, 1f)] [SerializeField] private float edgeFade = 0.75f;
    [Tooltip("Strength of the band of paper mist behind the words (0 to 1).")]
    [Range(0f, 1f)] [SerializeField] private float mistStrength = 0.82f;
    [Tooltip("Height of the mist on the screen (0 = bottom, 1 = top).")]
    [Range(0f, 1f)] [SerializeField] private float mistCenterY = 0.444f;
    [Tooltip("Width and height of the mist, as parts of the screen.")]
    [SerializeField] private Vector2 mistSize = new Vector2(0.333f, 0.306f);
    [Tooltip("How the world is frozen. Camera Render keeps the HUD out of the painting.")]
    [SerializeField] private CaptureMode captureMode = CaptureMode.CameraRender;
    [Tooltip("Tick this only if the painted world shows upside down.")]
    [SerializeField] private bool flipCapturedFrame = false;

    [Header("Red edges (they fade away while the world turns to ink)")]
    [Tooltip("The same sprite the peach row uses for its low health bleed: Assets/Peaches/peach_low_vignette.")]
    [SerializeField] private Sprite redEdgeSprite;
    [Tooltip("Used only when no peach row is in the scene; otherwise its Vignette Color and Vignette Max are read, so the hand over has no jump.")]
    [SerializeField] private Color redEdgeColor = new Color(0.85f, 0.05f, 0.08f, 1f);
    [Range(0f, 1f)] [SerializeField] private float redEdgeAlpha = 0.85f;

    [Header("The mark")]
    [Tooltip("The kanji, a white sprite that is tinted below. Assets/UI/GameOver/gameover_kanji_owari.")]
    [SerializeField] private Sprite kanjiSprite;
    [SerializeField] private Color kanjiColor = new Color(0.85f, 0.05f, 0.08f, 0.95f);
    [Tooltip("Height of the kanji in pixels at 1920 x 1080. The width follows the sprite.")]
    [SerializeField] private float kanjiHeight = 261f;
    [Tooltip("Height of the kanji's middle above the middle of the screen, in pixels.")]
    [SerializeField] private float kanjiY = 150f;
    [Tooltip("The brush line under the kanji, a white sprite tinted with Ink Text Color. Assets/UI/GameOver/gameover_brush_line.")]
    [SerializeField] private Sprite brushLineSprite;
    [SerializeField] private float brushLineWidth = 438f;
    [SerializeField] private float brushLineY = -78f;
    [Tooltip("The font for every word on this screen. Assets/Fonts/YujiSyuku-Regular SDF.")]
    [SerializeField] private TMP_FontAsset font;
    [SerializeField] private string titleText = "GAME OVER";
    [SerializeField] private float titleSize = 30f;
    [Tooltip("Space between the letters (TextMeshPro character spacing; 75 = three quarters of a letter).")]
    [SerializeField] private float titleLetterSpacing = 75f;
    [SerializeField] private float titleY = -154f;
    [Tooltip("Colour of the words and the brush line: ink, never pure black.")]
    [SerializeField] private Color inkTextColor = new Color(0.085f, 0.08f, 0.085f, 1f);

    [Header("Buttons")]
    [SerializeField] private string replayText = "REPLAY";
    [SerializeField] private string mainMenuText = "MAIN MENU";
    [Tooltip("Scene the Main Menu button loads. EMPTY = the button shows faint and cannot be pressed. Type the scene's name here once a main menu scene exists (it must be in File > Build Profiles > Scene List).")]
    [SerializeField] private string mainMenuScene = "";
    [SerializeField] private float buttonSize = 36f;
    [SerializeField] private float buttonLetterSpacing = 34f;
    [Tooltip("Height of the button line relative to the middle of the screen, in pixels (negative = below).")]
    [SerializeField] private float buttonsY = -298f;
    [Tooltip("Empty space between the two words, in pixels.")]
    [SerializeField] private float buttonsGap = 150f;
    [Tooltip("The dot in front of the picked button, a white sprite tinted below. Assets/UI/GameOver/gameover_dot.")]
    [SerializeField] private Sprite dotSprite;
    [SerializeField] private Color dotColor = new Color(0.85f, 0.05f, 0.08f, 1f);
    [SerializeField] private float dotSize = 9f;
    [Tooltip("How strong a button that is NOT picked looks (0 to 1).")]
    [Range(0f, 1f)] [SerializeField] private float idleAlpha = 0.62f;
    [Tooltip("How faint a button that cannot be pressed looks (0 to 1).")]
    [Range(0f, 1f)] [SerializeField] private float disabledAlpha = 0.30f;

    [Header("After Replay")]
    [Tooltip("ON = after Replay the new scene starts under the paper, so the second of bald fur while XFur builds is never seen. The full loading screen replaces this later.")]
    [SerializeField] private bool coverAfterReplay = true;
    [Tooltip("Seconds the paper stays fully closed after the scene has loaded. XFur needs about a second.")]
    [SerializeField] private float coverHoldSeconds = 1.2f;
    [Tooltip("Seconds the paper takes to open onto the scene.")]
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

    private static readonly int InkId = Shader.PropertyToID("_Ink");
    private static readonly int SoftTexId = Shader.PropertyToID("_SoftTex");
    private static readonly int PaperTexId = Shader.PropertyToID("_PaperTex");
    private static readonly int InkColorId = Shader.PropertyToID("_InkColor");
    private static readonly int InkStrengthId = Shader.PropertyToID("_InkStrength");
    private static readonly int BlurMipId = Shader.PropertyToID("_BlurMip");
    private static readonly int BlurSpreadId = Shader.PropertyToID("_BlurSpread");
    private static readonly int LowId = Shader.PropertyToID("_Low");
    private static readonly int HighId = Shader.PropertyToID("_High");
    private static readonly int EdgeStartId = Shader.PropertyToID("_EdgeStart");
    private static readonly int EdgeFadeId = Shader.PropertyToID("_EdgeFade");
    private static readonly int MistStrengthId = Shader.PropertyToID("_MistStrength");
    private static readonly int MistCenterYId = Shader.PropertyToID("_MistCenterY");
    private static readonly int MistSizeId = Shader.PropertyToID("_MistSize");
    private static readonly int FlipYId = Shader.PropertyToID("_FlipY");

    private class MenuItem
    {
        public Button button;
        public TextMeshProUGUI label;
        public Image[] dots;
        public float shown;      // 0 = idle look, 1 = picked look
    }

    private PlayerCombat playerCombat;
    private GameObject screenRoot;
    private RawImage world;              // the frozen frame that becomes the painting (or plain paper without the shader)
    private Image redEdges;
    private CanvasGroup titleGroup;
    private Image kanji;
    private CanvasGroup captionGroup;    // brush line + GAME OVER, a breath behind the kanji
    private CanvasGroup buttonsGroup;
    private MenuItem replayItem, mainMenuItem;
    private MenuItem[] menuItems;
    private GameObject lastPicked;
    private RawImage startCover;

    private Material inkMaterial;
    private RenderTexture frozenFrame, softFrame;
    private bool painted;                // true when the shader is painting a frozen frame
    private float redStart;

    private bool sequenceStarted;
    private bool screenComplete;
    private bool buttonsArmed;
    private bool skipRequested;
    private bool menuRegistered;
    private bool leaving;
    private float deathRealTime;

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
        bool shaderOk = inkShader != null && inkShader.isSupported;
        Debug.Log($"[GameOver] ready (sumi-e): PlayerCombat {(playerCombat != null ? "found" : "NOT FOUND, this screen will never show")}, ink shader {(shaderOk ? "ok" : "MISSING OR BROKEN, plain paper will be used")}, paper {(paperTexture != null ? "set" : "MISSING")}, kanji {(kanjiSprite != null ? "set" : "MISSING")}, font {(font != null ? font.name : "MISSING")}, Main Menu {(string.IsNullOrEmpty(mainMenuScene) ? "faint (no scene set)" : "-> " + mainMenuScene)}");
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

        UpdateMenuLook();
    }

    // ---------- the sequence ----------

    private IEnumerator Sequence()
    {
        Debug.Log($"[GameOver] Yoru died: waiting for the death animation to finish, then {delayAfterDeathClip:F1}s, then ink {inkFadeSeconds:F1}s, title {titleFadeSeconds:F1}s, buttons {buttonsFadeSeconds:F1}s");

        // The camera stops taking the mouse the moment she dies (it keeps following her).
        ThirdPersonCamera cam = FindFirstObjectByType<ThirdPersonCamera>();
        if (cam != null) cam.SetCameraEnabled(false);

        // 1. The killing hit and her whole death animation own the screen.
        float waited = 0f;
        while (!playerCombat.IsDeathClipFinished() && waited < maxWaitForDeathClip && !skipRequested)
        {
            waited += Time.unscaledDeltaTime;
            yield return null;
        }
        Debug.Log(waited >= maxWaitForDeathClip
            ? $"[GameOver] the death animation never reported its end: going on after {waited:F1}s (Max Wait For Death Clip)"
            : $"[GameOver] death animation finished {waited:F1}s after the hit (world at x{Time.timeScale:F2}); the world freezes in {delayAfterDeathClip:F1}s");
        yield return WaitReal(delayAfterDeathClip);

        // 2. The world freezes. The kept frame takes the place of the live picture, red edges and all.
        yield return FreezeWorld();
        screenRoot.SetActive(true);
        world.raycastTarget = true;          // nothing under this screen can be clicked any more
        if (painted)
        {
            yield return Fade(freezeFadeSeconds, k =>
            {
                SetAlpha(world, k);
                SetAlpha(redEdges, redStart * k);
            });
        }

        // 3. The world turns into ink on paper and the red lets go. Without the shader the paper simply fades in.
        yield return Fade(inkFadeSeconds, k =>
        {
            if (painted) inkMaterial.SetFloat(InkId, k);
            else SetAlpha(world, k);
            SetAlpha(redEdges, redStart * (1f - k));
        });

        // The world is hidden now: close the other menus' doors (inventory, parchments) the same way every menu does.
        if (!menuRegistered) { MenuGuard.Register(); menuRegistered = true; }

        // 4. The mark: the kanji first, the line and the words a breath behind it.
        yield return Fade(titleFadeSeconds, k =>
        {
            titleGroup.alpha = k;
            captionGroup.alpha = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.35f, 1f, k));
        });

        // 5. The buttons.
        yield return WaitReal(buttonsDelay);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        StartCoroutine(ArmButtons());
        yield return Fade(buttonsFadeSeconds, k => buttonsGroup.alpha = k);
        screenComplete = true;
        Debug.Log($"[GameOver] screen complete {Time.unscaledTime - deathRealTime:F1}s after the hit, waiting for Replay or Main Menu");
    }

    /// <summary>Keeps one frame of the game and hands it to the ink shader. The game camera draws it a second
    /// time into a texture, so the HUD and the red edges are not in the painting. Never throws: any failure
    /// falls back to a copy of the screen, and a missing shader to plain paper.</summary>
    private IEnumerator FreezeWorld()
    {
        yield return new WaitForEndOfFrame();

        painted = false;
        redStart = redEdgeAlpha;
        Color red = redEdgeColor;
        PeachHealthUI peaches = FindFirstObjectByType<PeachHealthUI>();
        if (peaches != null) { red = peaches.vignetteColor; redStart = peaches.vignetteMax; }
        red.a = 0f;
        redEdges.color = red;

        if (inkMaterial == null)
        {
            // No shader: the paper alone fades in over the live world in step 3.
            world.texture = paperTexture;
            SetAlpha(world, 0f);
            Debug.LogError("[GameOver] the ink shader is missing or did not compile, so the world is NOT painted: plain paper is used. Check GameOverScreen > Game Over Controller > The ink painting > Ink Shader (Assets/UI/GameOver/SumiEWorld) and the Console for a shader error.");
            yield break;
        }

        int w = Mathf.Max(16, Screen.width), h = Mathf.Max(16, Screen.height);
        frozenFrame = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { name = "GameOver frozen frame", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        frozenFrame.Create();

        string how = "screen copy";
        bool fromCamera = false;
        Camera gameCamera = Camera.main;
        if (captureMode == CaptureMode.CameraRender && gameCamera != null)
        {
            RenderTexture before = gameCamera.targetTexture;
            try
            {
                gameCamera.targetTexture = frozenFrame;
                gameCamera.Render();
                fromCamera = true;
                how = $"camera '{gameCamera.name}'";
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[GameOver] the camera could not draw the frozen frame ({e.Message}); copying the screen instead.");
            }
            finally
            {
                gameCamera.targetTexture = before;
            }
        }
        if (!fromCamera)
        {
            ScreenCapture.CaptureScreenshotIntoRenderTexture(frozenFrame);
            redStart = 0f;                   // the red edges are already inside a screen copy
        }

        // A half size copy with mips: the soft washes are read from its low mips.
        softFrame = new RenderTexture(Mathf.Max(8, w / 2), Mathf.Max(8, h / 2), 0, RenderTextureFormat.ARGB32)
        {
            name = "GameOver soft frame", useMipMap = true, autoGenerateMips = false,
            wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear
        };
        softFrame.Create();
        Graphics.Blit(frozenFrame, softFrame);
        softFrame.GenerateMips();

        float low = blackPoint, high = whitePoint;
        if (autoLevels) MeasureLevels(softFrame, ref low, ref high);

        inkMaterial.SetTexture(SoftTexId, softFrame);
        inkMaterial.SetTexture(PaperTexId, paperTexture);
        inkMaterial.SetColor(InkColorId, inkColor);
        inkMaterial.SetFloat(InkStrengthId, inkStrength);
        inkMaterial.SetFloat(BlurMipId, washSoftness);
        inkMaterial.SetFloat(BlurSpreadId, 1.5f * Mathf.Pow(2f, washSoftness));
        inkMaterial.SetFloat(LowId, low);
        inkMaterial.SetFloat(HighId, high);
        inkMaterial.SetFloat(EdgeStartId, edgeFadeStart);
        inkMaterial.SetFloat(EdgeFadeId, edgeFade);
        inkMaterial.SetFloat(MistStrengthId, mistStrength);
        inkMaterial.SetFloat(MistCenterYId, mistCenterY);
        inkMaterial.SetVector(MistSizeId, new Vector4(mistSize.x, mistSize.y, 0f, 0f));
        inkMaterial.SetFloat(FlipYId, flipCapturedFrame ? 1f : 0f);
        inkMaterial.SetFloat(InkId, 0f);

        world.texture = frozenFrame;
        world.material = inkMaterial;
        SetAlpha(world, 0f);
        painted = true;
        Debug.Log($"[GameOver] world frozen: {w} x {h} from {how}, levels {low:F2} to {high:F2}{(autoLevels ? " (measured)" : "")}, red edges start at {redStart:F2}");
    }

    /// <summary>The 4th and 96th percentile of the frame's brightness, read from a 64 x 36 copy. Those two points
    /// become full ink and bare paper, the same stretch the look was designed with.</summary>
    private static void MeasureLevels(RenderTexture source, ref float low, ref float high)
    {
        RenderTexture tiny = RenderTexture.GetTemporary(64, 36, 0, RenderTextureFormat.ARGB32);
        RenderTexture active = RenderTexture.active;
        Texture2D read = null;
        try
        {
            Graphics.Blit(source, tiny);
            RenderTexture.active = tiny;
            read = new Texture2D(64, 36, TextureFormat.RGBA32, false);
            read.ReadPixels(new Rect(0, 0, 64, 36), 0, 0);
            read.Apply(false);
            Color32[] px = read.GetPixels32();
            float[] lum = new float[px.Length];
            for (int i = 0; i < px.Length; i++)
                lum[i] = (0.30f * px[i].r + 0.59f * px[i].g + 0.11f * px[i].b) / 255f;
            System.Array.Sort(lum);
            low = lum[Mathf.Clamp(Mathf.RoundToInt(lum.Length * 0.04f), 0, lum.Length - 1)];
            high = lum[Mathf.Clamp(Mathf.RoundToInt(lum.Length * 0.96f), 0, lum.Length - 1)];
            high = Mathf.Max(high, low + 0.08f);      // a nearly flat frame must not be stretched into noise
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[GameOver] could not measure the frame's levels ({e.Message}); using Black Point and White Point.");
        }
        finally
        {
            RenderTexture.active = active;
            RenderTexture.ReleaseTemporary(tiny);
            if (read != null) Destroy(read);
        }
    }

    private IEnumerator ArmButtons()
    {
        float t = 0f;
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);   // nothing left picked from another menu
        while (t < buttonsGuardSeconds) { t += Time.unscaledDeltaTime; yield return null; }
        buttonsGroup.blocksRaycasts = true;
        buttonsArmed = true;
        Pick(replayItem.button.gameObject);
    }

    private void Pick(GameObject go)
    {
        lastPicked = go;
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(go);
    }

    /// <summary>The picked word is full ink with two red dots in front of it, the other one is lighter, a dead
    /// Main Menu lighter still. A click on empty paper must not leave the keys without a picked button.</summary>
    private void UpdateMenuLook()
    {
        if (menuItems == null) return;
        GameObject picked = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        if (buttonsArmed && !leaving)
        {
            bool mine = picked == replayItem.button.gameObject || picked == mainMenuItem.button.gameObject;
            if (mine) lastPicked = picked;
            else if (lastPicked != null) { Pick(lastPicked); picked = lastPicked; }
        }

        float step = Time.unscaledDeltaTime / 0.12f;
        foreach (MenuItem item in menuItems)
        {
            bool isPicked = buttonsArmed && picked == item.button.gameObject;
            item.shown = Mathf.MoveTowards(item.shown, isPicked ? 1f : 0f, step);
            float rest = item.button.interactable ? idleAlpha : disabledAlpha;
            item.label.alpha = Mathf.Lerp(rest, 1f, item.shown);
            foreach (Image dot in item.dots) SetAlpha(dot, dotColor.a * item.shown);
        }
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
        if (frozenFrame != null) { frozenFrame.Release(); Destroy(frozenFrame); }
        if (softFrame != null) { softFrame.Release(); Destroy(softFrame); }
        if (inkMaterial != null) Destroy(inkMaterial);
    }

    // ---------- after Replay: the scene opens from the paper ----------

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

        if (inkShader != null && inkShader.isSupported)
            inkMaterial = new Material(inkShader) { name = "GameOver ink painting (runtime)" };

        screenRoot = new GameObject("Screen", typeof(RectTransform));
        screenRoot.transform.SetParent(transform, false);
        Stretch(screenRoot.GetComponent<RectTransform>());

        world = NewRawImage("World", screenRoot.transform, null, Color.white);
        SetAlpha(world, 0f);

        redEdges = NewImage("RedEdges", screenRoot.transform, redEdgeSprite, redEdgeColor);
        Stretch(redEdges.rectTransform);
        redEdges.enabled = redEdgeSprite != null;
        SetAlpha(redEdges, 0f);

        // The mark: kanji, brush line, the two words.
        titleGroup = NewGroup("Kanji", screenRoot.transform);
        kanji = NewImage("Mark", titleGroup.transform, kanjiSprite, kanjiColor);
        kanji.enabled = kanjiSprite != null;
        kanji.preserveAspect = true;
        float aspect = kanjiSprite != null ? kanjiSprite.rect.width / Mathf.Max(1f, kanjiSprite.rect.height) : 1f;
        Center(kanji.rectTransform, 0f, kanjiY, kanjiHeight * aspect, kanjiHeight);

        captionGroup = NewGroup("Caption", screenRoot.transform);
        Image line = NewImage("BrushLine", captionGroup.transform, brushLineSprite, inkTextColor);
        line.enabled = brushLineSprite != null;
        line.preserveAspect = true;
        float lineAspect = brushLineSprite != null ? brushLineSprite.rect.height / Mathf.Max(1f, brushLineSprite.rect.width) : 0.1f;
        Center(line.rectTransform, 0f, brushLineY, brushLineWidth, brushLineWidth * lineAspect);
        SetAlpha(line, 0.85f);
        TextMeshProUGUI title = NewText("Title", captionGroup.transform, titleText, titleSize, titleLetterSpacing, inkTextColor);
        title.alpha = 0.85f;
        // TextMeshPro adds the letter spacing after the last letter too; half of it back keeps the word optically centred.
        Center(title.rectTransform, titleLetterSpacing * 0.01f * titleSize * 0.5f, titleY, 1600f, titleSize * 1.6f);

        // The buttons, one line, centred as a pair.
        buttonsGroup = NewGroup("Buttons", screenRoot.transform);
        buttonsGroup.interactable = true;      // a group that is not interactable would paint both words as dead
        buttonsGroup.blocksRaycasts = false;   // until ArmButtons: the mouse passes through, nothing is picked, no press can land
        replayItem = NewMenuItem("Replay", replayText, Replay);
        mainMenuItem = NewMenuItem("MainMenu", mainMenuText, GoToMainMenu);
        mainMenuItem.button.interactable = !string.IsNullOrEmpty(mainMenuScene);
        menuItems = new[] { replayItem, mainMenuItem };

        float w0 = replayItem.label.GetPreferredValues(replayText).x;
        float w1 = mainMenuItem.label.GetPreferredValues(mainMenuText).x;
        float left = -(w0 + buttonsGap + w1) * 0.5f;
        PlaceMenuItem(replayItem, left + w0 * 0.5f, w0);
        PlaceMenuItem(mainMenuItem, left + w0 + buttonsGap + w1 * 0.5f, w1);

        // Left and right (and up and down) move between the two; a dead Main Menu is skipped.
        Navigation nav = new Navigation { mode = Navigation.Mode.Explicit };
        if (mainMenuItem.button.interactable)
        {
            nav.selectOnRight = mainMenuItem.button; nav.selectOnLeft = mainMenuItem.button;
            nav.selectOnDown = mainMenuItem.button; nav.selectOnUp = mainMenuItem.button;
        }
        replayItem.button.navigation = nav;
        mainMenuItem.button.navigation = new Navigation
        {
            mode = Navigation.Mode.Explicit,
            selectOnLeft = replayItem.button, selectOnRight = replayItem.button,
            selectOnUp = replayItem.button, selectOnDown = replayItem.button
        };

        titleGroup.alpha = 0f;
        captionGroup.alpha = 0f;
        buttonsGroup.alpha = 0f;
        UpdateMenuLook();
        screenRoot.SetActive(false);

        startCover = NewRawImage("StartCover", transform, paperTexture, paperTexture != null ? Color.white : new Color(0.81f, 0.78f, 0.73f, 1f));
        startCover.gameObject.SetActive(false);
    }

    private MenuItem NewMenuItem(string name, string text, UnityEngine.Events.UnityAction onClick)
    {
        // An invisible box takes the mouse; the word itself carries the look.
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(SelectOnHover));
        go.transform.SetParent(buttonsGroup.transform, false);
        Image hit = go.GetComponent<Image>();
        hit.color = new Color(1f, 1f, 1f, 0f);
        hit.raycastTarget = true;

        MenuItem item = new MenuItem();
        item.label = NewText("Label", go.transform, text, buttonSize, buttonLetterSpacing, inkTextColor);
        Stretch(item.label.rectTransform);
        item.button = go.GetComponent<Button>();
        item.button.transition = Selectable.Transition.None;      // UpdateMenuLook owns the look
        item.button.onClick.AddListener(onClick);

        item.dots = new Image[2];
        for (int k = 0; k < 2; k++)
        {
            item.dots[k] = NewImage("Dot" + k, go.transform, dotSprite, dotColor);
            item.dots[k].enabled = dotSprite != null;
        }
        return item;
    }

    private void PlaceMenuItem(MenuItem item, float centreX, float textWidth)
    {
        RectTransform rt = item.button.GetComponent<RectTransform>();
        Center(rt, centreX, buttonsY, textWidth + 60f, buttonSize * 1.7f);
        // the word sits in the box with TextMeshPro's trailing letter spacing taken back
        item.label.rectTransform.offsetMin = new Vector2(buttonLetterSpacing * 0.01f * buttonSize * 0.5f, 0f);
        item.label.rectTransform.offsetMax = new Vector2(buttonLetterSpacing * 0.01f * buttonSize * 0.5f, 0f);
        for (int k = 0; k < 2; k++)
            Center(item.dots[k].rectTransform, -textWidth * 0.5f - 30f - k * 20f, 0f, dotSize, dotSize);
    }

    private CanvasGroup NewGroup(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasGroup));
        go.transform.SetParent(parent, false);
        Stretch(go.GetComponent<RectTransform>());
        CanvasGroup group = go.GetComponent<CanvasGroup>();
        group.interactable = false;
        group.blocksRaycasts = false;
        return group;
    }

    private Image NewImage(string name, Transform parent, Sprite sprite, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        Image image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private RawImage NewRawImage(string name, Transform parent, Texture texture, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(RawImage));
        go.transform.SetParent(parent, false);
        Stretch(go.GetComponent<RectTransform>());
        RawImage image = go.GetComponent<RawImage>();
        image.texture = texture;
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private TextMeshProUGUI NewText(string name, Transform parent, string text, float size, float letterSpacing, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        TextMeshProUGUI tmp = go.GetComponent<TextMeshProUGUI>();
        if (font != null) tmp.font = font;
        tmp.text = text;
        tmp.fontSize = size;
        tmp.characterSpacing = letterSpacing;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.raycastTarget = false;
        return tmp;
    }

    private static void Center(RectTransform rt, float x, float y, float width, float height)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(width, height);
        rt.anchoredPosition = new Vector2(x, y);
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

    /// <summary>The mouse and the keys share one pick: pointing at a button picks it.</summary>
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
