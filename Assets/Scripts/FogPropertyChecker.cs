using UnityEngine;

public class FogPropertyChecker : MonoBehaviour
{
    [ContextMenu("List Fog Properties")]
    void ListFogProperties()
    {
        var fogs = FindObjectsOfType<MonoBehaviour>();
        foreach (var fog in fogs)
        {
            if (fog.GetType().Name.Contains("Fog"))
            {
                Debug.Log($"=== FOUND: {fog.GetType().Name} on {fog.gameObject.name} ===");
                foreach (var prop in fog.GetType().GetProperties())
                {
                    Debug.Log($"  Property: {prop.Name} ({prop.PropertyType.Name})");
                }
                foreach (var field in fog.GetType().GetFields())
                {
                    Debug.Log($"  Field: {field.Name} ({field.FieldType.Name})");
                }
            }
        }
    }
}