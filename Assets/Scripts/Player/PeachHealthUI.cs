using UnityEngine;
using UnityEngine.UI;

public class PeachHealthUI : MonoBehaviour
{
    [Header("Peach Sprites")]
    public Sprite emptyPeach;
    public Sprite quarterPeach;
    public Sprite halfPeach;
    public Sprite threeQuarterPeach;
    public Sprite fullPeach;
    
    private Image[] peachImages;
    
    void Awake()
    {
        peachImages = new Image[3];
        
        for (int i = 0; i < 3; i++)
        {
            GameObject peach = new GameObject($"Peach_{i}");
            peach.transform.SetParent(transform);
            peach.transform.localScale = Vector3.one;
            
            Image img = peach.AddComponent<Image>();
            img.sprite = fullPeach;
            img.preserveAspect = true;
            
            RectTransform rt = peach.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(60, 60);
            rt.anchoredPosition = new Vector2(i * 70, 0);
            
            peachImages[i] = img;
        }
        
        Debug.Log("🍑 3 peaches created!");
    }
    
    public void UpdateHealth(int currentHP)
    {
        if (peachImages == null) return;
        
        Debug.Log($"🍑 UpdateHealth: {currentHP}/24 HP");
        
        // Peach 2 (rightmost): HP 17-24
        int peach2HP = Mathf.Clamp(currentHP - 16, 0, 8);
        peachImages[2].sprite = GetSprite(peach2HP);
        
        // Peach 1 (middle): HP 9-16
        int peach1HP = Mathf.Clamp(currentHP - 8, 0, 8);
        peachImages[1].sprite = GetSprite(peach1HP);
        
        // Peach 0 (leftmost): HP 1-8
        int peach0HP = Mathf.Clamp(currentHP, 0, 8);
        peachImages[0].sprite = GetSprite(peach0HP);
        
        Debug.Log($"  Peach 0: {peach0HP}/8, Peach 1: {peach1HP}/8, Peach 2: {peach2HP}/8");
    }
    
    Sprite GetSprite(int hp)
    {
        if (hp >= 7) return fullPeach;        // 7-8 HP
        if (hp >= 5) return threeQuarterPeach; // 5-6 HP
        if (hp >= 3) return halfPeach;         // 3-4 HP
        if (hp >= 1) return quarterPeach;      // 1-2 HP
        return emptyPeach;                     // 0 HP
    }
}