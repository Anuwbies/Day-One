using UnityEngine;
using UnityEngine.EventSystems;

public class InventoryUI : MonoBehaviour
{
    [Header("Inventory")]
    public PlayerInventory inventory;
    public InventorySlotUI[] slots;
    public GameObject inventoryWindow;
    public HotbarUI hotbar;

    [Header("Context Menu")]
    public InventoryItemContextMenu contextMenu;

    [Header("World Drop")]
    public Transform dropOrigin;
    public Vector2 dropOriginOffset;
    public Vector2 dropRadiusXY = new Vector2(0.5f, 0.25f);

    [Header("UI")]
    public RectTransform inventoryGrid;

    [Header("Split UI")]
    public InventorySplitUI splitUI;

    private bool isOpen = false;
    private Canvas canvas;

    public bool IsOpen => isOpen;
    public bool ConsumeClickThisFrame { get; private set; }

    private void Awake()
    {
        canvas = GetComponentInParent<Canvas>();
    }

    private void Start()
    {
        if (inventoryWindow != null)
            inventoryWindow.SetActive(false);

        if (inventory != null)
            inventory.OnInventoryChanged += RefreshUI;

        SetupSlotIndices();
        RefreshUI();
    }

    private void Update()
    {
        ConsumeClickThisFrame = false;

        HandleToggleKey();
        HandleClickOutside();
    }

    private void HandleToggleKey()
    {
        if (Input.GetKeyDown(KeyCode.I))
        {
            SetOpen(!isOpen);
        }
    }

    private void HandleClickOutside()
    {
        if (!isOpen)
            return;

        if (!Input.GetMouseButtonDown(0))
            return;

        // =========================
        // SPLIT UI HAS TOP PRIORITY
        // =========================
        if (splitUI != null && splitUI.IsOpen)
        {
            ConsumeClickThisFrame = true;
            return;
        }

        // =========================
        // CONTEXT MENU HAS PRIORITY
        // =========================
        if (contextMenu != null && contextMenu.IsOpen)
        {
            ConsumeClickThisFrame = true;
            return;
        }

        if (inventoryGrid == null)
            return;

        bool insideInventory = RectTransformUtility.RectangleContainsScreenPoint(
            inventoryGrid,
            Input.mousePosition,
            canvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null
                : canvas.worldCamera
        );

        if (!insideInventory)
        {
            SetOpen(false);
            ConsumeClickThisFrame = true;
        }
    }

    private void SetOpen(bool open)
    {
        isOpen = open;

        if (inventoryWindow != null)
            inventoryWindow.SetActive(isOpen);

        if (!isOpen && contextMenu != null)
            contextMenu.Hide();

        if (isOpen)
            RefreshUI();
    }

    private void SetupSlotIndices()
    {
        if (slots == null)
            return;

        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] == null)
                continue;

            // Drag & Drop
            var drag = slots[i].GetComponent<InventorySlotDragDrop>();
            if (drag == null)
                drag = slots[i].gameObject.AddComponent<InventorySlotDragDrop>();

            drag.slotIndex = i;
            drag.inventoryUI = this;

            // Right-click
            var rightClick = slots[i].GetComponent<InventorySlotRightClick>();
            if (rightClick == null)
                rightClick = slots[i].gameObject.AddComponent<InventorySlotRightClick>();

            rightClick.slotIndex = i;
            rightClick.inventoryUI = this;
        }
    }

    public void RefreshUI()
    {
        if (inventory == null || inventory.items == null || slots == null)
            return;

        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] == null)
                continue;

            if (i < inventory.items.Count &&
                inventory.items[i] != null &&
                inventory.items[i].item != null)
            {
                slots[i].SetSlot(
                    inventory.items[i].item.icon,
                    inventory.items[i].amount
                );
            }
            else
            {
                slots[i].ClearSlot();
            }
        }

        if (hotbar != null)
            hotbar.Refresh();
    }

    public void OpenContextMenu(int slotIndex, Vector2 screenPosition)
    {
        if (contextMenu == null || inventory == null)
            return;

        if (slotIndex < 0 || slotIndex >= inventory.items.Count)
            return;

        InventorySlot slot = inventory.items[slotIndex];
        if (slot == null || slot.item == null)
            return;

        contextMenu.Show(this, slot, screenPosition);
    }

    public void SwapOrMove(int from, int to)
    {
        if (inventory == null || inventory.items == null)
            return;

        if (from < 0 || from >= inventory.items.Count) return;
        if (to < 0 || to >= inventory.items.Count) return;

        var temp = inventory.items[from];
        inventory.items[from] = inventory.items[to];
        inventory.items[to] = temp;

        inventory.OnInventoryChanged?.Invoke();
    }

    public void DropItemFromSlot(int slotIndex)
    {
        if (inventory == null ||
            inventory.items == null ||
            slotIndex < 0 ||
            slotIndex >= inventory.items.Count)
            return;

        var invSlot = inventory.items[slotIndex];
        if (invSlot == null || invSlot.item == null)
            return;

        ItemData data = invSlot.item;

        if (data.worldPrefab == null)
        {
            Debug.LogError($"Item '{data.itemName}' has no worldPrefab assigned.");
            return;
        }

        Vector3 baseOrigin =
            (dropOrigin != null ? dropOrigin.position : Vector3.zero) +
            new Vector3(dropOriginOffset.x, dropOriginOffset.y, 0f);

        Vector2 randomUnit = Random.insideUnitCircle;
        Vector2 randomOffset = new Vector2(
            randomUnit.x * dropRadiusXY.x,
            randomUnit.y * dropRadiusXY.y
        );

        GameObject go = Instantiate(
            data.worldPrefab,
            baseOrigin + new Vector3(randomOffset.x, randomOffset.y, 0f),
            Quaternion.identity
        );

        Item worldItem = go.GetComponent<Item>();
        if (worldItem != null)
        {
            worldItem.data = data;
            worldItem.amount = invSlot.amount;
        }

        inventory.items[slotIndex] = null;
        inventory.OnInventoryChanged?.Invoke();
    }

    public void TryMergeOrSwap(int fromIndex, int toIndex)
    {
        if (inventory == null || inventory.items == null)
            return;

        if (fromIndex < 0 || toIndex < 0 ||
            fromIndex >= inventory.items.Count ||
            toIndex >= inventory.items.Count)
            return;

        InventorySlot fromSlot = inventory.items[fromIndex];
        InventorySlot toSlot = inventory.items[toIndex];

        if (fromSlot == null || toSlot == null)
        {
            SwapOrMove(fromIndex, toIndex);
            return;
        }

        // Same item & stackable → try merge
        if (fromSlot.item == toSlot.item &&
            fromSlot.item.stackable)
        {
            int maxStack = fromSlot.item.maxStack;
            int spaceLeft = maxStack - toSlot.amount;

            if (spaceLeft > 0)
            {
                int transferAmount = Mathf.Min(spaceLeft, fromSlot.amount);

                toSlot.amount += transferAmount;
                fromSlot.amount -= transferAmount;

                // Remove source slot if empty
                if (fromSlot.amount <= 0)
                    inventory.items[fromIndex] = null;

                inventory.OnInventoryChanged?.Invoke();
                return;
            }
        }

        // Otherwise fallback to swap
        SwapOrMove(fromIndex, toIndex);
    }

    public void DropSlot(InventorySlot slot)
    {
        if (inventory == null || inventory.items == null || slot == null)
            return;

        int index = inventory.items.IndexOf(slot);
        if (index == -1)
            return;

        DropItemFromSlot(index);
    }

    public bool HasEmptySlot()
    {
        if (inventory == null || inventory.items == null)
            return false;

        return inventory.items.Exists(slot => slot == null);
    }

    public void SplitSlot(InventorySlot sourceSlot, int splitAmount)
    {
        if (inventory == null || inventory.items == null || sourceSlot == null)
            return;

        if (splitAmount <= 0 || splitAmount >= sourceSlot.amount)
            return;

        // Find first empty slot
        int emptyIndex = inventory.items.FindIndex(slot => slot == null);

        // No empty slot → cannot split
        if (emptyIndex == -1)
        {
            Debug.Log("Cannot split: inventory is full.");
            return;
        }

        // Reduce original stack
        sourceSlot.amount -= splitAmount;

        // Create new stack
        InventorySlot newSlot = new InventorySlot(sourceSlot.item, splitAmount);

        // Place in empty slot
        inventory.items[emptyIndex] = newSlot;

        inventory.OnInventoryChanged?.Invoke();
    }

    public void DestroySlot(InventorySlot slot)
    {
        if (inventory == null || inventory.items == null || slot == null)
            return;

        if (!inventory.items.Contains(slot))
            return;

        inventory.items.Remove(slot);
        inventory.OnInventoryChanged?.Invoke();
    }

    private void OnDrawGizmosSelected()
    {
        if (dropOrigin == null)
            return;

        Vector3 center =
            dropOrigin.position +
            new Vector3(dropOriginOffset.x, dropOriginOffset.y, 0f);

        Gizmos.color = Color.yellow;

        const int segments = 32;
        Vector3 prevPoint = center + new Vector3(dropRadiusXY.x, 0f, 0f);

        for (int i = 1; i <= segments; i++)
        {
            float angle = (i / (float)segments) * Mathf.PI * 2f;
            Vector3 nextPoint = center + new Vector3(
                Mathf.Cos(angle) * dropRadiusXY.x,
                Mathf.Sin(angle) * dropRadiusXY.y,
                0f
            );

            Gizmos.DrawLine(prevPoint, nextPoint);
            prevPoint = nextPoint;
        }

        Gizmos.color = Color.red;
        Gizmos.DrawSphere(center, 0.05f);
    }
}
