using UnityEngine;

/// <summary>
/// Controls the visual ring meshes on Yoru's tails.
/// 
/// Each tail can have 0-10 rings:
/// - Left tail: LeftTail_NoRings, LeftTail_1_Ring, ..., LeftTail_10_Rings
/// - Right tail: RightTail_NoRings, RightTail_1_Ring, ..., RightTail_10_Rings
/// 
/// Performance: 
/// - References cached at Start
/// - Only toggles 2 objects per change (old off, new on)
/// - No per-frame updates
/// </summary>
public class RingMeshController : MonoBehaviour
{
    #region Serialized Fields
    
    [Header("Left Tail Meshes (Dark/Condemn) - 0 to 10 rings")]
    [SerializeField] private GameObject leftTail_NoRings;
    [SerializeField] private GameObject[] leftTailRings = new GameObject[10];
    
    [Header("Right Tail Meshes (Light/Forgive) - 0 to 10 rings")]
    [SerializeField] private GameObject rightTail_NoRings;
    [SerializeField] private GameObject[] rightTailRings = new GameObject[10];
    
    [Header("Setup")]
    [SerializeField] private bool autoFindOnStart = true;
    [SerializeField] private Transform meshParent;
    
    #endregion
    
    #region Private State
    
    private int currentLeftCount = -1;
    private int currentRightCount = -1;
    
    #endregion
    
    #region Unity Lifecycle
    
    private void Start()
    {
        if (autoFindOnStart && !HasAllReferences())
            AutoFindMeshes();
        
        ValidateReferences();
        
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
        if (leftCount != currentLeftCount)
            UpdateLeftTail(leftCount);
        
        if (rightCount != currentRightCount)
            UpdateRightTail(rightCount);
    }
    
    #endregion
    
    #region Mesh Updates
    
    private void UpdateLeftTail(int newCount)
    {
        if (currentLeftCount >= 0)
        {
            GameObject oldMesh = currentLeftCount == 0 ? leftTail_NoRings : GetLeftMesh(currentLeftCount);
            if (oldMesh != null) oldMesh.SetActive(false);
        }
        
        GameObject newMesh = newCount == 0 ? leftTail_NoRings : GetLeftMesh(newCount);
        if (newMesh != null) newMesh.SetActive(true);
        
        currentLeftCount = newCount;
    }
    
    private void UpdateRightTail(int newCount)
    {
        if (currentRightCount >= 0)
        {
            GameObject oldMesh = currentRightCount == 0 ? rightTail_NoRings : GetRightMesh(currentRightCount);
            if (oldMesh != null) oldMesh.SetActive(false);
        }
        
        GameObject newMesh = newCount == 0 ? rightTail_NoRings : GetRightMesh(newCount);
        if (newMesh != null) newMesh.SetActive(true);
        
        currentRightCount = newCount;
    }
    
    private GameObject GetLeftMesh(int ringCount)
    {
        if (ringCount < 1 || ringCount > 10) return null;
        return leftTailRings[ringCount - 1];
    }
    
    private GameObject GetRightMesh(int ringCount)
    {
        if (ringCount < 1 || ringCount > 10) return null;
        return rightTailRings[ringCount - 1];
    }
    
    #endregion
    
    #region Setup Helpers
    
    private bool HasAllReferences()
    {
        if (leftTail_NoRings == null || rightTail_NoRings == null)
            return false;
        
        for (int i = 0; i < 10; i++)
        {
            if (leftTailRings[i] == null || rightTailRings[i] == null)
                return false;
        }
        return true;
    }
    
    private void ValidateReferences()
    {
        int leftCount = 0, rightCount = 0;
        
        for (int i = 0; i < 10; i++)
        {
            if (leftTailRings[i] != null) leftCount++;
            if (rightTailRings[i] != null) rightCount++;
        }
        
        if (leftTail_NoRings == null || rightTail_NoRings == null)
            Debug.LogWarning("[RingMeshController] Missing NoRings meshes");
        
        Debug.Log($"[RingMeshController] Found {leftCount}/10 left, {rightCount}/10 right tail meshes");
    }
    
    private void AutoFindMeshes()
    {
        if (meshParent == null)
        {
            var player = GameObject.FindWithTag("Player");
            if (player != null)
                meshParent = player.transform.Find("Cat_All_10_Tails_v3");
        }
        
        if (meshParent == null) return;
        
        var allChildren = meshParent.GetComponentsInChildren<Transform>(true);
        
        foreach (var child in allChildren)
        {
            string name = child.name;
            
            // Left tail
            if (name == "LeftTail_NoRings") leftTail_NoRings = child.gameObject;
            else if (name.StartsWith("LeftTail_") && name.Contains("Ring"))
            {
                int ringNum = ExtractRingNumber(name);
                if (ringNum >= 1 && ringNum <= 10)
                    leftTailRings[ringNum - 1] = child.gameObject;
            }
            // Right tail
            else if (name == "RightTail_NoRings") rightTail_NoRings = child.gameObject;
            else if (name.StartsWith("RightTail_") && name.Contains("Ring"))
            {
                int ringNum = ExtractRingNumber(name);
                if (ringNum >= 1 && ringNum <= 10)
                    rightTailRings[ringNum - 1] = child.gameObject;
            }
        }
    }
    
    private int ExtractRingNumber(string name)
    {
        // Extract number from strings like "LeftTail_5_Rings" or "RightTail_10_Rings"
        string[] parts = name.Split('_');
        foreach (string part in parts)
        {
            if (int.TryParse(part, out int num))
                return num;
        }
        return -1;
    }
    
    #endregion
}