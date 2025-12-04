using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
public class EnemyPlayerController : MonoBehaviour
{
    // ===== Movement =====
    [Header("Movement")]
    [SerializeField] float moveSpeed = 3f;
    [SerializeField] float jumpForce = 6f;
    [SerializeField] float contactSpeed = 1f;   // 슬라임과 몸이 닿아 있을 때 속도

    // ===== Pathfinding / Tracking =====
    [Header("Pathfinding")]
    [SerializeField] bool useBFS = true;
    [SerializeField] float pathfindingUpdateInterval = 0.5f;
    [SerializeField] float maxDetectionRange = 20f;
    [SerializeField] float directChaseDistance = 3f;
    [SerializeField] float maxVerticalDistanceForDirectChase = 2f;
    [SerializeField] float directionChangeThreshold = 0.8f;
    [SerializeField] float stopDistance = 0.5f;
    [SerializeField] float directionStickTime = 0.2f;
    [SerializeField] float objectDetectionRadius = 10f;
    [SerializeField] int maxWaypoints = 5;

    [Header("Tracking")]
    [SerializeField][Range(0f, 1f)] float loseTrackProbability = 0.1f;
    [SerializeField] float trackCheckInterval = 1f;
    [SerializeField] float lostTrackDuration = 3f;

    // ===== Ground / Jump =====
    [Header("Ground Check")]
    [SerializeField] LayerMask groundLayer;
    [SerializeField] LayerMask objectLayer;
    [SerializeField] float groundCheckDistance = 0.2f;

    [Header("Coyote Time")]
    [SerializeField] float coyoteTime = 0.15f;
    float coyoteCounter;

    [Header("Fall")]
    [SerializeField] float fallMultiplier = 2f;

    [Header("Jump Decision")]
    [SerializeField] float jumpCheckDistance = 1f;
    [SerializeField] float jumpCheckHeight = 2f;
    [SerializeField] float maxJumpableHeight = 3f;
    [SerializeField] float jumpCooldown = 0.5f;

    [Header("Slime Collision")]
    [SerializeField] float stunDuration = 0.3f;     // 적이 멈춰있을 시간
    [SerializeField] float damageCooldown = 0.5f;   // 연속으로 데미지 안 주게 쿨다운

    bool isStunned = false;     // 적이 프리즈 상태인지
    float stunEndTime = 0f;     // 프리즈가 끝나는 시간
    float lastDamageTime = -10f; // 마지막으로 슬라임을 때린 시간


    // ===== Components =====
    Rigidbody2D rb;
    Animator anim;
    SpriteRenderer sr;

    // ===== State =====
    SlimePlayerController targetSlime;

    bool isTracking = false;
    bool isLost = false;
    float lostTrackTimer = 0f;
    float lastPathfindingUpdate = 0f;
    float lastTrackCheck = 0f;

    Vector3 baseScale;
    List<Vector3> currentPath;
    int currentPathIndex = 0;

    bool grounded = false;
    float inputX = 0f;
    Collider2D currentPlatform;

    bool isInContactWithSlime = false;  // 몸통끼리 접촉 여부
    float currentMoveSpeed;

    float lastInputX = 0f;
    float directionStickTimer = 0f;

    bool hasJustJumped = false;
    bool wasInAirAfterJump = false;
    float lastJumpTime = -10f;
    int consecutiveGroundedFrames = 0;
    bool jumpLocked = false;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.freezeRotation = true;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.gravityScale = 1f;
        }

        // 본체 콜라이더는 항상 충돌용 (IsTrigger = false)
        Collider2D col = GetComponent<Collider2D>();
        if (col == null)
        {
            Debug.LogWarning("EnemyPlayerController: Collider2D가 없습니다! BoxCollider2D를 추가하세요.");
        }
        else if (col.isTrigger)
        {
            Debug.LogWarning("EnemyPlayerController: 본체 Collider2D는 IsTrigger 꺼야 합니다. 자동으로 끕니다.");
            col.isTrigger = false;
        }
    }

    void Start()
    {
        if (rb == null) rb = GetComponent<Rigidbody2D>();
        anim = GetComponent<Animator>();
        sr = GetComponent<SpriteRenderer>();
        baseScale = transform.localScale;

        // 슬라임 타겟 찾기
        FindSlimeTarget();
        currentMoveSpeed = moveSpeed;
    }

    void Update()
    {
        // ----- Ground / Coyote -----
        grounded = IsGrounded();

        if (grounded)
        {
            coyoteCounter = coyoteTime;
            consecutiveGroundedFrames++;
            if (consecutiveGroundedFrames > 10) consecutiveGroundedFrames = 10;
        }
        else
        {
            coyoteCounter -= Time.deltaTime;
            consecutiveGroundedFrames = 0;
        }

        // ----- Jump Lock 해제 로직 -----
        if (hasJustJumped || jumpLocked)
        {
            if (rb != null && rb.linearVelocity.y > 0.1f)
            {
                wasInAirAfterJump = true;
                consecutiveGroundedFrames = 0;
                jumpLocked = true;
            }
            else if (grounded && rb != null && rb.linearVelocity.y <= 0.05f)
            {
                if (wasInAirAfterJump)
                {
                    consecutiveGroundedFrames++;
                    if (consecutiveGroundedFrames >= 3 &&
                        (Time.time - lastJumpTime) >= jumpCooldown)
                    {
                        hasJustJumped = false;
                        wasInAirAfterJump = false;
                        jumpLocked = false;
                        consecutiveGroundedFrames = 0;
                    }
                }
                else
                {
                    if ((Time.time - lastJumpTime) >= jumpCooldown)
                    {
                        hasJustJumped = false;
                        jumpLocked = false;
                    }
                }
            }
            else
            {
                consecutiveGroundedFrames = 0;
            }
        }

        // ----- 프리즈 해제 타이밍 -----
        if (isStunned && Time.time >= stunEndTime)
        {
            isStunned = false;
        }

        // ----- 추적 잃기 체크 -----
        if (Time.time - lastTrackCheck >= trackCheckInterval)
        {
            lastTrackCheck = Time.time;
            CheckLoseTrack();
        }

        // ----- 경로 갱신 -----
        if (isTracking && !isLost && targetSlime != null)
        {
            float distanceToSlime = Vector3.Distance(transform.position, targetSlime.transform.position);

            if (distanceToSlime > directChaseDistance)
            {
                bool needsUpdate = currentPath == null || currentPath.Count == 0;
                bool pathExpired = (Time.time - lastPathfindingUpdate) >= pathfindingUpdateInterval * 2f;
                bool reachedEnd = currentPath != null && currentPathIndex >= currentPath.Count;

                if ((needsUpdate || pathExpired || reachedEnd) &&
                    Time.time - lastPathfindingUpdate >= pathfindingUpdateInterval)
                {
                    lastPathfindingUpdate = Time.time;
                    UpdatePathfinding();
                }
            }
        }
        else if (isLost)
        {
            lostTrackTimer -= Time.deltaTime;
            if (lostTrackTimer <= 0f)
            {
                isLost = false;
                FindSlimeTarget();
            }
        }

        // ----- 방향 유지 타이머 -----
        if (directionStickTimer > 0f)
        {
            directionStickTimer -= Time.deltaTime;
        }

        // ----- 이동 방향 결정 -----
        if (isTracking && !isLost && targetSlime != null)
        {
            Vector3 toSlime = targetSlime.transform.position - transform.position;
            float distanceToSlime = toSlime.magnitude;
            float verticalDistance = Mathf.Abs(toSlime.y);
            float horizontalDistance = Mathf.Abs(toSlime.x);

            bool isAboveSlime = transform.position.y > targetSlime.transform.position.y;

            if (distanceToSlime <= directChaseDistance &&
                verticalDistance <= maxVerticalDistanceForDirectChase)
            {
                DirectChase();
            }
            else if (isAboveSlime &&
                     verticalDistance > maxVerticalDistanceForDirectChase &&
                     horizontalDistance > stopDistance)
            {
                float desiredInputX = Mathf.Sign(toSlime.x);

                if (directionStickTimer > 0f && lastInputX != 0f)
                {
                    inputX = lastInputX;
                }
                else if (lastInputX != 0f)
                {
                    float directionChange = Mathf.Abs(desiredInputX - lastInputX);
                    if (directionChange < directionChangeThreshold)
                    {
                        inputX = lastInputX;
                    }
                    else
                    {
                        inputX = desiredInputX;
                        lastInputX = inputX;
                        directionStickTimer = directionStickTime;
                    }
                }
                else
                {
                    inputX = desiredInputX;
                    lastInputX = inputX;
                    directionStickTimer = directionStickTime;
                }
            }
            else if (currentPath != null && currentPath.Count > 0)
            {
                MoveAlongPath();
            }
            else
            {
                inputX = 0f;
            }
        }
        else
        {
            inputX = 0f;
        }

        // ----- 스프라이트 방향 -----
        if (inputX != 0)
        {
            float sign = Mathf.Sign(inputX);
            transform.localScale = new Vector3(
                Mathf.Abs(baseScale.x) * sign,
                baseScale.y,
                baseScale.z
            );
        }

        // ----- 애니메이션 -----
        if (anim != null)
        {
            anim.SetFloat("Speed", Mathf.Abs(inputX));
            anim.SetBool("Grounded", grounded);
        }
    }

    void FixedUpdate()
    {
        if (rb == null) return;

        // 프리즈 상태이면 가로 이동 멈추기 (중력은 그대로)
        if (isStunned)
        {
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
        }
        else
        {
            // 슬라임과 몸이 닿아 있으면 느리게 이동
            currentMoveSpeed = isInContactWithSlime ? contactSpeed : moveSpeed;
            rb.linearVelocity = new Vector2(inputX * currentMoveSpeed, rb.linearVelocity.y);
        }

        // 더 빠른 낙하
        if (rb.linearVelocity.y < 0f)
        {
            rb.linearVelocity += Vector2.up *
                Physics2D.gravity.y * (fallMultiplier - 1f) * Time.fixedDeltaTime;
        }
    }

    // ===================== Target / Tracking =====================

    void FindSlimeTarget()
    {
        targetSlime = FindFirstObjectByType<SlimePlayerController>();

        if (targetSlime != null)
        {
            isTracking = true;
            isLost = false;
            UpdatePathfinding();
        }
        else
        {
            isTracking = false;
            isLost = false;
            targetSlime = null;
        }
    }

    void CheckLoseTrack()
    {
        if (!isTracking || isLost || targetSlime == null) return;

        float distance = Vector3.Distance(transform.position, targetSlime.transform.position);
        if (distance > maxDetectionRange)
        {
            LoseTrack();
            return;
        }

        if (Random.Range(0f, 1f) < loseTrackProbability)
        {
            LoseTrack();
        }
    }

    void LoseTrack()
    {
        isLost = true;
        lostTrackTimer = lostTrackDuration;
        currentPath = null;
        inputX = 0f;
    }

    // ===================== Pathfinding =====================

    void UpdatePathfinding()
    {
        if (targetSlime == null)
        {
            FindSlimeTarget();
            return;
        }

        Vector3 startPos = transform.position;
        Vector3 targetPos = targetSlime.transform.position;

        float distance = Vector3.Distance(startPos, targetPos);
        if (distance > maxDetectionRange)
        {
            LoseTrack();
            return;
        }

        if (useBFS)
            currentPath = FindPathBFS(startPos, targetPos);
        else
            currentPath = FindPathDFS(startPos, targetPos);

        if (currentPath != null && currentPath.Count > 0)
        {
            float minDistance = float.MaxValue;
            int closestIndex = 0;

            for (int i = 0; i < currentPath.Count; i++)
            {
                float dist = Vector3.Distance(startPos, currentPath[i]);
                if (dist < minDistance)
                {
                    minDistance = dist;
                    closestIndex = i;
                }
            }

            if (minDistance < 0.5f && closestIndex < currentPath.Count - 1)
            {
                closestIndex++;
            }

            currentPathIndex = closestIndex;
        }
        else
        {
            currentPathIndex = 0;
        }
    }

    List<Vector3> FindPathBFS(Vector3 start, Vector3 target)
    {
        List<Vector3> path = new List<Vector3>();
        path.Add(start);

        bool enemyOnGround = IsOnGroundLayer(start);
        bool enemyOnPlatform = IsEnemyOnPlatform(start);
        bool slimeOnPlatform = IsSlimeOnPlatform(target);
        bool slimeOnGround = IsSlimeOnGround(target);

        if (enemyOnGround && slimeOnPlatform)
        {
            Vector3? closestPlatform = FindClosestAccessiblePlatform(start, target);
            if (closestPlatform.HasValue)
                path.Add(closestPlatform.Value);
        }
        else if (enemyOnPlatform && slimeOnPlatform)
        {
            Vector3? intermediatePlatform = FindIntermediatePlatform(start, target);
            if (intermediatePlatform.HasValue)
                path.Add(intermediatePlatform.Value);
        }
        else
        {
            List<Vector3> objectWaypoints = FindNearbyObjects(start, target);
            foreach (Vector3 waypoint in objectWaypoints)
                path.Add(waypoint);
        }

        path.Add(target);
        return OptimizePath(path);
    }

    List<Vector3> FindPathDFS(Vector3 start, Vector3 target)
    {
        return FindPathBFS(start, target);
    }

    List<Vector3> FindNearbyObjects(Vector3 start, Vector3 target)
    {
        List<Vector3> waypoints = new List<Vector3>();

        Vector3 center = (start + target) / 2f;
        float searchRadius = Vector3.Distance(start, target) / 2f + objectDetectionRadius;

        Collider2D[] colliders = Physics2D.OverlapCircleAll(center, searchRadius, objectLayer);

        List<Vector3> candidateWaypoints = new List<Vector3>();

        foreach (Collider2D col in colliders)
        {
            Vector3 waypoint = new Vector3(
                col.bounds.center.x,
                col.bounds.max.y + 0.5f,
                0f
            );

            Vector3 toStart = waypoint - start;
            Vector3 toTarget = target - waypoint;
            Vector3 directPath = target - start;

            float directDistance = directPath.magnitude;
            float viaObjectDistance = toStart.magnitude + toTarget.magnitude;

            if (viaObjectDistance <= directDistance * 1.5f)
            {
                candidateWaypoints.Add(waypoint);
            }
        }

        candidateWaypoints.Sort((a, b) =>
        {
            float distA = Vector3.Distance(start, a) + Vector3.Distance(a, target);
            float distB = Vector3.Distance(start, b) + Vector3.Distance(b, target);
            return distA.CompareTo(distB);
        });

        int count = Mathf.Min(candidateWaypoints.Count, maxWaypoints);
        for (int i = 0; i < count; i++)
        {
            waypoints.Add(candidateWaypoints[i]);
        }

        return waypoints;
    }

    bool IsOnGroundLayer(Vector3 position)
    {
        Collider2D col = GetComponent<Collider2D>();
        if (col == null) return false;

        Vector2 origin = new Vector2(position.x, col.bounds.min.y);
        float checkDistance = 0.5f;

        RaycastHit2D hit = Physics2D.Raycast(origin, Vector2.down, checkDistance, groundLayer);
        return hit.collider != null;
    }

    bool IsEnemyOnPlatform(Vector3 position)
    {
        Collider2D col = GetComponent<Collider2D>();
        if (col == null) return false;

        Vector2 origin = new Vector2(position.x, col.bounds.min.y);
        float checkDistance = 0.5f;

        RaycastHit2D hit = Physics2D.Raycast(origin, Vector2.down, checkDistance, objectLayer);
        return hit.collider != null;
    }

    bool IsSlimeOnGround(Vector3 slimePosition)
    {
        if (targetSlime == null) return false;

        Collider2D slimeCol = targetSlime.GetComponent<Collider2D>();
        if (slimeCol == null) return false;

        Vector2 origin = new Vector2(slimePosition.x, slimeCol.bounds.min.y);
        float checkDistance = 0.5f;

        RaycastHit2D hit = Physics2D.Raycast(origin, Vector2.down, checkDistance, groundLayer);
        return hit.collider != null;
    }

    bool IsSlimeOnPlatform(Vector3 slimePosition)
    {
        if (targetSlime == null) return false;

        Collider2D slimeCol = targetSlime.GetComponent<Collider2D>();
        if (slimeCol == null) return false;

        Vector2 origin = new Vector2(slimePosition.x, slimeCol.bounds.min.y);
        float checkDistance = 0.5f;

        RaycastHit2D hit = Physics2D.Raycast(origin, Vector2.down, checkDistance, objectLayer);
        return hit.collider != null;
    }

    Vector3? FindClosestAccessiblePlatform(Vector3 start, Vector3 target)
    {
        Vector3 center = (start + target) / 2f;
        float searchRadius = Vector3.Distance(start, target) * 0.75f;

        Collider2D[] colliders = Physics2D.OverlapCircleAll(center, searchRadius, objectLayer);

        Vector3? bestPlatform = null;
        float bestScore = float.MaxValue;

        foreach (Collider2D col in colliders)
        {
            Vector3 platformPos = new Vector3(
                col.bounds.center.x,
                col.bounds.max.y + 0.5f,
                0f
            );

            Vector3 toPlatform = platformPos - start;
            Vector3 toTarget = target - start;
            float dotProduct = Vector3.Dot(toPlatform.normalized, toTarget.normalized);

            if (dotProduct > 0.3f)
            {
                float distanceToPlatform = Vector3.Distance(start, platformPos);
                float distanceFromPlatformToTarget = Vector3.Distance(platformPos, target);
                float heightDiff = platformPos.y - start.y;

                if (heightDiff <= maxJumpableHeight && heightDiff >= -0.5f)
                {
                    float score = distanceToPlatform + distanceFromPlatformToTarget * 0.5f;

                    if (score < bestScore)
                    {
                        bestScore = score;
                        bestPlatform = platformPos;
                    }
                }
            }
        }

        return bestPlatform;
    }

    Vector3? FindIntermediatePlatform(Vector3 start, Vector3 target)
    {
        Vector3 center = (start + target) / 2f;
        float searchRadius = Vector3.Distance(start, target) * 0.6f;

        Collider2D[] colliders = Physics2D.OverlapCircleAll(center, searchRadius, objectLayer);

        Vector3? bestPlatform = null;
        float bestScore = float.MaxValue;

        foreach (Collider2D col in colliders)
        {
            Vector3 platformPos = new Vector3(
                col.bounds.center.x,
                col.bounds.max.y + 0.5f,
                0f
            );

            Vector3 toPlatform = platformPos - start;
            Vector3 toTarget = target - start;
            float dotProduct = Vector3.Dot(toPlatform.normalized, toTarget.normalized);

            if (dotProduct > 0.5f && dotProduct < 0.95f)
            {
                float distanceToPlatform = Vector3.Distance(start, platformPos);
                float distanceFromPlatformToTarget = Vector3.Distance(platformPos, target);

                float heightDiffFromStart = Mathf.Abs(platformPos.y - start.y);
                float heightDiffToTarget = Mathf.Abs(platformPos.y - target.y);

                if (heightDiffFromStart <= maxJumpableHeight &&
                    heightDiffToTarget <= maxJumpableHeight)
                {
                    float totalDistance = distanceToPlatform + distanceFromPlatformToTarget;
                    float directDistance = Vector3.Distance(start, target);

                    if (totalDistance <= directDistance * 1.3f)
                    {
                        float score = totalDistance;

                        if (score < bestScore)
                        {
                            bestScore = score;
                            bestPlatform = platformPos;
                        }
                    }
                }
            }
        }

        return bestPlatform;
    }

    List<Vector3> OptimizePath(List<Vector3> path)
    {
        if (path.Count <= 2) return path;

        List<Vector3> optimized = new List<Vector3>();
        optimized.Add(path[0]);

        for (int i = 1; i < path.Count - 1; i++)
        {
            float distance = Vector3.Distance(optimized[optimized.Count - 1], path[i]);
            if (distance >= 1f)
            {
                optimized.Add(path[i]);
            }
        }

        optimized.Add(path[path.Count - 1]);
        return optimized;
    }

    void MoveAlongPath()
    {
        if (currentPath == null || currentPath.Count == 0) return;

        if (currentPathIndex >= currentPath.Count)
            currentPathIndex = currentPath.Count - 1;

        Vector3 targetWaypoint = currentPath[currentPathIndex];
        Vector3 direction = (targetWaypoint - transform.position);

        if (direction.magnitude < 1.5f)
        {
            currentPathIndex++;
            if (currentPathIndex >= currentPath.Count)
            {
                float distanceToTarget = Vector3.Distance(
                    transform.position,
                    targetSlime != null ? targetSlime.transform.position : transform.position
                );

                if (distanceToTarget > 1f &&
                    Time.time - lastPathfindingUpdate >= pathfindingUpdateInterval)
                {
                    UpdatePathfinding();
                }
                return;
            }

            targetWaypoint = currentPath[currentPathIndex];
            direction = (targetWaypoint - transform.position);
        }

        float desiredInputX = Mathf.Sign(direction.x);

        if (directionStickTimer > 0f && lastInputX != 0f)
        {
            inputX = lastInputX;
        }
        else if (lastInputX != 0f)
        {
            float directionChange = Mathf.Abs(desiredInputX - lastInputX);
            if (directionChange < directionChangeThreshold)
            {
                inputX = lastInputX;
            }
            else
            {
                inputX = desiredInputX;
                lastInputX = inputX;
                directionStickTimer = directionStickTime;
            }
        }
        else
        {
            inputX = desiredInputX;
            lastInputX = inputX;
            directionStickTimer = directionStickTime;
        }

        bool canJump =
            !jumpLocked &&
            grounded &&
            !hasJustJumped &&
            rb != null &&
            rb.linearVelocity.y <= 0.1f &&
            (Time.time - lastJumpTime) >= jumpCooldown &&
            !wasInAirAfterJump;

        if (canJump && ShouldJump(direction))
        {
            Jump();
        }
    }

    void DirectChase()
    {
        if (targetSlime == null) return;

        Vector3 direction = (targetSlime.transform.position - transform.position);
        float horizontalDistance = Mathf.Abs(direction.x);

        if (horizontalDistance <= stopDistance)
        {
            inputX = 0f;
            return;
        }

        float desiredInputX = Mathf.Sign(direction.x);

        if (directionStickTimer > 0f && lastInputX != 0f)
        {
            inputX = lastInputX;
        }
        else if (lastInputX != 0f)
        {
            float directionChange = Mathf.Abs(desiredInputX - lastInputX);
            if (directionChange < directionChangeThreshold)
            {
                inputX = lastInputX;
            }
            else
            {
                inputX = desiredInputX;
                lastInputX = inputX;
                directionStickTimer = directionStickTime;
            }
        }
        else
        {
            inputX = desiredInputX;
            lastInputX = inputX;
            directionStickTimer = directionStickTime;
        }

        bool canJump =
            !jumpLocked &&
            grounded &&
            !hasJustJumped &&
            rb != null &&
            rb.linearVelocity.y <= 0.1f &&
            (Time.time - lastJumpTime) >= jumpCooldown &&
            !wasInAirAfterJump;

        if (canJump && ShouldJump(direction))
        {
            Jump();
        }
    }

    bool ShouldJump(Vector3 direction)
    {
        Collider2D col = GetComponent<Collider2D>();
        if (col == null) return false;

        Vector2 origin = new Vector2(col.bounds.center.x, col.bounds.min.y);
        Vector2 checkDirection = new Vector2(Mathf.Sign(direction.x), 0f);

        RaycastHit2D hit = Physics2D.Raycast(
            origin,
            checkDirection,
            jumpCheckDistance,
            groundLayer | objectLayer
        );

        if (hit.collider != null)
        {
            float obstacleHeight = hit.point.y - origin.y;

            if (obstacleHeight > maxJumpableHeight)
                return false;

            Vector2 topCheckOrigin = new Vector2(hit.point.x, hit.point.y + jumpCheckHeight);
            RaycastHit2D topHit = Physics2D.Raycast(
                topCheckOrigin,
                Vector2.up,
                0.5f,
                groundLayer | objectLayer
            );

            return topHit.collider == null;
        }

        if (direction.y > 0.5f &&
            direction.y <= maxJumpableHeight &&
            grounded &&
            Mathf.Abs(direction.x) < 3f)
        {
            return true;
        }

        return false;
    }

    void Jump()
    {
        if (rb == null) return;

        if (jumpLocked || !grounded || hasJustJumped ||
            rb.linearVelocity.y > 0.1f || wasInAirAfterJump)
        {
            return;
        }

        if ((Time.time - lastJumpTime) < jumpCooldown)
            return;

        jumpLocked = true;
        hasJustJumped = true;
        wasInAirAfterJump = false;
        consecutiveGroundedFrames = 0;
        lastJumpTime = Time.time;

        rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0f);
        rb.AddForce(Vector2.up * jumpForce, ForceMode2D.Impulse);
        coyoteCounter = 0f;

        if (anim != null)
        {
            anim.SetTrigger("Jump");
        }
    }

    // ===================== Ground Check =====================

    bool IsGrounded()
    {
        Collider2D col = GetComponent<Collider2D>();
        if (col == null) return false;

        float checkOffset = 0.05f;
        float checkDistance = groundCheckDistance + checkOffset;
        int combinedMask = groundLayer | objectLayer;

        float halfWidth = col.bounds.size.x * 0.4f;
        Vector2 centerOrigin = new Vector2(col.bounds.center.x, col.bounds.min.y + checkOffset);
        Vector2 leftOrigin = new Vector2(col.bounds.center.x - halfWidth, col.bounds.min.y + checkOffset);
        Vector2 rightOrigin = new Vector2(col.bounds.center.x + halfWidth, col.bounds.min.y + checkOffset);

        Vector2[] directions = new Vector2[]
        {
            Vector2.down,
            (Vector2.down + Vector2.left).normalized,
            (Vector2.down + Vector2.right).normalized
        };

        Vector2[] origins = new Vector2[] { centerOrigin, leftOrigin, rightOrigin };

        foreach (Vector2 origin in origins)
        {
            foreach (Vector2 direction in directions)
            {
                RaycastHit2D hit = Physics2D.Raycast(
                    origin,
                    direction,
                    checkDistance * 1.5f,
                    combinedMask
                );

                Debug.DrawLine(origin, origin + direction * checkDistance * 1.5f, Color.red);

                if (hit.collider != null)
                {
                    if (((1 << hit.collider.gameObject.layer) & objectLayer) != 0)
                    {
                        currentPlatform = hit.collider;
                    }
                    return true;
                }
            }
        }

        currentPlatform = null;
        return false;
    }

    // ===================== Collision with Slime (몸 충돌만) =====================

    void OnCollisionEnter2D(Collision2D collision)
    {
        SlimePlayerController slime = collision.collider.GetComponent<SlimePlayerController>();
        if (slime == null) return;   // 슬라임이 아니면 무시

        isInContactWithSlime = true;

        // 데미지 쿨다운 체크
        if (Time.time - lastDamageTime < damageCooldown)
            return;

        lastDamageTime = Time.time;

        // 넉백 방향 = "적에서 슬라임으로 가는 방향" (슬라임 기준으로는 적 반대쪽으로 튕겨나감)
        Vector2 dir = (slime.transform.position - transform.position);
        dir.y = 0f; // 수평만
        if (dir.sqrMagnitude < 0.0001f)
        {
            dir = Vector2.right; // 혹시 같은 위치면 기본값
        }
        dir = dir.normalized;

        // 슬라임에게 데미지 + 넉백 방향 전달
        slime.TakeDamage(dir);

        // 적 잠깐 프리즈
        isStunned = true;
        stunEndTime = Time.time + stunDuration;
    }

    void OnCollisionStay2D(Collision2D collision)
    {
        SlimePlayerController slime = collision.collider.GetComponent<SlimePlayerController>();
        if (slime != null)
        {
            isInContactWithSlime = true;
            // 여기서는 추가 데미지 X (쿨다운 때문에 막혀 있음)
        }
    }

    void OnCollisionExit2D(Collision2D collision)
    {
        SlimePlayerController slime = collision.collider.GetComponent<SlimePlayerController>();
        if (slime != null)
        {
            isInContactWithSlime = false;
        }
    }

    // ===================== Gizmos =====================

    void OnDrawGizmosSelected()
    {
        if (currentPath != null && currentPath.Count > 0)
        {
            Gizmos.color = Color.yellow;
            for (int i = 0; i < currentPath.Count - 1; i++)
            {
                Gizmos.DrawLine(currentPath[i], currentPath[i + 1]);
            }
            foreach (Vector3 waypoint in currentPath)
            {
                Gizmos.DrawWireSphere(waypoint, 0.3f);
            }
        }

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, maxDetectionRange);
    }
}
