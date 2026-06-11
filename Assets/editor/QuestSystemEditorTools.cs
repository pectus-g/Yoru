using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The quest system's editor tooling, one file, two menu items:
///
/// YORU > Build Memory Parchments UI
///   Deletes any existing MemoryParchmentCanvas and builds it fresh: dim backdrop,
///   centred portrait parchment, title, five soul entry slots, page arrows, all wired
///   to MemoryParchmentUI via SerializedObject. Parchment art per tier, in priority:
///     Assets/UI/Parchments/Parchment_Tier{1..4}.png   (Hazel's AI art, per tier)
///     Assets/UI/Parchments/Parchment.png              (one shared painting)
///     a generated placeholder (aged paper, gold clouds, gold tree silhouettes)
///   Drop real art in, re-run the menu, done. The first TMP font in Assets/Fonts is
///   applied to every text, same rule as the dialogue restyler. Canvas sortingOrder is
///   40, below the dialogue canvas (50): a conversation always draws over the parchments.
///
/// YORU > Create Stolen Face Quest Assets
///   Updates Nopperabo_Quest.asset IN PLACE with the quest fields (steps, WORLD_EVENT
///   resolution, the LIAR! stamp). displayName and description are deliberately NOT
///   touched; DialogueAssetCreator owns them (single source, no divergence). Stamps
///   tier 3 + the parchment story on Nopperabo_Dialogue.asset, and creates
///   MujinaReveal_Dialogue.asset (endBehavior NONE; MujinaRevealController listens for
///   its FINAL_SUCCESS). Safe to re-run; run AFTER DialogueAssetCreator's menu when
///   starting fresh.
/// </summary>
public static class QuestSystemEditorTools
{
    #region Parchment UI Builder
    private const string CanvasName = "MemoryParchmentCanvas";
    private const string ArtFolder = "Assets/UI/Parchments";
    private const int SlotCount = 5;

    private static TMP_FontAsset customFont;

    [MenuItem("YORU/Build Memory Parchments UI")]
    public static void BuildParchmentUI()
    {
        GameObject existing = GameObject.Find(CanvasName);
        if (existing != null)
            Object.DestroyImmediate(existing);

        customFont = FindCustomFont();

        // ---------- Canvas ----------
        GameObject canvasGo = new GameObject(CanvasName, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 40; // dialogue canvas is 50; conversations draw on top

        CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        MemoryParchmentUI ui = canvasGo.AddComponent<MemoryParchmentUI>();

        // ---------- Panel root (everything toggled by J) ----------
        GameObject panel = NewRect("PanelRoot", canvasGo.transform, out RectTransform panelRt);
        Stretch(panelRt);

        GameObject dim = NewRect("Dim", panel.transform, out RectTransform dimRt);
        Stretch(dimRt);
        Image dimImage = dim.AddComponent<Image>();
        dimImage.color = new Color(0f, 0f, 0f, 0.55f);

        // ---------- The parchment ----------
        GameObject parchment = NewRect("Parchment", panel.transform, out RectTransform parchRt);
        parchRt.sizeDelta = new Vector2(760f, 950f);
        Image parchmentImage = parchment.AddComponent<Image>();
        parchmentImage.sprite = LoadTierArt(3) ?? EnsurePlaceholderParchment();
        parchmentImage.preserveAspect = false;

        // ---------- Title and page label ----------
        TextMeshProUGUI title = NewText("Title", parchment.transform, "Memories of the Lost",
            34f, FontStyles.Bold, TextAlignmentOptions.Center, InkColor());
        Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -64f), new Vector2(640f, 48f));

        TextMeshProUGUI pageLabel = NewText("PageLabel", parchment.transform, "Parchment II   .   Tier 3 Souls",
            18f, FontStyles.Italic, TextAlignmentOptions.Center, FadedInkColor());
        Place(pageLabel.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -104f), new Vector2(640f, 26f));

        // ---------- Entry slots ----------
        var slots = new List<MemoryParchmentUI.EntrySlot>();
        const float slotHeight = 148f;
        const float firstSlotY = -210f;
        for (int i = 0; i < SlotCount; i++)
        {
            slots.Add(BuildEntrySlot(parchment.transform, i, firstSlotY - i * slotHeight, slotHeight));
        }

        // ---------- Page arrows ----------
        Button prev = BuildArrowButton(panel.transform, "PreviousPage", "<", new Vector2(-510f, 0f));
        Button next = BuildArrowButton(panel.transform, "NextPage", ">", new Vector2(510f, 0f));
        UnityEditor.Events.UnityEventTools.AddIntPersistentListener(prev.onClick, ui.ChangePage, -1);
        UnityEditor.Events.UnityEventTools.AddIntPersistentListener(next.onClick, ui.ChangePage, 1);

        // ---------- Close hint ----------
        TextMeshProUGUI hint = NewText("CloseHint", panel.transform, "J / Esc to close",
            16f, FontStyles.Normal, TextAlignmentOptions.Center, new Color(0.85f, 0.85f, 0.85f, 0.7f));
        Place(hint.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 36f), new Vector2(400f, 24f));

        // ---------- Wire MemoryParchmentUI ----------
        SerializedObject so = new SerializedObject(ui);
        so.FindProperty("panelRoot").objectReferenceValue = panel;
        so.FindProperty("parchmentImage").objectReferenceValue = parchmentImage;
        so.FindProperty("titleText").objectReferenceValue = title;
        so.FindProperty("pageLabelText").objectReferenceValue = pageLabel;
        so.FindProperty("previousPageButton").objectReferenceValue = prev;
        so.FindProperty("nextPageButton").objectReferenceValue = next;

        SerializedProperty slotsProp = so.FindProperty("entrySlots");
        slotsProp.arraySize = slots.Count;
        for (int i = 0; i < slots.Count; i++)
        {
            SerializedProperty s = slotsProp.GetArrayElementAtIndex(i);
            s.FindPropertyRelative("root").objectReferenceValue = slots[i].root;
            s.FindPropertyRelative("nameText").objectReferenceValue = slots[i].nameText;
            s.FindPropertyRelative("strikeLine").objectReferenceValue = slots[i].strikeLine;
            s.FindPropertyRelative("storyText").objectReferenceValue = slots[i].storyText;
            s.FindPropertyRelative("questNameText").objectReferenceValue = slots[i].questNameText;
            s.FindPropertyRelative("questHintText").objectReferenceValue = slots[i].questHintText;
            s.FindPropertyRelative("statusText").objectReferenceValue = slots[i].statusText;
            s.FindPropertyRelative("trackButton").objectReferenceValue = slots[i].trackButton;
            s.FindPropertyRelative("trackLabel").objectReferenceValue = slots[i].trackLabel;
        }

        SerializedProperty artProp = so.FindProperty("tierParchmentSprites");
        artProp.arraySize = 4;
        for (int tier = 1; tier <= 4; tier++)
        {
            Sprite art = LoadTierArt(tier) ?? parchmentImage.sprite;
            artProp.GetArrayElementAtIndex(tier - 1).objectReferenceValue = art;
        }

        so.ApplyModifiedPropertiesWithoutUndo();

        panel.SetActive(false);
        Undo.RegisterCreatedObjectUndo(canvasGo, "Build Memory Parchments UI");
        EditorUtility.SetDirty(canvasGo);

        Debug.Log("[ParchmentBuilder] MemoryParchmentCanvas built. J opens it in Play mode. " +
                  $"Drop art into {ArtFolder} as Parchment_Tier1..4.png (or a single Parchment.png) and re-run this menu to apply it. " +
                  (customFont != null ? $"Font applied: {customFont.name}." : "No TMP font in Assets/Fonts; using TMP default."));
    }

    private static MemoryParchmentUI.EntrySlot BuildEntrySlot(Transform parchment, int index, float y, float height)
    {
        var slot = new MemoryParchmentUI.EntrySlot();

        GameObject root = NewRect($"Entry_{index}", parchment, out RectTransform rt);
        Place(rt, new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(640f, height));
        slot.root = root;

        // Left column (0..320): soul name with its strike line, story below.
        slot.nameText = NewText("Name", root.transform, "Soul Name",
            26f, FontStyles.Bold, TextAlignmentOptions.TopLeft, InkColor());
        Place(slot.nameText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, -8f), new Vector2(320f, 32f));

        // Strike-through line over the name (the mujina's lie). Disabled by default.
        GameObject strike = NewRect("StrikeLine", slot.nameText.transform, out RectTransform strikeRt);
        Place(strikeRt, new Vector2(0f, 0.5f), new Vector2(0f, 0f), new Vector2(240f, 3f));
        slot.strikeLine = strike.AddComponent<Image>();
        slot.strikeLine.color = new Color(0.45f, 0.12f, 0.1f, 0.95f);
        slot.strikeLine.raycastTarget = false;
        slot.strikeLine.enabled = false;

        slot.storyText = NewText("Story", root.transform, "Their story, as the parchment remembers it.",
            16f, FontStyles.Italic, TextAlignmentOptions.TopLeft, FadedInkColor());
        Place(slot.storyText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, -46f), new Vector2(320f, 86f));

        // Right column (350..640): status stamp on top, quest name, hint, track button.
        slot.statusText = NewText("Status", root.transform, "Found the Peace",
            20f, FontStyles.Bold | FontStyles.Italic, TextAlignmentOptions.TopRight, new Color(0.62f, 0.78f, 0.55f, 1f));
        Place(slot.statusText.rectTransform, new Vector2(1f, 1f), new Vector2(0f, -8f), new Vector2(280f, 28f));

        slot.questNameText = NewText("QuestName", root.transform, "Quest Name",
            18f, FontStyles.Bold, TextAlignmentOptions.TopLeft, InkColor());
        Place(slot.questNameText.rectTransform, new Vector2(1f, 1f), new Vector2(0f, -46f), new Vector2(280f, 24f));

        slot.questHintText = NewText("QuestHint", root.transform, "Current objective hint.",
            14f, FontStyles.Normal, TextAlignmentOptions.TopLeft, FadedInkColor());
        Place(slot.questHintText.rectTransform, new Vector2(1f, 1f), new Vector2(0f, -72f), new Vector2(280f, 40f));

        // Track button: transparent fill, glowing outline, matching the dialogue restyle language.
        GameObject buttonGo = NewRect("TrackButton", root.transform, out RectTransform buttonRt);
        Place(buttonRt, new Vector2(1f, 0f), new Vector2(0f, 8f), new Vector2(190f, 32f));
        Image buttonImage = buttonGo.AddComponent<Image>();
        buttonImage.sprite = LoadUiSprite("YORU_RoundedFrame");
        buttonImage.type = Image.Type.Sliced;
        buttonImage.color = new Color(1f, 0.92f, 0.75f, 0.9f);

        slot.trackButton = buttonGo.AddComponent<Button>();
        ColorBlock colors = slot.trackButton.colors;
        colors.highlightedColor = new Color(1f, 1f, 1f, 1f);
        colors.pressedColor = new Color(0.85f, 0.78f, 0.6f, 1f);
        slot.trackButton.colors = colors;

        slot.trackLabel = NewText("Label", buttonGo.transform, "Follow the Glow",
            15f, FontStyles.Normal, TextAlignmentOptions.Center, new Color(0.98f, 0.95f, 0.88f, 1f));
        Stretch(slot.trackLabel.rectTransform);

        // Faint divider under the slot.
        GameObject divider = NewRect("Divider", root.transform, out RectTransform divRt);
        Place(divRt, new Vector2(0.5f, 0f), new Vector2(0f, 4f), new Vector2(560f, 2f));
        Image divImage = divider.AddComponent<Image>();
        divImage.color = new Color(0.35f, 0.25f, 0.15f, 0.25f);
        divImage.raycastTarget = false;

        root.SetActive(false);
        return slot;
    }

    private static Button BuildArrowButton(Transform parent, string name, string label, Vector2 offset)
    {
        GameObject go = NewRect(name, parent, out RectTransform rt);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = offset;
        rt.sizeDelta = new Vector2(64f, 64f);

        Image image = go.AddComponent<Image>();
        image.sprite = LoadUiSprite("YORU_RoundedFrame");
        image.type = Image.Type.Sliced;
        image.color = new Color(1f, 0.92f, 0.75f, 0.85f);

        Button button = go.AddComponent<Button>();

        TextMeshProUGUI text = NewText("Label", go.transform, label,
            30f, FontStyles.Bold, TextAlignmentOptions.Center, new Color(0.98f, 0.95f, 0.88f, 1f));
        Stretch(text.rectTransform);

        return button;
    }
    #endregion

    #region Parchment Art
    private static Sprite LoadTierArt(int tier)
    {
        Sprite perTier = AssetDatabase.LoadAssetAtPath<Sprite>($"{ArtFolder}/Parchment_Tier{tier}.png");
        if (perTier != null) return perTier;
        return AssetDatabase.LoadAssetAtPath<Sprite>($"{ArtFolder}/Parchment.png");
    }

    /// <summary>
    /// Generates the placeholder parchment once: aged warm paper, mottling, darker
    /// vignette, kindei gold cloud bands, faint gold tree silhouettes. Saved to
    /// Assets/UI/Parchments so it imports as a normal sprite.
    /// </summary>
    private static Sprite EnsurePlaceholderParchment()
    {
        string path = $"{ArtFolder}/YORU_Parchment_Placeholder.png";
        Sprite existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (existing != null) return existing;

        EnsureFolder(ArtFolder);

        const int w = 768, h = 960;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[w * h];

        Color paperLight = new Color(0.91f, 0.86f, 0.73f);
        Color paperDark = new Color(0.78f, 0.70f, 0.54f);
        Color edgeBrown = new Color(0.45f, 0.36f, 0.24f);
        Color gold = new Color(0.85f, 0.68f, 0.32f);

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float mottle = Mathf.PerlinNoise(x * 0.013f, y * 0.013f) * 0.7f
                             + Mathf.PerlinNoise(x * 0.09f, y * 0.09f) * 0.3f;
                Color c = Color.Lerp(paperLight, paperDark, mottle * 0.55f);

                // Gold cloud bands, heavier toward top and bottom, faded in the centre
                // so the entry text stays legible.
                float cloud = Mathf.PerlinNoise(x * 0.006f + 31.7f, y * 0.006f + 11.3f);
                float vertical = Mathf.Abs((y / (float)h) - 0.5f) * 2f; // 0 centre, 1 edges
                float cloudStrength = Mathf.SmoothStep(0f, 1f, (cloud - 0.55f) * 4f) * Mathf.Lerp(0.08f, 0.5f, vertical);
                if (cloudStrength > 0f)
                    c = Color.Lerp(c, gold, Mathf.Clamp01(cloudStrength));

                // Gold flecks.
                float fleck = Mathf.PerlinNoise(x * 0.8f + 77.7f, y * 0.8f + 5.1f);
                if (fleck > 0.93f)
                    c = Color.Lerp(c, gold, (fleck - 0.93f) * 8f);

                // Vignette.
                float dx = (x / (float)w) - 0.5f;
                float dy = (y / (float)h) - 0.5f;
                float edge = Mathf.Clamp01((Mathf.Sqrt(dx * dx + dy * dy) - 0.42f) * 3.2f);
                c = Color.Lerp(c, edgeBrown, edge * 0.8f);

                pixels[y * w + x] = c;
            }
        }

        // Faint gold tree silhouettes, kept near the bottom corners.
        DrawTree(pixels, w, h, (int)(w * 0.16f), (int)(h * 0.10f), 95f, 90f, 5, gold, 0.35f);
        DrawTree(pixels, w, h, (int)(w * 0.84f), (int)(h * 0.08f), 80f, 90f, 5, gold, 0.30f);
        DrawTree(pixels, w, h, (int)(w * 0.55f), (int)(h * 0.05f), 60f, 88f, 4, gold, 0.22f);

        tex.SetPixels(pixels);
        tex.Apply();

        System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path);

        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.mipmapEnabled = false;
        importer.SaveAndReimport();

        Debug.Log($"[ParchmentBuilder] Placeholder parchment generated at {path}.");
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    /// <summary>
    /// Recursive branch silhouette: a trunk that splits into thinner branches.
    /// Painted softly so it reads as background art, never competing with text.
    /// </summary>
    private static void DrawTree(Color[] pixels, int w, int h, int x, int y, float length, float angleDeg, int depth, Color gold, float alpha)
    {
        if (depth <= 0 || length < 6f) return;

        float rad = angleDeg * Mathf.Deg2Rad;
        int x2 = x + Mathf.RoundToInt(Mathf.Cos(rad) * length);
        int y2 = y + Mathf.RoundToInt(Mathf.Sin(rad) * length);

        DrawSoftLine(pixels, w, h, x, y, x2, y2, Mathf.Max(1f, depth * 0.9f), gold, alpha);

        DrawTree(pixels, w, h, x2, y2, length * 0.68f, angleDeg + 27f, depth - 1, gold, alpha * 0.92f);
        DrawTree(pixels, w, h, x2, y2, length * 0.68f, angleDeg - 24f, depth - 1, gold, alpha * 0.92f);
        if (depth >= 4)
            DrawTree(pixels, w, h, x2, y2, length * 0.5f, angleDeg + 3f, depth - 2, gold, alpha * 0.85f);
    }

    private static void DrawSoftLine(Color[] pixels, int w, int h, int x1, int y1, int x2, int y2, float thickness, Color gold, float alpha)
    {
        int steps = Mathf.Max(Mathf.Abs(x2 - x1), Mathf.Abs(y2 - y1));
        for (int s = 0; s <= steps; s++)
        {
            float t = steps == 0 ? 0f : s / (float)steps;
            int cx = Mathf.RoundToInt(Mathf.Lerp(x1, x2, t));
            int cy = Mathf.RoundToInt(Mathf.Lerp(y1, y2, t));
            int r = Mathf.CeilToInt(thickness);

            for (int oy = -r; oy <= r; oy++)
            {
                for (int ox = -r; ox <= r; ox++)
                {
                    int px = cx + ox, py = cy + oy;
                    if (px < 0 || px >= w || py < 0 || py >= h) continue;

                    float d = Mathf.Sqrt(ox * ox + oy * oy) / Mathf.Max(1f, thickness);
                    if (d > 1f) continue;

                    int idx = py * w + px;
                    float a = alpha * (1f - d * d);
                    pixels[idx] = Color.Lerp(pixels[idx], gold, a);
                }
            }
        }
    }
    #endregion

    #region Stolen Face Asset Creator
    private const string QuestPath = "Assets/Quests/Nopperabo_Quest.asset";
    private const string NopperaboDialoguePath = "Assets/DialogueData/Nopperabo_Dialogue.asset";
    private const string RevealDialoguePath = "Assets/DialogueData/MujinaReveal_Dialogue.asset";

    [MenuItem("YORU/Create Stolen Face Quest Assets")]
    public static void CreateStolenFaceAssets()
    {
        QuestData quest = AssetDatabase.LoadAssetAtPath<QuestData>(QuestPath);
        if (quest == null)
        {
            Debug.LogError($"[QuestAssets] {QuestPath} not found. Run YORU > Create Test Dialogue Assets first.");
            return;
        }
        PopulateQuest(quest);

        DialogueData nopperabo = AssetDatabase.LoadAssetAtPath<DialogueData>(NopperaboDialoguePath);
        if (nopperabo != null)
        {
            nopperabo.tier = 3;
            nopperabo.soulStory = "A faceless woman wept on the road, begging for the face a beast had stolen. Every word of it was a mask.";
            EditorUtility.SetDirty(nopperabo);
        }
        else
        {
            Debug.LogWarning($"[QuestAssets] {NopperaboDialoguePath} not found; tier and story not stamped.");
        }

        DialogueData reveal = CreateOrLoad<DialogueData>(RevealDialoguePath, "Assets/DialogueData");
        PopulateRevealDialogue(reveal);

        EditorUtility.SetDirty(quest);
        EditorUtility.SetDirty(reveal);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[QuestAssets] Updated: {QuestPath}");
        Debug.Log($"[QuestAssets] Updated: {NopperaboDialoguePath} (tier 3 + parchment story)");
        Debug.Log($"[QuestAssets] Created/updated: {RevealDialoguePath}");
        Debug.Log("[QuestAssets] SCENE SETUP: " +
                  "(1) Empty GameObject with QuestManager (the journal is built in, nothing else to place). " +
                  "(2) Run YORU > Build Memory Parchments UI. " +
                  "(3) At the cave mouth: GameObject with a trigger Collider + MujinaRevealController; assign quest, caveMujina, roadsideSoul. " +
                  "(4) caveMujina: ACTIVE full enemy prefab (Nopperabo copy) standing in the cave. Fights Yoru normally. InteractableEnemy.dialogueData = MujinaReveal_Dialogue, interactionRange 25+. " +
                  "(5) GlowTrail object with questId stolen_face, stepId S1, child points along the route to the cave. " +
                  "(6) Cave loot: hand-placed ItemPickups, yours.");
    }

    private static void PopulateQuest(QuestData quest)
    {
        // displayName and description stay owned by DialogueAssetCreator.
        quest.questId = "stolen_face";
        quest.tier = 3;
        quest.giverDialogueId = "nopperabo";
        quest.resolutionType = QuestResolutionType.WORLD_EVENT;

        quest.steps = new List<QuestStep>
        {
            new QuestStep
            {
                stepId = "S1",
                hintText = "Find the <color=#7FD4A8>cave past the bent pines</color> and take back her stolen face.",
                trigger = QuestStepTrigger.ENTER_LOCATION,
                triggerId = "stolenface_cave"
            }
        };

        quest.rewards = new List<QuestReward>(); // cave loot is hand-placed

        quest.completedStatusText = "LIAR!";
        quest.strikeThroughOnComplete = true;

        // RETURN_TO_GIVER turn-in fields unused for this quest; left empty on purpose.
        quest.turnInLine = "";
        quest.turnInButtonText = "...";
        quest.farewellLine = "";
        quest.giverDisappearsOnCompletion = false;
    }

    /// <summary>
    /// The cave reveal: the noppera-bo guise drops, the mujina admits the face steal,
    /// senses Tomoe's secret (two souls in one skin), understands it is impossible, and
    /// the conversation ends. Linear beats, every option CORRECT (no strikes in a
    /// scripted scene). Draft copy; rewrite freely in the asset.
    /// </summary>
    private static void PopulateRevealDialogue(DialogueData d)
    {
        d.dialogueId = "mujina_reveal";
        d.soulName = "???";
        d.tier = 3;
        d.soulStory = "";
        d.excludeFromJournal = true; // the reveal is a scene, not a soul of its own
        d.startBeatId = "R1";
        d.endBehavior = DialogueEndBehavior.NONE;
        d.questToGive = null;
        d.miniGameToTrigger = null;
        d.cooldownSeconds = 0f;
        d.soulDefaultGoodbye = "...";
        d.postQuestWaitingLine = "";
        d.postQuestLeaveText = "";
        d.postQuestAskAgainText = "";
        d.mistakenIdentityLine = "";

        d.beats = new List<DialogueBeat>
        {
            Beat("R1",
                "You came. Through the dark, past the pines, all for a poor woman's face.",
                Opt("I have come to take back what you stole.", "R2")),

            Beat("R2",
                "Stolen? Look around, grandmother. No beast. No hoard. Only an empty cave and the fool who walked into it.",
                Opt("...You. You are the woman from the road.", "R3")),

            Beat("R3",
                "Woman. Beast. Beggar. I am whatever opens a door. A face is a key, and old keys open old locks.",
                Opt("Stay back.", "R4")),

            Beat("R4",
                "Hold still now. This will only take the rest of your...",
                Opt("...", "R5")),

            Beat("R5",
                "What... what are you? Two lights. Two souls in one skin. No. No, that face would burn me to the root.",
                Opt("Then you leave with nothing.", "R6")),

            Beat("R6",
                "With nothing, and with my own skin, which is more than most keep around me. Farewell, grandmother. This forest was getting dull anyway.",
                FinalOpt("..."))
        };
    }

    private static DialogueBeat Beat(string id, string soulLine, params DialogueOption[] options)
    {
        return new DialogueBeat
        {
            beatId = id,
            soulLine = soulLine,
            beatGoodbyeLine = "",
            options = new List<DialogueOption>(options),
            soulAudioClip = null,
            soulAnimationTrigger = "",
            cameraFramingId = ""
        };
    }

    private static DialogueOption Opt(string text, string nextBeatId)
    {
        return new DialogueOption
        {
            text = text,
            branchType = DialogueBranchType.CORRECT,
            nextBeatId = nextBeatId,
            optionGoodbyeLine = ""
        };
    }

    private static DialogueOption FinalOpt(string text)
    {
        return new DialogueOption
        {
            text = text,
            branchType = DialogueBranchType.FINAL_SUCCESS,
            nextBeatId = "",
            optionGoodbyeLine = ""
        };
    }
    #endregion

    #region Shared Helpers
    private static Color InkColor() { return new Color(0.18f, 0.13f, 0.09f, 1f); }
    private static Color FadedInkColor() { return new Color(0.32f, 0.25f, 0.17f, 1f); }

    private static Sprite LoadUiSprite(string baseName)
    {
        return AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/UI/{baseName}.png");
    }

    private static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder)) return;
        string parent = System.IO.Path.GetDirectoryName(folder).Replace('\\', '/');
        string child = System.IO.Path.GetFileName(folder);
        if (!AssetDatabase.IsValidFolder(parent))
            EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, child);
    }

    private static GameObject NewRect(string name, Transform parent, out RectTransform rt)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        return go;
    }

    private static TextMeshProUGUI NewText(string name, Transform parent, string content,
        float size, FontStyles style, TextAlignmentOptions alignment, Color color)
    {
        GameObject go = NewRect(name, parent, out RectTransform _);
        TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
        text.text = content;
        text.fontSize = size;
        text.fontStyle = style;
        text.alignment = alignment;
        text.color = color;
        text.raycastTarget = false;
        if (customFont != null)
            text.font = customFont;
        return text;
    }

    private static void Place(RectTransform rt, Vector2 anchor, Vector2 position, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = anchor;
        rt.anchoredPosition = position;
        rt.sizeDelta = size;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private static TMP_FontAsset FindCustomFont()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Fonts")) return null;
        string[] guids = AssetDatabase.FindAssets("t:TMP_FontAsset", new[] { "Assets/Fonts" });
        if (guids.Length == 0) return null;
        return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(guids[0]));
    }

    private static T CreateOrLoad<T>(string assetPath, string folder) where T : ScriptableObject
    {
        if (!AssetDatabase.IsValidFolder(folder))
        {
            string parent = System.IO.Path.GetDirectoryName(folder).Replace('\\', '/');
            string newFolder = System.IO.Path.GetFileName(folder);
            AssetDatabase.CreateFolder(parent, newFolder);
        }

        T asset = AssetDatabase.LoadAssetAtPath<T>(assetPath);
        if (asset == null)
        {
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, assetPath);
        }
        return asset;
    }
    #endregion
}
