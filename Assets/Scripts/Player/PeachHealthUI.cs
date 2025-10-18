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
    
    void Start()
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
        Debug.Log($"🍑 UpdateHealth: {currentHP}/12 HP");
        
        // Peach 2 (rightmost): HP 9-12
        int peach2HP = Mathf.Clamp(currentHP - 8, 0, 4);
        peachImages[2].sprite = GetSprite(peach2HP);
        
        // Peach 1 (middle): HP 5-8
        int peach1HP = Mathf.Clamp(currentHP - 4, 0, 4);
        peachImages[1].sprite = GetSprite(peach1HP);
        
        // Peach 0 (leftmost): HP 1-4
        int peach0HP = Mathf.Clamp(currentHP, 0, 4);
        peachImages[0].sprite = GetSprite(peach0HP);
        
        Debug.Log($"  Peach 0: {peach0HP}, Peach 1: {peach1HP}, Peach 2: {peach2HP}");
    }
    
    Sprite GetSprite(int hp)
    {
        if (hp == 4) return fullPeach;
        if (hp == 3) return threeQuarterPeach;
        if (hp == 2) return halfPeach;
        if (hp == 1) return quarterPeach;
        return emptyPeach;
    }
}