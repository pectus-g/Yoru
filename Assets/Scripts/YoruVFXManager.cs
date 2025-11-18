using UnityEngine;

public class YoruVFXManager : MonoBehaviour
{
    [Header("=== MOVEMENT EFFECTS ===")]
    [SerializeField] private ParticleSystem dustPuff;        // Footsteps when walking
    [SerializeField] private ParticleSystem jumpLaunch;      // When jumping
    [SerializeField] private ParticleSystem landingImpact;   // When landing
    [SerializeField] private ParticleSystem runTrail;        // Trail when running
    
    [Header("=== COMBAT EFFECTS ===")]
    [SerializeField] private ParticleSystem pawAttack1;      // Single paw swipe
    [SerializeField] private ParticleSystem pawAttack2;      // Double paw attack
    [SerializeField] private ParticleSystem leftTailMagic;   // Blue forgiveness magic
    [SerializeField] private ParticleSystem rightTailMagic;  // Red punishment magic
    
    [Header("=== CINEMATIC EFFECTS ===")]
    [SerializeField] private ParticleSystem soulFreeing;     // Soul particles going up
    [SerializeField] private ParticleSystem circleActivation;// Magic circle on ground
    [SerializeField] private ParticleSystem absorbing;       // Energy absorption
    
    [Header("Effect Spawn Points")]
    [SerializeField] private Transform leftPaw;
    [SerializeField] private Transform rightPaw;
    [SerializeField] private Transform leftTail;
    [SerializeField] private Transform rightTail;
    [SerializeField] private Transform centerBody;
    
    // Components
    private Animator animator;
    private CharacterController controller;
    private PlayerMovement playerMovement;
    
    // Tracking
    private float lastFootstepTime;
    private bool wasGrounded;
    
    void Start()
    {
        animator = GetComponent<Animator>();
        controller = GetComponent<CharacterController>();
        playerMovement = GetComponent<PlayerMovement>();
        
        // Create spawn points if not assigned
        if (!centerBody) centerBody = transform;
        CreateDefaultSpawnPoints();
    }
    
    void Update()
    {
        bool isGrounded = controller.isGrounded;
        float speed = animator.GetFloat("Speed");
        
        // MOVEMENT EFFECTS
        HandleMovementEffects(isGrounded, speed);
        
        // COMBAT EFFECTS - Check if combat animations are playing
        CheckCombatAnimations();
        
        // CINEMATIC EFFECTS - Check if cinematic animations are playing
        CheckCinematicAnimations();
        
        wasGrounded = isGrounded;
    }
    
    // ========== MOVEMENT EFFECTS ==========
    void HandleMovementEffects(bool isGrounded, float speed)
    {
        // Footstep dust
        if (isGrounded && speed > 0.1f)
        {
            float interval = speed > 1.5f ? 0.2f : 0.4f; // Faster when running
            
            if (Time.time - lastFootstepTime > interval)
            {
                lastFootstepTime = Time.time;
                PlayEffect(dustPuff, transform.position);
            }
            
            // Running trail
            if (speed > 1.5f && runTrail && !runTrail.isPlaying)
            {
                runTrail.Play();
            }
            else if (speed <= 1.5f && runTrail && runTrail.isPlaying)
            {
                runTrail.Stop();
            }
        }
        
        // Landing effect
        if (!wasGrounded && isGrounded)
        {
            PlayEffect(landingImpact, transform.position);
        }
    }
    
    // Called by PlayerMovement when jumping
    public void OnJump(int jumpNumber)
    {
        Vector3 pos = transform.position;
        
        if (jumpNumber == 1)
        {
            PlayEffect(jumpLaunch, pos);
        }
        else if (jumpNumber == 2)
        {
            // Double jump - bigger effect
            PlayEffect(jumpLaunch, pos);
            if (jumpLaunch)
            {
                jumpLaunch.transform.localScale = Vector3.one * 1.5f;
            }
        }
        else if (jumpNumber == 3)
        {
            // Triple jump - huge effect
            PlayEffect(jumpLaunch, pos);
            if (jumpLaunch)
            {
                jumpLaunch.transform.localScale = Vector3.one * 2f;
            }
        }
    }
    
    // ========== COMBAT EFFECTS ==========
    void CheckCombatAnimations()
    {
        AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(1); // Combat Layer
        
        // Single Paw Attack
        if (state.IsName("AttackPaw") && state.normalizedTime > 0.3f && state.normalizedTime < 0.4f)
        {
            if (!pawAttack1.isPlaying)
            {
                PlayEffect(pawAttack1, rightPaw ? rightPaw.position : transform.position + Vector3.up);
            }
        }
        
        // Double Paw Attack (TowPaw)
        if (state.IsName("TowPaw") && state.normalizedTime > 0.3f && state.normalizedTime < 0.4f)
        {
            if (!pawAttack2.isPlaying)
            {
                PlayEffect(pawAttack2, transform.position + Vector3.up);
            }
        }
        
        // Left Tail Cast (Blue - Forgiveness)
        if (state.IsName("LeftTailCast") && state.normalizedTime > 0.2f && state.normalizedTime < 0.3f)
        {
            if (!leftTailMagic.isPlaying)
            {
                PlayEffect(leftTailMagic, leftTail ? leftTail.position : transform.position + Vector3.left);
            }
        }
        
        // Right Tail Cast (Red - Punishment)
        if (state.IsName("RightTailCast") && state.normalizedTime > 0.2f && state.normalizedTime < 0.3f)
        {
            if (!rightTailMagic.isPlaying)
            {
                PlayEffect(rightTailMagic, rightTail ? rightTail.position : transform.position + Vector3.right);
            }
        }
    }
    
    // ========== CINEMATIC EFFECTS ==========
    void CheckCinematicAnimations()
    {
        AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(2); // Cinematic Layer
        
        // Freeing Soul - particles go upward
        if (state.IsName("FreeingSoul") && state.normalizedTime > 0.3f)
        {
            if (!soulFreeing.isPlaying)
            {
                PlayEffect(soulFreeing, centerBody.position + Vector3.up);
            }
        }
        
        // Circle Activation - ground effect
        if (state.IsName("CircleActivation") && state.normalizedTime > 0.2f)
        {
            if (!circleActivation.isPlaying)
            {
                PlayEffect(circleActivation, transform.position);
            }
        }
        
        // Absorbing - energy coming to character
        if (state.IsName("Absorbing") && state.normalizedTime > 0.1f)
        {
            if (!absorbing.isPlaying)
            {
                PlayEffect(absorbing, centerBody.position);
            }
        }
    }
    
    // ========== HELPER METHODS ==========
    void PlayEffect(ParticleSystem effect, Vector3 position)
    {
        if (effect != null)
        {
            effect.transform.position = position;
            effect.Play();
        }
    }
    
    void CreateDefaultSpawnPoints()
    {
        // Create spawn points if not found in model
        if (!leftPaw)
        {
            GameObject lp = new GameObject("LeftPawVFX");
            lp.transform.SetParent(transform);
            lp.transform.localPosition = new Vector3(-0.3f, 0.5f, 0.3f);
            leftPaw = lp.transform;
        }
        
        if (!rightPaw)
        {
            GameObject rp = new GameObject("RightPawVFX");
            rp.transform.SetParent(transform);
            rp.transform.localPosition = new Vector3(0.3f, 0.5f, 0.3f);
            rightPaw = rp.transform;
        }
        
        if (!leftTail)
        {
            GameObject lt = new GameObject("LeftTailVFX");
            lt.transform.SetParent(transform);
            lt.transform.localPosition = new Vector3(-0.4f, 0.7f, -0.5f);
            leftTail = lt.transform;
        }
        
        if (!rightTail)
        {
            GameObject rt = new GameObject("RightTailVFX");
            rt.transform.SetParent(transform);
            rt.transform.localPosition = new Vector3(0.4f, 0.7f, -0.5f);
            rightTail = rt.transform;
        }
    }
    
    // Manual trigger methods (can be called from Animation Events or other scripts)
    public void TriggerPawAttack(bool isDouble)
    {
        if (isDouble)
            PlayEffect(pawAttack2, transform.position + Vector3.up);
        else
            PlayEffect(pawAttack1, rightPaw.position);
    }
    
    public void TriggerTailCast(bool isLeft)
    {
        if (isLeft)
            PlayEffect(leftTailMagic, leftTail.position);
        else
            PlayEffect(rightTailMagic, rightTail.position);
    }
    
    public void TriggerCinematicEffect(string effectName)
    {
        switch (effectName)
        {
            case "soul":
                PlayEffect(soulFreeing, centerBody.position + Vector3.up);
                break;
            case "circle":
                PlayEffect(circleActivation, transform.position);
                break;
            case "absorb":
                PlayEffect(absorbing, centerBody.position);
                break;
        }
    }
}