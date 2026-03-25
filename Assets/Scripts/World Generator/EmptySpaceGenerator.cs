using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class EmptySpaceGenerator : MonoBehaviour
{
    private static readonly Vector3Int[] CardinalDirections =
    {
        Vector3Int.up,
        Vector3Int.down,
        Vector3Int.left,
        Vector3Int.right
    };

    private static readonly Vector3Int[] DiagonalDirections =
    {
        Vector3Int.up,
        Vector3Int.down,
        Vector3Int.left,
        Vector3Int.right,
        new Vector3Int(1, 1, 0),
        new Vector3Int(1, -1, 0),
        new Vector3Int(-1, 1, 0),
        new Vector3Int(-1, -1, 0)
    };

    [Header("References")]
    [SerializeField] private Tilemap targetTilemap;

    [Header("Shape Settings")]
    [Min(1)]
    [SerializeField] private int minAreaCount = 1;

    [Min(1)]
    [SerializeField] private int maxAreaCount = 1;

    [Min(1)]
    [SerializeField] private int targetCellCount = 24;

    [SerializeField] private Vector2Int maxReach = new Vector2Int(6, 6);

    [Range(0.1f, 1f)]
    [SerializeField] private float branchChance = 0.6f;

    [SerializeField] private bool includeDiagonals = true;
    [SerializeField] private bool useFixedSeed = false;
    [SerializeField] private int seed = 12345;
    [SerializeField] private bool autoRegenerateInEditor = true;

    [Header("Area Separation")]
    [Min(0)]
    [SerializeField] private int areaSpacing = 1;

    [Header("Gizmo Settings")]
    [SerializeField] private bool showAreaGizmo = true;
    [SerializeField] private Color fillGizmoColor = new Color(0.2f, 0.8f, 0.35f, 0.25f);
    [SerializeField] private Color wireGizmoColor = new Color(0.1f, 0.45f, 0.2f, 1f);

    [SerializeField, HideInInspector] private List<Vector3Int> generatedCells = new List<Vector3Int>();

    public IReadOnlyList<Vector3Int> GeneratedCells => generatedCells;

    private void Awake()
    {
        GenerateArea();
    }

    private void OnValidate()
    {
        minAreaCount = Mathf.Max(1, minAreaCount);
        maxAreaCount = Mathf.Max(minAreaCount, maxAreaCount);
        targetCellCount = Mathf.Max(1, targetCellCount);
        maxReach.x = Mathf.Max(0, maxReach.x);
        maxReach.y = Mathf.Max(0, maxReach.y);
        branchChance = Mathf.Clamp(branchChance, 0.1f, 1f);
        areaSpacing = Mathf.Max(0, areaSpacing);

        if (!Application.isPlaying && autoRegenerateInEditor)
        {
            GenerateArea();
        }
    }

    [ContextMenu("Generate Random Shape")]
    public void GenerateArea()
    {
        generatedCells.Clear();

        if (targetTilemap == null)
        {
            return;
        }

        List<Vector3Int> validTileCells = GetValidTileCells();
        if (validTileCells.Count == 0)
        {
            return;
        }

        int generationSeed = useFixedSeed ? seed : System.Guid.NewGuid().GetHashCode();
        System.Random random = new System.Random(generationSeed);
        HashSet<Vector3Int> validTileCellSet = new HashSet<Vector3Int>(validTileCells);
        HashSet<Vector3Int> allGeneratedCells = new HashSet<Vector3Int>();
        HashSet<Vector3Int> reservedCells = new HashSet<Vector3Int>();

        // Try to generate up to maxAreaCount; it will naturally stop early if TryGenerateArea returns false (no space).
        for (int areaIndex = 0; areaIndex < maxAreaCount; areaIndex++)
        {
            if (!TryGenerateArea(validTileCells, validTileCellSet, reservedCells, random, out HashSet<Vector3Int> areaCells))
            {
                break;
            }

            foreach (Vector3Int areaCell in areaCells)
            {
                allGeneratedCells.Add(areaCell);
            }

            ReserveAreaCells(areaCells, reservedCells);
        }

        generatedCells.AddRange(allGeneratedCells);
        generatedCells.Sort(CompareCells);
    }

    private bool TryGenerateArea(
        List<Vector3Int> validTileCells,
        HashSet<Vector3Int> validTileCellSet,
        HashSet<Vector3Int> reservedCells,
        System.Random random,
        out HashSet<Vector3Int> areaCells
    )
    {
        List<Vector3Int> availableOriginCells = GetAvailableOriginCells(validTileCells, reservedCells);
        if (availableOriginCells.Count == 0)
        {
            areaCells = null;
            return false;
        }

        ShuffleCells(availableOriginCells, random);

        for (int originIndex = 0; originIndex < availableOriginCells.Count; originIndex++)
        {
            Vector3Int originCell = availableOriginCells[originIndex];

            // Check if it's even possible to fit the area on the tilemap (ignoring other areas for now)
            if (GetMaximumPossibleCellCount(originCell, validTileCellSet, null) < targetCellCount)
            {
                continue;
            }

            // Generate an "ideal" area that ignores reserved space during growth to avoid distortion
            HashSet<Vector3Int> potentialArea = GenerateSingleArea(
                originCell,
                targetCellCount,
                validTileCellSet,
                null, // Ignore reserved cells during initial growth
                random
            );

            if (potentialArea.Count < targetCellCount)
            {
                continue;
            }

            // If it collides with reserved cells (including spacing), try to push the whole area away
            if (HasCollision(potentialArea, reservedCells))
            {
                if (TryPushArea(potentialArea, reservedCells, validTileCellSet, random, out HashSet<Vector3Int> pushedArea))
                {
                    areaCells = pushedArea;
                    return true;
                }
                // If pushing fails, move on to the next origin
                continue;
            }

            areaCells = potentialArea;
            return true;
        }

        areaCells = null;
        return false;
    }

    private bool HasCollision(HashSet<Vector3Int> areaCells, HashSet<Vector3Int> reservedCells)
    {
        foreach (Vector3Int cell in areaCells)
        {
            if (reservedCells.Contains(cell))
            {
                return true;
            }
        }
        return false;
    }

    private bool TryPushArea(
        HashSet<Vector3Int> areaCells,
        HashSet<Vector3Int> reservedCells,
        HashSet<Vector3Int> validTileCellSet,
        System.Random random,
        out HashSet<Vector3Int> pushedArea
    )
    {
        pushedArea = null;

        Vector3 collisionCentroid = Vector3.zero;
        int collisionCount = 0;
        Vector3 areaCentroid = Vector3.zero;

        foreach (Vector3Int cell in areaCells)
        {
            areaCentroid += (Vector3)cell;
            if (reservedCells.Contains(cell))
            {
                collisionCentroid += (Vector3)cell;
                collisionCount++;
            }
        }

        if (collisionCount == 0)
        {
            pushedArea = areaCells;
            return true;
        }

        areaCentroid /= areaCells.Count;
        collisionCentroid /= collisionCount;

        // Push direction: away from the center of collision
        Vector3 pushDir = (areaCentroid - collisionCentroid).normalized;
        if (pushDir.sqrMagnitude < 0.01f)
        {
            double angle = random.NextDouble() * 2 * System.Math.PI;
            pushDir = new Vector3((float)System.Math.Cos(angle), (float)System.Math.Sin(angle), 0);
        }

        // Try pushing in increasing steps to find a clear spot
        int maxPushDistance = Mathf.Max(areaSpacing * 2, maxReach.x, maxReach.y, 20);
        for (int step = 1; step <= maxPushDistance; step++)
        {
            Vector3Int offset = Vector3Int.RoundToInt(pushDir * step);
            if (offset == Vector3Int.zero) continue;

            HashSet<Vector3Int> candidate = new HashSet<Vector3Int>();
            bool valid = true;
            foreach (Vector3Int cell in areaCells)
            {
                Vector3Int moved = cell + offset;
                if (!validTileCellSet.Contains(moved) || reservedCells.Contains(moved))
                {
                    valid = false;
                    break;
                }
                candidate.Add(moved);
            }

            if (valid)
            {
                pushedArea = candidate;
                return true;
            }
        }

        return false;
    }

    [ContextMenu("Randomize Seed And Generate")]
    public void RandomizeSeedAndGenerate()
    {
        seed = System.Environment.TickCount ^ GetInstanceID();
        useFixedSeed = true;
        GenerateArea();
    }

    public bool ContainsWorldPoint(Vector3 worldPoint)
    {
        if (targetTilemap == null || generatedCells == null || generatedCells.Count == 0)
        {
            return false;
        }

        Vector3Int cellPosition = targetTilemap.WorldToCell(worldPoint);
        return generatedCells.Contains(cellPosition);
    }

    private List<Vector3Int> GetValidTileCells()
    {
        List<Vector3Int> validTileCells = new List<Vector3Int>();
        BoundsInt bounds = targetTilemap.cellBounds;

        foreach (Vector3Int cellPosition in bounds.allPositionsWithin)
        {
            if (targetTilemap.HasTile(cellPosition))
            {
                validTileCells.Add(cellPosition);
            }
        }

        validTileCells.Sort(CompareCells);
        return validTileCells;
    }

    private List<Vector3Int> GetAvailableOriginCells(
        List<Vector3Int> validTileCells,
        HashSet<Vector3Int> reservedCells
    )
    {
        List<Vector3Int> availableOriginCells = new List<Vector3Int>();

        for (int i = 0; i < validTileCells.Count; i++)
        {
            Vector3Int candidateCell = validTileCells[i];
            if (!reservedCells.Contains(candidateCell))
            {
                availableOriginCells.Add(candidateCell);
            }
        }

        return availableOriginCells;
    }
    private int GetMaximumPossibleCellCount(
        Vector3Int originCell,
        HashSet<Vector3Int> validTileCells,
        HashSet<Vector3Int> reservedCells
    )
    {
        int availableCellCount = 0;

        foreach (Vector3Int cell in validTileCells)
        {
            if (Mathf.Abs(cell.x - originCell.x) <= maxReach.x
                && Mathf.Abs(cell.y - originCell.y) <= maxReach.y
                && (reservedCells == null || !reservedCells.Contains(cell)))
            {
                availableCellCount++;
            }
        }

        return Mathf.Max(1, availableCellCount);
    }

    private HashSet<Vector3Int> GenerateSingleArea(
        Vector3Int originCell,
        int desiredCellCount,
        HashSet<Vector3Int> validTileCells,
        HashSet<Vector3Int> reservedCells,
        System.Random random
    )
    {
        HashSet<Vector3Int> occupiedCells = new HashSet<Vector3Int> { originCell };
        List<Vector3Int> frontierCells = new List<Vector3Int> { originCell };

        while (occupiedCells.Count < desiredCellCount && frontierCells.Count > 0)
        {
            int frontierIndex = random.Next(frontierCells.Count);
            Vector3Int sourceCell = frontierCells[frontierIndex];
            bool expanded = false;

            List<Vector3Int> shuffledDirections = GetShuffledDirections(random);
            for (int i = 0; i < shuffledDirections.Count; i++)
            {
                Vector3Int candidateCell = sourceCell + shuffledDirections[i];
                if (!CanUseCell(candidateCell, originCell, validTileCells, reservedCells, occupiedCells))
                {
                    continue;
                }

                if (!ShouldAcceptCell(candidateCell, originCell, random))
                {
                    continue;
                }

                occupiedCells.Add(candidateCell);
                frontierCells.Add(candidateCell);
                expanded = true;

                if (occupiedCells.Count >= desiredCellCount || random.NextDouble() > branchChance)
                {
                    break;
                }
            }

            if (!expanded)
            {
                frontierCells.RemoveAt(frontierIndex);
            }
        }

        FillRemainingCells(occupiedCells, desiredCellCount, originCell, validTileCells, reservedCells, random);
        return occupiedCells;
    }

    private bool CanUseCell(
        Vector3Int candidateCell,
        Vector3Int originCell,
        HashSet<Vector3Int> validTileCells,
        HashSet<Vector3Int> reservedCells,
        HashSet<Vector3Int> occupiedCells
    )
    {
        return validTileCells.Contains(candidateCell)
            && (reservedCells == null || !reservedCells.Contains(candidateCell))
            && !occupiedCells.Contains(candidateCell)
            && Mathf.Abs(candidateCell.x - originCell.x) <= maxReach.x
            && Mathf.Abs(candidateCell.y - originCell.y) <= maxReach.y;
    }

    private bool ShouldAcceptCell(Vector3Int candidateCell, Vector3Int originCell, System.Random random)
    {
        float normalizedX = maxReach.x <= 0
            ? 0f
            : Mathf.Abs(candidateCell.x - originCell.x) / (float)maxReach.x;

        float normalizedY = maxReach.y <= 0
            ? 0f
            : Mathf.Abs(candidateCell.y - originCell.y) / (float)maxReach.y;

        float edgeFactor = Mathf.Max(normalizedX, normalizedY);
        float acceptanceChance = Mathf.Lerp(0.9f, 0.3f, edgeFactor);
        return random.NextDouble() <= acceptanceChance;
    }

    private void FillRemainingCells(
        HashSet<Vector3Int> occupiedCells,
        int desiredCellCount,
        Vector3Int originCell,
        HashSet<Vector3Int> validTileCells,
        HashSet<Vector3Int> reservedCells,
        System.Random random
    )
    {
        if (occupiedCells.Count >= desiredCellCount)
        {
            return;
        }

        List<Vector3Int> occupiedList = new List<Vector3Int>(occupiedCells);
        while (occupiedCells.Count < desiredCellCount)
        {
            bool addedCell = false;

            for (int i = 0; i < occupiedList.Count && occupiedCells.Count < desiredCellCount; i++)
            {
                Vector3Int sourceCell = occupiedList[random.Next(occupiedList.Count)];
                List<Vector3Int> shuffledDirections = GetShuffledDirections(random);

                for (int directionIndex = 0; directionIndex < shuffledDirections.Count; directionIndex++)
                {
                    Vector3Int candidateCell = sourceCell + shuffledDirections[directionIndex];
                    if (!CanUseCell(candidateCell, originCell, validTileCells, reservedCells, occupiedCells))
                    {
                        continue;
                    }

                    occupiedCells.Add(candidateCell);
                    occupiedList.Add(candidateCell);
                    addedCell = true;
                    break;
                }
            }

            if (!addedCell)
            {
                break;
            }
        }
    }

    private void ReserveAreaCells(
        HashSet<Vector3Int> areaCells,
        HashSet<Vector3Int> reservedCells
    )
    {
        foreach (Vector3Int areaCell in areaCells)
        {
            for (int xOffset = -areaSpacing; xOffset <= areaSpacing; xOffset++)
            {
                for (int yOffset = -areaSpacing; yOffset <= areaSpacing; yOffset++)
                {
                    reservedCells.Add(new Vector3Int(
                        areaCell.x + xOffset,
                        areaCell.y + yOffset,
                        areaCell.z
                    ));
                }
            }
        }
    }

    private void ShuffleCells(List<Vector3Int> cells, System.Random random)
    {
        for (int i = cells.Count - 1; i > 0; i--)
        {
            int swapIndex = random.Next(i + 1);
            Vector3Int currentCell = cells[i];
            cells[i] = cells[swapIndex];
            cells[swapIndex] = currentCell;
        }
    }

    private List<Vector3Int> GetShuffledDirections(System.Random random)
    {
        Vector3Int[] sourceDirections = includeDiagonals ? DiagonalDirections : CardinalDirections;
        List<Vector3Int> shuffledDirections = new List<Vector3Int>(sourceDirections);

        for (int i = shuffledDirections.Count - 1; i > 0; i--)
        {
            int swapIndex = random.Next(i + 1);
            Vector3Int currentDirection = shuffledDirections[i];
            shuffledDirections[i] = shuffledDirections[swapIndex];
            shuffledDirections[swapIndex] = currentDirection;
        }

        return shuffledDirections;
    }

    private int CompareCells(Vector3Int left, Vector3Int right)
    {
        int yComparison = left.y.CompareTo(right.y);
        if (yComparison != 0)
        {
            return yComparison;
        }

        int xComparison = left.x.CompareTo(right.x);
        return xComparison != 0 ? xComparison : left.z.CompareTo(right.z);
    }

    private void OnDrawGizmos()
    {
        if (!showAreaGizmo || targetTilemap == null || generatedCells == null || generatedCells.Count == 0)
        {
            return;
        }

        Vector3 gizmoSize = GetWorldCellSize();

        for (int i = 0; i < generatedCells.Count; i++)
        {
            Vector3 worldCenter = targetTilemap.GetCellCenterWorld(generatedCells[i]);

            Gizmos.color = fillGizmoColor;
            Gizmos.DrawCube(worldCenter, gizmoSize);

            Gizmos.color = wireGizmoColor;
            Gizmos.DrawWireCube(worldCenter, gizmoSize);
        }
    }

    private Vector3 GetWorldCellSize()
    {
        Vector3 cellSize = targetTilemap.layoutGrid.cellSize;
        Vector3 gridScale = targetTilemap.layoutGrid.transform.lossyScale;

        return new Vector3(
            Mathf.Abs(cellSize.x * gridScale.x),
            Mathf.Abs(cellSize.y * gridScale.y),
            0.05f
        );
    }
}
