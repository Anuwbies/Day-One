using UnityEngine;
using UnityEngine.UI;

public class CraftButtonConsume : MonoBehaviour
{
    public CraftingGridController craftingGrid;

    private Button button;

    private void Awake()
    {
        button = GetComponent<Button>();
        button.onClick.AddListener(OnCraftPressed);
    }

    private void OnDestroy()
    {
        button.onClick.RemoveListener(OnCraftPressed);
    }

    private void OnCraftPressed()
    {
        if (craftingGrid == null)
            return;

        InventoryUI inventoryUI = craftingGrid.inventoryUI;
        if (inventoryUI == null || inventoryUI.inventory == null)
            return;

        CraftingRecipe recipe = craftingGrid.GetCurrentRecipe();
        if (recipe == null)
            return;

        // =========================
        // SMART INVENTORY CHECK
        // =========================
        if (!inventoryUI.CanAcceptItem(
                recipe.resultItem,
                recipe.resultAmount))
            return;

        ConsumeIngredients(recipe);
        GiveResult(recipe);

        craftingGrid.UpdateResultPreview();
    }

    // =========================
    // CONSUME LOGIC
    // =========================

    private void ConsumeIngredients(CraftingRecipe recipe)
    {
        if (recipe.shapeless)
        {
            ConsumeShapeless(recipe);
            return;
        }

        foreach (CraftingIngredient ing in recipe.ingredients)
        {
            CraftingSlot slot =
                craftingGrid.craftingSlots[ing.slotIndex].slot;

            slot.amount -= ing.amount;

            if (slot.amount <= 0)
                slot.Clear();

            craftingGrid.craftingSlots[ing.slotIndex].Refresh();
        }
    }

    private void ConsumeShapeless(CraftingRecipe recipe)
    {
        foreach (CraftingIngredient ing in recipe.ingredients)
        {
            foreach (CraftingSlotUI slotUI in craftingGrid.craftingSlots)
            {
                CraftingSlot slot = slotUI.slot;

                if (slot.IsEmpty)
                    continue;

                if (slot.item == ing.item &&
                    slot.amount >= ing.amount)
                {
                    slot.amount -= ing.amount;

                    if (slot.amount <= 0)
                        slot.Clear();

                    slotUI.Refresh();
                    break;
                }
            }
        }
    }

    // =========================
    // GIVE RESULT
    // =========================

    private void GiveResult(CraftingRecipe recipe)
    {
        InventoryUI inventoryUI = craftingGrid.inventoryUI;
        if (inventoryUI == null || inventoryUI.inventory == null)
            return;

        var items = inventoryUI.inventory.items;
        ItemData item = recipe.resultItem;
        int remaining = recipe.resultAmount;

        // 1. Merge into existing stacks
        if (item.stackable)
        {
            for (int i = 0; i < items.Count && remaining > 0; i++)
            {
                InventorySlot slot = items[i];
                if (slot == null || slot.item != item)
                    continue;

                if (slot.amount >= item.maxStack)
                    continue;

                int space = item.maxStack - slot.amount;
                int add = Mathf.Min(space, remaining);

                slot.amount += add;
                remaining -= add;
            }
        }

        // 2. Place into empty slots
        while (remaining > 0)
        {
            int emptyIndex = items.FindIndex(
                s => s == null || s.item == null);

            if (emptyIndex == -1)
                break;

            int amountToPlace = item.stackable
                ? Mathf.Min(item.maxStack, remaining)
                : 1;

            items[emptyIndex] =
                new InventorySlot(item, amountToPlace);

            remaining -= amountToPlace;
        }

        inventoryUI.inventory.OnInventoryChanged?.Invoke();
    }
}
