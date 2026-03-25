using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class WoodGenerator : MonoBehaviour
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

    [System.Serializable]
    private class WoodPrefabSpawnSettings
    {
        [SerializeField] private GameObject prefab;

        [Range(0f, 100f)]
        [SerializeField] private float tileSpawnChancePercent = 35f;

        [Range(0f, 100f)]
        [SerializeField] private float additionalAreaSpawnChancePercent = 20f;

        public GameObject Prefab => prefab;

        public float GetSpawnChance(bool isAdditionalSpawnArea)
        {
            return isAdditionalSpawnArea
                ? additionalAreaSpawnChancePercent
                : tileSpawnChancePercent;
        }

        public void ClampValues()
        {
            tileSpawnChancePercent = Mathf.Clamp(tileSpawnChancePercent, 0f, 100f);
            additionalAreaSpawnChancePercent = Mathf.Clamp(additionalAreaSpawnChancePercent, 0f, 100f);
        }
    }

    [Header("References")]
    [SerializeField] private Tilemap woodTilemap;
    [SerializeField] private Transform generatedWoodsParent;

    [Header("Wood Prefabs")]
    [SerializeField] private List<WoodPrefabSpawnSettings> woodPrefabSettings = new List<WoodPrefabSpawnSettings>();

    [Header("Generation Settings")]
    [Min(0f)]
    [SerializeField] private float minRandomDist = 1f;

    [Min(0f)]
    [SerializeField] private float maxRandomDist = 8f;

    [Min(1)]
    [SerializeField] private int woodsPerTile = 1;

    [Min(1)]
    [SerializeField] private int maxPlacementAttemptsPerWood = 4;

    [Min(0f)]
    [SerializeField] private float edgeTileMargin = 0f;

    [Header("Obstacle Settings")]
    [SerializeField] private LayerMask obstacleLayer = -1;

    [Header("Additional Spawn Areas")]
    [SerializeField] private List<EmptySpaceGenerator> emptySpaceGenerators;

    [SerializeField] private bool generateOnStart = true;
    [SerializeField] private bool clearBeforeGenerate = true;

    private readonly List<Vector3> generatedPositions = new List<Vector3>();
    private readonly List<Collider2D> overlapResults = new List<Collider2D>();
    private readonly Dictionary<GameObject, Collider2D> cachedWoodChildColliders = new Dictionary<GameObject, Collider2D>();
    private readonly List<WoodPrefabSpawnSettings> spawnablePrefabBuffer = new List<WoodPrefabSpawnSettings>();

    private void Start()
    {
        if (generateOnStart)
        {
            GenerateWoods();
        }
    }

    private void OnValidate()
    {
        cachedWoodChildColliders.Clear();
        minRandomDist = Mathf.Max(0f, minRandomDist);
        maxRandomDist = Mathf.Max(minRandomDist, maxRandomDist);
        woodsPerTile = Mathf.Max(1, woodsPerTile);
        maxPlacementAttemptsPerWood = Mathf.Max(1, maxPlacementAttemptsPerWood);
        edgeTileMargin = Mathf.Max(0f, edgeTileMargin);
        ClampWoodPrefabSettings();
    }

    [ContextMenu("Generate Woods")]
    public void GenerateWoods()
    {
        ClampWoodPrefabSettings();

        if (!HasValidWoodPrefab())
        {
            Debug.LogWarning($"No wood prefabs assigned for {name}.");
            return;
        }

        InitializeObstacleLayer();

        List<SpawnCell> spawnCells = CollectSpawnCells();
        if (spawnCells.Count == 0)
        {
            Debug.LogWarning($"No valid wood spawn cells found for {name}.");
            return;
        }

        Transform spawnParent = GetOrCreateGeneratedWoodsParent();

        if (clearBeforeGenerate)
        {
            ClearGeneratedWoods();
        }
        else
        {
            RebuildGeneratedPositions(spawnParent);
        }

        for (int cellIndex = 0; cellIndex < spawnCells.Count; cellIndex++)
        {
            if (!TryGetSpawnablePrefabsForTile(spawnCells[cellIndex], spawnablePrefabBuffer))
            {
                continue;
            }

            for (int spawnIndex = 0; spawnIndex < woodsPerTile; spawnIndex++)
            {
                TrySpawnWoodOnCell(spawnCells[cellIndex], spawnParent, spawnablePrefabBuffer);
            }
        }
    }

    [ContextMenu("Clear Generated Woods")]
    public void ClearGeneratedWoods()
    {
        Transform spawnParent = GetOrCreateGeneratedWoodsParent();

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
    }

    private void TrySpawnWoodOnCell(
        SpawnCell spawnCell,
        Transform spawnParent,
        List<WoodPrefabSpawnSettings> spawnablePrefabs
    )
    {
        for (int attempt = 0; attempt < maxPlacementAttemptsPerWood; attempt++)
        {
            if (!TryGetRandomWoodPrefab(spawnablePrefabs, out GameObject woodPrefab))
            {
                return;
            }

            Vector3 pivotPosition = GetRandomPivotPositionInCell(spawnCell);
            Vector3 spawnPosition = GetWoodRootPositionFromPivot(woodPrefab, pivotPosition);

            if (!IsOnSelectedSpawnArea(woodPrefab, spawnPosition, spawnCell))
            {
                continue;
            }

            if (!IsFarEnoughFromExistingWoods(pivotPosition))
            {
                continue;
            }

            if (!IsAreaFreeFromObstacles(woodPrefab, spawnPosition))
            {
                continue;
            }

            GameObject woodInstance = Instantiate(woodPrefab, spawnPosition, Quaternion.identity, spawnParent);
            woodInstance.name = woodPrefab.name;
            generatedPositions.Add(GetWoodPivotWorldPosition(woodInstance.transform));
            return;
        }
    }

    private List<SpawnCell> CollectSpawnCells()
    {
        List<SpawnCell> spawnCells = new List<SpawnCell>();
        Dictionary<string, int> cellIndices = new Dictionary<string, int>();

        if (woodTilemap != null)
        {
            BoundsInt bounds = woodTilemap.cellBounds;
            foreach (Vector3Int cellPosition in bounds.allPositionsWithin)
            {
                if (!woodTilemap.HasTile(cellPosition))
                {
                    continue;
                }

                AddSpawnCell(woodTilemap, cellPosition, false, spawnCells, cellIndices);
            }
        }

        if (emptySpaceGenerators == null)
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

    private bool IsOnSelectedSpawnArea(GameObject woodPrefab, Vector3 woodRootPosition, SpawnCell spawnCell)
    {
        if (spawnCell.SpawnTilemap == null)
        {
            return false;
        }

        if (spawnCell.IsEmptySpaceCell)
        {
            return IsInsideEmptySpace(woodPrefab, woodRootPosition, spawnCell.SpawnTilemap);
        }

        return IsOnSelectedTilemap(woodPrefab, woodRootPosition, spawnCell.SpawnTilemap);
    }

    private bool IsInsideEmptySpace(GameObject woodPrefab, Vector3 woodRootPosition, Tilemap sourceTilemap)
    {
        if (sourceTilemap == null)
        {
            return false;
        }

        if (!TryGetWoodColliderBounds(woodPrefab, woodRootPosition, out Bounds colliderBounds))
        {
            return IsInAnyEmptySpace(woodRootPosition);
        }

        return AreBoundsCoveredByEmptySpaces(colliderBounds, sourceTilemap);
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

    private bool IsOnSelectedTilemap(GameObject woodPrefab, Vector3 woodRootPosition, Tilemap sourceTilemap)
    {
        if (sourceTilemap == null)
        {
            return false;
        }

        if (!TryGetWoodColliderBounds(woodPrefab, woodRootPosition, out Bounds colliderBounds))
        {
            Vector3Int cellPosition = sourceTilemap.WorldToCell(woodRootPosition);
            return sourceTilemap.HasTile(cellPosition);
        }

        return AreBoundsCoveredByTilemap(sourceTilemap, colliderBounds);
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

    private bool IsFarEnoughFromExistingWoods(Vector3 candidatePosition)
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

    private bool IsAreaFreeFromObstacles(GameObject woodPrefab, Vector3 woodRootPosition)
    {
        ContactFilter2D filter = new ContactFilter2D
        {
            useTriggers = true
        };
        filter.SetLayerMask(obstacleLayer);

        overlapResults.Clear();
        Physics2D.SyncTransforms();
        GetPlacementOverlaps(woodPrefab, woodRootPosition, filter, overlapResults);

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
            generatedPositions.Add(GetWoodPivotWorldPosition(spawnParent.GetChild(i)));
        }
    }

    private Vector3 GetWoodRootPositionFromPivot(GameObject woodPrefab, Vector3 pivotPosition)
    {
        Vector3 pivotOffset = GetWoodPivotLocalOffset(woodPrefab);
        return new Vector3(
            pivotPosition.x - pivotOffset.x,
            pivotPosition.y - pivotOffset.y,
            woodPrefab.transform.position.z
        );
    }

    private Vector3 GetWoodPivotWorldPosition(Transform woodTransform)
    {
        if (woodTransform == null)
        {
            return Vector3.zero;
        }

        Collider2D childCollider = GetChildCollider(woodTransform.gameObject);
        if (childCollider == null)
        {
            return woodTransform.position;
        }

        return childCollider.transform.TransformPoint(childCollider.offset);
    }

    private Vector3 GetWoodPivotLocalOffset(GameObject woodPrefab)
    {
        Collider2D childCollider = GetWoodPrefabChildCollider(woodPrefab);
        if (childCollider == null)
        {
            return Vector3.zero;
        }

        return woodPrefab.transform.InverseTransformPoint(
            childCollider.transform.TransformPoint(childCollider.offset)
        );
    }

    private Collider2D GetWoodPrefabChildCollider(GameObject woodPrefab)
    {
        if (woodPrefab == null)
        {
            return null;
        }

        if (cachedWoodChildColliders.TryGetValue(woodPrefab, out Collider2D cachedCollider) && cachedCollider != null)
        {
            return cachedCollider;
        }

        Collider2D foundCollider = GetChildCollider(woodPrefab);
        cachedWoodChildColliders[woodPrefab] = foundCollider;
        return foundCollider;
    }

    private int GetPlacementOverlaps(
        GameObject woodPrefab,
        Vector3 woodRootPosition,
        ContactFilter2D filter,
        List<Collider2D> results
    )
    {
        Collider2D childCollider = GetWoodPrefabChildCollider(woodPrefab);
        if (childCollider == null)
        {
            return Physics2D.OverlapBox((Vector2)woodRootPosition, Vector2.one * 0.1f, 0f, filter, results);
        }

        if (childCollider is CircleCollider2D circleCollider)
        {
            Vector2 center = GetWoodChildColliderWorldCenter(woodPrefab, circleCollider, woodRootPosition);
            Vector3 scale = circleCollider.transform.lossyScale;
            float radius = circleCollider.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y));
            return Physics2D.OverlapCircle(center, radius, filter, results);
        }

        if (childCollider is BoxCollider2D boxCollider)
        {
            Vector2 center = GetWoodChildColliderWorldCenter(woodPrefab, boxCollider, woodRootPosition);
            Vector2 size = GetScaledColliderSize(boxCollider.size, boxCollider.transform);
            float angle = boxCollider.transform.eulerAngles.z;
            return Physics2D.OverlapBox(center, size, angle, filter, results);
        }

        if (TryGetWoodColliderBounds(woodPrefab, woodRootPosition, out Bounds colliderBounds))
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

    private Vector2 GetWoodChildColliderWorldCenter(
        GameObject woodPrefab,
        Collider2D childCollider,
        Vector3 woodRootPosition
    )
    {
        Vector3 rootOffset = woodRootPosition - woodPrefab.transform.position;
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

    private bool TryGetWoodColliderBounds(GameObject woodPrefab, Vector3 woodRootPosition, out Bounds colliderBounds)
    {
        Collider2D childCollider = GetWoodPrefabChildCollider(woodPrefab);
        if (childCollider == null)
        {
            colliderBounds = default;
            return false;
        }

        colliderBounds = childCollider.bounds;
        colliderBounds.center += woodRootPosition - woodPrefab.transform.position;
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

    private void ClampWoodPrefabSettings()
    {
        if (woodPrefabSettings == null)
        {
            woodPrefabSettings = new List<WoodPrefabSpawnSettings>();
        }

        for (int settingIndex = 0; settingIndex < woodPrefabSettings.Count; settingIndex++)
        {
            WoodPrefabSpawnSettings settings = woodPrefabSettings[settingIndex];
            if (settings != null)
            {
                settings.ClampValues();
            }
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

    private Transform GetOrCreateGeneratedWoodsParent()
    {
        if (generatedWoodsParent != null)
        {
            return generatedWoodsParent;
        }

        Transform existingChild = transform.Find("Generated Woods");
        if (existingChild != null)
        {
            generatedWoodsParent = existingChild;
            return generatedWoodsParent;
        }

        GameObject generatedParentObject = new GameObject("Generated Woods");
        generatedParentObject.transform.SetParent(transform, false);
        generatedWoodsParent = generatedParentObject.transform;
        return generatedWoodsParent;
    }

    private bool HasValidWoodPrefab()
    {
        if (woodPrefabSettings == null || woodPrefabSettings.Count == 0)
        {
            return false;
        }

        for (int i = 0; i < woodPrefabSettings.Count; i++)
        {
            WoodPrefabSpawnSettings settings = woodPrefabSettings[i];
            if (settings != null && settings.Prefab != null)
            {
                return true;
            }
        }

        return false;
    }

    private bool TryGetRandomWoodPrefab(
        List<WoodPrefabSpawnSettings> spawnablePrefabs,
        out GameObject woodPrefab
    )
    {
        woodPrefab = null;
        if (spawnablePrefabs == null || spawnablePrefabs.Count == 0)
        {
            return false;
        }

        int startIndex = Random.Range(0, spawnablePrefabs.Count);
        for (int offset = 0; offset < spawnablePrefabs.Count; offset++)
        {
            WoodPrefabSpawnSettings settings = spawnablePrefabs[(startIndex + offset) % spawnablePrefabs.Count];
            if (settings != null && settings.Prefab != null)
            {
                woodPrefab = settings.Prefab;
                return true;
            }
        }

        return false;
    }

    private bool TryGetSpawnablePrefabsForTile(
        SpawnCell spawnCell,
        List<WoodPrefabSpawnSettings> spawnablePrefabs
    )
    {
        spawnablePrefabs.Clear();

        for (int settingIndex = 0; settingIndex < woodPrefabSettings.Count; settingIndex++)
        {
            WoodPrefabSpawnSettings settings = woodPrefabSettings[settingIndex];
            if (settings == null || settings.Prefab == null)
            {
                continue;
            }

            if (Random.value * 100f <= settings.GetSpawnChance(spawnCell.IsEmptySpaceCell))
            {
                spawnablePrefabs.Add(settings);
            }
        }

        return spawnablePrefabs.Count > 0;
    }
}
