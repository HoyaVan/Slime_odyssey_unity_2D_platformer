using UnityEngine;

public class EnemyDamageTrigger : MonoBehaviour
{
    [Header("Reference")]
    [SerializeField] EnemyPlayerController enemy;   // 부모 Enemy

    [Header("Damage Settings")]
    [SerializeField] float damageCooldown = 0.5f;   // 같은 적에게 맞는 최소 간격
    // Note: knockbackForce removed - not currently used in damage calculation

    float lastDamageTime = -10f;

    void Reset()
    {
        // 에디터에서 스크립트 붙였을 때 자동으로 부모 Enemy 할당해주기
        if (enemy == null)
        {
            enemy = GetComponentInParent<EnemyPlayerController>();
        }

        // Trigger Collider 강제 설정
        Collider2D col = GetComponent<Collider2D>();
        if (col != null) col.isTrigger = true;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        TryDamage(other);
    }

    void OnTriggerStay2D(Collider2D other)
    {
        // 슬라임이 계속 닿아있을 때도 일정 간격으로 데미지 주고 싶으면 유지
        TryDamage(other);
    }

    void TryDamage(Collider2D other)
    {
        SlimePlayerController slime = other.GetComponent<SlimePlayerController>();
        if (slime == null) return;

        if (Time.time - lastDamageTime < damageCooldown) return;

        // 넉백 방향 계산 (Enemy → Slime 방향)
        Vector2 dir = (slime.transform.position - enemy.transform.position).normalized;
        if (dir.sqrMagnitude < 0.01f)
        {
            dir = Vector2.right; // 혹시 같은 위치이면 기본값
        }

        slime.TakeDamage(dir);   // 슬라임 스크립트의 함수 호출
        lastDamageTime = Time.time;
    }
}
