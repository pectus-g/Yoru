using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class YukiHideAndSeek : MonoBehaviour 
{
    [Header("Character References")]
    [SerializeField] private Animator animator;
    [SerializeField] private GhostEffect3D ghostEffect;
    [SerializeField] private NavMeshAgent navMeshAgent;
    [SerializeField] private Transform player;
    
    [Header("Hide and Seek Settings")]
    [SerializeField] private Transform[] hidingSpots;
    [SerializeField] private float detectionRange = 8f;
    [SerializeField] private float hidingRange = 2f; // How close to hiding spot to be considered hidden
    [SerializeField] private float playerViewAngle = 60f; // Player's field of view
    [SerializeField] private LayerMask obstacleLayer = -1;
    
    [Header("Behavior Settings")]
    [SerializeField] private float minHideTime = 3f; // Minimum time to stay hidden
    [SerializeField] private float maxHideTime = 10f; // Maximum time before automatically relocating
    [SerializeField] private float disappearTime = 2f; // Time before respawning after being found
    [SerializeField] private float respawnDelay = 3f; // Delay before appearing in new location
    
    // Animation parameter names
    private const string ANIM_IS_RUNNING = "IsRunning";
    private const string ANIM_IS_HIDING = "IsHiding";
    private const string ANIM_IS_DISCOVERED = "IsDiscovered";
    private const string ANIM_SHOULD_DISAPPEAR = "ShouldDisappear";
    
    // State management
    public enum YukiState 
    {
        Idle,
        Running,
        Hiding,
        Discovered,
        Disappearing,
        Respawning
    }
    
    private YukiState currentState = YukiState.Idle;
    private Transform currentTargetSpot;
    private Vector3 lastPlayerPosition;
    private float hideTimer = 0f;
    private bool playerCanSeeYuki = false;
    
    void Start() 
    {
        SetupComponents();
        StartGame();
    }
    
    void SetupComponents() 
    {
        // Get components if not assigned
        if (animator == null) animator = GetComponent<Animator>();
        if (ghostEffect == null) ghostEffect = GetComponent<GhostEffect3D>();
        if (navMeshAgent == null) navMeshAgent = GetComponent<NavMeshAgent>();
        
        // Find player if not assigned
        if (player == null) 
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null) player = playerObj.transform;
        }
        
        // Find hiding spots if not assigned
        if (hidingSpots == null || hidingSpots.Length == 0) 
        {
            GameObject[] spotObjects = GameObject.FindGameObjectsWithTag("HidingSpot");
            hidingSpots = new Transform[spotObjects.Length];
            for (int i = 0; i < spotObjects.Length; i++) 
            {
                hidingSpots[i] = spotObjects[i].transform;
            }
        }
    }
    
    void StartGame() 
    {
        // Start by finding a hiding spot
        FindNewHidingSpot();
    }
    
    void Update() 
    {
        if (player == null) return;
        
        UpdatePlayerDetection();
        HandleCurrentState();
        UpdateAnimator();
    }
    
    void UpdatePlayerDetection() 
    {
        float distanceToPlayer = Vector3.Distance(transform.position, player.position);
        playerCanSeeYuki = CanPlayerSeeYuki(distanceToPlayer);
    }
    
    bool CanPlayerSeeYuki(float distance) 
    {
        if (distance > detectionRange) return false;
        
        // Check if player is looking at Yuki
        Vector3 directionToYuki = (transform.position - player.position).normalized;
        Vector3 playerForward = player.forward;
        
        float angle = Vector3.Angle(playerForward, directionToYuki);
        if (angle > playerViewAngle / 2f) return false;
        
        // Raycast to check for obstacles
        RaycastHit hit;
        if (Physics.Raycast(player.position + Vector3.up * 1.5f, directionToYuki, out hit, distance, obstacleLayer)) 
        {
            if (hit.transform != transform) return false; // Something is blocking the view
        }
        
        return true;
    }
    
    void HandleCurrentState() 
    {
        switch (currentState) 
        {
            case YukiState.Idle:
                HandleIdleState();
                break;
                
            case YukiState.Running:
                HandleRunningState();
                break;
                
            case YukiState.Hiding:
                HandleHidingState();
                break;
                
            case YukiState.Discovered:
                HandleDiscoveredState();
                break;
                
            case YukiState.Disappearing:
                HandleDisappearingState();
                break;
                
            case YukiState.Respawning:
                HandleRespawningState();
                break;
        }
    }
    
    void HandleIdleState() 
    {
        if (playerCanSeeYuki || Vector3.Distance(transform.position, player.position) < detectionRange) 
        {
            StartRunningToHide();
        }
    }
    
    void HandleRunningState() 
    {
        if (navMeshAgent.remainingDistance < 0.5f && !navMeshAgent.pathPending) 
        {
            StartHiding();
        }
        
        // If player can still see Yuki while running, find a new spot
        if (playerCanSeeYuki && Random.Range(0f, 1f) < 0.1f) // 10% chance per frame to change spot
        {
            FindNewHidingSpot();
        }
    }
    
    void HandleHidingState() 
    {
        hideTimer += Time.deltaTime;
        
        // Check if player found Yuki
        if (playerCanSeeYuki && Vector3.Distance(transform.position, player.position) < hidingRange) 
        {
            OnDiscovered();
            return;
        }
        
        // Automatically find new spot after max hide time
        if (hideTimer > maxHideTime) 
        {
            FindNewHidingSpot();
        }
    }
    
    void HandleDiscoveredState() 
    {
        // This state is handled by the OnDiscovered coroutine
    }
    
    void HandleDisappearingState() 
    {
        // This state is handled by the DisappearAndRespawn coroutine
    }
    
    void HandleRespawningState() 
    {
        // This state is handled by the DisappearAndRespawn coroutine
    }
    
    void StartRunningToHide() 
    {
        if (currentState == YukiState.Running) return;
        
        currentState = YukiState.Running;
        ghostEffect.BecomeVisible(); // Make sure Yuki is visible while running
        
        if (currentTargetSpot == null) 
        {
            FindNewHidingSpot();
        }
        
        navMeshAgent.SetDestination(currentTargetSpot.position);
    }
    
    void StartHiding() 
    {
        currentState = YukiState.Hiding;
        hideTimer = 0f;
        ghostEffect.BecomeGhost(); // Become semi-transparent when hiding
        navMeshAgent.ResetPath();
    }
    
    void FindNewHidingSpot() 
    {
        if (hidingSpots.Length == 0) return;
        
        // Find the furthest hiding spot from the player
        Transform bestSpot = null;
        float maxDistance = 0f;
        
        foreach (Transform spot in hidingSpots) 
        {
            float distance = Vector3.Distance(spot.position, player.position);
            if (distance > maxDistance && spot != currentTargetSpot) 
            {
                maxDistance = distance;
                bestSpot = spot;
            }
        }
        
        if (bestSpot != null) 
        {
            currentTargetSpot = bestSpot;
            if (currentState != YukiState.Running) 
            {
                StartRunningToHide();
            }
            else 
            {
                navMeshAgent.SetDestination(currentTargetSpot.position);
            }
        }
    }
    
    void OnDiscovered() 
    {
        if (currentState == YukiState.Discovered) return;
        
        currentState = YukiState.Discovered;
        ghostEffect.BecomeVisible(); // Make sure player can see the discovery
        navMeshAgent.ResetPath();
        
        StartCoroutine(DisappearAndRespawn());
    }
    
    IEnumerator DisappearAndRespawn() 
    {
        // Wait a moment for the discovery animation
        yield return new WaitForSeconds(disappearTime);
        
        // Start disappearing
        currentState = YukiState.Disappearing;
        ghostEffect.FadeOut();
        
        // Wait for fade out to complete
        yield return new WaitForSeconds(1f);
        
        // Respawn phase
        currentState = YukiState.Respawning;
        
        // Move to new location instantly
        FindBestRespawnSpot();
        if (currentTargetSpot != null) 
        {
            transform.position = currentTargetSpot.position;
        }
        
        // Wait before becoming visible again
        yield return new WaitForSeconds(respawnDelay);
        
        // Reappear and start the game again
        ghostEffect.BecomeGhost(); // Start as ghost in new hiding spot
        currentState = YukiState.Hiding;
        hideTimer = 0f;
    }
    
    void FindBestRespawnSpot() 
    {
        if (hidingSpots.Length == 0) return;
        
        // Find a spot that's far from player and not in line of sight
        Transform bestSpot = null;
        float bestScore = 0f;
        
        foreach (Transform spot in hidingSpots) 
        {
            float distance = Vector3.Distance(spot.position, player.position);
            bool inLineOfSight = CanPlayerSeePosition(spot.position);
            
            float score = distance;
            if (!inLineOfSight) score *= 2f; // Prefer spots out of sight
            
            if (score > bestScore) 
            {
                bestScore = score;
                bestSpot = spot;
            }
        }
        
        currentTargetSpot = bestSpot;
    }
    
    bool CanPlayerSeePosition(Vector3 position) 
    {
        Vector3 directionToPosition = (position - player.position).normalized;
        float angle = Vector3.Angle(player.forward, directionToPosition);
        
        if (angle > playerViewAngle / 2f) return false;
        
        RaycastHit hit;
        float distance = Vector3.Distance(position, player.position);
        
        return !Physics.Raycast(player.position + Vector3.up * 1.5f, directionToPosition, out hit, distance, obstacleLayer);
    }
    
    void UpdateAnimator() 
    {
        if (animator == null) return;
        
        // Update animator parameters based on current state
        animator.SetBool(ANIM_IS_RUNNING, currentState == YukiState.Running);
        animator.SetBool(ANIM_IS_HIDING, currentState == YukiState.Hiding);
        animator.SetBool(ANIM_IS_DISCOVERED, currentState == YukiState.Discovered);
        
        if (currentState == YukiState.Disappearing) 
        {
            animator.SetTrigger(ANIM_SHOULD_DISAPPEAR);
        }
    }
    
    // Public methods for external control
    public void ResetGame() 
    {
        currentState = YukiState.Idle;
        ghostEffect.BecomeVisible();
        hideTimer = 0f;
        FindNewHidingSpot();
    }
    
    public YukiState GetCurrentState() => currentState;
    public bool IsHidden => currentState == YukiState.Hiding && !playerCanSeeYuki;
    
    // Debug methods
    void OnDrawGizmosSelected() 
    {
        // Draw detection range (as a wire sphere)
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);
        
        // Draw hiding range (as a wire sphere)
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, hidingRange);
        
        // Draw line to current target
        if (currentTargetSpot != null) 
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawLine(transform.position, currentTargetSpot.position);
        }
        
        // Draw player's view direction
        if (player != null) 
        {
            Gizmos.color = playerCanSeeYuki ? Color.red : Color.white;
            Gizmos.DrawRay(player.position, player.forward * detectionRange);
        }
    }
}