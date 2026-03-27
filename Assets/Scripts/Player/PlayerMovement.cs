using System.Collections.Generic;
using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    public float moveSpeed = 5f;
    public float sprintSpeed = 8f;

    [SerializeField] private SpriteRenderer spriteRenderer;

    private Rigidbody2D rb;
    public Vector2 movement;
    private PlayerStats stats;
    private bool isSprinting;
    private readonly Dictionary<int, float> movementSpeedMultipliers = new Dictionary<int, float>();

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        }

        stats = GetComponent<PlayerStats>();
    }

    void Update()
    {
        // If the game is paused, do not process movement or input
        if (Time.timeScale == 0)
        {
            movement = Vector2.zero;
            return;
        }

        // WASD input
        movement.x = Input.GetAxisRaw("Horizontal");
        movement.y = Input.GetAxisRaw("Vertical");

        // Normalize so diagonal speed isn't faster
        movement = movement.normalized;

        // Sprint input (requires energy and movement)
        bool isMoving = movement.sqrMagnitude > 0;
        isSprinting = Input.GetKey(KeyCode.LeftShift) && stats != null && stats.Energy > 0 && isMoving;

        if (isSprinting)
        {
            stats.UseEnergy(stats.sprintEnergyCost * Time.deltaTime);
        }

        // Flip sprite left/right
        if (spriteRenderer != null)
        {
            if (movement.x > 0)
                spriteRenderer.flipX = false;
            else if (movement.x < 0)
                spriteRenderer.flipX = true;
        }
    }

    void FixedUpdate()
    {
        float currentSpeed = (isSprinting ? sprintSpeed : moveSpeed) * GetMovementSpeedMultiplier();
        rb.MovePosition(rb.position + movement * currentSpeed * Time.fixedDeltaTime);
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

    private float GetMovementSpeedMultiplier()
    {
        float lowestMultiplier = 1f;

        foreach (float multiplier in movementSpeedMultipliers.Values)
        {
            lowestMultiplier = Mathf.Min(lowestMultiplier, multiplier);
        }

        return lowestMultiplier;
    }
}
