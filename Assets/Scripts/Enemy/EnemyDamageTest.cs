using UnityEngine;

public class EnemyDamageTest : MonoBehaviour
{
    [SerializeField] private int damageAmount = 10;
    
    void Update()
    {
        // Press SPACE to damage nearest enemy
        if (Input.GetKeyDown(KeyCode.Space))
        {
            DamageNearestEnemy();
        }
        
        // Press L to damage ALL enemies
        if (Input.GetKeyDown(KeyCode.L))
        {
            DamageAllEnemies();
        }
        
        // Press M to kill nearest enemy instantly
        if (Input.GetKeyDown(KeyCode.M))
        {
            KillNearestEnemy();
        }
    }
    
    void DamageNearestEnemy()
    {
        GameObject[] enemies = GameObject.FindGameObjectsWithTag("Enemy");
        
        if (enemies.Length == 0)
        {
            Debug.Log("No enemies found!");
            return;
        }
        
        // Find closest
        GameObject closestEnemy = null;
        float closestDistance = Mathf.Infinity;
        
        foreach (GameObject enemy in enemies)
        {
            float distance = Vector3.Distance(transform.position, enemy.transform.position);
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestEnemy = enemy;
            }
        }
        
        // Damage it
        if (closestEnemy != null)
        {
            EnemyHealth enemyHealth = closestEnemy.GetComponent<EnemyHealth>();
            if (enemyHealth != null)
            {
                enemyHealth.TakeDamage(damageAmount);
            }
        }
    }
    
    void DamageAllEnemies()
    {
        GameObject[] enemies = GameObject.FindGameObjectsWithTag("Enemy");
        
        foreach (GameObject enemy in enemies)
        {
            EnemyHealth enemyHealth = enemy.GetComponent<EnemyHealth>();
            if (enemyHealth != null && enemyHealth.IsAlive())
            {
                enemyHealth.TakeDamage(damageAmount);
            }
        }
        
        Debug.Log($"Damaged all {enemies.Length} enemies!");
    }
    
    void KillNearestEnemy()
    {
        GameObject[] enemies = GameObject.FindGameObjectsWithTag("Enemy");
        
        if (enemies.Length == 0)
        {
            Debug.Log("No enemies found!");
            return;
        }
        
        // Find closest
        GameObject closestEnemy = null;
        float closestDistance = Mathf.Infinity;
        
        foreach (GameObject enemy in enemies)
        {
            float distance = Vector3.Distance(transform.position, enemy.transform.position);
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestEnemy = enemy;
            }
        }
        
        // Kill it
        if (closestEnemy != null)
        {
            EnemyHealth enemyHealth = closestEnemy.GetComponent<EnemyHealth>();
            if (enemyHealth != null)
            {
                enemyHealth.TakeDamage(1000);
            }
        }
    }
}