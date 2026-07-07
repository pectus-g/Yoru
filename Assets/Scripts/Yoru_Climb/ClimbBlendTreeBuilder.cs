#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// One click rebuild of the Animator's ClimbLayer for the Zelda style climb.
///
/// WHAT IT BUILDS
///   The eight old hard switched climb states are replaced by exactly two states:
///
///     ClimbMove    a 2D directional blend tree driven by the ClimbX and ClimbY floats.
///                  The clips blend continuously with the input direction, so there is no
///                  discrete side state that can fail to fire and no visible cut between
///                  moves. Layout (x = sideways, y = up and down):
///                      RunL(-2,0)  SideL(-1,0)  Idle(0,0)  SideR(1,0)  RunR(2,0)
///                                               Up(0,1)
///                                               Down(0,-1)
///                  Shift pushes ClimbX toward plus or minus 2, which blends sideways
///                  walking into the wall run. Everything in between blends smoothly.
///
///     ClimbMantle  the pull up over the ledge, unchanged concept, played by code.
///
///   No transition arrows are wired. ClimbController drives the two floats every frame and
///   CrossFades between ClimbMove and ClimbMantle itself, same pattern as before.
///
/// CLIP SLOTS
///   The table below states exactly which FBX fills each slot. Slots marked TEMP still use
///   the OLD wall set because the animator has not delivered that clip yet (and the new
///   down clip currently ships with a leftover box). When the final clips land, this table
///   is updated and the tool is simply run again, it rebuilds the layer from scratch every
///   time, so re running is always safe.
///
/// SAFETY
///   Before touching anything the tool copies the controller file to the Backups folder
///   next to the project (outside Assets), with a timestamp. If anything looks wrong in
///   the Animator window afterwards, that copy is the restore point.
///
/// USAGE
///   Tools, YORU, Build Climb Blend Tree. Watch the Console for the summary. Run it again
///   any time, for example after the animator delivers updated clips.
///
/// This file must live inside an Editor folder, or stay wrapped in UNITY_EDITOR as it is.
/// </summary>
public static class ClimbBlendTreeBuilder
{
    private const string ControllerPath = "Assets/Animations_Yoru/Yoru_Animato_Controller.controller";
    private const string ClimbLayerName = "ClimbLayer";
    private const string MoveStateName = "ClimbMove";
    private const string MantleStateName = "ClimbMantle";
    private const string ParamX = "ClimbX";
    private const string ParamY = "ClimbY";

    private const string NewSet = "Assets/Animations_Yoru/climbSetNew/";
    private const string OldSet = "Assets/Animations_Yoru/Yoru climb set/";

    // The clip slot table. Position is (x, y) in the blend space. TEMP slots wait on the
    // animator's next delivery and are swapped here when the final files land.
    private struct Slot
    {
        public string label;
        public string fbxPath;
        public Vector2 position;
        public bool temp;

        public Slot(string label, string fbxPath, float x, float y, bool temp)
        {
            this.label = label;
            this.fbxPath = fbxPath;
            this.position = new Vector2(x, y);
            this.temp = temp;
        }
    }

    private static readonly Slot[] MoveSlots =
    {
        new Slot("Idle",  OldSet + "ClimbIdle.fbx",              0f,  0f, true),
        new Slot("Up",    NewSet + "New Climbing.fbx",           0f,  1f, false),
        new Slot("Down",  OldSet + "ClimbDown.fbx",              0f, -1f, true),
        new Slot("SideL", OldSet + "ClimbSidewayL.fbx",         -1f,  0f, true),
        new Slot("SideR", NewSet + "New Climbing Sideways.fbx",  1f,  0f, false),
        new Slot("RunL",  OldSet + "ClimbWallRunL.fbx",         -2f,  0f, true),
        new Slot("RunR",  OldSet + "ClimbWallRunR.fbx",          2f,  0f, true),
    };

    private const string MantleFbxPath = NewSet + "New Climbing On Top.fbx";

    [MenuItem("Tools/YORU/Build Climb Blend Tree")]
    private static void Build()
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null)
        {
            Debug.LogError("[ClimbBlendTreeBuilder] Controller not found at " + ControllerPath +
                ". Fix the path constant and run again.");
            return;
        }

        // Load every clip first. If anything is missing, stop BEFORE touching the layer,
        // so a bad run can never leave the layer half built.
        AnimationClip[] moveClips = new AnimationClip[MoveSlots.Length];
        bool allFound = true;
        for (int i = 0; i < MoveSlots.Length; i++)
        {
            moveClips[i] = LoadClip(MoveSlots[i].fbxPath);
            if (moveClips[i] == null)
            {
                Debug.LogError("[ClimbBlendTreeBuilder] No animation clip found in " + MoveSlots[i].fbxPath +
                    " (slot " + MoveSlots[i].label + ").");
                allFound = false;
            }
        }
        AnimationClip mantleClip = LoadClip(MantleFbxPath);
        if (mantleClip == null)
        {
            Debug.LogError("[ClimbBlendTreeBuilder] No animation clip found in " + MantleFbxPath + " (slot Mantle).");
            allFound = false;
        }
        if (!allFound)
        {
            Debug.LogError("[ClimbBlendTreeBuilder] Stopped, nothing was changed. Fix the missing clips above and run again.");
            return;
        }

        int layerIndex = FindLayerIndex(controller, ClimbLayerName);
        if (layerIndex < 0)
        {
            Debug.LogError("[ClimbBlendTreeBuilder] No layer named '" + ClimbLayerName + "' on " + controller.name + ".");
            return;
        }

        string backupPath = BackupControllerFile();

        EnsureFloatParameter(controller, ParamX);
        EnsureFloatParameter(controller, ParamY);

        AnimatorStateMachine sm = controller.layers[layerIndex].stateMachine;

        // Wipe the layer's states (the old eight, or a previous run of this tool), then
        // destroy any orphaned blend tree sub assets a previous run left inside the
        // controller file, so the asset never accumulates junk.
        ChildAnimatorState[] existing = sm.states;
        for (int i = 0; i < existing.Length; i++)
            sm.RemoveState(existing[i].state);
        Object[] subAssets = AssetDatabase.LoadAllAssetsAtPath(ControllerPath);
        for (int i = 0; i < subAssets.Length; i++)
        {
            BlendTree stale = subAssets[i] as BlendTree;
            if (stale != null && stale.name == MoveStateName)
                Object.DestroyImmediate(stale, true);
        }

        // Build the 2D directional blend tree. Freeform directional is the variant that
        // allows two clips on the same direction at different strengths, which is exactly
        // sideways walk at 1 and wall run at 2.
        BlendTree tree = new BlendTree
        {
            name = MoveStateName,
            blendType = BlendTreeType.FreeformDirectional2D,
            blendParameter = ParamX,
            blendParameterY = ParamY,
            hideFlags = HideFlags.HideInHierarchy
        };
        AssetDatabase.AddObjectToAsset(tree, controller);
        for (int i = 0; i < MoveSlots.Length; i++)
            tree.AddChild(moveClips[i], MoveSlots[i].position);

        AnimatorState moveState = sm.AddState(MoveStateName, new Vector3(240f, 60f, 0f));
        moveState.motion = tree;
        moveState.writeDefaultValues = true;

        AnimatorState mantleState = sm.AddState(MantleStateName, new Vector3(240f, 160f, 0f));
        mantleState.motion = mantleClip;
        mantleState.writeDefaultValues = true;

        sm.defaultState = moveState;

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();

        // Summary, one line per slot, so what got wired is never a guess.
        Debug.Log("[ClimbBlendTreeBuilder] ClimbLayer rebuilt. States: " + MoveStateName + " (blend tree), " +
            MantleStateName + ". Parameters: " + ParamX + ", " + ParamY + ". Backup: " + backupPath);
        for (int i = 0; i < MoveSlots.Length; i++)
        {
            Debug.Log("[ClimbBlendTreeBuilder]   " + MoveSlots[i].label + " (" + MoveSlots[i].position.x + "," +
                MoveSlots[i].position.y + ") = " + moveClips[i].name + " from " + MoveSlots[i].fbxPath +
                (MoveSlots[i].temp ? "   TEMP, waiting on the animator's final clip" : ""));
        }
        Debug.Log("[ClimbBlendTreeBuilder]   Mantle = " + mantleClip.name + " from " + MantleFbxPath);
    }

    /// <summary>First real animation clip inside an FBX, skipping Unity's preview clips.</summary>
    private static AnimationClip LoadClip(string fbxPath)
    {
        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(fbxPath);
        for (int i = 0; i < assets.Length; i++)
        {
            AnimationClip clip = assets[i] as AnimationClip;
            if (clip == null) continue;
            if (clip.name.StartsWith("__preview__")) continue;
            return clip;
        }
        return null;
    }

    private static int FindLayerIndex(AnimatorController controller, string layerName)
    {
        for (int i = 0; i < controller.layers.Length; i++)
            if (controller.layers[i].name == layerName) return i;
        return -1;
    }

    private static void EnsureFloatParameter(AnimatorController controller, string name)
    {
        AnimatorControllerParameter[] parameters = controller.parameters;
        for (int i = 0; i < parameters.Length; i++)
            if (parameters[i].name == name) return;
        controller.AddParameter(name, AnimatorControllerParameterType.Float);
    }

    /// <summary>
    /// Copies the controller file to (project)/Backups with a timestamp, so any run of this
    /// tool can be undone by copying the file back. Returns the backup path for the log.
    /// </summary>
    private static string BackupControllerFile()
    {
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string backupDir = Path.Combine(projectRoot, "Backups");
        Directory.CreateDirectory(backupDir);
        string stamp = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string target = Path.Combine(backupDir, "Yoru_Animato_Controller_" + stamp + ".controller.bak");
        File.Copy(Path.Combine(projectRoot, ControllerPath), target, true);
        return target;
    }
}
#endif
