#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Batch fixes for climbable cliffs. Complex cliff shapes cannot be covered by box or sphere
/// colliders, so this adds a MeshCollider (which matches the exact mesh) to every selected object
/// and its mesh children, puts them on the Climbable layer, and marks them Static.
///
/// A non convex MeshCollider is correct for static scenery. It is accurate, the CharacterController
/// stands on it (no sinking), and the climb sphere casts hit it. Convex is only needed for moving
/// rigidbodies, so leave it off here.
///
/// USAGE
///   1. Create a layer called "Climbable" (Edit, Project Settings, Tags and Layers).
///   2. Make sure Player and Climbable can collide (Edit, Project Settings, Physics, the matrix).
///      This is on by default. It keeps the cliff solid so Yoru can walk on it and stand on top.
///   3. Select your cliff objects in the scene.
///   4. Tools, YORU, Make Selection Climbable.
///
/// This file must live inside an Editor folder (it already is).
/// </summary>
public static class ClimbColliderTool
{
    private const string ClimbableLayerName = "Climbable";

    [MenuItem("Tools/YORU/Make Selection Climbable")]
    private static void MakeSelectionClimbable()
    {
        GameObject[] roots = Selection.gameObjects;
        if (roots == null || roots.Length == 0)
        {
            Debug.LogWarning("ClimbColliderTool: nothing selected.");
            return;
        }

        int layer = LayerMask.NameToLayer(ClimbableLayerName);
        if (layer < 0)
        {
            Debug.LogError("ClimbColliderTool: no layer named '" + ClimbableLayerName +
                "'. Create it in Project Settings, Tags and Layers, then run again.");
            return;
        }

        int collidersAdded = 0;
        int objectsTouched = 0;

        foreach (GameObject root in roots)
        {
            MeshFilter[] filters = root.GetComponentsInChildren<MeshFilter>(true);
            foreach (MeshFilter mf in filters)
            {
                if (mf.sharedMesh == null) continue;
                GameObject go = mf.gameObject;
                objectsTouched++;

                // Layer.
                Undo.RecordObject(go, "Make Climbable");
                go.layer = layer;

                // Static flags (batching and navigation).
                GameObjectUtility.SetStaticEditorFlags(go,
                    GameObjectUtility.GetStaticEditorFlags(go) |
                    StaticEditorFlags.BatchingStatic | StaticEditorFlags.NavigationStatic);

                // Mesh collider, only if the object has no collider yet.
                if (go.GetComponent<Collider>() == null)
                {
                    MeshCollider mc = Undo.AddComponent<MeshCollider>(go);
                    mc.convex = false;
                    mc.sharedMesh = mf.sharedMesh;
                    collidersAdded++;
                }

                EditorUtility.SetDirty(go);
            }
        }

        Debug.Log("ClimbColliderTool: set " + objectsTouched + " mesh object(s) to '" +
            ClimbableLayerName + "' and Static, added " + collidersAdded + " MeshCollider(s).");
    }

    [MenuItem("Tools/YORU/Add Mesh Colliders To Selection (layer untouched)")]
    private static void AddMeshCollidersOnly()
    {
        GameObject[] roots = Selection.gameObjects;
        if (roots == null || roots.Length == 0)
        {
            Debug.LogWarning("ClimbColliderTool: nothing selected.");
            return;
        }

        int collidersAdded = 0;
        foreach (GameObject root in roots)
        {
            MeshFilter[] filters = root.GetComponentsInChildren<MeshFilter>(true);
            foreach (MeshFilter mf in filters)
            {
                if (mf.sharedMesh == null) continue;
                if (mf.GetComponent<Collider>() != null) continue;

                MeshCollider mc = Undo.AddComponent<MeshCollider>(mf.gameObject);
                mc.convex = false;
                mc.sharedMesh = mf.sharedMesh;
                collidersAdded++;
                EditorUtility.SetDirty(mf.gameObject);
            }
        }

        Debug.Log("ClimbColliderTool: added " + collidersAdded + " MeshCollider(s).");
    }
}
#endif
