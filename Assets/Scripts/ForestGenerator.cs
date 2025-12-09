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

    void Start()
    {
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

            // Choose ONE spacing radius for this tile
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

                Instantiate(treePrefab, pos, Quaternion.identity);
                placedTrees.Add(pos);
                spawned++;
            }
        }
    }

    bool IsPositionValid(Vector3 pos, float dist)
    {
        // check against ALL OTHER TREES
        foreach (var t in placedTrees)
        {
            if (Vector3.Distance(pos, t) < dist)
                return false;
        }
        return true;
    }
}
