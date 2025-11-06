using UnityEngine;

public class KarmaManager : MonoBehaviour
{
    [Header("Karma Values")]
    [SerializeField] private int lightKarma = 0;
    [SerializeField] private int darkKarma = 0;
    
    [Header("Display (for debugging)")]
    [SerializeField] private bool showDebugUI = true;
    
    // Singleton
    public static KarmaManager Instance { get; private set; }
    
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
    
    public void AddLightKarma(int amount)
    {
        lightKarma += amount;
        Debug.Log($"✨ +{amount} Light Karma! Total: {lightKarma} Light, {darkKarma} Dark");
        
        // TODO: Update visual rings on tails
    }
    
    public void AddDarkKarma(int amount)
    {
        darkKarma += amount;
        Debug.Log($"🔥 +{amount} Dark Karma! Total: {lightKarma} Light, {darkKarma} Dark");
        
        // TODO: Update visual rings on tails
    }
    
    public int GetLightKarma() => lightKarma;
    public int GetDarkKarma() => darkKarma;
    public int GetTotalKarma() => lightKarma + darkKarma;
    
    public float GetKarmaBalance()
    {
        int total = GetTotalKarma();
        if (total == 0) return 0f;
        
        // Returns -1 (full dark) to +1 (full light)
        return (float)(lightKarma - darkKarma) / total;
    }
    
    // Debug display
    void OnGUI()
    {
        if (!showDebugUI) return;
        
        GUI.Label(new Rect(10, 100, 300, 20), $"Light Karma: {lightKarma}");
        GUI.Label(new Rect(10, 120, 300, 20), $"Dark Karma: {darkKarma}");
        GUI.Label(new Rect(10, 140, 300, 20), $"Balance: {GetKarmaBalance():F2}");
    }
}