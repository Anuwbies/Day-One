using UnityEngine;

public class CraftingGridController : MonoBehaviour
{
    public CraftingSlotUI[] craftingSlots;
    public InventoryUI inventoryUI;

    // Call this when the inventory/crafting UI is closed
    public void ReturnAllItemsToInventory()
    {
        if (inventoryUI == null ||
            inventoryUI.inventory == null ||
            inventoryUI.inventory.items == null)
            return;

        foreach (CraftingSlotUI craftSlotUI in craftingSlots)
        {
            if (craftSlotUI == null)
                continue;

            CraftingSlot slot = craftSlotUI.slot;

            if (slot == null || slot.IsEmpty)
                continue;

            // Find first empty inventory slot
            int emptyIndex = inventoryUI.inventory.items.FindIndex(i => i == null);

            if (emptyIndex == -1)
            {
                // Inventory full → do nothing for now
                // (We will improve this later)
                continue;
            }

            // Return item to inventory
            inventoryUI.inventory.items[emptyIndex] =
                new InventorySlot(slot.item, slot.amount);

            // Clear crafting slot
            craftSlotUI.Clear();
        }

        inventoryUI.inventory.OnInventoryChanged?.Invoke();
    }
}
