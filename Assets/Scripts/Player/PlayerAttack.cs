using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections.Generic; // Added for HashSet

public class PlayerAttack : MonoBehaviour
{
    [Header("Attack Settings")]
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

    // Prevents attack until mouse is released after UI click
    private bool requireMouseRelease = false;

    // Track enemies hit during the current attack swing to prevent double damage
    private HashSet<GameObject> enemiesHit = new HashSet<GameObject>();

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

        // HOLD-TO-ATTACK
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
        // Clear the list of hit enemies for this new swing
        enemiesHit.Clear();

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

        // Preserve chained attacks
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
        if (health == null)
            return;

        // Check if we already hit this specific enemy instance in this swing
        if (enemiesHit.Contains(health.gameObject))
            return;

        // Add to list so we don't hit it again this swing
        enemiesHit.Add(health.gameObject);

        int damage = playerStats.GetDamage(health.damageTarget);
        health.TakeDamage(damage);
    }
}