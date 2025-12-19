using UnityEngine;

/// <summary>
/// Controls the visual ring meshes on Yoru's tails.
/// Listens to WorldStateManager and toggles the correct mesh for each tail.
/// Only ONE mesh per tail is active at a time.
/// </summary>
public class RingMeshController : MonoBehaviour
{
    // ═══════════════════════════════════════════════════════════
    // REFERENCES
    // ═══════════════════════════════════════════════════════════
    
    [Header("Left Tail Meshes (Dark/Condemn)")]
    [Tooltip("The mesh shown when left tail has 0 rings")]
    [SerializeField] private GameObject leftTail_NoRings;
    
    [Tooltip("Array of meshes for 1-5 rings. Index 0 = 1 ring, Index 4 = 5 rings")]
    [SerializeField] private GameObject[] leftTailRings = new GameObject[5];
    
    [Header("Right Tail Meshes (Light/Forgive)")]
    [Tooltip("The mesh shown when right tail has 0 rings")]
    [SerializeField] private GameObject rightTail_NoRings;
    
    [Tooltip("Array of meshes for 1-5 rings. Index 0 = 1 ring, Index 4 = 5 rings")]
    [SerializeField] private GameObject[] rightTailRings = new GameObject[5];
    
    [Header("Auto-Find Settings")]
    [Tooltip("If true, automatically finds ring meshes by name on Start")]
    [SerializeField] private bool autoFindMeshes = true;
    
    [Tooltip("Parent transform containing all tail meshes (e.g., Cat_All_10_Tails_v3)")]
    [SerializeField] private Transform tailMeshParent;
    
    // ═══════════════════════════════════════════════════════════
    // LIFECYCLE
    // ═══════════════════════════════════════════════════════════
    
    private void Start()
    {
        if (autoFindMeshes)
        {
            FindMeshesAutomatically();
        }
        
        ValidateMeshReferences();
        
        // Subscribe to WorldStateManager
        if (WorldStateManager.Instance != null)
        {
            WorldStateManager.Instance.OnRingsChanged.AddListener(OnRingsChanged);
            
            // Initialize to current state
            OnRingsChanged(WorldStateManager.Instance.LeftRings, WorldStateManager.Instance.RightRings);
        }
        else
        {
            Debug.LogError("[RingMeshController] WorldStateManager.Instance not found! Make sure it exists in the scene.");
        }
    }
    
    private void OnDestroy()
    {
        // Unsubscribe to prevent memory leaks
        if (WorldStateManager.Instance != null)
        {
            WorldStateManager.Instance.OnRingsChanged.RemoveListener(OnRingsChanged);
        }
    }
    
    // ═══════════════════════════════════════════════════════════
    // EVENT HANDLER
    // ═══════════════════════════════════════════════════════════
    
    private void OnRingsChanged(int leftCount, int rightCount)
    {
        UpdateLeftTailMesh(leftCount);
        UpdateRightTailMesh(rightCount);
    }
    
    // ═══════════════════════════════════════════════════════════
    // MESH TOGGLING
    // ═══════════════════════════════════════════════════════════
    
    private void UpdateLeftTailMesh(int ringCount)
    {
        // Disable all left tail meshes first
        if (leftTail_NoRings != null)
            leftTail_NoRings.SetActive(false);
        
        for (int i = 0; i < leftTailRings.Length; i++)
        {
            if (leftTailRings[i] != null)
                leftTailRings[i].SetActive(false);
        }
        
        // Enable the correct mesh
        if (ringCount == 0)
        {
            if (leftTail_NoRings != null)
                leftTail_NoRings.SetActive(true);
        }
        else if (ringCount >= 1 && ringCount <= 5)
        {
            int index = ringCount - 1; // 1 ring = index 0, 5 rings = index 4
            if (leftTailRings[index] != null)
                leftTailRings[index].SetActive(true);
        }
    }
    
    private void UpdateRightTailMesh(int ringCount)
    {
        // Disable all right tail meshes first
        if (rightTail_NoRings != null)
            rightTail_NoRings.SetActive(false);
        
        for (int i = 0; i < rightTailRings.Length; i++)
        {
            if (rightTailRings[i] != null)
                rightTailRings[i].SetActive(false);
        }
        
        // Enable the correct mesh
        if (ringCount == 0)
        {
            if (rightTail_NoRings != null)
                rightTail_NoRings.SetActive(true);
        }
        else if (ringCount >= 1 && ringCount <= 5)
        {
            int index = ringCount - 1;
            if (rightTailRings[index] != null)
                rightTailRings[index].SetActive(true);
        }
    }
    
    // ═══════════════════════════════════════════════════════════
    // AUTO-FIND MESHES
    // ═══════════════════════════════════════════════════════════
    
    private void FindMeshesAutomatically()
    {
        if (tailMeshParent == null)
        {
            // Try to find by common name
            GameObject player = GameObject.FindWithTag("Player");
            if (player != null)
            {
                tailMeshParent = player.transform.Find("Cat_All_10_Tails_v3");
            }
        }
        
        if (tailMeshParent == null)
        {
            Debug.LogWarning("[RingMeshController] Could not find tail mesh parent. Please assign manually.");
            return;
        }
        
        // Find left tail meshes
        leftTail_NoRings = FindChildByName(tailMeshParent, "LeftTail_NoRings");
        for (int i = 0; i < 5; i++)
        {
            string meshName = $"LeftTail_{i + 1}_Ring" + (i > 0 ? "s" : "");
            // Try both singular and plural
            leftTailRings[i] = FindChildByName(tailMeshParent, $"LeftTail_{i + 1}_Ring");
            if (leftTailRings[i] == null)
                leftTailRings[i] = FindChildByName(tailMeshParent, $"LeftTail_{i + 1}_Rings");
        }
        
        // Find right tail meshes
        rightTail_NoRings = FindChildByName(tailMeshParent, "RightTail_NoRings");
        for (int i = 0; i < 5; i++)
        {
            rightTailRings[i] = FindChildByName(tailMeshParent, $"RightTail_{i + 1}_Ring");
            if (rightTailRings[i] == null)
                rightTailRings[i] = FindChildByName(tailMeshParent, $"RightTail_{i + 1}_Rings");
        }
        
        Debug.Log("[RingMeshController] Auto-find complete. Check Inspector to verify all meshes found.");
    }
    
    private GameObject FindChildByName(Transform parent, string name)
    {
        // Recursive search through all children
        foreach (Transform child in parent.GetComponentsInChildren<Transform>(true))
        {
            if (child.name == name)
                return child.gameObject;
        }
        return null;
    }
    
    // ═══════════════════════════════════════════════════════════
    // VALIDATION
    // ═══════════════════════════════════════════════════════════
    
    private void ValidateMeshReferences()
    {
        int missingCount = 0;
        
        if (leftTail_NoRings == null) { Debug.LogWarning("[RingMeshController] Missing: LeftTail_NoRings"); missingCount++; }
        if (rightTail_NoRings == null) { Debug.LogWarning("[RingMeshController] Missing: RightTail_NoRings"); missingCount++; }
        
        for (int i = 0; i < 5; i++)
        {
            if (leftTailRings[i] == null) { Debug.LogWarning($"[RingMeshController] Missing: LeftTail_{i + 1}_Ring(s)"); missingCount++; }
            if (rightTailRings[i] == null) { Debug.LogWarning($"[RingMeshController] Missing: RightTail_{i + 1}_Ring(s)"); missingCount++; }
        }
        
        if (missingCount == 0)
        {
            Debug.Log("[RingMeshController] All 12 mesh references found!");
        }
        else
        {
            Debug.LogWarning($"[RingMeshController] {missingCount} mesh reference(s) missing. Assign in Inspector.");
        }
    }
}