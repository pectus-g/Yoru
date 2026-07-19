// ============================================================================
// YoruCaveBossTools.cs — editor helpers for CaveScene_Oni_Boss1
// Menu: "Yoru Tools" (top menu bar). Run with the cave scene OPEN.
// Everything is undo-able (Cmd+Z) and reported in the Console.
// Remember to SAVE the scene (Cmd+S) after a fix.
// ============================================================================
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Unity.Cinemachine;

public static class YoruCaveBossTools
{
    // Foliage never gets colliders — walking through plants is correct.
    static readonly Regex FoliageName = new Regex(
        "fern|ivy|weed|grass|leaf|leaves|vine|flower|plant|bush|moss",
        RegexOptions.IgnoreCase);

    // ------------------------------------------------------------------
    // 1 — CAMERA
    // Why the camera doesn't follow: ThirdPersonCamera drives the follow
    // OFFSET every frame, but Cinemachine also needs the vcam's
    // Tracking Target to know WHO to follow. In DemoScene_Day that was a
    // hand-set scene override; this scene's camera prefab has it = None.
    // ------------------------------------------------------------------
    [MenuItem("Yoru Tools/1 — Fix Camera Follow (open scene)", priority = 1)]
    public static void FixCameraFollow()
    {
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
        {
            EditorUtility.DisplayDialog("Fix Camera Follow",
                "No object tagged 'Player' found in the open scene.\n\nIs PlayerYoru_Def in this scene?", "OK");
            return;
        }

        var vcam = Object.FindFirstObjectByType<CinemachineCamera>(FindObjectsInactive.Include);
        if (vcam == null)
        {
            EditorUtility.DisplayDialog("Fix Camera Follow",
                "No CinemachineCamera found in the open scene.\n\nIs MainCameraBackup in this scene?", "OK");
            return;
        }

        var report = new List<string>();

        // --- Wire the vcam Tracking Target -> Yoru ---
        Undo.RecordObject(vcam, "Fix Camera Follow");
        var target = vcam.Target;
        target.TrackingTarget = player.transform;
        vcam.Target = target;
        PrefabUtility.RecordPrefabInstancePropertyModifications(vcam);
        report.Add($"vcam '{vcam.gameObject.name}' Tracking Target -> '{player.name}'");

        // --- Also fill ThirdPersonCamera.playerTransform (it would auto-find
        //     at runtime, but set it so it's visible & correct in the Inspector) ---
        var tpc = Object.FindFirstObjectByType<ThirdPersonCamera>(FindObjectsInactive.Include);
        if (tpc != null)
        {
            var so = new SerializedObject(tpc);
            var sp = so.FindProperty("playerTransform");
            if (sp != null)
            {
                Undo.RecordObject(tpc, "Fix Camera Follow");
                sp.objectReferenceValue = player.transform;
                so.ApplyModifiedProperties();
                PrefabUtility.RecordPrefabInstancePropertyModifications(tpc);
                report.Add($"ThirdPersonCamera.playerTransform -> '{player.name}'");
            }
        }
        else report.Add("WARNING: no ThirdPersonCamera found on the camera rig.");

        // --- Health checks (report only, nothing changed) ---
        if (Object.FindFirstObjectByType<CinemachineBrain>(FindObjectsInactive.Include) == null)
            report.Add("WARNING: no CinemachineBrain in scene — Cinemachine cannot drive the camera!");
        if (Camera.main == null)
            report.Add("WARNING: no camera tagged 'MainCamera' in scene.");

        // Is there ground under Yoru? (prevents the classic fall-through-on-play)
        var origin = player.transform.position + Vector3.up * 1f;
        if (Physics.Raycast(origin, Vector3.down, out var hit, 500f))
            report.Add($"Ground check: '{hit.collider.gameObject.name}' is {hit.distance - 1f:F2}m under Yoru's feet.");
        else
            report.Add("WARNING: NOTHING under Yoru within 500m — she will fall on Play. Move her above the terrain/floor.");

        EditorSceneManager.MarkSceneDirty(player.scene);

        var msg = string.Join("\n", report);
        Debug.Log("[YoruTools] Fix Camera Follow:\n" + msg);
        EditorUtility.DisplayDialog("Fix Camera Follow — done",
            msg + "\n\nNow SAVE the scene (Cmd+S), then press Play to test.", "OK");
    }

    // ------------------------------------------------------------------
    // 2 — COLLIDER VERIFY (report only, changes nothing)
    // ------------------------------------------------------------------
    [MenuItem("Yoru Tools/2 — Verify Colliders (report only)", priority = 2)]
    public static void VerifyColliders()
    {
        Scan(fix: false);
    }

    // ------------------------------------------------------------------
    // 3 — COLLIDER FIX (adds MeshColliders where missing, re-enables disabled)
    // ------------------------------------------------------------------
    [MenuItem("Yoru Tools/3 — Fix Colliders (add missing)", priority = 3)]
    public static void FixColliders()
    {
        Scan(fix: true);
    }

    static void Scan(bool fix)
    {
        int ok = 0, foliage = 0, added = 0, reenabled = 0;
        var missing = new List<string>();
        var broken = new List<string>();

        foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
        {
            var go = r.gameObject;

            // Skip particles/VFX-ish and foliage by name
            if (FoliageName.IsMatch(go.name)) { foliage++; continue; }

            // A collider on the same object or any parent counts as covered
            var col = r.GetComponentInParent<Collider>(true);

            if (col == null)
            {
                if (fix)
                {
                    var mf = go.GetComponent<MeshFilter>();
                    if (mf != null && mf.sharedMesh != null)
                    {
                        var mc = Undo.AddComponent<MeshCollider>(go);
                        mc.sharedMesh = mf.sharedMesh;
                        added++;
                    }
                    else missing.Add(Path(go) + "  (no mesh — add a collider by hand)");
                }
                else missing.Add(Path(go));
            }
            else if (!col.enabled)
            {
                if (fix)
                {
                    Undo.RecordObject(col, "Enable collider");
                    col.enabled = true;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(col);
                    reenabled++;
                }
                else broken.Add(Path(go) + "  (collider DISABLED)");
            }
            else if (col is MeshCollider m && m.sharedMesh == null)
            {
                broken.Add(Path(go) + "  (MeshCollider has NO mesh)");
            }
            else ok++;
        }

        bool terrainOk = Object.FindFirstObjectByType<TerrainCollider>(FindObjectsInactive.Include) != null;

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Colliders OK: {ok}   |   Foliage skipped (correct): {foliage}");
        sb.AppendLine(terrainOk ? "Terrain: has TerrainCollider ✓" : "Terrain: NO TerrainCollider found!");
        if (fix) sb.AppendLine($"MeshColliders ADDED: {added}   |   re-enabled: {reenabled}");
        if (missing.Count > 0)
        {
            sb.AppendLine($"MISSING colliders ({missing.Count}):");
            foreach (var s in missing) sb.AppendLine("   " + s);
        }
        if (broken.Count > 0)
        {
            sb.AppendLine($"BROKEN colliders ({broken.Count}):");
            foreach (var s in broken) sb.AppendLine("   " + s);
        }
        if (missing.Count == 0 && broken.Count == 0 && !fix)
            sb.AppendLine("Nothing to fix — every structural piece is already collidable.");

        if (fix && (added > 0 || reenabled > 0))
            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());

        Debug.Log($"[YoruTools] Collider {(fix ? "fix" : "verify")}:\n" + sb);
        EditorUtility.DisplayDialog($"Colliders — {(fix ? "fixed" : "verified")}",
            sb.ToString() + (fix ? "\nSAVE the scene (Cmd+S)." : ""), "OK");
    }

    static string Path(GameObject go)
    {
        var t = go.transform;
        var s = t.name;
        while (t.parent != null) { t = t.parent; s = t.name + "/" + s; }
        return s;
    }
}
