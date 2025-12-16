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
    [Tooltip("Fixed horizontal offset")]
    public float xOffset = 0f;

    [Tooltip("Fixed vertical offset")]
    public float yOffset = 0f;

    [Tooltip("Random spread around the offset")]
    public Vector2 randomSpread = new Vector2(0.4f, 0.4f);

    [Header("Dead Zone (No Drop Area)")]
    [Tooltip("Size of the inner area where loot will NOT spawn")]
    public Vector2 deadZoneSize = new Vector2(0.3f, 0.3f);

    [Header("Destroy Object (Optional)")]
    [Tooltip("Destroy this GameObject after loot is dropped")]
    public bool destroyOnLoot = false;

    [Tooltip("Delay before destroying the object (seconds)")]
    public float destroyDelay = 0f;

    private bool looted = false;
    private const int MAX_POSITION_ATTEMPTS = 15;

    private void Awake()
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();

        EnemyHealth health = GetComponent<EnemyHealth>();
        if (health != null)
            health.OnDeath += Drop;
    }

    public void Drop()
    {
        if (looted)
            return;

        looted = true;

        if (changeSpriteOnLoot && spriteRenderer != null && afterLootSprite != null)
            spriteRenderer.sprite = afterLootSprite;

        if (hitbox != null)
            hitbox.enabled = false;

        SpawnLoot();

        if (destroyOnLoot)
            Destroy(gameObject, destroyDelay);
    }

    private void SpawnLoot()
    {
        Vector2 baseOffset = new Vector2(xOffset, yOffset);

        foreach (LootEntry entry in lootTable)
        {
            if (entry.prefab == null)
                continue;

            int count = Random.Range(entry.minAmount, entry.maxAmount + 1);

            for (int i = 0; i < count; i++)
            {
                Vector2 randomOffset = GenerateValidOffset();
                Vector2 finalPosition =
                    (Vector2)transform.position + baseOffset + randomOffset;

                Instantiate(entry.prefab, finalPosition, Quaternion.identity);
            }
        }
    }

    private Vector2 GenerateValidOffset()
    {
        Vector2 offset = Vector2.zero;

        for (int attempt = 0; attempt < MAX_POSITION_ATTEMPTS; attempt++)
        {
            offset = new Vector2(
                Random.Range(-randomSpread.x, randomSpread.x),
                Random.Range(-randomSpread.y, randomSpread.y)
            );

            if (!IsInsideDeadZone(offset))
                return offset;
        }

        return offset;
    }

    private bool IsInsideDeadZone(Vector2 offset)
    {
        return Mathf.Abs(offset.x) < deadZoneSize.x * 0.5f &&
               Mathf.Abs(offset.y) < deadZoneSize.y * 0.5f;
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 center = transform.position + new Vector3(xOffset, yOffset, 0f);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(
            center,
            new Vector3(randomSpread.x * 2f, randomSpread.y * 2f, 0.01f)
        );

        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(
            center,
            new Vector3(deadZoneSize.x, deadZoneSize.y, 0.01f)
        );

        Gizmos.DrawLine(transform.position, center);
    }
}

[System.Serializable]
public class LootEntry
{
    public GameObject prefab;
    public int minAmount = 1;
    public int maxAmount = 1;
}
