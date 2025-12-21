using UnityEngine;
using System.Collections;

/// <summary>
/// YORU: Ring Mesh Controller
/// 
/// FIXED BEHAVIOR:
/// 1. Activate NEW mesh first
/// 2. Wait 2-3 seconds (mesh transition delay)
/// 3. THEN deactivate the OLD mesh
/// 
/// This prevents bone visibility during transitions.
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
    [Tooltip("Seconds to wait before deactivating old mesh")]
    [Range(1f, 5f)]
    public float transitionDelay = 2.5f;
    
    [Header("=== AUTO-FIND SETTINGS ===")]
    public bool autoFindMeshes = true;
    public Transform searchRoot;
    
    [Header("=== DEBUG ===")]
    public bool logChanges = true;
    
    private GameObject[] leftTailMeshes;
    private GameObject[] rightTailMeshes;
    
    private int currentLeftRings = -1;
    private int currentRightRings = -1;
    
    private Coroutine leftTransitionCoroutine;
    private Coroutine rightTransitionCoroutine;
    
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
        // Initialize - deactivate all except 0 rings
        InitializeMeshes();
        
        if (WorldStateManager.Instance != null)
        {
            WorldStateManager.Instance.OnRingsChanged.AddListener(OnRingsChanged);
            // Apply initial state
            UpdateMeshes(WorldStateManager.Instance.LeftRings, WorldStateManager.Instance.RightRings);
        }
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
        // Start with all meshes OFF except 0 rings
        for (int i = 0; i < leftTailMeshes.Length; i++)
        {
            if (leftTailMeshes[i] != null)
                leftTailMeshes[i].SetActive(i == 0);
        }
        for (int i = 0; i < rightTailMeshes.Length; i++)
        {
            if (rightTailMeshes[i] != null)
                rightTailMeshes[i].SetActive(i == 0);
        }
        currentLeftRings = 0;
        currentRightRings = 0;
    }
    
    void AutoFindMeshes()
    {
        Transform root = searchRoot != null ? searchRoot : transform;
        
        leftTail_NoRings = FindChildByName(root, "LeftTail_NoRings");
        leftTail_1Ring = FindChildByName(root, "LeftTail_1_Ring");
        leftTail_2Rings = FindChildByName(root, "LeftTail_2_Rings");
        leftTail_3Rings = FindChildByName(root, "LeftTail_3_Rings");
        leftTail_4Rings = FindChildByName(root, "LeftTail_4_Rings");
        leftTail_5Rings = FindChildByName(root, "LeftTail_5_Rings");
        leftTail_6Rings = FindChildByName(root, "LeftTail_6_Rings");
        leftTail_7Rings = FindChildByName(root, "LeftTail_7_Rings");
        leftTail_8Rings = FindChildByName(root, "LeftTail_8_Rings");
        leftTail_9Rings = FindChildByName(root, "LeftTail_9_Rings");
        leftTail_10Rings = FindChildByName(root, "LeftTail_10_Rings");
        
        rightTail_NoRings = FindChildByName(root, "RightTail_NoRings");
        rightTail_1Ring = FindChildByName(root, "RightTail_1_Ring");
        rightTail_2Rings = FindChildByName(root, "RightTail_2_Rings");
        rightTail_3Rings = FindChildByName(root, "RightTail_3_Rings");
        rightTail_4Rings = FindChildByName(root, "RightTail_4_Rings");
        rightTail_5Rings = FindChildByName(root, "RightTail_5_Rings");
        rightTail_6Rings = FindChildByName(root, "RightTail_6_Rings");
        rightTail_7Rings = FindChildByName(root, "RightTail_7_Rings");
        rightTail_8Rings = FindChildByName(root, "RightTail_8_Rings");
        rightTail_9Rings = FindChildByName(root, "RightTail_9_Rings");
        rightTail_10Rings = FindChildByName(root, "RightTail_10_Rings");
        
        if (logChanges)
        {
            int leftFound = CountNonNull(leftTail_NoRings, leftTail_1Ring, leftTail_2Rings, leftTail_3Rings, 
                leftTail_4Rings, leftTail_5Rings, leftTail_6Rings, leftTail_7Rings, 
                leftTail_8Rings, leftTail_9Rings, leftTail_10Rings);
            int rightFound = CountNonNull(rightTail_NoRings, rightTail_1Ring, rightTail_2Rings, rightTail_3Rings,
                rightTail_4Rings, rightTail_5Rings, rightTail_6Rings, rightTail_7Rings,
                rightTail_8Rings, rightTail_9Rings, rightTail_10Rings);
            Debug.Log($"[RingMesh] Auto-found {leftFound}/11 left, {rightFound}/11 right meshes");
        }
    }
    
    GameObject FindChildByName(Transform root, string name)
    {
        Transform found = FindChildRecursive(root, name);
        return found != null ? found.gameObject : null;
    }
    
    Transform FindChildRecursive(Transform parent, string name)
    {
        foreach (Transform child in parent)
        {
            if (child.name == name) return child;
            Transform found = FindChildRecursive(child, name);
            if (found != null) return found;
        }
        return null;
    }
    
    int CountNonNull(params GameObject[] objects)
    {
        int count = 0;
        foreach (var obj in objects)
            if (obj != null) count++;
        return count;
    }
    
    void OnRingsChanged(int left, int right)
    {
        UpdateMeshes(left, right);
    }
    
    /// <summary>
    /// Update meshes with transition delay.
    /// Activates NEW mesh immediately, waits, then deactivates OLD mesh.
    /// </summary>
    public void UpdateMeshes(int leftRings, int rightRings)
    {
        leftRings = Mathf.Clamp(leftRings, 0, 10);
        rightRings = Mathf.Clamp(rightRings, 0, 10);
        
        // LEFT TAIL
        if (leftRings != currentLeftRings)
        {
            if (leftTransitionCoroutine != null)
                StopCoroutine(leftTransitionCoroutine);
            leftTransitionCoroutine = StartCoroutine(TransitionMesh(leftTailMeshes, currentLeftRings, leftRings, true));
            currentLeftRings = leftRings;
        }
        
        // RIGHT TAIL
        if (rightRings != currentRightRings)
        {
            if (rightTransitionCoroutine != null)
                StopCoroutine(rightTransitionCoroutine);
            rightTransitionCoroutine = StartCoroutine(TransitionMesh(rightTailMeshes, currentRightRings, rightRings, false));
            currentRightRings = rightRings;
        }
        
        if (logChanges)
            Debug.Log($"[RingMesh] Transitioning to {leftRings}L / {rightRings}R (delay: {transitionDelay}s)");
    }
    
    /// <summary>
    /// Coroutine that handles mesh transition:
    /// 1. Activate NEW mesh immediately
    /// 2. Wait transitionDelay seconds
    /// 3. Deactivate OLD mesh
    /// </summary>
    private IEnumerator TransitionMesh(GameObject[] meshes, int oldIndex, int newIndex, bool isLeft)
    {
        string side = isLeft ? "LEFT" : "RIGHT";
        
        // STEP 1: Activate NEW mesh IMMEDIATELY
        if (newIndex >= 0 && newIndex < meshes.Length && meshes[newIndex] != null)
        {
            meshes[newIndex].SetActive(true);
            if (logChanges)
                Debug.Log($"[RingMesh] {side}: Activated mesh [{newIndex}]");
        }
        
        // STEP 2: Wait for transition delay
        yield return new WaitForSeconds(transitionDelay);
        
        // STEP 3: Deactivate OLD mesh
        if (oldIndex >= 0 && oldIndex < meshes.Length && oldIndex != newIndex && meshes[oldIndex] != null)
        {
            meshes[oldIndex].SetActive(false);
            if (logChanges)
                Debug.Log($"[RingMesh] {side}: Deactivated mesh [{oldIndex}]");
        }
    }
    
    #region CONTEXT MENU
    
    [ContextMenu("Auto-Find Meshes")]
    public void ContextAutoFind()
    {
        AutoFindMeshes();
        BuildMeshArrays();
    }
    
    [ContextMenu("Test: 0/0")]
    public void Test00() => UpdateMeshes(0, 0);
    
    [ContextMenu("Test: 1L/0R")]
    public void Test10() => UpdateMeshes(1, 0);
    
    [ContextMenu("Test: 3L/0R")]
    public void Test30() => UpdateMeshes(3, 0);
    
    [ContextMenu("Test: 5L/0R")]
    public void Test50() => UpdateMeshes(5, 0);
    
    [ContextMenu("Test: 0L/5R")]
    public void Test05() => UpdateMeshes(0, 5);
    
    [ContextMenu("Test: 5L/5R Eclipse")]
    public void Test55() => UpdateMeshes(5, 5);
    
    [ContextMenu("Test: 10L/0R")]
    public void Test100() => UpdateMeshes(10, 0);
    
    [ContextMenu("Print Status")]
    public void PrintStatus()
    {
        Debug.Log($"=== RING MESH STATUS ===");
        Debug.Log($"Current: {currentLeftRings}L / {currentRightRings}R");
        Debug.Log($"Transition Delay: {transitionDelay}s");
        
        Debug.Log("LEFT MESHES:");
        for (int i = 0; i < leftTailMeshes.Length; i++)
        {
            if (leftTailMeshes[i] != null)
                Debug.Log($"  [{i}] {leftTailMeshes[i].name}: {(leftTailMeshes[i].activeSelf ? "ACTIVE" : "inactive")}");
            else
                Debug.Log($"  [{i}] NULL");
        }
        
        Debug.Log("RIGHT MESHES:");
        for (int i = 0; i < rightTailMeshes.Length; i++)
        {
            if (rightTailMeshes[i] != null)
                Debug.Log($"  [{i}] {rightTailMeshes[i].name}: {(rightTailMeshes[i].activeSelf ? "ACTIVE" : "inactive")}");
            else
                Debug.Log($"  [{i}] NULL");
        }
    }
    
    #endregion
}