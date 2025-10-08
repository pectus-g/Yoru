**🚀 CREATING DEMO ARCHITECTURE DOCUMENT NOW...**

---

# 📋 YORU: NEKOMATA - DEMO ARCHITECTURE DOCUMENT

**Version:** 1.0 Demo  
**Date:** October 9, 2025  
**Scope:** Complete Demo Build (Post-Alpha)  
**Playtime:** 15-20 minutes  
**Purpose:** Full demo for Steam page, marketing, and post-visa development

---

## 📑 TABLE OF CONTENTS

1. [Executive Summary](#executive-summary)
2. [Demo Overview](#demo-overview)
3. [Systems Architecture](#systems-architecture)
4. [New Systems (Beyond Alpha)](#new-systems-beyond-alpha)
   - [Transformation System](#transformation-system)
   - [Karma System](#karma-system)
   - [Dialogue System](#dialogue-system)
   - [Boss Encounter System](#boss-encounter-system)
   - [Ring & Ability System](#ring--ability-system)
   - [Hide and Seek System](#hide-and-seek-system)
   - [Objective Tracker](#objective-tracker)
   - [Memory System (Expanded)](#memory-system-expanded)
5. [Enemy AI Systems](#enemy-ai-systems)
6. [Enhanced Save System](#enhanced-save-system)
7. [Complete UI System](#complete-ui-system)
8. [Scene Structure](#scene-structure)
9. [Cinematics Integration](#cinematics-integration)
10. [Implementation Roadmap](#implementation-roadmap)
11. [Testing & QA](#testing--qa)
12. [Asset Requirements](#asset-requirements)

---

## 📊 EXECUTIVE SUMMARY

### Demo Scope

**What Demo Includes:**

**From Alpha (Enhanced):**
- ✅ Player movement & combat
- ✅ Health & Soul systems
- ✅ Basic save/load
- ✅ One enemy type (Hitotsume)

**NEW for Demo:**
- ✅ **Transformation system** (cat ↔ old lady)
- ✅ **Karma tracking** (dark/light points)
- ✅ **Dialogue system** (NPC + boss)
- ✅ **Boss encounter** (Oni)
- ✅ **Ring system** (first ability unlocked)
- ✅ **Hide and seek puzzle**
- ✅ **3 Enemy types** (Hitotsume, Kappa equivalent, Shou/Tiger)
- ✅ **Multiple cinematics** (4 total)
- ✅ **Objective tracker**
- ✅ **Enhanced UI** (full menus)

**Demo Flow (18 minutes):**
```
1. Opening Cutscene (90 sec) → Woman becomes Nekomata
2. Tutorial Area (5 min) → Learn movement, combat
3. First Encounters (3 min) → Fight 2 enemies, see memories
4. Hide and Seek (3 min) → Find granddaughter, emotional moment
5. Oni Boss (5 min) → Choice-driven encounter
6. Ring Unlock (30 sec) → Gain first ability
7. Closing Cutscene (90 sec) → Festival memory revealed
8. "To Be Continued" screen
```

**Technical Improvements:**
- Save system with multiple slots
- Complete pause menu system
- Character stats screen
- Journal/quest log
- Inventory integration (sake, peaches)
- Music state management
- Advanced camera states

---

## 🎮 DEMO OVERVIEW

### Player Experience Journey

**Act 1: Awakening (Minutes 0-5)**
```
Opening Cutscene (AI video)
    ↓
Player wakes in shrine area (Yoru, cat form)
    ↓
Tutorial: Movement, camera, basic controls
    ↓
Objective: "Explore the shrine area"
    ↓
Find collectibles, learn environment
    ↓
Objective updates: "Investigate the disturbance"
```

**Act 2: First Blood (Minutes 5-8)**
```
Encounter Hitotsume #1
    ↓
Tutorial combat: Light attack, heavy attack, dodge
    ↓
Defeat enemy
    ↓
Memory Cutscene: Hitotsume's tragic backstory (15 sec)
    ↓
Karma tracking begins (player chose to kill)
    ↓
Continue exploring
    ↓
Encounter Hitotsume #2
    ↓
Combat again (now player knows system)
    ↓
Memory Cutscene: Different Hitotsume memory
    ↓
Unlock Transformation ability
    ↓
Objective: "Seek the lost soul"
```

**Act 3: Lost Child (Minutes 8-11)**
```
Follow glowing footprints
    ↓
Hide and Seek puzzle begins
    ↓
Find girl hiding (3 locations)
    ↓
Third location: Graveyard
    ↓
Memory Cutscene: Playing with granddaughter (30 sec)
    ↓
Emotional dialogue moment
    ↓
"Do you remember me?"
    ↓
Oni appears, takes girl
    ↓
Objective: "Protect the child" OR "Let her go"
```

**Act 4: Boss Encounter (Minutes 11-16)**
```
Player choice:
   A) Protect → Boss fight OR persuasion
   B) Abandon → Skip fight, dark path
    ↓
If Fight chosen:
   - Optional: Transform to old lady (persuasion bonus)
   - Dialogue puzzle (3-5 questions)
   - Success → Girl saved, light path
   - Failure → Must fight Oni
    ↓
If Fight combat:
   - Full boss battle
   - Use sake to weaken (optional)
   - Defeat Oni
    ↓
Ring appears on tail
    ↓
First ability unlocked (Dark OR Light)
    ↓
Tutorial: Use ability (Q key)
```

**Act 5: Revelation (Minutes 16-18)**
```
Closing Cutscene (AI video)
    ↓
Festival memory plays
    ↓
Earthquake strikes
    ↓
Daughter and granddaughter die
    ↓
Yoru's decade of grief
    ↓
Merger with cat
    ↓
"I... I remember..."
    ↓
Fade to black
    ↓
"To Be Continued"
    ↓
Demo statistics screen:
   - Playtime
   - Rings earned (1)
   - Path chosen (Dark/Light)
   - Enemies defeated
```

---

## 🏗️ SYSTEMS ARCHITECTURE

### Complete System Diagram

```
┌────────────────────────────────────────────────────────────┐
│                     GAME MANAGER                           │
│              (Master Controller, Persistent)               │
└──────────┬─────────────────────────────────────────────────┘
           │
    ┌──────┼──────┬──────┬──────┬──────┬──────┬──────┬──────┐
    │      │      │      │      │      │      │      │      │
┌───▼──┐ ┌─▼──┐ ┌▼───┐ ┌▼───┐ ┌▼────┐ ┌▼────┐ ┌▼───┐ ┌▼────┐
│Player│ │Soul│ │Health│Karma│ │Save │ │ UI  │ │Dia-│ │Ring │
│Sys   │ │Mgr │ │ Mgr │Sys  │ │Sys  │ │Mgr  │ │logue│ │Sys │
└───┬──┘ └─┬──┘ └┬────┘ └┬───┘ └──┬──┘ └──┬──┘ └┬───┘ └┬────┘
    │      │     │       │        │       │     │     │
    │      │     │       │        │       │     │     │
┌───▼──────▼─────▼───────▼────────▼───────▼─────▼─────▼─────┐
│                    DATA LAYER                               │
│  - Player state (HP, Soul, position, form)                 │
│  - Karma points (dark/light)                               │
│  - Ring progress (abilities)                               │
│  - Story flags (bosses defeated, memories seen)            │
│  - Save data (multiple slots)                              │
└─────────────────────────────────────────────────────────────┘
```

### System Communication Flow

```
Player Input
    ↓
Action (move/attack/transform/dialogue)
    ↓
┌───────────────┬────────────────┬──────────────┐
│   Movement    │    Combat      │  Transform   │
└───────┬───────┴────────┬───────┴──────┬───────┘
        │                │              │
        ↓                ↓              ↓
   Controller      Damage System   Form Change
        │                │              │
        └────────────────┼──────────────┘
                         ↓
                  State Updates
                         ↓
        ┌────────────────┼──────────────┐
        │                │              │
   Health Mgr      Karma Sys      Soul Mgr
        │                │              │
        └────────────────┼──────────────┘
                         ↓
                    UI Updates
                         ↓
                  Save System
```

---

## 🆕 NEW SYSTEMS (BEYOND ALPHA)

### TRANSFORMATION SYSTEM

#### Purpose
Allow player to switch between cat form (combat) and old lady form (dialogue/persuasion).

#### Design Philosophy
- **Cat form** = Combat, exploration, agility
- **Old lady form** = Dialogue, persuasion, empathy
- Cannot transform during combat
- 2-second transformation animation
- Both forms share health and soul

#### TransformationManager.cs

```csharp
using UnityEngine;

public class TransformationManager : MonoBehaviour
{
    public enum PlayerForm
    {
        Cat,
        OldLady
    }
    
    [Header("Current State")]
    public PlayerForm currentForm = PlayerForm.Cat;
    
    [Header("Models")]
    public GameObject catModel;
    public GameObject oldLadyModel;
    
    [Header("Settings")]
    public float transformDuration = 2f;
    public ParticleSystem transformVFX;
    
    [Header("Audio")]
    public AudioClip transformSound;
    private AudioSource audioSource;
    
    private bool isTransforming = false;
    private bool inCombat = false;
    
    void Start()
    {
        audioSource = GetComponent<AudioSource>();
        
        // Start in cat form
        SetFormVisual(PlayerForm.Cat);
    }
    
    void Update()
    {
        // Transform hotkey (T key)
        if (Input.GetKeyDown(KeyCode.T) && !isTransforming && !inCombat)
        {
            Transform();
        }
    }
    
    public void Transform()
    {
        if (isTransforming) return;
        
        // Cannot transform in combat
        if (inCombat)
        {
            UIManager.Instance.ShowMessage("Cannot transform during combat!");
            return;
        }
        
        StartCoroutine(TransformSequence());
    }
    
    IEnumerator TransformSequence()
    {
        isTransforming = true;
        
        // Disable player controls
        GetComponent<PlayerController>().enabled = false;
        GetComponent<PlayerCombat>().enabled = false;
        
        // Play VFX
        if (transformVFX != null)
        {
            transformVFX.Play();
        }
        
        // Play sound
        if (audioSource != null && transformSound != null)
        {
            audioSource.PlayOneShot(transformSound);
        }
        
        // Screen flash effect
        UIManager.Instance.TransformationFlash();
        
        // Wait for effect
        yield return new WaitForSeconds(transformDuration * 0.5f);
        
        // Switch form
        if (currentForm == PlayerForm.Cat)
        {
            currentForm = PlayerForm.OldLady;
        }
        else
        {
            currentForm = PlayerForm.Cat;
        }
        
        SetFormVisual(currentForm);
        
        // Wait for effect end
        yield return new WaitForSeconds(transformDuration * 0.5f);
        
        // Re-enable controls
        GetComponent<PlayerController>().enabled = true;
        
        // Only enable combat in cat form
        if (currentForm == PlayerForm.Cat)
        {
            GetComponent<PlayerCombat>().enabled = true;
        }
        else
        {
            GetComponent<PlayerCombat>().enabled = false;
        }
        
        isTransforming = false;
    }
    
    void SetFormVisual(PlayerForm form)
    {
        if (form == PlayerForm.Cat)
        {
            catModel.SetActive(true);
            oldLadyModel.SetActive(false);
        }
        else
        {
            catModel.SetActive(false);
            oldLadyModel.SetActive(true);
        }
    }
    
    public void SetCombatState(bool combat)
    {
        inCombat = combat;
        
        // If entering combat while old lady, auto-transform to cat
        if (combat && currentForm == PlayerForm.OldLady)
        {
            Transform();
        }
    }
    
    public float GetPersuasionBonus()
    {
        // Old lady form gets +20% persuasion success
        return currentForm == PlayerForm.OldLady ? 0.2f : 0f;
    }
    
    public bool IsOldLadyForm()
    {
        return currentForm == PlayerForm.OldLady;
    }
}
```

#### Form Differences

**Cat Form:**
```
Pros:
+ Can use combat abilities
+ Faster movement speed (×1.0)
+ Can jump higher
+ Access to dark/light abilities

Cons:
- No persuasion bonus
- NPCs may be scared
- Less empathetic in dialogue
```

**Old Lady Form:**
```
Pros:
+ Persuasion bonus (+20%)
+ NPCs more trusting
+ Empathetic dialogue options
+ Slower, careful movement (fits character)

Cons:
- Cannot attack
- Slower movement (×0.7)
- Lower jump
- Cannot use abilities
```

#### Animation States

**Transformation Animation:**
```
States:
1. Cat Idle
2. Transform Start (crouch, glow)
3. Transform Loop (spinning particles)
4. Transform End (expand, reveal)
5. Old Lady Idle

Duration: 2 seconds total
Keyframes:
- 0.0s: Cat form
- 0.5s: Particles appear, cat crouches
- 1.0s: Screen flash, model swap
- 1.5s: Old lady rises
- 2.0s: Complete
```

---

### KARMA SYSTEM

#### Purpose
Track player's moral choices (dark vs light) affecting difficulty, dialogue, and world state.

#### Point System

**Point Sources:**

```
DARK POINTS (Violence/Selfishness):
Small enemy killed: +1
Medium enemy killed: +2
Boss fought: +4
Girl abandoned: +4
Refused to help NPC: +1

LIGHT POINTS (Mercy/Compassion):
Small enemy spared: +1 (future feature)
Medium enemy persuaded: +2 (future feature)
Boss persuaded: +4
Girl protected: +2
Helped NPC: +1
```

**Effects:**

```
Soul Regeneration Bonus:
Every 10 karma points = +1 Soul/second
(Works for BOTH dark and light!)

Persuasion Difficulty:
Dark Ratio = darkPoints / (darkPoints + lightPoints)
0.0-0.3 = Easy (3 correct answers needed)
0.4-0.6 = Medium (4 correct answers)
0.7-1.0 = Hard (5+ correct answers)

World Appearance: (Demo shows subtle hints)
High Dark: Fog thicker, colors muted
High Light: Clearer, warmer tones
Balanced: Twilight (default)
```

#### KarmaSystem.cs

```csharp
using UnityEngine;

public class KarmaSystem : MonoBehaviour
{
    public static KarmaSystem Instance { get; private set; }
    
    [Header("Karma Points")]
    public int darkPoints = 0;
    public int lightPoints = 0;
    
    [Header("Rings (Demo: Max 1)")]
    public int darkRings = 0;
    public int lightRings = 0;
    
    [Header("Boss Choices")]
    public string[] bossChoices; // "fight" or "persuade"
    
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
        
        bossChoices = new string[10]; // 10 bosses total
    }
    
    public void AddDarkPoints(int amount)
    {
        darkPoints += amount;
        UpdateSoulRegeneration();
        
        Debug.Log($"Dark points: {darkPoints}");
    }
    
    public void AddLightPoints(int amount)
    {
        lightPoints += amount;
        UpdateSoulRegeneration();
        
        Debug.Log($"Light points: {lightPoints}");
    }
    
    void UpdateSoulRegeneration()
    {
        int totalKarma = darkPoints + lightPoints;
        SoulManager.Instance.UpdateKarmaBonus(totalKarma);
    }
    
    public float GetPersuasionDifficulty()
    {
        int total = darkPoints + lightPoints;
        
        if (total == 0) return 0.5f; // Default medium
        
        float darkRatio = (float)darkPoints / total;
        return darkRatio;
    }
    
    public int GetRequiredCorrectAnswers()
    {
        float difficulty = GetPersuasionDifficulty();
        
        if (difficulty < 0.3f) return 3; // Easy
        if (difficulty < 0.7f) return 4; // Medium
        return 5; // Hard
    }
    
    public void AddRing(string type)
    {
        if (type == "dark")
        {
            darkRings++;
            AddDarkPoints(4); // Boss fight = 4 points
        }
        else if (type == "light")
        {
            lightRings++;
            AddLightPoints(4); // Boss persuaded = 4 points
        }
        
        // Increase max soul
        SoulManager.Instance.IncreaseMaxSoul(20);
        
        // Visual ring appears on tail
        RingSystem.Instance.ShowNewRing(type);
    }
    
    public void RecordBossChoice(int bossIndex, string choice)
    {
        bossChoices[bossIndex] = choice;
    }
    
    public string GetAlignment()
    {
        if (darkPoints > lightPoints * 2) return "Dark";
        if (lightPoints > darkPoints * 2) return "Light";
        return "Balanced";
    }
}
```

#### World State Changes (Demo - Subtle)

**For demo, only subtle visual hints:**

```csharp
public class WorldStateManager : MonoBehaviour
{
    [Header("Environment")]
    public Light directionalLight;
    public Material skyboxMaterial;
    public ParticleSystem fogParticles;
    
    void Update()
    {
        UpdateWorldAppearance();
    }
    
    void UpdateWorldAppearance()
    {
        string alignment = KarmaSystem.Instance.GetAlignment();
        
        switch (alignment)
        {
            case "Dark":
                // Slightly darker, cooler tones
                directionalLight.intensity = 0.7f;
                directionalLight.color = new Color(0.8f, 0.8f, 1f); // Blueish
                fogParticles.emissionRate = 50f;
                break;
                
            case "Light":
                // Slightly brighter, warmer tones
                directionalLight.intensity = 1.0f;
                directionalLight.color = new Color(1f, 0.95f, 0.9f); // Warm
                fogParticles.emissionRate = 20f;
                break;
                
            case "Balanced":
            default:
                // Default twilight
                directionalLight.intensity = 0.85f;
                directionalLight.color = new Color(1f, 0.9f, 0.85f);
                fogParticles.emissionRate = 30f;
                break;
        }
    }
}
```

---

### DIALOGUE SYSTEM

#### Purpose
Handle NPC conversations and boss persuasion puzzles with branching choices.

#### Dialogue Types

**1. Simple Dialogue (NPCs)**
- Linear conversation
- Click to advance
- No choices
- Used for Hana, tutorials

**2. Choice Dialogue (Bosses)**
- Branching options
- Player selects response
- Affects outcome
- Karma-based difficulty

#### DialogueManager.cs

```csharp
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

public class DialogueManager : MonoBehaviour
{
    public static DialogueManager Instance { get; private set; }
    
    [Header("UI References")]
    public GameObject dialogueBox;
    public TextMeshProUGUI speakerNameText;
    public TextMeshProUGUI dialogueText;
    public GameObject choicesPanel;
    public Button[] choiceButtons; // 4 max choices
    
    [Header("Settings")]
    public float textSpeed = 0.05f;
    public bool autoAdvance = false;
    
    private Queue<string> sentences;
    private bool isTyping = false;
    private bool dialogueActive = false;
    
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
        
        sentences = new Queue<string>();
    }
    
    void Start()
    {
        dialogueBox.SetActive(false);
        choicesPanel.SetActive(false);
    }
    
    void Update()
    {
        if (dialogueActive && Input.GetKeyDown(KeyCode.Space))
        {
            if (isTyping)
            {
                // Skip typing animation
                StopAllCoroutines();
                dialogueText.text = sentences.Peek();
                isTyping = false;
            }
            else
            {
                DisplayNextSentence();
            }
        }
    }
    
    public void StartDialogue(Dialogue dialogue)
    {
        dialogueActive = true;
        dialogueBox.SetActive(true);
        
        // Disable player movement
        GameObject.FindGameObjectWithTag("Player").GetComponent<PlayerController>().enabled = false;
        
        // Show cursor
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        
        speakerNameText.text = dialogue.speakerName;
        
        sentences.Clear();
        
        foreach (string sentence in dialogue.sentences)
        {
            sentences.Enqueue(sentence);
        }
        
        DisplayNextSentence();
    }
    
    public void DisplayNextSentence()
    {
        if (sentences.Count == 0)
        {
            EndDialogue();
            return;
        }
        
        string sentence = sentences.Dequeue();
        StopAllCoroutines();
        StartCoroutine(TypeSentence(sentence));
    }
    
    IEnumerator TypeSentence(string sentence)
    {
        isTyping = true;
        dialogueText.text = "";
        
        foreach (char letter in sentence.ToCharArray())
        {
            dialogueText.text += letter;
            yield return new WaitForSeconds(textSpeed);
        }
        
        isTyping = false;
    }
    
    void EndDialogue()
    {
        dialogueActive = false;
        dialogueBox.SetActive(false);
        
        // Re-enable player movement
        GameObject.FindGameObjectWithTag("Player").GetComponent<PlayerController>().enabled = true;
        
        // Hide cursor
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
    }
    
    // For choice-based dialogue
    public void ShowChoices(string[] choices, System.Action<int>[] callbacks)
    {
        choicesPanel.SetActive(true);
        
        for (int i = 0; i < choiceButtons.Length; i++)
        {
            if (i < choices.Length)
            {
                choiceButtons[i].gameObject.SetActive(true);
                choiceButtons[i].GetComponentInChildren<TextMeshProUGUI>().text = choices[i];
                
                int index = i; // Capture for lambda
                choiceButtons[i].onClick.RemoveAllListeners();
                choiceButtons[i].onClick.AddListener(() => OnChoiceSelected(index, callbacks));
            }
            else
            {
                choiceButtons[i].gameObject.SetActive(false);
            }
        }
    }
    
    void OnChoiceSelected(int choiceIndex, System.Action<int>[] callbacks)
    {
        choicesPanel.SetActive(false);
        callbacks[choiceIndex]?.Invoke(choiceIndex);
    }
}

[System.Serializable]
public class Dialogue
{
    public string speakerName;
    
    [TextArea(3, 10)]
    public string[] sentences;
}
```

#### Boss Persuasion System

**BossPersuasion.cs**
```csharp
using UnityEngine;
using System.Collections.Generic;

public class BossPersuasion : MonoBehaviour
{
    [Header("Boss Info")]
    public string bossName = "Oni";
    public int bossIndex = 0; // 0 = first boss
    
    [Header("Persuasion Questions")]
    public PersuasionQuestion[] questions;
    
    [Header("Difficulty")]
    private int requiredCorrectAnswers;
    private int currentCorrectAnswers = 0;
    private int currentQuestionIndex = 0;
    
    [Header("Callbacks")]
    public UnityEngine.Events.UnityEvent onPersuasionSuccess;
    public UnityEngine.Events.UnityEvent onPersuasionFailure;
    
    void Start()
    {
        // Calculate difficulty based on karma
        requiredCorrectAnswers = KarmaSystem.Instance.GetRequiredCorrectAnswers();
        
        // Apply transformation bonus
        TransformationManager transformMgr = GameObject.FindGameObjectWithTag("Player").GetComponent<TransformationManager>();
        if (transformMgr != null && transformMgr.IsOldLadyForm())
        {
            requiredCorrectAnswers -= 1; // Old lady form makes it easier
            requiredCorrectAnswers = Mathf.Max(2, requiredCorrectAnswers); // Min 2
        }
        
        Debug.Log($"Persuasion difficulty: Need {requiredCorrectAnswers} correct answers");
    }
    
    public void StartPersuasion()
    {
        currentQuestionIndex = 0;
        currentCorrectAnswers = 0;
        
        AskNextQuestion();
    }
    
    void AskNextQuestion()
    {
        if (currentQuestionIndex >= questions.Length)
        {
            // All questions asked, check result
            CheckPersuasionResult();
            return;
        }
        
        PersuasionQuestion question = questions[currentQuestionIndex];
        
        // Show dialogue
        Dialogue dialogue = new Dialogue();
        dialogue.speakerName = bossName;
        dialogue.sentences = new string[] { question.questionText };
        
        DialogueManager.Instance.StartDialogue(dialogue);
        
        // Show choices
        System.Action<int>[] callbacks = new System.Action<int>[question.choices.Length];
        for (int i = 0; i < callbacks.Length; i++)
        {
            int index = i;
            callbacks[i] = (choiceIndex) => OnChoiceSelected(index, question.correctChoiceIndex);
        }
        
        DialogueManager.Instance.ShowChoices(question.choices, callbacks);
    }
    
    void OnChoiceSelected(int selectedIndex, int correctIndex)
    {
        if (selectedIndex == correctIndex)
        {
            currentCorrectAnswers++;
            
            // Positive feedback
            Dialogue feedback = new Dialogue();
            feedback.speakerName = bossName;
            feedback.sentences = new string[] { questions[currentQuestionIndex].correctResponse };
            DialogueManager.Instance.StartDialogue(feedback);
        }
        else
        {
            // Negative feedback
            Dialogue feedback = new Dialogue();
            feedback.speakerName = bossName;
            feedback.sentences = new string[] { questions[currentQuestionIndex].incorrectResponse };
            DialogueManager.Instance.StartDialogue(feedback);
        }
        
        currentQuestionIndex++;
        
        // Wait then ask next question
        Invoke("AskNextQuestion", 2f);
    }
    
    void CheckPersuasionResult()
    {
        if (currentCorrectAnswers >= requiredCorrectAnswers)
        {
            // Success!
            Debug.Log("Persuasion successful!");
            KarmaSystem.Instance.AddRing("light");
            KarmaSystem.Instance.RecordBossChoice(bossIndex, "persuade");
            onPersuasionSuccess.Invoke();
        }
        else
        {
            // Failure - must fight
            Debug.Log("Persuasion failed!");
            onPersuasionFailure.Invoke();
        }
    }
}

[System.Serializable]
public class PersuasionQuestion
{
    [TextArea(2, 4)]
    public string questionText;
    
    public string[] choices; // 2-4 choices
    public int correctChoiceIndex;
    
    [TextArea(1, 3)]
    public string correctResponse;
    
    [TextArea(1, 3)]
    public string incorrectResponse;
}
```

#### Example: Oni Boss Persuasion Setup

**In Unity Inspector:**
```
BossPersuasion Component:
- Boss Name: "Oni"
- Boss Index: 0

Questions (Array, Size = 4):

Question 0:
  Question Text: "Why do you disturb my rest, Nekomata?"
  Choices:
    [0]: "I mean no harm to you"
    [1]: "This child's soul needs protecting"
    [2]: "I'll destroy you if I must"
  Correct Choice Index: 1
  Correct Response: "Protecting the innocent... perhaps you understand."
  Incorrect Response: "Empty words will not save you."

Question 1:
  Question Text: "Your claws are stained with death. How do you justify this?"
  Choices:
    [0]: "They attacked me first"
    [1]: "I seek redemption for my actions"
    [2]: "The strong survive"
  Correct Choice Index: 1
  Correct Response: "Redemption... a path I once walked..."
  Incorrect Response: "You lie to yourself."

Question 2:
  Question Text: "Why should I spare this soul when you've taken so many?"
  Choices:
    [0]: "Because I'll stop you by force"
    [1]: "Every soul deserves a chance"
    [2]: "She's just a child"
  Correct Choice Index: 2
  Correct Response: "A child... I remember children once..."
  Incorrect Response: "Mercy from a killer? Laughable."

Question 3:
  Question Text: "What would you sacrifice to save her?"
  Choices:
    [0]: "My life if necessary"
    [1]: "Anything but my freedom"
    [2]: "Why should I sacrifice anything?"
  Correct Choice Index: 0
  Correct Response: "That conviction... I shall honor it. Take the child."
  Incorrect Response: "Then you value nothing. Prepare yourself."
```

---

### BOSS ENCOUNTER SYSTEM

#### Purpose
Manage boss fight states, health, phases, and integration with persuasion/combat systems.

#### OniBoss.cs

```csharp
using UnityEngine;
using UnityEngine.Events;

public class OniBoss : MonoBehaviour
{
    public enum BossState
    {
        Inactive,
        Dialogue,
        Persuasion,
        Combat,
        Defeated
    }
    
    [Header("Boss Stats")]
    public int maxHealth = 200;
    public int currentHealth;
    public bool weakenedBySake = false;
    
    [Header("Combat")]
    public float attackRange = 3f;
    public int attackDamage = 20;
    public float attackCooldown = 2f;
    private float lastAttackTime;
    
    [Header("State")]
    public BossState currentState = BossState.Inactive;
    
    [Header("UI")]
    public GameObject bossHealthBarUI;
    public UnityEngine.UI.Slider bossHealthBar;
    public TMPro.TextMeshProUGUI bossNameText;
    
    [Header("References")]
    public BossPersuasion persuasionSystem;
    public Transform player;
    
    [Header("Events")]
    public UnityEvent onBossStart;
    public UnityEvent onBossDefeated;
    public UnityEvent onPersuaded;
    
    private Animator animator;
    
    void Start()
    {
        animator = GetComponent<Animator>();
        currentHealth = maxHealth;
        bossHealthBarUI.SetActive(false);
        
        player = GameObject.FindGameObjectWithTag("Player").transform;
    }
    
    public void StartBossEncounter()
    {
        currentState = BossState.Dialogue;
        onBossStart.Invoke();
        
        // Play boss entrance animation
        animator.SetTrigger("Enter");
        
        // Music change
        MusicManager.Instance.PlayBossMusic();
        
        // Show initial dialogue
        StartIntroDialogue();
    }
    
    void StartIntroDialogue()
    {
        Dialogue intro = new Dialogue();
        intro.speakerName = "Oni";
        intro.sentences = new string[] 
        {
            "This lost soul wanders where she shouldn't.",
            "The underworld calls for her."
        };
        
        DialogueManager.Instance.StartDialogue(intro);
        
        // After dialogue, show player choice
        Invoke("ShowPlayerChoice", 3f);
    }
    
    void ShowPlayerChoice()
    {
        string[] choices = new string[]
        {
            "Leave the child alone!", // Protect
            "Take someone else. Anyone but her.", // Abandon (dark)
            "Why do you want this child?" // Persuasion attempt
        };
        
        System.Action<int>[] callbacks = new System.Action<int>[]
        {
            (index) => OnChoice_Protect(),
            (index) => OnChoice_Abandon(),
            (index) => OnChoice_TryPersuade()
        };
        
        DialogueManager.Instance.ShowChoices(choices, callbacks);
    }
    
    void OnChoice_Protect()
    {
        // Player chose to protect - can fight OR persuade
        KarmaSystem.Instance.AddLightPoints(2); // Good intent
        
        // Save game before critical choice
        SaveSystem.Instance.SaveGame();
        
        // Offer persuasion attempt
        currentState = BossState.Persuasion;
        persuasionSystem.StartPersuasion();
    }
    
    void OnChoice_Abandon()
    {
        // Dark choice - skip fight entirely
        KarmaSystem.Instance.AddDarkPoints(4); // Heavy penalty
        
        // Girl cries and is taken
        PlayGirlBetrayalScene();
        
        // Grant dark ring without fight
        KarmaSystem.Instance.AddRing("dark");
        RingSystem.Instance.UnlockAbility("CorpseFire");
        
        // Boss leaves satisfied
        animator.SetTrigger("Leave");
        
        currentState = BossState.Defeated;
        Invoke("EndBossEncounter", 3f);
    }
    
    void OnChoice_TryPersuade()
    {
        // Opens persuasion dialogue puzzle
        currentState = BossState.Persuasion;
        persuasionSystem.StartPersuasion();
    }
    
    public void OnPersuasionSuccess()
    {
        // Persuaded successfully!
        currentState = BossState.Defeated;
        
        // Final dialogue
        Dialogue success = new Dialogue();
        success.speakerName = "Oni";
        success.sentences = new string[]
        {
            "Your conviction moves me, Nekomata.",
            "Take the child. I shall not pursue her."
        };
        
        DialogueManager.Instance.StartDialogue(success);
        
        // Grant light ring
        KarmaSystem.Instance.AddRing("light");
        RingSystem.Instance.UnlockAbility("HealingLight");
        
        // Boss leaves peacefully
        animator.SetTrigger("Leave");
        
        onPersuaded.Invoke();
        
        Invoke("EndBossEncounter", 5f);
    }
    
    public void OnPersuasionFailure()
    {
        // Failed persuasion - must fight!
        currentState = BossState.Combat;
        
        Dialogue failure = new Dialogue();
        failure.speakerName = "Oni";
        failure.sentences = new string[]
        {
            "You speak without wisdom.",
            "Prepare yourself!"
        };
        
        DialogueManager.Instance.StartDialogue(failure);
        
        Invoke("StartCombat", 2f);
    }
    
    void StartCombat()
    {
        currentState = BossState.Combat;
        
        // Show boss health bar
        bossHealthBarUI.SetActive(true);
        bossNameText.text = "ONI";
        UpdateHealthBar();
        
        // Check if player used sake
        CheckSakeWeakening();
        
        // Enable combat AI
        GetComponent<BossAI>().enabled = true;
        
        // Player can fight
        GameObject.FindGameObjectWithTag("Player").GetComponent<PlayerCombat>().enabled = true;
    }
    
    void CheckSakeWeakening()
    {
        // Check if player has sake in inventory
        if (InventoryManager.Instance.HasItem("Sake"))
        {
            // Offer sake option
            UIManager.Instance.ShowPrompt("Use Sake to weaken Oni? (Press R)", () => UseSakeOnBoss());
        }
    }
    
    void UseSakeOnBoss()
    {
        weakenedBySake = true;
        maxHealth = maxHealth / 2; // Boss health halved!
        currentHealth = maxHealth;
        UpdateHealthBar();
        
        // Remove sake from inventory
        InventoryManager.Instance.RemoveItem("Sake", 1);
        
        // Visual effect - Oni staggers
        animator.SetTrigger("Stagger");
        
        Debug.Log("Oni weakened by sake!");
    }
    
    public void TakeDamage(int damage)
    {
        if (currentState != BossState.Combat) return;
        
        currentHealth -= damage;
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);
        
        UpdateHealthBar();
        
        // Visual feedback
        StartCoroutine(DamageFlash());
        
        // Check defeat
        if (currentHealth <= 0)
        {
            BossDefeated();
        }
    }
    
    System.Collections.IEnumerator DamageFlash()
    {
        Renderer renderer = GetComponentInChildren<Renderer>();
        Color original = renderer.material.color;
        renderer.material.color = Color.red;
        yield return new WaitForSeconds(0.1f);
        renderer.material.color = original;
    }
    
    void UpdateHealthBar()
    {
        bossHealthBar.value = (float)currentHealth / maxHealth;
    }
    
    void BossDefeated()
    {
        currentState = BossState.Defeated;
        
        // Disable combat
        GetComponent<BossAI>().enabled = false;
        
        // Death animation
        animator.SetTrigger("Death");
        
        // Hide health bar
        bossHealthBarUI.SetActive(false);
        
        // Grant dark ring (fought boss)
        KarmaSystem.Instance.AddRing("dark");
        RingSystem.Instance.UnlockAbility("CorpseFire");
        
        // Fire event
        onBossDefeated.Invoke();
        
        // Music back to normal
        MusicManager.Instance.PlayExplorationMusic();
        
        // Clean up
        Invoke("EndBossEncounter", 3f);
    }
    
    void EndBossEncounter()
    {
        // Continue story
        // Play closing cutscene
        MemorySystem.Instance.PlayMemory("FestivalTragedy");
    }
    
    void PlayGirlBetrayalScene()
    {
        // TODO: Implement betrayal cutscene
        Debug.Log("Girl betrayal scene plays");
    }
}
```
**🚀 CONTINUING DEMO ARCHITECTURE - REMAINING SYSTEMS...**

---

### RING & ABILITY SYSTEM

#### Purpose
Visual representation of player's choices on Yoru's tails, unlocking abilities based on path.

#### Ring Visual Design

**Physical Representation:**
```
Yoru's Two Tails:
├─ Left Tail (Dark Path)
│  ├─ Ring 1 (Boss 1 fought) - Glows red/purple
│  ├─ Ring 2 (Boss 2 fought) - Empty in demo
│  └─ ... (Up to 10 rings max)
├─ Right Tail (Light Path)
   ├─ Ring 1 (Boss 1 persuaded) - Glows gold/white
   ├─ Ring 2 (Boss 2 persuaded) - Empty in demo
   └─ ... (Up to 10 rings max)
```

#### RingSystem.cs

```csharp
using UnityEngine;
using System.Collections.Generic;

public class RingSystem : MonoBehaviour
{
    public static RingSystem Instance { get; private set; }
    
    [Header("Visual Rings")]
    public Transform leftTail; // Parent transform for left tail rings
    public Transform rightTail; // Parent transform for right tail rings
    public GameObject ringPrefab; // Ring visual object
    
    [Header("Ring Visuals")]
    public Material darkRingMaterial; // Red/purple glow
    public Material lightRingMaterial; // Gold/white glow
    
    [Header("Abilities")]
    public List<AbilityData> darkAbilities; // 10 total
    public List<AbilityData> lightAbilities; // 10 total
    public AbilityData currentEquippedAbility;
    
    [Header("Ring Positions")]
    private Vector3[] leftTailRingPositions; // Pre-calculated positions
    private Vector3[] rightTailRingPositions;
    
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
        
        InitializeRingPositions();
    }
    
    void InitializeRingPositions()
    {
        // Calculate ring positions along tail (10 rings per tail)
        leftTailRingPositions = new Vector3[10];
        rightTailRingPositions = new Vector3[10];
        
        for (int i = 0; i < 10; i++)
        {
            // Evenly space rings along tail
            float t = i / 9f; // 0.0 to 1.0
            
            leftTailRingPositions[i] = Vector3.Lerp(
                leftTail.position, 
                leftTail.position + leftTail.forward * 2f, 
                t
            );
            
            rightTailRingPositions[i] = Vector3.Lerp(
                rightTail.position,
                rightTail.position + rightTail.forward * 2f,
                t
            );
        }
    }
    
    public void ShowNewRing(string type)
    {
        StartCoroutine(RingAppearSequence(type));
    }
    
    System.Collections.IEnumerator RingAppearSequence(string type)
    {
        // Pause game for dramatic moment
        Time.timeScale = 0.3f; // Slow-mo
        
        // Camera zoom on tail
        CinemachineImpulseSource impulse = GetComponent<CinemachineImpulseSource>();
        if (impulse != null)
        {
            impulse.GenerateImpulse();
        }
        
        // Determine which tail and ring index
        int ringIndex;
        Transform targetTail;
        Material ringMaterial;
        Vector3 ringPosition;
        
        if (type == "dark")
        {
            ringIndex = KarmaSystem.Instance.darkRings - 1;
            targetTail = leftTail;
            ringMaterial = darkRingMaterial;
            ringPosition = leftTailRingPositions[ringIndex];
        }
        else
        {
            ringIndex = KarmaSystem.Instance.lightRings - 1;
            targetTail = rightTail;
            ringMaterial = lightRingMaterial;
            ringPosition = rightTailRingPositions[ringIndex];
        }
        
        // Spawn ring with dramatic effect
        GameObject newRing = Instantiate(ringPrefab, ringPosition, Quaternion.identity, targetTail);
        newRing.GetComponent<Renderer>().material = ringMaterial;
        
        // Start small and grow
        newRing.transform.localScale = Vector3.zero;
        LeanTween.scale(newRing, Vector3.one, 1f).setEaseOutElastic();
        
        // Glow effect
        ParticleSystem particles = newRing.GetComponentInChildren<ParticleSystem>();
        if (particles != null)
        {
            particles.Play();
        }
        
        // UI notification
        UIManager.Instance.ShowRingUnlockedNotification(type, ringIndex + 1);
        
        yield return new WaitForSecondsRealtime(2f);
        
        // Resume normal time
        Time.timeScale = 1f;
    }
    
    public void UnlockAbility(string abilityName)
    {
        // Find ability in list
        AbilityData ability = null;
        
        foreach (var ab in darkAbilities)
        {
            if (ab.abilityName == abilityName)
            {
                ability = ab;
                break;
            }
        }
        
        if (ability == null)
        {
            foreach (var ab in lightAbilities)
            {
                if (ab.abilityName == abilityName)
                {
                    ability = ab;
                    break;
                }
            }
        }
        
        if (ability != null)
        {
            ability.isUnlocked = true;
            currentEquippedAbility = ability;
            
            // Update UI
            UIManager.Instance.UpdateAbilityIcon(ability);
            
            // Show tutorial
            UIManager.Instance.ShowAbilityTutorial(ability);
        }
    }
    
    public bool CanUseAbility()
    {
        if (currentEquippedAbility == null) return false;
        if (!currentEquippedAbility.isUnlocked) return false;
        
        // Check soul cost
        if (SoulManager.Instance.currentSoul < currentEquippedAbility.soulCost)
        {
            return false;
        }
        
        return true;
    }
    
    public void UseAbility()
    {
        if (!CanUseAbility()) return;
        
        // Consume soul
        SoulManager.Instance.UseSoul(currentEquippedAbility.soulCost);
        
        // Execute ability
        switch (currentEquippedAbility.abilityName)
        {
            case "CorpseFire":
                ExecuteCorpseFire();
                break;
            case "HealingLight":
                ExecuteHealingLight();
                break;
        }
    }
    
    void ExecuteCorpseFire()
    {
        // AOE damage ability
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        
        // Spawn VFX
        GameObject vfx = Instantiate(
            currentEquippedAbility.abilityVFX,
            player.transform.position,
            Quaternion.identity
        );
        
        // Play sound
        AudioSource.PlayClipAtPoint(currentEquippedAbility.abilitySound, player.transform.position);
        
        // Damage enemies in range
        Collider[] enemies = Physics.OverlapSphere(player.transform.position, 5f);
        foreach (Collider enemy in enemies)
        {
            if (enemy.CompareTag("Enemy"))
            {
                EnemyHealth enemyHealth = enemy.GetComponent<EnemyHealth>();
                if (enemyHealth != null)
                {
                    // Damage over time
                    StartCoroutine(ApplyDamageOverTime(enemyHealth, 15, 5f));
                }
            }
        }
        
        // Destroy VFX after duration
        Destroy(vfx, 5f);
    }
    
    System.Collections.IEnumerator ApplyDamageOverTime(EnemyHealth enemy, int damagePerSecond, float duration)
    {
        float elapsed = 0f;
        
        while (elapsed < duration && enemy != null)
        {
            enemy.TakeDamage(damagePerSecond);
            yield return new WaitForSeconds(1f);
            elapsed += 1f;
        }
    }
    
    void ExecuteHealingLight()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        PlayerHealth playerHealth = player.GetComponent<PlayerHealth>();
        
        // Spawn VFX
        GameObject vfx = Instantiate(
            currentEquippedAbility.abilityVFX,
            player.transform.position,
            Quaternion.identity,
            player.transform
        );
        
        // Play sound
        AudioSource.PlayClipAtPoint(currentEquippedAbility.abilitySound, player.transform.position);
        
        // Instant heal
        playerHealth.Heal(10);
        
        // Heal over time
        StartCoroutine(HealOverTime(playerHealth, 2, 5f));
        
        // Destroy VFX
        Destroy(vfx, 5f);
    }
    
    System.Collections.IEnumerator HealOverTime(PlayerHealth player, int healPerSecond, float duration)
    {
        float elapsed = 0f;
        
        while (elapsed < duration)
        {
            player.Heal(healPerSecond);
            yield return new WaitForSeconds(1f);
            elapsed += 1f;
        }
    }
}

[System.Serializable]
public class AbilityData
{
    public string abilityName;
    public string abilityType; // "dark" or "light"
    public int ringIndex; // 1-10
    
    [TextArea(2, 4)]
    public string description;
    
    public int soulCost;
    public Sprite abilityIcon;
    public GameObject abilityVFX;
    public AudioClip abilitySound;
    
    public bool isUnlocked = false;
}
```

#### Demo Abilities Setup

**Dark Ability: Corpse Fire (鬼火)**
```
Ability Data:
- Name: "CorpseFire"
- Type: "dark"
- Ring Index: 1
- Description: "Summon cursed blue flames that burn enemies' souls. Deals 15 damage per second for 5 seconds in a 5-meter radius."
- Soul Cost: 20
- Icon: [Blue flame sprite]
- VFX: [Blue particle system]
- Sound: [Whoosh + crackling fire]
```

**Light Ability: Healing Light (癒しの光)**
```
Ability Data:
- Name: "HealingLight"
- Type: "light"
- Ring Index: 1
- Description: "Channel spiritual energy to mend wounds. Instantly restores 10 HP, then heals 2 HP per second for 5 seconds (total 20 HP)."
- Soul Cost: 20
- Icon: [Golden light sprite]
- VFX: [Golden glow particles]
- Sound: [Soft chime + healing hum]
```

---

### HIDE AND SEEK SYSTEM

#### Purpose
Puzzle sequence where player finds granddaughter hiding in three locations, building to emotional reveal.

#### Design Flow

```
Stage 1: Introduction
- Objective appears: "Follow the footprints"
- Glowing footprints appear on ground
- Lead to first hiding spot

Stage 2: First Find (House)
- Girl giggling sound plays
- Player finds doll on ground
- Pick up doll = clue found
- Footprints appear leading to next spot

Stage 3: Second Find (Tree)
- Girl silhouette visible behind tree
- Approach = she runs away (short animation)
- More giggles
- Footprints to final spot

Stage 4: Third Find (Graveyard)
- No giggles this time (serious tone)
- Find girl sitting at small grave
- She's crying
- Trigger dialogue and memory

Stage 5: Memory & Boss
- Memory cutscene plays (playing with grandma)
- After memory: Dialogue begins
- Oni appears
- Boss encounter starts
```

#### HideAndSeekManager.cs

```csharp
using UnityEngine;
using System.Collections;

public class HideAndSeekManager : MonoBehaviour
{
    public static HideAndSeekManager Instance { get; private set; }
    
    public enum HideAndSeekState
    {
        NotStarted,
        FindingSpot1,
        FindingSpot2,
        FindingSpot3,
        MemoryPlaying,
        Complete
    }
    
    [Header("State")]
    public HideAndSeekState currentState = HideAndSeekState.NotStarted;
    
    [Header("Hiding Spots")]
    public Transform hidingSpot1; // House
    public Transform hidingSpot2; // Tree
    public Transform hidingSpot3; // Graveyard
    
    [Header("Girl")]
    public GameObject girlModel;
    public Transform girlSpot1Position;
    public Transform girlSpot2Position;
    public Transform girlSpot3Position;
    
    [Header("Footprints")]
    public GameObject footprintPrefab;
    public float footprintSpacing = 1f;
    private List<GameObject> activeFootprints = new List<GameObject>();
    
    [Header("Collectibles")]
    public GameObject dollPrefab;
    
    [Header("Audio")]
    public AudioClip giggleSound;
    public AudioClip cryingSound;
    private AudioSource audioSource;
    
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
        audioSource = GetComponent<AudioSource>();
        girlModel.SetActive(false);
    }
    
    public void StartHideAndSeek()
    {
        currentState = HideAndSeekState.FindingSpot1;
        
        // Update objective
        ObjectiveTracker.Instance.UpdateObjective("Follow the footprints");
        
        // Create footprints to first spot
        CreateFootprintPath(GameObject.FindGameObjectWithTag("Player").transform.position, hidingSpot1.position);
        
        // Auto-save
        SaveSystem.Instance.SaveGame();
    }
    
    void CreateFootprintPath(Vector3 start, Vector3 end)
    {
        // Clear old footprints
        foreach (GameObject fp in activeFootprints)
        {
            Destroy(fp);
        }
        activeFootprints.Clear();
        
        // Calculate path
        Vector3 direction = (end - start).normalized;
        float distance = Vector3.Distance(start, end);
        int footprintCount = Mathf.FloorToInt(distance / footprintSpacing);
        
        for (int i = 0; i < footprintCount; i++)
        {
            Vector3 position = start + direction * (i * footprintSpacing);
            position.y = 0.01f; // Slightly above ground
            
            GameObject footprint = Instantiate(footprintPrefab, position, Quaternion.LookRotation(direction));
            activeFootprints.Add(footprint);
            
            // Glow effect
            footprint.GetComponent<Renderer>().material.SetColor("_EmissionColor", Color.cyan * 2f);
        }
    }
    
    public void OnReachHidingSpot1()
    {
        if (currentState != HideAndSeekState.FindingSpot1) return;
        
        // Play giggle
        audioSource.PlayOneShot(giggleSound);
        
        // Spawn doll
        GameObject doll = Instantiate(dollPrefab, hidingSpot1.position, Quaternion.identity);
        doll.GetComponent<Collectible>().onCollected.AddListener(OnDollCollected);
        
        // Show girl briefly (silhouette)
        StartCoroutine(ShowGirlSilhouette(girlSpot1Position.position, 1f));
    }
    
    System.Collections.IEnumerator ShowGirlSilhouette(Vector3 position, float duration)
    {
        girlModel.transform.position = position;
        girlModel.SetActive(true);
        
        // Make translucent
        Renderer renderer = girlModel.GetComponent<Renderer>();
        Color color = renderer.material.color;
        color.a = 0.5f;
        renderer.material.color = color;
        
        yield return new WaitForSeconds(duration);
        
        girlModel.SetActive(false);
    }
    
    void OnDollCollected()
    {
        currentState = HideAndSeekState.FindingSpot2;
        
        // Update objective
        ObjectiveTracker.Instance.UpdateObjective("Find the girl");
        
        // Create footprints to second spot
        CreateFootprintPath(hidingSpot1.position, hidingSpot2.position);
    }
    
    public void OnReachHidingSpot2()
    {
        if (currentState != HideAndSeekState.FindingSpot2) return;
        
        // Girl runs away animation
        StartCoroutine(GirlRunsAway());
    }
    
    System.Collections.IEnumerator GirlRunsAway()
    {
        // Show girl
        girlModel.transform.position = girlSpot2Position.position;
        girlModel.SetActive(true);
        
        // Make opaque
        Renderer renderer = girlModel.GetComponent<Renderer>();
        Color color = renderer.material.color;
        color.a = 1f;
        renderer.material.color = color;
        
        // Play giggle
        audioSource.PlayOneShot(giggleSound);
        
        // Run animation
        girlModel.GetComponent<Animator>().SetTrigger("Run");
        
        // Move to third spot
        float duration = 2f;
        float elapsed = 0f;
        Vector3 startPos = girlSpot2Position.position;
        Vector3 endPos = girlSpot3Position.position;
        
        while (elapsed < duration)
        {
            girlModel.transform.position = Vector3.Lerp(startPos, endPos, elapsed / duration);
            elapsed += Time.deltaTime;
            yield return null;
        }
        
        // Hide when reached
        girlModel.SetActive(false);
        
        // Advance state
        currentState = HideAndSeekState.FindingSpot3;
        
        // Create footprints to graveyard
        CreateFootprintPath(hidingSpot2.position, hidingSpot3.position);
    }
    
    public void OnReachHidingSpot3()
    {
        if (currentState != HideAndSeekState.FindingSpot3) return;
        
        // Final spot - graveyard
        // Girl is sitting at grave, crying
        girlModel.transform.position = girlSpot3Position.position;
        girlModel.SetActive(true);
        girlModel.GetComponent<Animator>().SetTrigger("Sit");
        
        // Play crying sound
        audioSource.PlayOneShot(cryingSound);
        audioSource.loop = true;
        
        // Disable player movement temporarily
        GameObject.FindGameObjectWithTag("Player").GetComponent<PlayerController>().enabled = false;
        
        // Wait then play memory
        Invoke("PlayGranddaughterMemory", 3f);
    }
    
    void PlayGranddaughterMemory()
    {
        currentState = HideAndSeekState.MemoryPlaying;
        
        // Stop crying
        audioSource.loop = false;
        
        // Play memory cutscene
        MemorySystem.Instance.PlayMemory("GranddaughterMemory");
        
        // After memory, start dialogue
        MemorySystem.Instance.onMemoryComplete.AddListener(StartGirlDialogue);
    }
    
    void StartGirlDialogue()
    {
        Dialogue girlDialogue = new Dialogue();
        girlDialogue.speakerName = "Lost Girl";
        girlDialogue.sentences = new string[]
        {
            "...",
            "Do you... remember me?",
            "Grandma...?"
        };
        
        DialogueManager.Instance.StartDialogue(girlDialogue);
        
        // After dialogue, Oni appears
        Invoke("SummonOni", 5f);
    }
    
    void SummonOni()
    {
        currentState = HideAndSeekState.Complete;
        
        // Spawn Oni boss
        GameObject oniBoss = GameObject.FindGameObjectWithTag("Boss");
        oniBoss.GetComponent<OniBoss>().StartBossEncounter();
    }
}

public class Collectible : MonoBehaviour
{
    public UnityEngine.Events.UnityEvent onCollected;
    
    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            onCollected.Invoke();
            
            // Visual feedback
            ParticleSystem particles = GetComponentInChildren<ParticleSystem>();
            if (particles != null)
            {
                particles.transform.SetParent(null);
                particles.Play();
                Destroy(particles.gameObject, 2f);
            }
            
            Destroy(gameObject);
        }
    }
}
```

#### Hiding Spot Triggers

**HidingSpotTrigger.cs** (Attach to each spot)
```csharp
using UnityEngine;

public class HidingSpotTrigger : MonoBehaviour
{
    public int spotNumber; // 1, 2, or 3
    
    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            switch (spotNumber)
            {
                case 1:
                    HideAndSeekManager.Instance.OnReachHidingSpot1();
                    break;
                case 2:
                    HideAndSeekManager.Instance.OnReachHidingSpot2();
                    break;
                case 3:
                    HideAndSeekManager.Instance.OnReachHidingSpot3();
                    break;
            }
        }
    }
}
```

---

### OBJECTIVE TRACKER

#### Purpose
Simple, unobtrusive UI element showing current goal. Helps players who take breaks remember what to do.

#### ObjectiveTracker.cs

```csharp
using UnityEngine;
using TMPro;

public class ObjectiveTracker : MonoBehaviour
{
    public static ObjectiveTracker Instance { get; private set; }
    
    [Header("UI")]
    public GameObject objectivePanel;
    public TextMeshProUGUI objectiveText;
    
    [Header("Settings")]
    public bool showObjectives = true;
    public float fadeInDuration = 0.5f;
    public float fadeOutDuration = 0.5f;
    
    private string currentObjective = "";
    
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
        if (!showObjectives)
        {
            objectivePanel.SetActive(false);
        }
    }
    
    public void UpdateObjective(string newObjective)
    {
        if (!showObjectives) return;
        
        currentObjective = newObjective;
        
        // Fade out old text
        LeanTween.alpha(objectivePanel.GetComponent<RectTransform>(), 0f, fadeOutDuration).setOnComplete(() =>
        {
            // Update text
            objectiveText.text = "▸ " + currentObjective;
            
            // Fade in new text
            LeanTween.alpha(objectivePanel.GetComponent<RectTransform>(), 1f, fadeInDuration);
        });
        
        Debug.Log("Objective updated: " + newObjective);
    }
    
    public void ClearObjective()
    {
        currentObjective = "";
        LeanTween.alpha(objectivePanel.GetComponent<RectTransform>(), 0f, fadeOutDuration);
    }
    
    public void ToggleObjectiveDisplay(bool show)
    {
        showObjectives = show;
        objectivePanel.SetActive(show);
    }
}
```

#### Demo Objective Sequence

```
Demo Objectives (in order):
1. "Explore the shrine area" (Tutorial)
2. "Investigate the disturbance" (First enemy)
3. "Continue exploring" (Second enemy)
4. "Follow the footprints" (Hide and seek start)
5. "Find the girl" (During hide and seek)
6. "Protect the child" (Boss choice)
7. [Objective clears after boss resolved]
```

---

### MEMORY SYSTEM (EXPANDED)

#### Purpose
Play AI-generated video cutscenes at key story moments using Unity's Video Player.

#### MemorySystem.cs

```csharp
using UnityEngine;
using UnityEngine.Video;
using UnityEngine.Events;

public class MemorySystem : MonoBehaviour
{
    public static MemorySystem Instance { get; private set; }
    
    [Header("Video Player")]
    public VideoPlayer videoPlayer;
    public GameObject videoCanvas;
    
    [Header("Memory Database")]
    public MemoryData[] allMemories;
    
    [Header("Events")]
    public UnityEvent onMemoryStart;
    public UnityEvent onMemoryComplete;
    
    [Header("State")]
    private bool isPlayingMemory = false;
    
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
        videoCanvas.SetActive(false);
        videoPlayer.loopPointReached += OnVideoEnd;
    }
    
    public void PlayMemory(string memoryName)
    {
        // Find memory in database
        MemoryData memory = null;
        foreach (MemoryData m in allMemories)
        {
            if (m.memoryName == memoryName)
            {
                memory = m;
                break;
            }
        }
        
        if (memory == null)
        {
            Debug.LogError("Memory not found: " + memoryName);
            return;
        }
        
        StartCoroutine(PlayMemorySequence(memory));
    }
    
    System.Collections.IEnumerator PlayMemorySequence(MemoryData memory)
    {
        isPlayingMemory = true;
        onMemoryStart.Invoke();
        
        // Fade to black
        UIManager.Instance.FadeToBlack(1f);
        yield return new WaitForSeconds(1f);
        
        // Disable player controls
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        player.GetComponent<PlayerController>().enabled = false;
        
        // Show video canvas
        videoCanvas.SetActive(true);
        
        // Load and play video
        videoPlayer.url = System.IO.Path.Combine(Application.streamingAssetsPath, memory.videoFileName);
        videoPlayer.Prepare();
        
        while (!videoPlayer.isPrepared)
        {
            yield return null;
        }
        
        // Fade in video
        UIManager.Instance.FadeFromBlack(0.5f);
        
        videoPlayer.Play();
        
        // Wait for video to finish
        while (videoPlayer.isPlaying)
        {
            yield return null;
        }
    }
    
    void OnVideoEnd(VideoPlayer vp)
    {
        StartCoroutine(EndMemorySequence());
    }
    
    System.Collections.IEnumerator EndMemorySequence()
    {
        // Fade to black
        UIManager.Instance.FadeToBlack(1f);
        yield return new WaitForSeconds(1f);
        
        // Hide video
        videoCanvas.SetActive(false);
        
        // Re-enable player
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        player.GetComponent<PlayerController>().enabled = true;
        
        // Fade back to game
        UIManager.Instance.FadeFromBlack(1f);
        
        isPlayingMemory = false;
        onMemoryComplete.Invoke();
    }
}

[System.Serializable]
public class MemoryData
{
    public string memoryName;
    public string videoFileName; // Located in StreamingAssets folder
    
    [TextArea(2, 4)]
    public string memoryDescription;
    
    public bool hasBeenSeen = false;
}
```

#### Demo Memory List

```
Memory 1: "OpeningCinematic"
- File: opening_cutscene.mp4
- Description: "Elderly woman's loneliness, loss, transformation into Nekomata"
- Duration: 90 seconds

Memory 2: "Hitotsume_Memory"
- File: hitotsume_backstory.mp4
- Description: "One-eyed yokai was once a child, people feared him"
- Duration: 15 seconds

Memory 3: "GranddaughterMemory"
- File: granddaughter_hideandseek.mp4
- Description: "Playing hide and seek with granddaughter, warm memory"
- Duration: 30 seconds

Memory 4: "FestivalTragedy"
- File: festival_earthquake.mp4
- Description: "Festival scene, earthquake, loss of daughter and granddaughter"
- Duration: 60 seconds
```

#### Video Setup Instructions

**Unity Setup:**
```
1. Create folder: Assets/StreamingAssets/
2. Place all .mp4 videos in StreamingAssets/
3. Video Player Component settings:
   - Source: URL
   - Render Mode: Camera Far Plane
   - Target Camera: Main Camera
   - Aspect Ratio: Fit Vertically
   - Audio Output Mode: Direct
```

**Video Specifications:**
```
Format: MP4 (H.264 codec)
Resolution: 1920x1080 (Full HD)
Frame Rate: 30 FPS
Bitrate: 5-10 Mbps
Audio: AAC, 192 kbps, Stereo
```

---

### ENEMY AI SYSTEMS

#### Purpose
Intelligent enemy behavior for patrol, detection, chase, and combat.

#### EnemyAI.cs (Base class)

```csharp
using UnityEngine;
using UnityEngine.AI;

public class EnemyAI : MonoBehaviour
{
    public enum AIState
    {
        Idle,
        Patrol,
        Chase,
        Attack,
        Dead
    }
    
    [Header("AI State")]
    public AIState currentState = AIState.Patrol;
    
    [Header("Detection")]
    public float detectionRange = 10f;
    public float attackRange = 2f;
    public LayerMask playerLayer;
    
    [Header("Movement")]
    public Transform[] patrolPoints;
    public float patrolSpeed = 2f;
    public float chaseSpeed = 4f;
    public float rotationSpeed = 5f;
    
    [Header("Combat")]
    public int attackDamage = 10;
    public float attackCooldown = 2f;
    private float lastAttackTime;
    
    [Header("References")]
    protected Transform player;
    protected NavMeshAgent agent;
    protected Animator animator;
    protected EnemyHealth health;
    
    private int currentPatrolIndex = 0;
    
    protected virtual void Start()
    {
        player = GameObject.FindGameObjectWithTag("Player").transform;
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();
        health = GetComponent<EnemyHealth>();
        
        agent.speed = patrolSpeed;
        
        if (patrolPoints.Length > 0)
        {
            agent.SetDestination(patrolPoints[0].position);
        }
    }
    
    protected virtual void Update()
    {
        if (currentState == AIState.Dead) return;
        
        switch (currentState)
        {
            case AIState.Idle:
                UpdateIdle();
                break;
            case AIState.Patrol:
                UpdatePatrol();
                break;
            case AIState.Chase:
                UpdateChase();
                break;
            case AIState.Attack:
                UpdateAttack();
                break;
        }
        
        UpdateAnimations();
    }
    
    void UpdateIdle()
    {
        // Check for player
        if (CanSeePlayer())
        {
            TransitionToState(AIState.Chase);
        }
    }
    
    void UpdatePatrol()
    {
        // Check for player
        if (CanSeePlayer())
        {
            TransitionToState(AIState.Chase);
            return;
        }
        
        // Continue patrol
        if (!agent.pathPending && agent.remainingDistance < 0.5f)
        {
            GoToNextPatrolPoint();
        }
    }
    
    void GoToNextPatrolPoint()
    {
        if (patrolPoints.Length == 0) return;
        
        currentPatrolIndex = (currentPatrolIndex + 1) % patrolPoints.Length;
        agent.SetDestination(patrolPoints[currentPatrolIndex].position);
    }
    
    void UpdateChase()
    {
        float distanceToPlayer = Vector3.Distance(transform.position, player.position);
        
        // Check if in attack range
        if (distanceToPlayer <= attackRange)
        {
            TransitionToState(AIState.Attack);
            return;
        }
        
        // Check if lost player
        if (distanceToPlayer > detectionRange * 1.5f)
        {
            TransitionToState(AIState.Patrol);
            return;
        }
        
        // Chase player
        agent.SetDestination(player.position);
    }
    
    void UpdateAttack()
    {
        float distanceToPlayer = Vector3.Distance(transform.position, player.position);
        
        // Check if player escaped
        if (distanceToPlayer > attackRange * 1.2f)
        {
            TransitionToState(AIState.Chase);
            return;
        }
        
        // Stop moving
        agent.SetDestination(transform.position);
        
        // Face player
        Vector3 direction = (player.position - transform.position).normalized;
        Quaternion lookRotation = Quaternion.LookRotation(new Vector3(direction.x, 0, direction.z));
        transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, Time.deltaTime * rotationSpeed);
        
        // Attack on cooldown
        if (Time.time >= lastAttackTime + attackCooldown)
        {
            PerformAttack();
            lastAttackTime = Time.time;
        }
    }
    
    protected virtual void PerformAttack()
    {
        animator.SetTrigger("Attack");
        
        // Damage player (called from animation event)
    }
    
    // Called from animation event
    public void DealDamageToPlayer()
    {
        float distanceToPlayer = Vector3.Distance(transform.position, player.position);
        
        if (distanceToPlayer <= attackRange)
        {
            PlayerHealth playerHealth = player.GetComponent<PlayerHealth>();
            if (playerHealth != null)
            {
                playerHealth.TakeDamage(attackDamage);
            }
        }
    }
    
    bool CanSeePlayer()
    {
        float distanceToPlayer = Vector3.Distance(transform.position, player.position);
        
        if (distanceToPlayer > detectionRange) return false;
        
        // Raycast to check line of sight
        Vector3 directionToPlayer = (player.position - transform.position).normalized;
        RaycastHit hit;
        
        if (Physics.Raycast(transform.position + Vector3.up, directionToPlayer, out hit, detectionRange))
        {
            if (hit.collider.CompareTag("Player"))
            {
                return true;
            }
        }
        
        return false;
    }
    
    void TransitionToState(AIState newState)
    {
        // Exit current state
        switch (currentState)
        {
            case AIState.Patrol:
                agent.speed = chaseSpeed;
                break;
        }
        
        currentState = newState;
        
        // Enter new state
        switch (newState)
        {
            case AIState.Patrol:
                agent.speed = patrolSpeed;
                break;
            case AIState.Chase:
                agent.speed = chaseSpeed;
                // Notify music system
                MusicManager.Instance.EnterCombat();
                break;
            case AIState.Attack:
                break;
        }
    }
    
    void UpdateAnimations()
    {
        // Set animation parameters
        animator.SetFloat("Speed", agent.velocity.magnitude);
        animator.SetBool("IsChasing", currentState == AIState.Chase);
        animator.SetBool("IsAttacking", currentState == AIState.Attack);
    }
    
    public void OnDeath()
    {
        currentState = AIState.Dead;
        agent.enabled = false;
        animator.SetTrigger("Death");
        
        // Notify music system if no more enemies
        CheckCombatStatus();
    }
    
    void CheckCombatStatus()
    {
        // Count remaining enemies
        int enemyCount = GameObject.FindGameObjectsWithTag("Enemy").Length - 1; // -1 for this one
        
        if (enemyCount == 0)
        {
            MusicManager.Instance.ExitCombat();
        }
    }
    
    // Debug visualization
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);
        
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}
```

#### BossAI.cs (Boss-specific)

```csharp
using UnityEngine;

public class BossAI : EnemyAI
{
    [Header("Boss Specific")]
    public float specialAttackCooldown = 10f;
    private float lastSpecialAttackTime;
    
    public GameObject specialAttackVFX;
    public int specialAttackDamage = 40;
    
    protected override void Start()
    {
        base.Start();
        
        // Boss doesn't patrol, starts in idle
        currentState = AIState.Idle;
        agent.enabled = false; // Disabled until combat starts
    }
    
    public void StartBossCombat()
    {
        agent.enabled = true;
        TransitionToState(AIState.Chase);
    }
    
    protected override void Update()
    {
        base.Update();
        
        // Special attack on cooldown
        if (currentState == AIState.Attack && Time.time >= lastSpecialAttackTime + specialAttackCooldown)
        {
            PerformSpecialAttack();
            lastSpecialAttackTime = Time.time;
        }
    }
    
    void PerformSpecialAttack()
    {
        animator.SetTrigger("SpecialAttack");
        
        // AOE attack
        if (specialAttackVFX != null)
        {
            Instantiate(specialAttackVFX, transform.position, Quaternion.identity);
        }
        
        // Damage player if in range
        float distanceToPlayer = Vector3.Distance(transform.position, player.position);
        if (distanceToPlayer <= attackRange * 2f)
        {
            player.GetComponent<PlayerHealth>().TakeDamage(specialAttackDamage);
        }
    }
}
```

**🚀 CONTINUING DEMO ARCHITECTURE - FINAL SECTIONS...**

---

### ENHANCED SAVE SYSTEM

#### Purpose
Robust save/load functionality with multiple slots, scene persistence, and comprehensive data storage.

#### Enhanced SaveData Structure

**SaveData.cs** (Expanded)
```csharp
using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class EnhancedSaveData
{
    // Meta Information
    public int slotNumber;
    public string saveDate;
    public float totalPlaytime;
    public string currentScene;
    
    // Player Stats
    public int currentHealth;
    public int maxHealth;
    public int currentSoul;
    public int maxSoul;
    
    // Player Position
    public float posX;
    public float posY;
    public float posZ;
    public float rotY; // Rotation
    
    // Player Form
    public string currentForm; // "Cat" or "OldLady"
    
    // Karma System
    public int darkPoints;
    public int lightPoints;
    public int darkRings;
    public int lightRings;
    
    // Boss Progress
    public List<BossRecord> bossRecords;
    
    // Abilities
    public List<string> unlockedAbilities;
    public string equippedAbility;
    
    // Inventory
    public List<InventoryItemData> inventory;
    
    // Story Flags
    public List<string> completedObjectives;
    public List<string> seenMemories;
    public bool hideAndSeekComplete;
    public bool firstRingObtained;
    
    // Settings
    public float masterVolume;
    public float musicVolume;
    public float sfxVolume;
    public int graphicsQuality;
    
    // Screenshot (for save slot display)
    public string screenshotPath;
    
    public EnhancedSaveData()
    {
        bossRecords = new List<BossRecord>();
        unlockedAbilities = new List<string>();
        inventory = new List<InventoryItemData>();
        completedObjectives = new List<string>();
        seenMemories = new List<string>();
    }
}

[Serializable]
public class BossRecord
{
    public string bossName;
    public string outcome; // "fought", "persuaded", "abandoned"
    public bool defeated;
    public float defeatTime;
}

[Serializable]
public class InventoryItemData
{
    public string itemName;
    public int quantity;
}
```

#### Enhanced SaveSystem.cs

```csharp
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

public class EnhancedSaveSystem : MonoBehaviour
{
    public static EnhancedSaveSystem Instance { get; private set; }
    
    [Header("Save Settings")]
    public int maxSaveSlots = 10;
    private string saveDirectory;
    private string saveFilePrefix = "YoruSave_";
    private string saveExtension = ".json";
    private string screenshotFolder = "/Screenshots/";
    
    [Header("Auto-Save")]
    public bool autoSaveEnabled = true;
    public float autoSaveInterval = 300f; // 5 minutes
    private float lastAutoSaveTime;
    
    [Header("Current Save")]
    public int currentSaveSlot = 1;
    public EnhancedSaveData currentSaveData;
    
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
            return;
        }
        
        InitializeSaveSystem();
    }
    
    void InitializeSaveSystem()
    {
        saveDirectory = Application.persistentDataPath + "/Saves/";
        
        // Create directories if they don't exist
        if (!Directory.Exists(saveDirectory))
        {
            Directory.CreateDirectory(saveDirectory);
        }
        
        if (!Directory.Exists(Application.persistentDataPath + screenshotFolder))
        {
            Directory.CreateDirectory(Application.persistentDataPath + screenshotFolder);
        }
        
        Debug.Log("Save directory: " + saveDirectory);
    }
    
    void Update()
    {
        // Auto-save
        if (autoSaveEnabled && Time.time >= lastAutoSaveTime + autoSaveInterval)
        {
            QuickSave();
            lastAutoSaveTime = Time.time;
        }
        
        // Quick save hotkey
        if (Input.GetKeyDown(KeyCode.F5))
        {
            QuickSave();
        }
        
        // Quick load hotkey
        if (Input.GetKeyDown(KeyCode.F9))
        {
            QuickLoad();
        }
    }
    
    public void SaveGame(int slotNumber)
    {
        currentSaveSlot = slotNumber;
        
        EnhancedSaveData data = new EnhancedSaveData();
        data.slotNumber = slotNumber;
        
        // Gather all data
        CollectPlayerData(data);
        CollectProgressData(data);
        CollectInventoryData(data);
        CollectSettingsData(data);
        
        // Meta data
        data.saveDate = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        data.totalPlaytime = Time.timeSinceLevelLoad; // Simple for now
        data.currentScene = SceneManager.GetActiveScene().name;
        
        // Take screenshot for save slot
        string screenshotPath = TakeScreenshot(slotNumber);
        data.screenshotPath = screenshotPath;
        
        // Serialize to JSON
        string json = JsonUtility.ToJson(data, true);
        
        // Write to file
        string filePath = GetSaveFilePath(slotNumber);
        File.WriteAllText(filePath, json);
        
        Debug.Log($"Game saved to slot {slotNumber}");
        
        // Store current data
        currentSaveData = data;
        
        // Show UI confirmation
        UIManager.Instance.ShowSaveConfirmation($"Saved to Slot {slotNumber}");
    }
    
    void CollectPlayerData(EnhancedSaveData data)
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null) return;
        
        // Health
        PlayerHealth health = player.GetComponent<PlayerHealth>();
        data.currentHealth = health.currentHealth;
        data.maxHealth = health.maxHealth;
        
        // Soul
        data.currentSoul = SoulManager.Instance.currentSoul;
        data.maxSoul = SoulManager.Instance.maxSoul;
        
        // Position
        Transform playerTransform = player.transform;
        data.posX = playerTransform.position.x;
        data.posY = playerTransform.position.y;
        data.posZ = playerTransform.position.z;
        data.rotY = playerTransform.eulerAngles.y;
        
        // Form
        TransformationManager transformMgr = player.GetComponent<TransformationManager>();
        data.currentForm = transformMgr.currentForm.ToString();
    }
    
    void CollectProgressData(EnhancedSaveData data)
    {
        // Karma
        data.darkPoints = KarmaSystem.Instance.darkPoints;
        data.lightPoints = KarmaSystem.Instance.lightPoints;
        data.darkRings = KarmaSystem.Instance.darkRings;
        data.lightRings = KarmaSystem.Instance.lightRings;
        
        // Bosses
        for (int i = 0; i < KarmaSystem.Instance.bossChoices.Length; i++)
        {
            if (!string.IsNullOrEmpty(KarmaSystem.Instance.bossChoices[i]))
            {
                BossRecord record = new BossRecord();
                record.bossName = $"Boss_{i}";
                record.outcome = KarmaSystem.Instance.bossChoices[i];
                record.defeated = true;
                data.bossRecords.Add(record);
            }
        }
        
        // Abilities
        foreach (AbilityData ability in RingSystem.Instance.darkAbilities)
        {
            if (ability.isUnlocked)
            {
                data.unlockedAbilities.Add(ability.abilityName);
            }
        }
        foreach (AbilityData ability in RingSystem.Instance.lightAbilities)
        {
            if (ability.isUnlocked)
            {
                data.unlockedAbilities.Add(ability.abilityName);
            }
        }
        
        if (RingSystem.Instance.currentEquippedAbility != null)
        {
            data.equippedAbility = RingSystem.Instance.currentEquippedAbility.abilityName;
        }
        
        // Story flags
        data.hideAndSeekComplete = HideAndSeekManager.Instance.currentState == HideAndSeekManager.HideAndSeekState.Complete;
        data.firstRingObtained = (data.darkRings + data.lightRings) > 0;
    }
    
    void CollectInventoryData(EnhancedSaveData data)
    {
        // Get inventory items
        // (Assuming InventoryManager exists from previous work)
        if (InventoryManager.Instance != null)
        {
            foreach (var item in InventoryManager.Instance.GetAllItems())
            {
                InventoryItemData itemData = new InventoryItemData();
                itemData.itemName = item.itemName;
                itemData.quantity = item.quantity;
                data.inventory.Add(itemData);
            }
        }
    }
    
    void CollectSettingsData(EnhancedSaveData data)
    {
        // Audio settings
        data.masterVolume = AudioListener.volume;
        // (Add more settings as needed)
        
        // Graphics
        data.graphicsQuality = QualitySettings.GetQualityLevel();
    }
    
    public bool LoadGame(int slotNumber)
    {
        string filePath = GetSaveFilePath(slotNumber);
        
        if (!File.Exists(filePath))
        {
            Debug.LogWarning($"Save file not found in slot {slotNumber}");
            UIManager.Instance.ShowMessage("No save file found!");
            return false;
        }
        
        // Read file
        string json = File.ReadAllText(filePath);
        
        // Deserialize
        EnhancedSaveData data = JsonUtility.FromJson<EnhancedSaveData>(json);
        
        if (data == null)
        {
            Debug.LogError("Failed to load save data!");
            return false;
        }
        
        currentSaveSlot = slotNumber;
        currentSaveData = data;
        
        // Load scene if different
        if (SceneManager.GetActiveScene().name != data.currentScene)
        {
            // Store data for after scene loads
            PlayerPrefs.SetString("PendingLoadData", json);
            PlayerPrefs.SetInt("PendingLoadSlot", slotNumber);
            SceneManager.LoadScene(data.currentScene);
            return true;
        }
        
        // Apply data
        ApplySaveData(data);
        
        Debug.Log($"Game loaded from slot {slotNumber}");
        UIManager.Instance.ShowMessage($"Loaded Slot {slotNumber}");
        
        return true;
    }
    
    void ApplySaveData(EnhancedSaveData data)
    {
        // Apply player data
        ApplyPlayerData(data);
        ApplyProgressData(data);
        ApplyInventoryData(data);
        ApplySettingsData(data);
    }
    
    void ApplyPlayerData(EnhancedSaveData data)
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null) return;
        
        // Health
        PlayerHealth health = player.GetComponent<PlayerHealth>();
        health.currentHealth = data.currentHealth;
        health.maxHealth = data.maxHealth;
        
        // Soul
        SoulManager.Instance.currentSoul = data.currentSoul;
        SoulManager.Instance.maxSoul = data.maxSoul;
        
        // Position
        Vector3 position = new Vector3(data.posX, data.posY, data.posZ);
        player.transform.position = position;
        player.transform.eulerAngles = new Vector3(0, data.rotY, 0);
        
        // Form
        TransformationManager transformMgr = player.GetComponent<TransformationManager>();
        if (data.currentForm == "OldLady" && transformMgr.currentForm == TransformationManager.PlayerForm.Cat)
        {
            transformMgr.Transform();
        }
        
        // Update UI
        UIManager.Instance.UpdateHealthBar(data.currentHealth, data.maxHealth);
        UIManager.Instance.UpdateSoulBar(data.currentSoul, data.maxSoul);
    }
    
    void ApplyProgressData(EnhancedSaveData data)
    {
        // Karma
        KarmaSystem.Instance.darkPoints = data.darkPoints;
        KarmaSystem.Instance.lightPoints = data.lightPoints;
        KarmaSystem.Instance.darkRings = data.darkRings;
        KarmaSystem.Instance.lightRings = data.lightRings;
        
        // Bosses
        foreach (BossRecord record in data.bossRecords)
        {
            // Restore boss states
            // (Implementation depends on boss system)
        }
        
        // Abilities
        foreach (string abilityName in data.unlockedAbilities)
        {
            RingSystem.Instance.UnlockAbility(abilityName);
        }
        
        if (!string.IsNullOrEmpty(data.equippedAbility))
        {
            // Re-equip ability
            // (Implementation depends on ability system)
        }
    }
    
    void ApplyInventoryData(EnhancedSaveData data)
    {
        if (InventoryManager.Instance != null)
        {
            InventoryManager.Instance.ClearInventory();
            
            foreach (InventoryItemData itemData in data.inventory)
            {
                InventoryManager.Instance.AddItem(itemData.itemName, itemData.quantity);
            }
        }
    }
    
    void ApplySettingsData(EnhancedSaveData data)
    {
        AudioListener.volume = data.masterVolume;
        QualitySettings.SetQualityLevel(data.graphicsQuality);
    }
    
    string TakeScreenshot(int slotNumber)
    {
        string filename = $"save_{slotNumber}_screenshot.png";
        string fullPath = Application.persistentDataPath + screenshotFolder + filename;
        
        ScreenCapture.CaptureScreenshot(fullPath);
        
        return fullPath;
    }
    
    public void QuickSave()
    {
        SaveGame(currentSaveSlot);
    }
    
    public void QuickLoad()
    {
        LoadGame(currentSaveSlot);
    }
    
    public bool SaveExists(int slotNumber)
    {
        return File.Exists(GetSaveFilePath(slotNumber));
    }
    
    public EnhancedSaveData GetSaveData(int slotNumber)
    {
        string filePath = GetSaveFilePath(slotNumber);
        
        if (!File.Exists(filePath)) return null;
        
        string json = File.ReadAllText(filePath);
        return JsonUtility.FromJson<EnhancedSaveData>(json);
    }
    
    public void DeleteSave(int slotNumber)
    {
        string filePath = GetSaveFilePath(slotNumber);
        
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
            Debug.Log($"Deleted save slot {slotNumber}");
        }
        
        // Delete screenshot
        EnhancedSaveData data = GetSaveData(slotNumber);
        if (data != null && !string.IsNullOrEmpty(data.screenshotPath))
        {
            if (File.Exists(data.screenshotPath))
            {
                File.Delete(data.screenshotPath);
            }
        }
    }
    
    string GetSaveFilePath(int slotNumber)
    {
        return saveDirectory + saveFilePrefix + slotNumber + saveExtension;
    }
    
    public List<EnhancedSaveData> GetAllSaves()
    {
        List<EnhancedSaveData> saves = new List<EnhancedSaveData>();
        
        for (int i = 1; i <= maxSaveSlots; i++)
        {
            EnhancedSaveData data = GetSaveData(i);
            if (data != null)
            {
                saves.Add(data);
            }
        }
        
        return saves;
    }
}
```

#### Scene Load Handler

**OnSceneLoaded.cs**
```csharp
using UnityEngine;
using UnityEngine.SceneManagement;

public class OnSceneLoaded : MonoBehaviour
{
    void Start()
    {
        // Check if there's pending load data
        if (PlayerPrefs.HasKey("PendingLoadData"))
        {
            string json = PlayerPrefs.GetString("PendingLoadData");
            int slotNumber = PlayerPrefs.GetInt("PendingLoadSlot");
            
            EnhancedSaveData data = JsonUtility.FromJson<EnhancedSaveData>(json);
            
            // Apply data after scene loaded
            EnhancedSaveSystem.Instance.ApplySaveData(data);
            
            // Clear pending data
            PlayerPrefs.DeleteKey("PendingLoadData");
            PlayerPrefs.DeleteKey("PendingLoadSlot");
        }
    }
}
```

---

### COMPLETE UI SYSTEM

#### Purpose
Comprehensive UI covering all menus, HUD elements, and player interactions.

#### UIManager.cs (Complete)

```csharp
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

public class CompleteUIManager : MonoBehaviour
{
    public static CompleteUIManager Instance { get; private set; }
    
    [Header("HUD")]
    public GameObject hudCanvas;
    public Slider healthBar;
    public TextMeshProUGUI healthText;
    public Slider soulBar;
    public TextMeshProUGUI soulText;
    public Image lowHealthVignette;
    public GameObject abilityIconPanel;
    public Image abilityIcon;
    public TextMeshProUGUI abilityCostText;
    public TextMeshProUGUI objectiveText;
    
    [Header("Menus")]
    public GameObject pauseMenu;
    public GameObject mainMenu;
    public GameObject saveLoadMenu;
    public GameObject settingsMenu;
    public GameObject characterStatsMenu;
    public GameObject inventoryMenu;
    public GameObject journalMenu;
    public GameObject deathScreen;
    
    [Header("Notifications")]
    public GameObject notificationPanel;
    public TextMeshProUGUI notificationText;
    public GameObject ringUnlockedNotification;
    
    [Header("Fade")]
    public Image fadeImage;
    
    [Header("Boss UI")]
    public GameObject bossHealthPanel;
    public Slider bossHealthBar;
    public TextMeshProUGUI bossNameText;
    
    [Header("Save/Load Slots")]
    public Transform saveSlotContainer;
    public GameObject saveSlotPrefab;
    private List<SaveSlotUI> saveSlots = new List<SaveSlotUI>();
    
    [Header("Settings")]
    public float barLerpSpeed = 5f;
    
    private float targetHealthFill;
    private float targetSoulFill;
    private bool isPaused = false;
    
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
        InitializeUI();
    }
    
    void InitializeUI()
    {
        // Hide all menus
        pauseMenu.SetActive(false);
        saveLoadMenu.SetActive(false);
        settingsMenu.SetActive(false);
        characterStatsMenu.SetActive(false);
        inventoryMenu.SetActive(false);
        journalMenu.SetActive(false);
        deathScreen.SetActive(false);
        
        // Hide notifications
        notificationPanel.SetActive(false);
        ringUnlockedNotification.SetActive(false);
        
        // Hide boss UI
        bossHealthPanel.SetActive(false);
        
        // Setup fade
        fadeImage.color = Color.black;
        fadeImage.gameObject.SetActive(false);
        
        // Initialize save slots
        InitializeSaveSlots();
    }
    
    void Update()
    {
        // Smooth bar animations
        if (healthBar.value != targetHealthFill)
        {
            healthBar.value = Mathf.Lerp(healthBar.value, targetHealthFill, barLerpSpeed * Time.deltaTime);
        }
        
        if (soulBar.value != targetSoulFill)
        {
            soulBar.value = Mathf.Lerp(soulBar.value, targetSoulFill, barLerpSpeed * Time.deltaTime);
        }
        
        // Hotkeys
        HandleHotkeys();
    }
    
    void HandleHotkeys()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            TogglePauseMenu();
        }
        
        if (Input.GetKeyDown(KeyCode.I))
        {
            ToggleInventory();
        }
        
        if (Input.GetKeyDown(KeyCode.C))
        {
            ToggleCharacterStats();
        }
        
        if (Input.GetKeyDown(KeyCode.J))
        {
            ToggleJournal();
        }
    }
    
    // ===== HUD UPDATES =====
    
    public void UpdateHealthBar(int current, int max)
    {
        targetHealthFill = (float)current / max;
        healthText.text = $"{current} / {max}";
        
        // Low health warning
        if (current < max * 0.3f)
        {
            ShowLowHealthWarning(true);
        }
        else
        {
            ShowLowHealthWarning(false);
        }
    }
    
    public void UpdateSoulBar(int current, int max)
    {
        targetSoulFill = (float)current / max;
        soulText.text = $"{current} / {max}";
    }
    
    public void UpdateAbilityIcon(AbilityData ability)
    {
        if (ability == null)
        {
            abilityIconPanel.SetActive(false);
            return;
        }
        
        abilityIconPanel.SetActive(true);
        abilityIcon.sprite = ability.abilityIcon;
        abilityCostText.text = ability.soulCost.ToString();
    }
    
    public void ShowLowHealthWarning(bool show)
    {
        lowHealthVignette.gameObject.SetActive(show);
        
        if (show)
        {
            LeanTween.alpha(lowHealthVignette.rectTransform, 0.5f, 1f).setLoopPingPong();
        }
        else
        {
            LeanTween.cancel(lowHealthVignette.gameObject);
            lowHealthVignette.color = new Color(1, 0, 0, 0);
        }
    }
    
    // ===== MENUS =====
    
    public void TogglePauseMenu()
    {
        isPaused = !isPaused;
        pauseMenu.SetActive(isPaused);
        
        Time.timeScale = isPaused ? 0f : 1f;
        Cursor.visible = isPaused;
        Cursor.lockState = isPaused ? CursorLockMode.None : CursorLockMode.Locked;
        
        // Disable other menus if pause opened
        if (isPaused)
        {
            saveLoadMenu.SetActive(false);
            settingsMenu.SetActive(false);
            characterStatsMenu.SetActive(false);
            inventoryMenu.SetActive(false);
            journalMenu.SetActive(false);
        }
    }
    
    public void ToggleInventory()
    {
        if (deathScreen.activeSelf) return;
        
        bool active = !inventoryMenu.activeSelf;
        inventoryMenu.SetActive(active);
        
        Time.timeScale = active ? 0f : 1f;
        Cursor.visible = active;
        Cursor.lockState = active ? CursorLockMode.None : CursorLockMode.Locked;
    }
    
    public void ToggleCharacterStats()
    {
        if (deathScreen.activeSelf) return;
        
        bool active = !characterStatsMenu.activeSelf;
        characterStatsMenu.SetActive(active);
        
        if (active)
        {
            UpdateCharacterStatsDisplay();
        }
        
        Time.timeScale = active ? 0f : 1f;
        Cursor.visible = active;
        Cursor.lockState = active ? CursorLockMode.None : CursorLockMode.Locked;
    }
    
    public void ToggleJournal()
    {
        if (deathScreen.activeSelf) return;
        
        bool active = !journalMenu.activeSelf;
        journalMenu.SetActive(active);
        
        if (active)
        {
            UpdateJournalDisplay();
        }
        
        Time.timeScale = active ? 0f : 1f;
        Cursor.visible = active;
        Cursor.lockState = active ? CursorLockMode.None : CursorLockMode.Locked;
    }
    
    public void ShowSaveLoadMenu()
    {
        saveLoadMenu.SetActive(true);
        pauseMenu.SetActive(false);
        RefreshSaveSlots();
    }
    
    public void ShowSettingsMenu()
    {
        settingsMenu.SetActive(true);
        pauseMenu.SetActive(false);
    }
    
    public void ReturnToPauseMenu()
    {
        saveLoadMenu.SetActive(false);
        settingsMenu.SetActive(false);
        pauseMenu.SetActive(true);
    }
    
    // ===== CHARACTER STATS =====
    
    void UpdateCharacterStatsDisplay()
    {
        // Update health/soul display
        // Update ring visual
        // Update ability tree
        // (Implementation depends on UI layout)
    }
    
    // ===== JOURNAL =====
    
    void UpdateJournalDisplay()
    {
        // Show current objectives
        // Show completed objectives
        // Show boss history
        // Show memory collection
        // (Implementation depends on UI layout)
    }
    
    // ===== SAVE/LOAD SYSTEM =====
    
    void InitializeSaveSlots()
    {
        for (int i = 1; i <= 10; i++)
        {
            GameObject slotObj = Instantiate(saveSlotPrefab, saveSlotContainer);
            SaveSlotUI slotUI = slotObj.GetComponent<SaveSlotUI>();
            slotUI.slotNumber = i;
            saveSlots.Add(slotUI);
        }
    }
    
    void RefreshSaveSlots()
    {
        foreach (SaveSlotUI slot in saveSlots)
        {
            EnhancedSaveData data = EnhancedSaveSystem.Instance.GetSaveData(slot.slotNumber);
            slot.UpdateDisplay(data);
        }
    }
    
    // ===== NOTIFICATIONS =====
    
    public void ShowMessage(string message, float duration = 2f)
    {
        notificationText.text = message;
        notificationPanel.SetActive(true);
        
        LeanTween.alpha(notificationPanel.GetComponent<RectTransform>(), 1f, 0.3f);
        
        Invoke("HideNotification", duration);
    }
    
    void HideNotification()
    {
        LeanTween.alpha(notificationPanel.GetComponent<RectTransform>(), 0f, 0.3f).setOnComplete(() =>
        {
            notificationPanel.SetActive(false);
        });
    }
    
    public void ShowSaveConfirmation(string message = "Game Saved")
    {
        ShowMessage(message, 1.5f);
    }
    
    public void ShowRingUnlockedNotification(string ringType, int ringNumber)
    {
        ringUnlockedNotification.SetActive(true);
        
        TextMeshProUGUI notifText = ringUnlockedNotification.GetComponentInChildren<TextMeshProUGUI>();
        notifText.text = $"Ring {ringNumber} Unlocked\n{(ringType == "dark" ? "Dark Path" : "Light Path")}";
        
        LeanTween.scale(ringUnlockedNotification, Vector3.one * 1.2f, 0.5f).setEaseOutElastic();
        
        Invoke("HideRingNotification", 3f);
    }
    
    void HideRingNotification()
    {
        LeanTween.scale(ringUnlockedNotification, Vector3.zero, 0.3f).setOnComplete(() =>
        {
            ringUnlockedNotification.SetActive(false);
        });
    }
    
    public void ShowAbilityTutorial(AbilityData ability)
    {
        string tutorialText = $"New Ability Unlocked!\n\n{ability.abilityName}\n\n{ability.description}\n\nPress Q to use";
        ShowMessage(tutorialText, 5f);
    }
    
    // ===== BOSS UI =====
    
    public void ShowBossHealth(string bossName, int maxHealth)
    {
        bossHealthPanel.SetActive(true);
        bossNameText.text = bossName;
        bossHealthBar.maxValue = maxHealth;
        bossHealthBar.value = maxHealth;
    }
    
    public void UpdateBossHealth(int currentHealth)
    {
        bossHealthBar.value = currentHealth;
    }
    
    public void HideBossHealth()
    {
        bossHealthPanel.SetActive(false);
    }
    
    // ===== FADE EFFECTS =====
    
    public void FadeToBlack(float duration)
    {
        fadeImage.gameObject.SetActive(true);
        LeanTween.alpha(fadeImage.rectTransform, 1f, duration);
    }
    
    public void FadeFromBlack(float duration)
    {
        LeanTween.alpha(fadeImage.rectTransform, 0f, duration).setOnComplete(() =>
        {
            fadeImage.gameObject.SetActive(false);
        });
    }
    
    public void TransformationFlash()
    {
        StartCoroutine(FlashScreen(Color.white, 0.5f));
    }
    
    IEnumerator FlashScreen(Color color, float duration)
    {
        fadeImage.color = color;
        fadeImage.gameObject.SetActive(true);
        
        LeanTween.alpha(fadeImage.rectTransform, 1f, duration * 0.5f);
        yield return new WaitForSeconds(duration * 0.5f);
        
        LeanTween.alpha(fadeImage.rectTransform, 0f, duration * 0.5f);
        yield return new WaitForSeconds(duration * 0.5f);
        
        fadeImage.gameObject.SetActive(false);
        fadeImage.color = Color.black;
    }
    
    // ===== DEATH SCREEN =====
    
    public void ShowDeathScreen()
    {
        deathScreen.SetActive(true);
        Time.timeScale = 0f;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }
    
    public void OnLoadLastSave()
    {
        Time.timeScale = 1f;
        EnhancedSaveSystem.Instance.QuickLoad();
        deathScreen.SetActive(false);
    }
    
    public void OnQuitToMenu()
    {
        Time.timeScale = 1f;
        UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
    }
    
    // ===== BUTTON CALLBACKS =====
    
    public void OnResumeButton()
    {
        TogglePauseMenu();
    }
    
    public void OnSaveButton()
    {
        ShowSaveLoadMenu();
    }
    
    public void OnLoadButton()
    {
        ShowSaveLoadMenu();
    }
    
    public void OnSettingsButton()
    {
        ShowSettingsMenu();
    }
}
```

#### SaveSlotUI.cs

```csharp
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SaveSlotUI : MonoBehaviour
{
    public int slotNumber;
    
    [Header("UI Elements")]
    public Image screenshotImage;
    public TextMeshProUGUI slotNumberText;
    public TextMeshProUGUI locationText;
    public TextMeshProUGUI playtimeText;
    public TextMeshProUGUI dateText;
    public TextMeshProUGUI ringsText;
    public GameObject emptySlotPanel;
    public GameObject filledSlotPanel;
    public Button loadButton;
    public Button saveButton;
    public Button deleteButton;
    
    void Start()
    {
        slotNumberText.text = $"Slot {slotNumber}";
        
        loadButton.onClick.AddListener(OnLoadClick);
        saveButton.onClick.AddListener(OnSaveClick);
        deleteButton.onClick.AddListener(OnDeleteClick);
    }
    
    public void UpdateDisplay(EnhancedSaveData data)
    {
        if (data == null)
        {
            // Empty slot
            emptySlotPanel.SetActive(true);
            filledSlotPanel.SetActive(false);
            loadButton.interactable = false;
            deleteButton.interactable = false;
        }
        else
        {
            // Filled slot
            emptySlotPanel.SetActive(false);
            filledSlotPanel.SetActive(true);
            loadButton.interactable = true;
            deleteButton.interactable = true;
            
            // Update info
            locationText.text = FormatSceneName(data.currentScene);
            playtimeText.text = FormatPlaytime(data.totalPlaytime);
            dateText.text = data.saveDate;
            ringsText.text = $"{data.darkRings}D / {data.lightRings}L";
            
            // Load screenshot
            if (!string.IsNullOrEmpty(data.screenshotPath) && System.IO.File.Exists(data.screenshotPath))
            {
                byte[] fileData = System.IO.File.ReadAllBytes(data.screenshotPath);
                Texture2D texture = new Texture2D(2, 2);
                texture.LoadImage(fileData);
                screenshotImage.sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
            }
        }
    }
    
    string FormatSceneName(string sceneName)
    {
        // Convert "Alpha_ShrineArea" to "Shrine Area"
        return sceneName.Replace("_", " ").Replace("Alpha", "").Trim();
    }
    
    string FormatPlaytime(float seconds)
    {
        int hours = Mathf.FloorToInt(seconds / 3600);
        int minutes = Mathf.FloorToInt((seconds % 3600) / 60);
        return $"{hours}h {minutes}m";
    }
    
    void OnLoadClick()
    {
        EnhancedSaveSystem.Instance.LoadGame(slotNumber);
        CompleteUIManager.Instance.TogglePauseMenu();
    }
    
    void OnSaveClick()
    {
        EnhancedSaveSystem.Instance.SaveGame(slotNumber);
        UpdateDisplay(EnhancedSaveSystem.Instance.GetSaveData(slotNumber));
    }
    
    void OnDeleteClick()
    {
        // Show confirmation dialog
        // (Simple version - direct delete)
        EnhancedSaveSystem.Instance.DeleteSave(slotNumber);
        UpdateDisplay(null);
    }
}
```

---

### SCENE STRUCTURE

#### Complete Demo Scene Hierarchy

```
Demo_Scene
├─ === MANAGERS (DontDestroyOnLoad) ===
│  ├─ GameManager
│  │  └─ Scripts: GameManager, MusicManager
│  ├─ SystemManagers
│  │  ├─ SoulManager
│  │  ├─ KarmaSystem
│  │  ├─ SaveSystem (EnhancedSaveSystem)
│  │  ├─ RingSystem
│  │  ├─ DialogueManager
│  │  ├─ MemorySystem
│  │  ├─ ObjectiveTracker
│  │  └─ HideAndSeekManager
│
├─ === PLAYER ===
│  ├─ Yoru (Player)
│  │  ├─ Models
│  │  │  ├─ CatModel (active)
│  │  │  └─ OldLadyModel (inactive)
│  │  ├─ Components
│  │  │  ├─ CharacterController
│  │  │  ├─ PlayerController
│  │  │  ├─ PlayerCombat
│  │  │  ├─ PlayerHealth
│  │  │  └─ TransformationManager
│  │  └─ AttackPoint (empty transform for hitbox detection)
│  │
│  └─ CM FreeLook Camera
│     └─ CinemachineFreeLook component
│
├─ === ENVIRONMENT ===
│  ├─ Lighting
│  │  ├─ Directional Light (twilight, purple-orange)
│  │  ├─ ReflectionProbe
│  │  └─ LightProbeGroup
│  │
│  ├─ Post Processing
│  │  └─ Post-process Volume (bloom, color grading, vignette)
│  │
│  ├─ Area_01_Shrine
│  │  ├─ Ground
│  │  ├─ Shrine_Building (Eastlands asset)
│  │  ├─ Torii_Gate
│  │  ├─ Stone_Lanterns (×6)
│  │  ├─ Trees_Bamboo (×12)
│  │  ├─ Rocks_Scattered
│  │  └─ Cherry_Blossom_Tree
│  │
│  ├─ Area_02_Village_Path
│  │  ├─ Path_Ground
│  │  ├─ Houses (×3)
│  │  ├─ Fences
│  │  └─ Props
│  │
│  ├─ Area_03_Forest
│  │  ├─ Forest_Floor
│  │  ├─ Trees (×20)
│  │  ├─ Undergrowth
│  │  └─ Rocks
│  │
│  ├─ Area_04_Graveyard
│  │  ├─ Graveyard_Ground
│  │  ├─ Graves (×8)
│  │  ├─ Small_Grave (granddaughter's)
│  │  └─ Dead_Tree
│  │
│  └─ Area_05_Boss_Arena
│     ├─ Arena_Ground (circular)
│     ├─ Stone_Pillars (×8)
│     ├─ Ritual_Circle (ground decal)
│     └─ Lightning_Effects (inactive)
│
├─ === ENEMIES ===
│  ├─ Hitotsume_01
│  │  ├─ Model (Meshy generated)
│  │  ├─ NavMeshAgent
│  │  ├─ EnemyAI
│  │  ├─ EnemyHealth
│  │  └─ HealthBar_Canvas (world space)
│  │
│  ├─ Hitotsume_02
│  │  └─ [Same structure]
│  │
│  └─ Shou_Tiger (Medium enemy)
│     └─ [Same structure, different stats]
│
├─ === BOSSES ===
│  └─ Oni_Boss
│     ├─ Model
│     ├─ NavMeshAgent
│     ├─ BossAI (disabled initially)
│     ├─ OniBoss
│     ├─ EnemyHealth (200 HP)
│     └─ BossPersuasion
│
├─ === NPCs ===
│  ├─ Hana_Shrine_Maiden
│  │  ├─ Model
│  │  ├─ NPCDialogue
│  │  └─ InteractionTrigger
│  │
│  └─ Granddaughter_Ghost
│     ├─ Model
│     ├─ Animator
│     └─ Managed by HideAndSeekManager
│
├─ === TRIGGERS & CHECKPOINTS ===
│  ├─ Checkpoints
│  │  ├─ Checkpoint_Start (auto-save)
│  │  ├─ Checkpoint_BeforeHideSeek
│  │  └─ Checkpoint_BeforeBoss
│  │
│  ├─ HideAndSeek_Triggers
│  │  ├─ HidingSpot1_Trigger (house)
│  │  ├─ HidingSpot2_Trigger (tree)
│  │  └─ HidingSpot3_Trigger (graveyard)
│  │
│  └─ Area_Transitions
│     ├─ Shrine_To_Village
│     ├─ Village_To_Forest
│     └─ Forest_To_Graveyard
│
├─ === COLLECTIBLES ===
│  ├─ Sake_Bottle (pickup)
│  ├─ Peach_01 (Usagi shrine)
│  ├─ Peach_02
│  ├─ Peach_03
│  └─ Doll (hide and seek clue)
│
├─ === AUDIO ===
│  ├─ AudioSources_Music
│  │  ├─ Music_Exploration (shrine area)
│  │  ├─ Music_Combat
│  │  └─ Music_Boss
│  │
│  └─ AudioSources_Ambient
│     ├─ Wind_Loop
│     ├─ Birds_Chirping
│     └─ Water_Stream
│
├─ === UI (Canvas - Screen Space Overlay) ===
│  ├─ HUD_Canvas
│  │  ├─ HealthBar
│  │  ├─ SoulBar
│  │  ├─ AbilityIcon
│  │  ├─ ObjectiveText
│  │  └─ LowHealthVignette
│  │
│  ├─ Menu_Canvas
│  │  ├─ PauseMenu (inactive)
│  │  ├─ SaveLoadMenu (inactive)
│  │  ├─ SettingsMenu (inactive)
│  │  ├─ CharacterStatsMenu (inactive)
│  │  ├─ InventoryMenu (inactive)
│  │  ├─ JournalMenu (inactive)
│  │  └─ DeathScreen (inactive)
│  │
│  ├─ Dialogue_Canvas
│  │  ├─ DialogueBox (bottom)
│  │  └─ ChoicesPanel
│  │
│  ├─ Boss_Canvas
│  │  └─ BossHealthBar (top-center)
│  │
│  ├─ Notification_Canvas
│  │  ├─ MessagePanel
│  │  └─ RingUnlockedPanel
│  │
│  └─ Fade_Canvas
│     └─ FadeImage (full screen black)
│
└─ === NAVIGATION ===
   └─ NavMesh (baked for entire scene)
```

#### Scene Loading Flow

```
Main Menu
    ↓
Demo Scene loads
    ↓
Opening Cinematic plays
    ↓
Player spawns at Checkpoint_Start
    ↓
Gameplay begins
    ↓
[Player progresses through areas]
    ↓
Boss defeated/persuaded
    ↓
Closing Cinematic plays
    ↓
"To Be Continued" screen
    ↓
Demo Statistics displayed
    ↓
Return to Main Menu OR Continue (if full game)
```

---

### CINEMATICS INTEGRATION

#### Unity Timeline Setup

**Opening Cinematic Timeline:**
```
Timeline: Opening_Cutscene
Duration: 90 seconds

Tracks:
1. Video Track
   - Clip: opening_cutscene.mp4
   - Start: 0s, End: 90s

2. Audio Track
   - Clip: Sad_Music.mp3
   - Fade in/out

3. Activation Track
   - Disable player controls
   - Hide HUD
   - Show video canvas

4. Signal Track
   - Signal at 89s: OnCutsceneEnd()
```

**Memory Cutscenes:**
```
Timeline: Hitotsume_Memory
Duration: 15 seconds
- Short video
- Fade transitions
- Auto-continue after

Timeline: Granddaughter_Memory
Duration: 30 seconds
- Playing hide and seek
- Warm, nostalgic tone
- Dialogue after

Timeline: Festival_Tragedy
Duration: 60 seconds
- Earthquake scene
- Dramatic, emotional
- Major story beat
```

#### CutsceneController.cs

```csharp
using UnityEngine;
using UnityEngine.Playables;

public class CutsceneController : MonoBehaviour
{
    public static CutsceneController Instance { get; private set; }
    
    [Header("Timelines")]
    public PlayableDirector openingCutscene;
    public PlayableDirector hitotsumeCutscene;
    public PlayableDirector granddaughterCutscene;
    public PlayableDirector festivalCutscene;
    
    private PlayableDirector currentCutscene;
    
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
    
    public void PlayCutscene(string cutsceneName)
    {
        switch (cutsceneName)
        {
            case "Opening":
                currentCutscene = openingCutscene;
                break;
            case "Hitotsume":
                currentCutscene = hitotsumeCutscene;
                break;
            case "Granddaughter":
                currentCutscene = granddaughterCutscene;
                break;
            case "Festival":
                currentCutscene = festivalCutscene;
                break;
        }
        
        if (currentCutscene != null)
        {
            StartCoroutine(PlayCutsceneSequence());
        }
    }
    
    System.Collections.IEnumerator PlayCutsceneSequence()
    {
        // Disable player
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            player.GetComponent<PlayerController>().enabled = false;
        }
        
        // Hide HUD
        CompleteUIManager.Instance.hudCanvas.SetActive(false);
        
        // Play cutscene
        currentCutscene.Play();
        
        // Wait for completion
        while (currentCutscene.state == PlayState.Playing)
        {
            yield return null;
        }
        
        // Re-enable player
        if (player != null)
        {
            player.GetComponent<PlayerController>().enabled = true;
        }
        
        // Show HUD
        CompleteUIManager.Instance.hudCanvas.SetActive(true);
    }
}
```

**🚀 COMPLETING DEMO ARCHITECTURE - FINAL SECTIONS...**

---

## 📋 IMPLEMENTATION ROADMAP

### Complete Development Timeline (12 Weeks Post-Alpha)

#### Week 1-2: Transformation System
**Goal:** Player can switch between cat and old lady forms

**Tasks:**
- [ ] Create old lady model (commission or Meshy)
- [ ] Set up model swap system
- [ ] Implement TransformationManager.cs
- [ ] Create transformation VFX (particles, flash)
- [ ] Add transformation animation (2 seconds)
- [ ] Test form switching
- [ ] Verify combat disabled in old lady form
- [ ] Test persuasion bonus application

**Deliverable:** Working transformation system

**Testing Checklist:**
```
□ Can transform outside combat
□ Cannot transform during combat
□ Model swaps correctly
□ VFX plays smoothly
□ Controls disable appropriately
□ Camera adjusts to new model
□ Both forms share HP/Soul
```

---

#### Week 3-4: Karma & Dialogue Systems
**Goal:** Choices tracked, dialogue system functional

**Tasks:**
- [ ] Implement KarmaSystem.cs
- [ ] Test point accumulation
- [ ] Verify soul regen bonus
- [ ] Implement DialogueManager.cs
- [ ] Create dialogue UI (bottom textbox)
- [ ] Test text typing animation
- [ ] Add choice button system
- [ ] Implement BossPersuasion.cs
- [ ] Create Oni dialogue questions
- [ ] Test difficulty scaling with karma

**Deliverable:** Functional karma tracking and dialogue

**Testing Checklist:**
```
□ Karma points accumulate correctly
□ Soul regen increases with karma
□ Dialogue box displays properly
□ Text types smoothly
□ Choices appear and work
□ Player can select options
□ Persuasion difficulty scales
□ Old lady form bonus applies
```

---

#### Week 5-6: Hide and Seek Puzzle
**Goal:** Complete hide and seek sequence

**Tasks:**
- [ ] Implement HideAndSeekManager.cs
- [ ] Create footprint prefab with glow
- [ ] Set up 3 hiding spot triggers
- [ ] Place collectibles (doll)
- [ ] Script girl movement sequence
- [ ] Create girl model/animations
- [ ] Add giggles and crying sounds
- [ ] Script graveyard scene
- [ ] Integrate with memory system
- [ ] Test full sequence flow

**Deliverable:** Working puzzle from start to finish

**Testing Checklist:**
```
□ Footprints appear correctly
□ Lead to correct locations
□ Doll is collectible
□ Girl appears at spots
□ Running animation works
□ Sounds trigger properly
□ Graveyard scene emotional
□ Memory triggers after
□ Flows into boss encounter
```

---

#### Week 7-8: Boss Encounter
**Goal:** Complete Oni boss with all systems integrated

**Tasks:**
- [ ] Implement OniBoss.cs
- [ ] Implement BossAI.cs
- [ ] Create boss health bar UI
- [ ] Design boss arena environment
- [ ] Add environmental effects (lightning, fire)
- [ ] Implement sake weakening mechanic
- [ ] Script player choice (protect/abandon)
- [ ] Connect persuasion system
- [ ] Create boss combat patterns
- [ ] Add boss defeat/persuaded outcomes
- [ ] Test all three paths (fight/persuade/abandon)

**Deliverable:** Fully functional boss encounter

**Testing Checklist:**
```
□ Boss intro dialogue works
□ Player choices appear
□ Protect → fight/persuade
□ Abandon → dark ring granted
□ Persuasion puzzle functional
□ Boss fight challenging but fair
□ Sake weakens boss properly
□ All outcomes lead correctly
□ Ring appears after resolution
□ Music transitions smoothly
```

---

#### Week 9: Ring & Ability System
**Goal:** First ability unlocked and usable

**Tasks:**
- [ ] Implement RingSystem.cs
- [ ] Create ring visual prefabs
- [ ] Position rings on Yoru's tails
- [ ] Script ring appearance animation
- [ ] Create Corpse Fire ability
  - [ ] VFX (blue flames AOE)
  - [ ] Damage calculation
  - [ ] Soul cost system
- [ ] Create Healing Light ability
  - [ ] VFX (golden glow)
  - [ ] Healing calculation
  - [ ] HoT (heal over time)
- [ ] Add ability UI icon
- [ ] Implement Q hotkey
- [ ] Test both abilities

**Deliverable:** Working ring and ability system

**Testing Checklist:**
```
□ Ring appears on correct tail
□ Animation is dramatic
□ Corpse Fire deals damage
□ AOE range is correct (5m)
□ Healing Light heals properly
□ Instant + HoT works
□ Soul cost deducted
□ Q key triggers ability
□ Cannot use without enough soul
□ Abilities feel impactful
```

---

#### Week 10: Cinematics & Memory System
**Goal:** All 4 cutscenes integrated

**Tasks:**
- [ ] Generate AI videos (Veo/Runway)
  - [ ] Opening cutscene (90s)
  - [ ] Hitotsume memory (15s)
  - [ ] Granddaughter memory (30s)
  - [ ] Festival tragedy (60s)
- [ ] Process and compress videos
- [ ] Place in StreamingAssets folder
- [ ] Implement MemorySystem.cs
- [ ] Test video playback
- [ ] Add fade transitions
- [ ] Verify player control disable/enable
- [ ] Test memory triggers
- [ ] Ensure smooth flow

**Deliverable:** All cinematics playing correctly

**Testing Checklist:**
```
□ Videos load without errors
□ Playback is smooth (30 FPS min)
□ Audio syncs with video
□ Fade transitions work
□ Player controls disabled
□ Can't skip accidentally
□ HUD hides during cutscenes
□ Game resumes after
□ Emotional impact achieved
```

---

#### Week 11: Polish & Integration
**Goal:** All systems working together seamlessly

**Tasks:**
- [ ] Test full demo playthrough (multiple times)
- [ ] Balance health/soul values
- [ ] Balance enemy damage/health
- [ ] Tune boss difficulty
- [ ] Adjust karma point values
- [ ] Polish all VFX
- [ ] Add missing sound effects
- [ ] Implement music transitions
- [ ] Create demo statistics screen
- [ ] Add "To Be Continued" end screen
- [ ] Fix all major bugs
- [ ] Optimize performance

**Deliverable:** Polished, cohesive demo

**Testing Checklist:**
```
□ Complete demo playthrough (dark path)
□ Complete demo playthrough (light path)
□ Complete demo playthrough (abandon path)
□ No game-breaking bugs
□ Performance 30+ FPS
□ All systems integrated
□ Smooth transitions
□ No placeholder content
□ Professional presentation
```

---

#### Week 12: Final Testing & Release Prep
**Goal:** Demo ready for public release

**Tasks:**
- [ ] External playtesting (5-10 people)
- [ ] Gather feedback
- [ ] Fix critical bugs
- [ ] Adjust difficulty if needed
- [ ] Record gameplay footage for trailer
- [ ] Create Steam page assets
- [ ] Write Steam description
- [ ] Prepare marketing materials
- [ ] Final performance optimization
- [ ] Build executable
- [ ] Test build on multiple PCs
- [ ] Create README/instructions
- [ ] Upload to Steam/Itch.io

**Deliverable:** Released demo

**Final Testing Checklist:**
```
□ 10+ complete playthroughs
□ No crashes
□ No softlocks
□ Save/load works perfectly
□ All paths tested
□ Performance acceptable
□ Audio balanced
□ Visuals polished
□ Ready for public
```

---

## 🧪 TESTING & QA

### Testing Methodology

#### Phase 1: Unit Testing (Ongoing)
**Test each system independently as it's built**

**Player Systems:**
```
Movement Tests:
- Walk speed correct (3 m/s)
- Run speed correct (6 m/s)
- Jump height appropriate
- Gravity feels natural
- No wall clipping
- Camera follows smoothly

Combat Tests:
- Light attack damages (10 HP)
- Heavy attack damages (30 HP)
- Dodge gives i-frames (0.4s)
- Combo system works
- Hitboxes accurate
- VFX appear on hits
```

**Enemy Systems:**
```
AI Tests:
- Patrol works
- Detection range correct (10m)
- Chase activates
- Attack at range (2m)
- Returns to patrol when lost
- Death state works

Health Tests:
- Takes damage correctly
- Health bar updates
- Death animation plays
- Memory triggers after death
```

**Save System:**
```
Save Tests:
- Creates file correctly
- All data saved
- Screenshot captured
- Multiple slots work
- No data corruption

Load Tests:
- Restores all data
- Player position correct
- Stats restored
- Scene loads properly
- No errors
```

---

#### Phase 2: Integration Testing
**Test systems working together**

**Combat Flow:**
```
Test Sequence:
1. Player spawns
2. Approach enemy
3. Enemy detects and chases
4. Enter combat
5. Attack enemy
6. Enemy retaliates
7. Dodge attack
8. Defeat enemy
9. Memory plays
10. Return to exploration

Verification:
□ Music transitions (exploration → combat)
□ Health updates correctly
□ Soul regenerates
□ Karma points added
□ Memory triggers
□ Music returns to exploration
```

**Karma System:**
```
Test Sequence:
1. Start with 0 karma
2. Kill 2 small enemies (+2 dark)
3. Check soul regen (should be 10/sec base)
4. Face boss with karma
5. Persuasion should be harder (4 correct needed)
6. Transform to old lady (-1 requirement)
7. Successfully persuade or fail

Verification:
□ Points accumulate
□ Difficulty scales
□ Form bonus applies
□ Ring granted
□ Ability unlocked
```

**Hide and Seek:**
```
Test Sequence:
1. Trigger starts
2. Follow footprints
3. Find spot 1 (house)
4. Collect doll
5. Follow to spot 2 (tree)
6. Girl runs away
7. Follow to spot 3 (graveyard)
8. Memory plays
9. Dialogue appears
10. Boss encounter

Verification:
□ Each step triggers correctly
□ Cannot skip stages
□ Visual/audio feedback works
□ Emotional pacing maintained
□ Flows seamlessly
```

---

#### Phase 3: Stress Testing

**Edge Cases:**
```
Unusual Actions:
- Die during cutscene
- Save during combat
- Pause during transformation
- Use ability while damaged
- Attack during dialogue
- Transform at edge of map
- Multiple enemies at once
- Boss fight at low health
- Run out of soul mid-ability
- Spam save/load rapidly
```

**Performance Tests:**
```
Scenarios:
- Multiple particles on screen
- All enemies active
- Boss + VFX + particles
- Rapid camera movement
- Loading between areas
- Memory playback
- Save file operations
- Long play sessions (1+ hour)

Metrics:
- FPS should stay 30+ minimum
- No memory leaks
- Load times < 5 seconds
- No stuttering
- Smooth gameplay
```

---

#### Phase 4: Playtest Feedback

**Internal Testing (Week 11):**
```
Testers: You + 2-3 friends
Duration: 20-30 minutes per person
Focus Areas:
- Can complete demo?
- Difficulty appropriate?
- Controls intuitive?
- Story engaging?
- Any confusion?
- Technical issues?

Feedback Form:
1. Could you complete the demo? Y/N
2. How difficult was combat? (1-5)
3. Did you understand the story? Y/N
4. Were controls easy to learn? Y/N
5. Any bugs encountered? (describe)
6. What did you like most?
7. What needs improvement?
8. Would you wishlist? Y/N
```

**External Testing (Week 12):**
```
Testers: 5-10 strangers (Reddit, Discord)
Duration: Full demo + survey
Incentive: Thank them in credits

Key Questions:
- Completion rate (how many finish?)
- Average playtime
- Path chosen (dark/light/abandon?)
- Engagement (did they care about story?)
- Technical issues (crashes, bugs)
- Wishlist interest (would they buy?)

Success Metrics:
- 80%+ completion rate
- 90%+ say controls work well
- 70%+ emotionally engaged
- 60%+ would wishlist
- < 5% encounter critical bugs
```

---

### Bug Tracking Template

**Bug Report Format:**
```
BUG ID: DEMO-001
Severity: Critical / Major / Minor
Category: Gameplay / UI / Audio / Visual / Performance

Title: [Short description]

Steps to Reproduce:
1. [Action 1]
2. [Action 2]
3. [Action 3]

Expected Result:
[What should happen]

Actual Result:
[What actually happens]

Frequency: Always / Sometimes / Rare

Environment:
- Unity Version: 2022.3.XX
- Build Type: Editor / Standalone
- OS: Windows 10/11

Screenshots/Video:
[Attach if possible]

Status: Open / In Progress / Fixed / Won't Fix
Assigned To: [Your name]
Fix Date: [When fixed]
```

**Example Bugs:**
```
BUG-001: Player falls through floor in graveyard
Severity: Critical
Status: Fixed

BUG-002: Boss health bar doesn't hide after victory
Severity: Major
Status: Fixed

BUG-003: Footprints sometimes invisible
Severity: Major
Status: Fixed

BUG-004: Text typing sound too loud
Severity: Minor
Status: Fixed

BUG-005: Soul bar flickers at exactly 100/300
Severity: Minor
Status: Won't Fix (not noticeable)
```

---

## 📦 ASSET REQUIREMENTS

### Complete Asset List

#### 3D MODELS

**Player Character:**
```
Yoru (Cat Form)
- Source: Freelancer commission
- Cost: $650 (modeling) + $600 (animation)
- Format: FBX
- Polycount: 15,000 tris
- Textures: 2048×2048 (Diffuse, Normal, Metallic)
- Rig: Humanoid, facial bones for expressions
- Tails: Rigged separately (2 tails, 5 bones each)

Yoru (Old Lady Form)
- Source: Same freelancer
- Cost: $500 (shares skeleton with cat)
- Format: FBX
- Polycount: 12,000 tris
- Textures: 2048×2048
- Rig: Same skeleton as cat form
- Tails: Visible, same rig
```

**Enemies:**
```
Hitotsume-kozō (One-eyed Yokai)
- Source: Meshy AI generation
- Cost: $30 (Meshy credits)
- Format: FBX/OBJ
- Polycount: 8,000 tris
- Textures: 1024×1024
- Rig: Humanoid (for Mixamo)

Shou (Ink Tiger)
- Source: Meshy AI generation
- Cost: $30
- Format: FBX
- Polycount: 10,000 tris
- Textures: 1024×1024
- Rig: Quadruped custom

Oni (Boss)
- Source: Asset Store OR Meshy
- Cost: $50-100
- Format: FBX
- Polycount: 20,000 tris
- Textures: 2048×2048
- Rig: Humanoid
```

**NPCs:**
```
Hana (Shrine Maiden)
- Source: Asset Store
- Cost: $25
- Format: FBX
- Polycount: 8,000 tris

Granddaughter Ghost
- Source: Modify existing child model
- Cost: $20
- Format: FBX
- Polycount: 5,000 tris
- Alpha materials for ghost effect
```

**Props & Collectibles:**
```
Sake Bottle - $0 (free asset)
Peach (3D) - $0 (free asset)
Doll - $5 (simple model)
Footprint Decal - $0 (create in Photoshop)
```

**Total 3D Model Costs: ~$2,010**

---

#### ANIMATIONS

**Player Animations (Freelancer):**
```
Cat Form (13 animations):
1. Idle - 60 frames
2. Walk - 30 frames loop
3. Run - 24 frames loop
4. Jump - 30 frames
5. Fall - 20 frames loop
6. Land - 15 frames
7. Attack_Light_1 - 18 frames
8. Attack_Light_2 - 18 frames
9. Attack_Light_3 - 22 frames
10. Attack_Heavy - 30 frames
11. Dodge - 15 frames
12. Hit_Reaction - 12 frames
13. Death - 60 frames

Old Lady Form (6 animations):
1. Idle - 60 frames
2. Walk - 40 frames loop
3. Talk - 60 frames loop
4. Sit - 30 frames
5. Transform_Start - 30 frames
6. Transform_End - 30 frames

Transformation (2 animations):
1. Cat_To_Lady - 60 frames
2. Lady_To_Cat - 60 frames

Total Animations: 21
Cost: $600 (bulk rate from freelancer)
```

**Enemy Animations (Mixamo - FREE):**
```
All Enemies:
1. Idle
2. Walk
3. Run
4. Attack
5. Hit_Reaction
6. Death

Boss Additional:
7. Roar/Taunt
8. Special_Attack
9. Stagger
10. Victory

Cost: $0 (Mixamo free)
```

**Total Animation Costs: $600**

---

#### VFX & PARTICLES

**Particle Systems:**
```
Blue Flame (Nekomata signature)
- Base: Unity Particle System
- Texture: Create in Photoshop
- Color: Blue → Purple gradient
- Cost: $0 (custom)

Healing Light
- Base: Unity Particle System
- Texture: Soft glow
- Color: Gold → White
- Cost: $0 (custom)

Hit Sparks
- Source: Free asset (Cartoon FX)
- Cost: $0

Blood/Spirit Particles (enemy death)
- Source: Free asset
- Cost: $0

Transformation VFX
- Source: Unity VFX Graph
- Custom created
- Cost: $0

Cherry Blossom Petals
- Source: Free asset
- Cost: $0

Footprint Glow
- Source: Emission shader
- Cost: $0

Lightning (boss arena)
- Source: Free asset (Nature VFX)
- Cost: $0
```

**VFX Packs (Optional):**
```
Japanese VFX Pack (Asset Store)
- Includes: Ofuda, spirit flames, sakura
- Cost: $25
- Adds polish to abilities
```

**Total VFX Costs: $0-25**

---

#### UI GRAPHICS

**HUD Elements:**
```
Health Bar Frame - Create in Figma/Photoshop
Soul Bar Frame - Create in Figma
Ability Icon Frame - Create in Figma
Low Health Vignette - Red radial gradient

Fonts:
- UI Text: Noto Sans JP (Free, Google Fonts)
- Headers: Japanese-style font (Free)
- Dialogue: Readable serif (Free)

Icons:
- Health icon (heart/kanji) - Create or free asset
- Soul icon (flame/spirit) - Create or free asset
- Ability icons - Commission or create (2 icons)

Cost per icon if commissioned: $10
Total Icons needed: ~10
Cost: $100 if commissioned, $0 if self-made
```

**Menu Graphics:**
```
Button Normal - Rounded rect with border
Button Hover - Glow effect
Button Pressed - Darker shade
Panel Background - Semi-transparent dark
Menu Background - Blurred game view

All created in Figma/Photoshop
Cost: $0 (DIY) or $50-100 if commissioned
```

**Total UI Costs: $0-200**

---

#### AUDIO

**Music (3 tracks minimum):**
```
Option A: AI-Generated (Suno/Udio)
- Exploration Theme (2 min loop)
- Combat Theme (1 min loop)
- Boss Theme (2 min loop)
Cost: $10/month subscription = $10

Option B: Royalty-Free (Epidemic Sound, Artlist)
- High quality, curated
- Subscription: $15/month
Cost: $15

Option C: Commissioned Composer
- Custom, perfect fit
- Cost per track: $200-500
Total Cost: $600-1500

Recommended: Option A for demo (cheap, fast)
Then Option C for full game (quality)

Demo Music Cost: $10-15
```

**Sound Effects (50+ needed):**
```
Player Sounds:
- Footsteps (grass, stone, wood) - 9 variations
- Attack whooshes (light, heavy) - 6 variations
- Dodge sound - 2 variations
- Jump/land - 2 sounds
- Hurt sounds - 5 variations
- Death sound - 1

Enemy Sounds:
- Footsteps (per enemy type) - 6 total
- Attack sounds - 6 variations
- Hurt/death - 6 variations
- Idle sounds (breathing, growls) - 3

Boss Sounds:
- Roar - 2 variations
- Special attack - 2
- Death cry - 1

Ability Sounds:
- Corpse Fire - 1
- Healing Light - 1
- Transformation - 1

UI Sounds:
- Click - 1
- Hover - 1
- Notification - 1
- Save confirmation - 1

Environmental:
- Wind ambience - 1 loop
- Birds chirping - 1 loop
- Water stream - 1 loop

Dialogue:
- Text typing sound - 1
- Dialogue open/close - 2

Source Options:
A) Freesound.org (Free, find & download)
B) Sound effects pack (Asset Store, $20-50)
C) Generate with AI (ElevenLabs, experimental)

Recommended: Freesound + Asset Store pack

Total SFX Cost: $20-50
```

**Voice Acting (Optional for full game):**
```
Demo: No voice acting (text only)
Full Game: Consider for major characters
Cost per line: $1-5
Total lines (full game): ~500
Cost: $500-2500 (future consideration)
```

**Total Audio Costs: $30-65 for demo**

---

#### ENVIRONMENT ASSETS

**Eastlands Pack (Already Purchased ✅):**
```
Includes:
- Japanese temple buildings
- Torii gates
- Stone lanterns
- Bamboo trees
- Cherry blossom trees
- Rocks and ground textures
- Props (benches, barrels, etc.)

Cost: $40 (paid)
Sufficient for: Entire demo environment
```

**Additional Assets Needed:**
```
Graveyard Props
- Gravestones (×10) - Free asset
- Dead tree - Free asset

Boss Arena
- Ritual circle decal - Create in Photoshop
- Stone pillars - Included in Eastlands

Fog/Atmosphere
- Unity built-in fog
- Particle fog - Create custom

Cost: $0 (all free or included)
```

**Total Environment Costs: $40 (already paid)**

---

#### VIDEOS (AI-Generated Cinematics)

**AI Video Generation:**
```
Service: Runway Gen-3 OR Veo 2
Cost per second: ~$0.50-1.00
Quality: 1080p, 30 FPS

Opening Cutscene:
- Duration: 90 seconds
- Cost: $45-90

Hitotsume Memory:
- Duration: 15 seconds
- Cost: $8-15

Granddaughter Memory:
- Duration: 30 seconds
- Cost: $15-30

Festival Tragedy:
- Duration: 60 seconds
- Cost: $30-60

Total Duration: 195 seconds (3.25 minutes)
Total Cost: $98-195

Add 20% for iterations/retakes: $118-234

Recommended Budget: $250 for all cinematics
```

**Total Video Costs: $200-250**

---

### COMPLETE DEMO BUDGET SUMMARY

```
=== DEMO ASSET COSTS ===

3D Models:
- Yoru (Cat + Old Lady): $1,750
- Enemies (Hitotsume, Shou): $60
- Oni Boss: $75
- NPCs (Hana, Girl): $45
- Props: $5
Subtotal: $1,935

Animations:
- Player animations (freelancer): $600
- Enemy animations (Mixamo): $0
Subtotal: $600

VFX:
- Particle systems (custom): $0
- Optional VFX pack: $25
Subtotal: $25

UI:
- Icons and graphics: $100
Subtotal: $100

Audio:
- Music (AI-generated): $15
- Sound effects: $50
Subtotal: $65

Environment:
- Eastlands pack: $40 (paid)
Subtotal: $40

Videos:
- AI cinematics (4 total): $250
Subtotal: $250

=== TOTAL DEMO COSTS: $3,015 ===

Software/Tools:
- Unity (free Personal license): $0
- Meshy subscription: $30/month × 2 = $60
- Veo/Runway credits: Included in video costs
- ChatGPT Plus (for assistance): $40
Subtotal: $100

=== GRAND TOTAL: $3,115 ===
```

**Out of $60,000 full game budget: 5.2%**

**Remaining for full game development: $56,885**

---

## 📊 ASSET PIPELINE WORKFLOW

### Model Pipeline

```
1. CONCEPT
   ↓
[Sketch/reference images]
   ↓
2. MODELING
   ↓
Freelancer (Yoru) OR Meshy AI (Enemies)
   ↓
3. REVIEW
   ↓
Check polycount, proportions, details
   ↓
4. TEXTURING
   ↓
2K textures, PBR workflow
   ↓
5. RIGGING
   ↓
Humanoid rig, bone weights
   ↓
6. IMPORT TO UNITY
   ↓
Configure rig, materials, LODs
   ↓
7. TEST IN-GAME
   ↓
Verify animations, shaders, performance
   ↓
8. ITERATE IF NEEDED
```

### Animation Pipeline

```
1. REFERENCE
   ↓
Video reference for movement
   ↓
2. ANIMATE
   ↓
Freelancer (custom) OR Mixamo (generic)
   ↓
3. EXPORT
   ↓
FBX with baked animations
   ↓
4. IMPORT
   ↓
Unity Animation Importer
   ↓
5. ANIMATOR SETUP
   ↓
Create Animation Controller
   ↓
6. TRANSITIONS
   ↓
Blend between animations
   ↓
7. EVENTS
   ↓
Add Animation Events for gameplay
   ↓
8. TEST
   ↓
Verify smooth transitions, timing
```

### VFX Pipeline

```
1. CONCEPT
   ↓
Reference images/videos
   ↓
2. CREATE
   ↓
Unity Particle System OR VFX Graph
   ↓
3. TEXTURES
   ↓
Create sprite sheets if needed
   ↓
4. TIMING
   ↓
Adjust emission, lifetime, speed
   ↓
5. INTEGRATION
   ↓
Attach to abilities/attacks
   ↓
6. OPTIMIZATION
   ↓
Reduce particle count if needed
   ↓
7. POLISH
   ↓
Add glow, trails, distortion
```

### Audio Pipeline

```
1. SOURCE
   ↓
Record/download/generate sound
   ↓
2. EDIT
   ↓
Audacity: trim, normalize, effects
   ↓
3. FORMAT
   ↓
Export as WAV or OGG
   ↓
4. IMPORT
   ↓
Unity Audio Importer settings
   ↓
5. IMPLEMENTATION
   ↓
AudioSource components
   ↓
6. MIXING
   ↓
Volume levels, 3D sound settings
   ↓
7. TEST
   ↓
Verify no clipping, good balance
```

---

## ✅ DEFINITION OF DONE

### Demo is Complete When:

**Technical Requirements:**
- [ ] Runs at 30+ FPS on mid-tier PC
- [ ] No game-breaking bugs
- [ ] No crashes during normal play
- [ ] Save/load works 100% of time
- [ ] All systems integrated
- [ ] Performance optimized
- [ ] Build size < 2GB

**Content Requirements:**
- [ ] 15-20 minutes playtime
- [ ] All 4 cinematics included
- [ ] 3 enemy types functional
- [ ] 1 boss encounter complete
- [ ] 1 ring system working
- [ ] 1 ability per path unlocked
- [ ] Hide and seek puzzle complete
- [ ] Tutorial clear and helpful

**Quality Requirements:**
- [ ] Visuals polished (no placeholders)
- [ ] Audio mixed and balanced
- [ ] UI clean and functional
- [ ] Controls responsive
- [ ] Story emotionally engaging
- [ ] Professional presentation
- [ ] Cohesive art style

**Player Experience:**
- [ ] Tutorial teaches mechanics
- [ ] Difficulty balanced
- [ ] Clear objectives
- [ ] Satisfying combat
- [ ] Emotional story moments
- [ ] Meaningful choices
- [ ] Desire to see more (full game)

**Marketing Ready:**
- [ ] Gameplay footage recordable
- [ ] Screenshot-worthy moments
- [ ] Steam page assets ready
- [ ] Trailer can be made
- [ ] Wishlists can be collected

---

## 🎯 SUCCESS METRICS

### Internal Metrics (You)

```
Development:
□ Completed in 12 weeks
□ Stayed under $3,200 budget
□ No scope creep
□ All planned features included
□ Clean, maintainable code

Quality:
□ 0 critical bugs
□ < 5 major bugs at release
□ < 20 minor bugs
□ 30+ FPS average
□ Load times < 5 seconds
```

### External Metrics (Players)

```
Completion:
Target: 80%+ finish the demo
Measure: Track end screen reached

Engagement:
Target: 70%+ emotionally invested
Measure: Survey responses

Conversion:
Target: 60%+ would wishlist
Measure: Steam wishlist rate

Path Distribution:
- Light path: 40-50%
- Dark path: 30-40%
- Abandon path: 10-20%
Measure: Analytics tracking

Technical:
Target: < 5% encounter critical bugs
Measure: Bug reports

Satisfaction:
Target: 4+ / 5 average rating
Measure: Feedback forms
```

### Startup Visa Metrics

```
For Application:
□ Working demo exists
□ Gameplay video recorded
□ Professional presentation
□ Technical architecture documented
□ Development roadmap clear
□ Budget/timeline realistic
□ AI integration proven
□ Market interest shown (wishlists)

For Approval:
Target: Show technical capability + market potential
Success: Visa approved → Full game development
```

---

## 📝 FINAL NOTES

### Known Limitations (Demo Scope)

**Intentionally Excluded:**
- Multiple save slots (just quick save for demo)
- Full inventory system (just sake and peach)
- Map system (linear path, no map needed)
- Complex puzzle variety (just hide and seek)
- Multiple abilities (just 1 per path)
- World state changes (subtle hints only)
- NPC relationship system (Hana is simple)
- Advanced dialogue branching (boss only)

**These are for FULL GAME:**
- Will be expanded post-demo
- Foundation is built for scaling
- Demo proves systems work
- Full features in 18-month plan

### Post-Demo Expansion Plan

**Immediate (Month 1-2 after visa):**
- Add 2 more enemy types
- Implement 2 more boss encounters
- Expand map (2 more areas)
- Add inventory system fully

**Medium (Month 3-6):**
- Complete all 10 bosses
- Implement all 20 abilities
- Build dynamic world system
- Advanced dialogue system
- Complete NPC cast

**Long-term (Month 7-12):**
- Multiple endings implementation
- Polish and balancing
- Additional content (side quests)
- Console optimization (if targeting)
- Full testing and QA

**Final (Month 13-18):**
- Marketing campaign
- Press coverage
- Steam launch
- Post-launch support

---

## 🎉 CONCLUSION

### Demo Represents:

**10% of Content:**
- 1 of 10 rings
- 2 of 20 abilities  
- 3 of ~15 enemy types
- 1 of 10 major bosses
- 4 of ~30 memories

**100% of Systems:**
- ✅ All core mechanics
- ✅ All technical systems
- ✅ Complete architecture
- ✅ Scalable foundation
- ✅ Production pipeline

**The demo is NOT the game.**
**The demo is PROOF you can make the game.**

This is exactly what startup visa needs:
- Technical competence ✅
- Clear vision ✅
- Realistic plan ✅
- Market potential ✅
- Innovation (AI) ✅

---

**END OF DEMO ARCHITECTURE DOCUMENT**

**Total Pages: 36**

---

## 📋 DOCUMENT SUMMARY

**What You Now Have:**

1. **Alpha Architecture** (13 pages)
   - 6-week foundation build
   - Core systems only
   - Proof of concept

2. **Demo Architecture** (36 pages) ← THIS DOCUMENT
   - Complete 20-minute demo
   - All systems integrated
   - Visa-ready presentation

3. **Full Game Architecture** (Coming next)
   - 50-70 pages
   - Complete roadmap
   - All 10 bosses, 20 abilities, 7 endings

