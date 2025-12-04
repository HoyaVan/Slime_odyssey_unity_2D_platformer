using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class SlimePlayerController : MonoBehaviour
{
    [Header("Move")]
    [SerializeField] float moveSpeed = 3f;
    [SerializeField] float jumpForce = 6f;

    [Header("Drop Through")]
    [SerializeField] float dropDuration = 0.2f;   // 플랫폼 관통 유지 시간

    [Header("Ground Check")]
    [SerializeField] LayerMask groundLayer;   // 바닥 타일
    [SerializeField] LayerMask objectLayer;   // 박스, 이동발판 등 (플랫폼)
    [SerializeField] LayerMask animalLayer;   // 닭 같은 동물들

    [SerializeField] float groundCheckDistance = 0.2f;

    [Header("Coyote Time")]
    [SerializeField] float coyoteTime = 0.15f;
    float coyoteCounter;

    [Header("Fall")]
    [SerializeField] float fallMultiplier = 2f;

    [Header("Hurt")]
    [SerializeField] float hurtDuration = 0.3f;
    [SerializeField] float knockbackForce = 3f;  // 데미지 받을 때 밀려나는 힘
    bool isHurt = false;

    Rigidbody2D rb;
    Animator anim;

    bool grounded = false;
    float inputX = 0f;

    Vector3 baseScale;

    InputSystem_Actions inputActions;

    // 현재 밟고 있는 플랫폼(Objects layer) 콜라이더
    Collider2D currentPlatform;

    void Awake()
    {
        inputActions = new InputSystem_Actions();
    }

    void OnEnable()
    {
        if (inputActions != null)
        {
            inputActions.Enable();
        }
    }

    void OnDisable()
    {
        if (inputActions != null)
        {
            inputActions.Disable();
        }
    }

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        anim = GetComponent<Animator>();
        baseScale = transform.localScale;
    }

    void Update()
    {
        // --- 입력 & 방향 전환 ---
        if (!isHurt && inputActions != null)
        {
            Vector2 moveInput = inputActions.Player.Move.ReadValue<Vector2>();
            inputX = moveInput.x;

            if (inputX != 0)
            {
                float sign = Mathf.Sign(inputX);
                transform.localScale = new Vector3(
                    Mathf.Abs(baseScale.x) * sign,
                    baseScale.y,
                    baseScale.z
                );
            }
        }
        else
        {
            inputX = 0f;
        }

        // --- 땅 체크 & 코요테 타임 ---
        grounded = IsGrounded();

        if (grounded)
        {
            coyoteCounter = coyoteTime;
        }
        else
        {
            coyoteCounter -= Time.deltaTime;
        }

        // --- 점프 입력 ---
        bool jumpPressed = Keyboard.current != null
                           ? Keyboard.current.spaceKey.wasPressedThisFrame
                           : Input.GetKeyDown(KeyCode.Space);

        // ↓ 키 입력 (S 또는 아래 방향키)
        bool downPressed = Keyboard.current != null
            ? (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed)
            : (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow));

        // ↓ + Jump → 오브젝트 레이어(플랫폼) 위에 있을 때만 아래로 떨어지기
        if (!isHurt && jumpPressed && grounded && downPressed && IsOnObjectPlatform())
        {
            StartCoroutine(DropThroughPlatform());
            return; // 이 프레임에서는 일반 점프 막기
        }

        // 일반 점프
        if (!isHurt && jumpPressed && coyoteCounter > 0f)
        {
            Jump();
        }

        // --- 애니메이션 ---
        if (anim != null && !isHurt)
        {
            anim.SetFloat("Speed", Mathf.Abs(inputX));
            anim.SetBool("Grounded", grounded);
            if (rb != null)
            {
                anim.SetFloat("YVelocity", rb.linearVelocity.y);
            }
        }
    }

    void FixedUpdate()
    {
        if (rb == null) return;

        // --- 수평 이동 ---
        if (!isHurt)
        {
            rb.linearVelocity = new Vector2(inputX * moveSpeed, rb.linearVelocity.y);
        }

        // --- 더 빠른 낙하 ---
        if (rb.linearVelocity.y < 0f)
        {
            rb.linearVelocity += Vector2.up *
                Physics2D.gravity.y * (fallMultiplier - 1f) * Time.fixedDeltaTime;
        }
    }

    void Jump()
    {
        if (rb == null) return;

        rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0f);
        rb.AddForce(Vector2.up * jumpForce, ForceMode2D.Impulse);
        coyoteCounter = 0f;

        // 점프 사운드
        if (AudioPlayer.Instance != null)
        {
            AudioPlayer.Instance.PlayJump();
        }
    }

    // ↓ + 점프에서 호출: 현재 밟고 있는 플랫폼(Objects layer)에 대해 충돌 무시
    IEnumerator DropThroughPlatform()
    {
        if (currentPlatform == null)
            yield break;

        Collider2D slimeCollider = GetComponent<Collider2D>();
        if (slimeCollider == null)
            yield break;

        // 이 슬라임과 이 플랫폼 사이의 충돌만 잠시 무시
        Physics2D.IgnoreCollision(slimeCollider, currentPlatform, true);

        yield return new WaitForSeconds(dropDuration);

        Physics2D.IgnoreCollision(slimeCollider, currentPlatform, false);
        currentPlatform = null;
    }

    bool IsGrounded()
    {
        Collider2D col = GetComponent<Collider2D>();
        if (col == null) return false;

        float extraDistance = groundCheckDistance;
        // Ground + Object + Animal 레이어 모두 포함
        int combinedMask = groundLayer | objectLayer | animalLayer;

        // 콜라이더의 바닥에서 여러 점을 체크 (중앙, 왼쪽, 오른쪽)
        float halfWidth = col.bounds.size.x * 0.4f; // 콜라이더 너비의 40%
        Vector2 centerOrigin = new Vector2(col.bounds.center.x, col.bounds.min.y);
        Vector2 leftOrigin = new Vector2(col.bounds.center.x - halfWidth, col.bounds.min.y);
        Vector2 rightOrigin = new Vector2(col.bounds.center.x + halfWidth, col.bounds.min.y);

        // 여러 각도로 레이캐스트 (아래, 대각선 아래 왼쪽, 대각선 아래 오른쪽)
        Vector2[] directions = new Vector2[]
        {
            Vector2.down,                                    // 정확히 아래
            (Vector2.down + Vector2.left).normalized,        // 대각선 아래 왼쪽
            (Vector2.down + Vector2.right).normalized       // 대각선 아래 오른쪽
        };

        Vector2[] origins = new Vector2[] { centerOrigin, leftOrigin, rightOrigin };

        // 모든 조합으로 레이캐스트 수행
        foreach (Vector2 origin in origins)
        {
            foreach (Vector2 direction in directions)
            {
                RaycastHit2D hit = Physics2D.Raycast(
                    origin,
                    direction,
                    extraDistance * 1.5f, // 기울어진 플랫폼을 위해 거리 약간 증가
                    combinedMask
                );

                // Scene 뷰에서 빨간 선으로 보이게 (디버그용)
                Debug.DrawLine(origin, origin + direction * extraDistance * 1.5f, Color.red);

                if (hit.collider != null)
                {
                    return true;
                }
            }
        }

        return false;
    }

    // 현재 오브젝트 레이어(플랫폼) 위에 있는지 체크 + 그 플랫폼 콜라이더 기억
    bool IsOnObjectPlatform()
    {
        Collider2D col = GetComponent<Collider2D>();
        if (col == null) return false;

        Vector2 origin = new Vector2(col.bounds.center.x, col.bounds.min.y);
        float extraDistance = groundCheckDistance;

        // 오브젝트 레이어만 체크
        RaycastHit2D hit = Physics2D.Raycast(
            origin,
            Vector2.down,
            extraDistance,
            objectLayer
        );

        if (hit.collider != null)
        {
            currentPlatform = hit.collider;  // 지금 밟고 있는 플랫폼 저장
            return true;
        }

        currentPlatform = null;
        return false;
    }

    // ------------ Hurt ------------ //

    public void TakeDamage(Vector2 knockbackDirection = default)
    {
        if (isHurt) return;

        GameManager.Instance.ReduceLife(1);

        // 플레이어(slime) 데미지 사운드
        if (AudioPlayer.Instance != null)
        {
            AudioPlayer.Instance.PlaySlimeHurt();
        }

        StartCoroutine(HurtRoutine(knockbackDirection));
    }

    IEnumerator HurtRoutine(Vector2 knockbackDirection)
    {
        isHurt = true;

        // 애니메이션 트리거 먼저 설정 (다른 파라미터 설정 전에)
        if (anim != null)
        {
            anim.SetTrigger("Hurt");
        }

        // 넉백 적용
        if (rb != null)
        {
            // 넉백 방향이 제공된 경우에만 적용
            if (knockbackDirection != default && knockbackDirection.magnitude > 0.1f)
            {
                knockbackDirection.y = 0f;  // 수평 방향만
                knockbackDirection = knockbackDirection.normalized;
                rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);  // 기존 수평 속도 초기화
                rb.AddForce(knockbackDirection * knockbackForce, ForceMode2D.Impulse);
            }
            else
            {
                // 넉백 방향이 없으면 수평 속도만 정지
                rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            }
        }

        yield return new WaitForSeconds(hurtDuration);

        isHurt = false;
    }
}
