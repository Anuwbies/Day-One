using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections.Generic;

public class PlacementManager : MonoBehaviour
{
    public static PlacementManager Instance { get; private set; }

    public bool IsPlacing => isPlacing;

    [Header("Settings")]
    public List<string> obstacleSortingLayers = new List<string> { "World", "Foreground" };
    private LayerMask obstacleLayer = -1; // Hidden as you use Sorting Layers for filtering
    public string ghostSortingLayer = "Tool";
    public int ghostSortingOrder = 1001;
    [Range(0f, 1f)]
    public float ghostTransparency = 0.5f;

    public Color validColor = Color.green;
    public Color invalidColor = Color.red;

    private Material ghostMaterial;
    private Grid grid;
    private GridVisualizer gridVisualizer;
    private bool isPlacing = false;
    private ItemData currentItemData;
    private InventorySlot currentSlot;
    private InventoryUI inventoryUI;

    private GameObject ghostObject;
    private SpriteRenderer ghostRenderer;

    private List<Vector3Int> pendingCells = new List<Vector3Int>();
    private List<GameObject> pendingGhosts = new List<GameObject>();

    private Vector3Int startCell;
    private bool axisLocked = false;
    private bool isHorizontal = false;
    private bool canDrag = false;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        // Create an unlit material for ghosts so they aren't affected by lighting (prevents being black)
        Shader ghostShader = Shader.Find("Sprites/Default");
        if (ghostShader != null) ghostMaterial = new Material(ghostShader);

        // Find the grid in the scene
        grid = Object.FindAnyObjectByType<Grid>();
        if (grid != null)
        {
            gridVisualizer = grid.GetComponent<GridVisualizer>();
            if (gridVisualizer == null)
            {
                gridVisualizer = grid.gameObject.AddComponent<GridVisualizer>();
            }
        }
    }

    private void Update()
    {
        if (!isPlacing) return;

        // Force grid visibility if it was lost
        if (gridVisualizer != null && !gridVisualizer.isActiveAndEnabled && isPlacing)
        {
            gridVisualizer.SetVisible(true);
        }

        // Handle ESC specifically to cancel an active drag
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (canDrag)
            {
                Debug.Log("Cancelling drag placement via ESC.");
                ClearPending();
                canDrag = false;
            }
        }

        if (Time.timeScale == 0) return;

        if (Input.GetMouseButtonDown(0))
        {
            Vector3 mouseWorldPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            mouseWorldPos.z = 0;
            startCell = GetBottomLeftCell(mouseWorldPos);
            axisLocked = false;
            
            // Clear previous pending to be sure
            ClearPending();
            
            // Check if start position is valid before allowing drag
            Vector3 snappedStartPos = GetSnappedPosition(startCell);
            canDrag = IsPositionValid(snappedStartPos);

            if (canDrag)
            {
                ContinueDragPlacement();
            }
            else
            {
                Debug.Log("Cannot start placement: Start cell is occupied.");
            }
        }

        if (Input.GetMouseButton(0) && canDrag)
        {
            ContinueDragPlacement();
        }

        if (Input.GetMouseButtonUp(0))
        {
            if (canDrag)
            {
                FinalizePlacement();
            }
            canDrag = false;
        }

        if (Input.GetMouseButtonDown(1))
        {
            if (canDrag)
            {
                // If dragging, only cancel the drag/pending ghosts
                Debug.Log("Cancelling drag placement.");
                ClearPending();
                canDrag = false;
            }
            else
            {
                // If not dragging, exit placement mode entirely
                EndPlacement();
            }
        }

        UpdateGhost();
    }

    public void StartPlacement(ItemData data, InventorySlot slot, InventoryUI ui)
    {
        if (data == null || data.worldPrefab == null) return;

        currentItemData = data;
        currentSlot = slot;
        inventoryUI = ui;
        isPlacing = true;

        if (gridVisualizer != null) gridVisualizer.SetVisible(true);

        Debug.Log($"Starting placement for {data.itemName}");

        // Create main tracking ghost
        ghostObject = CreateGhostInstance("MainPlacementGhost");

        // Hide UI
        if (inventoryUI != null)
        {
            inventoryUI.SetInventoryOpen(false);
        }
    }

    private GameObject CreateGhostInstance(string name)
    {
        // Instantiate the actual prefab so it matches visuals and scale perfectly
        GameObject ghost = Instantiate(currentItemData.worldPrefab);
        ghost.name = name;

        // Disable or Remove all logic scripts to prevent behavior/errors
        // Keep YSorter active so the ghost sorts correctly in the world
        MonoBehaviour[] scripts = ghost.GetComponentsInChildren<MonoBehaviour>();
        foreach (var script in scripts)
        {
            if (script is YSorter) 
            {
                // Disable transparency fading on the ghost's sorter
                ((YSorter)script).enableTransparency = false;
                continue; 
            }
            DestroyImmediate(script);
        }

        // Set all renderers to transparent, on top, and use unlit material
        SpriteRenderer[] renderers = ghost.GetComponentsInChildren<SpriteRenderer>();
        foreach (var sr in renderers)
        {
            if (ghostMaterial != null) sr.material = ghostMaterial;
            sr.sortingLayerName = ghostSortingLayer;
            // The YSorter will overwrite sortingOrder, but we set the layer here
        }

        // Disable all colliders on the ghost
        Collider2D[] colliders = ghost.GetComponentsInChildren<Collider2D>();
        foreach (var col in colliders)
        {
            col.enabled = false;
        }
        
        return ghost;
    }

    private void UpdateGhost()
    {
        if (ghostObject == null || grid == null || currentItemData == null) return;

        Vector3 mouseWorldPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        mouseWorldPos.z = 0;

        // Snapping logic: find the bottom-left anchor cell based on centered mouse
        Vector3Int cellPos = GetBottomLeftCell(mouseWorldPos);

        // If dragging, apply axis lock to the main tracking ghost too
        if (Input.GetMouseButton(0))
        {
            if (axisLocked)
            {
                if (isHorizontal) cellPos.y = startCell.y;
                else cellPos.x = startCell.x;
            }
        }

        Vector3 snappedPos = GetSnappedPosition(cellPos);
        ghostObject.transform.position = snappedPos;

        // Validation check for the main cursor ghost (Check world AND pending ghosts)
        bool isValid = IsPositionValid(snappedPos, cellPos);
        UpdateGhostColor(ghostObject, isValid);

        // Hide mouse ghost if we are dragging and have reached the item limit, 
        // or if the current cell is already occupied by a pending ghost.
        if (Input.GetMouseButton(0) && currentSlot != null)
        {
            bool alreadyPending = pendingCells.Contains(cellPos);
            bool isFull = pendingCells.Count >= currentSlot.amount;
            ghostObject.SetActive(!alreadyPending && !isFull);
        }
        else
        {
            ghostObject.SetActive(true);
        }

        // VISUAL FEEDBACK: Update colors of all pending ghosts
        for (int i = 0; i < pendingCells.Count; i++)
        {
            Vector3 pPos = GetSnappedPosition(pendingCells[i]);
            UpdateGhostColor(pendingGhosts[i], IsPositionValid(pPos, pendingCells[i], true));
        }
    }

    private Vector3Int GetBottomLeftCell(Vector3 worldPos)
    {
        // To keep the item centered on the mouse, we offset the world position 
        // by half the item's grid size before converting to a cell coordinate.
        Vector3 offset = new Vector3(
            (currentItemData.gridWidth - 1) * grid.cellSize.x * 0.5f,
            (currentItemData.gridHeight - 1) * grid.cellSize.y * 0.5f,
            0
        );
        return grid.WorldToCell(worldPos - offset);
    }

    private Vector3 GetSnappedPosition(Vector3Int cellPos)
    {
        Vector3 cellCenter = grid.GetCellCenterWorld(cellPos);
        
        // Offset the position if the item is larger than 1x1 to keep it centered on its grid footprint
        float offsetX = (currentItemData.gridWidth - 1) * grid.cellSize.x * 0.5f;
        float offsetY = (currentItemData.gridHeight - 1) * grid.cellSize.y * 0.5f;
        
        return cellCenter + new Vector3(offsetX, offsetY, 0);
    }

    private void ContinueDragPlacement()
    {
        if (grid == null || currentSlot == null || currentItemData == null) return;

        Vector3 mouseWorldPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        mouseWorldPos.z = 0;
        Vector3Int currentCell = GetBottomLeftCell(mouseWorldPos);

        // Determine axis lock if not already set
        if (!axisLocked && currentCell != startCell)
        {
            axisLocked = true;
            isHorizontal = Mathf.Abs(currentCell.x - startCell.x) >= Mathf.Abs(currentCell.y - startCell.y);
        }

        // Apply axis lock: force currentCell to stay on the start axis
        if (axisLocked)
        {
            if (isHorizontal) currentCell.y = startCell.y;
            else currentCell.x = startCell.x;
        }
        else
        {
            currentCell = startCell;
        }

        List<Vector3Int> desiredCells = GetCellsInLine(startCell, currentCell);

        // 1. Remove ghosts that are no longer in the line
        for (int i = pendingCells.Count - 1; i >= 0; i--)
        {
            if (!desiredCells.Contains(pendingCells[i]))
            {
                Destroy(pendingGhosts[i]);
                pendingGhosts.RemoveAt(i);
                pendingCells.RemoveAt(i);
            }
        }

        // 2. Add ghosts for new cells in the line (respecting stack size and collision)
        foreach (Vector3Int cell in desiredCells)
        {
            if (pendingCells.Contains(cell)) continue;
            if (pendingCells.Count >= currentSlot.amount) break;

            Vector3 snappedPos = GetSnappedPosition(cell);
            
            // Check if this new ghost would be valid (considering world obstacles AND previous ghosts)
            if (IsPositionValid(snappedPos, cell))
            {
                GameObject pGhost = CreateGhostInstance("PendingGhost");
                pGhost.transform.position = snappedPos;
                UpdateGhostColor(pGhost, true);

                pendingCells.Add(cell);
                pendingGhosts.Add(pGhost);
            }
        }
    }

    private List<Vector3Int> GetCellsInLine(Vector3Int start, Vector3Int end)
    {
        List<Vector3Int> cells = new List<Vector3Int>();
        
        if (isHorizontal)
        {
            int step = currentItemData.gridWidth;
            int minX = Mathf.Min(start.x, end.x);
            int maxX = Mathf.Max(start.x, end.x);
            
            // Ensure we step from the start position correctly
            for (int x = start.x; x <= maxX; x += step)
                cells.Add(new Vector3Int(x, start.y, 0));
            for (int x = start.x - step; x >= minX; x -= step)
                cells.Add(new Vector3Int(x, start.y, 0));
        }
        else
        {
            int step = currentItemData.gridHeight;
            int minY = Mathf.Min(start.y, end.y);
            int maxY = Mathf.Max(start.y, end.y);
            
            for (int y = start.y; y <= maxY; y += step)
                cells.Add(new Vector3Int(start.x, y, 0));
            for (int y = start.y - step; y >= minY; y -= step)
                cells.Add(new Vector3Int(start.x, y, 0));
        }

        return cells;
    }

    private void FinalizePlacement()
    {
        if (pendingCells.Count == 0) return;

        Debug.Log($"Finalizing placement of {pendingCells.Count} {currentItemData.itemName}(s)");

        foreach (var cell in pendingCells)
        {
            Vector3 pos = GetSnappedPosition(cell);

            // RE-VALIDATE: Ensure position is still clear before final instantiation
            if (!IsPositionValid(pos, cell, true))
            {
                Debug.Log($"Skipping placement at {cell}: Position is now occupied.");
                continue;
            }

            Instantiate(currentItemData.worldPrefab, pos, Quaternion.identity);
            
            currentSlot.amount--;
            if (currentSlot.amount <= 0)
            {
                currentSlot.item = null;
                currentSlot.amount = 0;
                break;
            }
        }

        if (inventoryUI != null && inventoryUI.inventory != null)
        {
            inventoryUI.inventory.OnInventoryChanged?.Invoke();
        }

        ClearPending();

        // If we ran out of items, exit placement mode
        if (currentSlot == null || currentSlot.item == null)
        {
            EndPlacement();
        }
    }

    private void ClearPending()
    {
        foreach (var g in pendingGhosts)
        {
            if (g != null) Destroy(g);
        }
        pendingGhosts.Clear();
        pendingCells.Clear();
    }

    private void UpdateGhostColor(GameObject ghost, bool isValid)
    {
        if (ghost == null) return;
        
        Color baseColor = isValid ? validColor : invalidColor;
        baseColor.a = ghostTransparency;

        SpriteRenderer[] renderers = ghost.GetComponentsInChildren<SpriteRenderer>();
        foreach (var sr in renderers)
        {
            sr.color = baseColor;
        }
    }

    private bool IsPositionValid(Vector3 pos, Vector3Int? cellToIgnore = null, bool worldOnly = false)
    {
        Vector2 size = GetItemSize();
        
        // 1. Check World Obstacles
        // We only consider colliders tagged as "Obstacle" to be blocking.
        // This prevents utility triggers (like the player's pickup range) from interfering.
        Collider2D[] hits = Physics2D.OverlapBoxAll(pos, size * 0.9f, 0, obstacleLayer);
        
        foreach (var hit in hits)
        {
            // Ignore the ghosts themselves
            if (hit.gameObject.name.Contains("Ghost")) continue;

            if (hit.CompareTag("Obstacle"))
            {
                return false;
            }
        }

        if (worldOnly) return true;

        // 2. Check Pending Ghosts (Logical Overlap)
        foreach (var pCell in pendingCells)
        {
            if (cellToIgnore.HasValue && pCell == cellToIgnore.Value) continue;

            Vector3 pPos = GetSnappedPosition(pCell);
            
            // Check if the footprints overlap based on their actual world size
            if (Mathf.Abs(pos.x - pPos.x) < size.x * 0.9f &&
                Mathf.Abs(pos.y - pPos.y) < size.y * 0.9f)
            {
                return false;
            }
        }

        return true;
    }

    private Vector2 GetItemSize()
    {
        if (currentItemData == null)
            return new Vector2(0.8f, 0.8f);

        // Return dimensions based on grid size
        float width = currentItemData.gridWidth * grid.cellSize.x;
        float height = currentItemData.gridHeight * grid.cellSize.y;
        
        return new Vector2(width, height);
    }

    public void EndPlacement()
    {
        Debug.Log("Ending placement mode.");
        isPlacing = false;
        currentItemData = null;
        currentSlot = null;

        if (gridVisualizer != null) gridVisualizer.SetVisible(false);

        ClearPending();

        if (ghostObject != null)
        {
            Destroy(ghostObject);
        }
    }
}
