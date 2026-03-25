using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class BushGenerator : MonoBehaviour
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
    private class BushPrefabSpawnSettings
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
    [SerializeField] private Tilemap bushTilemap;
    [SerializeField] private Transform generatedBushesParent;

    [Header("Bush Prefabs")]
    [SerializeField] private List<BushPrefabSpawnSettings> bushPrefabSettings = new List<BushPrefabSpawnSettings>();

    [Header("Generation Settings")]
    [Min(0f)]
    [SerializeField] private float minRandomDist = 1f;

    [Min(0f)]
    [SerializeField] private float maxRandomDist = 8f;

    [Min(1)]
    [SerializeField] private int bushesPerTile = 1;

    [Min(1)]
    [SerializeField] private int maxPlacementAttemptsPerBush = 4;

    [Min(0f)]
    [SerializeField] private float edgeTileMargin = 0f;

    [Header("Obstacle Settings")]
    [SerializeField] private LayerMask obstacleLayer = -1;

    [Header("Additional Spawn Areas")]
    [SerializeField] private List<EmptySpaceGenerator> emptySpaceGenerators;

    [Header("Blue Berry Around Bush")]
    [SerializeField] private ItemData blueBerryItem;

    [Range(0f, 100f)]
    [SerializeField] private float blueBerrySpawnChancePercent = 25f;

    [Min(1)]
    [SerializeField] private int minBlueBerryAmount = 1;

    [Min(1)]
    [SerializeField] private int maxBlueBerryAmount = 2;

    [Header("Blue Berry Fallback Drop Area")]
    [SerializeField] private Vector2 blueBerrySpawnRadiusXY = new Vector2(0.45f, 0.2f);

    [SerializeField] private bool generateOnStart = true;
    [SerializeField] private bool clearBeforeGenerate = true;

    private readonly List<Vector3> generatedPositions = new List<Vector3>();
    private readonly List<Collider2D> overlapResults = new List<Collider2D>();
    private readonly Dictionary<GameObject, Collider2D> cachedBushChildColliders = new Dictionary<GameObject, Collider2D>();
    private readonly List<BushPrefabSpawnSettings> spawnablePrefabBuffer = new List<BushPrefabSpawnSettings>();

    private void Start()
    {
        if (generateOnStart)
        {
            GenerateBushes();
        }
    }

    private void OnValidate()
    {
        cachedBushChildColliders.Clear();
        minRandomDist = Mathf.Max(0f, minRandomDist);
        maxRandomDist = Mathf.Max(minRandomDist, maxRandomDist);
        bushesPerTile = Mathf.Max(1, bushesPerTile);
        maxPlacementAttemptsPerBush = Mathf.Max(1, maxPlacementAttemptsPerBush);
        edgeTileMargin = Mathf.Max(0f, edgeTileMargin);
        blueBerrySpawnChancePercent = Mathf.Clamp(blueBerrySpawnChancePercent, 0f, 100f);
        minBlueBerryAmount = Mathf.Max(1, minBlueBerryAmount);
        maxBlueBerryAmount = Mathf.Max(minBlueBerryAmount, maxBlueBerryAmount);
        blueBerrySpawnRadiusXY.x = Mathf.Max(0f, blueBerrySpawnRadiusXY.x);
        blueBerrySpawnRadiusXY.y = Mathf.Max(0f, blueBerrySpawnRadiusXY.y);
        ClampBushPrefabSettings();
        AssignDefaultBlueBerryItem();
    }

    [ContextMenu("Generate Bushes")]
    public void GenerateBushes()
    {
        ClampBushPrefabSettings();
        AssignDefaultBlueBerryItem();

        if (!HasValidBushPrefab())
        {
            Debug.LogWarning($"No bush prefabs assigned for {name}.");
            return;
        }

        InitializeObstacleLayer();

        List<SpawnCell> spawnCells = CollectSpawnCells();
        if (spawnCells.Count == 0)
        {
            Debug.LogWarning($"No valid bush spawn cells found for {name}.");
            return;
        }

        Transform spawnParent = GetOrCreateGeneratedBushesParent();

        if (clearBeforeGenerate)
        {
            ClearGeneratedBushes();
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

            for (int spawnIndex = 0; spawnIndex < bushesPerTile; spawnIndex++)
            {
                TrySpawnBushOnCell(spawnCells[cellIndex], spawnParent, spawnablePrefabBuffer);
            }
        }
    }

    [ContextMenu("Clear Generated Bushes")]
    public void ClearGeneratedBushes()
    {
        Transform spawnParent = GetOrCreateGeneratedBushesParent();

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

    private void TrySpawnBushOnCell(
        SpawnCell spawnCell,
        Transform spawnParent,
        List<BushPrefabSpawnSettings> spawnablePrefabs
    )
    {
        for (int attempt = 0; attempt < maxPlacementAttemptsPerBush; attempt++)
        {
            if (!TryGetRandomBushPrefab(spawnablePrefabs, out GameObject bushPrefab))
            {
                return;
            }

            Vector3 pivotPosition = GetRandomPivotPositionInCell(spawnCell);
            Vector3 spawnPosition = GetBushRootPositionFromPivot(bushPrefab, pivotPosition);

            if (!IsOnSelectedSpawnArea(bushPrefab, spawnPosition, spawnCell))
            {
                continue;
            }

            if (!IsFarEnoughFromExistingBushes(pivotPosition))
            {
                continue;
            }

            if (!IsAreaFreeFromObstacles(bushPrefab, spawnPosition))
            {
                continue;
            }

            GameObject bushInstance = Instantiate(bushPrefab, spawnPosition, Quaternion.identity, spawnParent);
            bushInstance.name = bushPrefab.name;
            generatedPositions.Add(GetBushPivotWorldPosition(bushInstance.transform));
            TrySpawnBlueBerryAroundBush(bushInstance.transform, spawnParent);
            return;
        }
    }

    private List<SpawnCell> CollectSpawnCells()
    {
        List<SpawnCell> spawnCells = new List<SpawnCell>();
        Dictionary<string, int> cellIndices = new Dictionary<string, int>();

        if (bushTilemap != null)
        {
            BoundsInt bounds = bushTilemap.cellBounds;
            foreach (Vector3Int cellPosition in bounds.allPositionsWithin)
            {
                if (!bushTilemap.HasTile(cellPosition))
                {
                    continue;
                }

                AddSpawnCell(bushTilemap, cellPosition, false, spawnCells, cellIndices);
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

    private bool IsOnSelectedSpawnArea(GameObject bushPrefab, Vector3 bushRootPosition, SpawnCell spawnCell)
    {
        if (spawnCell.SpawnTilemap == null)
        {
            return false;
        }

        if (spawnCell.IsEmptySpaceCell)
        {
            return IsInsideEmptySpace(bushPrefab, bushRootPosition, spawnCell.SpawnTilemap);
        }

        return IsOnSelectedTilemap(bushPrefab, bushRootPosition, spawnCell.SpawnTilemap);
    }

    private bool IsInsideEmptySpace(GameObject bushPrefab, Vector3 bushRootPosition, Tilemap sourceTilemap)
    {
        if (sourceTilemap == null)
        {
            return false;
        }

        if (!TryGetBushColliderBounds(bushPrefab, bushRootPosition, out Bounds colliderBounds))
        {
            return IsInAnyEmptySpace(bushRootPosition);
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

    private bool IsOnSelectedTilemap(GameObject bushPrefab, Vector3 bushRootPosition, Tilemap sourceTilemap)
    {
        if (sourceTilemap == null)
        {
            return false;
        }

        if (!TryGetBushColliderBounds(bushPrefab, bushRootPosition, out Bounds colliderBounds))
        {
            Vector3Int cellPosition = sourceTilemap.WorldToCell(bushRootPosition);
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

    private bool IsFarEnoughFromExistingBushes(Vector3 candidatePosition)
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

    private bool IsAreaFreeFromObstacles(GameObject bushPrefab, Vector3 bushRootPosition)
    {
        ContactFilter2D filter = new ContactFilter2D
        {
            useTriggers = true
        };
        filter.SetLayerMask(obstacleLayer);

        overlapResults.Clear();
        Physics2D.SyncTransforms();
        GetPlacementOverlaps(bushPrefab, bushRootPosition, filter, overlapResults);

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
            generatedPositions.Add(GetBushPivotWorldPosition(spawnParent.GetChild(i)));
        }
    }

    private Vector3 GetBushRootPositionFromPivot(GameObject bushPrefab, Vector3 pivotPosition)
    {
        Vector3 pivotOffset = GetBushPivotLocalOffset(bushPrefab);
        return new Vector3(
            pivotPosition.x - pivotOffset.x,
            pivotPosition.y - pivotOffset.y,
            bushPrefab.transform.position.z
        );
    }

    private Vector3 GetBushPivotWorldPosition(Transform bushTransform)
    {
        if (bushTransform == null)
        {
            return Vector3.zero;
        }

        Collider2D childCollider = GetChildCollider(bushTransform.gameObject);
        if (childCollider == null)
        {
            return bushTransform.position;
        }

        return childCollider.transform.TransformPoint(childCollider.offset);
    }

    private Vector3 GetBushPivotLocalOffset(GameObject bushPrefab)
    {
        Collider2D childCollider = GetBushPrefabChildCollider(bushPrefab);
        if (childCollider == null)
        {
            return Vector3.zero;
        }

        return bushPrefab.transform.InverseTransformPoint(
            childCollider.transform.TransformPoint(childCollider.offset)
        );
    }

    private Collider2D GetBushPrefabChildCollider(GameObject bushPrefab)
    {
        if (bushPrefab == null)
        {
            return null;
        }

        if (cachedBushChildColliders.TryGetValue(bushPrefab, out Collider2D cachedCollider) && cachedCollider != null)
        {
            return cachedCollider;
        }

        Collider2D foundCollider = GetChildCollider(bushPrefab);
        cachedBushChildColliders[bushPrefab] = foundCollider;
        return foundCollider;
    }

    private int GetPlacementOverlaps(
        GameObject bushPrefab,
        Vector3 bushRootPosition,
        ContactFilter2D filter,
        List<Collider2D> results
    )
    {
        Collider2D childCollider = GetBushPrefabChildCollider(bushPrefab);
        if (childCollider == null)
        {
            return Physics2D.OverlapBox((Vector2)bushRootPosition, Vector2.one * 0.1f, 0f, filter, results);
        }

        if (childCollider is CircleCollider2D circleCollider)
        {
            Vector2 center = GetBushChildColliderWorldCenter(bushPrefab, circleCollider, bushRootPosition);
            Vector3 scale = circleCollider.transform.lossyScale;
            float radius = circleCollider.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y));
            return Physics2D.OverlapCircle(center, radius, filter, results);
        }

        if (childCollider is BoxCollider2D boxCollider)
        {
            Vector2 center = GetBushChildColliderWorldCenter(bushPrefab, boxCollider, bushRootPosition);
            Vector2 size = GetScaledColliderSize(boxCollider.size, boxCollider.transform);
            float angle = boxCollider.transform.eulerAngles.z;
            return Physics2D.OverlapBox(center, size, angle, filter, results);
        }

        if (TryGetBushColliderBounds(bushPrefab, bushRootPosition, out Bounds colliderBounds))
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

    private Vector2 GetBushChildColliderWorldCenter(
        GameObject bushPrefab,
        Collider2D childCollider,
        Vector3 bushRootPosition
    )
    {
        Vector3 rootOffset = bushRootPosition - bushPrefab.transform.position;
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

    private bool TryGetBushColliderBounds(GameObject bushPrefab, Vector3 bushRootPosition, out Bounds colliderBounds)
    {
        Collider2D childCollider = GetBushPrefabChildCollider(bushPrefab);
        if (childCollider == null)
        {
            colliderBounds = default;
            return false;
        }

        colliderBounds = childCollider.bounds;
        colliderBounds.center += bushRootPosition - bushPrefab.transform.position;
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

    private void TrySpawnBlueBerryAroundBush(Transform bushTransform, Transform spawnParent)
    {
        if (bushTransform == null
            || blueBerryItem == null
            || blueBerryItem.worldPrefab == null
            || !blueBerryItem.canDrop
            || Random.value * 100f > blueBerrySpawnChancePercent)
        {
            return;
        }

        int amountToSpawn = Random.Range(minBlueBerryAmount, maxBlueBerryAmount + 1);
        Vector3 spawnPosition = GetBlueBerrySpawnPosition(bushTransform);

        GameObject berryInstance = Instantiate(blueBerryItem.worldPrefab, spawnPosition, Quaternion.identity, spawnParent);
        berryInstance.name = blueBerryItem.worldPrefab.name;

        Item worldItem = berryInstance.GetComponent<Item>();
        if (worldItem != null)
        {
            worldItem.data = blueBerryItem;
            worldItem.amount = amountToSpawn;
        }
    }

    private Vector3 GetBlueBerrySpawnPosition(Transform bushTransform)
    {
        DropLoot dropLoot = bushTransform.GetComponent<DropLoot>();
        if (dropLoot == null)
        {
            dropLoot = bushTransform.GetComponentInChildren<DropLoot>(true);
        }

        if (dropLoot != null)
        {
            Vector3 centerPosition = dropLoot.transform.position + new Vector3(dropLoot.xOffset, dropLoot.yOffset, 0f);
            Vector2 randomOffset = GetRandomPointInAnnulus(dropLoot.deadZoneRadius, dropLoot.dropRadius);
            return centerPosition + new Vector3(randomOffset.x, randomOffset.y, 0f);
        }

        Vector2 fallbackOffset = GetRandomPointInAnnulus(Vector2.zero, blueBerrySpawnRadiusXY);
        return bushTransform.position + new Vector3(fallbackOffset.x, fallbackOffset.y, 0f);
    }

    private Vector2 GetRandomPointInAnnulus(Vector2 minRadii, Vector2 maxRadii)
    {
        Vector2 direction = Random.insideUnitCircle.normalized;
        if (direction == Vector2.zero)
        {
            direction = Vector2.up;
        }

        float t = Random.Range(0f, 1f);
        float radiusX = Mathf.Lerp(minRadii.x, maxRadii.x, t);
        float radiusY = Mathf.Lerp(minRadii.y, maxRadii.y, t);
        return new Vector2(direction.x * radiusX, direction.y * radiusY);
    }

    private void ClampBushPrefabSettings()
    {
        if (bushPrefabSettings == null)
        {
            bushPrefabSettings = new List<BushPrefabSpawnSettings>();
        }

        for (int settingIndex = 0; settingIndex < bushPrefabSettings.Count; settingIndex++)
        {
            BushPrefabSpawnSettings settings = bushPrefabSettings[settingIndex];
            if (settings != null)
            {
                settings.ClampValues();
            }
        }
    }

    private void AssignDefaultBlueBerryItem()
    {
#if UNITY_EDITOR
        if (blueBerryItem == null)
        {
            blueBerryItem = AssetDatabase.LoadAssetAtPath<ItemData>("Assets/Item Data/Blue Berry.asset");
        }
#endif
    }

    private void InitializeObstacleLayer()
    {
        int obstacleLayerValue = obstacleLayer.value;
        if (obstacleLayerValue == -1 || obstacleLayerValue == 0)
        {
            obstacleLayer = ~(1 << 2);
        }
    }

    private Transform GetOrCreateGeneratedBushesParent()
    {
        if (generatedBushesParent != null)
        {
            return generatedBushesParent;
        }

        Transform existingChild = transform.Find("Generated Bushes");
        if (existingChild != null)
        {
            generatedBushesParent = existingChild;
            return generatedBushesParent;
        }

        GameObject generatedParentObject = new GameObject("Generated Bushes");
        generatedParentObject.transform.SetParent(transform, false);
        generatedBushesParent = generatedParentObject.transform;
        return generatedBushesParent;
    }

    private bool HasValidBushPrefab()
    {
        if (bushPrefabSettings == null || bushPrefabSettings.Count == 0)
        {
            return false;
        }

        for (int i = 0; i < bushPrefabSettings.Count; i++)
        {
            BushPrefabSpawnSettings settings = bushPrefabSettings[i];
            if (settings != null && settings.Prefab != null)
            {
                return true;
            }
        }

        return false;
    }

    private bool TryGetRandomBushPrefab(
        List<BushPrefabSpawnSettings> spawnablePrefabs,
        out GameObject bushPrefab
    )
    {
        bushPrefab = null;
        if (spawnablePrefabs == null || spawnablePrefabs.Count == 0)
        {
            return false;
        }

        int startIndex = Random.Range(0, spawnablePrefabs.Count);
        for (int offset = 0; offset < spawnablePrefabs.Count; offset++)
        {
            BushPrefabSpawnSettings settings = spawnablePrefabs[(startIndex + offset) % spawnablePrefabs.Count];
            if (settings != null && settings.Prefab != null)
            {
                bushPrefab = settings.Prefab;
                return true;
            }
        }

        return false;
    }

    private bool TryGetSpawnablePrefabsForTile(
        SpawnCell spawnCell,
        List<BushPrefabSpawnSettings> spawnablePrefabs
    )
    {
        spawnablePrefabs.Clear();

        for (int settingIndex = 0; settingIndex < bushPrefabSettings.Count; settingIndex++)
        {
            BushPrefabSpawnSettings settings = bushPrefabSettings[settingIndex];
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
