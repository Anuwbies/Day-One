using UnityEngine;

public class YSorter : MonoBehaviour
{
    public float sortYOffset = 0f;   // Sorting pivot relative to object
    public int offset = 0;

    private SpriteRenderer sr;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
    }

    void LateUpdate()
    {
        float y = transform.position.y + sortYOffset;
        sr.sortingOrder = Mathf.RoundToInt(-(y * 100)) + offset;
    }

    // Draw pivot gizmo
    void OnDrawGizmos()
    {
        Vector3 pivot = transform.position + new Vector3(0, sortYOffset, 0);
        Gizmos.color = Color.yellow;
        Gizmos.DrawSphere(pivot, 0.05f);
    }
}
