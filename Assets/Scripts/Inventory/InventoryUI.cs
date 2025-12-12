using UnityEngine;

public class InventoryUI : MonoBehaviour
{
    public PlayerInventory inventory;
    public InventorySlotUI[] slots;          // UI slot components (one per visual cell)
    public GameObject inventoryWindow;
    public HotbarUI hotbar;

    private bool isOpen = false;

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
        if (Input.GetKeyDown(KeyCode.I))
        {
            isOpen = !isOpen;

            if (inventoryWindow != null)
                inventoryWindow.SetActive(isOpen);

            if (isOpen)
                RefreshUI();
        }
    }

    private void SetupSlotIndices()
    {
        if (slots == null) return;

        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] == null) continue;

            var slotGO = slots[i].gameObject;

            var existing = slotGO.GetComponent<InventorySlotDragDrop>();
            if (existing == null)
            {
                var drag = slotGO.AddComponent<InventorySlotDragDrop>();
                drag.slotIndex = i;
                drag.inventoryUI = this;
            }
            else
            {
                existing.slotIndex = i;
                existing.inventoryUI = this;
            }
        }
    }

    public void RefreshUI()
    {
        if (inventory == null)
        {
            Debug.LogWarning("InventoryUI: Inventory reference is missing.");
            return;
        }

        if (inventory.items == null)
        {
            Debug.LogWarning("InventoryUI: Inventory.items list is null.");
            return;
        }

        if (slots == null || slots.Length == 0)
        {
            Debug.LogWarning("InventoryUI: No UI slots assigned.");
            return;
        }

        int count = Mathf.Min(slots.Length, inventory.items.Count);

        for (int i = 0; i < count; i++)
        {
            var slotUI = slots[i];

            if (slotUI == null)
                continue;

            var invItem = inventory.items[i];

            if (invItem != null && invItem.item != null)
            {
                slotUI.SetSlot(invItem.item.icon, invItem.amount);
            }
            else
            {
                slotUI.ClearSlot();
            }
        }

        if (hotbar != null)
            hotbar.Refresh();
    }

    public void SwapOrMove(int from, int to)
    {
        if (inventory == null || inventory.items == null)
            return;

        var items = inventory.items;

        if (from < 0 || from >= items.Count) return;
        if (to < 0 || to >= items.Count) return;

        if (items[to] == null)
        {
            items[to] = items[from];
            items[from] = null;
        }
        else
        {
            var temp = items[from];
            items[from] = items[to];
            items[to] = temp;
        }

        inventory.OnInventoryChanged?.Invoke();
    }
}
