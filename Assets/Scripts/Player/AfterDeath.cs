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
    [SerializeField] private float baseDisableDelay = 0.5f;

    [Header("Gizmos")]
    [SerializeField] private bool showImpactGizmo = true;
    [SerializeField] private Color impactGizmoColor = Color.red;
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
        DisablePlayerImmediately();
        StartCoroutine(DisablePlayerAfterDelay());
    }

    private System.Collections.IEnumerator DisablePlayerAfterDelay()
    {
        yield return new WaitForSeconds(baseDisableDelay);
        gameObject.SetActive(false);
    }

    private void DisablePlayerImmediately()
    {
        DisablePlayerChildren();
        DisablePlayerRootComponents();
    }

    private void DisablePlayerChildren()
    {
        for (int i = 0; i < transform.childCount; i++)
        {
            transform.GetChild(i).gameObject.SetActive(false);
        }
    }

    private void DisablePlayerRootComponents()
    {
        MonoBehaviour[] rootBehaviours = GetComponents<MonoBehaviour>();
        for (int i = 0; i < rootBehaviours.Length; i++)
        {
            MonoBehaviour behaviour = rootBehaviours[i];
            if (behaviour == null || behaviour == this)
            {
                continue;
            }

            behaviour.enabled = false;
        }

        Animator animator = GetComponent<Animator>();
        if (animator != null)
        {
            animator.enabled = false;
        }

        Collider2D[] colliders = GetComponents<Collider2D>();
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null)
            {
                colliders[i].enabled = false;
            }
        }

        Rigidbody2D rigidbody2D = GetComponent<Rigidbody2D>();
        if (rigidbody2D != null)
        {
            rigidbody2D.linearVelocity = Vector2.zero;
            rigidbody2D.angularVelocity = 0f;
            rigidbody2D.simulated = false;
        }
    }

    private void SpawnTombstone()
    {
        Vector3 landingPosition = GetTombstoneSpawnPosition();
        GameObject tombstone;

        if (tombstonePrefab != null)
        {
            tombstone = Instantiate(tombstonePrefab, landingPosition, transform.rotation);
            ConfigureSpawnedTombstone(tombstone);
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

        YSorter tombstoneSorter = tombstone.AddComponent<YSorter>();
        tombstoneSorter.enableTransparency = false;
        tombstoneSorter.alwaysInFrontOfPlayer = true;

        SpriteRenderer playerSpriteRenderer = GetComponent<SpriteRenderer>();
        if (playerSpriteRenderer != null)
        {
            tombstoneRenderer.sortingLayerID = playerSpriteRenderer.sortingLayerID;
            tombstoneRenderer.sortingOrder = playerSpriteRenderer.sortingOrder + 1;
            tombstoneRenderer.color = playerSpriteRenderer.color;
        }

        StartTombstoneFall(tombstone, landingPosition);
    }

    private void ConfigureSpawnedTombstone(GameObject tombstone)
    {
        if (tombstone == null)
        {
            return;
        }

        SpriteRenderer playerSpriteRenderer = GetComponent<SpriteRenderer>();
        if (playerSpriteRenderer != null)
        {
            SpriteRenderer tombstoneRenderer = tombstone.GetComponent<SpriteRenderer>();
            if (tombstoneRenderer != null)
            {
                tombstoneRenderer.sortingLayerID = playerSpriteRenderer.sortingLayerID;
                tombstoneRenderer.sortingOrder = playerSpriteRenderer.sortingOrder + 1;
            }
        }

        YSorter tombstoneSorter = tombstone.GetComponent<YSorter>();
        if (tombstoneSorter != null)
        {
            tombstoneSorter.alwaysBehindPlayer = false;
            tombstoneSorter.alwaysInFrontOfPlayer = true;
        }
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

    private void OnDrawGizmos()
    {
        if (!showImpactGizmo || Application.isPlaying && hasHandledDeath)
        {
            return;
        }

        Vector3 landingPosition = GetTombstoneSpawnPosition();
        Gizmos.color = impactGizmoColor;
        
        // Draw a distinct "Crush Mark" / Landing Spot
        Gizmos.DrawWireSphere(landingPosition, tombstoneGizmoRadius * 1.5f);
        
        // Draw an X to mark the spot
        float crossSize = tombstoneGizmoRadius * 1.2f;
        Gizmos.DrawLine(landingPosition + new Vector3(-crossSize, 0, -crossSize), landingPosition + new Vector3(crossSize, 0, crossSize));
        Gizmos.DrawLine(landingPosition + new Vector3(crossSize, 0, -crossSize), landingPosition + new Vector3(-crossSize, 0, crossSize));
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
