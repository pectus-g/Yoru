#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor-only setup that guarantees a Unity layer named "ExamineItem" exists, so the
/// item inspect camera (ItemExamineController) can render the item in isolation with no
/// manual project configuration. Runs automatically whenever scripts reload, and is
/// idempotent: once the layer is present it does nothing.
///
/// If you ever rename the examine layer on ItemExamineController, change LayerName here
/// to match (or just add the layer yourself in Project Settings > Tags and Layers).
/// </summary>
[InitializeOnLoad]
public static class ExamineLayerSetup
{
    private const string LayerName = "ExamineItem";

    static ExamineLayerSetup()
    {
        EnsureLayer(LayerName);
    }

    /// <summary>
    /// Add the named layer to the first free user slot (8 to 31) if it is not already
    /// defined. Built-in layers (slots 0 to 7) are left untouched.
    /// </summary>
    private static void EnsureLayer(string layerName)
    {
        Object[] assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
        if (assets == null || assets.Length == 0) return;

        SerializedObject tagManager = new SerializedObject(assets[0]);
        SerializedProperty layers = tagManager.FindProperty("layers");
        if (layers == null || !layers.isArray) return;

        // Already defined anywhere? Then there is nothing to do.
        for (int i = 0; i < layers.arraySize; i++)
        {
            if (layers.GetArrayElementAtIndex(i).stringValue == layerName) return;
        }

        // Write into the first empty user slot.
        for (int i = 8; i < layers.arraySize; i++)
        {
            SerializedProperty slot = layers.GetArrayElementAtIndex(i);
            if (string.IsNullOrEmpty(slot.stringValue))
            {
                slot.stringValue = layerName;
                tagManager.ApplyModifiedProperties();
                tagManager.Update();
                Debug.Log($"[ExamineLayerSetup] Created layer '{layerName}' in slot {i}.");
                return;
            }
        }

        Debug.LogWarning($"[ExamineLayerSetup] No free layer slot to create '{layerName}'. " +
                         "Free a user layer in Project Settings > Tags and Layers, or add it by hand.");
    }
}
#endif
