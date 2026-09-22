using UnityEngine;

[RequireComponent(typeof(Camera))]
public class CameraFollow2D : MonoBehaviour
{
    [Header("Target")]
    public Transform target;
    public Vector2 followOffset;
    public float smoothTime = 0.2f;

    [Header("Level Bounds (optional)")]
    public BoxCollider2D cameraBounds;   // assign CameraBounds here

    Vector3 velocity;
    Vector2 minBounds;
    Vector2 maxBounds;
    Camera cam;

    void Awake()
    {
        cam = GetComponent<Camera>();

        if (cameraBounds != null)
        {
            Bounds b = cameraBounds.bounds;
            minBounds = b.min;
            maxBounds = b.max;
        }
    }

    void LateUpdate()
    {
        if (target == null) return;

        // desired position following the player
        Vector3 targetPos = new Vector3(
            target.position.x + followOffset.x,
            target.position.y + followOffset.y,
            transform.position.z);

        // clamp **camera** to bounds (so we never show outside the box)
        if (cameraBounds != null)
        {
            float halfHeight = cam.orthographicSize;
            float halfWidth = halfHeight * cam.aspect;

            targetPos.x = Mathf.Clamp(targetPos.x, minBounds.x + halfWidth, maxBounds.x - halfWidth);
            targetPos.y = Mathf.Clamp(targetPos.y, minBounds.y + halfHeight, maxBounds.y - halfHeight);
        }

        // smoothly move camera only (player is untouched)
        transform.position = Vector3.SmoothDamp(transform.position, targetPos, ref velocity, smoothTime);
    }
}
