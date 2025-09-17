using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;

public class FBXMaterialRebinder : EditorWindow
{
    [MenuItem("Tools/Rebind Materials to FBX Meshes")]
    public static void RebindFBXMaterials()
    {
        string meshFolder = "Assets/Japanese City Megapack/Assets/Meshes";
        string materialFolder = "Assets/Japanese City Megapack/Assets/Materials";

        string[] materialGuids = AssetDatabase.FindAssets("t:Material", new[] { materialFolder });
        Dictionary<string, Material> materialLookup = new Dictionary<string, Material>();

        foreach (string guid in materialGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!materialLookup.ContainsKey(mat.name))
                materialLookup[mat.name] = mat;
        }

        string[] fbxGuids = AssetDatabase.FindAssets("t:GameObject", new[] { meshFolder });
        int fixedCount = 0;

        foreach (string fbxGuid in fbxGuids)
        {
            string fbxPath = AssetDatabase.GUIDToAssetPath(fbxGuid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);

            if (prefab == null) continue;

            MeshRenderer[] renderers = prefab.GetComponentsInChildren<MeshRenderer>(true);

            foreach (MeshRenderer renderer in renderers)
            {
                var materials = renderer.sharedMaterials;
                bool changed = false;

                for (int i = 0; i < materials.Length; i++)
                {
                    Material mat = materials[i];
                    if (mat == null || mat.shader.name == "Hidden/InternalErrorShader")
                    {
                        string meshName = renderer.name;
                        if (materialLookup.TryGetValue(meshName, out Material newMat))
                        {
                            materials[i] = newMat;
                            changed = true;
                            Debug.Log($"✔ Reassigned material for '{meshName}' in '{fbxPath}'");
                            fixedCount++;
                        }
                    }
                }

                if (changed)
                {
                    renderer.sharedMaterials = materials;
                    EditorUtility.SetDirty(renderer);
                }
            }
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"✅ Rebinding complete. Total materials reassigned: {fixedCount}");
    }
}
