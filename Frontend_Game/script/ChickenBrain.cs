using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(SpriteRenderer))]
public class ChickenBrain : MonoBehaviour
{
    [Header("Speeds")]
    [SerializeField] float wanderSpeed = 1f;
    [SerializeField] float runSpeed = 4f;

    [Header("Wander Timers (seconds)")]
    [SerializeField] float minWalkTime = 1f;
    [SerializeField] float maxWalkTime = 3f;
    [SerializeField] float minIdleTime = 0.5f;
    [SerializeField] float maxIdleTime = 2f;

    [Header("Jump Settings")]
    [SerializeField] float jumpForce = 5f;
    [SerializeField] float minJumpDelay = 1.5f;
    [SerializeField] float maxJumpDelay = 3.5f;

    [Header("Ground Check")]
    [SerializeField] Transform groundCheck;      // child at feet
    [SerializeField] float groundCheckRadius = 0.2f;
    [SerializeField] LayerMask groundLayer;      // e.g. Ground
    [SerializeField] LayerMask objectLayer;      // e.g. Object / Platform

    [Header("Panic")]
    [SerializeField] float panicDuration = 1.5f;

    Rigidbody2D rb;
    SpriteRenderer sr;

    enum State { Idle, Wander, Panic }
    State state;

    float stateTimer;
    int direction;          // -1, 0, 1
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

        ScheduleNextJump();
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
            if (state == State.Idle) SetWander();
            else if (state == State.Wander || state == State.Panic)
                SetIdle();
        }

        bool isGrounded = IsGrounded();

        if (isGrounded && Time.time >= nextJumpTime)
        {
            Jump();
        }
    }

    void FixedUpdate()
    {
        float speed =
            (state == State.Panic) ? runSpeed :
            (state == State.Wander) ? wanderSpeed :
            0f;

        rb.linearVelocity = new Vector2(direction * speed, rb.linearVelocity.y);
        UpdateFacing();
    }

    // ---------- STATE ----------

    void SetIdle()
    {
        state = State.Idle;
        direction = 0;
        stateTimer = Random.Range(minIdleTime, maxIdleTime);
    }

    void SetWander()
    {
        state = State.Wander;
        direction = Random.Range(0, 2) == 0 ? -1 : 1;
        stateTimer = Random.Range(minWalkTime, maxWalkTime);
        UpdateFacing();
    }

    public void SetPanic(Vector3 dangerPosition)
    {
        state = State.Panic;
        stateTimer = panicDuration;
        direction = (transform.position.x >= dangerPosition.x) ? 1 : -1;
        UpdateFacing();
    }

    // ---------- HELPERS ----------

    bool IsGrounded()
    {
        if (groundCheck == null) return false;

        // combine layers: ground OR objects
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

    void Jump()
    {
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0f);
        rb.AddForce(Vector2.up * jumpForce, ForceMode2D.Impulse);
        ScheduleNextJump();
    }

    void ScheduleNextJump()
    {
        nextJumpTime = Time.time + Random.Range(minJumpDelay, maxJumpDelay);
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.collider.CompareTag("Player"))
        {
            SetPanic(collision.transform.position);
        }
    }

    void OnDrawGizmosSelected()
    {
        if (groundCheck == null) return;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
    }
}
