using UnityEngine;

public class DamageTest : MonoBehaviour
{
    private PlayerHealth playerHealth;
    
    void Start()
    {
        playerHealth = GetComponent<PlayerHealth>();
    }
    
    void Update()
    {
        // Press H to take 10 damage
        if (Input.GetKeyDown(KeyCode.H))
        {
            playerHealth.TakeDamage(10);
            Debug.Log("Pressed H - Took 10 damage");
        }
        
        // Press J to heal 20 HP
        if (Input.GetKeyDown(KeyCode.J))
        {
            playerHealth.Heal(20);
            Debug.Log("Pressed J - Healed 20 HP");
        }
        
        // Press K to kill instantly
        if (Input.GetKeyDown(KeyCode.K))
        {
            playerHealth.TakeDamage(1000);
            Debug.Log("Pressed K - Player killed");
        }
    }
}