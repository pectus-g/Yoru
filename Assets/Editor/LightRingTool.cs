#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// ROUND 72 - places a ring of props (braziers, lanterns, anything) evenly around a point
/// and drops each one onto the ground for you.
///
/// Open it from the top menu: Yoru Tools > 4 - Light Ring Tool.
///
/// IMPORTANT, and the reason round 72 exists: the circles and the A1, A2, A3 marks that
/// appear in the Scene view are GUIDES ONLY. They are drawn by this window, they are not
/// objects, and they never show up when you press Play. Nothing is real until you press
/// the Place button. The box at the top of the window now says which state you are in.
///
/// It never changes the prefab itself. It only creates instances in the open scene, all
/// parented under one group object you can delete in one go. Undo works on the whole thing.
///
/// EDITOR ONLY. Nothing here ships in a build.
/// </summary>
public class LightRingTool : EditorWindow
{
    #region Settings

    private GameObject prefab;
    private Transform centreObject;
    private Vector3 centrePoint = new Vector3(478f, 0f, 430f);
    private bool useCentreObject = false;

    private float radius = 20f;
    private int count = 6;
    private float startAngle = 30f;
    private float yOffset = 0f;
    private bool faceCentre = true;
    private bool snapToGround = true;

    private float keepClearRadius = 14f;
    private string groupName = "LightRing";
    private string labelPrefix = "A";
    private bool drawPreview = true;

    private Vector2 scroll;

    private const string PrefKey = "Yoru.LightRingTool.";

    #endregion

    #region Window

    [MenuItem("Yoru Tools/4 - Light Ring Tool", priority = 4)]
    public static void Open()
    {
        LightRingTool w = GetWindow<LightRingTool>(false, "Light Ring", true);
        w.minSize = new Vector2(360f, 520f);
        w.Show();
    }

    private void OnEnable()
    {
        LoadPrefs();
        SceneView.duringSceneGui += OnSceneGUI;
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
        SavePrefs();
    }

    private void LoadPrefs()
    {
        string path = EditorPrefs.GetString(PrefKey + "prefab", "");
        if (!string.IsNullOrEmpty(path)) prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

        centrePoint = new Vector3(
            EditorPrefs.GetFloat(PrefKey + "cx", centrePoint.x),
            EditorPrefs.GetFloat(PrefKey + "cy", centrePoint.y),
            EditorPrefs.GetFloat(PrefKey + "cz", centrePoint.z));

        useCentreObject = EditorPrefs.GetBool(PrefKey + "useObj", useCentreObject);
        radius = EditorPrefs.GetFloat(PrefKey + "radius", radius);
        count = EditorPrefs.GetInt(PrefKey + "count", count);
        startAngle = EditorPrefs.GetFloat(PrefKey + "start", startAngle);
        yOffset = EditorPrefs.GetFloat(PrefKey + "yoff", yOffset);
        faceCentre = EditorPrefs.GetBool(PrefKey + "face", faceCentre);
        snapToGround = EditorPrefs.GetBool(PrefKey + "snap", snapToGround);
        keepClearRadius = EditorPrefs.GetFloat(PrefKey + "clear", keepClearRadius);
        groupName = EditorPrefs.GetString(PrefKey + "group", groupName);
        labelPrefix = EditorPrefs.GetString(PrefKey + "prefix", labelPrefix);
        drawPreview = EditorPrefs.GetBool(PrefKey + "preview", drawPreview);
    }

    private void SavePrefs()
    {
        EditorPrefs.SetString(PrefKey + "prefab", prefab != null ? AssetDatabase.GetAssetPath(prefab) : "");
        EditorPrefs.SetFloat(PrefKey + "cx", centrePoint.x);
        EditorPrefs.SetFloat(PrefKey + "cy", centrePoint.y);
        EditorPrefs.SetFloat(PrefKey + "cz", centrePoint.z);
        EditorPrefs.SetBool(PrefKey + "useObj", useCentreObject);
        EditorPrefs.SetFloat(PrefKey + "radius", radius);
        EditorPrefs.SetInt(PrefKey + "count", count);
        EditorPrefs.SetFloat(PrefKey + "start", startAngle);
        EditorPrefs.SetFloat(PrefKey + "yoff", yOffset);
        EditorPrefs.SetBool(PrefKey + "face", faceCentre);
        EditorPrefs.SetBool(PrefKey + "snap", snapToGround);
        EditorPrefs.SetFloat(PrefKey + "clear", keepClearRadius);
        EditorPrefs.SetString(PrefKey + "group", groupName);
        EditorPrefs.SetString(PrefKey + "prefix", labelPrefix);
        EditorPrefs.SetBool(PrefKey + "preview", drawPreview);
    }

    #endregion

    #region UI

    private void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);

        DrawStatusBox();
        EditorGUILayout.Space(8f);

        EditorGUILayout.LabelField("What to place", EditorStyles.boldLabel);
        prefab = (GameObject)EditorGUILayout.ObjectField("Prefab", prefab, typeof(GameObject), false);
        if (prefab == null)
            EditorGUILayout.HelpBox("No prefab yet. Drag your Brazier prefab in here, from Assets > Org_Prefabs > Lights. Until you do, the Place button stays greyed out.", MessageType.Warning);

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Where the ring goes", EditorStyles.boldLabel);
        useCentreObject = EditorGUILayout.Toggle("Use an object as centre", useCentreObject);
        if (useCentreObject)
        {
            centreObject = (Transform)EditorGUILayout.ObjectField("Centre object", centreObject, typeof(Transform), true);
            if (centreObject == null)
                EditorGUILayout.HelpBox("Drag an object from the Hierarchy in here, or untick the box above and type the position instead.", MessageType.Warning);
        }
        else
        {
            centrePoint = EditorGUILayout.Vector3Field("Centre position", centrePoint);
        }

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("The ring", EditorStyles.boldLabel);
        radius = EditorGUILayout.Slider("Radius (metres)", radius, 3f, 120f);
        count = EditorGUILayout.IntSlider("How many", count, 1, 32);
        startAngle = EditorGUILayout.Slider("Start angle", startAngle, 0f, 359f);
        EditorGUILayout.LabelField(" ", "0 = due north. With 6 at start 30 the north side is left open.", EditorStyles.miniLabel);
        yOffset = EditorGUILayout.FloatField("Height offset", yOffset);
        faceCentre = EditorGUILayout.Toggle("Turn to face the middle", faceCentre);
        snapToGround = EditorGUILayout.Toggle("Drop onto the ground", snapToGround);

        DrawReachBox();

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Naming", EditorStyles.boldLabel);
        groupName = EditorGUILayout.TextField("Group object", groupName);
        EditorGUILayout.LabelField(" ", "Change this name to keep a second ring as well as the first.", EditorStyles.miniLabel);
        labelPrefix = EditorGUILayout.TextField("Name prefix", labelPrefix);

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Scene view guides", EditorStyles.boldLabel);
        drawPreview = EditorGUILayout.Toggle("Draw the guide circles", drawPreview);
        keepClearRadius = EditorGUILayout.Slider("Keep clear radius", keepClearRadius, 0f, 60f);
        EditorGUILayout.LabelField(" ", "Red circle. The cinematic camera needs this empty.", EditorStyles.miniLabel);

        EditorGUILayout.Space(14f);

        bool canPlace = prefab != null && (!useCentreObject || centreObject != null);
        EditorGUI.BeginDisabledGroup(!canPlace);
        if (GUILayout.Button("PLACE THE RING  (this is the button that makes real objects)", GUILayout.Height(38f)))
        {
            PlaceRing();
            SavePrefs();
        }
        EditorGUI.EndDisabledGroup();

        if (!canPlace)
            EditorGUILayout.LabelField(" ", "Greyed out because something above is still empty.", EditorStyles.miniLabel);

        if (GUILayout.Button("Clear the ring")) ClearRing();

        EditorGUILayout.Space(10f);
        DrawBudgetBox();

        EditorGUILayout.EndScrollView();
    }

    /// <summary>The box that says whether anything real exists yet. This is the whole point of round 72.</summary>
    private void DrawStatusBox()
    {
        GameObject group = GameObject.Find(groupName);
        if (group == null)
        {
            EditorGUILayout.HelpBox(
                "NOTHING IS PLACED YET.\n\n"
              + "The circles and the " + labelPrefix + "1, " + labelPrefix + "2, " + labelPrefix + "3 marks in the Scene view are GUIDES ONLY. "
              + "They are drawn by this window. They are not objects, they are not in your scene, and they will NOT appear when you press Play.\n\n"
              + "Press the big Place button near the bottom to actually create them.",
                MessageType.Warning);
        }
        else
        {
            int n = group.transform.childCount;
            EditorGUILayout.HelpBox(
                "PLACED. \"" + groupName + "\" is in your scene with " + n + " object" + (n == 1 ? "" : "s") + " under it.\n\n"
              + "Press Place again to rebuild it with the current settings, or Clear to remove it.",
                MessageType.Info);
        }
    }

    /// <summary>Plain English: will these fires actually light the middle, or are they just for looks.</summary>
    private void DrawReachBox()
    {
        if (prefab == null) return;

        Light l = prefab.GetComponentInChildren<Light>(true);
        if (l == null)
        {
            EditorGUILayout.HelpBox(
                "This prefab has no light inside it. It will look like fire but it will not light anything around it.",
                MessageType.Warning);
            return;
        }

        string reach = l.range.ToString("0");
        string ring = radius.ToString("0");

        if (radius <= l.range * 0.75f)
        {
            EditorGUILayout.HelpBox(
                "Each fire lights about " + reach + "m around itself, and your circle is " + ring + "m out from the middle.\n\n"
              + "The fires WILL light the middle. Good for fighting in.",
                MessageType.Info);
        }
        else if (radius <= l.range * 1.3f)
        {
            EditorGUILayout.HelpBox(
                "Each fire lights about " + reach + "m around itself, and your circle is " + ring + "m out from the middle.\n\n"
              + "The fires only just reach the middle. It will be dim in there.",
                MessageType.Info);
        }
        else
        {
            EditorGUILayout.HelpBox(
                "Each fire lights about " + reach + "m around itself, but your circle is " + ring + "m out from the middle.\n\n"
              + "THE MIDDLE WILL STAY DARK. A circle this wide is for looks, not for light. It will be beautiful at the "
              + "edge of the cave and useless where you fight. If you want to see the boss, place a second smaller ring "
              + "inside this one (change the Group object name first, so this one is not deleted).",
                MessageType.Warning);
        }
    }

    private void DrawBudgetBox()
    {
        int budget = QualitySettings.pixelLightCount;
        Light[] all = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
        int on = 0;
        for (int i = 0; i < all.Length; i++)
            if (all[i] != null && all[i].enabled && all[i].isActiveAndEnabled) on++;

        string msg = "Lights switched on in this scene: " + on
                   + "\nPixel Light Count (the real limit): " + budget
                   + "\n\nOnly " + budget + " lights are drawn properly on any one object at a time. "
                   + "Keeping each fire's Range small is what stops them fighting over those slots.";
        EditorGUILayout.HelpBox(msg, on > budget + 8 ? MessageType.Warning : MessageType.Info);
    }

    #endregion

    #region Placing

    private Vector3 Centre()
    {
        return useCentreObject && centreObject != null ? centreObject.position : centrePoint;
    }

    private Vector3 RingPoint(int index, Vector3 c)
    {
        float step = 360f / Mathf.Max(1, count);
        float a = (startAngle + index * step) * Mathf.Deg2Rad;
        return new Vector3(c.x + Mathf.Sin(a) * radius, c.y, c.z + Mathf.Cos(a) * radius);
    }

    private void PlaceRing()
    {
        Vector3 c = Centre();
        ClearRing();

        GameObject group = new GameObject(groupName);
        Undo.RegisterCreatedObjectUndo(group, "Place Light Ring");
        group.transform.position = c;

        int dropped = 0;
        int missed = 0;

        for (int i = 0; i < count; i++)
        {
            Vector3 p = RingPoint(i, c);

            if (snapToGround)
            {
                RaycastHit hit;
                Vector3 from = new Vector3(p.x, c.y + 300f, p.z);
                if (Physics.Raycast(from, Vector3.down, out hit, 900f, ~0, QueryTriggerInteraction.Ignore))
                {
                    p.y = hit.point.y;
                    dropped++;
                }
                else
                {
                    p.y = c.y;
                    missed++;
                }
            }
            else
            {
                p.y = c.y;
            }

            p.y += yOffset;

            GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            if (go == null) go = Instantiate(prefab);

            go.name = labelPrefix + (i + 1);
            go.transform.SetParent(group.transform, true);
            go.transform.position = p;

            if (faceCentre)
            {
                Vector3 look = new Vector3(c.x, p.y, c.z) - p;
                if (look.sqrMagnitude > 0.0001f)
                    go.transform.rotation = Quaternion.LookRotation(look.normalized, Vector3.up);
            }

            Undo.RegisterCreatedObjectUndo(go, "Place Light Ring");
        }

        Selection.activeGameObject = group;
        EditorSceneManager.MarkSceneDirty(group.scene);

        string note = "Light Ring: PLACED " + count + " x " + prefab.name + " named \"" + groupName
                    + "\" at radius " + radius.ToString("0.#") + "m around ("
                    + c.x.ToString("0.#") + ", " + c.z.ToString("0.#") + ").";
        if (snapToGround) note += " Dropped onto ground: " + dropped + ", no ground found under: " + missed + ".";
        if (missed > 0) note += " The ones that missed are sitting at the centre height, move those by hand.";
        Debug.Log(note);
    }

    private void ClearRing()
    {
        GameObject existing = GameObject.Find(groupName);
        if (existing != null) Undo.DestroyObjectImmediate(existing);
    }

    #endregion

    #region Scene view

    private void OnSceneGUI(SceneView view)
    {
        if (!drawPreview) return;
        if (useCentreObject && centreObject == null) return;

        Vector3 c = Centre();
        bool placed = GameObject.Find(groupName) != null;

        if (keepClearRadius > 0.01f)
        {
            Handles.color = new Color(0.85f, 0.25f, 0.18f, 1f);
            Handles.DrawWireDisc(c, Vector3.up, keepClearRadius);
            Handles.Label(c + new Vector3(0f, 0.5f, keepClearRadius), keepClearRadius.ToString("0.#") + "m keep clear");
        }

        Handles.color = new Color(0.95f, 0.62f, 0.20f, 1f);
        Handles.DrawWireDisc(c, Vector3.up, radius);
        Handles.Label(c + new Vector3(0f, 0.5f, 0f), placed ? groupName + ": placed" : groupName + ": GUIDE ONLY, not placed yet");

        for (int i = 0; i < count; i++)
        {
            Vector3 p = RingPoint(i, c);
            Handles.DrawWireDisc(p, Vector3.up, 0.8f);
            Handles.DrawLine(p, p + new Vector3(0f, 3f, 0f));
            Handles.Label(p + new Vector3(0f, 3.4f, 0f), labelPrefix + (i + 1));
        }
    }

    #endregion
}
#endif
