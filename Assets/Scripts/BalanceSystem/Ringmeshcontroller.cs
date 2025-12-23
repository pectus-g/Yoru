using UnityEngine;
using System.Collections;

/// <summary>
/// YORU: Ring Mesh Controller - V6 WITH VFX SUPPORT
/// 
/// VFX System for Tail Transitions:
/// - Spawns VFX prefab when ring count changes
/// - VFX covers the entire tail, then fades
/// - Left tail: Fire effect (dark choices)
/// - Right tail: Light/sparkle effect (light choices)
/// 
/// See TailVFXSetupGuide.txt for how to create the VFX prefabs.
/// </summary>
public class RingMeshController : MonoBehaviour
{
    [Header("=== LEFT TAIL RING MESHES ===")]
    public GameObject leftTail_NoRings;
    public GameObject leftTail_1Ring;
    public GameObject leftTail_2Rings;
    public GameObject leftTail_3Rings;
    public GameObject leftTail_4Rings;
    public GameObject leftTail_5Rings;
    public GameObject leftTail_6Rings;
    public GameObject leftTail_7Rings;
    public GameObject leftTail_8Rings;
    public GameObject leftTail_9Rings;
    public GameObject leftTail_10Rings;
    
    [Header("=== RIGHT TAIL RING MESHES ===")]
    public GameObject rightTail_NoRings;
    public GameObject rightTail_1Ring;
    public GameObject rightTail_2Rings;
    public GameObject rightTail_3Rings;
    public GameObject rightTail_4Rings;
    public GameObject rightTail_5Rings;
    public GameObject rightTail_6Rings;
    public GameObject rightTail_7Rings;
    public GameObject rightTail_8Rings;
    public GameObject rightTail_9Rings;
    public GameObject rightTail_10Rings;
    
    [Header("=== TRANSITION SETTINGS ===")]
    [Tooltip("Seconds before old mesh is deactivated")]
    [Range(0.5f, 5f)]
    public float transitionDelay = 2f;
    
    [Header("=== LEFT TAIL VFX (Fire) ===")]
    [Tooltip("Fire VFX prefab - covers entire left tail during transition")]
    public GameObject leftTailVFXPrefab;
    [Tooltip("Spawn point - assign the LEFT TAIL ROOT BONE")]
    public Transform leftTailVFXSpawnPoint;
    
    [Header("=== RIGHT TAIL VFX (Light) ===")]
    [Tooltip("Light VFX prefab - covers entire right tail during transition")]
    public GameObject rightTailVFXPrefab;
    [Tooltip("Spawn point - assign the RIGHT TAIL ROOT BONE")]
    public Transform rightTailVFXSpawnPoint;
    
    [Header("=== VFX TIMING ===")]
    [Tooltip("Delay before mesh swap (VFX covers during this time)")]
    public float vfxCoverDelay = 0.5f;
    [Tooltip("Total VFX duration (auto-destroys after)")]
    public float vfxDuration = 2.5f;
    [Tooltip("Parent VFX to tail (follows movement)")]
    public bool parentVFXToTail = true;
    
    [Header("=== AUTO-FIND ===")]
    public bool autoFindMeshes = true;
    public Transform searchRoot;
    
    [Header("=== DEBUG ===")]
    public bool logChanges = true;
    
    // Internal arrays
    private GameObject[] leftTailMeshes;
    private GameObject[] rightTailMeshes;
    
    // Current state
    private int currentLeftRings = 0;
    private int currentRightRings = 0;
    
    // Active coroutines
    private Coroutine leftTransition;
    private Coroutine rightTransition;
    
    void Awake()
    {
        BuildMeshArrays();
        
        if (autoFindMeshes)
        {
            AutoFindMeshes();
            BuildMeshArrays();
        }
    }
    
    void Start()
    {
        InitializeMeshes();
        
        if (WorldStateManager.Instance != null)
        {
            WorldStateManager.Instance.OnRingsChanged.AddListener(OnRingsChanged);
            ApplyMeshesInstant(WorldStateManager.Instance.LeftRings, WorldStateManager.Instance.RightRings);
        }
        
        LogVFXStatus();
    }
    
    void OnDestroy()
    {
        if (WorldStateManager.Instance != null)
            WorldStateManager.Instance.OnRingsChanged.RemoveListener(OnRingsChanged);
    }
    
    void BuildMeshArrays()
    {
        leftTailMeshes = new GameObject[] {
            leftTail_NoRings, leftTail_1Ring, leftTail_2Rings, leftTail_3Rings,
            leftTail_4Rings, leftTail_5Rings, leftTail_6Rings, leftTail_7Rings,
            leftTail_8Rings, leftTail_9Rings, leftTail_10Rings
        };
        rightTailMeshes = new GameObject[] {
            rightTail_NoRings, rightTail_1Ring, rightTail_2Rings, rightTail_3Rings,
            rightTail_4Rings, rightTail_5Rings, rightTail_6Rings, rightTail_7Rings,
            rightTail_8Rings, rightTail_9Rings, rightTail_10Rings
        };
    }
    
    void InitializeMeshes()
    {
        for (int i = 0; i < leftTailMeshes.Length; i++)
            if (leftTailMeshes[i] != null)
                leftTailMeshes[i].SetActive(i == 0);
        
        for (int i = 0; i < rightTailMeshes.Length; i++)
            if (rightTailMeshes[i] != null)
                rightTailMeshes[i].SetActive(i == 0);
        
        currentLeftRings = 0;
        currentRightRings = 0;
    }
    
    void AutoFindMeshes()
    {
        Transform root = searchRoot != null ? searchRoot : transform;
        
        // LEFT TAIL
        leftTail_NoRings = FindChild(root, "LeftTail_NoRings");
        leftTail_1Ring = FindChild(root, "LeftTail_1_Ring");
        leftTail_2Rings = FindChild(root, "LeftTail_2_Rings");
        leftTail_3Rings = FindChild(root, "LeftTail_3_Rings");
        leftTail_4Rings = FindChild(root, "LeftTail_4_Rings");
        leftTail_5Rings = FindChild(root, "LeftTail_5_Rings");
        leftTail_6Rings = FindChild(root, "LeftTail_6_Rings");
        leftTail_7Rings = FindChild(root, "LeftTail_7_Rings");
        leftTail_8Rings = FindChild(root, "LeftTail_8_Rings");
        leftTail_9Rings = FindChild(root, "LeftTail_9_Rings");
        leftTail_10Rings = FindChild(root, "LeftTail_10_Rings");
        
        // RIGHT TAIL
        rightTail_NoRings = FindChild(root, "RightTail_NoRings");
        rightTail_1Ring = FindChild(root, "RightTail_1_Ring");
        rightTail_2Rings = FindChild(root, "RightTail_2_Rings");
        rightTail_3Rings = FindChild(root, "RightTail_3_Rings");
        rightTail_4Rings = FindChild(root, "RightTail_4_Rings");
        rightTail_5Rings = FindChild(root, "RightTail_5_Rings");
        rightTail_6Rings = FindChild(root, "RightTail_6_Rings");
        rightTail_7Rings = FindChild(root, "RightTail_7_Rings");
        rightTail_8Rings = FindChild(root, "RightTail_8_Rings");
        rightTail_9Rings = FindChild(root, "RightTail_9_Rings");
        rightTail_10Rings = FindChild(root, "RightTail_10_Rings");
        
        // Try to find VFX spawn points (tail root bones)
        if (leftTailVFXSpawnPoint == null)
            leftTailVFXSpawnPoint = FindTransformByNames(root, "L_Tail_01", "LeftTailRoot", "LeftTail01");
        if (rightTailVFXSpawnPoint == null)
            rightTailVFXSpawnPoint = FindTransformByNames(root, "R_Tail_01", "RightTailRoot", "RightTail01");
        
        if (logChanges)
        {
            int leftCount = CountNonNull(leftTailMeshes);
            int rightCount = CountNonNull(rightTailMeshes);
            Debug.Log($"[RingMesh] Auto-found {leftCount}/11 left, {rightCount}/11 right meshes");
        }
    }
    
    void LogVFXStatus()
    {
        if (logChanges)
        {
            string leftVFX = leftTailVFXPrefab != null ? "✓" : "○ (assign prefab)";
            string rightVFX = rightTailVFXPrefab != null ? "✓" : "○ (assign prefab)";
            string leftSpawn = leftTailVFXSpawnPoint != null ? leftTailVFXSpawnPoint.name : "○ (assign bone)";
            string rightSpawn = rightTailVFXSpawnPoint != null ? rightTailVFXSpawnPoint.name : "○ (assign bone)";
            
            Debug.Log($"[RingMesh] VFX Status:");
            Debug.Log($"  Left: Prefab={leftVFX}, Spawn={leftSpawn}");
            Debug.Log($"  Right: Prefab={rightVFX}, Spawn={rightSpawn}");
        }
    }
    
    GameObject FindChild(Transform root, string name)
    {
        Transform t = FindTransformRecursive(root, name);
        return t != null ? t.gameObject : null;
    }
    
    Transform FindTransformByNames(Transform root, params string[] names)
    {
        foreach (var name in names)
        {
            Transform t = FindTransformRecursive(root, name);
            if (t != null) return t;
        }
        return null;
    }
    
    Transform FindTransformRecursive(Transform parent, string name)
    {
        foreach (Transform child in parent)
        {
            if (child.name == name) return child;
            Transform found = FindTransformRecursive(child, name);
            if (found != null) return found;
        }
        return null;
    }
    
    int CountNonNull(GameObject[] arr)
    {
        int count = 0;
        foreach (var obj in arr)
            if (obj != null) count++;
        return count;
    }
    
    void OnRingsChanged(int left, int right)
    {
        UpdateMeshes(left, right);
    }
    
    void ApplyMeshesInstant(int left, int right)
    {
        left = Mathf.Clamp(left, 0, 10);
        right = Mathf.Clamp(right, 0, 10);
        
        for (int i = 0; i < leftTailMeshes.Length; i++)
            if (leftTailMeshes[i] != null)
                leftTailMeshes[i].SetActive(i == left);
        
        for (int i = 0; i < rightTailMeshes.Length; i++)
            if (rightTailMeshes[i] != null)
                rightTailMeshes[i].SetActive(i == right);
        
        currentLeftRings = left;
        currentRightRings = right;
    }
    
    public void UpdateMeshes(int leftRings, int rightRings)
    {
        leftRings = Mathf.Clamp(leftRings, 0, 10);
        rightRings = Mathf.Clamp(rightRings, 0, 10);
        
        // LEFT TAIL
        if (leftRings != currentLeftRings)
        {
            int oldLeft = currentLeftRings;
            currentLeftRings = leftRings;
            
            if (leftTransition != null)
                StopCoroutine(leftTransition);
            
            leftTransition = StartCoroutine(TransitionWithVFX(
                leftTailMeshes, oldLeft, leftRings, "LEFT",
                leftTailVFXPrefab, leftTailVFXSpawnPoint));
        }
        
        // RIGHT TAIL
        if (rightRings != currentRightRings)
        {
            int oldRight = currentRightRings;
            currentRightRings = rightRings;
            
            if (rightTransition != null)
                StopCoroutine(rightTransition);
            
            rightTransition = StartCoroutine(TransitionWithVFX(
                rightTailMeshes, oldRight, rightRings, "RIGHT",
                rightTailVFXPrefab, rightTailVFXSpawnPoint));
        }
        
        if (logChanges)
            Debug.Log($"[RingMesh] Transitioning to {leftRings}L / {rightRings}R");
    }
    
    /// <summary>
    /// Transition with VFX:
    /// 1. Spawn VFX (fire/light bursts up covering tail)
    /// 2. Wait for VFX to cover
    /// 3. Activate new mesh (hidden by VFX)
    /// 4. Wait for transition
    /// 5. Deactivate old mesh
    /// 6. VFX fades and auto-destroys
    /// </summary>
    IEnumerator TransitionWithVFX(GameObject[] meshes, int oldIndex, int newIndex, 
        string side, GameObject vfxPrefab, Transform spawnPoint)
    {
        // STEP 1: Spawn VFX (if assigned)
        GameObject vfxInstance = null;
        if (vfxPrefab != null && spawnPoint != null)
        {
            vfxInstance = Instantiate(vfxPrefab, spawnPoint.position, spawnPoint.rotation);
            
            if (parentVFXToTail)
                vfxInstance.transform.SetParent(spawnPoint);
            
            // Auto-destroy after duration
            Destroy(vfxInstance, vfxDuration);
            
            if (logChanges)
                Debug.Log($"[RingMesh] {side}: 🔥 VFX spawned!");
        }
        
        // STEP 2: Wait for VFX to cover the tail
        if (vfxPrefab != null)
            yield return new WaitForSeconds(vfxCoverDelay);
        
        // STEP 3: Activate new mesh (hidden by VFX flames)
        if (newIndex >= 0 && newIndex < meshes.Length && meshes[newIndex] != null)
        {
            meshes[newIndex].SetActive(true);
            if (logChanges)
                Debug.Log($"[RingMesh] {side}: Activated mesh [{newIndex}]");
        }
        
        // STEP 4: Wait for full transition
        yield return new WaitForSeconds(transitionDelay);
        
        // STEP 5: Deactivate old mesh
        if (oldIndex >= 0 && oldIndex < meshes.Length && oldIndex != newIndex && meshes[oldIndex] != null)
        {
            meshes[oldIndex].SetActive(false);
            if (logChanges)
                Debug.Log($"[RingMesh] {side}: Deactivated mesh [{oldIndex}]");
        }
        
        // STEP 6: Clean up orphaned meshes
        int currentTarget = (side == "LEFT") ? currentLeftRings : currentRightRings;
        for (int i = 0; i < meshes.Length; i++)
        {
            if (i != currentTarget && meshes[i] != null && meshes[i].activeSelf)
            {
                meshes[i].SetActive(false);
                if (logChanges)
                    Debug.Log($"[RingMesh] {side}: Cleaned up orphan [{i}]");
            }
        }
    }
    
    [ContextMenu("Force Cleanup Now")]
    public void ForceCleanup()
    {
        if (leftTransition != null) StopCoroutine(leftTransition);
        if (rightTransition != null) StopCoroutine(rightTransition);
        
        for (int i = 0; i < leftTailMeshes.Length; i++)
            if (leftTailMeshes[i] != null)
                leftTailMeshes[i].SetActive(i == currentLeftRings);
        
        for (int i = 0; i < rightTailMeshes.Length; i++)
            if (rightTailMeshes[i] != null)
                rightTailMeshes[i].SetActive(i == currentRightRings);
        
        Debug.Log($"[RingMesh] Force cleanup: {currentLeftRings}L / {currentRightRings}R");
    }
    
    #region CONTEXT MENU TESTS
    
    [ContextMenu("Test: Spawn Left VFX")]
    public void TestLeftVFX()
    {
        if (leftTailVFXPrefab != null && leftTailVFXSpawnPoint != null)
        {
            var vfx = Instantiate(leftTailVFXPrefab, leftTailVFXSpawnPoint.position, leftTailVFXSpawnPoint.rotation);
            if (parentVFXToTail) vfx.transform.SetParent(leftTailVFXSpawnPoint);
            Destroy(vfx, vfxDuration);
            Debug.Log("[RingMesh] Left VFX spawned for testing!");
        }
        else
        {
            Debug.LogWarning("[RingMesh] Assign leftTailVFXPrefab and leftTailVFXSpawnPoint first!");
        }
    }
    
    [ContextMenu("Test: Spawn Right VFX")]
    public void TestRightVFX()
    {
        if (rightTailVFXPrefab != null && rightTailVFXSpawnPoint != null)
        {
            var vfx = Instantiate(rightTailVFXPrefab, rightTailVFXSpawnPoint.position, rightTailVFXSpawnPoint.rotation);
            if (parentVFXToTail) vfx.transform.SetParent(rightTailVFXSpawnPoint);
            Destroy(vfx, vfxDuration);
            Debug.Log("[RingMesh] Right VFX spawned for testing!");
        }
        else
        {
            Debug.LogWarning("[RingMesh] Assign rightTailVFXPrefab and rightTailVFXSpawnPoint first!");
        }
    }
    
    [ContextMenu("Auto-Find Meshes")]
    public void ContextAutoFind()
    {
        AutoFindMeshes();
        BuildMeshArrays();
    }
    
    [ContextMenu("Test: 0L/0R")]
    public void Test00() { UpdateMeshes(0, 0); }
    
    [ContextMenu("Test: 5L/0R")]
    public void Test50() { UpdateMeshes(5, 0); }
    
    [ContextMenu("Test: 5L/5R (Eclipse)")]
    public void Test55() { UpdateMeshes(5, 5); }
    
    [ContextMenu("Print Status")]
    public void PrintStatus()
    {
        Debug.Log($"=== RING MESH STATUS ===");
        Debug.Log($"Current: {currentLeftRings}L / {currentRightRings}R");
        LogVFXStatus();
    }
    
    #endregion
}