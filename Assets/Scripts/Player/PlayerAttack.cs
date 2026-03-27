using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections.Generic;

public class PlayerAttack : MonoBehaviour
{
    [Header("Attack Settings")]
    public float attackDuration = 0.08f;
    public float attackCooldown = 0.3f;
    public float energyCostPerAttack = 1f;

    [Header("Attack Area Gizmo")]
    [SerializeField] private bool showAttackAreaGizmo = true;
    [SerializeField] private float attackAreaGizmoAngle = 0f;
    [SerializeField] private Color attackAreaGizmoColor = new Color(1f, 0.25f, 0.1f, 1f);

    [Header("References")]
    public SpriteRenderer spriteRenderer;
    public PolygonCollider2D attackCollider;
    public PlayerStats playerStats;
    public InventoryUI inventoryUI;

    private bool isAttacking = false;
    private bool canAttack = true;
    private PlayerMovement playerMovement;

    // Prevents attack until mouse is released after UI click
    private bool requireMouseRelease = false;

    // Track enemies hit during the current attack swing
    private HashSet<GameObject> enemiesHit = new HashSet<GameObject>();

    private Camera mainCam;

    public void BlockAttackUntilMouseRelease()
    {
        // Set flag to wait for button release
        requireMouseRelease = true;
        isAttacking = false;
        canAttack = false;

        if (playerMovement != null)
        {
            playerMovement.EndAttackAnimation();
        }

        // Reset canAttack quickly, but keep requireMouseRelease until mouse is up
        CancelInvoke(nameof(ResetAttack));
        Invoke(nameof(ResetAttack), 0.1f);
    }

    private void Start()
    {
        mainCam = Camera.main;
        playerMovement = GetComponent<PlayerMovement>();

        if (attackCollider != null)
            attackCollider.enabled = false;
    }

    private void Update()
    {
        // 1. Check UI interactions
        if (InventoryUI.ConsumeAnyClickThisFrame())
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

        if (InventoryUI.IsAnyInventoryOpen())
        {
            isAttacking = false;
            return;
        }

        // 1.5 Check Placement Mode
        if (PlacementManager.Instance != null && PlacementManager.Instance.IsPlacing)
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
        Vector2 attackDirection = (mousePos - transform.position).normalized;

        // Flip the player sprite to face the attack direction.
        if (playerMovement != null)
        {
            playerMovement.SetFacingDirection(attackDirection);
        }
        else if (spriteRenderer != null)
        {
            if (mousePos.x < transform.position.x)
                spriteRenderer.flipX = true; // Face Left
            else
                spriteRenderer.flipX = false; // Face Right
        }

        if (attackCollider != null)
        {
            float angle = Mathf.Atan2(attackDirection.y, attackDirection.x) * Mathf.Rad2Deg;

            // Rotate the attack area transform to the exact attack angle.
            attackCollider.transform.localRotation = Quaternion.Euler(0f, 0f, angle);

            // Reset scale to (1,1,1) to ensure no negative flipping conflicts with rotation
            attackCollider.transform.localScale = Vector3.one;

            attackCollider.enabled = true;
        }

        if (playerMovement != null)
        {
            playerMovement.StartAttackAnimation(attackDirection, attackDuration);
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

    private void OnDrawGizmosSelected()
    {
        if (!showAttackAreaGizmo || attackCollider == null)
        {
            return;
        }

        Quaternion gizmoRotation = Application.isPlaying && attackCollider.enabled
            ? attackCollider.transform.rotation
            : GetAttackColliderPreviewRotation();

        Vector2[] points = attackCollider.points;
        if (points == null || points.Length == 0)
        {
            return;
        }

        Matrix4x4 gizmoMatrix = Matrix4x4.TRS(
            attackCollider.transform.position,
            gizmoRotation,
            attackCollider.transform.lossyScale);

        Gizmos.color = attackAreaGizmoColor;

        for (int i = 0; i < points.Length; i++)
        {
            Vector3 start = gizmoMatrix.MultiplyPoint3x4(points[i]);
            Vector3 end = gizmoMatrix.MultiplyPoint3x4(points[(i + 1) % points.Length]);
            Gizmos.DrawLine(start, end);
        }
    }

    private Quaternion GetAttackColliderPreviewRotation()
    {
        Transform parentTransform = attackCollider.transform.parent;
        Quaternion parentRotation = parentTransform != null ? parentTransform.rotation : Quaternion.identity;
        return parentRotation * Quaternion.Euler(0f, 0f, attackAreaGizmoAngle);
    }
}
