using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections.Generic;

public class PlacementManager : MonoBehaviour
{
    public static PlacementManager Instance { get; private set; }

    public bool IsPlacing => isPlacing;

    [Header("Settings")]
    public List<string> obstacleSortingLayers = new List<string> { "World", "Foreground" };
    public LayerMask obstacleLayer = -1; 
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
    private List<Vector3> pendingPositions = new List<Vector3>();
    private List<GameObject> pendingGhosts = new List<GameObject>();

    private Vector3Int startCell;
    private bool axisLocked = false;
    private bool isHorizontal = false;
    private bool canDrag = false;

    private float placementStartTime;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        // Initialize obstacleLayer if it hasn't been set in the inspector
        if (obstacleLayer == -1 || obstacleLayer == 0)
        {
            obstacleLayer = ~(1 << 2); // Everything except Ignore Raycast
        }

        // Create an unlit material for ghosts so they aren't affected by lighting (prevents being black)
        Shader ghostShader = Shader.Find("Sprites/Default");
        if (ghostShader != null) ghostMaterial = new Material(ghostShader);

        FindGrid();
    }

    private bool FindGrid()
    {
        if (grid != null) return true;

        grid = Object.FindAnyObjectByType<Grid>();
        if (grid != null)
        {
            gridVisualizer = grid.GetComponent<GridVisualizer>();
            if (gridVisualizer == null)
            {
                gridVisualizer = grid.gameObject.AddComponent<GridVisualizer>();
            }
            return true;
        }
        return false;
    }

    private void Update()
    {
        if (!isPlacing) return;

        // Try to find grid if missing (e.g. if world was generated at runtime)
        if (grid == null) FindGrid();

        // Force grid visibility if it was lost
        if (gridVisualizer != null && !gridVisualizer.isActiveAndEnabled && isPlacing && currentItemData.snapToGrid)
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
            // Only block placement if pointer is over UI AND we didn't just start placement in this frame
            // (prevents the context menu click from blocking the first placement click if they overlap)
            if (EventSystem.current.IsPointerOverGameObject() && Time.time > placementStartTime + 0.1f) 
            {
                Debug.Log("Placement blocked: Pointer is over UI.");
                return;
            }

            Vector3 mouseWorldPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            mouseWorldPos.z = 0;
            
            Debug.Log($"Mouse Down at {mouseWorldPos}. Checking validity...");
            
            // Clear previous pending to be sure
            ClearPending();
            
            if (currentItemData.snapToGrid)
            {
                if (grid == null)
                {
                    Debug.LogWarning("Placement failed: No Grid found in scene.");
                    return;
                }

                startCell = GetBottomLeftCell(mouseWorldPos);
                axisLocked = false;
                Vector3 snappedStartPos = GetSnappedPosition(startCell);
                canDrag = IsPositionValid(snappedStartPos, startCell, false, ghostObject);
                Debug.Log($"Grid placement initial check at {startCell}: canDrag={canDrag}");
            }
            else
            {
                // For non-grid, we don't really support "drag lines", so we just treat it as a single placement on click
                canDrag = IsPositionValid(mouseWorldPos, null, false, ghostObject);
                Debug.Log($"Free placement initial check at {mouseWorldPos}: canDrag={canDrag}");
            }

            if (canDrag)
            {
                ContinueDragPlacement();
            }
            else
            {
                Debug.Log("Placement invalid at click position. canDrag is false.");
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
                int count = currentItemData.snapToGrid ? pendingCells.Count : pendingPositions.Count;
                Debug.Log($"Mouse Up: Attempting FinalizePlacement. Pending count: {count}");
                FinalizePlacement();
            }
            else
            {
                Debug.Log("Mouse Up: canDrag was false, skipping Finalize.");
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
        placementStartTime = Time.time;

        FindGrid();

        if (gridVisualizer != null && currentItemData.snapToGrid) gridVisualizer.SetVisible(true);

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

        // Set layer recursively to Ignore Raycast (Layer 2) to avoid hitting other ghosts
        SetLayerRecursive(ghost, 2);

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

        // Disable all rigidbodies on the ghost to prevent them from falling or interacting
        Rigidbody2D[] rbs = ghost.GetComponentsInChildren<Rigidbody2D>();
        foreach (var rb in rbs)
        {
            rb.simulated = false;
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

    private void SetLayerRecursive(GameObject obj, int newLayer)
    {
        if (obj == null) return;
        obj.layer = newLayer;
        foreach (Transform child in obj.transform)
        {
            SetLayerRecursive(child.gameObject, newLayer);
        }
    }

    private void UpdateGhost()
    {
        if (ghostObject == null || grid == null || currentItemData == null) return;

        Vector3 mouseWorldPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        mouseWorldPos.z = 0;

        Vector3 finalPos;
        Vector3Int? cellPos = null;

        if (currentItemData.snapToGrid)
        {
            // Snapping logic: find the bottom-left anchor cell based on centered mouse
            Vector3Int currentCell = GetBottomLeftCell(mouseWorldPos);

            // If dragging, apply axis lock to the main tracking ghost too
            if (Input.GetMouseButton(0) && axisLocked)
            {
                if (isHorizontal) currentCell.y = startCell.y;
                else currentCell.x = startCell.x;
            }

            finalPos = GetSnappedPosition(currentCell);
            cellPos = currentCell;
        }
        else
        {
            finalPos = mouseWorldPos;
        }

        ghostObject.transform.position = finalPos;

        // Validation check for the main cursor ghost (Check world AND pending ghosts)
        bool isValid = IsPositionValid(finalPos, cellPos, false, ghostObject);
        UpdateGhostColor(ghostObject, isValid);

        // Hide mouse ghost if we are dragging and have reached the item limit, 
        // or if the current cell is already occupied by a pending ghost.
        if (Input.GetMouseButton(0) && currentSlot != null)
        {
            bool alreadyPending = currentItemData.snapToGrid ? pendingCells.Contains(cellPos.Value) : pendingPositions.Contains(finalPos);
            bool isFull = (currentItemData.snapToGrid ? pendingCells.Count : pendingPositions.Count) >= currentSlot.amount;
            ghostObject.SetActive(!alreadyPending && !isFull);
        }
        else
        {
            ghostObject.SetActive(true);
        }

        // VISUAL FEEDBACK: Update colors of all pending ghosts
        if (currentItemData.snapToGrid)
        {
            for (int i = 0; i < pendingCells.Count; i++)
            {
                Vector3 pPos = GetSnappedPosition(pendingCells[i]);
                UpdateGhostColor(pendingGhosts[i], IsPositionValid(pPos, pendingCells[i], true, pendingGhosts[i]));
            }
        }
        else
        {
            for (int i = 0; i < pendingPositions.Count; i++)
            {
                UpdateGhostColor(pendingGhosts[i], IsPositionValid(pendingPositions[i], null, true, pendingGhosts[i]));
            }
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

        if (currentItemData.snapToGrid)
        {
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
                
                GameObject pGhost = CreateGhostInstance("PendingGhost");
                pGhost.transform.position = snappedPos;

                // Check if this new ghost is valid (considering world obstacles AND previous ghosts)
                if (IsPositionValid(snappedPos, cell, false, pGhost))
                {
                    UpdateGhostColor(pGhost, true);
                    pendingCells.Add(cell);
                    pendingGhosts.Add(pGhost);
                    Debug.Log($"Added pending ghost at cell {cell}. Total pending: {pendingCells.Count}");
                }
                else
                {
                    Debug.Log($"Failed to add pending ghost at cell {cell}: Invalid position.");
                    Destroy(pGhost);
                }
            }
        }
        else
        {
            // Non-grid placement: Just add single pending ghost at current mouse if valid and not too close to others
            if (pendingPositions.Count < currentSlot.amount)
            {
                // To prevent spamming ghosts on top of each other while dragging
                bool tooClose = false;
                foreach (var pos in pendingPositions)
                {
                    if (Vector3.Distance(mouseWorldPos, pos) < 0.5f) { tooClose = true; break; }
                }

                if (!tooClose)
                {
                    GameObject pGhost = CreateGhostInstance("PendingGhost");
                    pGhost.transform.position = mouseWorldPos;

                    if (IsPositionValid(mouseWorldPos, null, false, pGhost))
                    {
                        UpdateGhostColor(pGhost, true);
                        pendingPositions.Add(mouseWorldPos);
                        pendingGhosts.Add(pGhost);
                        Debug.Log($"Added pending ghost at free position {mouseWorldPos}. Total pending: {pendingPositions.Count}");
                    }
                    else
                    {
                        Debug.Log($"Failed to add pending ghost at free position {mouseWorldPos}: Invalid position.");
                        Destroy(pGhost);
                    }
                }
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
        int count = currentItemData.snapToGrid ? pendingCells.Count : pendingPositions.Count;
        if (count == 0) 
        {
            Debug.Log("FinalizePlacement: No pending positions/cells to place.");
            return;
        }

        Debug.Log($"Finalizing placement of {count} {currentItemData.itemName}(s)");

        if (currentItemData.snapToGrid)
        {
            for (int i = 0; i < pendingCells.Count; i++)
            {
                Vector3 pos = GetSnappedPosition(pendingCells[i]);
                PlaceItem(pos);
            }
        }
        else
        {
            for (int i = 0; i < pendingPositions.Count; i++)
            {
                PlaceItem(pendingPositions[i]);
            }
        }

        if (inventoryUI != null && inventoryUI.inventory != null)
        {
            inventoryUI.inventory.OnInventoryChanged?.Invoke();
        }

        ClearPending();

        // If we ran out of items, exit placement mode
        if (currentSlot == null || currentSlot.item == null || currentSlot.amount <= 0)
        {
            Debug.Log("Ran out of items, ending placement mode.");
            EndPlacement();
        }
    }

    private void PlaceItem(Vector3 pos)
    {
        Debug.Log($"Instantiating {currentItemData.itemName} at {pos}");
        GameObject go = Instantiate(currentItemData.worldPrefab, pos, Quaternion.identity);
        
        // Ensure the placed item has its data and amount set
        Item worldItem = go.GetComponent<Item>();
        if (worldItem != null)
        {
            worldItem.data = currentItemData;
            worldItem.amount = 1;
        }

        ConsumeItem();
    }

    private void ConsumeItem()
    {
        if (currentSlot == null) return;
        currentSlot.amount--;
        Debug.Log($"Consumed 1 {currentItemData.itemName}. Remaining: {currentSlot.amount}");
        if (currentSlot.amount <= 0)
        {
            currentSlot.item = null;
            currentSlot.amount = 0;
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
        pendingPositions.Clear();
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

    private bool IsPositionValid(Vector3 pos, Vector3Int? cellToIgnore = null, bool worldOnly = false, GameObject ghost = null)
    {
        if (currentItemData == null) return false;

        bool collisionFound = false;
        Collider2D obstacleCollider = null;
        string gName = ghost != null ? ghost.name : "None";

        if (ghost != null)
        {
            // Try to find a collider tagged "Obstacle" on the ghost
            Collider2D[] ghostColliders = ghost.GetComponentsInChildren<Collider2D>(true);
            foreach (var col in ghostColliders)
            {
                if (col.CompareTag("Obstacle"))
                {
                    obstacleCollider = col;
                    break;
                }
            }
        }

        // Sync transforms before any collision check to ensure newly moved ghosts are accurate in physics
        Physics2D.SyncTransforms();

        if (obstacleCollider != null)
        {
            // Sync ghost position to the test position temporarily for the check
            Vector3 prevPos = ghost.transform.position;
            ghost.transform.position = pos;

            bool originalEnabled = obstacleCollider.enabled;
            obstacleCollider.enabled = true;

            ContactFilter2D filter = new ContactFilter2D();
            filter.useTriggers = true; // Include trigger-based obstacles
            filter.SetLayerMask(obstacleLayer);

            List<Collider2D> results = new List<Collider2D>();
            int count = Physics2D.OverlapCollider(obstacleCollider, filter, results);

            obstacleCollider.enabled = originalEnabled;
            ghost.transform.position = prevPos;

            for (int i = 0; i < count; i++)
            {
                // 1. Skip self and ANY part of this ghost's hierarchy
                if (results[i].gameObject == ghost || results[i].transform.IsChildOf(ghost.transform)) continue;
                
                // 2. Extremely robust ghost check: check root name and layer
                Transform root = results[i].transform.root;
                if (root.name.Contains("Ghost") || results[i].gameObject.layer == 2) 
                {
                    continue;
                }

                if (results[i].CompareTag("Obstacle"))
                {
                    Debug.Log($"IsPositionValid({gName}): VALID COLLISION with Obstacle: {results[i].name} (Root: {root.name})");
                    collisionFound = true;
                    break;
                }
            }
        }
        else
        {
            // Fallback to box check based on grid size
            Vector2 size = GetItemSize();
            Collider2D[] hits = Physics2D.OverlapBoxAll(pos, size * 0.9f, 0, obstacleLayer);
            foreach (var hit in hits)
            {
                if (ghost != null && (hit.gameObject == ghost || hit.transform.IsChildOf(ghost.transform))) continue;
                
                Transform root = hit.transform.root;
                if (root.name.Contains("Ghost") || hit.gameObject.layer == 2) 
                {
                    continue;
                }

                if (hit.CompareTag("Obstacle"))
                {
                    Debug.Log($"IsPositionValid({gName} Box): VALID COLLISION with Obstacle: {hit.name} (Root: {root.name})");
                    collisionFound = true;
                    break;
                }
            }
        }

        if (collisionFound) return false;
        if (worldOnly) return true;

        Vector2 logicalSize = GetItemSize();
        // 2. Check Pending Ghosts (Logical Overlap)
        if (currentItemData.snapToGrid)
        {
            foreach (var pCell in pendingCells)
            {
                if (cellToIgnore.HasValue && pCell == cellToIgnore.Value) continue;
                Vector3 pPos = GetSnappedPosition(pCell);
                if (Mathf.Abs(pos.x - pPos.x) < logicalSize.x * 0.9f && Mathf.Abs(pos.y - pPos.y) < logicalSize.y * 0.9f)
                {
                    Debug.Log($"IsPositionValid({gName}): Blocked by pending grid cell.");
                    return false;
                }
            }
        }
        else
        {
            foreach (var pPos in pendingPositions)
            {
                if (Vector3.Distance(pos, pPos) < 0.1f) continue; // Same ghost
                if (Mathf.Abs(pos.x - pPos.x) < logicalSize.x * 0.9f && Mathf.Abs(pos.y - pPos.y) < logicalSize.y * 0.9f)
                {
                    Debug.Log($"IsPositionValid({gName}): Blocked by pending position.");
                    return false;
                }
            }
        }

        return true;
    }

    private Vector2 GetItemSize()
    {
        if (currentItemData == null)
            return new Vector2(0.8f, 0.8f);

        // If grid is missing, try to find it again
        if (grid == null) FindGrid();

        if (grid == null)
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
