using UnityEngine;
using System.Collections.Generic;

public class CraftingGridController : MonoBehaviour
{
    [Header("Grid (0–8, top-left → bottom-right)")]
    public CraftingSlotUI[] craftingSlots;

    [Header("Recipes")]
    public CraftingDatabase craftingDatabase;

    [Header("Result Preview")]
    public CraftingResultUI resultUI;

    [Header("Inventory")]
    public InventoryUI inventoryUI;

    private void Start()
    {
        UpdateResultPreview();
    }

    // =========================
    // PUBLIC API
    // =========================

    public CraftingRecipe GetCurrentRecipe()
    {
        return FindMatchingRecipe();
    }

    public void UpdateResultPreview()
    {
        CraftingRecipe recipe = FindMatchingRecipe();

        if (recipe == null)
        {
            resultUI.Clear();
            return;
        }

        bool inventoryFull =
            inventoryUI == null ||
            inventoryUI.inventory == null ||
            !inventoryUI.HasEmptySlot();

        resultUI.Show(
            recipe.resultItem,
            recipe.resultAmount,
            inventoryFull
        );
    }

    // =========================
    // RECIPE MATCHING
    // =========================

    private CraftingRecipe FindMatchingRecipe()
    {
        if (craftingDatabase == null)
            return null;

        foreach (CraftingRecipe recipe in craftingDatabase.recipes)
        {
            if (RecipeMatches(recipe))
                return recipe;
        }

        return null;
    }

    private bool RecipeMatches(CraftingRecipe recipe)
    {
        return recipe.shapeless
            ? ShapelessRecipeMatches(recipe)
            : ShapedRecipeMatches(recipe);
    }

    // =========================
    // SHAPED
    // =========================

    private bool ShapedRecipeMatches(CraftingRecipe recipe)
    {
        Dictionary<int, CraftingIngredient> requiredSlots = new();

        foreach (CraftingIngredient ing in recipe.ingredients)
            requiredSlots[ing.slotIndex] = ing;

        for (int i = 0; i < craftingSlots.Length; i++)
        {
            CraftingSlot slot = craftingSlots[i].slot;

            if (requiredSlots.ContainsKey(i))
            {
                CraftingIngredient req = requiredSlots[i];

                if (slot.IsEmpty) return false;
                if (slot.item != req.item) return false;
                if (slot.amount < req.amount) return false;
            }
            else
            {
                if (!slot.IsEmpty) return false;
            }
        }

        return true;
    }

    // =========================
    // SHAPELESS (STRICT SLOT)
    // =========================

    private bool ShapelessRecipeMatches(CraftingRecipe recipe)
    {
        List<CraftingSlot> occupiedSlots = new();

        foreach (CraftingSlotUI slotUI in craftingSlots)
        {
            if (!slotUI.slot.IsEmpty)
                occupiedSlots.Add(slotUI.slot);
        }

        if (occupiedSlots.Count != recipe.ingredients.Count)
            return false;

        bool[] used = new bool[occupiedSlots.Count];

        foreach (CraftingIngredient ing in recipe.ingredients)
        {
            bool matched = false;

            for (int i = 0; i < occupiedSlots.Count; i++)
            {
                if (used[i]) continue;

                CraftingSlot slot = occupiedSlots[i];

                if (slot.item == ing.item &&
                    slot.amount >= ing.amount)
                {
                    used[i] = true;
                    matched = true;
                    break;
                }
            }

            if (!matched)
                return false;
        }

        return true;
    }

    public void ReturnAllItemsToInventory()
    {
        if (inventoryUI == null ||
            inventoryUI.inventory == null)
            return;

        foreach (CraftingSlotUI slotUI in craftingSlots)
        {
            if (slotUI == null || slotUI.slot.IsEmpty)
                continue;

            ItemData item = slotUI.slot.item;
            int amount = slotUI.slot.amount;

            // Try to find empty inventory slot
            int emptyIndex =
                inventoryUI.inventory.items.FindIndex(
                    slot => slot == null || slot.item == null
                );

            if (emptyIndex != -1)
            {
                // ✅ Return to inventory
                inventoryUI.inventory.items[emptyIndex] =
                    new InventorySlot(item, amount);
            }
            else
            {
                // ❌ Inventory full → drop to world
                DropToWorld(item, amount);
            }

            // Always clear crafting slot
            slotUI.Clear();
        }

        inventoryUI.inventory.OnInventoryChanged?.Invoke();

        if (resultUI != null)
            resultUI.Clear();
    }

    private void DropToWorld(ItemData data, int amount)
    {
        if (data == null || data.worldPrefab == null)
            return;

        Vector3 baseOrigin =
            (inventoryUI.dropOrigin != null
                ? inventoryUI.dropOrigin.position
                : Vector3.zero)
            + new Vector3(
                inventoryUI.dropOriginOffset.x,
                inventoryUI.dropOriginOffset.y,
                0f
            );

        Vector2 randomUnit = Random.insideUnitCircle;
        Vector2 randomOffset = new Vector2(
            randomUnit.x * inventoryUI.dropRadiusXY.x,
            randomUnit.y * inventoryUI.dropRadiusXY.y
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
            worldItem.amount = amount;
        }
    }
}
