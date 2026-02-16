using UnityEngine;
using System.Collections.Generic;

public class EnemyAttack : MonoBehaviour
{
    [Header("Attack Settings")]
    [Tooltip("Amount of damage to deal to the player.")]
    public float damage = 10f;
    [Tooltip("Cooldown in seconds between attacks.")]
    public float attackRate = 1.0f;
    [Tooltip("Time before the hitbox activates (Telegraph/Cast time).")]
    public float attackCastDuration = 0.5f;
    [Tooltip("How long the hitbox stays active during an attack.")]
    public float attackDuration = 0.2f;
    [Tooltip("How long to wait after stopping before attacking.")]
    public float attackDelayAfterStop = 0.2f;
    [Tooltip("How long to wait after the attack finishes before moving again.")]
    public float movementDelayAfterAttack = 0.5f;

    [Header("8-Directional Offsets")]
    [Tooltip("Moves all 8 attack directions as a single group.")]
    public Vector2 groupOffset;
    [Tooltip("Offsets for the attack collider at each angle (relative to Group Offset):\n0: Right (0°)\n1: Top-Right (45°)\n2: Top (90°)\n3: Top-Left (135°)\n4: Left (180°)\n5: Bottom-Left (225°)\n6: Bottom (270°)\n7: Bottom-Right (315°)")]
    public Vector2[] directionalOffsets = new Vector2[8];

    [Header("References")]
    [Tooltip("The collider representing the enemy's weapon/attack area.")]
    public Collider2D attackCollider;
    public Collider2D playerCollider;
    [Tooltip("The collider used to calculate the center of the enemy (body).")]
    public Collider2D ownCollider;

    private float nextAttackTime;
    private EnemyController enemyController;

    // Movement tracking
    private Vector3 lastPosition;
    private float stopTime;
    private bool isMoving;
    private bool isAttacking; // Lock to prevent rotation during attack

    // Physics check requirements
    private ContactFilter2D playerFilter;
    private Collider2D[] overlapResults = new Collider2D[5]; // Increased buffer size

    // Track targets hit during the current swing to prevent multi-hits
    private HashSet<GameObject> hitTargets = new HashSet<GameObject>();

    private void Start()
    {
        enemyController = GetComponent<EnemyController>();

        // Auto-assign own collider if not set in Inspector
        if (ownCollider == null)
            ownCollider = GetComponent<Collider2D>();

        // Ensure weapon starts disabled
        if (attackCollider != null)
            attackCollider.enabled = false;

        // Auto-find player if not assigned
        if (playerCollider == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) playerCollider = p.GetComponent<Collider2D>();
        }

        // Setup filter to only detect the player layer, but allow triggers
        if (playerCollider != null)
        {
            playerFilter = new ContactFilter2D();
            playerFilter.useTriggers = true; // Important: Allow detecting trigger colliders
            playerFilter.useLayerMask = true;
            playerFilter.layerMask = LayerMask.GetMask(LayerMask.LayerToName(playerCollider.gameObject.layer));
        }

        // Initialize offsets if array is empty (failsafe)
        if (directionalOffsets.Length != 8)
        {
            System.Array.Resize(ref directionalOffsets, 8);
        }

        lastPosition = transform.position;
    }

    private void Update()
    {
        if (playerCollider == null) return;

        // 1. Check Behavior: Don't attack if we are purely Passive (fleeing logic)
        if (enemyController != null && enemyController.behavior == EnemyController.AIBehavior.Passive)
            return;

        // 2. Movement Check
        float speed = Vector3.Distance(transform.position, lastPosition) / Time.deltaTime;
        lastPosition = transform.position;
        isMoving = speed > 0.1f;

        if (isMoving)
        {
            stopTime = Time.time;
        }

        // Stop updating direction or starting new attacks if already mid-attack
        if (isAttacking) return;

        // 3. Attack Logic
        if (!isMoving && Time.time >= stopTime + attackDelayAfterStop)
        {
            // Position the collider toward the player before checking range
            UpdateColliderTransform(playerCollider.bounds.center);

            if (IsPlayerInRange() && Time.time >= nextAttackTime)
            {
                PerformAttack();
                nextAttackTime = Time.time + attackRate;
            }
        }
    }

    private void UpdateColliderTransform(Vector3 targetPos)
    {
        if (attackCollider == null) return;

        Vector3 myCenter = (ownCollider != null) ? ownCollider.bounds.center : transform.position;
        Vector2 direction = (targetPos - myCenter).normalized;

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        if (angle < 0) angle += 360f;

        int index = Mathf.RoundToInt(angle / 45f) % 8;
        float snappedAngle = index * 45f;

        // Apply rotation (World Rotation handles the visual rotation correctly)
        attackCollider.transform.rotation = Quaternion.Euler(0f, 0f, snappedAngle);

        if (directionalOffsets != null && directionalOffsets.Length > index)
        {
            Vector2 totalOffset = groupOffset + directionalOffsets[index];

            // FIX: If the enemy parent is flipped (Scale X is negative), 
            // we must invert the X offset so the weapon appears on the correct side in World Space.
            if (transform.lossyScale.x < 0)
            {
                totalOffset.x = -totalOffset.x;
            }

            attackCollider.transform.localPosition = (Vector3)totalOffset;
        }
    }

    private bool IsPlayerInRange()
    {
        if (attackCollider == null) return false;

        // Physics check: Does the attack collider overlap the player?
        // To ensure the query works, we momentarily enable the collider if it's disabled.
        // This allows OverlapCollider to function without triggering physics events (OnTriggerEnter) which run in the Physics step.
        bool wasEnabled = attackCollider.enabled;
        if (!wasEnabled)
        {
            attackCollider.enabled = true;
        }

        int count = Physics2D.OverlapCollider(attackCollider, playerFilter, overlapResults);

        if (!wasEnabled) attackCollider.enabled = false;

        // Double check results to ensure we actually hit the player
        for (int i = 0; i < count; i++)
        {
            if (overlapResults[i] == playerCollider)
                return true;
        }
        return false;
    }

    private void PerformAttack()
    {
        hitTargets.Clear();
        isAttacking = true; // Lock direction

        // Stop movement while attacking (start of cast)
        if (enemyController != null)
            enemyController.enabled = false;

        // Wait for cast duration before enabling hitbox
        Invoke(nameof(EnableHitbox), attackCastDuration);
    }

    private void EnableHitbox()
    {
        if (attackCollider != null)
        {
            attackCollider.enabled = true;
        }

        // Disable hitbox after active duration
        Invoke(nameof(DisableHitbox), attackDuration);
    }

    private void DisableHitbox()
    {
        if (attackCollider != null)
            attackCollider.enabled = false;

        // Resume movement after delay
        Invoke(nameof(EnableMovement), movementDelayAfterAttack);
    }

    private void EnableMovement()
    {
        if (enemyController != null)
            enemyController.enabled = true;

        isAttacking = false; // Unlock direction
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (attackCollider == null || !attackCollider.enabled)
            return;

        // Try to get PlayerStats from the collided object or its parent
        PlayerStats stats = collision.GetComponent<PlayerStats>();
        if (stats == null) stats = collision.GetComponentInParent<PlayerStats>();

        if (stats != null)
        {
            if (hitTargets.Contains(stats.gameObject))
                return;

            hitTargets.Add(stats.gameObject);

            // Deal actual damage
            stats.TakeDamage(damage);

            // Debug.Log($"{name} hit {collision.name} for {damage} damage!");
        }
    }

    private void OnDrawGizmosSelected()
    {
        // Draw Dots for offsets
        if (directionalOffsets == null) return;
        Gizmos.color = Color.cyan;
        for (int i = 0; i < directionalOffsets.Length; i++)
        {
            Vector2 totalLocalOffset = groupOffset + directionalOffsets[i];

            // Adjust Gizmo visualization for flipped parent as well
            if (transform.lossyScale.x < 0)
            {
                totalLocalOffset.x = -totalLocalOffset.x;
            }

            Vector3 offsetWorldPos = transform.TransformPoint(totalLocalOffset);
            Gizmos.DrawSphere(offsetWorldPos, 0.02f);
        }
    }
}