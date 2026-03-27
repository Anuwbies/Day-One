using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class WolfSpawner : MonoBehaviour
{
    private struct SpawnCell
    {
        public SpawnCell(Tilemap spawnTilemap, Vector3Int cellPosition, bool isEmptySpaceCell)
        {
            SpawnTilemap = spawnTilemap;
            CellPosition = cellPosition;
            IsEmptySpaceCell = isEmptySpaceCell;
        }

        public Tilemap SpawnTilemap { get; }
        public Vector3Int CellPosition { get; }
        public bool IsEmptySpaceCell { get; }
    }

    [Header("References")]
    [SerializeField] private Tilemap wolfTilemap;
    [SerializeField] private GameObject wolfPrefab;
    [SerializeField] private Transform generatedWolvesParent;

    [Header("Spawn Area Settings")]
    [SerializeField] private bool spawnOnlyInEmptySpace = false;
    [SerializeField] private bool includeEmptySpaceGenerators = false;
    [SerializeField] private List<EmptySpaceGenerator> emptySpaceGenerators = new List<EmptySpaceGenerator>();

    [Header("Generation Settings")]
    [Min(1)]
    [SerializeField] private int maxWolfCount = 8;

    [Min(1)]
    [SerializeField] private int maxPlacementAttemptsPerWolf = 4;

    [Min(0f)]
    [SerializeField] private float minRandomDist = 0.5f;

    [Min(0f)]
    [SerializeField] private float maxRandomDist = 2f;

    [Min(0f)]
    [SerializeField] private float edgeTileMargin = 0f;

    [Header("Respawn Settings")]
    [Min(0f)]
    [SerializeField] private float minRespawnTimeMinutes = 1f;

    [Min(0f)]
    [SerializeField] private float maxRespawnTimeMinutes = 3f;

    [Min(1)]
    [SerializeField] private int minRespawnCountAtATime = 1;

    [Min(1)]
    [SerializeField] private int maxRespawnCountAtATime = 1;

    [Header("Obstacle Settings")]
    [SerializeField] private LayerMask obstacleLayer = -1;

    [SerializeField] private bool generateOnStart = true;
    [SerializeField] private bool clearBeforeGenerate = true;

    private readonly List<Vector3> generatedPositions = new List<Vector3>();
    private readonly List<Collider2D> overlapResults = new List<Collider2D>();
    private Collider2D cachedWolfChildCollider;
    private Coroutine refreshTrackedWolfCountCoroutine;
    private Coroutine respawnWolfCoroutine;
    private int lastTrackedWolfCount;
    private bool suppressRespawnTracking;

    private void Start()
    {
        if (generateOnStart)
        {
            GenerateWolves();
        }

        RefreshTrackedWolfCountImmediate();
    }

    private void Update()
    {
        if (!Application.isPlaying || suppressRespawnTracking)
        {
            return;
        }

        QueueRespawnsForDestroyedWolves();
    }

    private void OnDisable()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        StopAllCoroutines();
        refreshTrackedWolfCountCoroutine = null;
        respawnWolfCoroutine = null;
        suppressRespawnTracking = false;
    }

    private void OnDestroy()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        StopAllCoroutines();
        refreshTrackedWolfCountCoroutine = null;
        respawnWolfCoroutine = null;
    }

    private void OnValidate()
    {
        cachedWolfChildCollider = null;
        maxWolfCount = Mathf.Max(1, maxWolfCount);
        maxPlacementAttemptsPerWolf = Mathf.Max(1, maxPlacementAttemptsPerWolf);
        minRandomDist = Mathf.Max(0f, minRandomDist);
        maxRandomDist = Mathf.Max(minRandomDist, maxRandomDist);
        edgeTileMargin = Mathf.Max(0f, edgeTileMargin);
        minRespawnTimeMinutes = Mathf.Max(0f, minRespawnTimeMinutes);
        maxRespawnTimeMinutes = Mathf.Max(minRespawnTimeMinutes, maxRespawnTimeMinutes);
        minRespawnCountAtATime = Mathf.Max(1, minRespawnCountAtATime);
        maxRespawnCountAtATime = Mathf.Max(minRespawnCountAtATime, maxRespawnCountAtATime);
    }

    [ContextMenu("Generate Wolves")]
    public void GenerateWolves()
    {
        if (!spawnOnlyInEmptySpace && wolfTilemap == null)
        {
            Debug.LogWarning($"No wolf tilemap assigned for {name}.");
            RefreshTrackedWolfCountImmediate();
            return;
        }

        if (wolfPrefab == null)
        {
            Debug.LogWarning($"No wolf prefab assigned for {name}.");
            RefreshTrackedWolfCountImmediate();
            return;
        }

        InitializeObstacleLayer();

        List<SpawnCell> spawnCells = CollectSpawnCells();
        if (spawnCells.Count == 0)
        {
            Debug.LogWarning($"No valid wolf spawn cells found for {name}.");
            RefreshTrackedWolfCountImmediate();
            return;
        }

        ShuffleSpawnCells(spawnCells);

        Transform spawnParent = GetOrCreateGeneratedWolvesParent();
        if (clearBeforeGenerate)
        {
            ClearGeneratedWolves();
        }
        else
        {
            RebuildGeneratedPositions(spawnParent);
        }

        int remainingWolfCount = Mathf.Max(0, maxWolfCount - generatedPositions.Count);
        if (remainingWolfCount <= 0)
        {
            RefreshTrackedWolfCountImmediate();
            return;
        }

        for (int cellIndex = 0; cellIndex < spawnCells.Count && remainingWolfCount > 0; cellIndex++)
        {
            if (TrySpawnWolfOnCell(spawnCells[cellIndex], spawnParent))
            {
                remainingWolfCount--;
            }
        }

        RefreshTrackedWolfCountDeferred();
    }

    [ContextMenu("Clear Generated Wolves")]
    public void ClearGeneratedWolves()
    {
        if (Application.isPlaying)
        {
            StopAllCoroutines();
            refreshTrackedWolfCountCoroutine = null;
            respawnWolfCoroutine = null;
            suppressRespawnTracking = false;
        }

        Transform spawnParent = GetOrCreateGeneratedWolvesParent();

        for (int i = spawnParent.childCount - 1; i >= 0; i--)
        {
            Transform child = spawnParent.GetChild(i);
            if (Application.isPlaying)
            {
                Destroy(child.gameObject);
            }
            else
            {
                DestroyImmediate(child.gameObject);
            }
        }

        generatedPositions.Clear();
        RefreshTrackedWolfCountDeferred();
    }

    private void QueueRespawnsForDestroyedWolves()
    {
        Transform spawnParent = GetOrCreateGeneratedWolvesParent();
        int currentWolfCount = GetCurrentWolfCount(spawnParent);

        if (currentWolfCount < lastTrackedWolfCount && respawnWolfCoroutine == null)
        {
            respawnWolfCoroutine = StartCoroutine(RespawnMissingWolvesInBatches());
        }

        lastTrackedWolfCount = currentWolfCount;
    }

    private IEnumerator RespawnMissingWolvesInBatches()
    {
        while (Application.isPlaying && isActiveAndEnabled)
        {
            float respawnDelaySeconds = GetRandomRespawnDelaySeconds();
            if (respawnDelaySeconds > 0f)
            {
                yield return new WaitForSeconds(respawnDelaySeconds);
            }

            Transform spawnParent = GetOrCreateGeneratedWolvesParent();
            int currentWolfCount = GetCurrentWolfCount(spawnParent);
            int missingWolfCount = Mathf.Max(0, maxWolfCount - currentWolfCount);
            if (missingWolfCount <= 0)
            {
                RefreshTrackedWolfCountImmediate();
                respawnWolfCoroutine = null;
                yield break;
            }

            int respawnBatchCount = Mathf.Min(missingWolfCount, GetScaledRespawnBatchCount(currentWolfCount));
            TryRespawnWolfBatch(spawnParent, respawnBatchCount);
            RefreshTrackedWolfCountImmediate();
        }

        respawnWolfCoroutine = null;
    }

    private int TryRespawnWolfBatch(Transform spawnParent, int respawnCount)
    {
        if (respawnCount <= 0 || wolfPrefab == null)
        {
            return 0;
        }

        if (!spawnOnlyInEmptySpace && wolfTilemap == null)
        {
            return 0;
        }

        InitializeObstacleLayer();

        List<SpawnCell> spawnCells = CollectSpawnCells();
        if (spawnCells.Count == 0)
        {
            return 0;
        }

        ShuffleSpawnCells(spawnCells);
        RebuildGeneratedPositions(spawnParent);

        int remainingRespawnCount = Mathf.Min(respawnCount, Mathf.Max(0, maxWolfCount - generatedPositions.Count));
        if (remainingRespawnCount <= 0)
        {
            return 0;
        }

        int spawnedWolfCount = 0;
        for (int cellIndex = 0; cellIndex < spawnCells.Count && remainingRespawnCount > 0; cellIndex++)
        {
            if (TrySpawnWolfOnCell(spawnCells[cellIndex], spawnParent))
            {
                spawnedWolfCount++;
                remainingRespawnCount--;
            }
        }

        return spawnedWolfCount;
    }

    private float GetRandomRespawnDelaySeconds()
    {
        return Random.Range(minRespawnTimeMinutes, maxRespawnTimeMinutes) * 60f;
    }

    private int GetScaledRespawnBatchCount(int currentWolfCount)
    {
        if (maxRespawnCountAtATime <= minRespawnCountAtATime)
        {
            return minRespawnCountAtATime;
        }

        float aliveRatio = maxWolfCount <= 0
            ? 1f
            : Mathf.Clamp01((float)currentWolfCount / maxWolfCount);

        float scaledBatchCount = Mathf.Lerp(maxRespawnCountAtATime, minRespawnCountAtATime, aliveRatio);
        int minBatchCount = Mathf.Clamp(Mathf.FloorToInt(scaledBatchCount), minRespawnCountAtATime, maxRespawnCountAtATime);
        int maxBatchCount = Mathf.Clamp(Mathf.CeilToInt(scaledBatchCount), minRespawnCountAtATime, maxRespawnCountAtATime);
        return Random.Range(minBatchCount, maxBatchCount + 1);
    }

    private void RefreshTrackedWolfCountImmediate()
    {
        lastTrackedWolfCount = GetCurrentWolfCount(GetOrCreateGeneratedWolvesParent());
    }

    private void RefreshTrackedWolfCountDeferred()
    {
        if (!Application.isPlaying)
        {
            RefreshTrackedWolfCountImmediate();
            return;
        }

        if (refreshTrackedWolfCountCoroutine != null)
        {
            StopCoroutine(refreshTrackedWolfCountCoroutine);
        }

        refreshTrackedWolfCountCoroutine = StartCoroutine(RefreshTrackedWolfCountNextFrame());
    }

    private IEnumerator RefreshTrackedWolfCountNextFrame()
    {
        suppressRespawnTracking = true;
        yield return null;

        refreshTrackedWolfCountCoroutine = null;
        lastTrackedWolfCount = GetCurrentWolfCount(GetOrCreateGeneratedWolvesParent());
        suppressRespawnTracking = false;
    }

    private bool TrySpawnWolfOnCell(SpawnCell spawnCell, Transform spawnParent)
    {
        for (int attempt = 0; attempt < maxPlacementAttemptsPerWolf; attempt++)
        {
            Vector3 pivotPosition = GetRandomPivotPositionInCell(spawnCell);
            Vector3 spawnPosition = GetWolfRootPositionFromPivot(pivotPosition);

            if (!IsOnSelectedSpawnArea(spawnPosition, spawnCell))
            {
                continue;
            }

            if (!IsFarEnoughFromExistingWolves(pivotPosition))
            {
                continue;
            }

            if (!IsAreaFreeFromObstacles(spawnPosition))
            {
                continue;
            }

            GameObject wolfInstance = Instantiate(wolfPrefab, spawnPosition, Quaternion.identity, spawnParent);
            wolfInstance.name = wolfPrefab.name;
            generatedPositions.Add(GetWolfPivotWorldPosition(wolfInstance.transform));
            return true;
        }

        return false;
    }

    private List<SpawnCell> CollectSpawnCells()
    {
        List<SpawnCell> spawnCells = new List<SpawnCell>();
        Dictionary<string, int> cellIndices = new Dictionary<string, int>();

        if (!spawnOnlyInEmptySpace && wolfTilemap != null)
        {
            BoundsInt bounds = wolfTilemap.cellBounds;
            foreach (Vector3Int cellPosition in bounds.allPositionsWithin)
            {
                if (!wolfTilemap.HasTile(cellPosition))
                {
                    continue;
                }

                AddSpawnCell(wolfTilemap, cellPosition, false, spawnCells, cellIndices);
            }
        }

        if ((!includeEmptySpaceGenerators && !spawnOnlyInEmptySpace) || emptySpaceGenerators == null)
        {
            return spawnCells;
        }

        for (int generatorIndex = 0; generatorIndex < emptySpaceGenerators.Count; generatorIndex++)
        {
            EmptySpaceGenerator emptySpaceGenerator = emptySpaceGenerators[generatorIndex];
            if (emptySpaceGenerator == null || emptySpaceGenerator.TargetTilemap == null)
            {
                continue;
            }

            IReadOnlyList<Vector3Int> generatedCells = emptySpaceGenerator.GeneratedCells;
            for (int cellIndex = 0; cellIndex < generatedCells.Count; cellIndex++)
            {
                AddSpawnCell(
                    emptySpaceGenerator.TargetTilemap,
                    generatedCells[cellIndex],
                    true,
                    spawnCells,
                    cellIndices
                );
            }
        }

        return spawnCells;
    }

    private void AddSpawnCell(
        Tilemap spawnTilemap,
        Vector3Int cellPosition,
        bool isEmptySpaceCell,
        List<SpawnCell> spawnCells,
        Dictionary<string, int> cellIndices
    )
    {
        if (spawnTilemap == null)
        {
            return;
        }

        GridLayout gridLayout = spawnTilemap.layoutGrid;
        int gridId = gridLayout != null ? gridLayout.GetInstanceID() : spawnTilemap.GetInstanceID();
        string key = $"{gridId}:{cellPosition.x}:{cellPosition.y}:{cellPosition.z}";

        if (cellIndices.TryGetValue(key, out int existingIndex))
        {
            if (isEmptySpaceCell && !spawnCells[existingIndex].IsEmptySpaceCell)
            {
                spawnCells[existingIndex] = new SpawnCell(spawnTilemap, cellPosition, true);
            }

            return;
        }

        cellIndices.Add(key, spawnCells.Count);
        spawnCells.Add(new SpawnCell(spawnTilemap, cellPosition, isEmptySpaceCell));
    }

    private bool IsOnSelectedSpawnArea(Vector3 wolfRootPosition, SpawnCell spawnCell)
    {
        if (spawnCell.SpawnTilemap == null)
        {
            return false;
        }

        if (spawnCell.IsEmptySpaceCell)
        {
            return IsInsideEmptySpace(wolfRootPosition, spawnCell.SpawnTilemap);
        }

        if (!IsOnSelectedTilemap(wolfRootPosition, spawnCell.SpawnTilemap))
        {
            return false;
        }

        if (!includeEmptySpaceGenerators && IsOverlappingAnyEmptySpace(wolfRootPosition))
        {
            return false;
        }

        return true;
    }

    private bool IsInsideEmptySpace(Vector3 wolfRootPosition, Tilemap sourceTilemap)
    {
        if (sourceTilemap == null)
        {
            return false;
        }

        if (!TryGetWolfColliderBounds(wolfRootPosition, out Bounds colliderBounds))
        {
            return IsInAnyEmptySpace(wolfRootPosition);
        }

        return AreBoundsCoveredByEmptySpaces(colliderBounds, sourceTilemap);
    }

    private bool IsOnSelectedTilemap(Vector3 wolfRootPosition, Tilemap sourceTilemap)
    {
        if (sourceTilemap == null)
        {
            return false;
        }

        if (!TryGetWolfColliderBounds(wolfRootPosition, out Bounds colliderBounds))
        {
            Vector3Int cellPosition = sourceTilemap.WorldToCell(wolfRootPosition);
            return sourceTilemap.HasTile(cellPosition);
        }

        return AreBoundsCoveredByTilemap(sourceTilemap, colliderBounds);
    }

    private bool IsInAnyEmptySpace(Vector3 worldPosition)
    {
        if (emptySpaceGenerators == null || emptySpaceGenerators.Count == 0)
        {
            return false;
        }

        for (int i = 0; i < emptySpaceGenerators.Count; i++)
        {
            if (emptySpaceGenerators[i] != null && emptySpaceGenerators[i].ContainsWorldPoint(worldPosition))
            {
                return true;
            }
        }

        return false;
    }

    private bool IsOverlappingAnyEmptySpace(Vector3 wolfRootPosition)
    {
        if (emptySpaceGenerators == null || emptySpaceGenerators.Count == 0)
        {
            return false;
        }

        if (!TryGetWolfColliderBounds(wolfRootPosition, out Bounds colliderBounds))
        {
            return IsInAnyEmptySpace(wolfRootPosition);
        }

        for (int i = 0; i < emptySpaceGenerators.Count; i++)
        {
            EmptySpaceGenerator emptySpaceGenerator = emptySpaceGenerators[i];
            if (emptySpaceGenerator == null || emptySpaceGenerator.TargetTilemap == null)
            {
                continue;
            }

            if (DoesBoundsOverlapEmptySpace(colliderBounds, emptySpaceGenerator))
            {
                return true;
            }
        }

        return false;
    }

    private bool DoesBoundsOverlapEmptySpace(Bounds colliderBounds, EmptySpaceGenerator emptySpaceGenerator)
    {
        const float edgeInset = 0.001f;
        Tilemap sourceTilemap = emptySpaceGenerator.TargetTilemap;
        Vector3 cellSize = sourceTilemap.layoutGrid.cellSize;
        Vector3 expandedSize = new Vector3(
            Mathf.Abs(cellSize.x) * edgeTileMargin * 2f,
            Mathf.Abs(cellSize.y) * edgeTileMargin * 2f,
            0f
        );

        colliderBounds.Expand(expandedSize);

        Vector3 minPoint = new Vector3(
            colliderBounds.min.x + edgeInset,
            colliderBounds.min.y + edgeInset,
            colliderBounds.center.z
        );

        Vector3 maxPoint = new Vector3(
            colliderBounds.max.x - edgeInset,
            colliderBounds.max.y - edgeInset,
            colliderBounds.center.z
        );

        Vector3Int minCell = sourceTilemap.WorldToCell(minPoint);
        Vector3Int maxCell = sourceTilemap.WorldToCell(maxPoint);

        for (int x = minCell.x; x <= maxCell.x; x++)
        {
            for (int y = minCell.y; y <= maxCell.y; y++)
            {
                Vector3 worldPoint = sourceTilemap.GetCellCenterWorld(new Vector3Int(x, y, 0));
                if (emptySpaceGenerator.ContainsWorldPoint(worldPoint))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private bool AreBoundsCoveredByEmptySpaces(Bounds colliderBounds, Tilemap sourceTilemap)
    {
        const float edgeInset = 0.001f;
        Vector3 cellSize = sourceTilemap.layoutGrid.cellSize;
        Vector3 expandedSize = new Vector3(
            Mathf.Abs(cellSize.x) * edgeTileMargin * 2f,
            Mathf.Abs(cellSize.y) * edgeTileMargin * 2f,
            0f
        );

        colliderBounds.Expand(expandedSize);

        Vector3 minPoint = new Vector3(
            colliderBounds.min.x + edgeInset,
            colliderBounds.min.y + edgeInset,
            colliderBounds.center.z
        );

        Vector3 maxPoint = new Vector3(
            colliderBounds.max.x - edgeInset,
            colliderBounds.max.y - edgeInset,
            colliderBounds.center.z
        );

        Vector3Int minCell = sourceTilemap.WorldToCell(minPoint);
        Vector3Int maxCell = sourceTilemap.WorldToCell(maxPoint);

        for (int x = minCell.x; x <= maxCell.x; x++)
        {
            for (int y = minCell.y; y <= maxCell.y; y++)
            {
                Vector3 worldPoint = sourceTilemap.GetCellCenterWorld(new Vector3Int(x, y, 0));
                if (!IsInAnyEmptySpace(worldPoint))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private bool AreBoundsCoveredByTilemap(Tilemap sourceTilemap, Bounds colliderBounds)
    {
        const float edgeInset = 0.001f;
        Vector3 cellSize = sourceTilemap.layoutGrid.cellSize;
        Vector3 expandedSize = new Vector3(
            Mathf.Abs(cellSize.x) * edgeTileMargin * 2f,
            Mathf.Abs(cellSize.y) * edgeTileMargin * 2f,
            0f
        );

        colliderBounds.Expand(expandedSize);

        Vector3 minPoint = new Vector3(
            colliderBounds.min.x + edgeInset,
            colliderBounds.min.y + edgeInset,
            colliderBounds.center.z
        );

        Vector3 maxPoint = new Vector3(
            colliderBounds.max.x - edgeInset,
            colliderBounds.max.y - edgeInset,
            colliderBounds.center.z
        );

        Vector3Int minCell = sourceTilemap.WorldToCell(minPoint);
        Vector3Int maxCell = sourceTilemap.WorldToCell(maxPoint);

        for (int x = minCell.x; x <= maxCell.x; x++)
        {
            for (int y = minCell.y; y <= maxCell.y; y++)
            {
                if (!sourceTilemap.HasTile(new Vector3Int(x, y, 0)))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private Vector3 GetRandomPivotPositionInCell(SpawnCell spawnCell)
    {
        Tilemap sourceTilemap = spawnCell.SpawnTilemap;
        Vector3 cellOrigin = sourceTilemap.CellToWorld(spawnCell.CellPosition);
        Vector3 cellSize = sourceTilemap.layoutGrid.cellSize;

        return new Vector3(
            cellOrigin.x + Random.Range(0.15f, 0.85f) * cellSize.x,
            cellOrigin.y + Random.Range(0.15f, 0.85f) * cellSize.y,
            0f
        );
    }

    private bool IsFarEnoughFromExistingWolves(Vector3 candidatePosition)
    {
        if (generatedPositions.Count == 0)
        {
            return true;
        }

        float requiredDistance = maxRandomDist <= 0f
            ? 0f
            : Random.Range(minRandomDist, maxRandomDist);

        for (int i = 0; i < generatedPositions.Count; i++)
        {
            if (Vector2.Distance(candidatePosition, generatedPositions[i]) < requiredDistance)
            {
                return false;
            }
        }

        return true;
    }

    private bool IsAreaFreeFromObstacles(Vector3 wolfRootPosition)
    {
        ContactFilter2D filter = new ContactFilter2D
        {
            useTriggers = true
        };
        filter.SetLayerMask(obstacleLayer);

        overlapResults.Clear();
        Physics2D.SyncTransforms();
        GetPlacementOverlaps(wolfRootPosition, filter, overlapResults);

        for (int i = 0; i < overlapResults.Count; i++)
        {
            Collider2D hit = overlapResults[i];
            if (hit != null && hit.CompareTag("Obstacle"))
            {
                return false;
            }
        }

        return true;
    }

    private void RebuildGeneratedPositions(Transform spawnParent)
    {
        generatedPositions.Clear();

        for (int i = 0; i < spawnParent.childCount; i++)
        {
            generatedPositions.Add(GetWolfPivotWorldPosition(spawnParent.GetChild(i)));
        }
    }

    private int GetCurrentWolfCount(Transform spawnParent)
    {
        return spawnParent != null ? spawnParent.childCount : 0;
    }

    private Vector3 GetWolfRootPositionFromPivot(Vector3 pivotPosition)
    {
        Vector3 pivotOffset = GetWolfPivotLocalOffset();
        return new Vector3(
            pivotPosition.x - pivotOffset.x,
            pivotPosition.y - pivotOffset.y,
            wolfPrefab.transform.position.z
        );
    }

    private Vector3 GetWolfPivotWorldPosition(Transform wolfTransform)
    {
        if (wolfTransform == null)
        {
            return Vector3.zero;
        }

        Collider2D childCollider = GetChildCollider(wolfTransform.gameObject);
        if (childCollider == null)
        {
            return wolfTransform.position;
        }

        return childCollider.transform.TransformPoint(childCollider.offset);
    }

    private Vector3 GetWolfPivotLocalOffset()
    {
        Collider2D childCollider = GetWolfPrefabChildCollider();
        if (childCollider == null)
        {
            return Vector3.zero;
        }

        return wolfPrefab.transform.InverseTransformPoint(
            childCollider.transform.TransformPoint(childCollider.offset)
        );
    }

    private Collider2D GetWolfPrefabChildCollider()
    {
        if (cachedWolfChildCollider != null)
        {
            return cachedWolfChildCollider;
        }

        cachedWolfChildCollider = GetChildCollider(wolfPrefab);
        return cachedWolfChildCollider;
    }

    private int GetPlacementOverlaps(Vector3 wolfRootPosition, ContactFilter2D filter, List<Collider2D> results)
    {
        Collider2D childCollider = GetWolfPrefabChildCollider();
        if (childCollider == null)
        {
            return Physics2D.OverlapBox((Vector2)wolfRootPosition, Vector2.one * 0.1f, 0f, filter, results);
        }

        if (childCollider is CircleCollider2D circleCollider)
        {
            Vector2 center = GetWolfChildColliderWorldCenter(circleCollider, wolfRootPosition);
            Vector3 scale = circleCollider.transform.lossyScale;
            float radius = circleCollider.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y));
            return Physics2D.OverlapCircle(center, radius, filter, results);
        }

        if (childCollider is BoxCollider2D boxCollider)
        {
            Vector2 center = GetWolfChildColliderWorldCenter(boxCollider, wolfRootPosition);
            Vector2 size = GetScaledColliderSize(boxCollider.size, boxCollider.transform);
            float angle = boxCollider.transform.eulerAngles.z;
            return Physics2D.OverlapBox(center, size, angle, filter, results);
        }

        if (TryGetWolfColliderBounds(wolfRootPosition, out Bounds colliderBounds))
        {
            return Physics2D.OverlapBox(
                (Vector2)colliderBounds.center,
                (Vector2)colliderBounds.size,
                0f,
                filter,
                results
            );
        }

        return 0;
    }

    private Vector2 GetWolfChildColliderWorldCenter(Collider2D childCollider, Vector3 wolfRootPosition)
    {
        Vector3 rootOffset = wolfRootPosition - wolfPrefab.transform.position;
        Vector3 worldCenter = childCollider.transform.TransformPoint(childCollider.offset) + rootOffset;
        return new Vector2(worldCenter.x, worldCenter.y);
    }

    private Vector2 GetScaledColliderSize(Vector2 localSize, Transform targetTransform)
    {
        Vector3 scale = targetTransform.lossyScale;
        return new Vector2(
            Mathf.Abs(localSize.x * scale.x),
            Mathf.Abs(localSize.y * scale.y)
        );
    }

    private bool TryGetWolfColliderBounds(Vector3 wolfRootPosition, out Bounds colliderBounds)
    {
        Collider2D childCollider = GetWolfPrefabChildCollider();
        if (childCollider == null)
        {
            colliderBounds = default;
            return false;
        }

        colliderBounds = childCollider.bounds;
        colliderBounds.center += wolfRootPosition - wolfPrefab.transform.position;
        return colliderBounds.size.sqrMagnitude > 0f;
    }

    private Collider2D GetChildCollider(GameObject target)
    {
        if (target == null)
        {
            return null;
        }

        Collider2D[] colliders = target.GetComponentsInChildren<Collider2D>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider2D collider = colliders[i];
            if (collider != null && collider.transform != target.transform)
            {
                return collider;
            }
        }

        return target.GetComponent<Collider2D>();
    }

    private void ShuffleSpawnCells(List<SpawnCell> spawnCells)
    {
        for (int i = spawnCells.Count - 1; i > 0; i--)
        {
            int swapIndex = Random.Range(0, i + 1);
            SpawnCell currentCell = spawnCells[i];
            spawnCells[i] = spawnCells[swapIndex];
            spawnCells[swapIndex] = currentCell;
        }
    }

    private void InitializeObstacleLayer()
    {
        int obstacleLayerValue = obstacleLayer.value;
        if (obstacleLayerValue == -1 || obstacleLayerValue == 0)
        {
            obstacleLayer = ~(1 << 2);
        }
    }

    private Transform GetOrCreateGeneratedWolvesParent()
    {
        if (generatedWolvesParent != null)
        {
            return generatedWolvesParent;
        }

        Transform existingChild = transform.Find("Generated Wolves");
        if (existingChild != null)
        {
            generatedWolvesParent = existingChild;
            return generatedWolvesParent;
        }

        GameObject generatedParentObject = new GameObject("Generated Wolves");
        generatedParentObject.transform.SetParent(transform, false);
        generatedWolvesParent = generatedParentObject.transform;
        return generatedWolvesParent;
    }
}

