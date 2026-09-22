using System.Collections.Generic;
using UnityEngine;

public class CoinSpawner : MonoBehaviour
{
    [Header("Spawn Settings")]
    [SerializeField] GameObject coinPrefab;

    [SerializeField] int maxSimultaneousCoins = 1;   // 화면에 동시에 존재 가능한 코인 수
    [SerializeField] int maxTotalSpawnCount = 0;     // 전체 스폰 가능한 최대 코인 수 (0 = 무제한)

    [SerializeField] float startSpawnDelay = 1f;     // 처음 스폰까지 대기 시간
    [SerializeField] float spawnInterval = 3f;       // 다음 스폰까지 간격
    [SerializeField] float spawnMargin = 0.5f;
    [SerializeField] float minDistanceFromPlayer = 2.5f;

    [Header("Player")]
    [SerializeField] Transform slime;

    [Header("Layers")]
    [SerializeField] LayerMask groundLayer;
    [SerializeField] LayerMask objectLayer;

    [Header("Spawn Area")]
    [SerializeField] bool useLevelWalls = true;
    [SerializeField] Vector2 customMinBounds;
    [SerializeField] Vector2 customMaxBounds;

    List<GameObject> activeCoins = new List<GameObject>();
    float spawnTimer;
    int totalSpawned = 0;

    void Start()
    {
        if (coinPrefab == null)
        {
            Debug.LogError("CoinSpawner: coinPrefab is null!");
            enabled = false;
            return;
        }

        if (slime == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) slime = player.transform;
        }

        spawnTimer = startSpawnDelay;
    }

    void Update()
    {
        CleanupList();

        // 더 이상 스폰 못 하는 상태면 종료
        bool canSpawnMoreTotal = (maxTotalSpawnCount == 0 || totalSpawned < maxTotalSpawnCount);
        if (!canSpawnMoreTotal) return;

        // 동시 코인 수가 제한보다 적을 때만 스폰 타이머 진행
        if (activeCoins.Count < maxSimultaneousCoins)
        {
            spawnTimer -= Time.deltaTime;
            if (spawnTimer <= 0f)
            {
                SpawnCoin();
                spawnTimer = spawnInterval;
            }
        }
    }

    void SpawnCoin()
    {
        bool canSpawnMoreTotal = (maxTotalSpawnCount == 0 || totalSpawned < maxTotalSpawnCount);
        if (!canSpawnMoreTotal) return;

        Vector3 pos = GetSafeSpawnPosition();
        GameObject coin = Instantiate(coinPrefab, pos, Quaternion.identity);
        activeCoins.Add(coin);
        totalSpawned++;
    }

    Vector3 GetSafeSpawnPosition()
    {
        float minX, maxX, minY, maxY;

        if (useLevelWalls && GameObject.Find("LevelWalls"))
        {
            Transform lw = GameObject.Find("LevelWalls").transform;
            Transform left = lw.Find("LeftWall");
            Transform right = lw.Find("RightWall");
            Transform top = lw.Find("TopWall");
            Transform down = lw.Find("DownWall");

            minX = left.GetComponent<Collider2D>().bounds.max.x + spawnMargin;
            maxX = right.GetComponent<Collider2D>().bounds.min.x - spawnMargin;
            minY = down.GetComponent<Collider2D>().bounds.max.y + spawnMargin;
            maxY = top.GetComponent<Collider2D>().bounds.min.y - spawnMargin;
        }
        else
        {
            minX = customMinBounds.x;
            maxX = customMaxBounds.x;
            minY = customMinBounds.y;
            maxY = customMaxBounds.y;
        }

        Vector3 pos = Vector3.zero;
        int safety = 0;

        while (true)
        {
            safety++;
            if (safety > 20)
            {
                pos = new Vector3((minX + maxX) * 0.5f, (minY + maxY) * 0.5f, 0f);
                break;
            }

            float x = Random.Range(minX, maxX);
            float y = FindGroundY(x, minY, maxY);

            pos = new Vector3(x, y, 0f);

            if (slime != null &&
                Vector2.Distance(pos, slime.position) < minDistanceFromPlayer)
            {
                // 플레이어와 너무 가까우면 다시 뽑기
                continue;
            }

            break;
        }

        return pos;
    }

    float FindGroundY(float x, float minY, float maxY)
    {
        Vector2 origin = new Vector2(x, maxY);
        float rayDistance = maxY - minY;

        int mask = groundLayer | objectLayer; // 여기서 animalLayer 절대 포함 X

        RaycastHit2D hit = Physics2D.Raycast(origin, Vector2.down, rayDistance, mask);

        if (hit.collider != null)
        {
            float offset = 0.3f;
            Collider2D col = coinPrefab.GetComponent<Collider2D>();
            if (col != null)
                offset = col.bounds.size.y / 2f + 0.05f;

            return hit.point.y + offset;
        }

        return (minY + maxY) * 0.5f;
    }

    void CleanupList()
    {
        for (int i = activeCoins.Count - 1; i >= 0; i--)
        {
            if (activeCoins[i] == null)
                activeCoins.RemoveAt(i);
        }
    }

    // 코인이 먹혔을 때 Coin.cs에서 알려줄 곳 (필수는 아님, 있지만 더 깔끔)
    public void NotifyCoinCollected(GameObject coin)
    {
        activeCoins.Remove(coin);

        // 코인을 먹자마자 바로 새 코인 나오게 하고 싶으면:
        // spawnTimer = 0f;
    }
}
