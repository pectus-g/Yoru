using UnityEngine;
using UnityEngine.UI;

public class SoulIconUI : MonoBehaviour
{
    [Header("Soul Sprites")]
    public Sprite emptySoul;
    public Sprite quarterSoul;
    public Sprite halfSoul;
    public Sprite threeQuarterSoul;
    public Sprite fullSoul;
    
    private Image[] soulImages;
    
    void Start()
    {
        soulImages = new Image[3]; // 3 soul icons
        
        for (int i = 0; i < 3; i++)
        {
            GameObject soul = new GameObject($"Soul_{i}");
            soul.transform.SetParent(transform);
            soul.transform.localScale = Vector3.one;
            
            Image img = soul.AddComponent<Image>();
            img.sprite = fullSoul;
            img.preserveAspect = true;
            
            RectTransform rt = soul.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(60, 60);
            rt.anchoredPosition = new Vector2(i * 70, 0);
            
            soulImages[i] = img;
        }
        
        Debug.Log("🔮 3 soul icons created!");
    }
    
    public void UpdateSoul(int currentSoul)
    {
        Debug.Log($"🔮 UpdateSoul: {currentSoul}/12");
        
        // Soul 2 (rightmost): 9-12
        int soul2HP = Mathf.Clamp(currentSoul - 8, 0, 4);
        soulImages[2].sprite = GetSprite(soul2HP);
        
        // Soul 1 (middle): 5-8
        int soul1HP = Mathf.Clamp(currentSoul - 4, 0, 4);
        soulImages[1].sprite = GetSprite(soul1HP);
        
        // Soul 0 (leftmost): 1-4
        int soul0HP = Mathf.Clamp(currentSoul, 0, 4);
        soulImages[0].sprite = GetSprite(soul0HP);
    }
    
    Sprite GetSprite(int amount)
    {
        if (amount == 4) return fullSoul;
        if (amount == 3) return threeQuarterSoul;
        if (amount == 2) return halfSoul;
        if (amount == 1) return quarterSoul;
        return emptySoul;
    }
}