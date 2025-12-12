using UnityEngine;

public class PlayerAttack : MonoBehaviour
{
    public float attackDamage = 10f;
    public float attackDuration = 0.08f;   // hitbox active time
    public float attackCooldown = 0.3f;    // delay between attacks
    public float energyCostPerAttack = 1f; // cost of each attack

    public SpriteRenderer spriteRenderer;
    public PolygonCollider2D attackCollider;
    public PlayerStats playerStats;

    private bool isAttacking = false;  // holding mouse
    private bool canAttack = true;     // cooldown flag

    private void Start()
    {
        attackCollider.enabled = false;
    }

    private void Update()
    {
        // If player holds left mouse button
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
        // Reject attacking if:
        // - still in cooldown
        // - no energy
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

        // Flip attack cone based on direction
        attackCollider.transform.localScale = new Vector3(
            facingLeft ? -1 : 1,
            1,
            1
        );

        // Enable hitbox briefly
        attackCollider.enabled = true;
        Invoke(nameof(DisableHitbox), attackDuration);

        // Begin cooldown
        Invoke(nameof(ResetAttack), attackCooldown);
    }

    private void DisableHitbox()
    {
        attackCollider.enabled = false;
    }

    private void ResetAttack()
    {
        canAttack = true;

        // Auto-attack again if player still holding mouse AND has energy
        if (isAttacking && playerStats.Energy > 0f)
            TryAttack();
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (!attackCollider.enabled) return;

        EnemyHealth enemy = collision.GetComponent<EnemyHealth>();
        if (enemy != null)
        {
            enemy.TakeDamage(attackDamage);
        }
    }
}
