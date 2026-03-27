using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class PlayerSpawner : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Tilemap playerSpawnTilemap;
    [SerializeField] private Transform playerTransform;

    [Header("Spawn Settings")]
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private bool relocateOnStart = true;
    [SerializeField] private Vector3 spawnOffset = Vector3.zero;
    [SerializeField] private bool preservePlayerZPosition = true;
    [SerializeField] private bool resetRigidbodyVelocity = true;

    private readonly List<Vector3Int> validSpawnCells = new List<Vector3Int>();

    private void Start()
    {
        if (relocateOnStart)
        {
            RelocatePlayer();
        }
    }

    [ContextMenu("Relocate Existing Player")]
    public void RelocatePlayer()
    {
        if (playerSpawnTilemap == null)
        {
            Debug.LogWarning($"No player spawn tilemap assigned for {name}.");
            return;
        }

        Transform targetPlayer = ResolvePlayerTransform();
        if (targetPlayer == null)
        {
            Debug.LogWarning($"No existing player object found for {name}.");
            return;
        }

        if (!TryGetRandomSpawnPosition(targetPlayer, out Vector3 spawnPosition))
        {
            Debug.LogWarning($"No valid player spawn tiles found for {name}.");
            return;
        }

        MovePlayerToSpawn(targetPlayer, spawnPosition + spawnOffset);
    }

    private Transform ResolvePlayerTransform()
    {
        Transform assignedPlayer = NormalizePlayerTransform(playerTransform);
        if (assignedPlayer != null && assignedPlayer.gameObject.scene.IsValid())
        {
            playerTransform = assignedPlayer;
            return playerTransform;
        }

        PlayerMovement[] playerMovements = Object.FindObjectsByType<PlayerMovement>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );

        Transform resolvedPlayer = FindPreferredPlayerTransform(playerMovements);
        if (resolvedPlayer != null)
        {
            playerTransform = resolvedPlayer;
            return playerTransform;
        }

        PlayerStats[] playerStatsComponents = Object.FindObjectsByType<PlayerStats>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );

        resolvedPlayer = FindPreferredPlayerTransform(playerStatsComponents);
        if (resolvedPlayer != null)
        {
            playerTransform = resolvedPlayer;
        }

        return playerTransform;
    }

    private Transform FindPreferredPlayerTransform<T>(T[] components) where T : Component
    {
        if (components == null || components.Length == 0)
        {
            return null;
        }

        Transform fallbackPlayer = null;

        for (int i = 0; i < components.Length; i++)
        {
            Transform candidate = NormalizePlayerTransform(components[i] != null ? components[i].transform : null);
            if (candidate == null)
            {
                continue;
            }

            if (MatchesPlayerTag(candidate.gameObject))
            {
                return candidate;
            }

            if (fallbackPlayer == null)
            {
                fallbackPlayer = candidate;
            }
        }

        return fallbackPlayer;
    }

    private Transform NormalizePlayerTransform(Transform candidate)
    {
        if (candidate == null)
        {
            return null;
        }

        Rigidbody2D rigidbody2D = candidate.GetComponentInParent<Rigidbody2D>();
        if (rigidbody2D != null)
        {
            return rigidbody2D.transform;
        }

        PlayerMovement playerMovement = candidate.GetComponentInParent<PlayerMovement>();
        if (playerMovement != null)
        {
            return playerMovement.transform;
        }

        PlayerStats playerStats = candidate.GetComponentInParent<PlayerStats>();
        if (playerStats != null)
        {
            return playerStats.transform;
        }

        return candidate.root;
    }

    private bool TryGetRandomSpawnPosition(Transform targetPlayer, out Vector3 spawnPosition)
    {
        validSpawnCells.Clear();

        BoundsInt bounds = playerSpawnTilemap.cellBounds;
        foreach (Vector3Int cellPosition in bounds.allPositionsWithin)
        {
            if (playerSpawnTilemap.HasTile(cellPosition))
            {
                validSpawnCells.Add(cellPosition);
            }
        }

        if (validSpawnCells.Count == 0)
        {
            spawnPosition = Vector3.zero;
            return false;
        }

        Vector3Int selectedCell = validSpawnCells[Random.Range(0, validSpawnCells.Count)];
        Vector3 cellCenter = playerSpawnTilemap.GetCellCenterWorld(selectedCell);
        float spawnZ = preservePlayerZPosition && targetPlayer != null
            ? targetPlayer.position.z
            : cellCenter.z;

        spawnPosition = new Vector3(cellCenter.x, cellCenter.y, spawnZ);
        return true;
    }

    private void MovePlayerToSpawn(Transform targetPlayer, Vector3 spawnPosition)
    {
        if (targetPlayer == null)
        {
            return;
        }

        Rigidbody2D playerRigidbody = targetPlayer.GetComponent<Rigidbody2D>();
        if (playerRigidbody != null)
        {
            if (resetRigidbodyVelocity)
            {
                playerRigidbody.linearVelocity = Vector2.zero;
                playerRigidbody.angularVelocity = 0f;
            }

            playerRigidbody.position = new Vector2(spawnPosition.x, spawnPosition.y);
        }

        Vector3 finalPosition = targetPlayer.position;
        finalPosition.x = spawnPosition.x;
        finalPosition.y = spawnPosition.y;
        finalPosition.z = spawnPosition.z;
        targetPlayer.position = finalPosition;

        PlayerMovement playerMovement = targetPlayer.GetComponent<PlayerMovement>();
        if (playerMovement != null)
        {
            playerMovement.movement = Vector2.zero;
        }

        Physics2D.SyncTransforms();
    }

    private bool MatchesPlayerTag(GameObject candidate)
    {
        if (candidate == null || string.IsNullOrWhiteSpace(playerTag))
        {
            return true;
        }

        try
        {
            return candidate.CompareTag(playerTag);
        }
        catch (UnityException)
        {
            return false;
        }
    }
}
