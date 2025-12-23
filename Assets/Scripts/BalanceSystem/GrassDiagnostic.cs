using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// YORU: Grass Diagnostic Tool
/// 
/// Use this to find what shader properties your grass materials actually have.
/// Add to any GameObject and run the context menu commands.
/// </summary>
public class GrassDiagnostic : MonoBehaviour
{
    [ContextMenu("Find All Grass Materials & Properties")]
    public void FindGrassMaterials()
    {
        Debug.Log("=== SEARCHING FOR GRASS MATERIALS ===");
        
        var renderers = FindObjectsOfType<Renderer>();
        HashSet<Material> found = new HashSet<Material>();
        
        foreach (var renderer in renderers)
        {
            foreach (var mat in renderer.sharedMaterials)
            {
                if (mat == null || mat.shader == null) continue;
                
                string matName = mat.name.ToLower();
                string shaderName = mat.shader.name.ToLower();
                
                if (matName.Contains("grass") || shaderName.Contains("grass"))
                {
                    if (!found.Contains(mat))
                    {
                        found.Add(mat);
                        
                        Debug.Log($"\n=== GRASS MATERIAL: {mat.name} ===");
                        Debug.Log($"Shader: {mat.shader.name}");
                        
                        // List all properties
                        int count = mat.shader.GetPropertyCount();
                        Debug.Log($"Properties ({count}):");
                        
                        for (int i = 0; i < count; i++)
                        {
                            string propName = mat.shader.GetPropertyName(i);
                            var propType = mat.shader.GetPropertyType(i);
                            
                            // Highlight wind-related properties
                            bool isWind = propName.ToLower().Contains("wind") || 
                                          propName.ToLower().Contains("sway") ||
                                          propName.ToLower().Contains("speed") ||
                                          propName.ToLower().Contains("intensity");
                            
                            string marker = isWind ? " ← WIND?" : "";
                            Debug.Log($"  [{i}] {propName} ({propType}){marker}");
                        }
                    }
                }
            }
        }
        
        Debug.Log($"\n=== FOUND {found.Count} GRASS MATERIALS ===");
        
        if (found.Count == 0)
        {
            Debug.Log("No grass materials found. Your grass might be:");
            Debug.Log("  - Unity Terrain grass (uses TerrainData.wavingGrass* settings)");
            Debug.Log("  - Not named with 'grass' in the material name");
            Debug.Log("  - Using a different shader type");
        }
    }
    
    [ContextMenu("Check Terrain Grass Settings")]
    public void CheckTerrainGrass()
    {
        var terrain = Terrain.activeTerrain;
        if (terrain == null)
        {
            Debug.Log("No active terrain found");
            return;
        }
        
        var td = terrain.terrainData;
        if (td == null)
        {
            Debug.Log("No terrain data");
            return;
        }
        
        Debug.Log("=== TERRAIN GRASS SETTINGS ===");
        Debug.Log($"wavingGrassSpeed: {td.wavingGrassSpeed}");
        Debug.Log($"wavingGrassStrength: {td.wavingGrassStrength}");
        Debug.Log($"wavingGrassAmount: {td.wavingGrassAmount}");
        Debug.Log($"wavingGrassTint: {td.wavingGrassTint}");
        
        Debug.Log("\n=== TERRAIN DETAIL LAYERS ===");
        Debug.Log($"Detail resolution: {td.detailResolution}");
        Debug.Log($"Detail layers: {td.detailPrototypes.Length}");
        
        for (int i = 0; i < td.detailPrototypes.Length; i++)
        {
            var proto = td.detailPrototypes[i];
            Debug.Log($"\nLayer [{i}]:");
            Debug.Log($"  Prototype: {(proto.prototype != null ? proto.prototype.name : "NULL")}");
            Debug.Log($"  Render Mode: {proto.renderMode}");
            Debug.Log($"  Use Instancing: {proto.useInstancing}");
            Debug.Log($"  Use Prototypes: {proto.usePrototypeMesh}");
        }
    }
    
    [ContextMenu("Test Terrain Grass Wind")]
    public void TestTerrainGrassWind()
    {
        var terrain = Terrain.activeTerrain;
        if (terrain == null)
        {
            Debug.LogError("No terrain found");
            return;
        }
        
        var td = terrain.terrainData;
        
        // Set to maximum wind
        td.wavingGrassSpeed = 2.5f;
        td.wavingGrassStrength = 1.0f;
        td.wavingGrassAmount = 1.0f;
        
        Debug.Log("Set terrain grass wind to MAXIMUM - check if grass is moving");
    }
    
    [ContextMenu("Find All Polyart Foliage Shaders")]
    public void FindPolyartShaders()
    {
        Debug.Log("=== ALL POLYART/DREAMSCAPE SHADERS IN USE ===");
        
        var renderers = FindObjectsOfType<Renderer>();
        HashSet<string> shaders = new HashSet<string>();
        
        foreach (var renderer in renderers)
        {
            foreach (var mat in renderer.sharedMaterials)
            {
                if (mat == null || mat.shader == null) continue;
                
                string shaderName = mat.shader.name;
                if (shaderName.ToLower().Contains("polyart") || 
                    shaderName.ToLower().Contains("dreamscape"))
                {
                    if (!shaders.Contains(shaderName))
                    {
                        shaders.Add(shaderName);
                        Debug.Log($"  {shaderName}");
                    }
                }
            }
        }
        
        Debug.Log($"\nTotal: {shaders.Count} unique Polyart shaders");
    }
}