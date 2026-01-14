using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public Transform target;
    public float smoothSpeed = 5f;
    public Vector3 offset;

    // Zoom settings
    public Camera cam;
    public float minZoom = 5f;
    public float maxZoom = 8f;
    public float zoomInSpeed = 2f;
    public float zoomOutSpeed = 8f;

    // Slowdown behavior
    public float zoomSlowRange = 0.75f;
    [Range(0.1f, 1f)]
    public float minSlowFactor = 0.3f; // 30% minimum speed

    // Idle delay settings
    public float idleDelay = 1.0f;
    private float idleTimer = 0f;

    private PlayerMovement playerMovement;

    void Start()
    {
        if (cam == null)
            cam = Camera.main;

        playerMovement = target.GetComponent<PlayerMovement>();
    }

    void LateUpdate()
    {
        if (target == null) return;

        // Smooth follow
        Vector3 desiredPosition = target.position + offset;
        transform.position = Vector3.Lerp(
            transform.position,
            desiredPosition,
            smoothSpeed * Time.deltaTime
        );

        // Determine target zoom
        float speed = playerMovement.movement.sqrMagnitude;
        float targetZoom;

        if (speed > 0.01f)
        {
            targetZoom = maxZoom;
            idleTimer = 0f;
        }
        else
        {
            idleTimer += Time.deltaTime;
            targetZoom = idleTimer >= idleDelay ? minZoom : cam.orthographicSize;
        }

        float currentZoom = cam.orthographicSize;

        // Base speed direction
        float baseSpeed = currentZoom < targetZoom ? zoomOutSpeed : zoomInSpeed;

        // Distance-based slowdown
        float distance = Mathf.Abs(targetZoom - currentZoom);

        float slowdownFactor = Mathf.Clamp01(distance / zoomSlowRange);

        // Enforce minimum slowdown limit
        slowdownFactor = Mathf.Max(slowdownFactor, minSlowFactor);

        float finalSpeed = baseSpeed * slowdownFactor;

        cam.orthographicSize = Mathf.MoveTowards(
            currentZoom,
            targetZoom,
            finalSpeed * Time.deltaTime
        );
    }
}
