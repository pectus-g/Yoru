#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// ROUND 71 - places a ring of props (braziers, lanterns, anything) evenly around a point
/// and drops each one onto the ground for you.
///
/// Open it from the top menu: Yoru Tools > 4 - Light Ring Tool.
///
/// Why this exists: a ring of six braziers is eighteen numbers typed by hand, and every one
/// of them has to be dropped onto the terrain afterwards. This does all of it in one click,
/// keeps the prefab link intact, and supports Undo, so you can try a radius, look at it,
/// press Cmd+Z, and try another.
///
/// It never changes the prefab itself. It only creates instances in the open scene, all
/// parented under one group object you can delete in one go.
///
/// EDITOR ONLY. Nothing here ships in a build.
/// </summary>
public class LightRingTool : EditorWindow
{
    #region Settings

    private GameObject prefab;
    private Transform centreObject;
    private Vector3 centrePoint = new Vector3(482f, 0f, 415f);
    private bool useCentreObject = true;

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

    #endregion

    #region Window

    [MenuItem("Yoru Tools/4 - Light Ring Tool", priority = 4)]
    public static void Open()
    {
        LightRingTool w = GetWindow<LightRingTool>(false, "Light Ring", true);
        w.minSize = new Vector2(340f, 460f);
        w.Show();
    }

    private void OnEnable()
    {
        SceneView.duringSceneGui += OnSceneGUI;
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
    }

    #endregion

    #region UI

    private void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);

        EditorGUILayout.LabelField("What to place", EditorStyles.boldLabel);
        prefab = (GameObject)EditorGUILayout.ObjectField("Prefab", prefab, typeof(GameObject), false);
        if (prefab == null)
            EditorGUILayout.HelpBox("Drag your Brazier prefab in here (Assets > Org_Prefabs > Lights > Brazier).", MessageType.Info);

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Where the ring goes", EditorStyles.boldLabel);
        useCentreObject = EditorGUILayout.Toggle("Use an object as centre", useCentreObject);
        if (useCentreObject)
        {
            centreObject = (Transform)EditorGUILayout.ObjectField("Centre object", centreObject, typeof(Transform), true);
            if (centreObject == null)
                EditorGUILayout.HelpBox("Drag CineStageMark in here, or untick the box above and type the position.", MessageType.Info);
        }
        else
        {
            centrePoint = EditorGUILayout.Vector3Field("Centre position", centrePoint);
        }

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("The ring", EditorStyles.boldLabel);
        radius = EditorGUILayout.Slider("Radius (metres)", radius, 3f, 80f);
        count = EditorGUILayout.IntSlider("How many", count, 1, 24);
        startAngle = EditorGUILayout.Slider("Start angle", startAngle, 0f, 359f);
        EditorGUILayout.LabelField(" ", "0 = due north. With 6 at start 30 the north side is left open.", EditorStyles.miniLabel);
        yOffset = EditorGUILayout.FloatField("Height offset", yOffset);
        faceCentre = EditorGUILayout.Toggle("Turn to face the middle", faceCentre);
        snapToGround = EditorGUILayout.Toggle("Drop onto the ground", snapToGround);

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Naming", EditorStyles.boldLabel);
        groupName = EditorGUILayout.TextField("Group object", groupName);
        labelPrefix = EditorGUILayout.TextField("Name prefix", labelPrefix);

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Scene view guides", EditorStyles.boldLabel);
        drawPreview = EditorGUILayout.Toggle("Draw the rings", drawPreview);
        keepClearRadius = EditorGUILayout.Slider("Keep clear radius", keepClearRadius, 0f, 40f);
        EditorGUILayout.LabelField(" ", "Red ring. The cinematic camera needs this empty.", EditorStyles.miniLabel);

        EditorGUILayout.Space(12f);

        bool canPlace = prefab != null && (!useCentreObject || centreObject != null);
        EditorGUI.BeginDisabledGroup(!canPlace);
        if (GUILayout.Button("Place the ring", GUILayout.Height(30f))) PlaceRing();
        EditorGUI.EndDisabledGroup();

        if (GUILayout.Button("Clear the ring")) ClearRing();

        EditorGUILayout.Space(10f);
        DrawBudgetBox();

        EditorGUILayout.EndScrollView();
    }

    private void DrawBudgetBox()
    {
        int budget = QualitySettings.pixelLightCount;
        Light[] all = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
        int realtime = 0;
        for (int i = 0; i < all.Length; i++)
            if (all[i] != null && all[i].enabled && all[i].isActiveAndEnabled) realtime++;

        string msg = "Lights switched on in this scene: " + realtime
                   + "\nPixel Light Count (the real limit): " + budget
                   + "\n\nOnly " + budget + " lights are drawn properly on any one object at a time. "
                   + "Keeping each torch Range small is what stops them fighting over those slots.";
        EditorGUILayout.HelpBox(msg, realtime > budget + 6 ? MessageType.Warning : MessageType.Info);
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

        string note = "Light Ring: placed " + count + " x " + prefab.name
                    + " at radius " + radius.ToString("0.#") + "m around ("
                    + c.x.ToString("0.#") + ", " + c.z.ToString("0.#") + ").";
        if (snapToGround) note += " Dropped onto ground: " + dropped + ", no ground found under: " + missed + ".";
        if (missed > 0) note += " The ones that missed are sitting at the centre height, move them by hand.";
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

        if (keepClearRadius > 0.01f)
        {
            Handles.color = new Color(0.85f, 0.25f, 0.18f, 1f);
            Handles.DrawWireDisc(c, Vector3.up, keepClearRadius);
            Handles.Label(c + new Vector3(0f, 0.5f, keepClearRadius), keepClearRadius.ToString("0.#") + "m keep clear");
        }

        Handles.color = new Color(0.95f, 0.62f, 0.20f, 1f);
        Handles.DrawWireDisc(c, Vector3.up, radius);

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
