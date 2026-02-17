using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    public float moveSpeed = 5f;
    public float sprintSpeed = 8f;

    private Rigidbody2D rb;
    public Vector2 movement;
    private Animator anim;
    private SpriteRenderer sr;
    private PlayerStats stats;
    private bool isSprinting;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        anim = GetComponent<Animator>();
        sr = GetComponent<SpriteRenderer>();
        stats = GetComponent<PlayerStats>();
    }

    void Update()
    {
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
        if (movement.x > 0)
            sr.flipX = false;
        else if (movement.x < 0)
            sr.flipX = true;

        // Animation trigger using bool
        anim.SetBool("isRunning", isMoving);
    }

    void FixedUpdate()
    {
        float currentSpeed = isSprinting ? sprintSpeed : moveSpeed;
        rb.MovePosition(rb.position + movement * currentSpeed * Time.fixedDeltaTime);
    }
}
