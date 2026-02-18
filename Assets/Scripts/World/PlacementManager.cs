using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections.Generic;

public class PlacementManager : MonoBehaviour
{
    public static PlacementManager Instance { get; private set; }

    public bool IsPlacing => isPlacing;

    [Header("Settings")]
    public LayerMask obstacleLayer;
    [Range(0f, 1f)]
    public float ghostTransparency = 0.5f;
    public Color validColor = Color.green;
    public Color invalidColor = Color.red;

    private Grid grid;
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

        // Find the grid in the scene
        grid = Object.FindAnyObjectByType<Grid>();
    }

    private void Update()
    {
        if (!isPlacing) return;

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
            startCell = grid.WorldToCell(mouseWorldPos);
            axisLocked = false;
            
            // Clear previous pending to be sure
            ClearPending();
            
            // Check if start position is valid before allowing drag
            Vector3 snappedStartPos = grid.GetCellCenterWorld(startCell);
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
        // GetComponentsInChildren<MonoBehaviour> only returns user scripts, 
        // not internal components like Transform, SpriteRenderer, or Collider2D.
        MonoBehaviour[] scripts = ghost.GetComponentsInChildren<MonoBehaviour>();
        foreach (var script in scripts)
        {
            DestroyImmediate(script);
        }

        // Set all renderers to transparent and on top
        SpriteRenderer[] renderers = ghost.GetComponentsInChildren<SpriteRenderer>();
        foreach (var sr in renderers)
        {
            sr.sortingLayerName = "UI";
            sr.sortingOrder = 1000;
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
        if (ghostObject == null || grid == null) return;

        Vector3 mouseWorldPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        mouseWorldPos.z = 0;

        Vector3Int cellPos = grid.WorldToCell(mouseWorldPos);

        // If dragging, apply axis lock to the main tracking ghost too
        if (Input.GetMouseButton(0))
        {
            if (axisLocked)
            {
                if (isHorizontal) cellPos.y = startCell.y;
                else cellPos.x = startCell.x;
            }
        }

        Vector3 snappedPos = grid.GetCellCenterWorld(cellPos);
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
            Vector3 pPos = grid.GetCellCenterWorld(pendingCells[i]);
            UpdateGhostColor(pendingGhosts[i], IsPositionValid(pPos, pendingCells[i], true));
        }
    }

    private void ContinueDragPlacement()
    {
        if (grid == null || currentSlot == null) return;

        Vector3 mouseWorldPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        mouseWorldPos.z = 0;
        Vector3Int currentCell = grid.WorldToCell(mouseWorldPos);

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

            Vector3 snappedPos = grid.GetCellCenterWorld(cell);
            
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
            int minX = Mathf.Min(start.x, end.x);
            int maxX = Mathf.Max(start.x, end.x);
            for (int x = minX; x <= maxX; x++)
                cells.Add(new Vector3Int(x, start.y, 0));
        }
        else
        {
            int minY = Mathf.Min(start.y, end.y);
            int maxY = Mathf.Max(start.y, end.y);
            for (int y = minY; y <= maxY; y++)
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
            Vector3 pos = grid.GetCellCenterWorld(cell);

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
        
        // 1. Check World Obstacles (Physics)
        Collider2D hit = Physics2D.OverlapBox(pos, size * 0.95f, 0, obstacleLayer);
        if (hit != null) return false;

        if (worldOnly) return true;

        // 2. Check Pending Ghosts (Logical Overlap)
        foreach (var pCell in pendingCells)
        {
            if (cellToIgnore.HasValue && pCell == cellToIgnore.Value) continue;

            Vector3 pPos = grid.GetCellCenterWorld(pCell);
            
            // Check if the footprints overlap based on their actual world size
            if (Mathf.Abs(pos.x - pPos.x) < size.x * 0.95f &&
                Mathf.Abs(pos.y - pPos.y) < size.y * 0.95f)
            {
                return false;
            }
        }

        return true;
    }

    private Vector2 GetItemSize()
    {
        if (currentItemData == null || currentItemData.worldPrefab == null)
            return new Vector2(0.8f, 0.8f);

        Vector3 scale = currentItemData.worldPrefab.transform.localScale;

        // Find the footprint box
        BoxCollider2D box = currentItemData.worldPrefab.GetComponentInChildren<BoxCollider2D>();
        if (box != null) 
        {
            // Multiply collider size by transform scale to get the real world width/height
            return new Vector2(box.size.x * scale.x, box.size.y * scale.y);
        }

        // Fallback to sprite size if no collider
        SpriteRenderer sr = currentItemData.worldPrefab.GetComponentInChildren<SpriteRenderer>();
        if (sr != null && sr.sprite != null)
        {
            return new Vector2(sr.sprite.bounds.size.x * scale.x, sr.sprite.bounds.size.y * scale.y);
        }

        return new Vector2(0.8f, 0.8f);
    }

    public void EndPlacement()
    {
        Debug.Log("Ending placement mode.");
        isPlacing = false;
        currentItemData = null;
        currentSlot = null;

        ClearPending();

        if (ghostObject != null)
        {
            Destroy(ghostObject);
        }
    }
}
