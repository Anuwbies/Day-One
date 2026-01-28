using UnityEngine;
using System;

public class EnemyHealth : MonoBehaviour
{
    [Header("Type")]
    public DamageTarget damageTarget = DamageTarget.Enemy;

    [Header("Health")]
    public float maxHealth = 30f;
    public float currentHealth;

    public bool IsDead => currentHealth <= 0f;

    public event Action OnDeath;

    private void Awake()
    {
        currentHealth = maxHealth;
    }

    public void TakeDamage(float amount)
    {
        if (IsDead)
            return;

        currentHealth -= amount;

        if (currentHealth <= 0f)
        {
            currentHealth = 0f;
            OnDeath?.Invoke();
        }
    }
}