using UnityEngine;

public class PlayerAttack : MonoBehaviour
{
    public float attackDamage = 10f;
    public float attackDuration = 0.08f;
    public float attackCooldown = 0.3f;
    public float energyCostPerAttack = 1f;

    public SpriteRenderer spriteRenderer;
    public PolygonCollider2D attackCollider;
    public PlayerStats playerStats;

    private bool isAttacking = false;
    private bool canAttack = true;

    private void Start()
    {
        attackCollider.enabled = false;
    }

    private void Update()
    {
        if (Input.GetMouseButton(0))
        {
            isAttacking = true;
            TryAttack();
        }
        else
        {
            isAttacking = false;
        }
    }

    private void TryAttack()
    {
        if (!canAttack || playerStats.Energy <= 0f)
            return;

        PerformAttack();
    }

    private void PerformAttack()
    {
        canAttack = false;

        // Consume energy
        playerStats.UseEnergy(energyCostPerAttack);

        bool facingLeft = spriteRenderer.flipX;

        // Flip attack cone
        attackCollider.transform.localScale = new Vector3(
            facingLeft ? -1 : 1,
            1,
            1
        );

        attackCollider.enabled = true;
        Invoke(nameof(DisableHitbox), attackDuration);
        Invoke(nameof(ResetAttack), attackCooldown);
    }

    private void DisableHitbox()
    {
        attackCollider.enabled = false;
    }

    private void ResetAttack()
    {
        canAttack = true;

        if (isAttacking && playerStats.Energy > 0f)
            TryAttack();
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (!attackCollider.enabled)
            return;

        // Only damage hit colliders
        if (!collision.CompareTag("Damageable"))
            return;

        EnemyHealth health = collision.GetComponentInParent<EnemyHealth>();
        if (health != null)
        {
            health.TakeDamage(attackDamage);
        }
    }
}
