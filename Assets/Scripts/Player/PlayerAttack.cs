using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections.Generic;

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

    // Track enemies hit during the current attack swing
    private HashSet<GameObject> enemiesHit = new HashSet<GameObject>();

    private Camera mainCam;

    private void Start()
    {
        mainCam = Camera.main;

        if (attackCollider != null)
            attackCollider.enabled = false;
    }

    private void Update()
    {
        // 1. Check UI interactions
        if (inventoryUI != null && inventoryUI.ConsumeClickThisFrame)
        {
            requireMouseRelease = true;
            isAttacking = false;
            return;
        }

        if (requireMouseRelease)
        {
            if (Input.GetMouseButtonUp(0))
                requireMouseRelease = false;
            return;
        }

        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            isAttacking = false;
            return;
        }

        if (inventoryUI != null && inventoryUI.IsOpen)
        {
            isAttacking = false;
            return;
        }

        // 2. Handle Input
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
        if (!canAttack) return;
        if (playerStats == null || playerStats.Energy <= 0f) return;

        PerformAttack();
    }

    private void PerformAttack()
    {
        enemiesHit.Clear();
        canAttack = false;
        playerStats.UseEnergy(energyCostPerAttack);

        // Get mouse position in World Space
        if (mainCam == null) mainCam = Camera.main;
        Vector3 mousePos = mainCam.ScreenToWorldPoint(Input.mousePosition);

        // --- Sprite Flipping Logic ---
        // Flip the player sprite to face the mouse cursor
        if (spriteRenderer != null)
        {
            if (mousePos.x < transform.position.x)
                spriteRenderer.flipX = true; // Face Left
            else
                spriteRenderer.flipX = false; // Face Right
        }

        if (attackCollider != null)
        {
            // --- 8-Directional Logic Start ---

            // Calculate direction vector from Player to Mouse
            Vector2 direction = (mousePos - transform.position).normalized;

            // Calculate angle in degrees
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

            // Snap to nearest 45 degrees (8 directions)
            // 0, 45, 90, 135, 180, 225, 270, 315
            float snappedAngle = Mathf.Round(angle / 45f) * 45f;

            // Apply rotation to the collider
            attackCollider.transform.rotation = Quaternion.Euler(0f, 0f, snappedAngle);

            // Reset scale to (1,1,1) to ensure no negative flipping conflicts with rotation
            attackCollider.transform.localScale = Vector3.one;

            // --- 8-Directional Logic End ---

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
        if (attackCollider == null || !attackCollider.enabled) return;

        // Find Health component on object or parent
        EnemyHealth health = collision.GetComponentInParent<EnemyHealth>();

        if (health == null) return;

        // Check against specific hitCollider if assigned
        if (health.hitCollider != null && collision != health.hitCollider)
            return;

        // Prevent multi-hits on the same enemy in one swing
        if (enemiesHit.Contains(health.gameObject))
            return;

        enemiesHit.Add(health.gameObject);

        int damage = playerStats.GetDamage(health.damageTarget);
        health.TakeDamage(damage);
    }
}