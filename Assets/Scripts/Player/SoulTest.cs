using UnityEngine;

public class SoulTest : MonoBehaviour
{
    private SoulManager soulManager;
    
    void Start()
    {
        soulManager = GetComponent<SoulManager>();
    }
    
    void Update()
    {
        // Press U to spend 1 soul
        if (Input.GetKeyDown(KeyCode.U))
        {
            soulManager.SpendSoul(1);
        }
        
        // Press I to restore 1 soul
        if (Input.GetKeyDown(KeyCode.I))
        {
            soulManager.RestoreSoul(1);
        }
    }
}