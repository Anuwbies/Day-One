using UnityEngine;
using System;

public class EnemyHealth : MonoBehaviour
{
    [Header("Type")]
    public DamageTarget damageTarget = DamageTarget.Enemy;

    [Header("Health")]
    public float maxHealth = 30f;
    public float currentHealth;

    [Header("Regeneration")]
    [Tooltip("Toggle to enable or disable health regeneration.")]
    public bool canRegenerate = true;
    [Tooltip("Time in seconds since last hit or aggression ended before regeneration starts.")]
    public float regenDelay = 3f;
    [Tooltip("Amount of health restored per second.")]
    public float regenRate = 5f;

    [Header("References")]
    [Tooltip("Assign the specific collider that represents the damageable area.")]
    public Collider2D hitCollider;
    private EnemyController enemyController;

    private float lastHitTime;

    public bool IsDead => currentHealth <= 0f;

    public event Action OnDeath;
    // Event to notify when damage is taken, useful for "On Hit" aggression logic
    public event Action OnDamageTaken;

    private void Awake()
    {
        currentHealth = maxHealth;
        // Initialize lastHitTime so regen can occur immediately if starting damaged (rare, but safe)
        lastHitTime = -regenDelay;

        // Auto-assign references if not manually set
        if (hitCollider == null) hitCollider = GetComponent<Collider2D>();
        enemyController = GetComponent<EnemyController>();
    }

    private void Update()
    {
        // Reset the timer if we are currently aggroed
        if (enemyController != null && enemyController.IsAggroed)
        {
            lastHitTime = Time.time;
            return;
        }

        if (canRegenerate && !IsDead && currentHealth < maxHealth)
        {
            // Check if enough time has passed since the last hit OR aggression ended
            if (Time.time >= lastHitTime + regenDelay)
            {
                Regenerate();
            }
        }
    }

    private void Regenerate()
    {
        currentHealth += regenRate * Time.deltaTime;

        // Clamp health so it doesn't exceed max
        if (currentHealth > maxHealth)
        {
            currentHealth = maxHealth;
        }
    }

    public void TakeDamage(float amount)
    {
        if (IsDead)
            return;

        currentHealth -= amount;

        // Reset the regeneration timer
        lastHitTime = Time.time;

        // Notify listeners that we've been hit
        OnDamageTaken?.Invoke();

        if (currentHealth <= 0f)
        {
            currentHealth = 0f;
            OnDeath?.Invoke();
        }
    }
}