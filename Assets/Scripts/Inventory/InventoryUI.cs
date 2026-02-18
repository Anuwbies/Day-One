using System.Collections.Generic;
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
    public RectTransform craftPanel;

    [SerializeField]
    private CraftingGridController craftingGrid;

    [Header("Split UI")]
    public InventorySplitUI splitUI;

    [Header("Destroy UI")]
    public InventoryDestroyUI destroyUI;

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
        // If the game is paused, do not process hotkeys
        if (Time.timeScale == 0) return;

        if (Input.GetKeyDown(KeyCode.Tab))
        {
            SetInventoryOpen(!isOpen);
        }
    }

    public void TryAddSingleItem(int fromIndex, int toIndex)
    {
        if (inventory == null || inventory.items == null)
            return;

        if (fromIndex < 0 || toIndex < 0 ||
            fromIndex >= inventory.items.Count ||
            toIndex >= inventory.items.Count)
            return;

        InventorySlot target = inventory.items[toIndex];

        // Target slot must already exist (single-item drag only adds to valid slot)
        if (target == null || target.item == null)
            return;

        // Respect max stack
        if (!target.item.stackable ||
            target.amount >= target.item.maxStack)
            return;

        target.amount += 1;

        inventory.OnInventoryChanged?.Invoke();
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
        // DESTROY UI HAS TOP PRIORITY
        // =========================
        if (destroyUI != null && destroyUI.IsOpen)
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

        if (!IsPointerInsideSafeUI(Input.mousePosition))
        {
            SetInventoryOpen(false);
            ConsumeClickThisFrame = true;
        }
    }

    private bool IsPointerInsideSafeUI(Vector2 screenPosition)
    {
        Camera cam = canvas.renderMode == RenderMode.ScreenSpaceOverlay
            ? null
            : canvas.worldCamera;

        if (inventoryGrid != null &&
            RectTransformUtility.RectangleContainsScreenPoint(
                inventoryGrid, screenPosition, cam))
            return true;

        if (craftPanel != null &&
            RectTransformUtility.RectangleContainsScreenPoint(
                craftPanel, screenPosition, cam))
            return true;

        return false;
    }

    public void SetInventoryOpen(bool open)
    {
        isOpen = open;

        // If we are opening the inventory, cancel any active placement
        if (isOpen && PlacementManager.Instance != null && PlacementManager.Instance.IsPlacing)
        {
            PlacementManager.Instance.EndPlacement();
        }

        if (inventoryWindow != null)
            inventoryWindow.SetActive(isOpen);

        if (!isOpen)
        {
            if (contextMenu != null)
                contextMenu.Hide();

            if (splitUI != null && splitUI.IsOpen)
                splitUI.Cancel();

            if (destroyUI != null && destroyUI.IsOpen)
                destroyUI.Cancel();

            // =========================
            // RETURN CRAFTING ITEMS
            // =========================
            if (craftingGrid != null)
                craftingGrid.ReturnAllItemsToInventory();
        }

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

        if (craftingGrid != null)
            craftingGrid.UpdateResultPreview();
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

    // =========================
    // OPEN DESTROY UI
    // =========================
    public void OpenDestroyUI(InventorySlot slot, Vector2 screenPosition)
    {
        if (destroyUI == null || slot == null || slot.item == null)
            return;

        destroyUI.Show(this, slot, screenPosition);
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

                if (fromSlot.amount <= 0)
                    inventory.items[fromIndex] = null;

                inventory.OnInventoryChanged?.Invoke();
                return;
            }
        }

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

        return inventory.items.Exists(slot => slot == null || slot.item == null);
    }

    public void SplitSlot(InventorySlot sourceSlot, int splitAmount)
    {
        if (inventory == null || inventory.items == null || sourceSlot == null)
            return;

        if (splitAmount <= 0 || splitAmount >= sourceSlot.amount)
            return;

        int emptyIndex = inventory.items.FindIndex(slot => slot == null || slot.item == null);
        if (emptyIndex == -1)
        {
            Debug.Log("Cannot split: inventory is full.");
            return;
        }

        sourceSlot.amount -= splitAmount;
        inventory.items[emptyIndex] = new InventorySlot(sourceSlot.item, splitAmount);

        inventory.OnInventoryChanged?.Invoke();
    }

    public void DestroyItem(InventorySlot slot)
    {
        if (inventory == null || inventory.items == null || slot == null)
            return;

        int index = inventory.items.IndexOf(slot);
        if (index == -1)
            return;

        inventory.items[index] = null;
        inventory.OnInventoryChanged?.Invoke();
    }

    // =========================
    // DOUBLE CLICK COMBINE (LOW AMOUNT FIRST)
    // =========================
    public void CombineAllSameItems(int targetIndex)
    {
        if (inventory == null || inventory.items == null)
            return;

        if (targetIndex < 0 || targetIndex >= inventory.items.Count)
            return;

        InventorySlot targetSlot = inventory.items[targetIndex];
        if (targetSlot == null || targetSlot.item == null)
            return;

        ItemData item = targetSlot.item;

        if (!item.stackable)
            return;

        // Target already full → do nothing
        if (targetSlot.amount >= item.maxStack)
            return;

        int spaceLeft = item.maxStack - targetSlot.amount;

        // =========================
        // COLLECT VALID SOURCE STACKS
        // =========================
        List<int> sourceIndices = new List<int>();

        for (int i = 0; i < inventory.items.Count; i++)
        {
            if (i == targetIndex)
                continue;

            InventorySlot slot = inventory.items[i];
            if (slot == null || slot.item != item)
                continue;

            // Ignore already max stacks
            if (slot.amount >= item.maxStack)
                continue;

            sourceIndices.Add(i);
        }

        // =========================
        // SORT BY LOWEST AMOUNT FIRST
        // =========================
        sourceIndices.Sort((a, b) =>
            inventory.items[a].amount.CompareTo(inventory.items[b].amount)
        );

        // =========================
        // TRANSFER ITEMS
        // =========================
        foreach (int index in sourceIndices)
        {
            if (spaceLeft <= 0)
                break;

            InventorySlot source = inventory.items[index];
            if (source == null)
                continue;

            int transfer = Mathf.Min(spaceLeft, source.amount);

            targetSlot.amount += transfer;
            source.amount -= transfer;
            spaceLeft -= transfer;

            if (source.amount <= 0)
                inventory.items[index] = null;
        }

        inventory.OnInventoryChanged?.Invoke();
    }

    public bool CanAcceptItem(ItemData item, int amount)
    {
        if (inventory == null || inventory.items == null)
            return false;

        var items = inventory.items;
        int remaining = amount;

        // 1. Merge into stacks
        if (item.stackable)
        {
            foreach (var slot in items)
            {
                if (slot == null || slot.item != item)
                    continue;

                if (slot.amount >= item.maxStack)
                    continue;

                remaining -= (item.maxStack - slot.amount);
                if (remaining <= 0)
                    return true;
            }
        }

        // 2. Empty slots
        int emptySlots = items.FindAll(s => s == null || s.item == null).Count;

        if (!item.stackable)
            return emptySlots >= remaining;

        int stacksNeeded = Mathf.CeilToInt((float)remaining / item.maxStack);
        return emptySlots >= stacksNeeded;
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
