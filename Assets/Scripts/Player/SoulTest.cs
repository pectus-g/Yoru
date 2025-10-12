using UnityEngine;

public class SoulTest : MonoBehaviour
{
    private SoulManager soulManager;
    
    [SerializeField] private int spendAmount = 20;
    [SerializeField] private int restoreAmount = 30;
    [SerializeField] private int drainAmount = 15;
    
    void Start()
    {
        soulManager = GetComponent<SoulManager>();
    }
    
    void Update()
    {
        // Press U to spend soul (use ability)
        if (Input.GetKeyDown(KeyCode.U))
        {
            bool success = soulManager.SpendSoul(spendAmount);
            if (success)
            {
                Debug.Log($"Used ability! Spent {spendAmount} soul");
            }
            else
            {
                Debug.Log("Not enough soul to use ability!");
            }
        }
        
        // Press I to restore soul (pickup)
        if (Input.GetKeyDown(KeyCode.I))
        {
            soulManager.RestoreSoul(restoreAmount);
            Debug.Log($"Picked up soul orb! Restored {restoreAmount} soul");
        }
        
        // Press O to drain soul (enemy attack)
        if (Input.GetKeyDown(KeyCode.O))
        {
            soulManager.DrainSoul(drainAmount);
            Debug.Log($"Enemy drained {drainAmount} soul!");
        }
        
        // Press P to set soul to 50% (for testing)
        if (Input.GetKeyDown(KeyCode.P))
        {
            soulManager.SetSoul(50);
            Debug.Log("Set soul to 50%");
        }
    }
}