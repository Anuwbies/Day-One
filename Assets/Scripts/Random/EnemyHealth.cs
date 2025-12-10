using UnityEngine;

public class EnemyHealth : MonoBehaviour
{
    public float hp = 30f;

    public void TakeDamage(float amount)
    {
        hp -= amount;
        if (hp <= 0f)
        {
            Die();
        }
    }

    private void Die()
    {
        Destroy(gameObject);
    }
}
