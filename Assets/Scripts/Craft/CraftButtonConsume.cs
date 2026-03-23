using UnityEngine;
using UnityEngine.UI;

public class CraftButtonConsume : MonoBehaviour
{
    public static System.Action<ItemData, int> AnyItemCrafted;

    public CraftingGridController craftingGrid;

    private Button button;
    private RepeatablePointerButton inputHandler;

    private void Awake()
    {
        button = GetComponent<Button>();
        // We will now use inputHandler instead of the simple button click
        
        inputHandler = gameObject.AddComponent<RepeatablePointerButton>();
        inputHandler.onLeftClick = () => TryCraft();
        inputHandler.onHoldAction = () => TryCraft();
        inputHandler.onRightClick = () => CraftAll();
    }

    private void TryCraft()
    {
        OnCraftPressed();
    }

    private void CraftAll()
    {
        // Keep crafting until it fails (no ingredients or full inventory)
        int safetyLimit = 999;
        while (OnCraftPressed() && safetyLimit > 0)
        {
            safetyLimit--;
        }
    }

    private bool OnCraftPressed()
    {
        if (craftingGrid == null)
            return false;

        InventoryUI inventoryUI = craftingGrid.inventoryUI;
        if (inventoryUI == null || inventoryUI.inventory == null)
            return false;

        CraftingRecipe recipe = craftingGrid.GetCurrentRecipe();
        CraftingIngredientSet set = craftingGrid.CurrentMatchedSet;

        if (recipe == null || set == null)
            return false;

        if (!inventoryUI.CanAcceptItem(
                recipe.resultItem,
                recipe.resultAmount))
            return false;

        ConsumeIngredientSet(set);
        GiveResult(recipe);
        NotifyCraftCompleted(recipe);

        craftingGrid.UpdateResultPreview();
        return true;
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
            CraftingSlotUI slotUI =
                craftingGrid.GetSlotUIForRecipeIndex(ing.slotIndex);

            if (slotUI == null)
                continue;

            CraftingSlot slot = slotUI.slot;

            slot.amount -= ing.amount;

            if (slot.amount <= 0)
                slot.Clear();

            slotUI.Refresh();
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

    private void NotifyCraftCompleted(CraftingRecipe recipe)
    {
        if (recipe == null)
            return;

        AnyItemCrafted?.Invoke(recipe.resultItem, Mathf.Max(1, recipe.resultAmount));
    }
}
