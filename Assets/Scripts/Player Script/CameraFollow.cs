using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public Transform target;
    public float smoothSpeed = 5f;
    public Vector3 offset;

    // Zoom settings
    public Camera cam;
    public float minZoom = 5f;       // Zoomed in when idle
    public float maxZoom = 8f;       // Zoomed out when moving
    public float zoomInSpeed = 2f;   // units per second when zooming in
    public float zoomOutSpeed = 8f;  // units per second when zooming out

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
        transform.position = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed * Time.deltaTime);

        // Determine target zoom
        float speed = playerMovement.movement.sqrMagnitude;
        float targetZoom;

        if (speed > 0.01f)
        {
            // Moving → zoom out
            targetZoom = maxZoom;
            idleTimer = 0f;
        }
        else
        {
            // Idle → start counting
            idleTimer += Time.deltaTime;
            targetZoom = idleTimer >= idleDelay ? minZoom : cam.orthographicSize;
        }

        // Move zoom linearly toward target
        float zoomSpeed = cam.orthographicSize < targetZoom ? zoomOutSpeed : zoomInSpeed;
        cam.orthographicSize = Mathf.MoveTowards(cam.orthographicSize, targetZoom, zoomSpeed * Time.deltaTime);
    }
}
