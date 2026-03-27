using System.Collections.Generic;
using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    public float moveSpeed = 5f;
    public float sprintSpeed = 8f;

    [Header("References")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Transform visualAnimationTarget;

    [Header("Walk Animation Settings")]
    [SerializeField] private bool enableWalkAnimation = true;
    [SerializeField] private float walkAnimationSpeed = 8f;
    [SerializeField] private float walkAnimationAmplitude = 0.04f;
    [SerializeField] private float walkRotationAmplitude = 6f;

    [Header("Attack Animation Settings")]
    [SerializeField] private bool enableAttackAnimation = true;
    [SerializeField] private float attackWindupAnimationAmplitude = 0.08f;
    [SerializeField] private float attackSlashAnimationAmplitude = 0.12f;
    [SerializeField] private float attackWindupPositionYOffset = 0f;
    [SerializeField] private float attackSlashPositionYOffset = 0f;
    [SerializeField] private float attackWindupRotationAmplitude = 10f;
    [SerializeField] private float attackSlashRotationAmplitude = 18f;
    [SerializeField, Range(0.05f, 1f)] private float attackSlashPhaseRatio = 0.45f;

    [Header("Tool Animation Settings")]
    [SerializeField] private float toolAttackRotationAmplitude = 45f;
    [SerializeField] private ToolManager toolManager;

    private Rigidbody2D rb;
    public Vector2 movement;
    private PlayerStats stats;
    private bool isSprinting;
    private readonly Dictionary<int, float> movementSpeedMultipliers = new Dictionary<int, float>();
    private Transform walkAnimationTarget;
    private Vector3 walkAnimationBaseScale = Vector3.one;
    private Vector3 walkAnimationBaseLocalPosition = Vector3.zero;
    private Quaternion walkAnimationBaseLocalRotation = Quaternion.identity;
    private Transform[] walkAnimationChildTargets;
    private Vector3[] walkAnimationChildBaseScales;
    private Quaternion[] walkAnimationChildBaseRotations;
    private float lastAnimationRotationOffset;
    private bool isAttackAnimating;
    private Vector2 attackAnimationDirection = Vector2.right;
    private float attackAnimationStartTime;
    private float attackAnimationWindupDuration;
    private float attackAnimationSlashDuration;
    private float attackAnimationRecoveryDuration;
    private Quaternion toolBaseLocalRotation = Quaternion.identity;
    private Transform lastActiveTool;

    private void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        stats = GetComponent<PlayerStats>();
        if (toolManager == null) toolManager = GetComponent<ToolManager>();
        ResolveAnimationReferences();
        CacheWalkAnimationState();
    }

    private void OnValidate()
    {
        ResolveAnimationReferences();

        if (!Application.isPlaying)
        {
            CacheWalkAnimationState();
        }
    }

    private void OnDisable()
    {
        ResetMovementAnimation();
    }

    private void OnDestroy()
    {
        ResetMovementAnimation();
    }

    private void Update()
    {
        if (Time.timeScale == 0)
        {
            movement = Vector2.zero;
            isSprinting = false;
            return;
        }

        if (isAttackAnimating)
        {
            movement = Vector2.zero;
            isSprinting = false;
            return;
        }

        movement.x = Input.GetAxisRaw("Horizontal");
        movement.y = Input.GetAxisRaw("Vertical");
        movement = movement.normalized;

        bool isMoving = movement.sqrMagnitude > 0f;
        isSprinting = Input.GetKey(KeyCode.LeftShift) && stats != null && stats.Energy > 0 && isMoving;

        if (isSprinting)
        {
            stats.UseEnergy(stats.sprintEnergyCost * Time.deltaTime);
        }

        if (!isAttackAnimating)
        {
            SetFacingDirection(movement);
        }
    }

    private void FixedUpdate()
    {
        float currentSpeed = GetCurrentMovementSpeed();
        rb.MovePosition(rb.position + movement * currentSpeed * Time.fixedDeltaTime);
    }

    private void LateUpdate()
    {
        UpdateMovementAnimation();
    }

    public void SetMovementSpeedMultiplier(Object source, float multiplier)
    {
        if (source == null)
        {
            return;
        }

        movementSpeedMultipliers[source.GetInstanceID()] = Mathf.Max(0f, multiplier);
    }

    public void ClearMovementSpeedMultiplier(Object source)
    {
        if (source == null)
        {
            return;
        }

        movementSpeedMultipliers.Remove(source.GetInstanceID());
    }

    public void SetFacingDirection(Vector2 direction)
    {
        if (spriteRenderer == null)
        {
            return;
        }

        if (direction.x > 0.001f)
        {
            spriteRenderer.flipX = false;
        }
        else if (direction.x < -0.001f)
        {
            spriteRenderer.flipX = true;
        }
    }

    public void StartAttackAnimation(Vector2 direction, float windupDuration, float slashDuration, float recoveryDuration)
    {
        ResolveAnimationReferences();

        if (walkAnimationTarget == null)
        {
            CacheWalkAnimationState();
        }
        else
        {
            ResetMovementAnimation();
        }

        isAttackAnimating = true;
        movement = Vector2.zero;
        isSprinting = false;
        attackAnimationDirection = direction.sqrMagnitude > 0.001f ? direction.normalized : GetCurrentFacingDirection();
        SetFacingDirection(attackAnimationDirection);
        attackAnimationStartTime = Time.time;
        attackAnimationWindupDuration = Mathf.Max(0f, windupDuration);
        attackAnimationSlashDuration = Mathf.Max(0f, slashDuration);
        attackAnimationRecoveryDuration = Mathf.Max(0f, recoveryDuration);

        // Cache tool rotation
        if (toolManager != null)
        {
            lastActiveTool = toolManager.GetActiveToolTransform();
            if (lastActiveTool != null)
            {
                toolBaseLocalRotation = lastActiveTool.localRotation;
            }
        }
    }

    public void StartAttackAnimation(Vector2 direction, float totalDuration)
    {
        float clampedDuration = Mathf.Max(0f, totalDuration);
        float slashDuration = clampedDuration * Mathf.Clamp01(attackSlashPhaseRatio);
        float recoveryDuration = Mathf.Max(clampedDuration - slashDuration, 0f);
        StartAttackAnimation(direction, 0f, slashDuration, recoveryDuration);
    }

    public void EndAttackAnimation()
    {
        isAttackAnimating = false;
        attackAnimationWindupDuration = 0f;
        attackAnimationSlashDuration = 0f;
        attackAnimationRecoveryDuration = 0f;
        ResetMovementAnimation();
    }

    private float GetCurrentMovementSpeed()
    {
        if (isAttackAnimating)
        {
            return 0f;
        }

        return (isSprinting ? sprintSpeed : moveSpeed) * GetMovementSpeedMultiplier();
    }

    private float GetMovementSpeedMultiplier()
    {
        float lowestMultiplier = 1f;

        foreach (float multiplier in movementSpeedMultipliers.Values)
        {
            lowestMultiplier = Mathf.Min(lowestMultiplier, multiplier);
        }

        return lowestMultiplier;
    }

    private void UpdateMovementAnimation()
    {
        if (walkAnimationTarget == null)
        {
            return;
        }

        if (isAttackAnimating)
        {
            if (enableAttackAnimation && ApplyAttackAnimation())
            {
                return;
            }

            EndAttackAnimation();
        }

        if (enableWalkAnimation && movement.sqrMagnitude > 0.0001f)
        {
            ApplyWalkAnimation();
            return;
        }

        ResetMovementAnimation();
    }

    private void ApplyWalkAnimation()
    {
        float normalizedSpeed = GetMovementAnimationNormalizedSpeed();
        float walkPhase = Time.time * walkAnimationSpeed * Mathf.Lerp(0.7f, 1.2f, normalizedSpeed);
        float sway = Mathf.Sin(walkPhase);
        float footPlant = (Mathf.Sin((walkPhase * 2f) - (Mathf.PI * 0.5f)) + 1f) * 0.5f;
        footPlant = Mathf.SmoothStep(0f, 1f, footPlant);

        float xScaleFactor = 1f + (walkAnimationAmplitude * 0.55f * footPlant);
        float yScaleFactor = 1f - (walkAnimationAmplitude * 0.8f * footPlant);

        walkAnimationTarget.localScale = new Vector3(
            walkAnimationBaseScale.x * Mathf.Max(xScaleFactor, 0.01f),
            walkAnimationBaseScale.y * Mathf.Max(yScaleFactor, 0.01f),
            walkAnimationBaseScale.z);
        ApplyWalkAnimationChildScaleCompensation();
        ApplyAnimationRotationOffset(walkRotationAmplitude * sway * normalizedSpeed);
    }

    private bool ApplyAttackAnimation()
    {
        float attackDuration = attackAnimationWindupDuration + attackAnimationSlashDuration + attackAnimationRecoveryDuration;
        if (attackDuration <= 0.0001f)
        {
            ApplyAttackPose(
                1f + attackSlashAnimationAmplitude,
                1f - (attackSlashAnimationAmplitude * 0.72f),
                GetAttackAnimationRotationSign(attackAnimationDirection) * attackSlashRotationAmplitude,
                attackSlashPositionYOffset,
                1f);
            return false;
        }

        float elapsed = Mathf.Max(Time.time - attackAnimationStartTime, 0f);
        if (elapsed >= attackDuration)
        {
            return false;
        }

        float rotationSign = GetAttackAnimationRotationSign(attackAnimationDirection);
        float windupX = 1f - (attackWindupAnimationAmplitude * 0.45f);
        float windupY = 1f + (attackWindupAnimationAmplitude * 0.8f);
        float slashX = 1f + attackSlashAnimationAmplitude;
        float slashY = 1f - (attackSlashAnimationAmplitude * 0.72f);

        if (elapsed < attackAnimationWindupDuration && attackAnimationWindupDuration > 0.0001f)
        {
            float phaseProgress = Mathf.SmoothStep(0f, 1f, elapsed / attackAnimationWindupDuration);
            ApplyAttackPose(
                Mathf.Lerp(1f, windupX, phaseProgress),
                Mathf.Lerp(1f, windupY, phaseProgress),
                Mathf.Lerp(0f, -attackWindupRotationAmplitude * rotationSign, phaseProgress),
                Mathf.Lerp(0f, attackWindupPositionYOffset, phaseProgress),
                -phaseProgress * 0.5f);
            return true;
        }

        elapsed -= attackAnimationWindupDuration;
        if (elapsed < attackAnimationSlashDuration && attackAnimationSlashDuration > 0.0001f)
        {
            float phaseProgress = Mathf.SmoothStep(0f, 1f, elapsed / attackAnimationSlashDuration);
            float slashStartX = attackAnimationWindupDuration > 0.0001f ? windupX : 1f;
            float slashStartY = attackAnimationWindupDuration > 0.0001f ? windupY : 1f;
            float slashStartRotation = attackAnimationWindupDuration > 0.0001f ? -attackWindupRotationAmplitude : 0f;
            float slashStartYOffset = attackAnimationWindupDuration > 0.0001f ? attackWindupPositionYOffset : 0f;
            ApplyAttackPose(
                Mathf.Lerp(slashStartX, slashX, phaseProgress),
                Mathf.Lerp(slashStartY, slashY, phaseProgress),
                Mathf.Lerp(slashStartRotation, attackSlashRotationAmplitude, phaseProgress) * rotationSign,
                Mathf.Lerp(slashStartYOffset, attackSlashPositionYOffset, phaseProgress),
                Mathf.Lerp(-0.5f, 1f, phaseProgress));
            return true;
        }

        elapsed -= attackAnimationSlashDuration;
        float recoveryProgress = attackAnimationRecoveryDuration > 0.0001f
            ? Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / attackAnimationRecoveryDuration))
            : 1f;
        ApplyAttackPose(
            Mathf.Lerp(slashX, 1f, recoveryProgress),
            Mathf.Lerp(slashY, 1f, recoveryProgress),
            Mathf.Lerp(attackSlashRotationAmplitude * rotationSign, 0f, recoveryProgress),
            Mathf.Lerp(attackSlashPositionYOffset, 0f, recoveryProgress),
            Mathf.Lerp(1f, 0f, recoveryProgress));
        return true;
    }

    private void ApplyAttackPose(float xScaleFactor, float yScaleFactor, float rotationOffset, float positionYOffset, float toolProgress)
    {
        walkAnimationTarget.localScale = new Vector3(
            walkAnimationBaseScale.x * Mathf.Max(xScaleFactor, 0.01f),
            walkAnimationBaseScale.y * Mathf.Max(yScaleFactor, 0.01f),
            walkAnimationBaseScale.z);
        ApplyWalkAnimationChildScaleCompensation();
        ApplyAnimationPositionYOffset(positionYOffset);
        ApplyAnimationRotationOffset(rotationOffset);

        // Animate Tool
        if (toolManager != null)
        {
            Transform activeTool = toolManager.GetActiveToolTransform();
            if (activeTool != null)
            {
                float toolRot = toolProgress * toolAttackRotationAmplitude * GetAttackAnimationRotationSign(attackAnimationDirection);
                activeTool.localRotation = toolBaseLocalRotation * Quaternion.Euler(0, 0, toolRot);
            }
        }
    }

    private float GetMovementAnimationNormalizedSpeed()
    {
        float referenceSpeed = Mathf.Max(
            Mathf.Max(moveSpeed * GetMovementSpeedMultiplier(), sprintSpeed * GetMovementSpeedMultiplier()),
            0.01f);
        float currentSpeed = movement.sqrMagnitude > 0.0001f ? GetCurrentMovementSpeed() * movement.magnitude : 0f;
        float normalizedSpeed = Mathf.Clamp01(currentSpeed / referenceSpeed);
        return Mathf.Lerp(0.45f, 1f, normalizedSpeed);
    }

    private float GetAttackAnimationRotationSign(Vector2 direction)
    {
        if (direction.x > 0.05f)
        {
            return 1f;
        }

        if (direction.x < -0.05f)
        {
            return -1f;
        }

        if (spriteRenderer != null && spriteRenderer.flipX)
        {
            return -1f;
        }

        return direction.y < 0f ? -1f : 1f;
    }

    private Vector2 GetCurrentFacingDirection()
    {
        if (spriteRenderer != null && spriteRenderer.flipX)
        {
            return Vector2.left;
        }

        return Vector2.right;
    }

    private void ResolveAnimationReferences()
    {
        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponentInChildren<SpriteRenderer>(true);
        }

        if (visualAnimationTarget == null)
        {
            visualAnimationTarget = spriteRenderer != null ? spriteRenderer.transform : transform;
        }
    }

    private void CacheWalkAnimationState()
    {
        walkAnimationTarget = visualAnimationTarget != null
            ? visualAnimationTarget
            : (spriteRenderer != null ? spriteRenderer.transform : transform);

        if (walkAnimationTarget == null)
        {
            walkAnimationChildTargets = System.Array.Empty<Transform>();
            walkAnimationChildBaseScales = System.Array.Empty<Vector3>();
            walkAnimationChildBaseRotations = System.Array.Empty<Quaternion>();
            return;
        }

        walkAnimationBaseScale = walkAnimationTarget.localScale;
        walkAnimationBaseLocalPosition = walkAnimationTarget.localPosition;
        walkAnimationBaseLocalRotation = walkAnimationTarget.localRotation;
        CacheWalkAnimationChildren();
    }

    private void ResetMovementAnimation()
    {
        ResetAnimationRotationOffset();

        if (walkAnimationTarget != null)
        {
            walkAnimationTarget.localScale = walkAnimationBaseScale;
            walkAnimationTarget.localPosition = walkAnimationBaseLocalPosition;

            if (!IsVisualAnimationAppliedToRoot())
            {
                walkAnimationTarget.localRotation = walkAnimationBaseLocalRotation;
            }
        }

        if (lastActiveTool != null)
        {
            lastActiveTool.localRotation = toolBaseLocalRotation;
        }

        RestoreWalkAnimationChildScales();
        RestoreWalkAnimationChildRotations();
    }

    private void ResetAnimationRotationOffset()
    {
        if (Mathf.Abs(lastAnimationRotationOffset) < 0.001f)
        {
            return;
        }

        if (IsVisualAnimationAppliedToRoot())
        {
            transform.rotation *= Quaternion.Euler(0f, 0f, -lastAnimationRotationOffset);
            RestoreWalkAnimationChildRotations();
        }
        else if (walkAnimationTarget != null)
        {
            walkAnimationTarget.localRotation = walkAnimationBaseLocalRotation;
        }

        lastAnimationRotationOffset = 0f;
    }

    private void ApplyAnimationRotationOffset(float rotationOffset)
    {
        if (Mathf.Abs(rotationOffset) < 0.001f)
        {
            RestoreWalkAnimationChildRotations();
            lastAnimationRotationOffset = 0f;
            return;
        }

        if (IsVisualAnimationAppliedToRoot())
        {
            transform.rotation *= Quaternion.Euler(0f, 0f, rotationOffset);
            ApplyWalkAnimationChildRotationCompensation(rotationOffset);
        }
        else if (walkAnimationTarget != null)
        {
            walkAnimationTarget.localRotation = walkAnimationBaseLocalRotation * Quaternion.Euler(0f, 0f, rotationOffset);
        }

        lastAnimationRotationOffset = rotationOffset;
    }

    private void ApplyAnimationPositionYOffset(float yOffset)
    {
        if (walkAnimationTarget == null)
        {
            return;
        }

        if (IsVisualAnimationAppliedToRoot())
        {
            walkAnimationTarget.localPosition = walkAnimationBaseLocalPosition;
            return;
        }

        walkAnimationTarget.localPosition = walkAnimationBaseLocalPosition + new Vector3(0f, yOffset, 0f);
    }

    private void CacheWalkAnimationChildren()
    {
        if (walkAnimationTarget == null)
        {
            walkAnimationChildTargets = System.Array.Empty<Transform>();
            walkAnimationChildBaseScales = System.Array.Empty<Vector3>();
            walkAnimationChildBaseRotations = System.Array.Empty<Quaternion>();
            return;
        }

        int childCount = walkAnimationTarget.childCount;
        walkAnimationChildTargets = new Transform[childCount];
        walkAnimationChildBaseScales = new Vector3[childCount];
        walkAnimationChildBaseRotations = new Quaternion[childCount];

        for (int i = 0; i < childCount; i++)
        {
            Transform child = walkAnimationTarget.GetChild(i);
            walkAnimationChildTargets[i] = child;
            walkAnimationChildBaseScales[i] = child.localScale;
            walkAnimationChildBaseRotations[i] = child.localRotation;
        }
    }

    private void ApplyWalkAnimationChildScaleCompensation()
    {
        if (walkAnimationChildTargets == null || walkAnimationTarget == null)
        {
            return;
        }

        Vector3 parentScaleRatio = new Vector3(
            SafeDivide(walkAnimationTarget.localScale.x, walkAnimationBaseScale.x),
            SafeDivide(walkAnimationTarget.localScale.y, walkAnimationBaseScale.y),
            SafeDivide(walkAnimationTarget.localScale.z, walkAnimationBaseScale.z));

        for (int i = 0; i < walkAnimationChildTargets.Length; i++)
        {
            Transform child = walkAnimationChildTargets[i];
            if (child == null)
            {
                continue;
            }

            Vector3 baseScale = walkAnimationChildBaseScales[i];
            child.localScale = new Vector3(
                SafeDivide(baseScale.x, parentScaleRatio.x),
                SafeDivide(baseScale.y, parentScaleRatio.y),
                SafeDivide(baseScale.z, parentScaleRatio.z));
        }
    }

    private void RestoreWalkAnimationChildScales()
    {
        if (walkAnimationChildTargets == null)
        {
            return;
        }

        for (int i = 0; i < walkAnimationChildTargets.Length; i++)
        {
            Transform child = walkAnimationChildTargets[i];
            if (child != null)
            {
                child.localScale = walkAnimationChildBaseScales[i];
            }
        }
    }

    private void ApplyWalkAnimationChildRotationCompensation(float rotationOffset)
    {
        if (walkAnimationChildTargets == null || walkAnimationChildBaseRotations == null)
        {
            return;
        }

        Quaternion inverseRotationOffset = Quaternion.Euler(0f, 0f, -rotationOffset);

        for (int i = 0; i < walkAnimationChildTargets.Length; i++)
        {
            Transform child = walkAnimationChildTargets[i];
            if (child != null)
            {
                child.localRotation = inverseRotationOffset * walkAnimationChildBaseRotations[i];
            }
        }
    }

    private void RestoreWalkAnimationChildRotations()
    {
        if (walkAnimationChildTargets == null || walkAnimationChildBaseRotations == null)
        {
            return;
        }

        for (int i = 0; i < walkAnimationChildTargets.Length; i++)
        {
            Transform child = walkAnimationChildTargets[i];
            if (child != null)
            {
                child.localRotation = walkAnimationChildBaseRotations[i];
            }
        }
    }

    private bool IsVisualAnimationAppliedToRoot()
    {
        return walkAnimationTarget == null || walkAnimationTarget == transform;
    }

    private static float SafeDivide(float numerator, float denominator)
    {
        if (Mathf.Abs(denominator) < 0.0001f)
        {
            return 1f;
        }

        return numerator / denominator;
    }
}
