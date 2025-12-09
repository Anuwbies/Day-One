using UnityEngine;

public class InventoryUI : MonoBehaviour
{
    public PlayerInventory inventory;
    public InventorySlotUI[] slots;          // UI slot components (one per visual cell)
    public GameObject inventoryWindow;
    public HotbarUI hotbar;                  // assign via Inspector (used to refresh hotbar view)

    private bool isOpen = false;

    private void Start()
    {
        if (inventoryWindow != null)
            inventoryWindow.SetActive(false);

        if (inventory != null)
            inventory.OnInventoryChanged += RefreshUI;

        SetupSlotIndices(); // attach drag components (if not already present)
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
        for (int i = 0; i < slots.Length; i++)
        {
            var slotGO = slots[i].gameObject;
            // Attach InventorySlotDragDrop if not present (prevents duplicate component error)
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
        for (int i = 0; i < slots.Length; i++)
        {
            if (inventory.items[i] != null)
            {
                var slot = inventory.items[i];
                slots[i].SetSlot(slot.item.icon, slot.amount);
            }
            else
            {
                slots[i].ClearSlot();
            }
        }

        if (hotbar != null)
            hotbar.Refresh();
    }


    /// <summary>
    /// Move item at index 'from' to 'to' (insert) when destination empty, or swap if destination occupied.
    /// This method correctly accounts for index shifting when removing before inserting.
    /// </summary>
    public void SwapOrMove(int from, int to)
    {
        var items = inventory.items;

        if (from < 0 || from >= items.Count) return;
        if (to < 0 || to >= items.Count) return;

        // Move into empty slot
        if (items[to] == null)
        {
            items[to] = items[from];
            items[from] = null;
        }
        else
        {
            // Swap
            var temp = items[from];
            items[from] = items[to];
            items[to] = temp;
        }

        inventory.OnInventoryChanged?.Invoke();
    }
}
