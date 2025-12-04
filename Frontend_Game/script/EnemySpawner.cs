using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemySpawner : MonoBehaviour
{
    [Header("Spawn Settings")]
    [SerializeField] GameObject enemyPrefab;  // 적 프리팹 (인스펙터에서 할당)
    [SerializeField] int maxEnemyCount = 5;  // 최대 적 수
    [SerializeField] float startSpawnTime = 2f;  // 스폰 시작 시간 (초)
    [SerializeField] float spawnInterval = 3f;  // 스폰 간격 (초)
    [SerializeField] float spawnMargin = 1f;  // 벽으로부터의 최소 거리

    [Header("Spawn Area")]
    [SerializeField] bool useLevelWalls = true;  // LevelWalls를 사용할지 여부
    [SerializeField] Vector2 customMinBounds;  // 커스텀 최소 경계 (useLevelWalls가 false일 때)
    [SerializeField] Vector2 customMaxBounds;  // 커스텀 최대 경계 (useLevelWalls가 false일 때)

    [Header("Ground Detection")]
    [SerializeField] LayerMask groundLayer;  // 지면 레이어
    [SerializeField] LayerMask objectLayer;  // 오브젝트 레이어
    [SerializeField] LayerMask animalLayer;  // 동물 레이어

    List<GameObject> spawnedEnemies = new List<GameObject>();
    float spawnTimer = 0f;
    bool hasStartedSpawning = false;

    void Start()
    {
        if (enemyPrefab == null)
        {
            Debug.LogError("EnemySpawner: Enemy Prefab이 할당되지 않았습니다!");
            enabled = false;
            return;
        }

        // 시작 시간까지 대기
        spawnTimer = startSpawnTime;
    }

    void Update()
    {
        // 시작 시간이 되었는지 확인
        if (!hasStartedSpawning && Time.time >= startSpawnTime)
        {
            hasStartedSpawning = true;
            SpawnEnemy();
            spawnTimer = spawnInterval;
        }

        // 스폰 중이고 최대 수에 도달하지 않았으면 계속 스폰
        if (hasStartedSpawning && spawnedEnemies.Count < maxEnemyCount)
        {
            spawnTimer -= Time.deltaTime;
            if (spawnTimer <= 0f)
            {
                SpawnEnemy();
                spawnTimer = spawnInterval;
            }
        }

        // 죽은 적들을 리스트에서 제거
        CleanupDeadEnemies();
    }

    void SpawnEnemy()
    {
        if (spawnedEnemies.Count >= maxEnemyCount)
            return;

        // 적 인스턴스 생성
        GameObject newEnemy = Instantiate(enemyPrefab);

        // 랜덤 위치 설정
        Vector3 spawnPosition = GetRandomSpawnPosition();
        newEnemy.transform.position = spawnPosition;

        // 활성화 (프리팹이 비활성화되어 있다면)
        newEnemy.SetActive(true);

        // Collider2D가 있는지 확인하고 지면 위에 배치
        Collider2D enemyCol = newEnemy.GetComponent<Collider2D>();
        if (enemyCol != null)
        {
            // 콜라이더가 지면과 겹치지 않도록 조정
            Vector2 checkOrigin = new Vector2(spawnPosition.x, spawnPosition.y + 5f);  // 위에서부터 체크
            RaycastHit2D groundHit = Physics2D.Raycast(
                checkOrigin,
                Vector2.down,
                10f,
                groundLayer | objectLayer | animalLayer
            );

            if (groundHit.collider != null)
            {
                // 지면 위에 콜라이더의 절반 높이만큼 오프셋 추가
                float offset = enemyCol.bounds.size.y / 2f + 0.1f;
                Vector3 adjustedPosition = new Vector3(
                    spawnPosition.x,
                    groundHit.point.y + offset,
                    spawnPosition.z
                );
                newEnemy.transform.position = adjustedPosition;
                spawnPosition = adjustedPosition;
            }
        }

        // 리스트에 추가
        spawnedEnemies.Add(newEnemy);
    }

    Vector3 GetRandomSpawnPosition()
    {
        float minX, maxX, minY, maxY;

        if (useLevelWalls)
        {
            // LevelWalls에서 경계 가져오기
            GameObject levelWalls = GameObject.Find("LevelWalls");
            if (levelWalls == null)
            {
                Debug.LogWarning("LevelWalls를 찾을 수 없습니다. 커스텀 경계를 사용합니다.");
                minX = customMinBounds.x;
                maxX = customMaxBounds.x;
                minY = customMinBounds.y;
                maxY = customMaxBounds.y;
            }
            else
            {
                // 벽들 찾기
                Transform topWall = levelWalls.transform.Find("TopWall");
                Transform rightWall = levelWalls.transform.Find("RightWall");
                Transform leftWall = levelWalls.transform.Find("LeftWall");
                Transform downWall = levelWalls.transform.Find("DownWall");

                if (topWall == null || rightWall == null || leftWall == null || downWall == null)
                {
                    Debug.LogWarning("LevelWalls의 일부 벽을 찾을 수 없습니다. 커스텀 경계를 사용합니다.");
                    minX = customMinBounds.x;
                    maxX = customMaxBounds.x;
                    minY = customMinBounds.y;
                    maxY = customMaxBounds.y;
                }
                else
                {
                    // 벽들의 경계 계산
                    minX = leftWall.position.x;
                    maxX = rightWall.position.x;
                    minY = downWall.position.y;
                    maxY = topWall.position.y;

                    // Collider가 있는 경우 더 정확한 경계 사용
                    Collider2D leftCollider = leftWall.GetComponent<Collider2D>();
                    Collider2D rightCollider = rightWall.GetComponent<Collider2D>();
                    Collider2D topCollider = topWall.GetComponent<Collider2D>();
                    Collider2D downCollider = downWall.GetComponent<Collider2D>();

                    if (leftCollider != null)
                    {
                        minX = leftCollider.bounds.max.x + spawnMargin;
                    }
                    else
                    {
                        minX += spawnMargin;
                    }

                    if (rightCollider != null)
                    {
                        maxX = rightCollider.bounds.min.x - spawnMargin;
                    }
                    else
                    {
                        maxX -= spawnMargin;
                    }

                    if (downCollider != null)
                    {
                        minY = downCollider.bounds.max.y + spawnMargin;
                    }
                    else
                    {
                        minY += spawnMargin;
                    }

                    if (topCollider != null)
                    {
                        maxY = topCollider.bounds.min.y - spawnMargin;
                    }
                    else
                    {
                        maxY -= spawnMargin;
                    }
                }
            }
        }
        else
        {
            // 커스텀 경계 사용
            minX = customMinBounds.x;
            maxX = customMaxBounds.x;
            minY = customMinBounds.y;
            maxY = customMaxBounds.y;
        }

        // 랜덤 X 위치 생성
        float randomX = Random.Range(minX, maxX);

        // Y 위치는 지면 위에 배치 (레이캐스트로 지면 찾기)
        float randomY = FindGroundY(randomX, minY, maxY);

        return new Vector3(randomX, randomY, 0f);
    }

    float FindGroundY(float x, float minY, float maxY)
    {
        // X 위치에서 지면을 찾기 위해 위에서 아래로 레이캐스트
        Vector2 rayOrigin = new Vector2(x, maxY);
        float rayDistance = maxY - minY;

        int combinedMask = groundLayer | objectLayer | animalLayer;

        RaycastHit2D hit = Physics2D.Raycast(
            rayOrigin,
            Vector2.down,
            rayDistance,
            combinedMask
        );

        if (hit.collider != null)
        {
            // 지면 위에 충분한 여유 공간 추가 (콜라이더 높이 고려)
            float offset = 0.5f;  // 기본 오프셋

            // 적 프리팹의 콜라이더 높이를 고려
            if (enemyPrefab != null)
            {
                Collider2D enemyCol = enemyPrefab.GetComponent<Collider2D>();
                if (enemyCol != null)
                {
                    offset = enemyCol.bounds.size.y / 2f + 0.1f;  // 콜라이더 높이의 절반 + 여유
                }
            }

            return hit.point.y + offset;
        }

        // 지면을 찾지 못한 경우 중간 높이 사용
        Debug.LogWarning($"EnemySpawner: X={x} 위치에서 지면을 찾을 수 없습니다. 기본 높이 사용: {(minY + maxY) / 2f}");
        return (minY + maxY) / 2f;
    }

    void CleanupDeadEnemies()
    {
        // null이거나 비활성화된 적들을 리스트에서 제거
        for (int i = spawnedEnemies.Count - 1; i >= 0; i--)
        {
            if (spawnedEnemies[i] == null || !spawnedEnemies[i].activeInHierarchy)
            {
                spawnedEnemies.RemoveAt(i);
            }
        }
    }

    // 현재 스폰된 적 수를 반환 (디버깅용)
    public int GetCurrentEnemyCount()
    {
        return spawnedEnemies.Count;
    }

    // 모든 적 제거 (필요시 호출)
    public void ClearAllEnemies()
    {
        foreach (GameObject enemy in spawnedEnemies)
        {
            if (enemy != null)
            {
                Destroy(enemy);
            }
        }
        spawnedEnemies.Clear();
    }
}

