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
///   meshes as children. A collider left on an LOD child, or a collider built from a low detail
///   LOD mesh, sits below the visible LOD0 surface in places, and Yoru sinks. This tool puts ONE
///   collider on each LOD Group object, built from the LOD0 (highest detail) mesh, and removes
///   any stale colliders left on the LOD children. If a collider already exists on the LOD Group
///   object, its mesh is corrected to LOD0 rather than skipped, so re running always repairs a
///   bad earlier state.
///
/// MULTI OBJECT SELECTIONS
///   Every LOD Group underneath the selection is processed. Selecting one parent container, like
///   the Mountains group, fixes every mountain inside it in a single run.
///
/// NON LOD OBJECTS
///   A plain mesh object simply gets a MeshCollider matching its mesh.
///
/// USAGE
///   1. Create a layer called "Climbable" (Edit, Project Settings, Tags and Layers).
///   2. Make sure Player and Climbable can collide (Edit, Project Settings, Physics, the matrix).
///   3. Select your cliff objects in the scene, or one parent container holding all of them.
///   4. Tools, YORU, Make Selection Climbable. Safe to run again, it cleans up and re does itself.
///
/// This file must live inside an Editor folder, or stay wrapped in UNITY_EDITOR as it is.
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
        int collidersFixed = 0;
        int staleRemoved = 0;
        int objectsTouched = 0;

        foreach (GameObject root in roots)
            ApplyClimbable(root, layer, ref collidersAdded, ref collidersFixed, ref staleRemoved, ref objectsTouched);

        Debug.Log("ClimbColliderTool: processed " + objectsTouched + " object(s), added " +
            collidersAdded + " MeshCollider(s), corrected " + collidersFixed +
            " existing collider(s), removed " + staleRemoved + " stale LOD child collider(s).");
    }

    private static void ApplyClimbable(GameObject root, int layer, ref int collidersAdded,
        ref int collidersFixed, ref int staleRemoved, ref int objectsTouched)
    {
        // Every LOD Group under this selection root, including the root itself and inactive children.
        LODGroup[] lodGroups = root.GetComponentsInChildren<LODGroup>(true);

        if (lodGroups.Length > 0)
        {
            foreach (LODGroup lodGroup in lodGroups)
                ApplyToLodGroup(lodGroup, layer, ref collidersAdded, ref collidersFixed, ref staleRemoved, ref objectsTouched);
        }
        else
        {
            // No LOD groups anywhere below: a mesh collider per mesh.
            MeshFilter[] filters = root.GetComponentsInChildren<MeshFilter>(true);
            foreach (MeshFilter mf in filters)
            {
                if (mf.sharedMesh == null) continue;
                GameObject go = mf.gameObject;

                SetLayerAndStatic(go, layer);
                objectsTouched++;

                MeshCollider mc = go.GetComponent<MeshCollider>();
                if (mc == null && go.GetComponent<Collider>() == null)
                {
                    mc = Undo.AddComponent<MeshCollider>(go);
                    mc.convex = false;
                    mc.sharedMesh = mf.sharedMesh;
                    collidersAdded++;
                }
                else if (mc != null && mc.sharedMesh != mf.sharedMesh)
                {
                    Undo.RecordObject(mc, "Fix Climbable Collider Mesh");
                    mc.convex = false;
                    mc.sharedMesh = mf.sharedMesh;
                    collidersFixed++;
                }
                EditorUtility.SetDirty(go);
            }
        }
    }

    /// <summary>One stable collider on the LOD Group object, built from the LOD0 mesh.</summary>
    private static void ApplyToLodGroup(LODGroup lodGroup, int layer, ref int collidersAdded,
        ref int collidersFixed, ref int staleRemoved, ref int objectsTouched)
    {
        GameObject target = lodGroup.gameObject;

        staleRemoved += RemoveLodChildColliders(lodGroup);

        Mesh lod0Mesh = GetLod0Mesh(lodGroup);
        if (lod0Mesh == null)
        {
            Debug.LogWarning("ClimbColliderTool: " + target.name +
                " has an LOD Group but no readable LOD0 mesh. No collider added.");
            return;
        }

        SetLayerAndStatic(target, layer);
        objectsTouched++;

        MeshCollider mc = target.GetComponent<MeshCollider>();
        if (mc == null)
        {
            mc = Undo.AddComponent<MeshCollider>(target);
            mc.convex = false;
            mc.sharedMesh = lod0Mesh;
            collidersAdded++;
        }
        else if (mc.sharedMesh != lod0Mesh || mc.convex)
        {
            // A collider from an earlier run or a wrong mesh: correct it instead of skipping.
            Undo.RecordObject(mc, "Fix Climbable Collider Mesh");
            mc.convex = false;
            mc.sharedMesh = lod0Mesh;
            collidersFixed++;
        }
        EditorUtility.SetDirty(target);
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

    /// <summary>Removes MeshColliders left on the LOD child renderers by an earlier run. Returns how many.</summary>
    private static int RemoveLodChildColliders(LODGroup lodGroup)
    {
        int removed = 0;
        foreach (LOD lod in lodGroup.GetLODs())
        {
            foreach (Renderer r in lod.renderers)
            {
                if (r == null) continue;
                if (r.gameObject == lodGroup.gameObject) continue; // never strip the target itself
                MeshCollider mc = r.GetComponent<MeshCollider>();
                if (mc != null)
                {
                    Undo.DestroyObjectImmediate(mc);
                    removed++;
                }
            }
        }
        return removed;
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