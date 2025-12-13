using UnityEngine;
using System.Collections.Generic;

namespace Yoru.BalanceSystem
{
    /// <summary>
    /// RingMeshController - Handles Tail Ring Mesh Toggling
    /// 
    /// Yoru's character has multiple mesh variants for each tail showing different ring counts.
    /// This script toggles the correct mesh based on the current ring count.
    /// 
    /// HIERARCHY STRUCTURE:
    /// PlayerYoru_Dec12 (Tag: "Player")
    /// └── Cat_All_10_Tails_v3
    ///     ├── LeftTail_NoRings      ← Active at game start
    ///     ├── LeftTail_1_Ring
    ///     ├── LeftTail_2_Rings
    ///     ├── ... (up to LeftTail_10_Rings)
    ///     ├── RightTail_NoRings     ← Active at game start
    ///     ├── RightTail_1_Ring
    ///     ├── RightTail_2_Rings
    ///     ├── ... (up to RightTail_10_Rings)
    ///     └── model:Cat_BODY
    /// 
    /// TOGGLE LOGIC:
    /// - Only ONE mesh per tail is active at any time
    /// - Getting 1st left ring: Disable LeftTail_NoRings, Enable LeftTail_1_Ring
    /// - Getting 2nd left ring: Disable LeftTail_1_Ring, Enable LeftTail_2_Rings
    /// </summary>
    public class RingMeshController : MonoBehaviour
    {
        #region Inspector Fields
        [Header("=== PLAYER REFERENCE ===")]
        [Tooltip("The player's root GameObject. Will auto-find by 'Player' tag if not set.")]
        [SerializeField] private GameObject playerRoot;
        
        [Tooltip("Name of the child containing all tail meshes")]
        [SerializeField] private string tailContainerName = "Cat_All_10_Tails_v3";

        [Header("=== MESH NAMING CONVENTIONS ===")]
        [Tooltip("Base name for left tail meshes (before number)")]
        [SerializeField] private string leftTailPrefix = "LeftTail_";
        
        [Tooltip("Base name for right tail meshes (before number)")]
        [SerializeField] private string rightTailPrefix = "RightTail_";
        
        [Tooltip("Name suffix for no-rings variant")]
        [SerializeField] private string noRingsSuffix = "NoRings";
        
        [Tooltip("Name suffix for single ring variant")]
        [SerializeField] private string singleRingSuffix = "_Ring";
        
        [Tooltip("Name suffix for multiple rings variant")]
        [SerializeField] private string multipleRingsSuffix = "_Rings";

        [Header("=== DEBUG ===")]
        [SerializeField] private bool enableDebugLogs = true;
        [SerializeField] private bool autoFindOnStart = true;
        #endregion

        #region Private Fields
        private Transform tailContainer;
        private Dictionary<int, GameObject> leftTailMeshes = new Dictionary<int, GameObject>();
        private Dictionary<int, GameObject> rightTailMeshes = new Dictionary<int, GameObject>();
        private int currentLeftRings = 0;
        private int currentRightRings = 0;
        private bool isInitialized = false;
        #endregion

        #region Unity Lifecycle
        private void Start()
        {
            if (autoFindOnStart)
            {
                Initialize();
            }
            
            // Subscribe to WorldStateManager events
            if (WorldStateManager.Instance != null)
            {
                WorldStateManager.Instance.OnRingsChanged.AddListener(OnRingsChanged);
                
                // Initialize to current state
                OnRingsChanged(WorldStateManager.Instance.LeftRings, WorldStateManager.Instance.RightRings);
            }
        }

        private void OnDestroy()
        {
            if (WorldStateManager.Instance != null)
            {
                WorldStateManager.Instance.OnRingsChanged.RemoveListener(OnRingsChanged);
            }
        }
        #endregion

        #region Initialization
        public bool Initialize()
        {
            if (isInitialized) return true;
            
            // Find player if not assigned
            if (playerRoot == null)
            {
                playerRoot = GameObject.FindGameObjectWithTag("Player");
                
                if (playerRoot == null)
                {
                    Debug.LogError("[RingMeshController] Could not find player with tag 'Player'!");
                    return false;
                }
            }
            
            if (enableDebugLogs)
                Debug.Log($"[RingMeshController] Found player: {playerRoot.name}");
            
            // Find tail container
            tailContainer = FindDeepChild(playerRoot.transform, tailContainerName);
            
            if (tailContainer == null)
            {
                Debug.LogError($"[RingMeshController] Could not find tail container: {tailContainerName}");
                return false;
            }
            
            if (enableDebugLogs)
                Debug.Log($"[RingMeshController] Found tail container: {tailContainer.name}");
            
            // Find and cache all tail meshes
            CacheTailMeshes();
            
            isInitialized = true;
            
            // Set initial state (no rings)
            SetLeftTailMesh(0);
            SetRightTailMesh(0);
            
            return true;
        }

        private void CacheTailMeshes()
        {
            leftTailMeshes.Clear();
            rightTailMeshes.Clear();
            
            foreach (Transform child in tailContainer)
            {
                string childName = child.name;
                
                // Check for LEFT tail meshes
                if (childName.StartsWith(leftTailPrefix))
                {
                    int ringCount = ParseRingCount(childName, leftTailPrefix);
                    if (ringCount >= 0)
                    {
                        leftTailMeshes[ringCount] = child.gameObject;
                        if (enableDebugLogs)
                            Debug.Log($"[RingMeshController] Cached LEFT mesh: {childName} → {ringCount} rings");
                    }
                }
                // Check for RIGHT tail meshes
                else if (childName.StartsWith(rightTailPrefix))
                {
                    int ringCount = ParseRingCount(childName, rightTailPrefix);
                    if (ringCount >= 0)
                    {
                        rightTailMeshes[ringCount] = child.gameObject;
                        if (enableDebugLogs)
                            Debug.Log($"[RingMeshController] Cached RIGHT mesh: {childName} → {ringCount} rings");
                    }
                }
            }
            
            Debug.Log($"[RingMeshController] Cached {leftTailMeshes.Count} LEFT and {rightTailMeshes.Count} RIGHT tail meshes");
        }

        private int ParseRingCount(string meshName, string prefix)
        {
            string remainder = meshName.Substring(prefix.Length);
            
            // Check for "NoRings"
            if (remainder == noRingsSuffix)
                return 0;
            
            // Check for single ring "1_Ring" or multiple rings "2_Rings"
            // Try to extract number from strings like "1_Ring", "2_Rings", "10_Rings"
            if (remainder.Contains("_Ring"))
            {
                string numberPart = remainder.Split('_')[0];
                if (int.TryParse(numberPart, out int count))
                {
                    return count;
                }
            }
            
            return -1; // Invalid format
        }
        #endregion

        #region Mesh Toggling
        private void OnRingsChanged(int leftRings, int rightRings)
        {
            if (!isInitialized)
            {
                if (!Initialize()) return;
            }
            
            if (leftRings != currentLeftRings)
            {
                SetLeftTailMesh(leftRings);
                currentLeftRings = leftRings;
            }
            
            if (rightRings != currentRightRings)
            {
                SetRightTailMesh(rightRings);
                currentRightRings = rightRings;
            }
        }

        private void SetLeftTailMesh(int ringCount)
        {
            // Disable all left tail meshes
            foreach (var kvp in leftTailMeshes)
            {
                kvp.Value.SetActive(false);
            }
            
            // Enable the correct one
            if (leftTailMeshes.TryGetValue(ringCount, out GameObject targetMesh))
            {
                targetMesh.SetActive(true);
                
                if (enableDebugLogs)
                    Debug.Log($"[RingMeshController] LEFT tail set to {ringCount} rings: {targetMesh.name}");
            }
            else
            {
                Debug.LogWarning($"[RingMeshController] Could not find LEFT tail mesh for {ringCount} rings!");
            }
        }

        private void SetRightTailMesh(int ringCount)
        {
            // Disable all right tail meshes
            foreach (var kvp in rightTailMeshes)
            {
                kvp.Value.SetActive(false);
            }
            
            // Enable the correct one
            if (rightTailMeshes.TryGetValue(ringCount, out GameObject targetMesh))
            {
                targetMesh.SetActive(true);
                
                if (enableDebugLogs)
                    Debug.Log($"[RingMeshController] RIGHT tail set to {ringCount} rings: {targetMesh.name}");
            }
            else
            {
                Debug.LogWarning($"[RingMeshController] Could not find RIGHT tail mesh for {ringCount} rings!");
            }
        }
        #endregion

        #region Utility Methods
        /// <summary>
        /// Find a child by name, searching through all descendants
        /// </summary>
        private Transform FindDeepChild(Transform parent, string childName)
        {
            foreach (Transform child in parent)
            {
                if (child.name == childName)
                    return child;
                
                Transform found = FindDeepChild(child, childName);
                if (found != null)
                    return found;
            }
            return null;
        }

        /// <summary>
        /// Manually set ring counts (for testing)
        /// </summary>
        public void SetRings(int left, int right)
        {
            OnRingsChanged(left, right);
        }

        /// <summary>
        /// Get list of all found mesh names (for debugging)
        /// </summary>
        public List<string> GetFoundMeshNames()
        {
            List<string> names = new List<string>();
            
            foreach (var kvp in leftTailMeshes)
                names.Add($"LEFT[{kvp.Key}]: {kvp.Value.name}");
            
            foreach (var kvp in rightTailMeshes)
                names.Add($"RIGHT[{kvp.Key}]: {kvp.Value.name}");
            
            return names;
        }

        /// <summary>
        /// Log all found meshes to console
        /// </summary>
        [ContextMenu("Log Found Meshes")]
        public void LogFoundMeshes()
        {
            Debug.Log("=== RING MESH CONTROLLER - FOUND MESHES ===");
            foreach (string name in GetFoundMeshNames())
            {
                Debug.Log(name);
            }
        }
        #endregion

        #region Editor Validation
        #if UNITY_EDITOR
        private void OnValidate()
        {
            // Ensure prefixes end with underscore
            if (!string.IsNullOrEmpty(leftTailPrefix) && !leftTailPrefix.EndsWith("_"))
                leftTailPrefix += "_";
            
            if (!string.IsNullOrEmpty(rightTailPrefix) && !rightTailPrefix.EndsWith("_"))
                rightTailPrefix += "_";
        }
        #endif
        #endregion
    }
}