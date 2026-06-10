using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// One-click creation of the Dialogue System v2 test content.
///
/// Menu: YORU > Create Test Dialogue Assets
///   1. Creates Assets/Quests/Nopperabo_Quest.asset (QuestData).
///   2. Populates the EXISTING Assets/DialogueData/Nopperabo_Dialogue.asset in place with
///      the full v2 Nopperabo conversation. Populating in place preserves the asset GUID,
///      so the scene's Nopperabo_prefab InteractableEnemy wiring survives untouched.
///      If the asset is missing it is created at the same path.
///   3. Wires the quest as questToGive on the dialogue.
///
/// Data plumbing only, for this one soul. Future dialogues get their own utility entries.
/// Safe to re-run: it overwrites the same two assets with the same content.
/// </summary>
public static class DialogueAssetCreator
{
    private const string QuestFolder = "Assets/Quests";
    private const string QuestPath = QuestFolder + "/Nopperabo_Quest.asset";
    private const string DialoguePath = "Assets/DialogueData/Nopperabo_Dialogue.asset";

    [MenuItem("YORU/Create Test Dialogue Assets")]
    public static void CreateTestDialogueAssets()
    {
        QuestData quest = CreateOrLoad<QuestData>(QuestPath, QuestFolder);
        PopulateQuest(quest);

        DialogueData dialogue = CreateOrLoad<DialogueData>(DialoguePath, "Assets/DialogueData");
        PopulateDialogue(dialogue, quest);

        EditorUtility.SetDirty(quest);
        EditorUtility.SetDirty(dialogue);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[DialogueAssetCreator] Created/updated: {QuestPath}");
        Debug.Log($"[DialogueAssetCreator] Created/updated: {DialoguePath} (in place, scene wiring preserved)");
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

    private static void PopulateQuest(QuestData quest)
    {
        quest.displayName = "The Stolen Face";
        quest.description = "A faceless woman on the road begs for help. A beast in a <color=#7FD4A8>cave past the bent pines</color> wears her stolen face and sleeps on a hoard of stolen things. Retrieve the <color=#E8A33D>face</color> and bring it back to her.";
    }

    private static void PopulateDialogue(DialogueData d, QuestData quest)
    {
        d.dialogueId = "nopperabo";
        d.soulName = "Nopperab\u014d";
        d.startBeatId = "B1";
        d.soulDefaultGoodbye = "...go. The night is long enough alone.";
        d.cooldownSeconds = 10f; // Test value. Production: 300.
        d.endBehavior = DialogueEndBehavior.GIVE_QUEST;
        d.questToGive = quest;
        d.miniGameToTrigger = null;

        d.postQuestWaitingLine = "Empty hands... no, do not explain. The <color=#7FD4A8>cave</color> waits past the bent pines. So will I.";
        d.postQuestLeaveText = "I will return when I have it.";
        d.postQuestAskAgainText = "What was it you asked for?";

        d.mistakenIdentityLine = "...oh. Forgive me. For a moment I thought a nekomata stalked me. The night plays tricks on what little sense I have left.";

        d.beats = new List<DialogueBeat>
        {
            Beat("B1",
                "Do not come closer. Please. I have nothing left to greet you with.",
                "Go. The road is kinder than you.",
                Opt("I did not come to stare. I came to listen.", DialogueBranchType.CORRECT, "B2"),
                Opt("Turn around. Let me see you.", DialogueBranchType.SOFT_WRONG, "B1A"),
                OptGoodbye("Souls do not weep on roadsides. Tricksters do.", "Then let the trickster weep alone. Go.")),

            Beat("B1A",
                "See me? There is nothing to see. That is the whole of my misery.",
                "...leave me to my nothing.",
                Opt("Forgive me. Tell me what happened.", DialogueBranchType.CORRECT, "B2"),
                OptGoodbye("Everyone has a face. Stop hiding yours.", "Hiding. You think I would choose this. Leave me.")),

            Beat("B2",
                "I was walking home by night. A stranger wept on this very road. I leaned close to comfort her... and woke with nothing. No eyes. No mouth. No name anyone remembers.",
                "You doubt me. Everyone doubts what they cannot see.",
                Opt("Who would do such a thing?", DialogueBranchType.CORRECT, "B3"),
                Opt("No mouth... yet you speak. How?", DialogueBranchType.SOFT_WRONG, "B2A"),
                OptGoodbye("I have heard this exact tale before. Told to frighten travelers.", "Then go laugh about it at the teahouse. They all do.")),

            Beat("B2A",
                "Grief speaks without a mouth. Surely a woman your age knows that.",
                "Enough. Questions never gave anyone their face back.",
                Opt("...I do. Go on. Tell me who took it.", DialogueBranchType.CORRECT, "B3"),
                OptGoodbye("That is a very practiced answer.", "Practiced. Yes. I have told it to the dark a thousand nights. Goodbye.")),

            Beat("B3",
                "A beast of the hills wears my <color=#E8A33D>face</color> now. It dens in a <color=#7FD4A8>cave past the bent pines</color>, sleeping on everything it has ever stolen.",
                "Forget the cave. Forget me.",
                Opt("Then I will go to this cave.", DialogueBranchType.CORRECT, "B4"),
                Opt("Why have you not taken it back yourself?", DialogueBranchType.SOFT_WRONG, "B3A"),
                OptGoodbye("A beast that steals faces... and yet it let you walk away alive.", "Alive. You call this alive. Cruel old woman.")),

            Beat("B3A",
                "With what eyes would I find the path? With what mouth would I demand it back? I am less than a shadow now.",
                "Forget the cave. Forget me.",
                Opt("Then my eyes will find it for you.", DialogueBranchType.CORRECT, "B4"),
                OptGoodbye("A shadow that found me easily enough.", "...sharp. Too sharp. Goodbye, granny.")),

            Beat("B4",
                "Bring my <color=#E8A33D>face</color> home to me. It is pale, and light as paper. The beast keeps it close while it sleeps.",
                "Then I will wait for someone kinder.",
                Opt("Wait here. I will bring it back to you.", DialogueBranchType.FINAL_SUCCESS, ""),
                Opt("And what do I get for braving a beast's den?", DialogueBranchType.SOFT_WRONG, "B4A"),
                OptGoodbye("Swear on your soul that every word is true.", "My soul... my soul is not mine to swear on. Just go.")),

            Beat("B4A",
                "The beast sleeps on its hoard. Gold, charms, trinkets of a hundred travelers. Take all of it. I want only what is mine.",
                "Greed and doubt. What a pair you carry.",
                Opt("Deal. The face for you, the rest for me.", DialogueBranchType.FINAL_SUCCESS, ""),
                OptGoodbye("A hoard of a hundred travelers... how would you know that?", "I... enough. Forget I spoke. Forget the cave."))
        };
    }

    #region Builders
    private static DialogueBeat Beat(string id, string soulLine, string beatGoodbye, params DialogueOption[] options)
    {
        return new DialogueBeat
        {
            beatId = id,
            soulLine = soulLine,
            beatGoodbyeLine = beatGoodbye,
            options = new List<DialogueOption>(options),
            soulAudioClip = null,
            soulAnimationTrigger = "",
            cameraFramingId = ""
        };
    }

    private static DialogueOption Opt(string text, DialogueBranchType type, string nextBeatId)
    {
        return new DialogueOption
        {
            text = text,
            branchType = type,
            nextBeatId = nextBeatId,
            optionGoodbyeLine = ""
        };
    }

    private static DialogueOption OptGoodbye(string text, string goodbye)
    {
        return new DialogueOption
        {
            text = text,
            branchType = DialogueBranchType.HARD_WRONG,
            nextBeatId = "",
            optionGoodbyeLine = goodbye
        };
    }
    #endregion
}
