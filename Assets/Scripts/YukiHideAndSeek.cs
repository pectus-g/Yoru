using System.Collections;
using UnityEngine;
using UnityEngine.AI;

public class YukiHideAndSeek : MonoBehaviour 
{
    [Header("Character References")]
    [SerializeField] private Animator animator;
    [SerializeField] private GhostEffect3D ghostEffect;
    [SerializeField] private NavMeshAgent navMeshAgent;
    [SerializeField] private Transform player;
    [SerializeField] private ParticleSystem fairyParticles;
    [SerializeField] private AudioSource audioSource;
    
    [Header("Hide and Seek Settings")]
    [SerializeField] private Transform[] hidingSpots;
    [SerializeField] private float detectionRange = 8f; // When Yuki starts running
    [SerializeField] private float foundRange = 2f; // When player finds Yuki
    [SerializeField] private float nearSpotDistance = 5f; // How far from hiding spot to appear
    
    [Header("Behavior Settings")]
    [SerializeField] private float fadeOutTime = 2f;
    [SerializeField] private float fadeInTime = 2f;
    [SerializeField] private float respawnDelay = 1f;
    [SerializeField] private float runSpeed = 5f;
    [SerializeField] private float walkSpeed = 3.5f;
    
    [Header("Fairy Effects")]
    [SerializeField] private float particleStartDelay = 0.3f;
    [SerializeField] private float particleDuration = 4f;
    [SerializeField] private AudioClip laughingSound;
    [SerializeField] private AudioClip screamSound; // NEW: Scream for devil spawn
    [SerializeField] private AudioClip backgroundMusic;
    
    [Header("Game Progression")]
    [SerializeField] private int maxHideSeekRounds = 3;
    [SerializeField] private GameObject devilToSpawn; // Changed from enemyToSpawn
    [SerializeField] private Transform devilSpawnPoint;
    
    // State management
    public enum YukiState 
    {
        WaitingNearSpot,    // NEW: Waiting near a hiding spot for player to approach
        RunningToHide,      // Running TO the hiding spot
        Hiding,             // At the hiding spot, ghostly
        FadingOut,          // Vanishing after being found
        Vanished,           // Invisible, teleporting
        FadingIn,           // Appearing near next spot
        FinalHiding,        // NEW: At final spot, waiting for devil spawn
        DevilSummoned       // NEW: Devil has appeared
    }
    
    private YukiState currentState = YukiState.FadingIn; // Start by appearing near first spot
    private Transform currentHidingSpot;
    private int currentSpotIndex = 0;
    private int timesFound = 0;
    private bool backgroundMusicPlaying = false;
    
    void Start() 
    {
        SetupComponents();
        StartGame();
    }
    
    void SetupComponents() 
    {
        if (animator == null) animator = GetComponent<Animator>();
        if (ghostEffect == null) ghostEffect = GetComponent<GhostEffect3D>();
        if (navMeshAgent == null) navMeshAgent = GetComponent<NavMeshAgent>();
        if (audioSource == null) audioSource = GetComponent<AudioSource>();
        
        if (player == null) 
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null) player = playerObj.transform;
        }
        
        if (hidingSpots == null || hidingSpots.Length == 0) 
        {
            GameObject[] spotObjects = GameObject.FindGameObjectsWithTag("HidingSpot");
            hidingSpots = new Transform[spotObjects.Length];
            for (int i = 0; i < spotObjects.Length; i++) 
            {
                hidingSpots[i] = spotObjects[i].transform;
            }
        }
        
        if (fairyParticles != null) fairyParticles.Stop();
        
        navMeshAgent.speed = walkSpeed;
        Debug.Log($"Yuki Setup: Found {hidingSpots.Length} hiding spots");
    }
    
    void StartGame() 
    {
        currentSpotIndex = 0;
        timesFound = 0;
        
        if (hidingSpots.Length > 0) 
        {
            currentHidingSpot = hidingSpots[currentSpotIndex];
        }
        
        PlayBackgroundMusic();
        
        // Start by appearing near first hiding spot
        StartCoroutine(AppearNearSpot());
        
        Debug.Log($"=== GAME STARTED === Will appear near Element {currentSpotIndex}");
    }
    
    void Update() 
    {
        if (player == null) return;
        
        float distanceToPlayer = Vector3.Distance(transform.position, player.position);
        
        switch (currentState) 
        {
            case YukiState.WaitingNearSpot:
                HandleWaitingNearSpot(distanceToPlayer);
                break;
                
            case YukiState.RunningToHide:
                HandleRunningToHide(distanceToPlayer);
                break;
                
            case YukiState.Hiding:
                HandleHiding(distanceToPlayer);
                break;
                
            case YukiState.FinalHiding:
                HandleFinalHiding(distanceToPlayer);
                break;
        }
        
        UpdateAnimator();
    }
    
    void HandleWaitingNearSpot(float distanceToPlayer) 
    {
        // When player gets close, start running to the actual hiding spot
        if (distanceToPlayer <= detectionRange) 
        {
            StartRunningToActualSpot();
        }
    }
    
    void HandleRunningToHide(float distanceToPlayer) 
    {
        // Check if reached hiding spot
        if (navMeshAgent.enabled && navMeshAgent.remainingDistance < 1f && !navMeshAgent.pathPending) 
        {
            ReachedHidingSpot();
        }
    }
    
    void HandleHiding(float distanceToPlayer) 
    {
        // Check if player found Yuki
        if (distanceToPlayer <= foundRange) 
        {
            OnPlayerFoundYuki();
        }
    }
    
    void HandleFinalHiding(float distanceToPlayer) 
    {
        // Check if player found Yuki at final spot
        if (distanceToPlayer <= foundRange) 
        {
            OnFinalDiscovery();
        }
    }
    
    void StartRunningToActualSpot() 
    {
        currentState = YukiState.RunningToHide;
        navMeshAgent.speed = runSpeed;
        navMeshAgent.enabled = true;
        
        if (currentHidingSpot != null) 
        {
            navMeshAgent.SetDestination(currentHidingSpot.position);
            Debug.Log($"=== YUKI RUNNING TO SPOT === Element {currentSpotIndex}: {currentHidingSpot.name}");
        }
    }
    
    void ReachedHidingSpot() 
    {
        // Check if this is the final spot
        if (currentSpotIndex >= maxHideSeekRounds - 1) 
        {
            // Final spot - stay here and wait
            currentState = YukiState.FinalHiding;
            navMeshAgent.ResetPath();
            ghostEffect.BecomeGhost(); // Become ghostly but don't vanish
            Debug.Log($"=== REACHED FINAL SPOT === Element {currentSpotIndex}, waiting for discovery");
        }
        else 
        {
            // Normal spot - become ghostly and wait to be found
            currentState = YukiState.Hiding;
            navMeshAgent.ResetPath();
            ghostEffect.BecomeGhost();
            Debug.Log($"=== REACHED HIDING SPOT === Element {currentSpotIndex}, now hiding (ghostly)");
        }
    }
    
    void OnPlayerFoundYuki() 
    {
        if (currentState != YukiState.Hiding) return;
        
        timesFound++;
        Debug.Log($"=== FOUND AT ELEMENT {currentSpotIndex} === Round {timesFound}/{maxHideSeekRounds}");
        
        // Vanish and move to near next spot
        StartCoroutine(VanishAndMoveToNextArea());
    }
    
    void OnFinalDiscovery() 
    {
        if (currentState != YukiState.FinalHiding) return;
        
        timesFound++;
        currentState = YukiState.DevilSummoned;
        
        Debug.Log($"=== FINAL DISCOVERY === Summoning devil!");
        
        // Play laugh then scream, then spawn devil
        StartCoroutine(DevilSummonSequence());
    }
    
    IEnumerator VanishAndMoveToNextArea() 
    {
        currentState = YukiState.FadingOut;
        
        // Play laughing sound
        if (audioSource != null && laughingSound != null) 
        {
            audioSource.PlayOneShot(laughingSound);
        }
        
        // Start vanish with particles
        yield return new WaitForSeconds(particleStartDelay);
        PlayAngelicParticles();
        StartCoroutine(AngelicFadeOut());
        
        // Wait for fade to complete
        yield return new WaitForSeconds(fadeOutTime - particleStartDelay);
        
        currentState = YukiState.Vanished;
        
        // Move to next spot area
        currentSpotIndex++;
        yield return new WaitForSeconds(respawnDelay);
        
        // Appear near next spot
        StartCoroutine(AppearNearSpot());
    }
    
    IEnumerator AppearNearSpot() 
    {
        currentState = YukiState.FadingIn;
        
        if (currentSpotIndex < hidingSpots.Length) 
        {
            currentHidingSpot = hidingSpots[currentSpotIndex];
            
            // Find a position NEAR the hiding spot (not at it)
            Vector3 nearPosition = GetPositionNearSpot(currentHidingSpot.position);
            transform.position = nearPosition;
            
            // Make sure Yuki is visible (not ghostly) when appearing near spot
            ghostEffect.SetAlphaImmediate(0f); // Start invisible
            
            Debug.Log($"=== APPEARING NEAR ELEMENT {currentSpotIndex} === Distance {Vector3.Distance(nearPosition, currentHidingSpot.position):F1}m from {currentHidingSpot.name}");
            Debug.Log($"=== POSITIONS === Yuki: {transform.position}, Hiding Spot: {currentHidingSpot.position}");
            
            // Play appear particles
            PlayAngelicParticles();
            StartCoroutine(AngelicFadeIn());
            
            yield return new WaitForSeconds(fadeInTime);
            
            // Now waiting near the spot for player to approach
            currentState = YukiState.WaitingNearSpot;
            Debug.Log($"=== NOW WAITING === Near Element {currentSpotIndex} for player to get close (detection range: {detectionRange})");
        }
        else 
        {
            Debug.LogError($"=== ERROR === CurrentSpotIndex {currentSpotIndex} is beyond hidingSpots length {hidingSpots.Length}");
        }
    }
    
    Vector3 GetPositionNearSpot(Vector3 spotPosition) 
    {
        // Instead of random, use a fixed offset from the hiding spot toward the player
        Vector3 directionToPlayer = (player.position - spotPosition).normalized;
        
        // Place Yuki between the player and the hiding spot, at nearSpotDistance from the spot
        Vector3 nearPosition = spotPosition + (directionToPlayer * nearSpotDistance);
        
        // Ensure Y position is same as hiding spot (avoid floating/underground)
        nearPosition.y = spotPosition.y;
        
        // Try to find valid NavMesh position near this point
        UnityEngine.AI.NavMeshHit navHit;
        if (UnityEngine.AI.NavMesh.SamplePosition(nearPosition, out navHit, nearSpotDistance * 2f, UnityEngine.AI.NavMesh.AllAreas))
        {
            nearPosition = navHit.position;
        }
        else
        {
            // Fallback: use the hiding spot position itself if no valid nearby position found
            Debug.LogWarning($"Could not find valid near position for {currentHidingSpot.name}, using spot position");
            nearPosition = spotPosition;
        }
        
        return nearPosition;
    }
    
    IEnumerator DevilSummonSequence() 
    {
        Debug.Log("=== DEVIL SUMMON SEQUENCE === Starting...");
        
        // First: Laugh
        if (audioSource != null && laughingSound != null) 
        {
            audioSource.PlayOneShot(laughingSound);
            yield return new WaitForSeconds(laughingSound.length);
        }
        
        // Then: Scream
        if (audioSource != null && screamSound != null) 
        {
            audioSource.PlayOneShot(screamSound);
        }
        
        // Spawn devil
        if (devilToSpawn != null && devilSpawnPoint != null) 
        {
            GameObject spawnedDevil = Instantiate(devilToSpawn, devilSpawnPoint.position, devilSpawnPoint.rotation);
            Debug.Log($"=== DEVIL SPAWNED === At {devilSpawnPoint.name}!");
        }
        
        // Play dramatic particles
        PlayAngelicParticles();
        
        // Stop background music (optional - create tension)
        if (audioSource != null && audioSource.isPlaying) 
        {
            audioSource.Stop();
        }
        
        Debug.Log("=== GAME PHASE COMPLETE === Devil has been summoned!");
    }
    
    // Particle and fade effects (same as before)
    IEnumerator AngelicFadeOut() 
    {
        float elapsedTime = 0f;
        float startAlpha = ghostEffect.CurrentAlpha;
        
        while (elapsedTime < fadeOutTime) 
        {
            elapsedTime += Time.deltaTime;
            float progress = elapsedTime / fadeOutTime;
            float curve = Mathf.Sin(progress * Mathf.PI * 0.5f);
            float currentAlpha = Mathf.Lerp(startAlpha, 0f, curve);
            ghostEffect.SetAlphaImmediate(currentAlpha);
            yield return null;
        }
        
        ghostEffect.SetAlphaImmediate(0f);
    }
    
    IEnumerator AngelicFadeIn() 
    {
        float elapsedTime = 0f;
        
        while (elapsedTime < fadeInTime) 
        {
            elapsedTime += Time.deltaTime;
            float progress = elapsedTime / fadeInTime;
            float curve = Mathf.Sin(progress * Mathf.PI * 0.5f);
            float currentAlpha = Mathf.Lerp(0f, 0.3f, curve);
            ghostEffect.SetAlphaImmediate(currentAlpha);
            yield return null;
        }
        
        ghostEffect.SetAlphaImmediate(0.3f);
    }
    
    void PlayAngelicParticles() 
    {
        if (fairyParticles != null) 
        {
            fairyParticles.transform.position = transform.position + Vector3.up * 1f;
            fairyParticles.Play();
            StartCoroutine(StopParticlesAfterDelay());
        }
    }
    
    IEnumerator StopParticlesAfterDelay() 
    {
        yield return new WaitForSeconds(particleDuration);
        if (fairyParticles != null) fairyParticles.Stop();
    }
    
    void PlayBackgroundMusic() 
    {
        if (audioSource != null && backgroundMusic != null && !backgroundMusicPlaying) 
        {
            audioSource.clip = backgroundMusic;
            audioSource.loop = true;
            audioSource.volume = 0.3f;
            audioSource.Play();
            backgroundMusicPlaying = true;
        }
    }
    
    void UpdateAnimator() 
    {
        if (animator == null) return;
        
        // Reset all animation bools
        animator.SetBool("IsAlert", false);
        animator.SetBool("IsLookAround", false);
        animator.SetBool("IsRunning", false);
        
        // Set correct animation based on current state
        switch (currentState) 
        {
            case YukiState.WaitingNearSpot:
                // Use Alert animation - waiting for player to get close
                animator.SetBool("IsAlert", true);
                Debug.Log("=== ANIMATION === Alert (waiting for player approach)");
                break;
                
            case YukiState.RunningToHide:
                // Use Running animation - moving to hiding spot
                animator.SetBool("IsRunning", true);
                Debug.Log("=== ANIMATION === Running (to hiding spot)");
                break;
                
            case YukiState.Hiding:
            case YukiState.FinalHiding:
                // Use Look Around animation - hiding and waiting to be found
                animator.SetBool("IsLookAround", true);
                Debug.Log("=== ANIMATION === Look Around (waiting to be found)");
                break;
                
            case YukiState.FadingOut:
                // Keep current animation while fading out
                Debug.Log("=== ANIMATION === Fading out (keep current pose)");
                break;
                
            case YukiState.FadingIn:
            case YukiState.Vanished:
            case YukiState.DevilSummoned:
                // Use Idle for these states
                Debug.Log("=== ANIMATION === Idle");
                break;
        }
    }
    
    // Public methods
    public void ResetGame() 
    {
        StopAllCoroutines();
        currentSpotIndex = 0;
        timesFound = 0;
        navMeshAgent.enabled = true;
        StartGame();
    }
    
    public YukiState GetCurrentState() => currentState;
    public bool IsDevilSummoned => currentState == YukiState.DevilSummoned;
    public int GetTimesFound() => timesFound;
    public int GetCurrentSpotIndex() => currentSpotIndex;
    
    // Debug visualization
    void OnDrawGizmosSelected() 
    {
        // Draw detection range
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);
        
        // Draw found range
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, foundRange);
        
        // Draw line to current hiding spot
        if (currentHidingSpot != null) 
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(transform.position, currentHidingSpot.position);
        }
        
        // Draw near-spot range around hiding spots
        if (hidingSpots != null) 
        {
            for (int i = 0; i < hidingSpots.Length; i++) 
            {
                Transform spot = hidingSpots[i];
                if (spot != null) 
                {
                    if (i == currentSpotIndex) 
                        Gizmos.color = Color.green;
                    else if (i < currentSpotIndex) 
                        Gizmos.color = Color.gray;
                    else 
                        Gizmos.color = Color.white;
                        
                    Gizmos.DrawWireCube(spot.position, Vector3.one * 0.8f);
                    
                    // Draw "near spot" area
                    Gizmos.color = Color.magenta;
                    Gizmos.DrawWireSphere(spot.position, nearSpotDistance);
                }
            }
        }
        
        #if UNITY_EDITOR
        UnityEditor.Handles.Label(transform.position + Vector3.up * 2f, 
            $"Element: {currentSpotIndex}/{hidingSpots?.Length - 1}\nRound: {timesFound}/{maxHideSeekRounds}\nState: {currentState}");
        #endif
    }
}