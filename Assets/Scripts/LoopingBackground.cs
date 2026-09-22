using System.Collections.Generic;
using UnityEngine;

public class LoopingBackground : MonoBehaviour
{
    [SerializeField] float speed = 1f;

    List<Transform> tiles = new List<Transform>();
    float tileWidth;

    void Start()
    {
        // 자식 타일들 모음
        foreach (Transform child in transform)
            tiles.Add(child);

        // 타일 한 개의 가로 길이 측정
        var sr = tiles[0].GetComponent<SpriteRenderer>();
        tileWidth = sr.bounds.size.x;
    }

    void Update()
    {
        // 모든 타일을 왼쪽으로 이동
        foreach (var tile in tiles)
            tile.Translate(Vector2.left * speed * Time.deltaTime);

        // 가장 왼쪽 / 오른쪽 타일 찾기
        Transform left = tiles[0], right = tiles[0];

        foreach (var t in tiles)
        {
            if (t.position.x < left.position.x) left = t;
            if (t.position.x > right.position.x) right = t;
        }

        // 카메라 왼쪽 바깥으로 완전히 나가면 오른쪽 뒤로 보내기
        float camLeft = Camera.main.ViewportToWorldPoint(new Vector3(0, 0)).x;

        if (left.position.x + tileWidth < camLeft)
        {
            left.position = new Vector3(
                right.position.x + tileWidth,
                left.position.y,
                left.position.z
            );
        }
    }
}
