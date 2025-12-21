using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// YORU: Shader Property Finder
/// 
/// Use this to find the exact wind property names in your Polyart shaders.
/// 
/// HOW TO USE:
/// 1. Add this script to any GameObject in DemoScene_Day
/// 2. Enter Play mode
/// 3. Right-click this component → "Find All Wind Properties"
/// 4. Check Console for results
/// 5. Update YoruCozyIntegration with the correct property names
/// </summary>
public class ShaderPropertyFinder : MonoBehaviour
{
    [Header("=== RESULTS ===")]
    [TextArea(10, 20)]
    public string foundProperties = "Click 'Find All Wind Properties' in Play mode";
    
    [ContextMenu("Find All Wind Properties")]
    public void FindAllWindProperties()
    {
        var renderers = FindObjectsOfType<Renderer>();
        HashSet<string> processedShaders = new HashSet<string>();
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        
        sb.AppendLine("=== WIND-RELATED SHADER PROPERTIES ===\n");
        
        foreach (var renderer in renderers)
        {
            foreach (var mat in renderer.sharedMaterials)
            {
                if (mat == null || mat.shader == null) continue;
                
                string shaderName = mat.shader.name;
                
                // Only process foliage/tree/grass shaders
                string lower = shaderName.ToLower();
                if (!lower.Contains("foliage") && 
                    !lower.Contains("tree") && 
                    !lower.Contains("grass") &&
                    !lower.Contains("leaf") &&
                    !lower.Contains("polyart") &&
                    !lower.Contains("dreamscape") &&
                    !lower.Contains("pa_") &&
                    !lower.Contains("wind"))
                {
                    continue;
                }
                
                // Skip already processed shaders
                if (processedShaders.Contains(shaderName)) continue;
                processedShaders.Add(shaderName);
                
                sb.AppendLine($"SHADER: {shaderName}");
                sb.AppendLine($"  Material: {mat.name}");
                
                // Find all float properties that might be wind-related
                int count = mat.shader.GetPropertyCount();
                bool foundAny = false;
                
                for (int i = 0; i < count; i++)
                {
                    string propName = mat.shader.GetPropertyName(i);
                    string propLower = propName.ToLower();
                    
                    // Look for wind/sway related properties
                    if (propLower.Contains("wind") || 
                        propLower.Contains("sway") ||
                        propLower.Contains("speed") ||
                        propLower.Contains("strength") ||
                        propLower.Contains("amplitude") ||
                        propLower.Contains("frequency") ||
                        propLower.Contains("movement") ||
                        propLower.Contains("wave"))
                    {
                        var propType = mat.shader.GetPropertyType(i);
                        sb.AppendLine($"    → {propName} ({propType})");
                        foundAny = true;
                    }
                }
                
                if (!foundAny)
                {
                    sb.AppendLine("    (no wind properties found - listing ALL):");
                    for (int i = 0; i < count && i < 30; i++)
                    {
                        string propName = mat.shader.GetPropertyName(i);
                        var propType = mat.shader.GetPropertyType(i);
                        sb.AppendLine($"    [{i}] {propName} ({propType})");
                    }
                    if (count > 30) sb.AppendLine($"    ... and {count - 30} more");
                }
                
                sb.AppendLine();
            }
        }
        
        if (processedShaders.Count == 0)
        {
            sb.AppendLine("No foliage/tree shaders found in scene.");
        }
        
        foundProperties = sb.ToString();
        Debug.Log(foundProperties);
    }
    
    [ContextMenu("Test Wind Global Properties")]
    public void TestWindGlobalProperties()
    {
        Debug.Log("=== SETTING GLOBAL WIND PROPERTIES ===");
        
        // Test setting various common property names
        string[] props = {
            "_WindSpeed", "_WindStrength", "_SwayAmount", "_SwaySpeed", "_Sway",
            "_WindMultiplier", "_WindIntensity", "_GlobalWindSpeed",
            "CZY_WindSpeed", "CZY_WindMultiplier"
        };
        
        foreach (string prop in props)
        {
            Shader.SetGlobalFloat(prop, 1.0f);
            Debug.Log($"  Set {prop} = 1.0");
        }
        
        Debug.Log("\nWind properties set to maximum. Check if foliage is moving.");
        Debug.Log("If still not moving, the shader uses different property names.");
    }
    
    [ContextMenu("Reset Wind Global Properties")]
    public void ResetWindGlobalProperties()
    {
        string[] props = {
            "_WindSpeed", "_WindStrength", "_SwayAmount", "_SwaySpeed", "_Sway",
            "_WindMultiplier", "_WindIntensity", "_GlobalWindSpeed",
            "CZY_WindSpeed", "CZY_WindMultiplier"
        };
        
        foreach (string prop in props)
        {
            Shader.SetGlobalFloat(prop, 0f);
        }
        
        Debug.Log("Wind properties reset to 0");
    }
}