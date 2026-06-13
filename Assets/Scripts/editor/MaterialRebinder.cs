using UnityEngine;
using UnityEditor;
using System.IO;

public class MaterialRebinder : EditorWindow
{
    [MenuItem("Tools/Rebind Broken Materials By Name")]
    static void RebindMaterials()
    {
        string[] allMatGuids = AssetDatabase.FindAssets("t:Material", new[] { "Assets/Japanese City Megapack/Assets/Materials" });
        var materialDict = new System.Collections.Generic.Dictionary<string, Material>();

        foreach (string guid in allMatGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null && !materialDict.ContainsKey(mat.name))
            {
                materialDict[mat.name] = mat;
            }
        }

        int fixedCount = 0;

        // Go through all MeshRenderers in the scene and prefabs
        GameObject[] allObjects = GameObject.FindObjectsOfType<GameObject>();

        foreach (GameObject obj in allObjects)
        {
            MeshRenderer renderer = obj.GetComponent<MeshRenderer>();
            if (renderer == null) continue;

            var materials = renderer.sharedMaterials;
            bool changed = false;

            for (int i = 0; i < materials.Length; i++)
            {
                if (materials[i] == null || materials[i].shader.name == "Hidden/InternalErrorShader")
                {
                    string guessedName = obj.name;
                    if (materialDict.ContainsKey(guessedName))
                    {
                        materials[i] = materialDict[guessedName];
                        changed = true;
                        fixedCount++;
                        Debug.Log($"✔ Reassigned material on '{obj.name}' to '{guessedName}'");
                    }
                }
            }

            if (changed)
            {
                renderer.sharedMaterials = materials;
                EditorUtility.SetDirty(renderer);
            }
        }

        Debug.Log($"✅ Finished reassigning materials. Fixed: {fixedCount}");
    }
}
