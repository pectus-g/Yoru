using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// One-click creation of the Dialogue System v2 test content.
///
/// Menu: YORU > Create Test Dialogue Assets
///   1. Creates Assets/Quests/Nopperabo_Quest.asset and Assets/Quests/Kodama_Quest.asset.
///   2. Populates the EXISTING Nopperabo_Dialogue.asset and Kodama_Dialogue.asset in
///      Assets/DialogueData in place with their full v2 conversations. Populating in
///      place preserves each asset GUID, so the scene InteractableEnemy wiring survives.
///      Missing assets are created at the same paths.
///   3. Wires each quest as questToGive on its dialogue.
///
/// Data plumbing only. Future souls get their own entries here.
/// Safe to re-run: it overwrites the same assets with the same content.
/// </summary>
public static class DialogueAssetCreator
{
    private const string QuestFolder = "Assets/Quests";
    private const string QuestPath = QuestFolder + "/Nopperabo_Quest.asset";
    private const string DialoguePath = "Assets/DialogueData/Nopperabo_Dialogue.asset";
    private const string KodamaQuestPath = QuestFolder + "/Kodama_Quest.asset";
    private const string KodamaDialoguePath = "Assets/DialogueData/Kodama_Dialogue.asset";

    [MenuItem("YORU/Create Test Dialogue Assets")]
    public static void CreateTestDialogueAssets()
    {
        QuestData quest = CreateOrLoad<QuestData>(QuestPath, QuestFolder);
        PopulateQuest(quest);

        DialogueData dialogue = CreateOrLoad<DialogueData>(DialoguePath, "Assets/DialogueData");
        PopulateDialogue(dialogue, quest);

        QuestData kodamaQuest = CreateOrLoad<QuestData>(KodamaQuestPath, QuestFolder);
        PopulateKodamaQuest(kodamaQuest);

        DialogueData kodamaDialogue = CreateOrLoad<DialogueData>(KodamaDialoguePath, "Assets/DialogueData");
        PopulateKodamaDialogue(kodamaDialogue, kodamaQuest);

        EditorUtility.SetDirty(quest);
        EditorUtility.SetDirty(dialogue);
        EditorUtility.SetDirty(kodamaQuest);
        EditorUtility.SetDirty(kodamaDialogue);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[DialogueAssetCreator] Created/updated: {QuestPath}");
        Debug.Log($"[DialogueAssetCreator] Created/updated: {DialoguePath} (in place, scene wiring preserved)");
        Debug.Log($"[DialogueAssetCreator] Created/updated: {KodamaQuestPath}");
        Debug.Log($"[DialogueAssetCreator] Created/updated: {KodamaDialoguePath} (in place, scene wiring preserved)");
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

    private static void PopulateKodamaQuest(QuestData quest)
    {
        quest.displayName = "The Last Seed";
        quest.description = "A small forest spirit lost its tree. Its last <color=#E8A33D>seed</color> fell into the river and drifted <color=#7FD4A8>downstream past the red stones</color>. Find the seed and plant it in the <color=#7FD4A8>bare clearing</color> where the tree once stood.";
    }

    private static void PopulateKodamaDialogue(DialogueData d, QuestData quest)
    {
        d.dialogueId = "kodama";
        d.soulName = "Kodama";
        d.startBeatId = "B1";
        d.soulDefaultGoodbye = "...the leaves are quieter than you. Goodbye.";
        d.cooldownSeconds = 10f; // Test value. Production: 300.
        d.endBehavior = DialogueEndBehavior.GIVE_QUEST;
        d.questToGive = quest;
        d.miniGameToTrigger = null;

        d.postQuestWaitingLine = "Is it in the earth yet...? No. No, I would feel it. Go gently. Seeds frighten easily.";
        d.postQuestLeaveText = "Soon, little one.";
        d.postQuestAskAgainText = "What was it you asked for?";

        d.mistakenIdentityLine = "...oh! You are not the cat-thing. Forgive me, I hid for nothing. The shadows make monsters of everyone.";

        d.beats = new List<DialogueBeat>
        {
            Beat("B1",
                "...you can see me?",
                "Then look away. Everyone does, in the end.",
                Opt("I can. Are you hurt?", DialogueBranchType.CORRECT, "B2"),
                Opt("What are you doing here, spirit?", DialogueBranchType.SOFT_WRONG, "B1A"),
                OptGoodbye("Leave this place.", "...it was my place first. But yes. I will leave it too, soon enough.")),

            Beat("B1A",
                "Doing...? I am not doing anything. I am only... left.",
                "Left, and best left alone.",
                Opt("Then tell me what happened.", DialogueBranchType.CORRECT, "B2"),
                OptGoodbye("Spirits should move on.", "Move on to where? My home was the moving-on place. Go.")),

            Beat("B2",
                "My tree... they took it. All of it. There was a seed... I saw it fall into the water.",
                "The water took the last of it. The water can keep you too.",
                Opt("Where did the river carry it?", DialogueBranchType.CORRECT, "B3"),
                Opt("I will find who cut it down.", DialogueBranchType.SOFT_WRONG, "B2A"),
                OptGoodbye("Trees die. That is the way of things.", "The way of things. Yes. And I am the part that stays behind. Leave me to it.")),

            Beat("B2A",
                "No. No anger. Anger is how forests burn. Only the seed matters now.",
                "Put the axe out of your mind, or leave.",
                Opt("Then the seed. Where did it fall?", DialogueBranchType.CORRECT, "B3"),
                OptGoodbye("The ones who did this deserve worse.", "Then you and the axe are the same shape. Go away.")),

            Beat("B3",
                "Downstream... past the red stones where the herons stand. It is so small. Smaller than hope.",
                "Forget it. The river forgets everything eventually.",
                Opt("Small things grow. I will find it.", DialogueBranchType.CORRECT, "B4"),
                OptGoodbye("A single seed in a whole river? Impossible.", "...yes. Impossible. That is what the herons said too.")),

            Beat("B4",
                "Find it. Plant it where my tree stood... the bare earth still remembers the roots. Then I can sleep.",
                "Then I will wait for kinder hands.",
                Opt("Rest soon, little one. I will plant your forest.", DialogueBranchType.FINAL_SUCCESS, ""),
                Opt("And if the seed did not survive the water?", DialogueBranchType.SOFT_WRONG, "B4A"),
                OptGoodbye("What do I gain from digging in the dirt?", "...nothing. You gain nothing. Forget me.")),

            Beat("B4A",
                "It survived. Seeds are patient... more patient than grief. Please.",
                "If you cannot believe in a seed, you cannot help me.",
                Opt("Then I will bring it home.", DialogueBranchType.FINAL_SUCCESS, ""),
                OptGoodbye("You put too much faith in one seed.", "Faith is all a forest is, before it is a forest. Goodbye."))
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