using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// ROUND 87 (23 Sep 2026, step 5): one click turns the cave's PlayerYoru_1.1 into an instance of the
/// new prefab Assets/Org_Prefabs/yoru_f/Yoru/Yoru_Original.prefab without changing anything on her.
///
/// 1. Checks: not in Play, no Prefab Mode, the cave scene is the open scene, exactly one
///    PlayerYoru_1.1 at the top of the Hierarchy that is not part of a prefab, Player Health >
///    Testing > Cannot Die off (this tool saves the scene), and no Yoru_Original.prefab yet.
///    Any fail: it stops before changing anything and says why.
/// 2. Records every serialized value on her and on all her children, and every link from the rest
///    of the scene into her.
/// 3. Makes the prefab and connects her to it, the same as dragging her into the Project window.
/// 4. Records again. A prefab file cannot hold links to things that only live in the scene (the
///    scene camera on Yoru Light Rig, XFur's helper materials and texture saved inside the scene
///    file). If Unity dropped any of them on her, the same object is put back (an override on the
///    cave copy).
/// 5. Saves the scene only when both records match value for value and the links into her are the
///    same, writes Temp/YoruOriginal_step5.txt and deletes itself. Otherwise it saves nothing,
///    keeps itself and writes the report.
/// </summary>
public static class YoruOriginalOnce
{
    private const string MenuPath = "Tools/YORU/Make Yoru_Original (step 5, runs once)";
    private const string ScenePath = "Assets/Scenes 1/CaveScene_Oni_Boss1.unity";
    private const string RootName = "PlayerYoru_1.1";
    private const string PrefabFolder = "Assets/Org_Prefabs/yoru_f/Yoru";
    private const string PrefabPath = "Assets/Org_Prefabs/yoru_f/Yoru/Yoru_Original.prefab";
    private const string SelfPath = "Assets/Scripts/Editor/YoruOriginalOnce.cs";
    private const string ReportPath = "Temp/YoruOriginal_step5.txt";
    private const char Sep = '\t';
    private const int ListLimit = 400;

    private sealed class Snapshot
    {
        public readonly Dictionary<string, string> Values = new Dictionary<string, string>();
        public readonly Dictionary<int, Object> Owners = new Dictionary<int, Object>();
        public readonly Dictionary<string, Object> SceneOnly = new Dictionary<string, Object>();
        public int Objects;
        public int Components;
    }

    // Shared by every record of one run: the same object always gets the same number, so a new
    // object can never pass for the old one, and the owner of a value is kept as a short number.
    private static readonly Dictionary<Object, int> Identity = new Dictionary<Object, int>();
    private static readonly Dictionary<string, int> OwnerIds = new Dictionary<string, int>();
    private static readonly List<string> OwnerNames = new List<string>();

    [MenuItem(MenuPath)]
    private static void Run()
    {
        Identity.Clear();
        OwnerIds.Clear();
        OwnerNames.Clear();

        var report = new StringBuilder();
        report.AppendLine("Step 5: " + RootName + " becomes an instance of " + PrefabPath);
        report.AppendLine("Run " + System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) + ", Unity " + Application.unityVersion);

        Scene scene;
        GameObject root;
        string problem = PreFlight(out scene, out root);
        if (problem != null)
        {
            Finish(report, "STOPPED, nothing was changed: " + problem, 1);
            return;
        }

        try
        {
            Convert(report, scene, root);
        }
        catch (System.Exception e)
        {
            Debug.LogException(e);
            Finish(report, "FAILED with an error (" + e.Message + "). Do not save the scene: File > Open Scene > " + ScenePath + " > Don't Save.", 2);
        }
    }

    // ---- 2 to 5. record, make the prefab, record again, compare, save ----

    private static void Convert(StringBuilder report, Scene scene, GameObject root)
    {
        report.AppendLine("Scene had unsaved changes before the run: " + (scene.isDirty ? "YES (the save at the end includes them)" : "no"));

        Snapshot before = Take(root);
        List<string> linksBefore = LinksIntoHer(scene, root);
        report.AppendLine("Before: " + Count(before.Objects) + " objects, " + Count(before.Components) + " components, " + Count(before.Values.Count) + " values.");
        report.AppendLine("Links from the rest of the scene into her (" + Count(linksBefore.Count) + "):");
        AppendList(report, linksBefore);
        report.AppendLine("Her links to scene-only objects (" + Count(before.SceneOnly.Count) + "):");
        AppendList(report, Kinds(before));

        bool made;
        GameObject asset = PrefabUtility.SaveAsPrefabAssetAndConnect(root, PrefabPath, InteractionMode.AutomatedAction, out made);
        if (!made || asset == null)
        {
            Finish(report, "FAILED: Unity did not make the prefab. Do not save the scene: File > Open Scene > " + ScenePath + " > Don't Save.", 2);
            return;
        }

        report.AppendLine("Prefab made: " + PrefabPath + " (guid " + AssetDatabase.AssetPathToGUID(PrefabPath) + ", root name in the file '" + asset.name + "').");
        if (root == null)
        {
            foreach (GameObject go in scene.GetRootGameObjects())
            {
                if (PrefabUtility.GetCorrespondingObjectFromSource(go) == asset)
                {
                    root = go;
                    break;
                }
            }

            report.AppendLine("Unity replaced her object while connecting; the new one found: " + (root != null ? "yes" : "NO"));
            if (root == null)
            {
                Finish(report, "FAILED: she is not in the scene after the prefab was made. Do not save the scene: File > Open Scene > " + ScenePath + " > Don't Save.", 2);
                return;
            }
        }

        if (root.name != RootName)
        {
            report.AppendLine("Unity renamed her to '" + root.name + "'; set back to " + RootName + ".");
            root.name = RootName;
        }

        bool isInstance = PrefabUtility.IsPartOfPrefabInstance(root);
        report.AppendLine("She is an instance of the prefab now: " + (isInstance ? "yes" : "NO"));

        Snapshot after = Take(root);
        List<string> restored = Restore(before, after);
        report.AppendLine("Scene-only links Unity dropped on her and the tool put back (" + Count(restored.Count) + "):");
        AppendList(report, restored);
        if (restored.Count > 0)
        {
            after = Take(root);
        }

        List<string> differences = Compare(before, after);
        List<string> linksAfter = LinksIntoHer(scene, root);
        bool linksSame = string.Join("\n", linksBefore.ToArray()) == string.Join("\n", linksAfter.ToArray());
        report.AppendLine("After: " + Count(after.Objects) + " objects, " + Count(after.Components) + " components, " + Count(after.Values.Count) + " values.");
        report.AppendLine("Differences before against after (" + Count(differences.Count) + "):");
        AppendList(report, differences);
        report.AppendLine("Links into her after (" + Count(linksAfter.Count) + ", " + (linksSame ? "same as before" : "NOT the same as before") + "):");
        AppendList(report, linksAfter);

        List<string> overrides = Overrides(root);
        report.AppendLine("Overrides the cave copy keeps (" + Count(overrides.Count) + "):");
        AppendList(report, overrides);

        if (!isInstance || differences.Count > 0 || !linksSame)
        {
            Finish(report, "NOT SAVED: the records do not match. Do not save the scene: File > Open Scene > " + ScenePath + " > Don't Save. The prefab file stays for Claude to look at.", 2);
            return;
        }

        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene))
        {
            Finish(report, "FAILED: the records match but Unity did not save the scene.", 2);
            return;
        }

        Finish(report, "DONE: 0 differences on " + Count(after.Values.Count) + " values, " + Count(linksAfter.Count) + " links into her unchanged, scene saved. This tool removed itself.", 0);
    }

    // ---- 1. checks, nothing changes here ----

    private static string PreFlight(out Scene scene, out GameObject root)
    {
        scene = SceneManager.GetActiveScene();
        root = null;

        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return "stop Play first.";
        }

        if (PrefabStageUtility.GetCurrentPrefabStage() != null)
        {
            return "close Prefab Mode first.";
        }

        if (scene.path != ScenePath)
        {
            return "open " + ScenePath + " first (the open scene is '" + scene.path + "').";
        }

        if (!AssetDatabase.IsValidFolder(PrefabFolder))
        {
            return "the folder " + PrefabFolder + " is missing.";
        }

        if (File.Exists(PrefabPath) || AssetDatabase.LoadAssetAtPath<Object>(PrefabPath) != null)
        {
            return PrefabPath + " already exists.";
        }

        var found = new List<GameObject>();
        foreach (GameObject go in scene.GetRootGameObjects())
        {
            if (go.name == RootName)
            {
                found.Add(go);
            }
        }

        if (found.Count != 1)
        {
            return "expected one " + RootName + " at the top of the Hierarchy, found " + Count(found.Count) + ".";
        }

        root = found[0];
        if (PrefabUtility.IsPartOfAnyPrefab(root))
        {
            return RootName + " is already part of a prefab.";
        }

        PlayerHealth health = root.GetComponentInChildren<PlayerHealth>(true);
        if (health != null)
        {
            SerializedProperty cannotDie = new SerializedObject(health).FindProperty("cannotDie");
            if (cannotDie != null && cannotDie.boolValue)
            {
                return "untick " + RootName + " > Player Health > Testing > Cannot Die, then click again (this tool saves the scene).";
            }
        }

        return null;
    }

    // ---- 2 and 4. the record of every serialized value on her ----

    private static Snapshot Take(GameObject root)
    {
        var snap = new Snapshot();
        Transform top = root.transform;
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            string goKey = PathOf(t, top);
            snap.Objects++;
            Add(snap, goKey + " | GameObject", t.gameObject, top);

            var seen = new Dictionary<System.Type, int>();
            Component[] components = t.GetComponents<Component>();
            for (int i = 0; i < components.Length; i++)
            {
                Component c = components[i];
                if (c == null)
                {
                    snap.Values[OwnerId(goKey + " | missing script #" + Count(i)).ToString(CultureInfo.InvariantCulture) + Sep] = "missing";
                    continue;
                }

                System.Type type = c.GetType();
                int n;
                seen.TryGetValue(type, out n);
                seen[type] = n + 1;
                snap.Components++;
                Add(snap, goKey + " | " + type.FullName + "#" + Count(n), c, top);
            }
        }

        return snap;
    }

    private static void Add(Snapshot snap, string ownerKey, Object owner, Transform top)
    {
        int id = OwnerId(ownerKey);
        snap.Owners[id] = owner;
        string prefix = id.ToString(CultureInfo.InvariantCulture) + Sep;

        var so = new SerializedObject(owner);
        SerializedProperty p = so.GetIterator();
        bool enter = true;
        while (p.Next(enter))
        {
            enter = true;
            string path = p.propertyPath;
            if (path == "m_PrefabInstance" || path == "m_CorrespondingSourceObject" || path == "m_PrefabAsset")
            {
                enter = false;
                continue;
            }

            string key = prefix + path;
            if (p.propertyType == SerializedPropertyType.ManagedReference)
            {
                snap.Values[key] = "managed " + p.managedReferenceFullTypename;
                continue;
            }

            string value = Leaf(p, top, snap, key);
            if (value == null)
            {
                continue;
            }

            snap.Values[key] = value;
            enter = false;
        }
    }

    // A value that is stored as it is. Anything else (vectors, arrays, structs) returns null and its
    // children are recorded one by one.
    private static string Leaf(SerializedProperty p, Transform top, Snapshot snap, string key)
    {
        switch (p.propertyType)
        {
            case SerializedPropertyType.Integer:
                return p.longValue.ToString(CultureInfo.InvariantCulture);
            case SerializedPropertyType.Boolean:
                return p.boolValue ? "true" : "false";
            case SerializedPropertyType.Float:
                return p.doubleValue.ToString("R", CultureInfo.InvariantCulture);
            case SerializedPropertyType.String:
                return "\"" + p.stringValue + "\"";
            case SerializedPropertyType.Color:
                return ColorText(p.colorValue);
            case SerializedPropertyType.LayerMask:
            case SerializedPropertyType.Enum:
            case SerializedPropertyType.ArraySize:
            case SerializedPropertyType.Character:
                return p.intValue.ToString(CultureInfo.InvariantCulture);
            case SerializedPropertyType.FixedBufferSize:
                return p.fixedBufferSize.ToString(CultureInfo.InvariantCulture);
            case SerializedPropertyType.AnimationCurve:
                return CurveText(p.animationCurveValue);
            case SerializedPropertyType.ObjectReference:
                return RefText(p.objectReferenceValue, top, snap, key);
            case SerializedPropertyType.ExposedReference:
                return RefText(p.exposedReferenceValue, top, null, key);
            default:
                return null;
        }
    }

    // How a link is written down: an asset by its path, a scene object by where it sits in the
    // Hierarchy, and a scene-only helper (material, texture) by its own identity in this run.
    private static string RefText(Object o, Transform top, Snapshot snap, string key)
    {
        if (o == null)
        {
            return "None";
        }

        if (EditorUtility.IsPersistent(o))
        {
            return "asset " + AssetDatabase.GetAssetPath(o) + " : " + o.GetType().Name + " '" + o.name + "'";
        }

        GameObject go = o as GameObject;
        Component comp = o as Component;
        Transform t = null;
        if (go != null)
        {
            t = go.transform;
        }
        else if (comp != null)
        {
            t = comp.transform;
        }

        bool mine = t != null && t.IsChildOf(top);
        if (!mine && snap != null)
        {
            snap.SceneOnly[key] = o;
        }

        if (t == null)
        {
            return "scene object " + o.GetType().Name + " '" + o.name + "' #" + Count(IdOf(o));
        }

        string where = mine ? "her " + PathOf(t, top) : "scene " + PathOf(t, null);
        return where + " : " + o.GetType().Name + (comp != null ? "#" + Count(IndexOf(comp)) : string.Empty);
    }

    // ---- 3 and 4. putting back what the prefab file could not hold ----

    private static List<string> Restore(Snapshot before, Snapshot after)
    {
        var restored = new List<string>();
        foreach (KeyValuePair<string, Object> link in before.SceneOnly)
        {
            string was = before.Values[link.Key];
            string now;
            if (after.Values.TryGetValue(link.Key, out now) && now == was)
            {
                continue;
            }

            int cut = link.Key.IndexOf(Sep);
            int id = int.Parse(link.Key.Substring(0, cut), CultureInfo.InvariantCulture);
            Object owner;
            if (!after.Owners.TryGetValue(id, out owner) || owner == null)
            {
                continue;
            }

            var so = new SerializedObject(owner);
            SerializedProperty prop = so.FindProperty(link.Key.Substring(cut + 1));
            if (prop == null || prop.propertyType != SerializedPropertyType.ObjectReference)
            {
                continue;
            }

            prop.objectReferenceValue = link.Value;
            so.ApplyModifiedPropertiesWithoutUndo();
            restored.Add(Show(link.Key) + " : " + (now ?? "(gone)") + " -> " + was);
        }

        return restored;
    }

    private static List<string> Compare(Snapshot a, Snapshot b)
    {
        var diffs = new List<string>();
        foreach (KeyValuePair<string, string> kv in a.Values)
        {
            string v;
            if (!b.Values.TryGetValue(kv.Key, out v))
            {
                diffs.Add(Show(kv.Key) + " : " + kv.Value + " -> (gone)");
            }
            else if (v != kv.Value)
            {
                diffs.Add(Show(kv.Key) + " : " + kv.Value + " -> " + v);
            }
        }

        foreach (KeyValuePair<string, string> kv in b.Values)
        {
            if (!a.Values.ContainsKey(kv.Key))
            {
                diffs.Add(Show(kv.Key) + " : (new) -> " + kv.Value);
            }
        }

        diffs.Sort(System.StringComparer.Ordinal);
        return diffs;
    }

    // Every link from the rest of the scene into her (today: the two cameras).
    private static List<string> LinksIntoHer(Scene scene, GameObject root)
    {
        var links = new List<string>();
        Transform top = root.transform;
        MonoBehaviour[] all = Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (MonoBehaviour mb in all)
        {
            if (mb == null || mb.gameObject.scene != scene || mb.transform.IsChildOf(top))
            {
                continue;
            }

            var so = new SerializedObject(mb);
            SerializedProperty p = so.GetIterator();
            bool enter = true;
            while (p.Next(enter))
            {
                enter = p.propertyType != SerializedPropertyType.String;
                if (p.propertyType != SerializedPropertyType.ObjectReference)
                {
                    continue;
                }

                Object o = p.objectReferenceValue;
                GameObject go = o as GameObject;
                Component comp = o as Component;
                Transform t = null;
                if (go != null)
                {
                    t = go.transform;
                }
                else if (comp != null)
                {
                    t = comp.transform;
                }

                if (t == null || !t.IsChildOf(top))
                {
                    continue;
                }

                links.Add(PathOf(mb.transform, null) + " > " + mb.GetType().Name + " > " + p.propertyPath + " -> her " + PathOf(t, top) + " : " + o.GetType().Name);
            }
        }

        links.Sort(System.StringComparer.Ordinal);
        return links;
    }

    private static List<string> Overrides(GameObject root)
    {
        var list = new List<string>();
        PropertyModification[] mods = PrefabUtility.GetPropertyModifications(root);
        if (mods == null)
        {
            return list;
        }

        foreach (PropertyModification m in mods)
        {
            string target = m.target != null ? m.target.GetType().Name + " '" + m.target.name + "'" : "(no target)";
            string value = m.objectReference != null ? RefText(m.objectReference, root.transform, null, string.Empty) : "\"" + m.value + "\"";
            list.Add(target + " . " + m.propertyPath + " = " + value);
        }

        list.Sort(System.StringComparer.Ordinal);
        return list;
    }

    private static List<string> Kinds(Snapshot snap)
    {
        var counts = new SortedDictionary<string, int>(System.StringComparer.Ordinal);
        foreach (KeyValuePair<string, Object> link in snap.SceneOnly)
        {
            string path = link.Key.Substring(link.Key.IndexOf(Sep) + 1);
            string kind = link.Value.GetType().Name + " '" + link.Value.name + "' via " + path;
            int n;
            counts.TryGetValue(kind, out n);
            counts[kind] = n + 1;
        }

        var list = new List<string>();
        foreach (KeyValuePair<string, int> kv in counts)
        {
            list.Add(Count(kv.Value) + " x " + kv.Key);
        }

        return list;
    }

    // ---- 5. the report ----

    private static void Finish(StringBuilder report, string verdict, int level)
    {
        report.AppendLine(verdict);
        try
        {
            Directory.CreateDirectory("Temp");
            File.WriteAllText(ReportPath, report.ToString());
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[YoruOriginal] could not write " + ReportPath + ": " + e.Message);
        }

        string line = "[YoruOriginal] " + verdict + " Report: " + ReportPath;
        if (level == 0)
        {
            Debug.Log(line);
            AssetDatabase.DeleteAsset(SelfPath);
        }
        else if (level == 1)
        {
            Debug.LogWarning(line);
        }
        else
        {
            Debug.LogError(line);
        }
    }

    // ---- small helpers ----

    private static int OwnerId(string ownerKey)
    {
        int id;
        if (!OwnerIds.TryGetValue(ownerKey, out id))
        {
            id = OwnerNames.Count;
            OwnerIds[ownerKey] = id;
            OwnerNames.Add(ownerKey);
        }

        return id;
    }

    private static int IdOf(Object o)
    {
        int id;
        if (!Identity.TryGetValue(o, out id))
        {
            id = Identity.Count;
            Identity[o] = id;
        }

        return id;
    }

    private static string Show(string key)
    {
        int cut = key.IndexOf(Sep);
        int id = int.Parse(key.Substring(0, cut), CultureInfo.InvariantCulture);
        return OwnerNames[id] + " | " + key.Substring(cut + 1);
    }

    private static string PathOf(Transform t, Transform top)
    {
        var parts = new List<string>();
        for (Transform x = t; x != null; x = x.parent)
        {
            parts.Add(x.name + "[" + Count(x.GetSiblingIndex()) + "]");
            if (x == top)
            {
                break;
            }
        }

        parts.Reverse();
        return string.Join("/", parts.ToArray());
    }

    private static int IndexOf(Component c)
    {
        int n = 0;
        foreach (Component other in c.GetComponents<Component>())
        {
            if (other == c)
            {
                return n;
            }

            if (other != null && other.GetType() == c.GetType())
            {
                n++;
            }
        }

        return n;
    }

    private static void AppendList(StringBuilder report, List<string> lines)
    {
        for (int i = 0; i < lines.Count; i++)
        {
            if (i == ListLimit)
            {
                report.AppendLine("  ... and " + Count(lines.Count - ListLimit) + " more");
                break;
            }

            report.AppendLine("  " + lines[i]);
        }
    }

    private static string Count(int n)
    {
        return n.ToString(CultureInfo.InvariantCulture);
    }

    private static string F(float v)
    {
        return v.ToString("R", CultureInfo.InvariantCulture);
    }

    private static string ColorText(Color c)
    {
        return F(c.r) + " " + F(c.g) + " " + F(c.b) + " " + F(c.a);
    }

    private static string CurveText(AnimationCurve curve)
    {
        if (curve == null)
        {
            return "no curve";
        }

        var sb = new StringBuilder();
        sb.Append(Count((int)curve.preWrapMode)).Append('/').Append(Count((int)curve.postWrapMode));
        foreach (Keyframe k in curve.keys)
        {
            sb.Append(' ').Append(F(k.time)).Append(',').Append(F(k.value));
            sb.Append(',').Append(F(k.inTangent)).Append(',').Append(F(k.outTangent));
            sb.Append(',').Append(F(k.inWeight)).Append(',').Append(F(k.outWeight));
            sb.Append(',').Append(Count((int)k.weightedMode));
        }

        return sb.ToString();
    }
}
