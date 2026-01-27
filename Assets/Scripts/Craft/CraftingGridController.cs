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

    public CraftingIngredientSet CurrentMatchedSet { get; private set; }

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

        bool blocked =
        inventoryUI == null ||
        inventoryUI.inventory == null ||
        !inventoryUI.CanAcceptItem(
        recipe.resultItem,
        recipe.resultAmount
    );


        resultUI.Show(
        recipe.resultItem,
        recipe.resultAmount,
        blocked
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
            // 🔒 LOCATION CHECK (Inventory only)
            if (!recipe.craftableLocations.HasFlag(CraftingLocation.Inventory))
                continue;

            if (RecipeMatches(recipe))
                return recipe;
        }

        return null;
    }

    private bool RecipeMatches(CraftingRecipe recipe)
    {
        foreach (CraftingIngredientSet set in recipe.ingredientSets)
        {
            bool matched = set.shapeless
                ? ShapelessSetMatches(set)
                : ShapedSetMatches(set);

            if (matched)
            {
                CurrentMatchedSet = set;
                return true;
            }
        }

        return false;
    }

    // =========================
    // SHAPED
    // =========================

    private bool ShapedSetMatches(CraftingIngredientSet set)
    {
        Dictionary<int, CraftingIngredient> requiredSlots = new();

        foreach (CraftingIngredient ing in set.ingredients)
            requiredSlots[ing.slotIndex] = ing;

        for (int i = 0; i < craftingSlots.Length; i++)
        {
            CraftingSlot slot = craftingSlots[i].slot;

            if (requiredSlots.TryGetValue(i, out CraftingIngredient req))
            {
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

    private bool ShapelessSetMatches(CraftingIngredientSet set)
    {
        List<CraftingSlot> occupiedSlots = new();

        foreach (CraftingSlotUI slotUI in craftingSlots)
        {
            if (!slotUI.slot.IsEmpty)
                occupiedSlots.Add(slotUI.slot);
        }

        if (occupiedSlots.Count != set.ingredients.Count)
            return false;

        bool[] used = new bool[occupiedSlots.Count];

        foreach (CraftingIngredient ing in set.ingredients)
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
        if (inventoryUI == null || inventoryUI.inventory == null)
            return;

        var items = inventoryUI.inventory.items;

        foreach (CraftingSlotUI slotUI in craftingSlots)
        {
            if (slotUI == null || slotUI.slot.IsEmpty)
                continue;

            ItemData item = slotUI.slot.item;
            int remaining = slotUI.slot.amount;

            // =========================
            // 1. MERGE INTO STACKS
            // =========================
            if (item.stackable)
            {
                for (int i = 0; i < items.Count && remaining > 0; i++)
                {
                    InventorySlot invSlot = items[i];
                    if (invSlot == null || invSlot.item != item)
                        continue;

                    if (invSlot.amount >= item.maxStack)
                        continue;

                    int space = item.maxStack - invSlot.amount;
                    int add = Mathf.Min(space, remaining);

                    invSlot.amount += add;
                    remaining -= add;
                }
            }

            // =========================
            // 2. PLACE INTO EMPTY SLOTS
            // =========================
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

            // =========================
            // 3. DROP ONLY IF NECESSARY
            // =========================
            if (remaining > 0)
            {
                DropToWorld(item, remaining);
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
