using UnityEngine;
using System.Collections.Generic;

public class DropLoot : MonoBehaviour
{
    [Header("Optional Sprite Change")]
    public SpriteRenderer spriteRenderer;
    public Sprite afterLootSprite;
    public bool changeSpriteOnLoot = false;

    [Header("Hitbox Disable")]
    public Collider2D hitbox;

    [Header("Loot Settings")]
    public List<LootEntry> lootTable = new List<LootEntry>();

    [Header("Drop Position Offsets")]
    [Tooltip("Fixed horizontal offset from the object center")]
    public float xOffset = 0f;

    [Tooltip("Fixed vertical offset from the object center")]
    public float yOffset = 0f;

    [Header("Circular Drop Settings")]
    [Tooltip("Outer Radius (X, Y). Set X > Y for a wide oval.")]
    public Vector2 dropRadius = new Vector2(1.5f, 1.5f);

    [Tooltip("Inner Radius (X, Y). The empty space in the middle.")]
    public Vector2 deadZoneRadius = new Vector2(0.5f, 0.5f);

    [Header("Destroy Object (Optional)")]
    [Tooltip("Destroy this GameObject after loot is dropped")]
    public bool destroyOnLoot = false;

    [Tooltip("Delay before destroying the object (seconds)")]
    public float destroyDelay = 0f;

    private bool looted = false;

    private void Awake()
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();

        // Optional: Listen for death event if you have an EnemyHealth script
        EnemyHealth health = GetComponent<EnemyHealth>();
        if (health != null)
            health.OnDeath += Drop;
    }

    public void Drop()
    {
        if (looted)
            return;

        looted = true;

        // Change sprite to "looted" version (e.g. open chest)
        if (changeSpriteOnLoot && spriteRenderer != null && afterLootSprite != null)
            spriteRenderer.sprite = afterLootSprite;

        // Disable collider so player can walk through
        if (hitbox != null)
            hitbox.enabled = false;

        SpawnLoot();

        if (destroyOnLoot)
            Destroy(gameObject, destroyDelay);
    }

    private void SpawnLoot()
    {
        Vector2 baseOffset = new Vector2(xOffset, yOffset);
        Vector3 centerPos = transform.position + (Vector3)baseOffset;

        foreach (LootEntry entry in lootTable)
        {
            if (entry.prefab == null)
                continue;

            int count = Random.Range(entry.minAmount, entry.maxAmount + 1);

            for (int i = 0; i < count; i++)
            {
                // Get a random position within the oval annulus
                Vector2 randomOffset = GetRandomPointInAnnulus(deadZoneRadius, dropRadius);
                Vector2 finalPosition = (Vector2)centerPos + randomOffset;

                Instantiate(entry.prefab, finalPosition, Quaternion.identity);
            }
        }
    }

    // Calculates a random point between minRadii and maxRadii (Oval support)
    private Vector2 GetRandomPointInAnnulus(Vector2 minRadii, Vector2 maxRadii)
    {
        // Get a random direction (normalized vector)
        Vector2 dir = Random.insideUnitCircle.normalized;

        // If normalized failed (rare 0,0 case), pick UP as default
        if (dir == Vector2.zero)
            dir = Vector2.up;

        // Pick a random t (0 to 1)
        float t = Random.Range(0f, 1f);

        // Interpolate between the inner radius and outer radius for X and Y separately
        float rX = Mathf.Lerp(minRadii.x, maxRadii.x, t);
        float rY = Mathf.Lerp(minRadii.y, maxRadii.y, t);

        // Apply the elliptical scaling
        return new Vector2(dir.x * rX, dir.y * rY);
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 center = transform.position + new Vector3(xOffset, yOffset, 0f);

        // Draw Outer Oval (Yellow)
        Gizmos.color = Color.yellow;
        DrawEllipse(center, dropRadius.x, dropRadius.y);

        // Draw Inner Dead Zone Oval (Red)
        Gizmos.color = Color.red;
        DrawEllipse(center, deadZoneRadius.x, deadZoneRadius.y);

        // Draw Center Line to show offset
        Gizmos.color = Color.white;
        Gizmos.DrawLine(transform.position, center);
    }

    // Helper to draw ellipses in the editor
    private void DrawEllipse(Vector3 center, float radiusX, float radiusY)
    {
        float step = 10f;
        Vector3 lastPos = center + new Vector3(radiusX, 0, 0); // Start at 0 degrees

        for (float angle = step; angle <= 360; angle += step)
        {
            float rad = angle * Mathf.Deg2Rad;
            Vector3 nextPos = center + new Vector3(Mathf.Cos(rad) * radiusX, Mathf.Sin(rad) * radiusY, 0);
            Gizmos.DrawLine(lastPos, nextPos);
            lastPos = nextPos;
        }
    }
}

[System.Serializable]
public class LootEntry
{
    public GameObject prefab;
    public int minAmount = 1;
    public int maxAmount = 1;
}