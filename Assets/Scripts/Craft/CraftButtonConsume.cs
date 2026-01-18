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
        if (inventoryUI == null ||
            inventoryUI.inventory == null ||
            !inventoryUI.HasEmptySlot())
        {
            // Inventory full → do nothing
            return;
        }

        CraftingRecipe recipe = craftingGrid.GetCurrentRecipe();
        if (recipe == null)
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

    private void GiveResult(CraftingRecipe recipe)
    {
        InventoryUI inventoryUI = craftingGrid.inventoryUI;
        if (inventoryUI == null || inventoryUI.inventory == null)
            return;

        int emptyIndex =
            inventoryUI.inventory.items.FindIndex(i => i == null);

        if (emptyIndex == -1)
            return;

        inventoryUI.inventory.items[emptyIndex] =
            new InventorySlot(recipe.resultItem, recipe.resultAmount);

        inventoryUI.inventory.OnInventoryChanged?.Invoke();
    }
}