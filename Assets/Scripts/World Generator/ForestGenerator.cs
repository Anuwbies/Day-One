using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class ForestGenerator : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Tilemap forestTilemap;
    [SerializeField] private GameObject treePrefab;
    [SerializeField] private Transform generatedTreesParent;

    [Header("Generation Settings")]
    [Min(0f)]
    [SerializeField] private float minRandomDist = 1f;

    [Min(0f)]
    [SerializeField] private float maxRandomDist = 8f;

    [Min(1)]
    [SerializeField] private int treesPerTile = 1;

    [Min(1)]
    [SerializeField] private int maxPlacementAttemptsPerTree = 4;

    [Min(0f)]
    [SerializeField] private float edgeTileMargin = 0f;

    [SerializeField] private bool generateOnStart = true;
    [SerializeField] private bool clearBeforeGenerate = true;

    private readonly List<Vector3> generatedPositions = new List<Vector3>();
    private Collider2D cachedTreeChildCollider;

    private void Start()
    {
        if (generateOnStart)
        {
            GenerateForest();
        }
    }

    private void OnValidate()
    {
        cachedTreeChildCollider = null;
        minRandomDist = Mathf.Max(0f, minRandomDist);
        maxRandomDist = Mathf.Max(minRandomDist, maxRandomDist);
        treesPerTile = Mathf.Max(1, treesPerTile);
        maxPlacementAttemptsPerTree = Mathf.Max(1, maxPlacementAttemptsPerTree);
        edgeTileMargin = Mathf.Max(0f, edgeTileMargin);
    }

    [ContextMenu("Generate Forest")]
    public void GenerateForest()
    {
        if (forestTilemap == null)
        {
            Debug.LogWarning($"No forest tilemap assigned for {name}.");
            return;
        }

        if (treePrefab == null)
        {
            Debug.LogWarning($"No tree prefab assigned for {name}.");
            return;
        }

        Transform spawnParent = GetOrCreateGeneratedTreesParent();

        if (clearBeforeGenerate)
        {
            ClearGeneratedTrees();
        }
        else
        {
            RebuildGeneratedPositions(spawnParent);
        }

        BoundsInt bounds = forestTilemap.cellBounds;

        foreach (Vector3Int cellPosition in bounds.allPositionsWithin)
        {
            if (!forestTilemap.HasTile(cellPosition))
            {
                continue;
            }

            for (int i = 0; i < treesPerTile; i++)
            {
                TrySpawnTreeOnCell(cellPosition, spawnParent);
            }
        }
    }

    [ContextMenu("Clear Generated Trees")]
    public void ClearGeneratedTrees()
    {
        Transform spawnParent = GetOrCreateGeneratedTreesParent();

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

    private void TrySpawnTreeOnCell(Vector3Int cellPosition, Transform spawnParent)
    {
        for (int attempt = 0; attempt < maxPlacementAttemptsPerTree; attempt++)
        {
            Vector3 pivotPosition = GetRandomPivotPositionInCell(cellPosition);
            Vector3 spawnPosition = GetTreeRootPositionFromPivot(pivotPosition);

            if (!IsOnSelectedTilemap(spawnPosition))
            {
                continue;
            }

            if (!IsFarEnoughFromExistingTrees(pivotPosition))
            {
                continue;
            }

            GameObject treeInstance = Instantiate(treePrefab, spawnPosition, Quaternion.identity, spawnParent);
            treeInstance.name = treePrefab.name;
            generatedPositions.Add(GetTreePivotWorldPosition(treeInstance.transform));
            return;
        }
    }

    private Vector3 GetRandomPivotPositionInCell(Vector3Int cellPosition)
    {
        Vector3 cellOrigin = forestTilemap.CellToWorld(cellPosition);
        Vector3 cellSize = forestTilemap.layoutGrid.cellSize;
        Vector3 pivotPosition = new Vector3(
            cellOrigin.x + Random.Range(0.15f, 0.85f) * cellSize.x,
            cellOrigin.y + Random.Range(0.15f, 0.85f) * cellSize.y,
            treePrefab.transform.position.z
        );

        return pivotPosition;
    }

    private bool IsOnSelectedTilemap(Vector3 treeRootPosition)
    {
        if (forestTilemap == null)
        {
            return false;
        }

        if (!TryGetTreeColliderBounds(treeRootPosition, out Bounds colliderBounds))
        {
            Vector3Int cellPosition = forestTilemap.WorldToCell(treeRootPosition);
            return forestTilemap.HasTile(cellPosition);
        }

        return AreBoundsCoveredByTilemap(colliderBounds);
    }

    private bool AreBoundsCoveredByTilemap(Bounds colliderBounds)
    {
        const float edgeInset = 0.001f;
        Vector3 cellSize = forestTilemap.layoutGrid.cellSize;
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

        Vector3Int minCell = forestTilemap.WorldToCell(minPoint);
        Vector3Int maxCell = forestTilemap.WorldToCell(maxPoint);

        for (int x = minCell.x; x <= maxCell.x; x++)
        {
            for (int y = minCell.y; y <= maxCell.y; y++)
            {
                Vector3Int cellPosition = new Vector3Int(x, y, 0);
                if (!forestTilemap.HasTile(cellPosition))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private bool IsFarEnoughFromExistingTrees(Vector3 candidatePosition)
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
            Vector3 existingPosition = generatedPositions[i];
            if (Vector2.Distance(candidatePosition, existingPosition) < requiredDistance)
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
            generatedPositions.Add(GetTreePivotWorldPosition(spawnParent.GetChild(i)));
        }
    }

    private Vector3 GetTreeRootPositionFromPivot(Vector3 pivotPosition)
    {
        Vector3 pivotOffset = GetTreePivotLocalOffset();
        return new Vector3(
            pivotPosition.x - pivotOffset.x,
            pivotPosition.y - pivotOffset.y,
            treePrefab.transform.position.z
        );
    }

    private Vector3 GetTreePivotWorldPosition(Transform treeTransform)
    {
        if (treeTransform == null)
        {
            return Vector3.zero;
        }

        Collider2D childCollider = GetChildCollider(treeTransform.gameObject);
        if (childCollider == null)
        {
            return treeTransform.position;
        }

        return childCollider.transform.TransformPoint(childCollider.offset);
    }

    private Vector3 GetTreePivotLocalOffset()
    {
        Collider2D childCollider = GetTreePrefabChildCollider();
        if (childCollider == null)
        {
            return Vector3.zero;
        }

        return treePrefab.transform.InverseTransformPoint(
            childCollider.transform.TransformPoint(childCollider.offset)
        );
    }

    private Collider2D GetTreePrefabChildCollider()
    {
        if (cachedTreeChildCollider != null)
        {
            return cachedTreeChildCollider;
        }

        cachedTreeChildCollider = GetChildCollider(treePrefab);
        return cachedTreeChildCollider;
    }

    private bool TryGetTreeColliderBounds(Vector3 treeRootPosition, out Bounds colliderBounds)
    {
        Collider2D childCollider = GetTreePrefabChildCollider();
        if (childCollider == null)
        {
            colliderBounds = default;
            return false;
        }

        colliderBounds = childCollider.bounds;
        colliderBounds.center += treeRootPosition - treePrefab.transform.position;
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

    private Transform GetOrCreateGeneratedTreesParent()
    {
        if (generatedTreesParent != null)
        {
            return generatedTreesParent;
        }

        Transform existingChild = transform.Find("Generated Trees");
        if (existingChild != null)
        {
            generatedTreesParent = existingChild;
            return generatedTreesParent;
        }

        GameObject generatedParentObject = new GameObject("Generated Trees");
        generatedParentObject.transform.SetParent(transform, false);
        generatedTreesParent = generatedParentObject.transform;
        return generatedTreesParent;
    }
}
