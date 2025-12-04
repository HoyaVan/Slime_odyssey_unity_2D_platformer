using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(SpriteRenderer))]
public class GooseBrain : MonoBehaviour
{
    [Header("Speeds")]
    [SerializeField] float waddleSpeed = 1f;     // wander speed
    [SerializeField] float sprintSpeed = 4f;     // panic speed

    [Header("Waddle Timers (seconds)")]
    [SerializeField] float minWalkTime = 1f;
    [SerializeField] float maxWalkTime = 3f;
    [SerializeField] float minIdleTime = 0.5f;
    [SerializeField] float maxIdleTime = 2f;

    [Header("Honking Jump Settings")]
    [SerializeField] float jumpForce = 5f;
    [SerializeField] float minHonkJumpDelay = 1.5f;
    [SerializeField] float maxHonkJumpDelay = 3.5f;

    [Header("Ground Check")]
    [SerializeField] Transform groundCheck;
    [SerializeField] float groundCheckRadius = 0.2f;
    [SerializeField] LayerMask groundLayer;
    [SerializeField] LayerMask objectLayer;

    [Header("Panic Mode")]
    [SerializeField] float panicDuration = 1.5f;

    Rigidbody2D rb;
    SpriteRenderer sr;

    enum GooseState { Idle, Waddle, Panic }
    GooseState state;

    float stateTimer;
    int direction;     // -1, 0, 1
    float nextJumpTime;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.freezeRotation = true;

        sr = GetComponent<SpriteRenderer>();

        if (groundCheck == null)
        {
            Transform found = transform.Find("GroundCheck");
            if (found != null) groundCheck = found;
        }

        ScheduleNextHonkJump();
    }

    void Start()
    {
        SetIdle();
    }

    void Update()
    {
        stateTimer -= Time.deltaTime;

        if (stateTimer <= 0f)
        {
            if (state == GooseState.Idle) SetWaddle();
            else if (state == GooseState.Waddle || state == GooseState.Panic)
                SetIdle();
        }

        bool grounded = IsGrounded();

        if (grounded && Time.time >= nextJumpTime)
        {
            HonkJump();
        }
    }

    void FixedUpdate()
    {
        float speed =
            (state == GooseState.Panic) ? sprintSpeed :
            (state == GooseState.Waddle) ? waddleSpeed :
            0f;

        rb.linearVelocity = new Vector2(direction * speed, rb.linearVelocity.y);
        UpdateFacing();
    }

    // ----------------- STATE SYSTEM -----------------

    void SetIdle()
    {
        state = GooseState.Idle;
        direction = 0;
        stateTimer = Random.Range(minIdleTime, maxIdleTime);
    }

    void SetWaddle()
    {
        state = GooseState.Waddle;
        direction = Random.Range(0, 2) == 0 ? -1 : 1;
        stateTimer = Random.Range(minWalkTime, maxWalkTime);
        UpdateFacing();
    }

    public void TriggerPanic(Vector3 dangerPosition)
    {
        state = GooseState.Panic;
        stateTimer = panicDuration;
        direction = (transform.position.x >= dangerPosition.x) ? 1 : -1;
        UpdateFacing();
    }

    // ----------------- HELPERS -----------------

    bool IsGrounded()
    {
        if (groundCheck == null) return false;

        int mask = groundLayer | objectLayer;

        bool hit = Physics2D.OverlapCircle(
            groundCheck.position,
            groundCheckRadius,
            mask
        );

        return hit;
    }

    void UpdateFacing()
    {
        if (direction == 0) return;
        // base sprite faces RIGHT
        sr.flipX = (direction < 0);
    }

    void HonkJump()
    {
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0f);
        rb.AddForce(Vector2.up * jumpForce, ForceMode2D.Impulse);
        ScheduleNextHonkJump();
    }

    void ScheduleNextHonkJump()
    {
        nextJumpTime = Time.time + Random.Range(minHonkJumpDelay, maxHonkJumpDelay);
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.collider.CompareTag("Player"))
        {
            TriggerPanic(collision.transform.position);
        }
    }

    void OnDrawGizmosSelected()
    {
        if (groundCheck == null) return;
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
    }
}
