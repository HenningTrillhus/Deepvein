using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform target;
    [SerializeField] private Vector2 offset = Vector2.zero;
 
    [Header("Smoothing")]
    [Tooltip("Roughly how long (seconds) the camera takes to catch up. 0 = instant.")]
    [SerializeField] private float smoothTime = 0.15f;
 
    [Header("Optional world bounds")]
    [SerializeField] private bool useBounds = false;
    [SerializeField] private Vector2 minBounds;
    [SerializeField] private Vector2 maxBounds;
 
    private Vector3 velocity;
    private Camera cam;
 
    private void Awake()
    {
        cam = GetComponent<Camera>();
    }
 
    private void Start()
    {
        // Snap to the player on the first frame so the camera doesn't "fly in"
        if (target != null)
            transform.position = GetDesiredPosition();
    }
 
    // LateUpdate runs after the player has moved this frame, which prevents jitter
    private void LateUpdate()
    {
        if (target == null) return;
 
        Vector3 desired = GetDesiredPosition();
 
        transform.position = smoothTime > 0f
            ? Vector3.SmoothDamp(transform.position, desired, ref velocity, smoothTime)
            : desired;
    }
 
    private Vector3 GetDesiredPosition()
    {
        Vector3 pos = new Vector3(
            target.position.x + offset.x,
            target.position.y + offset.y,
            transform.position.z // keep the camera's Z (usually -10)
        );
 
        if (useBounds && cam != null && cam.orthographic)
        {
            // Keep the camera's view edges inside the world bounds
            float halfHeight = cam.orthographicSize;
            float halfWidth = halfHeight * cam.aspect;
 
            pos.x = Mathf.Clamp(pos.x, minBounds.x + halfWidth, maxBounds.x - halfWidth);
            pos.y = Mathf.Clamp(pos.y, minBounds.y + halfHeight, maxBounds.y - halfHeight);
        }
 
        return pos;
    }
 
    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
    }
}
 