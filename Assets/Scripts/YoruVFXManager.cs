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
    
    [Header("Debug")]
    [SerializeField] private bool debugMode = false;
    [SerializeField] private bool disableCombatChecks = false;
    [SerializeField] private bool disableCinematicChecks = false;
    
    // Components
    private Animator animator;
    private CharacterController controller;
    
    // Tracking
    private float lastFootstepTime;
    private bool wasGrounded;
    
    void Start()
    {
        animator = GetComponent<Animator>();
        controller = GetComponent<CharacterController>();
        
        // Create spawn points if not assigned
        if (!centerBody) centerBody = transform;
        
        // Only create default spawn points if we need them
        if (debugMode)
        {
            CreateDefaultSpawnPoints();
        }
    }
    
    void Update()
    {
        if (!controller || !animator) return; // Safety check
        
        bool isGrounded = controller.isGrounded;
        float speed = animator.GetFloat("Speed");
        
        // MOVEMENT EFFECTS - Always check these
        HandleMovementEffects(isGrounded, speed);
        
        // COMBAT EFFECTS - Only if not disabled and layer exists
        if (!disableCombatChecks && animator.layerCount > 1)
        {
            CheckCombatAnimations();
        }
        
        // CINEMATIC EFFECTS - Only if not disabled and layer exists
        if (!disableCinematicChecks && animator.layerCount > 2)
        {
            CheckCinematicAnimations();
        }
        
        wasGrounded = isGrounded;
    }
    
    // ========== MOVEMENT EFFECTS ==========
    void HandleMovementEffects(bool isGrounded, float speed)
    {
        // Footstep dust
        if (isGrounded && speed > 0.1f && dustPuff != null)
        {
            float interval = speed > 1.5f ? 0.2f : 0.4f;
            
            if (Time.time - lastFootstepTime > interval)
            {
                lastFootstepTime = Time.time;
                PlayEffect(dustPuff, transform.position);
            }
        }
        
        // Running trail
        if (runTrail != null)
        {
            if (speed > 1.5f && isGrounded && !runTrail.isPlaying)
            {
                runTrail.Play();
            }
            else if ((speed <= 1.5f || !isGrounded) && runTrail.isPlaying)
            {
                runTrail.Stop();
            }
        }
        
        // Landing effect
        if (!wasGrounded && isGrounded && landingImpact != null)
        {
            PlayEffect(landingImpact, transform.position);
        }
    }
    
    // Called by PlayerMovement when jumping
    public void OnJump(int jumpNumber)
    {
        if (jumpLaunch == null) return;
        
        Vector3 pos = transform.position;
        PlayEffect(jumpLaunch, pos);
        
        // Scale effect based on jump number
        var originalScale = jumpLaunch.transform.localScale;
        if (jumpNumber == 2)
        {
            jumpLaunch.transform.localScale = Vector3.one * 1.5f;
        }
        else if (jumpNumber == 3)
        {
            jumpLaunch.transform.localScale = Vector3.one * 2f;
        }
        else
        {
            jumpLaunch.transform.localScale = Vector3.one;
        }
    }
    
    // ========== COMBAT EFFECTS ==========
    void CheckCombatAnimations()
    {
        // Safety check for layer
        if (animator.layerCount <= 1) return;
        
        AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(1);
        float layerWeight = animator.GetLayerWeight(1);
        
        // Only check if layer is active
        if (layerWeight < 0.1f) return;
        
        // For now, we'll comment out the specific animation checks since they might be breaking
        // You can uncomment these once you verify the animation names match
        
        /*
        // Single Paw Attack
        if (state.IsName("AttackPaw") && pawAttack1 != null)
        {
            if (state.normalizedTime > 0.3f && state.normalizedTime < 0.4f)
            {
                if (!pawAttack1.isPlaying)
                {
                    PlayEffect(pawAttack1, rightPaw ? rightPaw.position : transform.position + Vector3.up);
                }
            }
        }
        */
        
        if (debugMode)
        {
            Debug.Log($"Combat Layer State: {state.fullPathHash}, Weight: {layerWeight}");
        }
    }
    
    // ========== CINEMATIC EFFECTS ==========
    void CheckCinematicAnimations()
    {
        // Safety check for layer
        if (animator.layerCount <= 2) return;
        
        AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(2);
        float layerWeight = animator.GetLayerWeight(2);
        
        // Only check if layer is active
        if (layerWeight < 0.1f) return;
        
        // For now, we'll comment out the specific animation checks
        // You can uncomment these once you verify the animation names match
        
        /*
        // Freeing Soul
        if (state.IsName("FreeingSoul") && soulFreeing != null)
        {
            if (state.normalizedTime > 0.3f && !soulFreeing.isPlaying)
            {
                PlayEffect(soulFreeing, centerBody.position + Vector3.up);
            }
        }
        */
        
        if (debugMode)
        {
            Debug.Log($"Cinematic Layer State: {state.fullPathHash}, Weight: {layerWeight}");
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
    
    // Manual trigger methods for AnimationTester
    public void TriggerPawAttack(bool isDouble)
    {
        if (isDouble && pawAttack2 != null)
            PlayEffect(pawAttack2, transform.position + Vector3.up);
        else if (!isDouble && pawAttack1 != null)
            PlayEffect(pawAttack1, rightPaw ? rightPaw.position : transform.position + Vector3.up);
    }
    
    public void TriggerTailCast(bool isLeft)
    {
        if (isLeft && leftTailMagic != null)
            PlayEffect(leftTailMagic, leftTail ? leftTail.position : transform.position + Vector3.left);
        else if (!isLeft && rightTailMagic != null)
            PlayEffect(rightTailMagic, rightTail ? rightTail.position : transform.position + Vector3.right);
    }
    
    public void TriggerCinematicEffect(string effectName)
    {
        switch (effectName)
        {
            case "soul":
                if (soulFreeing != null)
                    PlayEffect(soulFreeing, centerBody ? centerBody.position + Vector3.up : transform.position + Vector3.up);
                break;
            case "circle":
                if (circleActivation != null)
                    PlayEffect(circleActivation, transform.position);
                break;
            case "absorb":
                if (absorbing != null)
                    PlayEffect(absorbing, centerBody ? centerBody.position : transform.position);
                break;
        }
    }
}