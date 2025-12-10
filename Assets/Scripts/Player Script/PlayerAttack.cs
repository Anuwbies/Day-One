using UnityEngine;

public class PlayerAttack : MonoBehaviour
{
    [Header("Attack Settings")]
    public float attackCooldown = 0.3f;
    public float attackDuration = 0.1f;
    public float damage = 10f;

    private bool isAttacking = false;
    private float cooldownTimer = 0f;

    private PolygonCollider2D attackCollider;

    private void Start()
    {
        attackCollider = GetComponent<PolygonCollider2D>();

        if (attackCollider == null)
        {
            Debug.LogError("PlayerAttack: No PolygonCollider2D found on this GameObject.");
        }

        attackCollider.enabled = false; // Disable at start
    }

    private void Update()
    {
        cooldownTimer -= Time.deltaTime;

        if (Input.GetMouseButtonDown(0) && cooldownTimer <= 0f)
        {
            PerformAttack();
        }
    }

    private void PerformAttack()
    {
        isAttacking = true;
        cooldownTimer = attackCooldown;

        attackCollider.enabled = true;
        Invoke(nameof(DisableAttack), attackDuration);
    }

    private void DisableAttack()
    {
        attackCollider.enabled = false;
        isAttacking = false;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!isAttacking)
            return;

        EnemyHealth enemy = other.GetComponent<EnemyHealth>();
        if (enemy != null)
        {
            enemy.TakeDamage(damage);
        }
    }
}
