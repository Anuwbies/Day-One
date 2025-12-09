using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    public float moveSpeed = 5f;

    private Rigidbody2D rb;
    public Vector2 movement;
    private Animator anim;
    private SpriteRenderer sr;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        anim = GetComponent<Animator>();
        sr = GetComponent<SpriteRenderer>();
    }

    void Update()
    {
        // WASD input
        movement.x = Input.GetAxisRaw("Horizontal");
        movement.y = Input.GetAxisRaw("Vertical");

        // Normalize so diagonal speed isn't faster
        movement = movement.normalized;

        // Flip sprite left/right
        if (movement.x > 0)
            sr.flipX = false;
        else if (movement.x < 0)
            sr.flipX = true;

        // Animation trigger using bool
        bool isMoving = movement.sqrMagnitude > 0;
        anim.SetBool("isRunning", isMoving);
    }

    void FixedUpdate()
    {
        rb.MovePosition(rb.position + movement * moveSpeed * Time.fixedDeltaTime);
    }
}
