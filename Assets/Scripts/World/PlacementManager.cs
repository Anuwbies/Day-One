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

        UpdateGhost();

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
        ghostRenderer = ghostObject.GetComponent<SpriteRenderer>();

        // Hide UI
        if (inventoryUI != null)
        {
            inventoryUI.inventoryWindow.SetActive(false);
        }
    }

    private GameObject CreateGhostInstance(string name)
    {
        GameObject ghost = new GameObject(name);
        SpriteRenderer sr = ghost.AddComponent<SpriteRenderer>();

        // Use the icon or a sprite from the prefab
        SpriteRenderer prefabRenderer = currentItemData.worldPrefab.GetComponentInChildren<SpriteRenderer>();
        sr.sprite = prefabRenderer != null ? prefabRenderer.sprite : currentItemData.icon;

        sr.sortingLayerName = "UI";
        sr.sortingOrder = 1000;
        
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

        // Validation check for the main cursor ghost
        bool isValid = IsPositionValid(snappedPos) && !pendingCells.Contains(cellPos);
        UpdateGhostColor(ghostRenderer, isValid);

        // VISUAL FEEDBACK: Update colors of all pending ghosts in case an entity moved onto them
        for (int i = 0; i < pendingCells.Count; i++)
        {
            Vector3 pPos = grid.GetCellCenterWorld(pendingCells[i]);
            SpriteRenderer sr = pendingGhosts[i].GetComponent<SpriteRenderer>();
            UpdateGhostColor(sr, IsPositionValid(pPos));
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
            // If not locked yet, only the start cell is valid
            currentCell = startCell;
        }

        // Calculate the range of cells from start to current
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

        // 2. Add ghosts for new cells in the line (respecting stack size)
        foreach (Vector3Int cell in desiredCells)
        {
            if (pendingCells.Contains(cell)) continue;
            
            // Check if we have enough items left in the stack
            if (pendingCells.Count >= currentSlot.amount) break;

            Vector3 snappedPos = grid.GetCellCenterWorld(cell);
            if (IsPositionValid(snappedPos))
            {
                GameObject pGhost = CreateGhostInstance("PendingGhost");
                pGhost.transform.position = snappedPos;
                UpdateGhostColor(pGhost.GetComponent<SpriteRenderer>(), true);

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
            if (!IsPositionValid(pos))
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

    private void UpdateGhostColor(SpriteRenderer sr, bool isValid)
    {
        if (sr == null) return;
        
        Color baseColor = isValid ? validColor : invalidColor;
        baseColor.a = ghostTransparency;
        sr.color = baseColor;
    }

    private bool IsPositionValid(Vector3 pos)
    {
        Collider2D hit = Physics2D.OverlapBox(pos, new Vector2(0.8f, 0.8f), 0, obstacleLayer);
        return hit == null;
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
