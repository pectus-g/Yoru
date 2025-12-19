using UnityEngine;

/// <summary>
/// Controls the visual ring meshes on Yoru's tails.
/// 
/// Performance: 
/// - References cached at Start
/// - Only toggles 2 objects per change (old off, new on)
/// - No per-frame updates
/// </summary>
public class RingMeshController : MonoBehaviour
{
    #region Serialized Fields
    
    [Header("Left Tail Meshes (Dark/Condemn)")]
    [SerializeField] private GameObject leftTail_NoRings;
    [SerializeField] private GameObject[] leftTailRings = new GameObject[5];
    
    [Header("Right Tail Meshes (Light/Forgive)")]
    [SerializeField] private GameObject rightTail_NoRings;
    [SerializeField] private GameObject[] rightTailRings = new GameObject[5];
    
    [Header("Setup")]
    [SerializeField] private bool autoFindOnStart = true;
    [SerializeField] private Transform meshParent;
    
    #endregion
    
    #region Private State
    
    private int currentLeftCount = -1;  // -1 = uninitialized
    private int currentRightCount = -1;
    private bool isInitialized;
    
    #endregion
    
    #region Unity Lifecycle
    
    private void Start()
    {
        if (autoFindOnStart && !HasAllReferences())
            AutoFindMeshes();
        
        isInitialized = HasAllReferences();
        
        if (!isInitialized)
        {
            Debug.LogError("[RingMeshController] Missing mesh references. Assign in Inspector.");
            enabled = false;
            return;
        }
        
        // Subscribe to events
        if (WorldStateManager.Instance != null)
        {
            WorldStateManager.Instance.OnRingsChanged.AddListener(OnRingsChanged);
            OnRingsChanged(WorldStateManager.Instance.LeftRings, WorldStateManager.Instance.RightRings);
        }
    }
    
    private void OnDestroy()
    {
        if (WorldStateManager.Instance != null)
            WorldStateManager.Instance.OnRingsChanged.RemoveListener(OnRingsChanged);
    }
    
    #endregion
    
    #region Event Handler
    
    private void OnRingsChanged(int leftCount, int rightCount)
    {
        if (!isInitialized) return;
        
        // Only update if changed
        if (leftCount != currentLeftCount)
            UpdateLeftTail(leftCount);
        
        if (rightCount != currentRightCount)
            UpdateRightTail(rightCount);
    }
    
    #endregion
    
    #region Mesh Updates
    
    private void UpdateLeftTail(int newCount)
    {
        // Disable old mesh
        if (currentLeftCount >= 0)
        {
            GameObject oldMesh = currentLeftCount == 0 ? leftTail_NoRings : leftTailRings[currentLeftCount - 1];
            if (oldMesh != null) oldMesh.SetActive(false);
        }
        
        // Enable new mesh
        GameObject newMesh = newCount == 0 ? leftTail_NoRings : leftTailRings[newCount - 1];
        if (newMesh != null) newMesh.SetActive(true);
        
        currentLeftCount = newCount;
    }
    
    private void UpdateRightTail(int newCount)
    {
        // Disable old mesh
        if (currentRightCount >= 0)
        {
            GameObject oldMesh = currentRightCount == 0 ? rightTail_NoRings : rightTailRings[currentRightCount - 1];
            if (oldMesh != null) oldMesh.SetActive(false);
        }
        
        // Enable new mesh
        GameObject newMesh = newCount == 0 ? rightTail_NoRings : rightTailRings[newCount - 1];
        if (newMesh != null) newMesh.SetActive(true);
        
        currentRightCount = newCount;
    }
    
    #endregion
    
    #region Setup Helpers
    
    private bool HasAllReferences()
    {
        if (leftTail_NoRings == null || rightTail_NoRings == null)
            return false;
        
        for (int i = 0; i < 5; i++)
        {
            if (leftTailRings[i] == null || rightTailRings[i] == null)
                return false;
        }
        return true;
    }
    
    private void AutoFindMeshes()
    {
        if (meshParent == null)
        {
            var player = GameObject.FindWithTag("Player");
            if (player != null)
                meshParent = player.transform.Find("Cat_All_10_Tails_v3");
        }
        
        if (meshParent == null)
        {
            Debug.LogWarning("[RingMeshController] Could not find mesh parent.");
            return;
        }
        
        // Cache the transform lookup - do this once
        var allChildren = meshParent.GetComponentsInChildren<Transform>(true);
        
        foreach (var child in allChildren)
        {
            string name = child.name;
            
            // Left tail
            if (name == "LeftTail_NoRings") leftTail_NoRings = child.gameObject;
            else if (name == "LeftTail_1_Ring") leftTailRings[0] = child.gameObject;
            else if (name == "LeftTail_2_Rings") leftTailRings[1] = child.gameObject;
            else if (name == "LeftTail_3_Rings") leftTailRings[2] = child.gameObject;
            else if (name == "LeftTail_4_Rings") leftTailRings[3] = child.gameObject;
            else if (name == "LeftTail_5_Rings") leftTailRings[4] = child.gameObject;
            // Right tail
            else if (name == "RightTail_NoRings") rightTail_NoRings = child.gameObject;
            else if (name == "RightTail_1_Ring") rightTailRings[0] = child.gameObject;
            else if (name == "RightTail_2_Rings") rightTailRings[1] = child.gameObject;
            else if (name == "RightTail_3_Rings") rightTailRings[2] = child.gameObject;
            else if (name == "RightTail_4_Rings") rightTailRings[3] = child.gameObject;
            else if (name == "RightTail_5_Rings") rightTailRings[4] = child.gameObject;
        }
    }
    
    #endregion
}