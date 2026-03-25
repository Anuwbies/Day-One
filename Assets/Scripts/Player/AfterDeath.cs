using UnityEngine;

public class AfterDeath : MonoBehaviour
{
    [SerializeField] private PlayerStats playerStats;
    [SerializeField] private GameObject tombstonePrefab;
    [SerializeField] private Sprite tombstoneSprite;
    [SerializeField] private Vector3 tombstoneOffset;
    [SerializeField] private float tombstoneFallHeight = 2.5f;
    [SerializeField] private float tombstoneFallDuration = 0.35f;
    [SerializeField] private float canvasEnableDelay = 1f;
    [SerializeField] private float canvasPanelScaleDuration = 0.2f;
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
        gameObject.SetActive(false);
    }

    private void SpawnTombstone()
    {
        Vector3 landingPosition = GetTombstoneSpawnPosition();
        GameObject tombstone;

        if (tombstonePrefab != null)
        {
            tombstone = Instantiate(tombstonePrefab, landingPosition, transform.rotation);
            StartTombstoneFall(tombstone, landingPosition);
            return;
        }

        if (tombstoneSprite == null)
        {
            Debug.LogWarning("AfterDeath could not spawn a tombstone because no tombstone prefab or sprite is assigned.", this);
            return;
        }

        tombstone = new GameObject("Tombstone");
        tombstone.transform.SetPositionAndRotation(landingPosition, Quaternion.identity);
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

        StartTombstoneFall(tombstone, landingPosition);
    }

    private Vector3 GetTombstoneSpawnPosition()
    {
        return transform.position + tombstoneOffset;
    }

    private Vector3 GetTombstoneDropStartPosition(Vector3 landingPosition)
    {
        return landingPosition + Vector3.up * Mathf.Max(0f, tombstoneFallHeight);
    }

    private void StartTombstoneFall(GameObject tombstone, Vector3 landingPosition)
    {
        TombstoneFallAnimator fallAnimator = tombstone.GetComponent<TombstoneFallAnimator>();
        if (fallAnimator == null)
        {
            fallAnimator = tombstone.AddComponent<TombstoneFallAnimator>();
        }

        fallAnimator.BeginFall(
            landingPosition,
            tombstoneFallHeight,
            tombstoneFallDuration,
            canvasEnableDelay,
            canvasPanelScaleDuration
        );
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 landingPosition = GetTombstoneSpawnPosition();
        Vector3 dropStartPosition = GetTombstoneDropStartPosition(landingPosition);

        Gizmos.color = tombstoneGizmoColor;
        Gizmos.DrawLine(transform.position, landingPosition);
        Gizmos.DrawLine(dropStartPosition, landingPosition);
        Gizmos.DrawWireSphere(landingPosition, tombstoneGizmoRadius);
        Gizmos.DrawWireCube(dropStartPosition, Vector3.one * tombstoneGizmoRadius);

        float crossSize = tombstoneGizmoRadius * 0.7f;
        Gizmos.DrawLine(landingPosition + Vector3.left * crossSize, landingPosition + Vector3.right * crossSize);
        Gizmos.DrawLine(landingPosition + Vector3.up * crossSize, landingPosition + Vector3.down * crossSize);
    }
}
