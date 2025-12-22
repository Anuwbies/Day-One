using UnityEngine;
using UnityEngine.EventSystems;

public class PlayerAttack : MonoBehaviour
{
    [Header("Attack Settings")]
    public float attackDamage = 10f;
    public float attackDuration = 0.08f;
    public float attackCooldown = 0.3f;
    public float energyCostPerAttack = 1f;

    [Header("References")]
    public SpriteRenderer spriteRenderer;
    public PolygonCollider2D attackCollider;
    public PlayerStats playerStats;
    public InventoryUI inventoryUI;

    private bool isAttacking = false;
    private bool canAttack = true;

    // NEW: prevents attack until mouse is released
    private bool requireMouseRelease = false;

    private void Start()
    {
        if (attackCollider != null)
            attackCollider.enabled = false;
    }

    private void Update()
    {
        // Inventory/UI consumed this click → require release
        if (inventoryUI != null && inventoryUI.ConsumeClickThisFrame)
        {
            requireMouseRelease = true;
            isAttacking = false;
            return;
        }

        // Wait until mouse button is released once
        if (requireMouseRelease)
        {
            if (Input.GetMouseButtonUp(0))
                requireMouseRelease = false;

            return;
        }

        // Pointer over UI
        if (EventSystem.current != null &&
            EventSystem.current.IsPointerOverGameObject())
        {
            isAttacking = false;
            return;
        }

        // Inventory open
        if (inventoryUI != null && inventoryUI.IsOpen)
        {
            isAttacking = false;
            return;
        }

        // ORIGINAL HOLD-TO-ATTACK LOGIC (unchanged)
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
        if (!canAttack)
            return;

        if (playerStats == null || playerStats.Energy <= 0f)
            return;

        PerformAttack();
    }

    private void PerformAttack()
    {
        canAttack = false;

        // Consume energy
        playerStats.UseEnergy(energyCostPerAttack);

        bool facingLeft = spriteRenderer != null && spriteRenderer.flipX;

        if (attackCollider != null)
        {
            attackCollider.transform.localScale = new Vector3(
                facingLeft ? -1f : 1f,
                1f,
                1f
            );

            attackCollider.enabled = true;
        }

        Invoke(nameof(DisableHitbox), attackDuration);
        Invoke(nameof(ResetAttack), attackCooldown);
    }

    private void DisableHitbox()
    {
        if (attackCollider != null)
            attackCollider.enabled = false;
    }

    private void ResetAttack()
    {
        canAttack = true;

        // Preserve original chained attack behavior
        if (isAttacking && playerStats != null && playerStats.Energy > 0f)
            TryAttack();
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (attackCollider == null || !attackCollider.enabled)
            return;

        if (!collision.CompareTag("Damageable"))
            return;

        EnemyHealth health = collision.GetComponentInParent<EnemyHealth>();
        if (health != null)
        {
            health.TakeDamage(attackDamage);
        }
    }
}
