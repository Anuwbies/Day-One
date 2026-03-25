using UnityEngine;

public class YSorter : MonoBehaviour
{
    [System.Serializable]
    public struct TransparencyTrigger
    {
        public string name;
        public Vector2 offset;
        public Vector2 size;
        [Tooltip("Higher radius makes the corners rounder. If radius >= half-size, it becomes a circle/capsule.")]
        public float cornerRadius;
    }

    public float sortYOffset = 0f;   // Sorting pivot relative to object
    public int offset = 0;
    [Tooltip("If enabled, this renderer will always stay behind the player.")]
    public bool alwaysBehindPlayer = false;
    [Tooltip("If enabled, this renderer will always stay behind the nearest parent sort reference in the hierarchy.")]
    public bool alwaysBehindParent = false;

    [Header("Transparency Settings")]
    public bool enableTransparency = true;
    public float fadeAlpha = 0.5f;
    public float fadeSpeed = 10f;
    
    [Tooltip("Define multiple areas that trigger transparency when the player enters them.")]
    public TransparencyTrigger[] triggerAreas;

    [Header("Gizmo Settings")]
    [Tooltip("Show or hide the transparency trigger area gizmos in the editor.")]
    public bool showTriggerAreaGizmos = true;

    private SpriteRenderer sr;
    private SpriteRenderer playerSR;
    private Transform playerTransform;
    private float targetAlpha = 1f;
    private float originalAlpha = 1f;
    private const float PivotGizmoRadius = 0.025f;
    private const float PlayerGizmoRadius = 0.05f;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        if (sr != null) originalAlpha = sr.color.a;
        
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            playerTransform = player.transform;
            playerSR = player.GetComponent<SpriteRenderer>();
            if (player == gameObject) enableTransparency = false;
        }
    }

    void LateUpdate()
    {
        // Lazy find player if not already found
        if (playerTransform == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                playerTransform = player.transform;
                playerSR = player.GetComponent<SpriteRenderer>();
                if (player == gameObject) enableTransparency = false;
            }
        }

        float pivotY = transform.position.y + sortYOffset;
        if (sr != null)
        {
            int sortingOrder = Mathf.RoundToInt(-(pivotY * 100)) + offset;

            if (alwaysBehindPlayer && playerTransform != null)
            {
                int playerSortingOrder = playerSR != null
                    ? playerSR.sortingOrder
                    : Mathf.RoundToInt(-(playerTransform.position.y * 100));

                sortingOrder = Mathf.Min(sortingOrder, playerSortingOrder - 1);
            }

            if (alwaysBehindParent && TryGetParentSortingOrder(out int parentSortingOrder))
            {
                sortingOrder = Mathf.Min(sortingOrder, parentSortingOrder - 1);
            }

            sr.sortingOrder = sortingOrder;
        }

        if (enableTransparency && playerTransform != null)
        {
            UpdateTransparency(pivotY);
        }
    }

    private void UpdateTransparency(float pivotY)
    {
        Vector3 playerPos = playerTransform.position;

        // Player is behind if their sorting order is lower than this object's
        // Fallback to Y comparison if playerSR is not available
        bool isBehind = (playerSR != null) ? (playerSR.sortingOrder < sr.sortingOrder) : (playerPos.y > pivotY);
        bool isInAnyTrigger = false;

        // Check if player's center is inside any of the defined trigger areas
        if (triggerAreas != null)
        {
            foreach (var trigger in triggerAreas)
            {
                Vector3 triggerPos = transform.position + (Vector3)trigger.offset;
                
                // Rounded Rectangle Logic:
                Vector2 d = new Vector2(Mathf.Abs(playerPos.x - triggerPos.x), Mathf.Abs(playerPos.y - triggerPos.y));
                Vector2 halfSize = trigger.size * 0.5f;
                float r = Mathf.Clamp(trigger.cornerRadius, 0, Mathf.Min(halfSize.x, halfSize.y));
                Vector2 q = new Vector2(Mathf.Max(d.x - (halfSize.x - r), 0), Mathf.Max(d.y - (halfSize.y - r), 0));
                
                if (q.sqrMagnitude <= r * r)
                {
                    isInAnyTrigger = true;
                    break;
                }
            }
        }

        // Trigger transparency if behind AND inside a trigger area
        targetAlpha = (isBehind && isInAnyTrigger) ? fadeAlpha : originalAlpha;

        if (Mathf.Abs(sr.color.a - targetAlpha) > 0.01f)
        {
            float newAlpha = Mathf.MoveTowards(sr.color.a, targetAlpha, fadeSpeed * Time.deltaTime);
            Color c = sr.color;
            c.a = newAlpha;
            sr.color = c;
        }
    }

    // Draw pivot gizmo
    void OnDrawGizmos()
    {
        if (alwaysBehindPlayer)
        {
            return;
        }

        Vector3 pivot = transform.position + new Vector3(0, sortYOffset, 0);
        
        // 1. Draw the Sorting Pivot (Yellow)
        Gizmos.color = Color.yellow;
        Gizmos.DrawSphere(pivot, PivotGizmoRadius);

        if (enableTransparency && showTriggerAreaGizmos && triggerAreas != null)
        {
            // 2. Draw each Trigger Area (Magenta)
            Gizmos.color = Color.magenta;
            foreach (var trigger in triggerAreas)
            {
                Vector3 triggerPos = transform.position + (Vector3)trigger.offset;
                DrawRoundedRect(triggerPos, trigger.size, trigger.cornerRadius);
            }
        }

        // Visualize the player position (Cyan) - Works in Edit Mode too
        Transform pTransform = playerTransform;
        if (pTransform == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) pTransform = p.transform;
        }

        if (pTransform != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(pTransform.position, PlayerGizmoRadius);
        }
    }

    private void DrawRoundedRect(Vector3 center, Vector2 size, float radius)
    {
        Vector2 halfSize = size * 0.5f;
        float r = Mathf.Clamp(radius, 0, Mathf.Min(halfSize.x, halfSize.y));

        // Draw 4 segments of the rectangle
        Gizmos.DrawLine(center + new Vector3(-halfSize.x + r, halfSize.y, 0), center + new Vector3(halfSize.x - r, halfSize.y, 0)); // Top
        Gizmos.DrawLine(center + new Vector3(-halfSize.x + r, -halfSize.y, 0), center + new Vector3(halfSize.x - r, -halfSize.y, 0)); // Bottom
        Gizmos.DrawLine(center + new Vector3(-halfSize.x, halfSize.y - r, 0), center + new Vector3(-halfSize.x, -halfSize.y + r, 0)); // Left
        Gizmos.DrawLine(center + new Vector3(halfSize.x, halfSize.y - r, 0), center + new Vector3(halfSize.x, -halfSize.y + r, 0)); // Right

        if (r > 0)
        {
            // Draw corners
            DrawArc(center + new Vector3(halfSize.x - r, halfSize.y - r, 0), r, 0, 90);    // Top Right
            DrawArc(center + new Vector3(-halfSize.x + r, halfSize.y - r, 0), r, 90, 180);  // Top Left
            DrawArc(center + new Vector3(-halfSize.x + r, -halfSize.y + r, 0), r, 180, 270); // Bottom Left
            DrawArc(center + new Vector3(halfSize.x - r, -halfSize.y + r, 0), r, 270, 360); // Bottom Right
        }
    }

    private void DrawArc(Vector3 center, float r, float startAngle, float endAngle)
    {
        Vector3 prevPoint = center + new Vector3(Mathf.Cos(startAngle * Mathf.Deg2Rad) * r, Mathf.Sin(startAngle * Mathf.Deg2Rad) * r, 0);
        int segments = 8;
        for (int i = 1; i <= segments; i++)
        {
            float angle = Mathf.Lerp(startAngle, endAngle, (float)i / segments);
            Vector3 nextPoint = center + new Vector3(Mathf.Cos(angle * Mathf.Deg2Rad) * r, Mathf.Sin(angle * Mathf.Deg2Rad) * r, 0);
            Gizmos.DrawLine(prevPoint, nextPoint);
            prevPoint = nextPoint;
        }
    }

    private bool TryGetParentSortingOrder(out int sortingOrder)
    {
        Transform current = transform.parent;
        while (current != null)
        {
            YSorter parentSorter = current.GetComponent<YSorter>();
            if (parentSorter != null)
            {
                sortingOrder = parentSorter.GetCurrentSortingOrder();
                return true;
            }

            SpriteRenderer parentRenderer = current.GetComponent<SpriteRenderer>();
            if (parentRenderer != null)
            {
                sortingOrder = parentRenderer.sortingOrder;
                return true;
            }

            current = current.parent;
        }

        sortingOrder = 0;
        return false;
    }

    private int GetCurrentSortingOrder()
    {
        if (sr != null)
        {
            return sr.sortingOrder;
        }

        float pivotY = transform.position.y + sortYOffset;
        return Mathf.RoundToInt(-(pivotY * 100)) + offset;
    }
}
