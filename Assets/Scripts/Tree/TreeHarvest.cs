using UnityEngine;

public class TreeHarvest : MonoBehaviour
{
    [Header("Sprites")]
    public Sprite trunkSprite;

    [Header("Drops")]
    public GameObject woodDropPrefab;
    public int minDrops = 2;
    public int maxDrops = 4;

    [Header("References")]
    public SpriteRenderer spriteRenderer;
    public Collider2D hitCollider;     // trigger collider
    public Collider2D solidCollider;   // movement collider (optional control)

    private EnemyHealth health;
    private bool harvested = false;

    private void Awake()
    {
        health = GetComponent<EnemyHealth>();

        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();

        health.OnDeath += OnTreeDestroyed;
    }

    private void OnTreeDestroyed()
    {
        if (harvested)
            return;

        harvested = true;

        // Change sprite to trunk
        if (trunkSprite != null)
            spriteRenderer.sprite = trunkSprite;

        // Disable ONLY hit collider (cannot be attacked again)
        if (hitCollider != null)
            hitCollider.enabled = false;

        SpawnDrops();
    }

    private void SpawnDrops()
    {
        if (woodDropPrefab == null)
            return;

        int count = Random.Range(minDrops, maxDrops + 1);

        for (int i = 0; i < count; i++)
        {
            Vector2 offset = Random.insideUnitCircle * 0.4f;
            Instantiate(
                woodDropPrefab,
                (Vector2)transform.position + offset,
                Quaternion.identity
            );
        }
    }
}
