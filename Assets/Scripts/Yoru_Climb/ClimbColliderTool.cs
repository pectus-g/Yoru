#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Batch fix for climbable cliffs, LOD aware.
///
/// Complex cliff shapes cannot be covered by box or sphere colliders, so this uses a non convex
/// MeshCollider, which matches the exact mesh. Non convex is correct for static scenery: it is
/// accurate, the CharacterController stands on it (no sinking), and the climb casts hit it.
///
/// LOD MODELS (the important case)
///   Many environment props, like the EastLands mountains, have an LOD Group with several LOD
///   meshes as children. The LOD Group switches those child meshes on and off by camera distance,
///   so a collider placed on a child stops colliding when that child is switched off, and Yoru
///   sinks. This tool detects an LOD Group and instead puts ONE collider on the LOD Group object
///   itself, built from the LOD0 (highest detail) mesh. That object is never toggled, so the
///   collider is always solid. It also removes any stale colliders left on the LOD children.
///
/// NON LOD OBJECTS
///   A plain mesh object simply gets a MeshCollider matching its mesh.
///
/// USAGE
///   1. Create a layer called "Climbable" (Edit, Project Settings, Tags and Layers).
///   2. Make sure Player and Climbable can collide (Edit, Project Settings, Physics, the matrix).
///   3. Select your cliff objects in the scene (the top object, the one with the LOD Group).
///   4. Tools, YORU, Make Selection Climbable. Safe to run again, it cleans up and re does itself.
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
            ApplyClimbable(root, layer, ref collidersAdded, ref objectsTouched);

        Debug.Log("ClimbColliderTool: processed " + objectsTouched + " object(s), added " +
            collidersAdded + " MeshCollider(s).");
    }

    private static void ApplyClimbable(GameObject root, int layer, ref int collidersAdded, ref int objectsTouched)
    {
        LODGroup lodGroup = root.GetComponentInChildren<LODGroup>();

        if (lodGroup != null)
        {
            // LOD model: one stable collider on the LOD Group object, never toggled by the group.
            GameObject target = lodGroup.gameObject;

            RemoveLodChildColliders(lodGroup);

            Mesh lod0Mesh = GetLod0Mesh(lodGroup);
            if (lod0Mesh == null)
            {
                Debug.LogWarning("ClimbColliderTool: " + root.name +
                    " has an LOD Group but no readable LOD0 mesh. No collider added.");
                return;
            }

            SetLayerAndStatic(target, layer);
            objectsTouched++;

            if (target.GetComponent<Collider>() == null)
            {
                MeshCollider mc = Undo.AddComponent<MeshCollider>(target);
                mc.convex = false;
                mc.sharedMesh = lod0Mesh;
                collidersAdded++;
            }
            EditorUtility.SetDirty(target);
        }
        else
        {
            // No LOD group: a mesh collider per mesh.
            MeshFilter[] filters = root.GetComponentsInChildren<MeshFilter>(true);
            foreach (MeshFilter mf in filters)
            {
                if (mf.sharedMesh == null) continue;
                GameObject go = mf.gameObject;

                SetLayerAndStatic(go, layer);
                objectsTouched++;

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
    }

    /// <summary>Sets the object to the climbable layer and marks it Static (batching and navigation).</summary>
    private static void SetLayerAndStatic(GameObject go, int layer)
    {
        Undo.RecordObject(go, "Make Climbable");
        go.layer = layer;
        GameObjectUtility.SetStaticEditorFlags(go,
            GameObjectUtility.GetStaticEditorFlags(go) |
            StaticEditorFlags.BatchingStatic | StaticEditorFlags.NavigationStatic);
    }

    /// <summary>Removes MeshColliders left on the LOD child renderers by an earlier run.</summary>
    private static void RemoveLodChildColliders(LODGroup lodGroup)
    {
        foreach (LOD lod in lodGroup.GetLODs())
        {
            foreach (Renderer r in lod.renderers)
            {
                if (r == null) continue;
                MeshCollider mc = r.GetComponent<MeshCollider>();
                if (mc != null) Undo.DestroyObjectImmediate(mc);
            }
        }
    }

    /// <summary>Finds the LOD0 mesh from the highest detail LOD level.</summary>
    private static Mesh GetLod0Mesh(LODGroup lodGroup)
    {
        LOD[] lods = lodGroup.GetLODs();
        if (lods.Length == 0) return null;

        foreach (Renderer r in lods[0].renderers)
        {
            if (r == null) continue;

            MeshFilter mf = r.GetComponent<MeshFilter>();
            if (mf != null && mf.sharedMesh != null) return mf.sharedMesh;

            SkinnedMeshRenderer smr = r as SkinnedMeshRenderer;
            if (smr != null && smr.sharedMesh != null) return smr.sharedMesh;
        }
        return null;
    }
}
#endif