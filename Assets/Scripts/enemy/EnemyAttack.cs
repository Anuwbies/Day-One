using UnityEngine;

public class EnemyAttack : MonoBehaviour
{
    [Header("Attack Settings")]
    [Tooltip("Amount of damage to deal to the player.")]
    public float damage = 10f;
    [Tooltip("Cooldown in seconds between attacks.")]
    public float attackRate = 1.0f;
    [Tooltip("Distance required to land an attack.")]
    public float attackRange = 1.2f;

    [Header("References")]
    public Collider2D playerCollider;

    private float nextAttackTime;
    private EnemyController enemyController;
    private Collider2D ownCollider;

    private void Start()
    {
        enemyController = GetComponent<EnemyController>();
        ownCollider = GetComponent<Collider2D>();

        // Auto-find player if not assigned
        if (playerCollider == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) playerCollider = p.GetComponent<Collider2D>();
        }
    }

    private void Update()
    {
        if (playerCollider == null) return;

        // 1. Check Behavior: Don't attack if we are purely Passive (fleeing logic)
        if (enemyController != null && enemyController.behavior == EnemyController.AIBehavior.Passive)
            return;

        // 2. Check Distance and Cooldown
        float distance = GetDistanceToPlayer();

        if (distance <= attackRange && Time.time >= nextAttackTime)
        {
            PerformAttack();
            nextAttackTime = Time.time + attackRate;
        }
    }

    private float GetDistanceToPlayer()
    {
        // Use Collider2D.Distance for accurate edge-to-edge distance
        if (ownCollider != null && playerCollider != null)
        {
            ColliderDistance2D dist = ownCollider.Distance(playerCollider);
            return dist.distance;
        }

        // Fallback to center-point distance if colliders are missing
        return Vector2.Distance(transform.position, playerCollider.transform.position);
    }

    private void PerformAttack()
    {
        // Attempt to deal damage to the Player
        // We use SendMessage here to be compatible with whatever health script you have on the player
        // (e.g. PlayerHealth, PlayerStats, etc.) looking for a method named "TakeDamage".
        playerCollider.SendMessage("TakeDamage", damage, SendMessageOptions.DontRequireReceiver);

        // Optional: If you have a specific PlayerStats script, you can cast it like this:
        /*
        PlayerStats stats = playerCollider.GetComponent<PlayerStats>();
        if (stats != null) stats.TakeDamage(damage);
        */

        Debug.Log($"{name} attacked Player for {damage} damage!");
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.magenta;

        // Draw attack range from collider center if possible to match logic
        Vector3 center = (ownCollider != null) ? ownCollider.bounds.center : transform.position;
        Gizmos.DrawWireSphere(center, attackRange);
    }
}