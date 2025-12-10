using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections.Generic;

public class ForestAreaGenerator : MonoBehaviour
{
    [Header("References")]
    public Tilemap forestTilemap;
    public GameObject treePrefab;

    [Header("Generation Settings")]
    public float minRandomDist = 1f;
    public float maxRandomDist = 5f;
    public int treesPerTile = 1;

    private List<Vector3> forestPositions = new List<Vector3>();
    private List<Vector3> placedTrees = new List<Vector3>();

    private Transform treeParent; // single parent container

    void Start()
    {
        // Create TreeParent under THIS object
        GameObject parentObj = new GameObject("TreeParent");
        parentObj.transform.SetParent(this.transform);
        treeParent = parentObj.transform;

        CollectForestTiles();
        GenerateTrees();
    }

    void CollectForestTiles()
    {
        BoundsInt bounds = forestTilemap.cellBounds;

        foreach (var pos in bounds.allPositionsWithin)
        {
            if (!forestTilemap.HasTile(pos)) continue;

            forestPositions.Add(
                forestTilemap.CellToWorld(pos) + new Vector3(0.5f, 0.5f, 0)
            );
        }
    }

    void GenerateTrees()
    {
        foreach (var basePos in forestPositions)
        {
            int spawned = 0;
            int attempts = 0;

            float spacingRadius = Random.Range(minRandomDist, maxRandomDist);

            while (spawned < treesPerTile && attempts < 50)
            {
                attempts++;

                Vector3 pos = basePos + new Vector3(
                    Random.Range(-0.5f, 0.5f),
                    Random.Range(-0.5f, 0.5f),
                    0
                );

                if (!IsPositionValid(pos, spacingRadius)) continue;

                // Instantiate tree and parent under TreeParent
                GameObject tree = Instantiate(treePrefab, pos, Quaternion.identity, treeParent);

                placedTrees.Add(pos);
                spawned++;
            }
        }
    }

    bool IsPositionValid(Vector3 pos, float dist)
    {
        foreach (var t in placedTrees)
        {
            if (Vector3.Distance(pos, t) < dist)
                return false;
        }
        return true;
    }
}
