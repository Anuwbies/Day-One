using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))] // Ensure RB exists for physics settings
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

    [Header("Speed Settings")]
    [Tooltip("Speed at which the enemy patrols.")]
    public float patrolSpeed = 2f;
    [Tooltip("Speed at which the enemy chases the player.")]
    public float chaseSpeed = 4f;
    [Tooltip("Speed at which the enemy flees from the player.")]
    public float fleeSpeed = 5f;

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
    private SpriteRenderer spriteRenderer;
    private Rigidbody2D rb; // Reference to Rigidbody
    public bool IsAggroed { get; private set; } = false;
    private Vector2 startPosition;

    private Vector2 patrolTarget;
    private float nextMoveTime;
    private ContactFilter2D obstacleFilter;

    private Vector2 lockedFleeDirection;
    private float fleeLockTimer = 0f;
    private Vector2 lockedSlideDirection;
    private float slideSide = 0f; // 1 for CW, -1 for CCW, 0 for none
    private float clearPathTimer = 0f; // Buffer to prevent flickering resets
    private int currentWallID = 0; // The InstanceID of the wall we are currently hugging
    private int lastWallID = 0; // Memory for the last wall encountered
    private float lastWallSide = 0f; // Side used for the last wall encountered
    private bool isReturningToPatrol = false;

    private void Start()
    {
        enemyHealth = GetComponent<EnemyHealth>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        rb = GetComponent<Rigidbody2D>();

        // Setup obstacle filter based on physics settings
        obstacleFilter = new ContactFilter2D();
        obstacleFilter.useTriggers = false;
        obstacleFilter.useLayerMask = true;
        obstacleFilter.layerMask = Physics2D.GetLayerCollisionMask(gameObject.layer);

        // CONFIGURING PHYSICS TO PREVENT SLIDING
        if (rb != null)
        {
            rb.gravityScale = 0f; // Ensure top-down physics
            rb.constraints = RigidbodyConstraints2D.FreezeRotation; // Prevent rolling
            rb.linearDamping = 20f; // HIGH DRAG: Stops sliding when pushed
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        }

        // Auto-assign own collider if not set in Inspector
        if (ownCollider == null)
            ownCollider = GetComponent<Collider2D>();

        // Set start position based on Collider Center if available, otherwise Transform
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
        if (!IsAggroed)
        {
            if (behavior == AIBehavior.Aggressive)
            {
                // Check if player is inside the detection ellipse
                if (IsInEllipticalRange(playerPos, myPos, detectionRange))
                {
                    IsAggroed = true;
                }
            }
            // Retaliatory and Passive aggro is handled in HandleDamageTaken

            // If not aggroed, patrol/wander
            Patrol();
        }

        // 2. Handle Active Behavior (Chasing or Fleeing)
        if (IsAggroed)
        {
            // Check if player is OUTSIDE the disengage ellipse
            if (!IsInEllipticalRange(playerPos, myPos, disengageRange))
            {
                // If far enough away, stop chasing (or stop fleeing)
                IsAggroed = false;
                isReturningToPatrol = true;
                
                // Reset wall memory for fresh start when returning
                lastWallID = 0;
                lastWallSide = 0f;
                
                // Clear any movement locks from the chase
                lockedSlideDirection = Vector2.zero;
                lockedFleeDirection = Vector2.zero;

                // HANDLE PATROL RESET
                if (resetPatrolAfterAggro)
                {
                    // Update startPosition to current position so it patrols around where it stopped
                    startPosition = ownCollider != null ? ownCollider.bounds.center : transform.position;
                    patrolTarget = startPosition;
                    nextMoveTime = Time.time + waitTime;
                }
                else
                {
                    // Return to original start position
                    patrolTarget = startPosition;
                    // We don't set nextMoveTime here so it starts moving immediately
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
        float dx = targetPos.x - centerPos.x;
        float dy = targetPos.y - centerPos.y;

        float rx = Mathf.Max(range.x, 0.001f);
        float ry = Mathf.Max(range.y, 0.001f);

        return ((dx * dx) / (rx * rx)) + ((dy * dy) / (ry * ry)) <= 1f;
    }

    private void HandleDamageTaken()
    {
        // Trigger aggression/action state for Retaliatory (Chase) and Passive (Flee)
        if (behavior == AIBehavior.Retaliatory || behavior == AIBehavior.Passive)
        {
            IsAggroed = true;
        }
    }

    private void Patrol()
    {
        Vector3 currentPos = ownCollider != null ? ownCollider.bounds.center : transform.position;

        if (Vector2.Distance(currentPos, patrolTarget) < 0.2f)
        {
            rb.linearVelocity = Vector2.zero;
            lockedSlideDirection = Vector2.zero;
            isReturningToPatrol = false;

            // Reset wall memory upon reaching destination
            lastWallID = 0;
            lastWallSide = 0f;
            
            // Arrived at target
            if (Time.time >= nextMoveTime)
            {
                PickNewPatrolTarget();
            }
        }
        else
        {
            // Move and check if blocked
            if (MoveTo(patrolTarget, true, patrolSpeed, isReturningToPatrol))
            {
                // If blocked by a wall, pick a new target immediately
                PickNewPatrolTarget();
            }
        }
    }

    private void PickNewPatrolTarget()
    {
        isReturningToPatrol = false;
        lockedSlideDirection = Vector2.zero;
        Vector3 currentPos = ownCollider != null ? ownCollider.bounds.center : transform.position;
        bool targetFound = false;
        float checkRadius = ownCollider != null ? Mathf.Max(ownCollider.bounds.extents.x, ownCollider.bounds.extents.y) : 0.4f;

        for (int i = 0; i < 15; i++)
        {
            Vector2 randomPoint = Random.insideUnitCircle;
            randomPoint.x *= patrolRange.x;
            randomPoint.y *= patrolRange.y;

            Vector2 potentialTarget = startPosition + randomPoint;

            // Check if potentialTarget is inside an obstacle
            Collider2D[] results = new Collider2D[2];
            int count = Physics2D.OverlapCircle(potentialTarget, checkRadius, obstacleFilter, results);
            
            bool isClear = true;
            for (int j = 0; j < count; j++)
            {
                if (results[j] != null && results[j] != playerCollider && results[j] != ownCollider)
                {
                    isClear = false;
                    break;
                }
            }

            if (isClear)
            {
                patrolTarget = potentialTarget;
                targetFound = true;
                break;
            }
        }

        if (targetFound)
        {
            nextMoveTime = Time.time + waitTime;
        }
        else
        {
            patrolTarget = currentPos;
            nextMoveTime = Time.time + waitTime;
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
                if (Time.time < fleeLockTimer && lockedFleeDirection != Vector2.zero)
                {
                    // Still in 'locked' fleeing mode, check if we are STILL blocked in this direction
                    RaycastHit2D[] fleeHits = new RaycastHit2D[1];
                    if (rb.Cast(lockedFleeDirection, obstacleFilter, fleeHits, 0.4f) == 0 || fleeHits[0].collider == playerCollider)
                    {
                        ApplyFacing(lockedFleeDirection);
                        rb.linearVelocity = lockedFleeDirection * fleeSpeed;
                        return;
                    }
                }

                Vector3 diff = myCenter - playerCenter;
                if (diff.sqrMagnitude < 0.001f) diff = Random.insideUnitCircle;

                Vector2 fleeDirection = diff.normalized;
                
                // Check if fleeing direction is blocked
                RaycastHit2D[] initialFleeHits = new RaycastHit2D[1];
                if (rb.Cast(fleeDirection, obstacleFilter, initialFleeHits, 0.8f) > 0 && initialFleeHits[0].collider != playerCollider)
                {
                    // If blocked, try alternative flee angles
                    bool foundEscape = false;
                    // Check wider angles first to ensure a clear turn
                    float[] escapeAngles = { 45f, -45f, 90f, -90f, 135f, -135f };
                    
                    foreach (float angle in escapeAngles)
                    {
                        Vector2 altDir = Quaternion.Euler(0, 0, angle) * fleeDirection;
                        if (rb.Cast(altDir, obstacleFilter, initialFleeHits, 1.0f) == 0 || initialFleeHits[0].collider == playerCollider)
                        {
                            fleeDirection = altDir;
                            foundEscape = true;
                            
                            // LOCK THIS DIRECTION to prevent jitter
                            lockedFleeDirection = altDir;
                            fleeLockTimer = Time.time + 0.4f; 
                            break;
                        }
                    }
                    
                    if (!foundEscape)
                    {
                        rb.linearVelocity = Vector2.zero;
                        lockedFleeDirection = Vector2.zero;
                        return;
                    }
                }
                else
                {
                    lockedFleeDirection = Vector2.zero; // Clear lock if direct path is open
                }

                ApplyFacing(fleeDirection);
                rb.linearVelocity = fleeDirection * fleeSpeed;
                return;
            }

            // --- AGGRESSIVE / RETALIATORY BEHAVIOR (CHASE) ---
            if (!IsInEllipticalRange(playerCenter, myCenter, stoppingDistance))
            {
                bool blocked = MoveTo(playerCenter, true, chaseSpeed, true);
                if (blocked)
                {
                    // If we can't reach the player because of a wall, stop moving and wait
                    rb.linearVelocity = Vector2.zero;
                }
            }
            else
            {
                // Stop moving but still face the player
                rb.linearVelocity = Vector2.zero;
                Vector3 transformTarget = playerCenter;
                if (ownCollider != null)
                {
                    Vector3 centerOffset = (Vector3)ownCollider.bounds.center - transform.position;
                    transformTarget = playerCenter - centerOffset;
                }
                Vector3 direction = transformTarget - transform.position;
                ApplyFacing(direction);
            }
        }
    }

    private bool MoveTo(Vector2 target, bool useCenterAlignment, float speed, bool allowWallHug)
    {
        Vector3 transformTarget = target;

        if (useCenterAlignment && ownCollider != null)
        {
            Vector3 centerOffset = (Vector3)ownCollider.bounds.center - transform.position;
            transformTarget = (Vector3)target - centerOffset;
        }

        Vector2 currentPos = rb.position;
        Vector2 diff = (Vector2)transformTarget - currentPos;
        
        if (diff.sqrMagnitude < 0.01f)
        {
            rb.linearVelocity = Vector2.zero;
            lockedSlideDirection = Vector2.zero;
            slideSide = 0f;
            currentWallID = 0;
            return false;
        }

        Vector2 direction = diff.normalized;
        float castDistance = 0.5f;
        RaycastHit2D[] hits = new RaycastHit2D[1];

        // 1. Check if the direct path is clear
        bool directPathBlocked = rb.Cast(direction, obstacleFilter, hits, castDistance + 0.1f) > 0 && hits[0].collider != playerCollider;

        // 2. Handle the "Clear Path" scenario (including the slide-reset buffer)
        if (!directPathBlocked)
        {
            if (slideSide != 0f)
            {
                // Path is clear. Wait for the buffer to ensure we clear the corner.
                if (clearPathTimer == 0f) clearPathTimer = Time.time + 0.25f;

                if (Time.time < clearPathTimer && lockedSlideDirection != Vector2.zero)
                {
                    ApplyFacing(direction);
                    rb.linearVelocity = lockedSlideDirection * speed;
                    return false;
                }
                else
                {
                    slideSide = 0f;
                    lockedSlideDirection = Vector2.zero;
                    clearPathTimer = 0f;
                    currentWallID = 0;
                }
            }

            ApplyFacing(direction);
            rb.linearVelocity = direction * speed;
            if (diff.sqrMagnitude < (speed * Time.deltaTime) * (speed * Time.deltaTime))
                 rb.linearVelocity = diff / Time.deltaTime;
            
            return false;
        }

        // 3. Path is BLOCKED
        if (!allowWallHug)
        {
            // If wall hugging is not allowed (regular patrolling), just stop and return blocked
            rb.linearVelocity = Vector2.zero;
            ApplyFacing(direction);
            return true;
        }

        // 4. Handle Wall Hugging
        clearPathTimer = 0f; 
        Vector2 hitNormal = hits[0].normal;
        int hitID = hits[0].collider.GetInstanceID();
        
        // Initial choice of side if we just hit the wall
        if (slideSide == 0f)
        {
            // If we hit the same wall again, reuse the side we used before
            if (hitID == lastWallID && lastWallSide != 0f)
            {
                slideSide = lastWallSide;
            }
            else
            {
                // New wall or new encounter, pick the best side based on goal
                Vector2 tangentCW = new Vector2(hitNormal.y, -hitNormal.x);
                Vector2 tangentCCW = new Vector2(-hitNormal.y, hitNormal.x);
                slideSide = (Vector2.Dot(tangentCW, direction) > Vector2.Dot(tangentCCW, direction)) ? 1f : -1f;
                
                // Remember this choice for this specific wall
                lastWallID = hitID;
                lastWallSide = slideSide;
            }
            currentWallID = hitID;
        }

        // Update currentWallID if we hit a NEW wall while sliding (e.g. cornering)
        if (hitID != currentWallID)
        {
             currentWallID = hitID;
             // We DON'T update lastWallID/lastWallSide here because we are still 
             // in the middle of a continuous slide encounter.
        }

        // Calculate tangent based on current normal and persistent side
        Vector2 chosenTangent = (slideSide > 0) 
            ? new Vector2(hitNormal.y, -hitNormal.x) 
            : new Vector2(-hitNormal.y, hitNormal.x);

        // Try to move in that tangent direction
        if (rb.Cast(chosenTangent, obstacleFilter, hits, castDistance) == 0 || hits[0].collider == playerCollider)
        {
            lockedSlideDirection = chosenTangent;
            ApplyFacing(direction);
            rb.linearVelocity = chosenTangent * speed;
            return false;
        }

        // 4. Stuck
        rb.linearVelocity = Vector2.zero;
        lockedSlideDirection = Vector2.zero;
        slideSide = 0f;
        currentWallID = 0;
        ApplyFacing(direction);
        return true;
    }

    private void ApplyFacing(Vector3 direction)
    {
        if (enableRotation)
        {
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(new Vector3(0, 0, angle));
        }

        if (enableFlip && direction.x != 0 && spriteRenderer != null)
        {
            if (direction.x > 0)
                spriteRenderer.flipX = false;
            else
                spriteRenderer.flipX = true;
        }
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 drawCenter = transform.position;

        if (ownCollider != null)
        {
            drawCenter = ownCollider.bounds.center;
        }
        else
        {
            Collider2D col = GetComponent<Collider2D>();
            if (col != null) drawCenter = col.bounds.center;
        }

        DrawEllipse(drawCenter, disengageRange, Color.red);

        if (behavior == AIBehavior.Aggressive)
        {
            DrawEllipse(drawCenter, detectionRange, Color.yellow);
        }

        DrawEllipse(drawCenter, stoppingDistance, Color.blue);

        Vector3 patrolCenter = Application.isPlaying ? (Vector3)startPosition : drawCenter;
        DrawEllipse(patrolCenter, patrolRange, Color.green);
    }

    private void DrawEllipse(Vector3 center, Vector2 range, Color color)
    {
        Gizmos.color = color;
        float theta = 0f;
        float step = 0.1f;

        Vector3 prevPos = center + new Vector3(range.x, 0, 0);

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