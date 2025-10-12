using UnityEngine;

public class EnemyCombat : MonoBehaviour
{
    // Enum definition (no Header attribute allowed here)
    public enum EnemyState
    {
        LostSoul,      // Confused, looking around (pre-dialogue)
        Dialogue,      // Talking to player (frozen)
        Hostile,       // Attacking player (post dark choice)
        Peaceful,      // Passing on (post light choice)
        Dead           // Defeated
    }
    
    [Header("Enemy State")]
    [SerializeField] private EnemyState currentState = EnemyState.LostSoul;
    
    [Header("Detection")]
    [SerializeField] private float detectionRange = 10f;
    [SerializeField] private float attackRange = 2.5f;
    
    [Header("Attack Settings")]
    [SerializeField] private int punchDamage = 10;
    [SerializeField] private int kickDamage = 15;
    [SerializeField] private float attackCooldown = 2f;
    private float attackTimer = 0f;
    
    [Header("Movement")]
    [SerializeField] private float walkSpeed = 1.5f;
    [SerializeField] private float runSpeed = 4f;
    [SerializeField] private float rotationSpeed = 5f;
    
    [Header("Lost Soul Behavior")]
    [SerializeField] private float lookAroundInterval = 5f;
    private float lookAroundTimer = 0f;
    
    private Transform player;
    private EnemyHealth enemyHealth;
    private Animator animator;
    private bool isAttacking = false;
    
    void Start()
    {
        // Find player
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            player = playerObj.transform;
            Debug.Log($"{gameObject.name} found player at {player.position}");
        }
        else
        {
            Debug.LogError($"{gameObject.name} could not find Player! Make sure Player has 'Player' tag!");
        }
        
        enemyHealth = GetComponent<EnemyHealth>();
        animator = GetComponent<Animator>();
        
        // Start as confused lost soul
        SetState(EnemyState.LostSoul);
        
        Debug.Log($"{gameObject.name} initialized. Detection Range: {detectionRange}, Attack Range: {attackRange}");
    }
    
    void Update()
    {
        // ========== DEBUG KEYS (FOR TESTING) ==========
        
        // Press T to make enemy hostile
        if (Input.GetKeyDown(KeyCode.T))
        {
            SetState(EnemyState.Hostile);
            Debug.Log($"[DEBUG] {gameObject.name} forced to HOSTILE state!");
        }
        
        // Press Y to test walk animation
        if (Input.GetKeyDown(KeyCode.Y))
        {
            if (animator != null)
            {
                animator.SetBool("IsWalking", true);
                animator.SetBool("IsRunning", false);
            }
            Debug.Log($"[DEBUG] Testing WALK animation");
        }
        
        // Press U to test run animation
        if (Input.GetKeyDown(KeyCode.U))
        {
            if (animator != null)
            {
                animator.SetBool("IsWalking", false);
                animator.SetBool("IsRunning", true);
            }
            Debug.Log($"[DEBUG] Testing RUN animation");
        }
        
        // Press I to test idle animation
        if (Input.GetKeyDown(KeyCode.I))
        {
            if (animator != null)
            {
                animator.SetBool("IsWalking", false);
                animator.SetBool("IsRunning", false);
            }
            Debug.Log($"[DEBUG] Testing IDLE animation");
        }
        
        // Press P to show current distance to player
        if (Input.GetKeyDown(KeyCode.P))
        {
            if (player != null)
            {
                float dist = Vector3.Distance(transform.position, player.position);
                Debug.Log($"[DEBUG] Distance to player: {dist:F2} units | Detection: {detectionRange} | Attack: {attackRange}");
            }
        }
        
        // ========== END DEBUG KEYS ==========
        
        // Don't do anything if dead
        if (enemyHealth != null && enemyHealth.IsDead())
        {
            SetState(EnemyState.Dead);
            return;
        }
        
        // Update attack cooldown
        if (attackTimer > 0)
        {
            attackTimer -= Time.deltaTime;
        }
        
        // Run current state behavior
        switch (currentState)
        {
            case EnemyState.LostSoul:
                HandleLostSoulState();
                break;
                
            case EnemyState.Dialogue:
                HandleDialogueState();
                break;
                
            case EnemyState.Hostile:
                HandleHostileState();
                break;
                
            case EnemyState.Peaceful:
                HandlePeacefulState();
                break;
                
            case EnemyState.Dead:
                HandleDeadState();
                break;
        }
    }
    
    void HandleLostSoulState()
    {
        // Confused soul looking around
        if (animator != null)
        {
            animator.SetBool("IsWalking", false);
            animator.SetBool("IsRunning", false);
        }
        
        // Occasionally look around confused
        lookAroundTimer -= Time.deltaTime;
        if (lookAroundTimer <= 0)
        {
            // We don't have LookAround trigger in your animator yet
            // Just reset timer for now
            lookAroundTimer = lookAroundInterval;
        }
        
        // Note: Will transition to Dialogue state when player presses E
        // That will be handled by dialogue system later
    }
    
    void HandleDialogueState()
    {
        // Frozen during dialogue - do nothing
        if (animator != null)
        {
            animator.SetBool("IsWalking", false);
            animator.SetBool("IsRunning", false);
        }
    }
    
    void HandleHostileState()
    {
        if (player == null)
        {
            Debug.LogWarning($"{gameObject.name} is hostile but player is null!");
            return;
        }
        
        float distanceToPlayer = Vector3.Distance(transform.position, player.position);
        
        // Debug: Log distance occasionally (every 60 frames = ~1 second)
        if (Time.frameCount % 60 == 0)
        {
            Debug.Log($"{gameObject.name} HOSTILE: Distance={distanceToPlayer:F2}, InDetection={distanceToPlayer <= detectionRange}, InAttack={distanceToPlayer <= attackRange}");
        }
        
        // Look at player
        LookAtPlayer();
        
        // Check if in attack range
        if (distanceToPlayer <= attackRange)
        {
            // Stop and attack
            if (animator != null)
            {
                animator.SetBool("IsWalking", false);
                animator.SetBool("IsRunning", false);
            }
            
            if (attackTimer <= 0 && !isAttacking)
            {
                StartCoroutine(AttackPlayer());
            }
        }
        else if (distanceToPlayer <= detectionRange)
        {
            // Run toward player
            ChasePlayer();
        }
        else
        {
            // Player escaped - return to lost soul state
            Debug.Log($"{gameObject.name} lost player, returning to LostSoul state");
            SetState(EnemyState.LostSoul);
        }
    }
    
    void HandlePeacefulState()
    {
        // Soul is passing on peacefully
        // Play peaceful animation, fade out, etc.
        // For now, just stay idle
        if (animator != null)
        {
            animator.SetBool("IsWalking", false);
            animator.SetBool("IsRunning", false);
        }
        
        // Could add: Float upward, fade out, spawn particles, etc.
    }
    
    void HandleDeadState()
    {
        // Already dead - do nothing
        if (animator != null)
        {
            animator.SetBool("IsWalking", false);
            animator.SetBool("IsRunning", false);
        }
    }
    
    void LookAtPlayer()
    {
        Vector3 directionToPlayer = (player.position - transform.position).normalized;
        directionToPlayer.y = 0;
        
        if (directionToPlayer != Vector3.zero)
        {
            Quaternion lookRotation = Quaternion.LookRotation(directionToPlayer);
            transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, Time.deltaTime * rotationSpeed);
        }
    }
    
    void ChasePlayer()
{
    Vector3 directionToPlayer = (player.position - transform.position).normalized;
    directionToPlayer.y = 0; // Don't move up/down
    
    Vector3 newPosition = transform.position + directionToPlayer * runSpeed * Time.deltaTime;
    newPosition.y = transform.position.y; // Lock Y position (stay at current height)
    
    transform.position = newPosition;
    
    // Play run animation
    if (animator != null)
    {
        animator.SetBool("IsWalking", false);
        animator.SetBool("IsRunning", true);
    }
}
    
    System.Collections.IEnumerator AttackPlayer()
    {
        isAttacking = true;
        
        // Choose random attack (punch or kick)
        int attackType = Random.Range(0, 2); // 0 or 1
        int damage = (attackType == 0) ? punchDamage : kickDamage;
        string attackName = (attackType == 0) ? "Punch" : "Kick";
        
        // Play attack animation
        if (animator != null)
        {
            animator.SetInteger("AttackType", attackType);
            animator.SetTrigger("Attack");
        }
        
        Debug.Log($"💥 {gameObject.name} uses {attackName}!");
        
        // Wait for animation to reach hit frame (adjust timing as needed)
        yield return new WaitForSeconds(0.5f);
        
        // Deal damage if still in range
        if (player != null)
        {
            float distanceToPlayer = Vector3.Distance(transform.position, player.position);
            
            if (distanceToPlayer <= attackRange)
            {
                PlayerHealth playerHealth = player.GetComponent<PlayerHealth>();
                if (playerHealth != null)
                {
                    playerHealth.TakeDamage(damage);
                    Debug.Log($"⚔️ Player took {damage} damage from {attackName}!");
                }
                else
                {
                    Debug.LogError("Player doesn't have PlayerHealth component!");
                }
            }
            else
            {
                Debug.Log($"{gameObject.name} attack missed - player moved out of range!");
            }
        }
        
        // Reset cooldown
        attackTimer = attackCooldown;
        isAttacking = false;
    }
    
    // Public method to change state (called by dialogue system)
    public void SetState(EnemyState newState)
    {
        if (currentState == newState) return;
        
        Debug.Log($"🔄 {gameObject.name} state: {currentState} → {newState}");
        currentState = newState;
        
        // Trigger death animation if becoming dead
        if (newState == EnemyState.Dead && animator != null)
        {
            animator.SetTrigger("Die");
        }
    }
    
    public EnemyState GetCurrentState() => currentState;
    
    // Called when player chooses to fight (dark path)
    public void BecomeHostile()
    {
        SetState(EnemyState.Hostile);
    }
    
    // Called when player chooses empathy (light path)
    public void BecomePeaceful()
    {
        SetState(EnemyState.Peaceful);
        // Could trigger peaceful death/ascension here
        StartCoroutine(PassOnPeacefully());
    }
    
    System.Collections.IEnumerator PassOnPeacefully()
    {
        // Wait a moment
        yield return new WaitForSeconds(2f);
        
        // Fade out and destroy
        Debug.Log($"✨ {gameObject.name} passes on peacefully...");
        Destroy(gameObject, 1f);
    }
    
    // Debug visualization
    void OnDrawGizmosSelected()
    {
        // Detection range (yellow)
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);
        
        // Attack range (red)
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}