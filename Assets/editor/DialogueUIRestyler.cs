using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One-click Zelda-style (BotW-like) dialogue UI, built CLEAN.
///
/// Menu: YORU > Restyle Dialogue UI (Zelda Style)
///
/// Instead of patching the legacy dialogue objects (whose inherited layout fought every
/// change), this builds a brand-new self-contained canvas from scratch:
///
///   DialogueCanvas_v2 (Screen Space Overlay, 1920x1080 reference scaler, sort order 50)
///     DialoguePanel_v2 (full-screen soft black gradient rising from the bottom)
///       SpeakerName    (small light text above the dialogue line, lower left-centre)
///       DialogueText   (white, rich text on for keyword colours)
///       Choice0..2     (dark rounded buttons stacked at the lower right, hover-lit)
///
/// It then rewires the DialogueManager's serialized references (panel, name, text,
/// buttons, labels) to the new objects and disables the old panel GameObject, so the
/// legacy UI can never draw again. The manager's code is untouched; its Start() wires
/// the click listeners to whatever buttons are referenced, which are now these.
///
/// All visuals are original generated sprites (rounded rect + vertical gradient in
/// Assets/UI). Safe to re-run: the previous DialogueCanvas_v2 is deleted and rebuilt.
/// SAVE THE SCENE afterwards.
/// </summary>
public static class DialogueUIRestyler
{
    private const string SpriteFolder = "Assets/UI";
    private const string RoundedPath = SpriteFolder + "/YORU_RoundedRect.png";
    private const string GradientPath = SpriteFolder + "/YORU_BottomGradient.png";
    private const string SoftBoxPath = SpriteFolder + "/YORU_SoftBox.png";
    private const string FramePath = SpriteFolder + "/YORU_RoundedFrame.png";
    private const string CanvasName = "DialogueCanvas_v2";

    // Layout (in 1920x1080 reference pixels)
    private const float TextLeft = 170f;
    private const float TextWidth = 1000f;
    private const int ChoiceCount = 3;

    // Palette
    private static readonly Color NameColor = new Color(0.886f, 0.871f, 0.812f, 1f);
    private static readonly Color TextColor = new Color(0.97f, 0.97f, 0.97f, 1f);
    private static readonly Color ButtonColor = new Color(0.075f, 0.075f, 0.09f, 0.93f);
    private static readonly Color LabelColor = new Color(0.93f, 0.93f, 0.93f, 1f);

    [MenuItem("YORU/Restyle Dialogue UI (Zelda Style)")]
    public static void Restyle()
    {
        DialogueManager dm = Object.FindObjectOfType<DialogueManager>(true);
        if (dm == null)
        {
            Debug.LogError("[DialogueUIRestyler] No DialogueManager found. Open DemoScene_Day first, then run this again.");
            return;
        }

        Sprite rounded = EnsureRoundedSprite();
        Sprite gradient = EnsureGradientSprite();
        Sprite softBox = EnsureSoftBoxSprite();
        Sprite frame = EnsureFrameSprite();
        if (rounded == null || gradient == null || softBox == null || frame == null)
        {
            Debug.LogError("[DialogueUIRestyler] Could not create or load the UI sprites. Aborting.");
            return;
        }

        SerializedObject so = new SerializedObject(dm);
        GameObject oldPanel = so.FindProperty("dialoguePanel").objectReferenceValue as GameObject;

        // Rebuild from zero every run.
        GameObject previous = GameObject.Find(CanvasName);
        if (previous != null)
            Undo.DestroyObjectImmediate(previous);

        // 1. Canvas with a known-good scaler. Nothing inherited from the legacy UI.
        GameObject canvasGO = new GameObject(CanvasName, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Undo.RegisterCreatedObjectUndo(canvasGO, "Build Dialogue UI v2");
        Canvas canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;
        CanvasScaler scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        // 2. Panel: full-screen bottom gradient. The manager toggles this object.
        GameObject panel = NewRect("DialoguePanel_v2", canvasGO.transform, out RectTransform panelRt);
        panelRt.anchorMin = Vector2.zero;
        panelRt.anchorMax = Vector2.one;
        panelRt.offsetMin = Vector2.zero;
        panelRt.offsetMax = Vector2.zero;
        Image panelImg = panel.AddComponent<Image>();
        panelImg.sprite = gradient;
        panelImg.type = Image.Type.Simple;
        panelImg.color = Color.white; // gradient texture carries its own black + alpha
        panelImg.raycastTarget = true;

        // 3. Soft dark feathered box behind the text block, BotW-style framing.
        GameObject textBack = NewRect("TextBackdrop", panel.transform, out RectTransform backRt);
        backRt.anchorMin = Vector2.zero;
        backRt.anchorMax = Vector2.zero;
        backRt.pivot = Vector2.zero;
        backRt.anchoredPosition = new Vector2(TextLeft - 44f, 40f);
        backRt.sizeDelta = new Vector2(TextWidth + 88f, 226f);
        Image backImg = textBack.AddComponent<Image>();
        backImg.sprite = softBox;
        backImg.type = Image.Type.Sliced;
        backImg.color = new Color(0f, 0f, 0f, 0.55f);
        backImg.raycastTarget = false;

        // Speaker name, then the dialogue line under it, padded inside the backdrop.
        TMP_Text nameText = NewText(panel.transform, "SpeakerName",
            new Vector2(TextLeft, 212f), new Vector2(TextWidth, 34f),
            22f, FontStyles.Bold, NameColor, TextAlignmentOptions.Left);
        nameText.characterSpacing = 2f;

        TMP_Text dialogueText = NewText(panel.transform, "DialogueText",
            new Vector2(TextLeft, 58f), new Vector2(TextWidth, 144f),
            26f, FontStyles.Normal, TextColor, TextAlignmentOptions.TopLeft);
        dialogueText.richText = true;
        dialogueText.lineSpacing = 8f;

        // 4. Choice buttons: dark rounded stack at the lower right, index 0 on top.
        Button[] buttons = new Button[ChoiceCount];
        TMP_Text[] labels = new TMP_Text[ChoiceCount];
        for (int i = 0; i < ChoiceCount; i++)
        {
            GameObject btnGO = NewRect($"Choice{i}", panel.transform, out RectTransform btnRt);
            btnRt.anchorMin = new Vector2(1f, 0f);
            btnRt.anchorMax = new Vector2(1f, 0f);
            btnRt.pivot = new Vector2(1f, 0f);
            btnRt.anchoredPosition = new Vector2(-56f, 64f + (ChoiceCount - 1 - i) * 64f);
            btnRt.sizeDelta = new Vector2(450f, 54f);

            Image btnImg = btnGO.AddComponent<Image>();
            btnImg.sprite = rounded;
            btnImg.type = Image.Type.Sliced;
            btnImg.pixelsPerUnitMultiplier = 1.5f; // keeps the corner radius tidy at 54px height
            btnImg.color = ButtonColor;

            // Thin light outline, the BotW button signature.
            GameObject frameGO = NewRect("Frame", btnGO.transform, out RectTransform frameRt);
            frameRt.anchorMin = Vector2.zero;
            frameRt.anchorMax = Vector2.one;
            frameRt.offsetMin = Vector2.zero;
            frameRt.offsetMax = Vector2.zero;
            Image frameImg = frameGO.AddComponent<Image>();
            frameImg.sprite = frame;
            frameImg.type = Image.Type.Sliced;
            frameImg.pixelsPerUnitMultiplier = 1.5f;
            frameImg.color = new Color(1f, 1f, 1f, 0.22f);
            frameImg.raycastTarget = false;

            Button button = btnGO.AddComponent<Button>();
            button.targetGraphic = btnImg;
            button.transition = Selectable.Transition.ColorTint;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(2.4f, 2.3f, 1.9f, 1f); // dark base needs a strong hover lift
            colors.pressedColor = new Color(1.5f, 1.5f, 1.4f, 1f);
            colors.selectedColor = new Color(2f, 1.95f, 1.7f, 1f);
            colors.fadeDuration = 0.08f;
            button.colors = colors;

            TMP_Text label = NewText(btnGO.transform, "Label",
                Vector2.zero, Vector2.zero,
                18f, FontStyles.Normal, LabelColor, TextAlignmentOptions.Left);
            RectTransform labelRt = label.rectTransform;
            labelRt.anchorMin = Vector2.zero;
            labelRt.anchorMax = Vector2.one;
            labelRt.pivot = new Vector2(0.5f, 0.5f);
            labelRt.offsetMin = new Vector2(24f, 7f);
            labelRt.offsetMax = new Vector2(-24f, -7f);

            buttons[i] = button;
            labels[i] = label;
        }

        // 5. Rewire the manager to the new UI and retire the old panel for good.
        so.FindProperty("dialoguePanel").objectReferenceValue = panel;
        so.FindProperty("speakerNameText").objectReferenceValue = nameText;
        so.FindProperty("dialogueText").objectReferenceValue = dialogueText;
        SerializedProperty buttonsProp = so.FindProperty("choiceButtons");
        SerializedProperty textsProp = so.FindProperty("choiceTexts");
        buttonsProp.arraySize = ChoiceCount;
        textsProp.arraySize = ChoiceCount;
        for (int i = 0; i < ChoiceCount; i++)
        {
            buttonsProp.GetArrayElementAtIndex(i).objectReferenceValue = buttons[i];
            textsProp.GetArrayElementAtIndex(i).objectReferenceValue = labels[i];
        }
        so.ApplyModifiedProperties();

        if (oldPanel != null)
        {
            Undo.RecordObject(oldPanel, "Disable legacy dialogue panel");
            oldPanel.SetActive(false);
        }

        // Hidden until the manager opens a conversation.
        panel.SetActive(false);

        EditorUtility.SetDirty(dm);
        EditorSceneManager.MarkSceneDirty(canvasGO.scene);
        Debug.Log("[DialogueUIRestyler] Built DialogueCanvas_v2 (clean BotW-style UI), rewired DialogueManager, disabled the legacy panel. SAVE THE SCENE (Ctrl+S).");
    }

    #region Builders
    private static GameObject NewRect(string name, Transform parent, out RectTransform rt)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        rt = go.GetComponent<RectTransform>();
        return go;
    }

    private static TMP_Text NewText(Transform parent, string name, Vector2 anchoredPosition, Vector2 size,
        float fontSize, FontStyles style, Color color, TextAlignmentOptions alignment)
    {
        GameObject go = NewRect(name, parent, out RectTransform rt);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.zero;
        rt.pivot = Vector2.zero;
        rt.anchoredPosition = anchoredPosition;
        rt.sizeDelta = size;

        TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
        text.enableAutoSizing = false;
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.color = color;
        text.alignment = alignment;
        text.raycastTarget = false;
        return text;
    }
    #endregion

    #region Sprites
    /// <summary>
    /// White 9-sliceable rounded rectangle with anti-aliased corners, tintable by Image.color.
    /// </summary>
    private static Sprite EnsureRoundedSprite()
    {
        AssetDatabase.DeleteAsset(RoundedPath); // regenerate every run so tuning lands
        EnsureFolder();

        const int size = 128;
        const float radius = 30f;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color32[] pixels = new Color32[size * size];

        for (int yPix = 0; yPix < size; yPix++)
        {
            for (int xPix = 0; xPix < size; xPix++)
            {
                float alpha = RoundedRectAlpha(xPix + 0.5f, yPix + 0.5f, size, size, radius);
                pixels[yPix * size + xPix] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply();
        File.WriteAllBytes(RoundedPath, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);

        ImportSprite(RoundedPath, new Vector4(36f, 36f, 36f, 36f));
        return AssetDatabase.LoadAssetAtPath<Sprite>(RoundedPath);
    }

    /// <summary>
    /// Black vertical gradient: strongest at the bottom edge, fully transparent by 45%
    /// of screen height. Stretched over the panel it gives the BotW soft backdrop.
    /// </summary>
    private static Sprite EnsureGradientSprite()
    {
        AssetDatabase.DeleteAsset(GradientPath); // regenerate every run so tuning lands
        EnsureFolder();

        const int w = 8;
        const int h = 256;
        const float fadeTop = 0.5f;
        const float maxAlpha = 0.88f;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        Color32[] pixels = new Color32[w * h];

        for (int yPix = 0; yPix < h; yPix++)
        {
            float t = yPix / (float)(h - 1);                  // 0 = bottom row, 1 = top row
            float a = Mathf.Clamp01((fadeTop - t) / fadeTop); // 1 at bottom, 0 at fadeTop
            a = Mathf.Pow(a, 1.35f) * maxAlpha;
            byte alpha = (byte)Mathf.RoundToInt(a * 255f);
            for (int xPix = 0; xPix < w; xPix++)
                pixels[yPix * w + xPix] = new Color32(0, 0, 0, alpha);
        }

        tex.SetPixels32(pixels);
        tex.Apply();
        File.WriteAllBytes(GradientPath, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);

        ImportSprite(GradientPath, Vector4.zero);
        return AssetDatabase.LoadAssetAtPath<Sprite>(GradientPath);
    }

    /// <summary>
    /// Heavily feathered rounded rectangle: a soft shadow box, the BotW text backdrop.
    /// </summary>
    private static Sprite EnsureSoftBoxSprite()
    {
        AssetDatabase.DeleteAsset(SoftBoxPath);
        EnsureFolder();

        const int size = 192;
        const float radius = 48f;
        const float feather = 34f;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color32[] pixels = new Color32[size * size];

        for (int yPix = 0; yPix < size; yPix++)
        {
            for (int xPix = 0; xPix < size; xPix++)
            {
                float dx = Mathf.Max(Mathf.Abs(xPix + 0.5f - size * 0.5f) - (size * 0.5f - radius), 0f);
                float dy = Mathf.Max(Mathf.Abs(yPix + 0.5f - size * 0.5f) - (size * 0.5f - radius), 0f);
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01((radius - dist) / feather);
                a = a * a * (3f - 2f * a); // smoothstep for a blur-like falloff
                pixels[yPix * size + xPix] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply();
        File.WriteAllBytes(SoftBoxPath, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);

        ImportSprite(SoftBoxPath, new Vector4(80f, 80f, 80f, 80f));
        return AssetDatabase.LoadAssetAtPath<Sprite>(SoftBoxPath);
    }

    /// <summary>
    /// Thin anti-aliased outline ring matching the rounded rect, for button frames.
    /// </summary>
    private static Sprite EnsureFrameSprite()
    {
        AssetDatabase.DeleteAsset(FramePath);
        EnsureFolder();

        const int size = 128;
        const float radius = 30f;
        const float thickness = 2.5f;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color32[] pixels = new Color32[size * size];

        for (int yPix = 0; yPix < size; yPix++)
        {
            for (int xPix = 0; xPix < size; xPix++)
            {
                float dx = Mathf.Max(Mathf.Abs(xPix + 0.5f - size * 0.5f) - (size * 0.5f - radius), 0f);
                float dy = Mathf.Max(Mathf.Abs(yPix + 0.5f - size * 0.5f) - (size * 0.5f - radius), 0f);
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                float edge = Mathf.Abs(dist - (radius - thickness)); // band around the inset edge
                float a = Mathf.Clamp01(thickness - edge + 0.5f) * Mathf.Clamp01(radius - dist + 0.5f);
                pixels[yPix * size + xPix] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply();
        File.WriteAllBytes(FramePath, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);

        ImportSprite(FramePath, new Vector4(36f, 36f, 36f, 36f));
        return AssetDatabase.LoadAssetAtPath<Sprite>(FramePath);
    }

    private static void EnsureFolder()
    {
        if (!AssetDatabase.IsValidFolder(SpriteFolder))
            AssetDatabase.CreateFolder("Assets", "UI");
    }

    private static void ImportSprite(string path, Vector4 border)
    {
        AssetDatabase.ImportAsset(path);
        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.spriteBorder = border;
        TextureImporterSettings settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect;
        importer.SetTextureSettings(settings);
        importer.SaveAndReimport();
    }

    private static float RoundedRectAlpha(float x, float y, float w, float h, float r)
    {
        float dx = Mathf.Max(Mathf.Abs(x - w * 0.5f) - (w * 0.5f - r), 0f);
        float dy = Mathf.Max(Mathf.Abs(y - h * 0.5f) - (h * 0.5f - r), 0f);
        float dist = Mathf.Sqrt(dx * dx + dy * dy);
        return Mathf.Clamp01(r - dist + 0.5f);
    }
    #endregion
}