using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Yoru's pause menu (Esc), in the game over screen's language: washi paper, ink, one red accent,
/// no black. The one difference Hazel asked for: the background is the frozen game, BLURRED and
/// SEEN THROUGH THE PAPER, not painted into ink.
///
/// Esc pauses, Esc resumes. The whole SCREEN is kept, not the camera alone the way the game over
/// screen does it, so the peach row and the red bleed are inside the blur with everything else.
/// The picture is opaque and this canvas sits above the HUD, so nothing has to be hidden or moved.
///
/// What it takes over while it is up, and gives back exactly as it found it:
///   Time.timeScale (CACHED, not forced to 1 on the way out, so bullet time survives a pause)
///   AudioListener.pause
///   MenuGuard (the bag and the parchments cannot open behind it)
///   PeachHealthUI (switched off: its heartbeat runs on unscaled time and would keep beating)
///   ThirdPersonCamera mouse look, the player's NavMeshAgent, the cursor
///
/// RESTART and QUIT ask a second time: the word turns red and reads SURE?, and the next press does it.
/// </summary>
public class PauseMenuController : MonoBehaviour
{
    public enum CaptureMode
    {
        [Tooltip("Keeps the whole screen, HUD and red edges included, so the peaches blur with the world. This is the look Hazel picked.")]
        ScreenGrab,
        [Tooltip("The game camera draws one extra frame: the world only, no HUD in the picture. Use only if Screen Grab looks wrong.")]
        CameraRender
    }

    public enum FlipMode
    {
        [Tooltip("Turn the screen copy the right way up and leave a camera render alone. The copy of the screen comes back upside down on Mac (Metal), measured 21 Sep 2026.")]
        Auto,
        [Tooltip("Never turn the frozen frame over.")]
        Never,
        [Tooltip("Always turn the frozen frame over.")]
        Always
    }

    [Header("Key")]
    [Tooltip("The key that pauses and resumes. Esc is only taken when no other menu and no dialogue is up.")]
    [SerializeField] private KeyCode pauseKey = KeyCode.Escape;

    [Header("Timing (real seconds, the paused clock does not touch them)")]
    [Tooltip("The paper, the mark and the items fade in over this long. The mark starts at a third of it, the items at two thirds.")]
    [SerializeField] private float fadeInSeconds = 0.45f;
    [Tooltip("The screen fades away over this long when she resumes.")]
    [SerializeField] private float fadeOutSeconds = 0.25f;
    [Tooltip("A key or a click after this many seconds finishes the fade in at once, for the player who pauses forty times an hour.")]
    [SerializeField] private float skipFadeAfter = 0.08f;
    [Tooltip("How long SURE? waits for the second press before the word goes back to normal.")]
    [SerializeField] private float confirmSeconds = 3f;

    [Header("The paper")]
    [Tooltip("Assets/UI/Pause/PausePaper (shader YORU/UI/Pause Paper). Empty or broken = plain paper instead of the blurred world.")]
    [SerializeField] private Shader pauseShader;
    [Tooltip("The paper itself. Assets/UI/GameOver/gameover_washi, the same sheet the game over screen uses.")]
    [SerializeField] private Texture2D paperTexture;
    [Tooltip("How much of the frozen world comes through the paper (0 = paper only, 1 = world at full). This is the transparency dial.")]
    [Range(0f, 1f)] [SerializeField] private float worldAmount = 0.58f;
    [Tooltip("How much of the world's colour survives (0 = grey ink, 1 = full colour).")]
    [Range(0f, 1f)] [SerializeField] private float colourAmount = 0.3f;
    [Tooltip("How blurry the frozen world is (mip level). 2 = softer detail still readable, 4 = a wash of light and dark.")]
    [Range(0f, 6f)] [SerializeField] private float blurSoftness = 3f;
    [Tooltip("ON = the darkest and brightest parts of the frozen screen are measured and stretched, so a dark cave and a bright field both read. OFF = use the two points below.")]
    [SerializeField] private bool autoLevels = true;
    [Tooltip("Auto Levels OFF only: screen brightness that becomes the darkest paper.")]
    [Range(0f, 1f)] [SerializeField] private float blackPoint = 0.04f;
    [Tooltip("Auto Levels OFF only: screen brightness that becomes the brightest paper.")]
    [Range(0f, 1f)] [SerializeField] private float whitePoint = 0.6f;
    [Tooltip("The world starts to give way to bare paper this far from the middle (0 = centre, 1 = the screen edge).")]
    [Range(0f, 1f)] [SerializeField] private float edgeFadeStart = 0.32f;
    [Tooltip("How much of the world is gone at the corners.")]
    [Range(0f, 1f)] [SerializeField] private float edgeFade = 0.55f;
    [Tooltip("Strength of the band of paper mist behind the words.")]
    [Range(0f, 1f)] [SerializeField] private float mistStrength = 0.42f;
    [Tooltip("Height of the mist on the screen (0 = bottom, 1 = top).")]
    [Range(0f, 1f)] [SerializeField] private float mistCenterY = 0.398f;
    [Tooltip("Width and height of the mist, as parts of the screen.")]
    [SerializeField] private Vector2 mistSize = new Vector2(0.396f, 0.343f);
    [Tooltip("How the world is frozen. Screen Grab keeps the peaches inside the blur.")]
    [SerializeField] private CaptureMode captureMode = CaptureMode.ScreenGrab;
    [Tooltip("Which way up the frozen frame is. Auto turns the screen copy over (that is how it comes back on this Mac) and leaves a camera render alone. Only change this if the background shows upside down.")]
    [SerializeField] private FlipMode flipFrozenFrame = FlipMode.Auto;

    [Header("The mark")]
    [Tooltip("The kanji, a white sprite that is tinted below. Assets/UI/Pause/pause_kanji_tomeru (tomeru, to stop).")]
    [SerializeField] private Sprite kanjiSprite;
    [SerializeField] private Color kanjiColor = new Color(0.85f, 0.05f, 0.08f, 0.95f);
    [Tooltip("Height of the kanji in pixels at 1920 x 1080. The width follows the sprite.")]
    [SerializeField] private float kanjiHeight = 187f;
    [Tooltip("Height of the kanji's middle above the middle of the screen, in pixels.")]
    [SerializeField] private float kanjiY = 220f;
    [Tooltip("The brush line under the kanji. Assets/UI/GameOver/gameover_brush_line.")]
    [SerializeField] private Sprite brushLineSprite;
    [SerializeField] private float brushLineWidth = 400f;
    [SerializeField] private float brushLineY = 54f;
    [Tooltip("The font for every word on this screen. Assets/Fonts/YujiSyuku-Regular SDF.")]
    [SerializeField] private TMP_FontAsset font;
    [SerializeField] private string titleText = "PAUSED";
    [SerializeField] private float titleSize = 28f;
    [Tooltip("Space between the letters (TextMeshPro character spacing; 75 = three quarters of a letter).")]
    [SerializeField] private float titleLetterSpacing = 75f;
    [SerializeField] private float titleY = -12f;
    [Tooltip("Colour of the words and the brush line: ink, never pure black.")]
    [SerializeField] private Color inkTextColor = new Color(0.085f, 0.08f, 0.085f, 1f);

    [Header("The items")]
    [SerializeField] private string resumeText = "RESUME";
    [SerializeField] private string restartText = "RESTART";
    [SerializeField] private string mainMenuText = "MAIN MENU";
    [SerializeField] private string quitText = "QUIT";
    [Tooltip("What RESTART and QUIT turn into after the first press. The next press does it.")]
    [SerializeField] private string confirmText = "SURE?";
    [Tooltip("Scene the Main Menu item loads. EMPTY = the item shows faint and cannot be pressed. It must also be in File > Build Profiles > Scene List.")]
    [SerializeField] private string mainMenuScene = "";
    [SerializeField] private float itemSize = 34f;
    [SerializeField] private float itemLetterSpacing = 34f;
    [Tooltip("Height of the FIRST item relative to the middle of the screen, in pixels (negative = below).")]
    [SerializeField] private float firstItemY = -150f;
    [Tooltip("Pixels between one item and the next.")]
    [SerializeField] private float itemSpacing = 76f;
    [Tooltip("The dot in front of the picked item. Assets/UI/GameOver/gameover_dot.")]
    [SerializeField] private Sprite dotSprite;
    [SerializeField] private Color dotColor = new Color(0.85f, 0.05f, 0.08f, 1f);
    [SerializeField] private float dotSize = 9f;
    [Tooltip("How strong an item that is NOT picked looks.")]
    [Range(0f, 1f)] [SerializeField] private float idleAlpha = 0.62f;
    [Tooltip("How faint an item that cannot be pressed looks.")]
    [Range(0f, 1f)] [SerializeField] private float disabledAlpha = 0.3f;
    [Tooltip("Colour of a word that is waiting for its second press.")]
    [SerializeField] private Color confirmColor = new Color(0.85f, 0.05f, 0.08f, 1f);

    [Header("When Esc does nothing")]
    [Tooltip("ON = no pausing while something other than an aim ability owns the world clock, which is how the Oni's cinematic is kept whole.")]
    [SerializeField] private bool blockWhileClockIsNotOne = true;
    [Tooltip("No pausing while the Oni's animator is in one of these states. Phase_Transition is his roar between phases.")]
    [SerializeField] private string[] blockedOniStates = new[] { "Phase_Transition" };

    private static readonly int ShowId = Shader.PropertyToID("_Show");
    private static readonly int SoftTexId = Shader.PropertyToID("_SoftTex");
    private static readonly int PaperTexId = Shader.PropertyToID("_PaperTex");
    private static readonly int WorldId = Shader.PropertyToID("_World");
    private static readonly int ColourId = Shader.PropertyToID("_Colour");
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
        public string word;
        public bool needsConfirm;
        public System.Action action;
        public float shown;          // 0 = idle look, 1 = picked look
    }

    private GameObject screenRoot;
    private RawImage world;
    private CanvasGroup markGroup;
    private CanvasGroup itemsGroup;
    private MenuItem[] menuItems;
    private MenuItem resumeItem, restartItem, mainMenuItem, quitItem;
    private GameObject lastPicked;

    private Material paperMaterial;
    private RenderTexture frozenFrame, softFrame;
    private bool painted;

    private PlayerCombat playerCombat;
    private Animator oniAnimator;
    private ThirdPersonCamera cameraController;
    private NavMeshAgent playerNavAgent;
    private PeachHealthUI peachUI;

    private bool isPaused;
    private bool shown;              // the fade in has finished
    private bool menuArmed;          // presses land
    private bool leaving;            // resuming, restarting or quitting
    private bool skipFade;
    private float openedAt;
    private float cachedTimeScale = 1f;
    private bool navAgentWasEnabled;
    private bool peachUIWasEnabled;
    private MenuItem pendingItem;    // the one showing SURE?
    private float confirmUntil;
    private Coroutine fadeRoutine;   // the fade in or the fade out, never both at once

    /// <summary>True while the pause screen is on. Nothing reads this yet; it is here for the main menu work.</summary>
    public bool IsPaused => isPaused;

    private void Awake()
    {
        BuildScreen();
    }

    private void Start()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        playerCombat = player != null ? player.GetComponent<PlayerCombat>() : null;
        playerNavAgent = player != null ? player.GetComponent<NavMeshAgent>() : null;
        cameraController = FindFirstObjectByType<ThirdPersonCamera>();
        peachUI = FindFirstObjectByType<PeachHealthUI>();
        bool shaderOk = pauseShader != null && pauseShader.isSupported;
        Debug.Log($"[Pause] ready: key {pauseKey}, paper shader {(shaderOk ? "ok" : "MISSING OR BROKEN, plain paper will be used")}, paper {(paperTexture != null ? "set" : "MISSING")}, kanji {(kanjiSprite != null ? "set" : "MISSING")}, font {(font != null ? font.name : "MISSING")}, capture {captureMode}, Main Menu {(string.IsNullOrEmpty(mainMenuScene) ? "faint (no scene set)" : "-> " + mainMenuScene)}");
    }

    private void Update()
    {
        if (!isPaused)
        {
            if (Input.GetKeyDown(pauseKey)) TryOpen();
            return;
        }

        if (Input.GetKeyDown(pauseKey) && !leaving)
        {
            Resume();
            return;
        }

        if (!shown && !skipFade && Time.unscaledTime - openedAt >= skipFadeAfter
            && (Input.anyKeyDown || Input.GetMouseButtonDown(0)))
            skipFade = true;

        UpdateConfirm();
        UpdateMenuLook();
    }

    // ---------- opening and closing ----------

    /// <summary>Esc belongs to whatever else is open. This is the only place that decides.</summary>
    private void TryOpen()
    {
        if (isPaused || leaving) return;

        if (MenuGuard.IsAnyMenuOpen)
        {
            Debug.Log("[Pause] Esc ignored: another menu is open, Esc belongs to it");
            return;
        }
        if (DialogueManager.Instance != null && DialogueManager.Instance.IsDialogueActive)
        {
            Debug.Log("[Pause] Esc ignored: a conversation is on screen, Esc belongs to it");
            return;
        }
        if (playerCombat != null && playerCombat.IsDead())
        {
            Debug.Log("[Pause] Esc ignored: Yoru is dead, the game over screen owns the screen");
            return;
        }
        if (OniIsInABlockedState(out string state))
        {
            Debug.Log($"[Pause] Esc ignored: the Oni is in '{state}'");
            return;
        }
        if (blockWhileClockIsNotOne && !IsAiming() && Mathf.Abs(Time.timeScale - 1f) > 0.01f)
        {
            Debug.Log($"[Pause] Esc ignored: something else owns the world clock (x{Time.timeScale:F2}), most likely the Oni's cinematic");
            return;
        }

        Open();
    }

    private void Open()
    {
        isPaused = true;
        shown = false;
        menuArmed = false;
        skipFade = false;
        leaving = false;
        openedAt = Time.unscaledTime;

        cachedTimeScale = Time.timeScale;
        Time.timeScale = 0f;

        MenuGuard.Register();

        // The mouse stops driving the camera, and the agent is put back to whatever it really was:
        // PlayerMovement keeps it asleep on purpose, so it must never be blindly switched on again.
        if (cameraController != null) cameraController.SetCameraEnabled(false);
        if (playerNavAgent != null)
        {
            navAgentWasEnabled = playerNavAgent.enabled;
            playerNavAgent.enabled = false;
        }

        // The peach row runs its heartbeat on unscaled time, so without this it keeps beating behind
        // a paused listener and lets go of a pile of beats on resume.
        if (peachUI == null) peachUI = FindFirstObjectByType<PeachHealthUI>();
        if (peachUI != null)
        {
            peachUIWasEnabled = peachUI.enabled;
            peachUI.enabled = false;
        }

        AudioListener.pause = true;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (fadeRoutine != null) StopCoroutine(fadeRoutine);
        fadeRoutine = StartCoroutine(ShowScreen());
    }

    private IEnumerator ShowScreen()
    {
        yield return FreezeScreen();

        screenRoot.SetActive(true);
        world.raycastTarget = true;          // nothing under the pause screen can be clicked
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);

        yield return Fade(fadeInSeconds, k =>
        {
            if (painted) paperMaterial.SetFloat(ShowId, k);
            else SetAlpha(world, k);
            markGroup.alpha = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.33f, 1f, k));
            itemsGroup.alpha = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.66f, 1f, k));
        });

        shown = true;
        menuArmed = true;
        itemsGroup.blocksRaycasts = true;
        Pick(resumeItem.button.gameObject);
        Debug.Log($"[Pause] paused: world clock was x{cachedTimeScale:F2}, screen frozen {(painted ? "and blurred" : "WITHOUT the shader, plain paper")}, up in {Time.unscaledTime - openedAt:F2}s real");
    }

    private void Resume()
    {
        if (leaving || !isPaused) return;
        leaving = true;
        menuArmed = false;
        itemsGroup.blocksRaycasts = false;
        CancelConfirm();
        skipFade = false;
        if (fadeRoutine != null) StopCoroutine(fadeRoutine);   // Esc during the fade in: that fade stops here
        fadeRoutine = StartCoroutine(HideScreen());
    }

    private IEnumerator HideScreen()
    {
        // Fade from where the screen actually is, not from full: a second Esc in the middle of the
        // fade in must not make the mark jump to full before it goes.
        float fromPaper = painted ? paperMaterial.GetFloat(ShowId) : world.color.a;
        float fromMark = markGroup.alpha;
        float fromItems = itemsGroup.alpha;

        yield return Fade(fadeOutSeconds, k =>
        {
            float back = 1f - k;
            if (painted) paperMaterial.SetFloat(ShowId, fromPaper * back);
            else SetAlpha(world, fromPaper * back);
            markGroup.alpha = fromMark * back;
            itemsGroup.alpha = fromItems * back;
        });

        screenRoot.SetActive(false);
        world.raycastTarget = false;
        ReleaseFrozenFrame();
        PutTheWorldBack();
        isPaused = false;
        leaving = false;
        shown = false;
        Debug.Log($"[Pause] resumed: world clock back to x{Time.timeScale:F2}");
    }

    /// <summary>Everything this screen took over, handed back exactly as it was found.</summary>
    private void PutTheWorldBack()
    {
        Time.timeScale = cachedTimeScale;
        AudioListener.pause = false;
        MenuGuard.Unregister();
        if (cameraController != null) cameraController.SetCameraEnabled(true);
        if (playerNavAgent != null) playerNavAgent.enabled = navAgentWasEnabled;
        if (peachUI != null) peachUI.enabled = peachUIWasEnabled;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    // ---------- the frozen screen ----------

    /// <summary>Keeps one frame and hands it to the paper shader. The WHOLE SCREEN is kept, HUD and red
    /// edges included, so the peaches blur with the world. Never throws: if the screen cannot be copied
    /// the game camera draws the frame instead, and a missing shader falls back to plain paper.</summary>
    private IEnumerator FreezeScreen()
    {
        yield return new WaitForEndOfFrame();

        painted = false;
        if (paperMaterial == null)
        {
            world.texture = paperTexture;
            SetAlpha(world, 0f);
            Debug.LogError("[Pause] the paper shader is missing or did not compile, so the world is NOT blurred: plain paper is used. Check PauseMenu > Pause Menu Controller > The paper > Pause Shader (Assets/UI/Pause/PausePaper) and the Console for a shader error.");
            yield break;
        }

        int w = Mathf.Max(16, Screen.width), h = Mathf.Max(16, Screen.height);
        frozenFrame = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32)
        {
            name = "Pause frozen screen", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear
        };
        frozenFrame.Create();

        string how = "screen copy";
        bool captured = false;
        bool fromScreenGrab = false;
        if (captureMode == CaptureMode.ScreenGrab)
        {
            try
            {
                ScreenCapture.CaptureScreenshotIntoRenderTexture(frozenFrame);
                captured = true;
                fromScreenGrab = true;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[Pause] the screen could not be copied ({e.Message}); the camera will draw the frame instead, so the HUD will not be in it.");
            }
        }
        if (!captured)
        {
            Camera gameCamera = Camera.main;
            if (gameCamera != null)
            {
                RenderTexture before = gameCamera.targetTexture;
                try
                {
                    gameCamera.targetTexture = frozenFrame;
                    gameCamera.Render();
                    how = $"camera '{gameCamera.name}' (no HUD in it)";
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"[Pause] the camera could not draw the frozen frame either ({e.Message}).");
                }
                finally
                {
                    gameCamera.targetTexture = before;
                }
            }
        }

        // A half size copy with mips: the blur is read from its low mips.
        softFrame = new RenderTexture(Mathf.Max(8, w / 2), Mathf.Max(8, h / 2), 0, RenderTextureFormat.ARGB32)
        {
            name = "Pause soft screen", useMipMap = true, autoGenerateMips = false,
            wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear
        };
        softFrame.Create();
        Graphics.Blit(frozenFrame, softFrame);
        softFrame.GenerateMips();

        float low = blackPoint, high = whitePoint;
        if (autoLevels) MeasureLevels(softFrame, ref low, ref high);

        paperMaterial.SetTexture(SoftTexId, softFrame);
        paperMaterial.SetTexture(PaperTexId, paperTexture);
        paperMaterial.SetFloat(WorldId, worldAmount);
        paperMaterial.SetFloat(ColourId, colourAmount);
        paperMaterial.SetFloat(BlurMipId, blurSoftness);
        paperMaterial.SetFloat(BlurSpreadId, 1.5f * Mathf.Pow(2f, blurSoftness));
        paperMaterial.SetFloat(LowId, low);
        paperMaterial.SetFloat(HighId, high);
        paperMaterial.SetFloat(EdgeStartId, edgeFadeStart);
        paperMaterial.SetFloat(EdgeFadeId, edgeFade);
        paperMaterial.SetFloat(MistStrengthId, mistStrength);
        paperMaterial.SetFloat(MistCenterYId, mistCenterY);
        paperMaterial.SetVector(MistSizeId, new Vector4(mistSize.x, mistSize.y, 0f, 0f));
        // The copy of the screen comes back upside down (Metal on her Mac, every pause in the
        // 20:05 log). A camera render does not. Auto turns over the one that needs it.
        bool flip = flipFrozenFrame == FlipMode.Always
                 || (flipFrozenFrame == FlipMode.Auto && fromScreenGrab);
        paperMaterial.SetFloat(FlipYId, flip ? 1f : 0f);
        paperMaterial.SetFloat(ShowId, 0f);

        world.texture = frozenFrame;
        world.material = paperMaterial;
        SetAlpha(world, 1f);
        painted = true;
        Debug.Log($"[Pause] screen frozen: {w} x {h} from {how}, {(flip ? "turned the right way up" : "not turned over")} ({flipFrozenFrame}), levels {low:F2} to {high:F2}{(autoLevels ? " (measured)" : "")}, world {worldAmount:F2}, blur mip {blurSoftness:F1}");
    }

    /// <summary>The 4th and 96th percentile of the frame's brightness, read from a 64 x 36 copy.</summary>
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
            high = Mathf.Max(high, low + 0.08f);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[Pause] could not measure the screen's levels ({e.Message}); using Black Point and White Point.");
        }
        finally
        {
            RenderTexture.active = active;
            RenderTexture.ReleaseTemporary(tiny);
            if (read != null) Destroy(read);
        }
    }

    private void ReleaseFrozenFrame()
    {
        world.texture = null;
        if (frozenFrame != null) { frozenFrame.Release(); Destroy(frozenFrame); frozenFrame = null; }
        if (softFrame != null) { softFrame.Release(); Destroy(softFrame); softFrame = null; }
        painted = false;
    }

    // ---------- the items ----------

    private void Press(MenuItem item)
    {
        if (!menuArmed || leaving || item == null || !item.button.interactable) return;

        if (item.needsConfirm && pendingItem != item)
        {
            CancelConfirm();
            pendingItem = item;
            confirmUntil = Time.unscaledTime + confirmSeconds;
            item.label.text = confirmText;
            Debug.Log($"[Pause] {item.word} asked again: the next press does it");
            return;
        }

        CancelConfirm();
        if (item.action != null) item.action();
    }

    private void UpdateConfirm()
    {
        if (pendingItem == null) return;
        bool timedOut = Time.unscaledTime > confirmUntil;
        GameObject picked = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        bool movedAway = picked != null && picked != pendingItem.button.gameObject;
        if (timedOut || movedAway)
        {
            Debug.Log($"[Pause] {pendingItem.word} let go ({(timedOut ? "no second press" : "the pick moved away")})");
            CancelConfirm();
        }
    }

    private void CancelConfirm()
    {
        if (pendingItem == null) return;
        pendingItem.label.text = pendingItem.word;
        pendingItem = null;
    }

    /// <summary>The picked word is full ink with two red dots in front of it, the others lighter, a dead
    /// Main Menu lighter still. A click on empty paper must not leave the keys without a picked item.</summary>
    private void UpdateMenuLook()
    {
        if (menuItems == null) return;

        GameObject picked = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        if (menuArmed && !leaving)
        {
            bool mine = false;
            foreach (MenuItem item in menuItems)
                if (picked == item.button.gameObject) mine = true;
            if (mine) lastPicked = picked;
            else if (lastPicked != null) { Pick(lastPicked); picked = lastPicked; }
        }

        float step = Time.unscaledDeltaTime / 0.12f;
        foreach (MenuItem item in menuItems)
        {
            bool isPicked = menuArmed && picked == item.button.gameObject;
            item.shown = Mathf.MoveTowards(item.shown, isPicked ? 1f : 0f, step);
            float rest = item.button.interactable ? idleAlpha : disabledAlpha;
            item.label.color = item == pendingItem ? confirmColor : inkTextColor;
            item.label.alpha = Mathf.Lerp(rest, 1f, item.shown);
            foreach (Image dot in item.dots) SetAlpha(dot, dotColor.a * item.shown);
        }
    }

    private void Pick(GameObject go)
    {
        lastPicked = go;
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(go);
    }

    private void Restart()
    {
        if (leaving) return;
        leaving = true;
        Scene scene = SceneManager.GetActiveScene();
        Debug.Log($"[Pause] Restart: reloading '{scene.name}'");
        if (OniDebugLogFile.IsOpen) OniDebugLogFile.Marker("RESTART from the pause menu: scene reloaded");
        LeaveTheScene();
        GameOverController.RequestStartCover();
#if UNITY_EDITOR
        // In the editor the scene is loaded by its path, so Restart works even while the scene is not
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
            Debug.LogError($"[Pause] Main Menu: the scene '{mainMenuScene}' is not in File > Build Profiles > Scene List, so it cannot be loaded. Add it there, or fix the name in PauseMenu > Pause Menu Controller > The items > Main Menu Scene.");
            return;
        }
        leaving = true;
        Debug.Log($"[Pause] Main Menu: loading '{mainMenuScene}'");
        LeaveTheScene();
        SceneManager.LoadScene(mainMenuScene);
    }

    private void Quit()
    {
        if (leaving) return;
        leaving = true;
        Debug.Log("[Pause] Quit");
        LeaveTheScene();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    /// <summary>Restart, Main Menu and Quit all leave: the clock goes back to 1, not to what was cached.</summary>
    private void LeaveTheScene()
    {
        AudioListener.pause = false;
        MenuGuard.Unregister();
        if (peachUI != null) peachUI.enabled = peachUIWasEnabled;
        Time.timeScale = 1f;
        isPaused = false;
    }

    private void OnDestroy()
    {
        // Play stopped or the scene changed while this screen held the menu lock.
        if (isPaused)
        {
            MenuGuard.Unregister();
            AudioListener.pause = false;
            isPaused = false;
        }
        if (frozenFrame != null) { frozenFrame.Release(); Destroy(frozenFrame); }
        if (softFrame != null) { softFrame.Release(); Destroy(softFrame); }
        if (paperMaterial != null) Destroy(paperMaterial);
    }

    // ---------- the world around it ----------

    private static bool IsAiming()
    {
        return TailAimController.IsAiming || TailAimController4Leg.IsAiming;
    }

    private bool OniIsInABlockedState(out string state)
    {
        state = null;
        if (blockedOniStates == null || blockedOniStates.Length == 0) return false;

        if (oniAnimator == null)
        {
            OniBoss boss = FindFirstObjectByType<OniBoss>();
            if (boss != null) oniAnimator = boss.GetComponentInChildren<Animator>(true);
            if (oniAnimator == null) return false;
        }

        AnimatorStateInfo now = oniAnimator.GetCurrentAnimatorStateInfo(0);
        AnimatorStateInfo next = oniAnimator.GetNextAnimatorStateInfo(0);
        bool inTransition = oniAnimator.IsInTransition(0);
        foreach (string name in blockedOniStates)
        {
            if (string.IsNullOrEmpty(name)) continue;
            if (now.IsName(name) || (inTransition && next.IsName(name)))
            {
                state = name;
                return true;
            }
        }
        return false;
    }

    private IEnumerator Fade(float seconds, System.Action<float> apply)
    {
        float t = 0f;
        float length = Mathf.Max(0.01f, seconds);
        while (t < length && !skipFade)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / length);
            apply(k * k * (3f - 2f * k));   // smooth ease
            yield return null;
        }
        apply(1f);
        skipFade = false;
    }

    // ---------- building the screen ----------

    private void BuildScreen()
    {
        if (screenRoot != null) return;

        if (EventSystem.current == null && FindFirstObjectByType<EventSystem>() == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

        if (pauseShader != null && pauseShader.isSupported)
            paperMaterial = new Material(pauseShader) { name = "Pause paper (runtime)" };

        screenRoot = new GameObject("Screen", typeof(RectTransform));
        screenRoot.transform.SetParent(transform, false);
        Stretch(screenRoot.GetComponent<RectTransform>());

        world = NewRawImage("World", screenRoot.transform, null, Color.white);
        SetAlpha(world, 0f);

        // The mark: the kanji, the brush line, PAUSED.
        markGroup = NewGroup("Mark", screenRoot.transform);
        Image kanji = NewImage("Kanji", markGroup.transform, kanjiSprite, kanjiColor);
        kanji.enabled = kanjiSprite != null;
        kanji.preserveAspect = true;
        float aspect = kanjiSprite != null ? kanjiSprite.rect.width / Mathf.Max(1f, kanjiSprite.rect.height) : 1f;
        Center(kanji.rectTransform, 0f, kanjiY, kanjiHeight * aspect, kanjiHeight);

        Image line = NewImage("BrushLine", markGroup.transform, brushLineSprite, inkTextColor);
        line.enabled = brushLineSprite != null;
        line.preserveAspect = true;
        float lineAspect = brushLineSprite != null ? brushLineSprite.rect.height / Mathf.Max(1f, brushLineSprite.rect.width) : 0.1f;
        Center(line.rectTransform, 0f, brushLineY, brushLineWidth, brushLineWidth * lineAspect);
        SetAlpha(line, 0.85f);

        TextMeshProUGUI title = NewText("Title", markGroup.transform, titleText, titleSize, titleLetterSpacing, inkTextColor);
        title.alpha = 0.85f;
        // TextMeshPro adds the letter spacing after the last letter too; half of it back keeps the word optically centred.
        Center(title.rectTransform, titleLetterSpacing * 0.01f * titleSize * 0.5f, titleY, 1600f, titleSize * 1.6f);

        // The items, one under the other.
        itemsGroup = NewGroup("Items", screenRoot.transform);
        itemsGroup.interactable = true;       // a group that is not interactable would paint every word as dead
        itemsGroup.blocksRaycasts = false;    // until the fade has finished: the mouse passes through

        resumeItem = NewMenuItem("Resume", resumeText, false, Resume);
        restartItem = NewMenuItem("Restart", restartText, true, Restart);
        mainMenuItem = NewMenuItem("MainMenu", mainMenuText, false, GoToMainMenu);
        quitItem = NewMenuItem("Quit", quitText, true, Quit);
        mainMenuItem.button.interactable = !string.IsNullOrEmpty(mainMenuScene);
        menuItems = new[] { resumeItem, restartItem, mainMenuItem, quitItem };

        for (int i = 0; i < menuItems.Length; i++)
            PlaceMenuItem(menuItems[i], firstItemY - i * itemSpacing);

        BuildNavigation();

        markGroup.alpha = 0f;
        itemsGroup.alpha = 0f;
        UpdateMenuLook();
        screenRoot.SetActive(false);
    }

    /// <summary>Up and down walk the column and wrap around. An item that cannot be pressed is stepped over.</summary>
    private void BuildNavigation()
    {
        System.Collections.Generic.List<MenuItem> live = new System.Collections.Generic.List<MenuItem>();
        foreach (MenuItem item in menuItems)
            if (item.button.interactable) live.Add(item);

        for (int i = 0; i < live.Count; i++)
        {
            Button up = live[(i - 1 + live.Count) % live.Count].button;
            Button down = live[(i + 1) % live.Count].button;
            live[i].button.navigation = new Navigation
            {
                mode = Navigation.Mode.Explicit,
                selectOnUp = up, selectOnDown = down,
                selectOnLeft = null, selectOnRight = null
            };
        }
    }

    private MenuItem NewMenuItem(string name, string text, bool needsConfirm, System.Action action)
    {
        // An invisible box takes the mouse; the word itself carries the look.
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(SelectOnHover));
        go.transform.SetParent(itemsGroup.transform, false);
        Image hit = go.GetComponent<Image>();
        hit.color = new Color(1f, 1f, 1f, 0f);
        hit.raycastTarget = true;

        MenuItem item = new MenuItem();
        item.word = text;
        item.needsConfirm = needsConfirm;
        item.action = action;
        item.label = NewText("Label", go.transform, text, itemSize, itemLetterSpacing, inkTextColor);
        Stretch(item.label.rectTransform);
        item.button = go.GetComponent<Button>();
        item.button.transition = Selectable.Transition.None;      // UpdateMenuLook owns the look
        item.button.onClick.AddListener(() => Press(item));

        item.dots = new Image[2];
        for (int k = 0; k < 2; k++)
        {
            item.dots[k] = NewImage("Dot" + k, go.transform, dotSprite, dotColor);
            item.dots[k].enabled = dotSprite != null;
        }
        return item;
    }

    private void PlaceMenuItem(MenuItem item, float y)
    {
        float textWidth = item.label.GetPreferredValues(item.word).x;
        RectTransform rt = item.button.GetComponent<RectTransform>();
        Center(rt, 0f, y, textWidth + 60f, itemSize * 1.7f);
        // the word sits in the box with TextMeshPro's trailing letter spacing taken back
        item.label.rectTransform.offsetMin = new Vector2(itemLetterSpacing * 0.01f * itemSize * 0.5f, 0f);
        item.label.rectTransform.offsetMax = new Vector2(itemLetterSpacing * 0.01f * itemSize * 0.5f, 0f);
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

    /// <summary>The mouse and the keys share one pick: pointing at an item picks it.</summary>
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
