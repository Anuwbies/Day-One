using UnityEngine;
using UnityEngine.Serialization;

[RequireComponent(typeof(Rigidbody2D))] // Ensure RB exists for physics settings
public class EnemyController : MonoBehaviour
{
    public enum AIBehavior
    {
        Passive,        // Never attacks, flees when hit
        Aggressive,     // Attacks when player is close
        Retaliatory,    // Attacks only when hit
        FleeOnSight     // Flees when the player is close
    }

    [Header("AI Settings")]
    [Tooltip("Passive: Flees when hit.\nAggressive: Attacks when in range.\nRetaliatory: Attacks when hit.\nFleeOnSight: Flees when the player enters range.")]
    public AIBehavior behavior = AIBehavior.Aggressive;

    [Header("Testing / Debug")]
    [Tooltip("If true, the enemy will stay in idle and skip all AI logic.")]
    public bool debugLockIdle = false;
    [Tooltip("If true, the enemy will stay in patrol/walk mode and skip aggro logic.")]
    public bool debugLockWalkPatrol = false;

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

    [Header("Idle Animation Settings")]
    [Tooltip("Should the enemy have a breathing idle animation?")]
    public bool enableIdleAnimation = true;
    [Tooltip("Speed of the breathing animation.")]
    public float idleAnimationSpeed = 2f;
    [Tooltip("How much the scale changes during breathing.")]
    public float idleAnimationAmplitude = 0.05f;

    [Header("Walk / Patrol Animation Settings")]
    [Tooltip("Should the enemy have a walk / patrol animation while moving?")]
    public bool enableWalkPatrolAnimation = true;
    [Tooltip("Speed of the walk / patrol animation.")]
    public float walkPatrolAnimationSpeed = 8f;
    [Tooltip("How much the walk / patrol animation changes scale.")]
    public float walkPatrolAnimationAmplitude = 0.04f;
    [Tooltip("How much the walk / patrol animation tilts left and right in degrees.")]
    public float walkPatrolRotationAmplitude = 6f;

    [Header("Ranges")]
    [Tooltip("Detection range (X, Y) for Aggressive and FleeOnSight behavior.")]
    public Vector2 detectionRange = new Vector2(5f, 5f);

    [Tooltip("Disengage range (X, Y) where enemy stops chasing (or stops fleeing).")]
    public Vector2 disengageRange = new Vector2(10f, 10f);

    [Tooltip("Minimum distance (X, Y) to keep from the player to avoid overlapping.")]
    public Vector2 stoppingDistance = new Vector2(1.5f, 1.5f);

    [Header("References")]
    [Tooltip("Assign the Player's Collider here. If left empty, it will auto-detect a valid player body collider.")]
    public Collider2D playerCollider;
    [Tooltip("Assign the Enemy's Collider here (optional, will auto-detect).")]
    public Collider2D ownCollider;
    [Tooltip("Optional child transform used for the enemy shadow sprite.")]
    public Transform shadowChild;
    [FormerlySerializedAs("flipShadowWithFacing")]
    [Tooltip("If true, the assigned shadow child will mirror its local X position when facing left or right.")]
    public bool mirrorShadowPositionWithFacing = false;

    private EnemyHealth enemyHealth;
    private SpriteRenderer spriteRenderer;
    private Rigidbody2D rb; // Reference to Rigidbody
    private Transform idleAnimationTarget;
    private Vector3 idleAnimationBaseScale = Vector3.one;
    private Transform[] idleAnimationChildTargets;
    private Vector3[] idleAnimationChildBaseScales;
    private Quaternion[] idleAnimationChildBaseRotations;
    private float lastAnimationRotationOffset = 0f;
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
    private bool idleLockWasActive = false;
    private bool walkPatrolLockWasActive = false;
    private Transform cachedShadowChild;
    private Vector3 shadowChildBaseLocalPosition;
    private bool hasShadowChildBaseLocalPosition = false;

    private void Start()
    {
        enemyHealth = GetComponent<EnemyHealth>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        }
        CacheShadowChildLocalPosition();
        rb = GetComponent<Rigidbody2D>();
        idleAnimationTarget = transform;
        idleAnimationBaseScale = idleAnimationTarget.localScale;
        CacheIdleAnimationChildren();

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

        TryAssignPlayerCollider();
    }

    private void OnDestroy()
    {
        ResetMovementAnimation();

        if (enemyHealth != null)
        {
            enemyHealth.OnDamageTaken -= HandleDamageTaken;
        }
    }

    private void OnDisable()
    {
        ResetMovementAnimation();
    }

    private void Update()
    {
        ResetAnimationRotationOffset();

        if (enemyHealth.IsDead)
        {
            return;
        }

        if (debugLockIdle)
        {
            walkPatrolLockWasActive = false;
            ApplyIdleLock();
            return;
        }

        idleLockWasActive = false;

        if (debugLockWalkPatrol)
        {
            ApplyWalkPatrolLock();
            Patrol();
            return;
        }

        walkPatrolLockWasActive = false;

        if (playerCollider != null && !IsLivePlayerCollider(playerCollider))
        {
            ClearPlayerTarget();
            ClearAggroState();
        }

        if (playerCollider == null)
        {
            TryAssignPlayerCollider();
        }

        if (playerCollider == null)
        {
            Patrol();
            return;
        }

        // Use centers of colliders for interaction logic
        Vector3 playerPos = playerCollider.bounds.center;
        Vector3 myPos = ownCollider != null ? ownCollider.bounds.center : transform.position;

        // 1. Check triggers to START aggression
        if (!IsAggroed)
        {
            if (behavior == AIBehavior.Aggressive || behavior == AIBehavior.FleeOnSight)
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
                ClearAggroState();
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
        if (debugLockIdle || debugLockWalkPatrol)
        {
            return;
        }

        // Trigger aggression/action state for Retaliatory (Chase) and Passive (Flee)
        if (behavior == AIBehavior.Retaliatory ||
            behavior == AIBehavior.Passive ||
            behavior == AIBehavior.FleeOnSight)
        {
            IsAggroed = true;
        }
    }

    private void LateUpdate()
    {
        UpdateMovementAnimation();
    }

    private void ApplyIdleLock()
    {
        Vector2 currentPos = ownCollider != null ? ownCollider.bounds.center : transform.position;

        if (!idleLockWasActive)
        {
            IsAggroed = false;
            nextMoveTime = Time.time + waitTime;
            idleLockWasActive = true;
        }

        patrolTarget = currentPos;
        isReturningToPatrol = false;
        lastWallID = 0;
        lastWallSide = 0f;
        currentWallID = 0;
        slideSide = 0f;
        clearPathTimer = 0f;
        lockedSlideDirection = Vector2.zero;
        lockedFleeDirection = Vector2.zero;
        fleeLockTimer = 0f;

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
        }
    }

    private void ApplyWalkPatrolLock()
    {
        if (!walkPatrolLockWasActive)
        {
            ClearAggroState();
            nextMoveTime = Time.time;
            walkPatrolLockWasActive = true;
        }

        IsAggroed = false;
    }

    private void UpdateMovementAnimation()
    {
        if (idleAnimationTarget == null)
        {
            return;
        }

        if (enemyHealth != null && enemyHealth.IsDead)
        {
            ResetMovementAnimation();
            return;
        }

        bool isMoving = rb != null && rb.linearVelocity.sqrMagnitude > 0.0001f;
        if (isMoving && enableWalkPatrolAnimation)
        {
            ApplyWalkPatrolAnimation();
            return;
        }

        if (!isMoving && enableIdleAnimation)
        {
            ApplyIdleAnimation();
            return;
        }

        ResetMovementAnimation();
    }

    private void ApplyIdleAnimation()
    {
        float idlePhase = Time.time * idleAnimationSpeed;
        float breathe = (Mathf.Sin(idlePhase - (Mathf.PI * 0.5f)) + 1f) * 0.5f;
        breathe = Mathf.SmoothStep(0f, 1f, breathe);

        float xScaleFactor = 1f - (idleAnimationAmplitude * 0.35f * breathe);
        float yScaleFactor = 1f + (idleAnimationAmplitude * 0.85f * breathe);

        idleAnimationTarget.localScale = new Vector3(
            idleAnimationBaseScale.x * Mathf.Max(xScaleFactor, 0.01f),
            idleAnimationBaseScale.y * Mathf.Max(yScaleFactor, 0.01f),
            idleAnimationBaseScale.z);
        ApplyIdleAnimationChildScaleCompensation();
    }

    private void ApplyWalkPatrolAnimation()
    {
        float normalizedSpeed = GetMovementAnimationNormalizedSpeed();
        float walkPhase = Time.time * walkPatrolAnimationSpeed * Mathf.Lerp(0.7f, 1.2f, normalizedSpeed);
        float sway = Mathf.Sin(walkPhase);
        float footPlant = (Mathf.Sin((walkPhase * 2f) - (Mathf.PI * 0.5f)) + 1f) * 0.5f;
        footPlant = Mathf.SmoothStep(0f, 1f, footPlant);

        float xScaleFactor = 1f + (walkPatrolAnimationAmplitude * 0.55f * footPlant);
        float yScaleFactor = 1f - (walkPatrolAnimationAmplitude * 0.8f * footPlant);

        idleAnimationTarget.localScale = new Vector3(
            idleAnimationBaseScale.x * Mathf.Max(xScaleFactor, 0.01f),
            idleAnimationBaseScale.y * Mathf.Max(yScaleFactor, 0.01f),
            idleAnimationBaseScale.z);
        ApplyIdleAnimationChildScaleCompensation();

        ApplyAnimationRotationOffset(walkPatrolRotationAmplitude * sway * normalizedSpeed);
    }

    private float GetMovementAnimationNormalizedSpeed()
    {
        if (rb == null)
        {
            return 1f;
        }

        float referenceSpeed = Mathf.Max(Mathf.Max(patrolSpeed, chaseSpeed), Mathf.Max(fleeSpeed, 0.01f));
        float normalizedSpeed = Mathf.Clamp01(rb.linearVelocity.magnitude / referenceSpeed);
        return Mathf.Lerp(0.45f, 1f, normalizedSpeed);
    }

    private void ResetMovementAnimation()
    {
        ResetAnimationRotationOffset();

        if (idleAnimationTarget != null)
        {
            idleAnimationTarget.localScale = idleAnimationBaseScale;
        }

        RestoreIdleAnimationChildScales();
        RestoreIdleAnimationChildRotations();
    }

    private void ResetAnimationRotationOffset()
    {
        if (Mathf.Abs(lastAnimationRotationOffset) < 0.001f)
        {
            return;
        }

        transform.rotation *= Quaternion.Euler(0f, 0f, -lastAnimationRotationOffset);
        RestoreIdleAnimationChildRotations();
        lastAnimationRotationOffset = 0f;
    }

    private void ApplyAnimationRotationOffset(float rotationOffset)
    {
        if (Mathf.Abs(rotationOffset) < 0.001f)
        {
            RestoreIdleAnimationChildRotations();
            lastAnimationRotationOffset = 0f;
            return;
        }

        transform.rotation *= Quaternion.Euler(0f, 0f, rotationOffset);
        ApplyIdleAnimationChildRotationCompensation(rotationOffset);
        lastAnimationRotationOffset = rotationOffset;
    }

    private void CacheIdleAnimationChildren()
    {
        int childCount = transform.childCount;
        idleAnimationChildTargets = new Transform[childCount];
        idleAnimationChildBaseScales = new Vector3[childCount];
        idleAnimationChildBaseRotations = new Quaternion[childCount];

        for (int i = 0; i < childCount; i++)
        {
            Transform child = transform.GetChild(i);
            idleAnimationChildTargets[i] = child;
            idleAnimationChildBaseScales[i] = child.localScale;
            idleAnimationChildBaseRotations[i] = child.localRotation;
        }
    }

    private void ApplyIdleAnimationChildScaleCompensation()
    {
        if (idleAnimationChildTargets == null || idleAnimationTarget == null)
        {
            return;
        }

        Vector3 parentScaleRatio = new Vector3(
            SafeDivide(idleAnimationTarget.localScale.x, idleAnimationBaseScale.x),
            SafeDivide(idleAnimationTarget.localScale.y, idleAnimationBaseScale.y),
            SafeDivide(idleAnimationTarget.localScale.z, idleAnimationBaseScale.z));

        for (int i = 0; i < idleAnimationChildTargets.Length; i++)
        {
            Transform child = idleAnimationChildTargets[i];
            if (child == null)
            {
                continue;
            }

            Vector3 baseScale = idleAnimationChildBaseScales[i];
            child.localScale = new Vector3(
                SafeDivide(baseScale.x, parentScaleRatio.x),
                SafeDivide(baseScale.y, parentScaleRatio.y),
                SafeDivide(baseScale.z, parentScaleRatio.z));
        }
    }

    private void RestoreIdleAnimationChildScales()
    {
        if (idleAnimationChildTargets == null)
        {
            return;
        }

        for (int i = 0; i < idleAnimationChildTargets.Length; i++)
        {
            Transform child = idleAnimationChildTargets[i];
            if (child != null)
            {
                child.localScale = idleAnimationChildBaseScales[i];
            }
        }
    }

    private void ApplyIdleAnimationChildRotationCompensation(float rotationOffset)
    {
        if (idleAnimationChildTargets == null || idleAnimationChildBaseRotations == null)
        {
            return;
        }

        Quaternion inverseRotationOffset = Quaternion.Euler(0f, 0f, -rotationOffset);

        for (int i = 0; i < idleAnimationChildTargets.Length; i++)
        {
            Transform child = idleAnimationChildTargets[i];
            if (child != null)
            {
                child.localRotation = inverseRotationOffset * idleAnimationChildBaseRotations[i];
            }
        }
    }

    private void RestoreIdleAnimationChildRotations()
    {
        if (idleAnimationChildTargets == null || idleAnimationChildBaseRotations == null)
        {
            return;
        }

        for (int i = 0; i < idleAnimationChildTargets.Length; i++)
        {
            Transform child = idleAnimationChildTargets[i];
            if (child != null)
            {
                child.localRotation = idleAnimationChildBaseRotations[i];
            }
        }
    }

    private static float SafeDivide(float numerator, float denominator)
    {
        if (Mathf.Abs(denominator) < 0.0001f)
        {
            return 1f;
        }

        return numerator / denominator;
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
            if (behavior == AIBehavior.Passive || behavior == AIBehavior.FleeOnSight)
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
            bool shouldFlip = direction.x < 0f;
            spriteRenderer.flipX = shouldFlip;

            CacheShadowChildLocalPosition();
            if (shadowChild != null && hasShadowChildBaseLocalPosition)
            {
                Vector3 shadowLocalPosition = shadowChildBaseLocalPosition;

                if (mirrorShadowPositionWithFacing)
                {
                    shadowLocalPosition.x = shouldFlip
                        ? -shadowChildBaseLocalPosition.x
                        : shadowChildBaseLocalPosition.x;
                }

                shadowChild.localPosition = shadowLocalPosition;
            }
        }
    }

    private void CacheShadowChildLocalPosition()
    {
        if (shadowChild == null)
        {
            cachedShadowChild = null;
            hasShadowChildBaseLocalPosition = false;
            return;
        }

        if (cachedShadowChild != shadowChild || !hasShadowChildBaseLocalPosition)
        {
            cachedShadowChild = shadowChild;
            shadowChildBaseLocalPosition = shadowChild.localPosition;
            hasShadowChildBaseLocalPosition = true;
        }
    }

    private void TryAssignPlayerCollider()
    {
        if (IsLivePlayerCollider(playerCollider))
        {
            return;
        }

        ClearPlayerTarget();

        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj == null)
        {
            return;
        }

        playerCollider = FindPreferredPlayerCollider(playerObj);
    }

    private Collider2D FindPreferredPlayerCollider(GameObject playerObj)
    {
        if (playerObj == null)
        {
            return null;
        }

        PlayerStats stats = ResolvePlayerStats(playerObj.transform);
        if (stats != null && stats.IsDead)
        {
            return null;
        }

        Collider2D rootCollider = playerObj.GetComponent<Collider2D>();
        if (IsValidPlayerBodyCollider(rootCollider))
        {
            return rootCollider;
        }

        Rigidbody2D playerRb = playerObj.GetComponent<Rigidbody2D>();
        Collider2D[] colliders = playerObj.GetComponentsInChildren<Collider2D>(true);

        Collider2D triggerFallback = null;

        for (int i = 0; i < colliders.Length; i++)
        {
            Collider2D candidate = colliders[i];
            if (candidate == null || !candidate.enabled)
            {
                continue;
            }

            if (playerRb != null && candidate.attachedRigidbody == playerRb && !candidate.isTrigger)
            {
                return candidate;
            }

            if (!candidate.isTrigger && triggerFallback == null)
            {
                triggerFallback = candidate;
            }
        }

        if (triggerFallback != null)
        {
            return triggerFallback;
        }

        for (int i = 0; i < colliders.Length; i++)
        {
            Collider2D candidate = colliders[i];
            if (candidate != null && candidate.enabled)
            {
                return candidate;
            }
        }

        return null;
    }

    private bool IsValidPlayerBodyCollider(Collider2D candidate)
    {
        return candidate != null && candidate.enabled && !candidate.isTrigger;
    }

    public void ClearAggroState()
    {
        IsAggroed = false;
        isReturningToPatrol = true;
        lastWallID = 0;
        lastWallSide = 0f;
        currentWallID = 0;
        slideSide = 0f;
        clearPathTimer = 0f;
        lockedSlideDirection = Vector2.zero;
        lockedFleeDirection = Vector2.zero;
        fleeLockTimer = 0f;

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
        }

        if (resetPatrolAfterAggro)
        {
            startPosition = ownCollider != null ? ownCollider.bounds.center : transform.position;
            patrolTarget = startPosition;
            nextMoveTime = Time.time + waitTime;
        }
        else
        {
            patrolTarget = startPosition;
        }
    }

    private void ClearPlayerTarget()
    {
        playerCollider = null;
    }

    private static PlayerStats ResolvePlayerStats(Component source)
    {
        if (source == null)
        {
            return null;
        }

        PlayerStats stats = source.GetComponent<PlayerStats>();
        if (stats == null)
        {
            stats = source.GetComponentInParent<PlayerStats>();
        }

        return stats;
    }

    private bool IsLivePlayerCollider(Collider2D candidate)
    {
        if (candidate == null || !candidate.enabled || !candidate.gameObject.activeInHierarchy)
        {
            return false;
        }

        PlayerStats stats = ResolvePlayerStats(candidate);
        return stats != null && !stats.IsDead;
    }

    private void OnDrawGizmosSelected()
    {
        DrawDetectionRangeGizmo();
        Vector3 drawCenter = GetGizmoDrawCenter();

        DrawEllipse(drawCenter, disengageRange, Color.yellow);

        DrawEllipse(drawCenter, stoppingDistance, Color.blue);

        Vector3 patrolCenter = Application.isPlaying ? (Vector3)startPosition : drawCenter;
        DrawEllipse(patrolCenter, patrolRange, Color.green);
    }

    private void DrawDetectionRangeGizmo()
    {
        if (behavior != AIBehavior.Aggressive && behavior != AIBehavior.FleeOnSight)
        {
            return;
        }

        DrawEllipse(GetGizmoDrawCenter(), detectionRange, Color.red);
    }

    private Vector3 GetGizmoDrawCenter()
    {
        Vector3 drawCenter = transform.position;

        if (ownCollider != null)
        {
            drawCenter = ownCollider.bounds.center;
        }
        else
        {
            Collider2D col = GetComponent<Collider2D>();
            if (col != null)
            {
                drawCenter = col.bounds.center;
            }
        }

        return drawCenter;
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
