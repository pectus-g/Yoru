using UnityEngine;
using System.Collections;

/// <summary>
/// Handles the full enemy death sequence for YORU.
/// Attach to any enemy alongside EnemyHealth.
/// 
/// Sequence:
/// 1. Death animation plays (triggered by EnemyHealth)
/// 2. Brief dramatic pause
/// 3. Spirit flash (white pulses)
/// 4. Soul VFX prefab spawns (assign any professional particle in Inspector)
/// 5. Body dissolves (fade + shrink)
/// 6. Cleanup → destroy or respawn
/// 
/// Integration: EnemyHealth.Die() calls StartDeathSequence().
/// </summary>
public class EnemyDeathEffect : MonoBehaviour
{
    [Header("Death Animation")]
    [Tooltip("Length of the death animation clip in seconds")]
    [SerializeField] private float deathAnimDuration = 2f;
    [Tooltip("Dramatic pause after animation finishes")]
    [SerializeField] private float postAnimPause = 0.5f;

    [Header("Spirit Flash")]
    [SerializeField] private Color flashColor = new Color(1f, 1f, 1f, 1f);
    [SerializeField] private float flashDuration = 0.12f;
    [SerializeField] private int flashCount = 2;

    [Header("Soul VFX")]
    [Tooltip("Drag any particle prefab here. If empty, no soul particles spawn.")]
    [SerializeField] private GameObject soulVFXPrefab;
    [Tooltip("Offset from enemy center where VFX spawns")]
    [SerializeField] private Vector3 vfxSpawnOffset = new Vector3(0f, 1f, 0f);
    [Tooltip("Auto-destroy VFX after this many seconds (0 = don't auto-destroy)")]
    [SerializeField] private float vfxLifetime = 4f;

    [Header("Dissolve")]
    [SerializeField] private float dissolveDuration = 1.5f;
    [SerializeField] private float shrinkAmount = 0.15f;
    [SerializeField] private float sinkAmount = 0.1f;

    [Header("Respawn (Testing)")]
    [SerializeField] private bool allowRespawn = false;
    [SerializeField] private float respawnDelay = 10f;

    // Cached references
    private Renderer[] renderers;
    private Color[][] originalColors;
    private Vector3 originalScale;
    private Vector3 spawnPosition;
    private Quaternion spawnRotation;

    // Component references
    private EnemyHealth enemyHealth;
    private EnemyCombat enemyCombat;
    private EnemyHealthBar healthBar;
    private Collider enemyCollider;
    private Animator animator;

    private bool deathStarted = false;

    void Start()
    {
        renderers = GetComponentsInChildren<Renderer>();
        enemyHealth = GetComponent<EnemyHealth>();
        enemyCombat = GetComponent<EnemyCombat>();
        healthBar = GetComponent<EnemyHealthBar>();
        enemyCollider = GetComponent<Collider>();
        animator = GetComponent<Animator>();

        // Store spawn position for respawn
        spawnPosition = transform.position;
        spawnRotation = transform.rotation;
        originalScale = transform.localScale;

        CacheOriginalColors();

        Debug.Log($"[DeathFX] {gameObject.name} death effects ready");
    }

    // ========================================
    // PUBLIC API — called from EnemyHealth.Die()
    // ========================================

    public void StartDeathSequence()
    {
        if (deathStarted) return;
        deathStarted = true;
        StartCoroutine(DeathSequence());
    }

    // ========================================
    // DEATH SEQUENCE COROUTINE
    // ========================================

    private IEnumerator DeathSequence()
    {Debug.Log($"[DeathFX] ☠ {gameObject.name} death sequence started");

// Immediately disable combat & collision
if (enemyCombat != null) enemyCombat.enabled = false;
if (enemyCollider != null) enemyCollider.enabled = false;
if (animator != null)
{
   // Clear ALL pending triggers that could override our Play call
    animator.ResetTrigger("Hit");
    animator.ResetTrigger("Die");
    animator.ResetTrigger("Attack");
    animator.Play("die", 0, 0f);
    Debug.Log($"[DeathFX] Triggered Death animation (triggers cleared)");
    Debug.Log($"[DeathFX] Animator enabled: {animator.enabled}");
    Debug.Log($"[DeathFX] Animator gameObject active: {animator.gameObject.activeInHierarchy}");
    Debug.Log($"[DeathFX] Animator state hash: {animator.GetCurrentAnimatorStateInfo(0).shortNameHash}");
    Debug.Log($"[DeathFX] 'die' hash: {Animator.StringToHash("die")}");
    Debug.Log($"[DeathFX] Hash match: {animator.GetCurrentAnimatorStateInfo(0).shortNameHash == Animator.StringToHash("die")}");
    Debug.Log($"[DeathFX] State length: {animator.GetCurrentAnimatorStateInfo(0).length}");
    Debug.Log($"[DeathFX] State speed: {animator.GetCurrentAnimatorStateInfo(0).speed}");
    Debug.Log($"[DeathFX] IsInTransition: {animator.IsInTransition(0)}");
}
// 1. Death animation plays (already triggered by EnemyHealth)
Debug.Log($"[DeathFX] Playing death animation ({deathAnimDuration}s)");
yield return new WaitForSeconds(deathAnimDuration);

// 2. Dramatic pause
yield return new WaitForSeconds(postAnimPause);

// Capture position AFTER animation finishes (not before, avoids root motion drift)
Vector3 deathPosition = transform.position;

// Stop animator so root motion can't move the body during dissolve
if (animator != null) animator.enabled = false;

        // 3. Spirit flash
        Debug.Log("[DeathFX] Spirit flash");
        yield return StartCoroutine(SpiritFlash());

        // 4. Soul VFX (prefab-based — assign in Inspector)
        SpawnSoulVFX(deathPosition);

        // Small overlap — VFX starts while dissolve begins
        yield return new WaitForSeconds(0.3f);

        // 5. Dissolve (fade + shrink)
        Debug.Log("[DeathFX] Dissolving");
        yield return StartCoroutine(Dissolve(deathPosition));

        Debug.Log($"[DeathFX] ☠ {gameObject.name} death sequence complete");

        // 6. Cleanup
        if (allowRespawn)
        {
            Debug.Log($"[DeathFX] Will respawn in {respawnDelay}s");
            StartCoroutine(RespawnAfterDelay());
        }
        else
        {
            Destroy(gameObject, 0.5f);
        }
    }

    // ========================================
    // SPIRIT FLASH
    // ========================================

    private IEnumerator SpiritFlash()
    {
        for (int f = 0; f < flashCount; f++)
        {
            SetAllRendererColors(flashColor);
            yield return new WaitForSeconds(flashDuration);

            RestoreOriginalColors();
            yield return new WaitForSeconds(flashDuration * 0.6f);
        }

        // One final flash that lingers slightly longer
        SetAllRendererColors(flashColor);
        yield return new WaitForSeconds(flashDuration * 1.5f);
        RestoreOriginalColors();
    }

    // ========================================
    // SOUL VFX — prefab-based, assign in Inspector
    // ========================================

    private void SpawnSoulVFX(Vector3 deathPosition)
    {
        if (soulVFXPrefab == null)
        {
            Debug.Log("[DeathFX] No soul VFX prefab assigned — skipping particles");
            return;
        }

        Vector3 spawnPos = deathPosition + vfxSpawnOffset;
        GameObject vfxInstance = Instantiate(soulVFXPrefab, spawnPos, Quaternion.identity);
        Debug.Log($"[DeathFX] Soul VFX spawned at {spawnPos}");

        if (vfxLifetime > 0f)
        {
            Destroy(vfxInstance, vfxLifetime);
        }
    }

    // ========================================
    // DISSOLVE — fade + shrink from death position
    // ========================================

    private IEnumerator Dissolve(Vector3 deathPosition)
    {
        float elapsed = 0f;
        Vector3 startScale = transform.localScale;
        Vector3 targetScale = startScale * (1f - shrinkAmount);

        PrepareTransparentMaterials();

        while (elapsed < dissolveDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / dissolveDuration;

            float easedT = 1f - Mathf.Pow(1f - t, 2f);
            float alpha = 1f - easedT;

            // Fade all materials
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null) continue;
                foreach (Material mat in renderers[i].materials)
                {
                    if (mat.HasProperty("_Color"))
                    {
                        Color c = mat.color;
                        c.a = alpha;
                        mat.color = c;
                    }
                }
            }

            // Subtle shrink
            float shrinkT = Mathf.SmoothStep(0f, 1f, t);
            transform.localScale = Vector3.Lerp(startScale, targetScale, shrinkT);

            // Sink from DEATH position (not spawn)
            float currentSink = shrinkT * sinkAmount;
            transform.position = deathPosition - Vector3.up * currentSink;

            yield return null;
        }

        // Ensure fully invisible
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
                renderers[i].enabled = false;
        }
    }

    // ========================================
    // RESPAWN
    // ========================================

    private IEnumerator RespawnAfterDelay()
    {
        foreach (var r in renderers)
        {
            if (r != null) r.enabled = false;
        }

        if (healthBar != null) healthBar.enabled = false;

        yield return new WaitForSeconds(respawnDelay);

        // Reset transform to SPAWN position
        transform.position = spawnPosition;
        transform.rotation = spawnRotation;
        transform.localScale = originalScale;

        // Restore materials
        RestoreOriginalColors();
        RestoreMaterialOpacity();

        // Re-enable renderers
        foreach (var r in renderers)
        {
            if (r != null) r.enabled = true;
        }

        // Re-enable components
        if (enemyCollider != null) enemyCollider.enabled = true;
        if (enemyCombat != null)
        {
            enemyCombat.enabled = true;
            enemyCombat.ResetCombatState();
        }

        // Reset health
        if (enemyHealth != null) enemyHealth.ResetHealth();

        // Reset health bar
        if (healthBar != null)
        {
            healthBar.enabled = true;
            healthBar.ResetBar();
        }

        // Reset animator
        if (animator != null)
        { animator.enabled = true;
            animator.Rebind();
            animator.Update(0f);
        }

        // Reset death flag
        deathStarted = false;

        Debug.Log($"[DeathFX] ✨ {gameObject.name} respawned at spawn position!");
    }

    // ========================================
    // MATERIAL HELPERS
    // ========================================

    private void CacheOriginalColors()
    {
        originalColors = new Color[renderers.Length][];
        for (int i = 0; i < renderers.Length; i++)
        {
            Material[] mats = renderers[i].materials;
            originalColors[i] = new Color[mats.Length];
            for (int j = 0; j < mats.Length; j++)
            {
                if (mats[j].HasProperty("_Color"))
                    originalColors[i][j] = mats[j].color;
                else
                    originalColors[i][j] = Color.white;
            }
        }
    }

    private void SetAllRendererColors(Color color)
    {
        foreach (var r in renderers)
        {
            if (r == null) continue;
            foreach (var mat in r.materials)
            {
                if (mat.HasProperty("_Color"))
                    mat.color = color;
                if (mat.HasProperty("_EmissionColor"))
                {
                    mat.EnableKeyword("_EMISSION");
                    mat.SetColor("_EmissionColor", color * 0.5f);
                }
            }
        }
    }

    private void RestoreOriginalColors()
    {
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null) continue;
            Material[] mats = renderers[i].materials;
            for (int j = 0; j < mats.Length && j < originalColors[i].Length; j++)
            {
                if (mats[j].HasProperty("_Color"))
                    mats[j].color = originalColors[i][j];
                if (mats[j].HasProperty("_EmissionColor"))
                    mats[j].SetColor("_EmissionColor", Color.black);
            }
        }
    }

    private void PrepareTransparentMaterials()
    {
        foreach (var r in renderers)
        {
            if (r == null) continue;
            foreach (var mat in r.materials)
            {
                if (mat.HasProperty("_Mode"))
                {
                    mat.SetFloat("_Mode", 2);
                    mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    mat.SetInt("_ZWrite", 0);
                    mat.DisableKeyword("_ALPHATEST_ON");
                    mat.EnableKeyword("_ALPHABLEND_ON");
                    mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                    mat.renderQueue = 3000;
                }
            }
        }
    }

    private void RestoreMaterialOpacity()
    {
        foreach (var r in renderers)
        {
            if (r == null) continue;
            foreach (var mat in r.materials)
            {
                if (mat.HasProperty("_Mode"))
                {
                    mat.SetFloat("_Mode", 0);
                    mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
                    mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
                    mat.SetInt("_ZWrite", 1);
                    mat.EnableKeyword("_ALPHATEST_ON");
                    mat.DisableKeyword("_ALPHABLEND_ON");
                    mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                    mat.renderQueue = -1;
                }

                if (mat.HasProperty("_Color"))
                {
                    Color c = mat.color;
                    c.a = 1f;
                    mat.color = c;
                }
            }
        }
    }
}