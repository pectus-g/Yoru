using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class YoruSleepIntro : MonoBehaviour
{
    private Animator animator;
    private Rigidbody rb;
    private CharacterController cc;
    private Vector3 sleepPosition;
    private Quaternion sleepRotation;
    private bool isSleeping = true;
    private bool isFrozen = true;

    // Scripts that were auto-disabled so we can re-enable them
    private List<MonoBehaviour> disabledScripts = new List<MonoBehaviour>();

    void Start()
    {
        animator = GetComponent<Animator>();
        rb = GetComponent<Rigidbody>();
        cc = GetComponent<CharacterController>();

        // Auto-find and disable PlayerMovement and PlayerCombat
        DisableScript<PlayerMovement>();
        DisableScript<PlayerCombat>();

        // Freeze position and rotation
        sleepPosition = transform.position;
        sleepRotation = transform.rotation;

        if (rb != null)
        {
            rb.isKinematic = true;
            rb.linearVelocity = Vector3.zero;
        }

        if (cc != null)
            cc.enabled = false;

        // Start sleeping
        animator.SetBool("Sleep", true);

        // Hold last frame of sleep animation
        StartCoroutine(HoldSleepPose());
    }

    private void DisableScript<T>() where T : MonoBehaviour
    {
        T script = GetComponent<T>();
        if (script != null && script.enabled)
        {
            script.enabled = false;
            disabledScripts.Add(script);
            Debug.Log($"[SleepIntro] Disabled {typeof(T).Name}");
        }
    }

    void Update()
    {
        if (isSleeping && Input.GetMouseButtonDown(1))
        {
            isSleeping = false;
            StartCoroutine(WakeUpRoutine());
        }
    }

    void LateUpdate()
    {
        if (isFrozen)
        {
            transform.position = sleepPosition;
            transform.rotation = sleepRotation;

            if (rb != null)
                rb.linearVelocity = Vector3.zero;
        }
    }

    private IEnumerator HoldSleepPose()
    {
        // Wait a couple frames for animator to start transitioning
        yield return null;
        yield return null;

        // Wait up to 3 seconds for sleep state to start
        float timeout = 3f;
        float elapsed = 0f;
        AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(2);

        while (!state.IsName("sleep") && elapsed < timeout)
        {
            yield return null;
            elapsed += Time.deltaTime;
            state = animator.GetCurrentAnimatorStateInfo(2);
        }

        if (!state.IsName("sleep"))
        {
            Debug.LogWarning("[SleepIntro] Never entered sleep state! Check Cinematic Layer index and state name.");
            yield break;
        }

        // Wait until it nearly finishes
        while (state.IsName("sleep") && state.normalizedTime < 0.95f)
        {
            // Bail out if player already pressed M2
            if (!isSleeping) yield break;

            yield return null;
            state = animator.GetCurrentAnimatorStateInfo(2);
        }

        // Only freeze if still sleeping (player hasn't pressed M2)
        if (isSleeping)
        {
            animator.speed = 0f;
            Debug.Log("[SleepIntro] Holding sleep pose (animator frozen)");
        }
    }

    private IEnumerator WakeUpRoutine()
    {
        // Unfreeze animator
        animator.speed = 1f;

        // Stop sleep, start wake up
        animator.SetBool("Sleep", false);
        animator.SetBool("WakeUp", true);

        // Give animator time to react
        yield return null;
        yield return null;
        yield return new WaitForSeconds(0.1f);

        // Wait up to 3 seconds for WakeUp state
        float timeout = 3f;
        float elapsed = 0f;
        AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(2);

        while (!state.IsName("WakeUp") && elapsed < timeout)
        {
            yield return null;
            elapsed += Time.deltaTime;
            state = animator.GetCurrentAnimatorStateInfo(2);
        }

        if (!state.IsName("WakeUp"))
        {
            Debug.LogWarning("[SleepIntro] Never entered WakeUp state! Force-unfreezing.");
            UnfreezeEverything();
            yield break;
        }

        Debug.Log("[SleepIntro] WakeUp animation playing...");

        // Wait until WakeUp animation finishes
        while (state.IsName("WakeUp") && state.normalizedTime < 0.95f)
        {
            yield return null;
            state = animator.GetCurrentAnimatorStateInfo(2);
        }

        // Clear WakeUp bool
        animator.SetBool("WakeUp", false);

        Debug.Log("[SleepIntro] WakeUp complete, unfreezing player");

        UnfreezeEverything();
    }

    private void UnfreezeEverything()
    {
        isFrozen = false;

        if (rb != null)
            rb.isKinematic = false;

        if (cc != null)
            cc.enabled = true;

        // Re-enable all scripts we disabled
        foreach (var script in disabledScripts)
        {
            if (script != null)
            {
                script.enabled = true;
                Debug.Log($"[SleepIntro] Re-enabled {script.GetType().Name}");
            }
        }

        // Self-destruct — job is done, don't need this script anymore
        Destroy(this);
    }
}