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
        CraftingIngredientSet set = craftingGrid.CurrentMatchedSet;

        if (recipe == null || set == null)
            return;

        if (!inventoryUI.CanAcceptItem(
                recipe.resultItem,
                recipe.resultAmount))
            return;

        ConsumeIngredientSet(set);
        GiveResult(recipe);

        craftingGrid.UpdateResultPreview();
    }

    // =========================
    // CONSUME INGREDIENT SET
    // =========================

    private void ConsumeIngredientSet(CraftingIngredientSet set)
    {
        if (set.shapeless)
            ConsumeShapeless(set);
        else
            ConsumeShaped(set);
    }

    private void ConsumeShaped(CraftingIngredientSet set)
    {
        foreach (CraftingIngredient ing in set.ingredients)
        {
            CraftingSlot slot =
                craftingGrid.craftingSlots[ing.slotIndex].slot;

            slot.amount -= ing.amount;

            if (slot.amount <= 0)
                slot.Clear();

            craftingGrid.craftingSlots[ing.slotIndex].Refresh();
        }
    }

    private void ConsumeShapeless(CraftingIngredientSet set)
    {
        foreach (CraftingIngredient ing in set.ingredients)
        {
            int remaining = ing.amount;

            foreach (CraftingSlotUI slotUI in craftingGrid.craftingSlots)
            {
                if (remaining <= 0)
                    break;

                CraftingSlot slot = slotUI.slot;

                if (slot.IsEmpty || slot.item != ing.item)
                    continue;

                int used = Mathf.Min(slot.amount, remaining);
                slot.amount -= used;
                remaining -= used;

                if (slot.amount <= 0)
                    slot.Clear();

                slotUI.Refresh();
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
