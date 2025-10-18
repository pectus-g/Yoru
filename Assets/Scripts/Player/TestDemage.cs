using UnityEngine;

public class TestDamage : MonoBehaviour
{
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.K))
        {
            GetComponent<PlayerHealth>().TakeDamage(1);
        }
        
        if (Input.GetKeyDown(KeyCode.J))
        {
            GetComponent<PlayerHealth>().Heal(1);
        }
    }
}