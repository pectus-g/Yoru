using UnityEngine;

public class EnemyDamageTest : MonoBehaviour
{
    [SerializeField] private int damageAmount = 1;
    
    void Update()
    {
        // Press K to damage player (NOT SPACE!)
        if (Input.GetKeyDown(KeyCode.K))
        {
            DamagePlayer();
        }
        
        // Press J to heal player
        if (Input.GetKeyDown(KeyCode.J))
        {
            HealPlayer();
        }
    }
    
    void DamagePlayer()
    {
        PlayerHealth playerHealth = GetComponent<PlayerHealth>();
        if (playerHealth != null)
        {
            playerHealth.TakeDamage(damageAmount);
        }
    }
    
    void HealPlayer()
    {
        PlayerHealth playerHealth = GetComponent<PlayerHealth>();
        if (playerHealth != null)
        {
            playerHealth.Heal(1);
        }
    }
}