**🚀 STARTING NOW! ALPHA ARCHITECTURE DOCUMENT - CREATING...**

---

# 📋 YORU: NEKOMATA - ALPHA ARCHITECTURE DOCUMENT

**Version:** 1.0 Alpha  
**Date:** October 9, 2025  
**Scope:** 6-Week Alpha Prototype  
**Playtime:** 3-5 minutes  
**Purpose:** Proof of concept for startup visa application

---

## 📑 TABLE OF CONTENTS

1. [Executive Summary](#executive-summary)
2. [System Overview](#system-overview)
3. [Core Systems](#core-systems)
   - [Player Movement System](#player-movement-system)
   - [Combat System](#combat-system)
   - [Soul System](#soul-system)
   - [Health System](#health-system)
   - [Save System](#save-system)
   - [UI System](#ui-system)
4. [Data Structures](#data-structures)
5. [Scene Structure](#scene-structure)
6. [Implementation Guide](#implementation-guide)
7. [Asset Requirements](#asset-requirements)
8. [Testing Procedures](#testing-procedures)
9. [Timeline & Milestones](#timeline--milestones)

---

## 📊 EXECUTIVE SUMMARY

### Alpha Prototype Goals

**Primary Objective:**  
Create a functional, recordable gameplay loop demonstrating core mechanics and AI-powered development workflow.

**Alpha Contains:**
- ✅ Player movement (cat form only)
- ✅ Basic combat (light attack, heavy attack, dodge)
- ✅ Soul system (mana/resource management)
- ✅ Health system (player + one enemy)
- ✅ One enemy type (Hitotsume-kozō)
- ✅ Memory cutscene system (AI-generated video trigger)
- ✅ Save/Load system (basic)
- ✅ Complete UI (HUD + menus)

**Alpha Does NOT Contain:**
- ❌ Transformation system (old lady form)
- ❌ Karma tracking (backend only, no visible impact)
- ❌ Dialogue system
- ❌ Boss encounters
- ❌ Multiple abilities
- ❌ Ring system

**Target Deliverable:**  
3-5 minute playable demo showing:
1. Player spawns in shrine area
2. Explores environment
3. Encounters Hitotsume enemy
4. Engages in combat
5. Defeats enemy
6. Watches memory cutscene (15 seconds)
7. "Alpha Complete" screen

**Technical Stack:**
- Engine: Unity 2022.3 LTS
- Language: C#
- Camera: Cinemachine FreeLook
- Input: Unity Input System (keyboard + future gamepad support)
- Save Format: JSON

---

## 🎮 SYSTEM OVERVIEW

### Architecture Diagram

```
┌─────────────────────────────────────────────────────┐
│                  GAME MANAGER                       │
│              (Singleton, Persistent)                │
└─────────────┬───────────────────────────────────────┘
              │
    ┌─────────┼─────────┬─────────┬─────────┬─────────┐
    │         │         │         │         │         │
┌───▼───┐ ┌──▼──┐  ┌───▼───┐ ┌───▼───┐ ┌───▼────┐ ┌──▼───┐
│Player │ │Soul │  │Health │ │ Save  │ │   UI   │ │Scene │
│System │ │Mgr  │  │ Mgr   │ │System │ │Manager │ │Loader│
└───┬───┘ └──┬──┘  └───┬───┘ └───┬───┘ └───┬────┘ └──────┘
    │        │         │         │         │
┌───▼────────▼─────────▼─────────▼─────────▼──────┐
│           GAME STATE (Data Layer)                │
│  - Player stats (HP, Soul, position)             │
│  - Enemy states                                  │
│  - Save data                                     │
│  - UI states                                     │
└──────────────────────────────────────────────────┘
```

### System Communication Flow

```
Player Input → Player Controller → Combat System → Damage System
                                                        ↓
                                                  Health Manager
                                                        ↓
                                              Update UI / Check Death
```

---

## 🎯 CORE SYSTEMS

### PLAYER MOVEMENT SYSTEM

#### Purpose
Handle all player locomotion, camera control, and basic interactions in cat form.

#### Components

**PlayerController.cs** (Main script)
```csharp
public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    public float walkSpeed = 3f;
    public float runSpeed = 6f;
    public float jumpForce = 8f;
    public float gravity = -20f;
    
    [Header("Camera")]
    public Transform cameraTransform;
    public float rotationSpeed = 5f;
    
    [Header("Ground Check")]
    public Transform groundCheck;
    public float groundDistance = 0.4f;
    public LayerMask groundMask;
    
    private CharacterController controller;
    private Vector3 velocity;
    private bool isGrounded;
    
    void Start()
    {
        controller = GetComponent<CharacterController>();
    }
    
    void Update()
    {
        HandleMovement();
        HandleJump();
        HandleGravity();
    }
    
    void HandleMovement()
    {
        // Get input
        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");
        
        // Calculate move direction relative to camera
        Vector3 forward = cameraTransform.forward;
        Vector3 right = cameraTransform.right;
        forward.y = 0f;
        right.y = 0f;
        forward.Normalize();
        right.Normalize();
        
        Vector3 moveDirection = forward * vertical + right * horizontal;
        
        // Apply speed (shift to run)
        float speed = Input.GetKey(KeyCode.LeftShift) ? runSpeed : walkSpeed;
        
        // Move character
        controller.Move(moveDirection * speed * Time.deltaTime);
        
        // Rotate character to face movement direction
        if (moveDirection.magnitude > 0.1f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(moveDirection);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }
    }
    
    void HandleJump()
    {
        isGrounded = Physics.CheckSphere(groundCheck.position, groundDistance, groundMask);
        
        if (isGrounded && velocity.y < 0)
        {
            velocity.y = -2f; // Small downward force
        }
        
        if (Input.GetButtonDown("Jump") && isGrounded)
        {
            velocity.y = Mathf.Sqrt(jumpForce * -2f * gravity);
        }
    }
    
    void HandleGravity()
    {
        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
    }
}
```

#### Cinemachine Camera Setup

**FreeLook Camera Configuration:**
```
GameObject: CM FreeLook Camera
Component: CinemachineFreeLook

Settings:
- Follow: Player transform
- Look At: Player transform (offset +1.5 Y)
- X Axis: Input Axis Name = "Mouse X", Speed = 300
- Y Axis: Input Axis Name = "Mouse Y", Speed = 2

Rig Settings:
- Top Rig:
  - Height: 3m
  - Radius: 6m
- Middle Rig:
  - Height: 1.5m
  - Radius: 4m
- Bottom Rig:
  - Height: 0.5m
  - Radius: 3m

Collision:
- Add CinemachineCollider extension
- Avoid Obstacles: true
- Distance Limit: 0.2m
- Damping: 0.5s
```

#### Animation States

**Required Animations:**
1. Idle (loop)
2. Walk (loop)
3. Run (loop)
4. Jump (oneshot)
5. Fall (loop)
6. Land (oneshot)

**Animation Controller Structure:**
```
States:
- Idle (default)
  → Walk (Speed > 0.1)
  → Run (Speed > 0.5)
  → Jump (IsGrounded = false, Velocity.y > 0)
  → Fall (IsGrounded = false, Velocity.y < 0)
  → Land (IsGrounded = true from Fall)
    → Idle (after 0.3s)
```

**Parameters:**
- Speed (float): Movement magnitude
- IsGrounded (bool): Ground detection
- Jump (trigger): Jump input

---

### COMBAT SYSTEM

#### Purpose
Handle player attacks, hit detection, damage calculation, and combat flow.

#### Components

**PlayerCombat.cs**
```csharp
public class PlayerCombat : MonoBehaviour
{
    [Header("Combat Stats")]
    public int lightAttackDamage = 10;
    public int heavyAttackDamage = 30;
    
    [Header("Attack Settings")]
    public float lightAttackCooldown = 0.3f;
    public float heavyAttackCooldown = 0.8f;
    public float dodgeCooldown = 0.4f;
    public float dodgeDistance = 3f;
    
    [Header("Hit Detection")]
    public Transform attackPoint;
    public float attackRange = 1.5f;
    public LayerMask enemyLayers;
    
    [Header("VFX")]
    public ParticleSystem blueFlameVFX;
    public GameObject clawSlashEffect;
    
    private Animator animator;
    private bool canAttack = true;
    private bool canDodge = true;
    private int comboStep = 0;
    
    void Start()
    {
        animator = GetComponent<Animator>();
    }
    
    void Update()
    {
        HandleCombatInput();
    }
    
    void HandleCombatInput()
    {
        // Light Attack (Left Mouse / J key)
        if (Input.GetMouseButtonDown(0) && canAttack)
        {
            LightAttack();
        }
        
        // Heavy Attack (Right Mouse / K key)
        if (Input.GetMouseButtonDown(1) && canAttack)
        {
            HeavyAttack();
        }
        
        // Dodge (Space / Shift)
        if (Input.GetKeyDown(KeyCode.Space) && canDodge)
        {
            Dodge();
        }
    }
    
    void LightAttack()
    {
        canAttack = false;
        comboStep++;
        
        // Trigger animation
        animator.SetTrigger("LightAttack");
        animator.SetInteger("ComboStep", comboStep);
        
        // Reset combo after 1 second
        Invoke("ResetCombo", 1f);
        
        // Deal damage (called from animation event)
        // See DealDamage() below
        
        // Cooldown
        StartCoroutine(AttackCooldown(lightAttackCooldown));
    }
    
    void HeavyAttack()
    {
        canAttack = false;
        comboStep = 0; // Heavy attack resets combo
        
        animator.SetTrigger("HeavyAttack");
        
        // VFX
        if (blueFlameVFX != null)
        {
            blueFlameVFX.Play();
        }
        
        StartCoroutine(AttackCooldown(heavyAttackCooldown));
    }
    
    void Dodge()
    {
        canDodge = false;
        
        animator.SetTrigger("Dodge");
        
        // Get dodge direction (based on input or forward)
        Vector3 dodgeDirection = transform.forward;
        
        // Quick dash
        GetComponent<CharacterController>().Move(dodgeDirection * dodgeDistance * Time.deltaTime);
        
        // Invincibility frames (0.4 seconds)
        GetComponent<PlayerHealth>().SetInvincible(true);
        Invoke("EndInvincibility", 0.4f);
        
        StartCoroutine(DodgeCooldown());
    }
    
    // Called from animation event at hit frame
    public void DealDamage()
    {
        // Detect enemies in range
        Collider[] hitEnemies = Physics.OverlapSphere(attackPoint.position, attackRange, enemyLayers);
        
        foreach (Collider enemy in hitEnemies)
        {
            // Get damage amount based on attack type
            int damage = comboStep > 0 ? lightAttackDamage : heavyAttackDamage;
            
            // Apply damage
            EnemyHealth enemyHealth = enemy.GetComponent<EnemyHealth>();
            if (enemyHealth != null)
            {
                enemyHealth.TakeDamage(damage);
            }
            
            // Spawn hit effect
            if (clawSlashEffect != null)
            {
                Instantiate(clawSlashEffect, enemy.transform.position, Quaternion.identity);
            }
        }
    }
    
    void ResetCombo()
    {
        comboStep = 0;
        animator.SetInteger("ComboStep", 0);
    }
    
    void EndInvincibility()
    {
        GetComponent<PlayerHealth>().SetInvincible(false);
    }
    
    IEnumerator AttackCooldown(float cooldown)
    {
        yield return new WaitForSeconds(cooldown);
        canAttack = true;
    }
    
    IEnumerator DodgeCooldown()
    {
        yield return new WaitForSeconds(dodgeCooldown);
        canDodge = true;
    }
    
    // Visualize attack range in editor
    void OnDrawGizmosSelected()
    {
        if (attackPoint == null) return;
        
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(attackPoint.position, attackRange);
    }
}
```

#### Combat Animation Events

**Setup in Unity Animator:**

For each attack animation, add Animation Events at hit frames:

```
LightAttack1 animation:
- Frame 8/18: Call "DealDamage"

LightAttack2 animation:
- Frame 6/18: Call "DealDamage"

LightAttack3 animation:
- Frame 10/20: Call "DealDamage"

HeavyAttack animation:
- Frame 15/30: Call "DealDamage"
```

#### Visual Effects

**Blue Flame VFX (Nekomata's Signature):**
```
Particle System Settings:
- Duration: 0.5s
- Start Lifetime: 0.3-0.5s
- Start Speed: 2-4
- Start Size: 0.2-0.5
- Start Color: Blue (#4da6ff) to Purple (#b366ff)
- Emission: Burst of 20 particles
- Shape: Sphere, Radius 0.5
- Color over Lifetime: Fade to transparent
- Velocity over Lifetime: Upward spiral
```

**Claw Slash Effect:**
```
Simple sprite animation:
- 4 frames of claw slash
- Blue-white color
- Plays once and destroys
- Duration: 0.2s
```

---

### SOUL SYSTEM

#### Purpose
Manage player's mana/spiritual energy resource used for abilities.

#### Core Stats
```
Starting Max Soul: 100
Soul Regeneration: 10/second (base)
Alpha Abilities: None (just display)
Full Game: Increases with karma and rings
```

#### SoulManager.cs

```csharp
public class SoulManager : MonoBehaviour
{
    public static SoulManager Instance { get; private set; }
    
    [Header("Soul Stats")]
    public int maxSoul = 100;
    public int currentSoul = 100;
    public float soulRegenRate = 10f; // per second
    
    [Header("Karma Bonus (Future)")]
    public int karmaPoints = 0; // Not used in Alpha
    private float karmaRegenBonus = 0f;
    
    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    void Start()
    {
        currentSoul = maxSoul;
    }
    
    void Update()
    {
        RegenerateSoul();
    }
    
    void RegenerateSoul()
    {
        if (currentSoul < maxSoul)
        {
            float totalRegen = soulRegenRate + karmaRegenBonus;
            currentSoul += Mathf.RoundToInt(totalRegen * Time.deltaTime);
            currentSoul = Mathf.Clamp(currentSoul, 0, maxSoul);
            
            // Update UI
            UIManager.Instance.UpdateSoulBar(currentSoul, maxSoul);
        }
    }
    
    public bool UseSoul(int amount)
    {
        if (currentSoul >= amount)
        {
            currentSoul -= amount;
            UIManager.Instance.UpdateSoulBar(currentSoul, maxSoul);
            return true;
        }
        return false;
    }
    
    public void RestoreSoul(int amount)
    {
        currentSoul += amount;
        currentSoul = Mathf.Clamp(currentSoul, 0, maxSoul);
        UIManager.Instance.UpdateSoulBar(currentSoul, maxSoul);
    }
    
    public void RestoreSoulFull()
    {
        currentSoul = maxSoul;
        UIManager.Instance.UpdateSoulBar(currentSoul, maxSoul);
    }
    
    // For future: Karma increases regen
    public void UpdateKarmaBonus(int karma)
    {
        karmaPoints = karma;
        // Every 10 karma = +1 soul/sec
        karmaRegenBonus = Mathf.Floor(karma / 10f);
    }
    
    // For future: Rings increase max soul
    public void IncreaseMaxSoul(int amount)
    {
        maxSoul += amount;
        currentSoul += amount; // Also restore by same amount
        UIManager.Instance.UpdateSoulBar(currentSoul, maxSoul);
    }
}
```

#### Soul Visual Indicator

**UI Bar Behavior:**
- Smooth lerp when changing values
- Glow effect when full
- Pulse effect when regenerating
- Dim when low (< 30%)

---

### HEALTH SYSTEM

#### Purpose
Manage health for player and enemies, handle damage, death states.

#### PlayerHealth.cs

```csharp
public class PlayerHealth : MonoBehaviour
{
    [Header("Health Stats")]
    public int maxHealth = 100;
    public int currentHealth = 100;
    
    [Header("Damage Settings")]
    public bool isInvincible = false;
    public float invincibilityDuration = 1f;
    
    [Header("Visual Effects")]
    public GameObject damageVFX;
    public Material flashMaterial;
    private Material originalMaterial;
    private Renderer playerRenderer;
    
    [Header("Audio")]
    public AudioClip hurtSound;
    public AudioClip deathSound;
    private AudioSource audioSource;
    
    void Start()
    {
        currentHealth = maxHealth;
        playerRenderer = GetComponentInChildren<Renderer>();
        originalMaterial = playerRenderer.material;
        audioSource = GetComponent<AudioSource>();
        
        UIManager.Instance.UpdateHealthBar(currentHealth, maxHealth);
    }
    
    public void TakeDamage(int damage)
    {
        if (isInvincible) return;
        
        currentHealth -= damage;
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);
        
        // Update UI
        UIManager.Instance.UpdateHealthBar(currentHealth, maxHealth);
        
        // Visual feedback
        StartCoroutine(DamageFlash());
        if (damageVFX != null)
        {
            Instantiate(damageVFX, transform.position, Quaternion.identity);
        }
        
        // Audio
        if (audioSource != null && hurtSound != null)
        {
            audioSource.PlayOneShot(hurtSound);
        }
        
        // Screen effect
        if (currentHealth < maxHealth * 0.3f)
        {
            UIManager.Instance.ShowLowHealthWarning(true);
        }
        else
        {
            UIManager.Instance.ShowLowHealthWarning(false);
        }
        
        // Check death
        if (currentHealth <= 0)
        {
            Die();
        }
        
        // Temporary invincibility
        StartCoroutine(InvincibilityFrames());
    }
    
    public void Heal(int amount)
    {
        currentHealth += amount;
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);
        
        UIManager.Instance.UpdateHealthBar(currentHealth, maxHealth);
        
        // Turn off low health warning if healed above 30%
        if (currentHealth >= maxHealth * 0.3f)
        {
            UIManager.Instance.ShowLowHealthWarning(false);
        }
    }
    
    public void SetInvincible(bool invincible)
    {
        isInvincible = invincible;
    }
    
    IEnumerator InvincibilityFrames()
    {
        isInvincible = true;
        yield return new WaitForSeconds(invincibilityDuration);
        isInvincible = false;
    }
    
    IEnumerator DamageFlash()
    {
        playerRenderer.material = flashMaterial;
        yield return new WaitForSeconds(0.1f);
        playerRenderer.material = originalMaterial;
    }
    
    void Die()
    {
        // Death animation
        GetComponent<Animator>().SetTrigger("Death");
        
        // Audio
        if (audioSource != null && deathSound != null)
        {
            audioSource.PlayOneShot(deathSound);
        }
        
        // Disable controls
        GetComponent<PlayerController>().enabled = false;
        GetComponent<PlayerCombat>().enabled = false;
        
        // Show death screen after animation
        Invoke("ShowDeathScreen", 2f);
    }
    
    void ShowDeathScreen()
    {
        UIManager.Instance.ShowDeathScreen();
    }
}
```

#### EnemyHealth.cs

```csharp
public class EnemyHealth : MonoBehaviour
{
    [Header("Health Stats")]
    public int maxHealth = 50;
    public int currentHealth = 50;
    
    [Header("UI")]
    public GameObject healthBarPrefab;
    private EnemyHealthBar healthBarUI;
    
    [Header("Death")]
    public GameObject deathVFX;
    public float deathDelay = 2f;
    
    void Start()
    {
        currentHealth = maxHealth;
        
        // Spawn health bar UI
        if (healthBarPrefab != null)
        {
            GameObject healthBarObj = Instantiate(healthBarPrefab, transform);
            healthBarUI = healthBarObj.GetComponent<EnemyHealthBar>();
            healthBarUI.SetMaxHealth(maxHealth);
            healthBarUI.gameObject.SetActive(false); // Hidden until damaged
        }
    }
    
    public void TakeDamage(int damage)
    {
        currentHealth -= damage;
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);
        
        // Show and update health bar
        if (healthBarUI != null)
        {
            healthBarUI.gameObject.SetActive(true);
            healthBarUI.SetHealth(currentHealth);
        }
        
        // Visual feedback
        StartCoroutine(DamageFlash());
        
        // Check death
        if (currentHealth <= 0)
        {
            Die();
        }
    }
    
    IEnumerator DamageFlash()
    {
        Renderer renderer = GetComponentInChildren<Renderer>();
        Color originalColor = renderer.material.color;
        renderer.material.color = Color.red;
        yield return new WaitForSeconds(0.1f);
        renderer.material.color = originalColor;
    }
    
    void Die()
    {
        // Death animation
        GetComponent<Animator>().SetTrigger("Death");
        
        // VFX
        if (deathVFX != null)
        {
            Instantiate(deathVFX, transform.position, Quaternion.identity);
        }
        
        // Disable AI and collider
        GetComponent<EnemyAI>().enabled = false;
        GetComponent<Collider>().enabled = false;
        
        // Hide health bar
        if (healthBarUI != null)
        {
            healthBarUI.gameObject.SetActive(false);
        }
        
        // Trigger memory cutscene after delay
        Invoke("TriggerMemory", deathDelay);
    }
    
    void TriggerMemory()
    {
        // Play memory cutscene
        MemorySystem.Instance.PlayMemory("Hitotsume_Memory");
        
        // Destroy enemy
        Destroy(gameObject, 0.5f);
    }
}
```

---

### SAVE SYSTEM

#### Purpose
Persist game state across sessions using JSON format.

#### SaveData Structure

**SaveData.cs**
```csharp
[System.Serializable]
public class SaveData
{
    // Player Stats
    public int currentHealth;
    public int maxHealth;
    public int currentSoul;
    public int maxSoul;
    
    // Player Position
    public float posX;
    public float posY;
    public float posZ;
    
    // Scene Info
    public string currentScene;
    public string lastCheckpoint;
    
    // Progression (for future)
    public int darkPoints;
    public int lightPoints;
    public int darkRings;
    public int lightRings;
    
    // Inventory (for future)
    public List<string> inventory;
    
    // Story Flags (for future)
    public List<string> storyFlags;
    
    // Meta
    public string saveDate;
    public float playtime;
    
    public SaveData()
    {
        inventory = new List<string>();
        storyFlags = new List<string>();
    }
}
```

#### SaveSystem.cs

```csharp
using System.IO;
using UnityEngine;

public class SaveSystem : MonoBehaviour
{
    public static SaveSystem Instance { get; private set; }
    
    private string saveDirectory;
    private string saveFileName = "SaveSlot";
    private string saveExtension = ".json";
    
    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
        
        saveDirectory = Application.persistentDataPath + "/Saves/";
        
        // Create save directory if it doesn't exist
        if (!Directory.Exists(saveDirectory))
        {
            Directory.CreateDirectory(saveDirectory);
        }
    }
    
    public void SaveGame(int slotNumber = 1)
    {
        SaveData data = new SaveData();
        
        // Player Stats
        PlayerHealth playerHealth = GameObject.FindGameObjectWithTag("Player").GetComponent<PlayerHealth>();
        data.currentHealth = playerHealth.currentHealth;
        data.maxHealth = playerHealth.maxHealth;
        data.currentSoul = SoulManager.Instance.currentSoul;
        data.maxSoul = SoulManager.Instance.maxSoul;
        
        // Player Position
        Transform playerTransform = GameObject.FindGameObjectWithTag("Player").transform;
        data.posX = playerTransform.position.x;
        data.posY = playerTransform.position.y;
        data.posZ = playerTransform.position.z;
        
        // Scene Info
        data.currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        
        // Meta
        data.saveDate = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        data.playtime = Time.timeSinceLevelLoad; // Simple for now
        
        // Convert to JSON
        string json = JsonUtility.ToJson(data, true);
        
        // Write to file
        string filePath = saveDirectory + saveFileName + slotNumber + saveExtension;
        File.WriteAllText(filePath, json);
        
        Debug.Log("Game saved to: " + filePath);
        
        // Show save confirmation UI
        UIManager.Instance.ShowSaveConfirmation();
    }
    
    public bool LoadGame(int slotNumber = 1)
    {
        string filePath = saveDirectory + saveFileName + slotNumber + saveExtension;
        
        if (!File.Exists(filePath))
        {
            Debug.LogWarning("Save file not found: " + filePath);
            return false;
        }
        
        // Read file
        string json = File.ReadAllText(filePath);
        
        // Parse JSON
        SaveData data = JsonUtility.FromJson<SaveData>(json);
        
        // Load scene first (if different)
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != data.currentScene)
        {
            // Store data temporarily to apply after scene loads
            PlayerPrefs.SetString("TempSaveData", json);
            UnityEngine.SceneManagement.SceneManager.LoadScene(data.currentScene);
            return true;
        }
        
        // Apply save data
        ApplySaveData(data);
        
        Debug.Log("Game loaded from: " + filePath);
        return true;
    }
    
    void ApplySaveData(SaveData data)
    {
        // Player Stats
        PlayerHealth playerHealth = GameObject.FindGameObjectWithTag("Player").GetComponent<PlayerHealth>();
        playerHealth.currentHealth = data.currentHealth;
        playerHealth.maxHealth = data.maxHealth;
        
        SoulManager.Instance.currentSoul = data.currentSoul;
        SoulManager.Instance.maxSoul = data.maxSoul;
        
        // Player Position
        Transform playerTransform = GameObject.FindGameObjectWithTag("Player").transform;
        playerTransform.position = new Vector3(data.posX, data.posY, data.posZ);
        
        // Update UI
        UIManager.Instance.UpdateHealthBar(data.currentHealth, data.maxHealth);
        UIManager.Instance.UpdateSoulBar(data.currentSoul, data.maxSoul);
    }
    
    public bool SaveExists(int slotNumber)
    {
        string filePath = saveDirectory + saveFileName + slotNumber + saveExtension;
        return File.Exists(filePath);
    }
    
    public SaveData GetSaveData(int slotNumber)
    {
        string filePath = saveDirectory + saveFileName + slotNumber + saveExtension;
        
        if (!File.Exists(filePath))
        {
            return null;
        }
        
        string json = File.ReadAllText(filePath);
        return JsonUtility.FromJson<SaveData>(json);
    }
    
    public void DeleteSave(int slotNumber)
    {
        string filePath = saveDirectory + saveFileName + slotNumber + saveExtension;
        
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
            Debug.Log("Save deleted: " + filePath);
        }
    }
}
```

#### Auto-Save Triggers

**AutoSave.cs** (Attach to checkpoints)
```csharp
public class AutoSave : MonoBehaviour
{
    public int saveSlot = 1;
    
    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            SaveSystem.Instance.SaveGame(saveSlot);
        }
    }
}
```

---

### UI SYSTEM

#### Purpose
Display all game information to player: health, soul, menus, etc.

#### UIManager.cs (Singleton)

```csharp
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }
    
    [Header("HUD")]
    public Slider healthBar;
    public TextMeshProUGUI healthText;
    public Slider soulBar;
    public TextMeshProUGUI soulText;
    public Image lowHealthVignette;
    
    [Header("Menus")]
    public GameObject pauseMenu;
    public GameObject deathScreen;
    public GameObject saveConfirmation;
    
    [Header("Settings")]
    public float barLerpSpeed = 5f;
    
    private float targetHealthFill;
    private float targetSoulFill;
    
    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    void Start()
    {
        // Hide menus on start
        pauseMenu.SetActive(false);
        deathScreen.SetActive(false);
        saveConfirmation.SetActive(false);
        lowHealthVignette.gameObject.SetActive(false);
    }
    
    void Update()
    {
        // Smooth lerp health bar
        if (healthBar.value != targetHealthFill)
        {
            healthBar.value = Mathf.Lerp(healthBar.value, targetHealthFill, barLerpSpeed * Time.deltaTime);
        }
        
        // Smooth lerp soul bar
        if (soulBar.value != targetSoulFill)
        {
            soulBar.value = Mathf.Lerp(soulBar.value, targetSoulFill, barLerpSpeed * Time.deltaTime);
        }
        
        // Pause menu toggle
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            TogglePauseMenu();
        }
    }
    
    public void UpdateHealthBar(int current, int max)
    {
        targetHealthFill = (float)current / max;
        healthText.text = $"{current} / {max}";
    }
    
    public void UpdateSoulBar(int current, int max)
    {
        targetSoulFill = (float)current / max;
        soulText.text = $"{current} / {max}";
    }
    
    public void ShowLowHealthWarning(bool show)
    {
        lowHealthVignette.gameObject.SetActive(show);
        
        if (show)
        {
            // Pulse effect
            LeanTween.alpha(lowHealthVignette.rectTransform, 0.5f, 1f).setLoopPingPong();
        }
        else
        {
            LeanTween.cancel(lowHealthVignette.gameObject);
        }
    }
    
    public void TogglePauseMenu()
    {
        bool isActive = !pauseMenu.activeSelf;
        pauseMenu.SetActive(isActive);
        Time.timeScale = isActive ? 0f : 1f;
        Cursor.visible = isActive;
        Cursor.lockState = isActive ? CursorLockMode.None : CursorLockMode.Locked;
    }
    
    public void ShowDeathScreen()
    {
        deathScreen.SetActive(true);
        Time.timeScale = 0f;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }
    
    public void ShowSaveConfirmation()
    {
        saveConfirmation.SetActive(true);
        Invoke("HideSaveConfirmation", 2f);
    }
    
    void HideSaveConfirmation()
    {
        saveConfirmation.SetActive(false);
    }
    
    // Button functions
    public void ResumeGame()
    {
        TogglePauseMenu();
    }
    
    public void SaveGame()
    {
        SaveSystem.Instance.SaveGame();
    }
    
    public void LoadGame()
    {
        SaveSystem.Instance.LoadGame();
        TogglePauseMenu();
    }
    
    public void QuitToMainMenu()
    {
        Time.timeScale = 1f;
        UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
    }
    
    public void RestartFromLastSave()
    {
        Time.timeScale = 1f;
        SaveSystem.Instance.LoadGame();
    }
}
```

#### UI Layout (Canvas Structure)

```
Canvas (Screen Space - Overlay)
├─ HUD
│  ├─ HealthBar (Top-left)
│  │  ├─ Background (Dark)
│  │  ├─ Fill (Red)
│  │  └─ Text (235 / 300)
│  ├─ SoulBar (Below health)
│  │  ├─ Background (Dark)
│  │  ├─ Fill (Blue)
│  │  └─ Text (180 / 300)
│  └─ LowHealthVignette (Full screen, red overlay)
├─ PauseMenu (Initially inactive)
│  ├─ Background (Dim overlay)
│  ├─ Panel
│  │  ├─ Title ("Paused")
│  │  ├─ Button: Resume
│  │  ├─ Button: Save Game
│  │  ├─ Button: Load Game
│  │  ├─ Button: Settings
│  │  └─ Button: Quit to Menu
├─ DeathScreen (Initially inactive)
│  ├─ Background (Black overlay)
│  ├─ Text ("You have fallen...")
│  ├─ Button: Load Last Save
│  └─ Button: Quit to Menu
└─ SaveConfirmation (Initially inactive)
   └─ Text ("Game Saved!")
```

---

## 📦 DATA STRUCTURES

### Game State Management

**GameManager.cs** (Singleton, persistent)
```csharp
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }
    
    [Header("Game State")]
    public bool isPaused = false;
    public bool playerAlive = true;
    
    [Header("References")]
    public GameObject playerPrefab;
    public Transform playerSpawnPoint;
    
    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
    
    public void PauseGame()
    {
        isPaused = true;
        Time.timeScale = 0f;
    }
    
    public void ResumeGame()
    {
        isPaused = false;
        Time.timeScale = 1f;
    }
    
    public void PlayerDied()
    {
        playerAlive = false;
        // Handle death logic
    }
    
    public void RespawnPlayer()
    {
        playerAlive = true;
        // Respawn at checkpoint
    }
}
```

---

## 🗺️ SCENE STRUCTURE

### Alpha Scene Layout

**Scene Name:** "Alpha_ShrineArea"

**Hierarchy:**
```
Alpha_ShrineArea
├─ Managers
│  ├─ GameManager
│  ├─ SoulManager
│  └─ SaveSystem
├─ Player
│  ├─ Yoru_Model (from freelancer)
│  ├─ CM FreeLook Camera
│  ├─ PlayerController
│  ├─ PlayerCombat
│  └─ PlayerHealth
├─ Environment
│  ├─ Shrine (Eastlands assets)
│  ├─ Trees
│  ├─ Rocks
│  ├─ Ground
│  └─ Lighting
├─ Enemies
│  └─ Hitotsume_01
│     ├─ Model (Meshy generated)
│     ├─ EnemyAI
│     └─ EnemyHealth
├─ Checkpoints
│  └─ StartCheckpoint (AutoSave trigger)
└─ UI
   └─ Canvas (all UI elements)
```

**Lighting Setup:**
- Directional Light (Twilight, purple-orange)
- Ambient: Gradient (purple to orange)
- Fog: Enabled, purple tint
- Post-Processing: Bloom, color grading

---

## 🛠️ IMPLEMENTATION GUIDE

### Week-by-Week Breakdown

#### Week 1: Foundation (Oct 21-27)
**Tasks:**
1. Set up Unity project
2. Import Eastlands assets
3. Build shrine environment (basic layout)
4. Import Yoru model (when received from freelancer)
5. Set up Cinemachine camera

**Deliverable:** Empty scene with environment and camera

#### Week 2: Player Movement (Oct 28-Nov 3)
**Tasks:**
1. Implement PlayerController.cs
2. Set up character controller
3. Import animations from freelancer
4. Configure animation controller
5. Test movement feel

**Deliverable:** Player can walk, run, jump smoothly

#### Week 3: Combat System (Nov 4-10)
**Tasks:**
1. Implement PlayerCombat.cs
2. Set up attack animations
3. Add animation events
4. Create attack VFX (blue flames)
5. Test attack hitboxes

**Deliverable:** Player can attack (light/heavy/dodge)

#### Week 4: Enemy & Health (Nov 11-17)
**Tasks:**
1. Import Hitotsume model (Meshy)
2. Set up Mixamo animations
3. Implement EnemyHealth.cs
4. Implement EnemyAI.cs (basic patrol/chase)
5. Create enemy health bar UI

**Deliverable:** Enemy can be fought and killed

#### Week 5: Systems Integration (Nov 18-24)
**Tasks:**
1. Implement SoulManager.cs
2. Implement PlayerHealth.cs
3. Implement SaveSystem.cs
4. Implement UIManager.cs
5. Create all UI elements

**Deliverable:** All systems working together

#### Week 6: Polish & Memory (Nov 25-Dec 1)
**Tasks:**
1. Generate AI memory video (Veo)
2. Implement MemorySystem.cs (video player)
3. Lighting and post-processing
4. Music integration
5. Bug fixing and testing
6. Record gameplay footage

**Deliverable:** Complete alpha ready for showcase

---

### Testing Checklist

**Movement Tests:**
- [ ] Player walks at correct speed
- [ ] Player runs when holding shift
- [ ] Jump feels responsive
- [ ] Camera follows smoothly
- [ ] No clipping through walls
- [ ] Gravity feels natural

**Combat Tests:**
- [ ] Light attack deals 10 damage
- [ ] Heavy attack deals 30 damage
- [ ] Attacks hit enemies correctly
- [ ] Dodge provides invincibility
- [ ] VFX appear on hits
- [ ] Animations play correctly

**Health Tests:**
- [ ] Player health bar updates
- [ ] Enemy health bar appears on damage
- [ ] Low health warning at 30%
- [ ] Death triggers correctly
- [ ] Invincibility frames work

**Soul Tests:**
- [ ] Soul bar displays correctly
- [ ] Soul regenerates at 10/sec
- [ ] Soul bar smooth lerps

**Save Tests:**
- [ ] Can save game
- [ ] Can load game
- [ ] Player position restored
- [ ] Health/Soul restored
- [ ] Save file exists in folder

**UI Tests:**
- [ ] Health bar fills correctly
- [ ] Soul bar fills correctly
- [ ] Pause menu works
- [ ] Death screen appears
- [ ] All buttons functional

---

## 📁 ASSET REQUIREMENTS

### 3D Models

**Player:**
- Yoru (Nekomata, humanoid form)
- Source: Freelancer commission
- Format: FBX
- Polycount: 10k-20k tris
- Rig: Humanoid
- Textures: 2048x2048 (diffuse, normal, metallic)

**Enemy:**
- Hitotsume-kozō (one-eyed yokai)
- Source: Meshy AI generation
- Format: FBX/OBJ
- Polycount: 5k-10k tris
- Rig: Humanoid (for Mixamo)
- Textures: 1024x1024

**Environment:**
- Eastlands Asset Pack (already purchased ✅)

### Animations

**Player Animations (From Freelancer):**
1. Idle
2. Walk
3. Run
4. Jump
5. Fall
6. Land
7. Attack_Light_1
8. Attack_Light_2
9. Attack_Light_3
10. Attack_Heavy
11. Dodge
12. Hit_Reaction
13. Death

Total: 13 animations

**Enemy Animations (Mixamo):**
1. Idle
2. Walk
3. Run
4. Attack
5. Hit_Reaction
6. Death

Total: 6 animations

### VFX & Particles

**Required Effects:**
1. Blue Flame (Nekomata signature)
   - Particle system
   - Color: Blue to purple
   
2. Claw Slash
   - Sprite animation (4 frames)
   - Color: Blue-white
   
3. Hit Sparks
   - Simple particle burst
   
4. Death VFX
   - Spirit particles rising
   
5. Low Health Vignette
   - Red overlay texture

### Audio

**Music:**
1. Ambient Exploration (loop, 2 min)
   - Sad koto, shakuhachi
   - Slow tempo, melancholic

**Sound Effects:**
1. Footsteps (grass, stone)
2. Attack whoosh (light)
3. Attack whoosh (heavy)
4. Hit impact (flesh)
5. Dodge sound (wind)
6. Player hurt (3 variations)
7. Player death
8. Enemy hurt
9. Enemy death
10. UI click
11. UI hover

Total: ~15 sound effects

### UI Graphics

**HUD Elements:**
1. Health bar frame
2. Health bar fill (red)
3. Soul bar frame
4. Soul bar fill (blue)
5. Low health vignette

**Menu Elements:**
1. Button normal state
2. Button hover state
3. Button pressed state
4. Panel background
5. Pause menu background (dim overlay)

---

## ✅ TESTING PROCEDURES

### Phase 1: Unit Testing

**Test each system independently:**

**Movement Test:**
```
1. Start game
2. Move with WASD - verify smooth movement
3. Rotate camera - verify no jitter
4. Jump - verify feels good
5. Run - verify speed increase
6. Walk off edge - verify gravity
```

**Combat Test:**
```
1. Press Left Mouse - verify light attack
2. Press Right Mouse - verify heavy attack
3. Press Space - verify dodge
4. Test combo - light > light > light
5. Verify VFX appear
6. Verify animations play
```

**Health Test:**
```
1. Take damage - verify bar updates
2. Health < 30% - verify warning appears
3. Die - verify death screen
4. Load save - verify respawn
```

### Phase 2: Integration Testing

**Test systems together:**

```
Full Combat Loop:
1. Player spawns
2. Approach enemy
3. Enemy aggros
4. Enter combat
5. Attack enemy
6. Enemy retaliates
7. Player takes damage
8. Dodge attack
9. Defeat enemy
10. Memory plays
11. Return to exploration
```

### Phase 3: Stress Testing

**Edge cases:**

```
1. Die during cutscene
2. Save during combat
3. Pause during attack
4. Load save mid-air
5. Attack through walls
6. Camera clipping
7. Multiple enemies (if added later)
```

### Phase 4: Performance Testing

**Target Performance:**
- 60 FPS on mid-tier PC
- 30 FPS minimum on low-tier
- No memory leaks
- Quick load times (< 5 seconds)

**Tools:**
- Unity Profiler
- Frame Debugger
- Memory Profiler

---

## 📅 TIMELINE & MILESTONES

### 6-Week Schedule

**Week 1 (Oct 21-27): Setup**
- Milestone: Environment built, camera working
- Deliverable: Walkable shrine area

**Week 2 (Oct 28-Nov 3): Player**
- Milestone: Player movement complete
- Deliverable: Controllable character

**Week 3 (Nov 4-10): Combat**
- Milestone: Combat system functional
- Deliverable: Player can attack

**Week 4 (Nov 11-17): Enemy**
- Milestone: Enemy AI and health working
- Deliverable: Complete combat loop

**Week 5 (Nov 18-24): Systems**
- Milestone: All systems integrated
- Deliverable: Playable alpha build

**Week 6 (Nov 25-Dec 1): Polish**
- Milestone: Alpha complete and polished
- Deliverable: Recordable demo for visa

### Critical Path

```
Environment → Player Movement → Combat → Enemy → Integration → Polish
    ↓             ↓                ↓        ↓          ↓          ↓
  Week 1        Week 2          Week 3   Week 4     Week 5    Week 6
```

**Potential Risks:**
1. Freelancer animations delayed → Use Mixamo temporarily
2. Meshy model quality low → Regenerate or use Asset Store
3. Cinemachine camera issues → Fall back to simple follow camera
4. VFX too complex → Use simpler particle effects

---

## 🎯 SUCCESS CRITERIA

**Alpha is complete when:**

✅ Player can move smoothly in 3D space  
✅ Player can perform all combat actions  
✅ Enemy can be fought and defeated  
✅ Health and Soul systems functional  
✅ UI displays all information clearly  
✅ Save/Load works correctly  
✅ Memory cutscene plays after enemy defeat  
✅ No game-breaking bugs  
✅ Runs at 30+ FPS  
✅ Gameplay is recordable for pitch video  

---

## 📝 NOTES FOR IMPLEMENTATION

### Code Organization

```
Assets/
├─ Scripts/
│  ├─ Player/
│  │  ├─ PlayerController.cs
│  │  ├─ PlayerCombat.cs
│  │  └─ PlayerHealth.cs
│  ├─ Enemy/
│  │  ├─ EnemyAI.cs
│  │  └─ EnemyHealth.cs
│  ├─ Managers/
│  │  ├─ GameManager.cs
│  │  ├─ SoulManager.cs
│  │  ├─ SaveSystem.cs
│  │  └─ UIManager.cs
│  └─ Systems/
│     ├─ SaveData.cs
│     └─ MemorySystem.cs
├─ Prefabs/
│  ├─ Player/
│  ├─ Enemies/
│  └─ UI/
├─ Materials/
├─ Textures/
├─ Models/
├─ Animations/
├─ Audio/
└─ Scenes/
```

### Best Practices

1. **Use Singletons for Managers** (GameManager, SoulManager, etc.)
2. **Tag everything properly** (Player, Enemy, Ground)
3. **Use layers for hit detection** (PlayerAttacks, EnemyAttacks)
4. **Comment complex logic**
5. **Keep functions small** (< 20 lines each)
6. **Test frequently** (don't wait until end)
7. **Version control** (Git recommended)

---

## 🚀 NEXT STEPS

After Alpha completion, you will have:
1. Working prototype
2. Recordable gameplay footage
3. Proof of technical capability
4. Foundation for demo expansion

**This alpha will be used for:**
- Startup visa application
- Marketing materials (trailer)
- Social media content
- Steam page teaser
- Investor pitches

---

**END OF ALPHA ARCHITECTURE DOCUMENT**

---

**DOCUMENT COMPLETE! ✅**

**This is your Alpha Architecture - 12 pages in Markdown format!**

**What I've covered:**
✅ Executive summary
✅ Complete system designs
✅ Full code examples
✅ Scene structure
✅ Week-by-week implementation guide
✅ Asset requirements
✅ Testing procedures
✅ Timeline with milestones

**How to use this:**
1. Copy all text above (from "# 📋 YORU..." to end)
2. Paste into a Markdown editor (VS Code, Obsidian, Typora)
3. Save as `Alpha_Architecture.md`
4. You can convert to PDF using markdown-pdf tools
5. You can also paste into Google Docs and format

**Questions before I create DEMO Architecture next?**

Or should I continue with Demo doc (25-35 pages)? 🚀