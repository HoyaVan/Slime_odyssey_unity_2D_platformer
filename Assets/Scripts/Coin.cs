using UnityEngine;

public class Coin : MonoBehaviour
{
    [SerializeField] int scoreAmount = 5;
    [SerializeField] GameObject pickupVfxPrefab;

    bool collected = false;

    void OnTriggerEnter2D(Collider2D other)
    {
        // 중복 수집 방지: 이미 수집되었으면 즉시 리턴
        if (collected) return;

        SlimePlayerController slime = other.GetComponent<SlimePlayerController>();
        if (slime == null) return;

        // 즉시 collected 플래그 설정하여 중복 호출 방지
        collected = true;

        // Collider 비활성화하여 추가 트리거 방지
        Collider2D coinCollider = GetComponent<Collider2D>();
        if (coinCollider != null)
        {
            coinCollider.enabled = false;
        }

        // 점수 올리기
        if (GameManager.Instance != null)
        {
            GameManager.Instance.AddScore(scoreAmount);
            GameManager.Instance.RegisterCoinCollected();  // ← 클리어 체크
        }

        // 코인 수집 사운드
        if (AudioPlayer.Instance != null)
        {
            AudioPlayer.Instance.PlayCoinCollect();
        }

        // 파티클
        if (pickupVfxPrefab != null)
        {
            Instantiate(pickupVfxPrefab, transform.position, Quaternion.identity);
        }

        // ⭐ 스포너에게 "이 코인이 먹혔다" 라고 전달
        CoinSpawner spawner = FindFirstObjectByType<CoinSpawner>();
        if (spawner != null)
        {
            spawner.NotifyCoinCollected(gameObject);   // ← 여기!
        }

        // 코인 비활성화 후 파괴 (즉시 파괴하면 다른 스크립트에서 접근 불가)
        gameObject.SetActive(false);
        Destroy(gameObject, 0.1f);
    }
}
