using UnityEngine;

public class EnemyController : MonoBehaviour
{
    public enum AIBehavior
    {
        Passive,        // Never attacks, flees when hit
        Aggressive,     // Attacks when player is close
        Retaliatory     // Attacks only when hit
    }

    [Header("AI Settings")]
    [Tooltip("Passive: Flees when hit.\nAggressive: Attacks when in range.\nRetaliatory: Attacks when hit.")]
    public AIBehavior behavior = AIBehavior.Aggressive;

    [Tooltip("Speed at which the enemy moves towards the player.")]
    public float moveSpeed = 4f;

    [Header("Patrol Settings")]
    [Tooltip("Range (X, Y) from start position the enemy can wander.")]
    public Vector2 patrolRange = new Vector2(3f, 3f);
    [Tooltip("How long to wait at a patrol point before moving to the next.")]
    public float waitTime = 2f;
    [Tooltip("If true, the enemy sets a new patrol center where it stops chasing/fleeing. If false, it returns to the original start position.")]
    public bool resetPatrolAfterAggro = false;

    [Header("Movement Settings")]
    [Tooltip("Should the enemy rotate to face the player? (Top-down shooter style)")]
    public bool enableRotation = false;
    [Tooltip("Should the enemy flip sprite on X axis to face the target? (Side-scroller/RPG style)")]
    public bool enableFlip = true;

    [Header("Ranges")]
    [Tooltip("Detection range (X, Y) for Aggressive behavior.")]
    public Vector2 detectionRange = new Vector2(5f, 5f);

    [Tooltip("Disengage range (X, Y) where enemy stops chasing (or stops fleeing).")]
    public Vector2 disengageRange = new Vector2(10f, 10f);

    [Tooltip("Minimum distance (X, Y) to keep from the player to avoid overlapping.")]
    public Vector2 stoppingDistance = new Vector2(1.5f, 1.5f);

    [Header("References")]
    [Tooltip("Assign the Player's Collider here.")]
    public Collider2D playerCollider;
    [Tooltip("Assign the Enemy's Collider here (optional, will auto-detect).")]
    public Collider2D ownCollider;

    private EnemyHealth enemyHealth;
    private bool isAggroed = false;
    private Vector2 startPosition;
    private Vector3 initialScale;

    private Vector2 patrolTarget;
    private float nextMoveTime;

    private void Start()
    {
        initialScale = transform.localScale;
        enemyHealth = GetComponent<EnemyHealth>();

        // Auto-assign own collider if not set in Inspector
        if (ownCollider == null)
            ownCollider = GetComponent<Collider2D>();

        // Set start position based on Collider Center if available, otherwise Transform
        // This ensures the Patrol Range is centered on the body, not the feet (pivot).
        if (ownCollider != null)
            startPosition = ownCollider.bounds.center;
        else
            startPosition = transform.position;

        patrolTarget = startPosition;

        // Subscribe to damage event
        if (enemyHealth != null)
        {
            enemyHealth.OnDamageTaken += HandleDamageTaken;
        }

        // Auto-find player collider if not assigned
        if (playerCollider == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
                playerCollider = playerObj.GetComponent<Collider2D>();
        }
    }

    private void OnDestroy()
    {
        if (enemyHealth != null)
        {
            enemyHealth.OnDamageTaken -= HandleDamageTaken;
        }
    }

    private void Update()
    {
        if (playerCollider == null || enemyHealth.IsDead) return;

        // Use centers of colliders for interaction logic
        Vector3 playerPos = playerCollider.bounds.center;
        Vector3 myPos = ownCollider != null ? ownCollider.bounds.center : transform.position;

        // 1. Check triggers to START aggression
        if (!isAggroed)
        {
            if (behavior == AIBehavior.Aggressive)
            {
                // Check if player is inside the detection ellipse
                if (IsInEllipticalRange(playerPos, myPos, detectionRange))
                {
                    isAggroed = true;
                }
            }
            // Retaliatory and Passive aggro is handled in HandleDamageTaken

            // If not aggroed, patrol/wander
            Patrol();
        }

        // 2. Handle Active Behavior (Chasing or Fleeing)
        if (isAggroed)
        {
            // Check if player is OUTSIDE the disengage ellipse
            if (!IsInEllipticalRange(playerPos, myPos, disengageRange))
            {
                // If far enough away, stop chasing (or stop fleeing)
                isAggroed = false;

                // HANDLE PATROL RESET
                if (resetPatrolAfterAggro)
                {
                    // Update startPosition to current position so it patrols around where it stopped
                    startPosition = ownCollider != null ? ownCollider.bounds.center : transform.position;
                    // Reset target to current spot to idle briefly before picking a new random point
                    patrolTarget = startPosition;
                    nextMoveTime = Time.time + waitTime;
                }
            }
            else
            {
                ChasePlayer();
            }
        }
    }

    private bool IsInEllipticalRange(Vector2 targetPos, Vector2 centerPos, Vector2 range)
    {
        // Equation of an ellipse: (x-h)^2/rx^2 + (y-k)^2/ry^2 <= 1
        float dx = targetPos.x - centerPos.x;
        float dy = targetPos.y - centerPos.y;

        // Prevent division by zero
        float rx = Mathf.Max(range.x, 0.001f);
        float ry = Mathf.Max(range.y, 0.001f);

        return ((dx * dx) / (rx * rx)) + ((dy * dy) / (ry * ry)) <= 1f;
    }

    private void HandleDamageTaken()
    {
        // Trigger aggression/action state for Retaliatory (Chase) and Passive (Flee)
        if (behavior == AIBehavior.Retaliatory || behavior == AIBehavior.Passive)
        {
            isAggroed = true;
        }
    }

    private void Patrol()
    {
        // Use center position for distance checks if collider is available
        Vector3 currentPos = ownCollider != null ? ownCollider.bounds.center : transform.position;

        if (Vector2.Distance(currentPos, patrolTarget) < 0.1f)
        {
            // Arrived at target
            if (Time.time >= nextMoveTime)
            {
                // Generate random point in unit circle, then scale by range X and Y
                Vector2 randomPoint = Random.insideUnitCircle;
                randomPoint.x *= patrolRange.x;
                randomPoint.y *= patrolRange.y;

                patrolTarget = startPosition + randomPoint;
                nextMoveTime = Time.time + waitTime;
            }
        }
        else
        {
            // Move center of enemy to the patrol target
            MoveTo(patrolTarget, true);
        }
    }

    private void ChasePlayer()
    {
        if (playerCollider != null)
        {
            Vector3 playerCenter = playerCollider.bounds.center;
            Vector3 myCenter = ownCollider != null ? ownCollider.bounds.center : transform.position;

            // --- PASSIVE BEHAVIOR (FLEE) ---
            if (behavior == AIBehavior.Passive)
            {
                // Calculate direction AWAY from player
                Vector3 diff = myCenter - playerCenter;
                if (diff.sqrMagnitude < 0.001f) diff = Random.insideUnitCircle; // Handle overlap

                Vector3 fleeTarget = myCenter + diff.normalized;
                MoveTo(fleeTarget, true);
                return;
            }

            // --- AGGRESSIVE / RETALIATORY BEHAVIOR (CHASE) ---
            // Check if OUTSIDE the stopping distance (Elliptical check)
            if (!IsInEllipticalRange(playerCenter, myCenter, stoppingDistance))
            {
                // Move towards center
                MoveTo(playerCenter, true);
            }
            else
            {
                // Stop moving but still face the player
                Vector3 transformTarget = playerCenter;
                if (ownCollider != null)
                {
                    // Calculate consistent direction logic manually since we aren't moving
                    Vector3 centerOffset = ownCollider.bounds.center - transform.position;
                    transformTarget = playerCenter - centerOffset;
                }
                Vector3 direction = transformTarget - transform.position;
                ApplyFacing(direction);
            }
        }
    }

    // added bool useCenterAlignment to differentiate between chasing a collider center vs moving to a raw patrol point
    private void MoveTo(Vector2 target, bool useCenterAlignment)
    {
        // Determine the actual destination for the Transform
        Vector3 transformTarget = target;

        if (useCenterAlignment && ownCollider != null)
        {
            // Calculate the offset between the Collider Center and the Transform Pivot
            Vector3 centerOffset = ownCollider.bounds.center - transform.position;

            // Adjust the target so that the Collider Center lands on the Target, not the Pivot
            transformTarget = (Vector3)target - centerOffset;
        }

        Vector3 direction = transformTarget - transform.position;

        ApplyFacing(direction);

        // 2D Movement Logic
        transform.position = Vector2.MoveTowards(transform.position, transformTarget, moveSpeed * Time.deltaTime);
    }

    private void ApplyFacing(Vector3 direction)
    {
        // 2D Rotation Logic
        if (enableRotation)
        {
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(new Vector3(0, 0, angle));
        }

        // 2D Flip Logic
        if (enableFlip && direction.x != 0)
        {
            // Face Right
            if (direction.x > 0)
                transform.localScale = new Vector3(Mathf.Abs(initialScale.x), initialScale.y, initialScale.z);
            // Face Left
            else
                transform.localScale = new Vector3(-Mathf.Abs(initialScale.x), initialScale.y, initialScale.z);
        }
    }

    // Visualize ranges in the Editor
    private void OnDrawGizmosSelected()
    {
        // Determine the center point for drawing ranges
        Vector3 drawCenter = transform.position;

        // Use exposed collider if available, otherwise get component
        if (ownCollider != null)
        {
            drawCenter = ownCollider.bounds.center;
        }
        else
        {
            Collider2D col = GetComponent<Collider2D>();
            if (col != null) drawCenter = col.bounds.center;
        }

        // Draw Disengage Range
        DrawEllipse(drawCenter, disengageRange, Color.red);

        if (behavior == AIBehavior.Aggressive)
        {
            // Draw Detection Range
            DrawEllipse(drawCenter, detectionRange, Color.yellow);
        }

        // Draw Stopping Distance (now Ellipse)
        DrawEllipse(drawCenter, stoppingDistance, Color.blue);

        // Draw Patrol Range
        // Use startPosition if playing (which is likely the collider center now), otherwise estimated center
        Vector3 patrolCenter = Application.isPlaying ? (Vector3)startPosition : drawCenter;
        DrawEllipse(patrolCenter, patrolRange, Color.green);
    }

    private void DrawEllipse(Vector3 center, Vector2 range, Color color)
    {
        Gizmos.color = color;
        float theta = 0f;
        float step = 0.1f;

        // Calculate initial point
        Vector3 prevPos = center + new Vector3(range.x, 0, 0);

        // Draw loop
        for (theta = step; theta < Mathf.PI * 2 + step; theta += step)
        {
            float x = range.x * Mathf.Cos(theta);
            float y = range.y * Mathf.Sin(theta);
            Vector3 newPos = center + new Vector3(x, y, 0);

            Gizmos.DrawLine(prevPos, newPos);
            prevPos = newPos;
        }
    }
}