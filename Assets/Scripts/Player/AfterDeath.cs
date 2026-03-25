using UnityEngine;

public class AfterDeath : MonoBehaviour
{
    [SerializeField] private PlayerStats playerStats;
    [SerializeField] private GameObject tombstonePrefab;
    [SerializeField] private Sprite tombstoneSprite;
    [SerializeField] private Vector3 tombstoneOffset;
    [SerializeField] private Color tombstoneGizmoColor = new Color(0.7f, 0.9f, 1f, 1f);
    [SerializeField] private float tombstoneGizmoRadius = 0.2f;

    private bool hasHandledDeath;

    private void Awake()
    {
        if (playerStats == null)
        {
            playerStats = GetComponent<PlayerStats>();
        }
    }

    private void OnEnable()
    {
        if (playerStats != null)
        {
            playerStats.OnDeath += HandlePlayerDeath;
        }
    }

    private void OnDisable()
    {
        if (playerStats != null)
        {
            playerStats.OnDeath -= HandlePlayerDeath;
        }
    }

    private void HandlePlayerDeath()
    {
        if (hasHandledDeath)
        {
            return;
        }

        hasHandledDeath = true;
        SpawnTombstone();
        Destroy(gameObject);
    }

    private void SpawnTombstone()
    {
        Vector3 spawnPosition = GetTombstoneSpawnPosition();

        if (tombstonePrefab != null)
        {
            Instantiate(tombstonePrefab, spawnPosition, transform.rotation);
            return;
        }

        if (tombstoneSprite == null)
        {
            Debug.LogWarning("AfterDeath could not spawn a tombstone because no tombstone prefab or sprite is assigned.", this);
            return;
        }

        GameObject tombstone = new GameObject("Tombstone");
        tombstone.transform.SetPositionAndRotation(spawnPosition, Quaternion.identity);
        tombstone.transform.localScale = transform.localScale;

        SpriteRenderer tombstoneRenderer = tombstone.AddComponent<SpriteRenderer>();
        tombstoneRenderer.sprite = tombstoneSprite;

        SpriteRenderer playerSpriteRenderer = GetComponent<SpriteRenderer>();
        if (playerSpriteRenderer != null)
        {
            tombstoneRenderer.sortingLayerID = playerSpriteRenderer.sortingLayerID;
            tombstoneRenderer.sortingOrder = playerSpriteRenderer.sortingOrder;
            tombstoneRenderer.color = playerSpriteRenderer.color;
        }
    }

    private Vector3 GetTombstoneSpawnPosition()
    {
        return transform.position + tombstoneOffset;
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 spawnPosition = GetTombstoneSpawnPosition();

        Gizmos.color = tombstoneGizmoColor;
        Gizmos.DrawLine(transform.position, spawnPosition);
        Gizmos.DrawWireSphere(spawnPosition, tombstoneGizmoRadius);

        float crossSize = tombstoneGizmoRadius * 0.7f;
        Gizmos.DrawLine(spawnPosition + Vector3.left * crossSize, spawnPosition + Vector3.right * crossSize);
        Gizmos.DrawLine(spawnPosition + Vector3.up * crossSize, spawnPosition + Vector3.down * crossSize);
    }
}
