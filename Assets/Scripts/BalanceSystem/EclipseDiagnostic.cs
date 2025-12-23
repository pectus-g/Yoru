using UnityEngine;
using DistantLands.Cozy;
using System.Reflection;

/// <summary>
/// YORU: Eclipse Diagnostic Tool
/// 
/// Use this to debug why Eclipse isn't working.
/// Add to any GameObject and run the context menu commands.
/// </summary>
public class EclipseDiagnostic : MonoBehaviour
{
    [ContextMenu("1. Find All COZY Module Types")]
    public void FindAllModuleTypes()
    {
        Debug.Log("=== SEARCHING FOR COZY MODULE TYPES ===");
        
        foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies())
        {
            try
            {
                foreach (var type in assembly.GetTypes())
                {
                    if (type.Namespace != null && type.Namespace.Contains("DistantLands") && 
                        type.Name.Contains("Module"))
                    {
                        Debug.Log($"  Found: {type.FullName}");
                    }
                }
            }
            catch { }
        }
    }
    
    [ContextMenu("2. Find Eclipse Specifically")]
    public void FindEclipseType()
    {
        Debug.Log("=== SEARCHING FOR ECLIPSE ===");
        
        System.Type foundType = null;
        
        foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies())
        {
            try
            {
                foreach (var type in assembly.GetTypes())
                {
                    if (type.Name.ToLower().Contains("eclipse"))
                    {
                        Debug.Log($"  Found type: {type.FullName} in {assembly.GetName().Name}");
                        
                        if (type.Name.Contains("Module"))
                        {
                            foundType = type;
                            Debug.Log($"  ^^^ THIS IS LIKELY THE MODULE");
                        }
                    }
                }
            }
            catch { }
        }
        
        if (foundType != null)
        {
            Debug.Log($"\n=== ECLIPSE MODULE: {foundType.FullName} ===");
            Debug.Log("Fields:");
            foreach (var f in foundType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                Debug.Log($"  Field: {f.Name} ({f.FieldType.Name})");
            }
            Debug.Log("Properties:");
            foreach (var p in foundType.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                Debug.Log($"  Property: {p.Name} ({p.PropertyType.Name})");
            }
        }
    }
    
    [ContextMenu("3. Try to Access Eclipse Module")]
    public void TryAccessEclipse()
    {
        var cozy = CozyWeather.instance;
        if (cozy == null)
        {
            Debug.LogError("CozyWeather.instance is null!");
            return;
        }
        
        Debug.Log("=== TRYING TO ACCESS ECLIPSE MODULE ===");
        
        // Try to find the type
        System.Type eclipseType = null;
        foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies())
        {
            try
            {
                foreach (var type in assembly.GetTypes())
                {
                    if (type.Name == "EclipseModule" || type.Name == "CozyEclipseModule")
                    {
                        eclipseType = type;
                        Debug.Log($"Found type: {type.FullName}");
                        break;
                    }
                }
            }
            catch { }
            if (eclipseType != null) break;
        }
        
        if (eclipseType == null)
        {
            Debug.LogError("Eclipse module type not found in assemblies!");
            Debug.Log("Make sure COZY Eclipse package is installed");
            return;
        }
        
        // Try GetModule
        try
        {
            var getModuleMethod = typeof(CozyWeather).GetMethod("GetModule");
            if (getModuleMethod == null)
            {
                Debug.LogError("GetModule method not found!");
                return;
            }
            
            var genericMethod = getModuleMethod.MakeGenericMethod(eclipseType);
            var module = genericMethod.Invoke(cozy, null);
            
            if (module == null)
            {
                Debug.LogError("Eclipse module is NULL - you need to ADD it in COZY Settings!");
                Debug.Log("1. Select your COZY Weather Sphere");
                Debug.Log("2. Click 'Settings' tab");
                Debug.Log("3. Click 'Add New Module'");
                Debug.Log("4. Add 'Eclipse Module'");
                return;
            }
            
            Debug.Log($"SUCCESS! Eclipse module found: {module.GetType().FullName}");
            
            // Try to read eclipseRatio
            var field = eclipseType.GetField("eclipseRatio", 
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            
            if (field != null)
            {
                var val = field.GetValue(module);
                Debug.Log($"eclipseRatio field value: {val}");
            }
            else
            {
                Debug.Log("eclipseRatio field not found, checking properties...");
                var prop = eclipseType.GetProperty("eclipseRatio",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (prop != null)
                {
                    var val = prop.GetValue(module);
                    Debug.Log($"eclipseRatio property value: {val}");
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error: {e.Message}\n{e.StackTrace}");
        }
    }
    
    [ContextMenu("4. Force Eclipse to 1.0")]
    public void ForceEclipse()
    {
        var cozy = CozyWeather.instance;
        if (cozy == null) return;
        
        System.Type eclipseType = null;
        foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies())
        {
            foreach (var type in assembly.GetTypes())
            {
                if (type.Name.Contains("Eclipse") && type.Name.Contains("Module"))
                {
                    eclipseType = type;
                    break;
                }
            }
            if (eclipseType != null) break;
        }
        
        if (eclipseType == null)
        {
            Debug.LogError("Eclipse type not found");
            return;
        }
        
        try
        {
            var getModuleMethod = typeof(CozyWeather).GetMethod("GetModule");
            var genericMethod = getModuleMethod.MakeGenericMethod(eclipseType);
            var module = genericMethod.Invoke(cozy, null);
            
            if (module == null)
            {
                Debug.LogError("Module is NULL");
                return;
            }
            
            // Try to set eclipseRatio
            var field = eclipseType.GetField("eclipseRatio", 
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            
            if (field != null)
            {
                field.SetValue(module, 1.0f);
                Debug.Log("Set eclipseRatio to 1.0!");
                
                // Also set time to 5PM
                cozy.timeModule.currentTime = new MeridiemTime(17, 0);
                Debug.Log("Set time to 5PM - look at the sun!");
            }
            else
            {
                Debug.LogError("eclipseRatio field not found");
                
                // List all fields
                Debug.Log("Available fields:");
                foreach (var f in eclipseType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                {
                    if (f.FieldType == typeof(float))
                        Debug.Log($"  {f.Name} = {f.GetValue(module)}");
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error: {e.Message}");
        }
    }
}