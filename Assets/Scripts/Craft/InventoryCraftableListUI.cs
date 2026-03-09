using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class InventoryCraftableListUI : MonoBehaviour
{
    [Header("Data")]
    [SerializeField] private CraftingDatabase craftingDatabase;

    [Header("UI")]
    [SerializeField] private Transform contentRoot;
    [SerializeField] private GameObject craftableItemPrefab;

    [Header("Crafting")]
    [SerializeField] private CraftingGridController craftingGridController;

    private void Start()
    {
        BuildList();
    }

    public void BuildList()
    {
        Clear();

        if (craftingDatabase == null)
            return;

        foreach (CraftingRecipe recipe in craftingDatabase.recipes)
        {
            if (!recipe.craftableLocations.HasFlag(CraftingLocation.Inventory))
                continue;

            if (recipe.ingredientSets == null)
                continue;

            HashSet<string> shownVariants = new HashSet<string>();

            foreach (CraftingIngredientSet set in recipe.ingredientSets)
            {
                string key = BuildIngredientSetKey(set);

                // Skip duplicate ingredient variants
                if (!shownVariants.Add(key))
                    continue;

                CreateEntry(recipe, set);
            }
        }
    }

    // =========================
    // ENTRY
    // =========================
    private void CreateEntry(CraftingRecipe recipe, CraftingIngredientSet set)
    {
        GameObject go = Instantiate(craftableItemPrefab, contentRoot);

        Image iconImage =
            go.transform.Find("Icon")
            ?.GetComponent<Image>();

        TextMeshProUGUI itemNameText =
            go.transform.Find("Text/Item Name")
            ?.GetComponent<TextMeshProUGUI>();

        TextMeshProUGUI ingredientsText =
            go.transform.Find("Text/Item Ingredients")
            ?.GetComponent<TextMeshProUGUI>();

        Button button = go.GetComponent<Button>();
        if (button == null)
            button = go.AddComponent<Button>();

        // =========================
        // RESULT ITEM UI
        // =========================
        if (recipe.resultItem != null)
        {
            if (itemNameText != null)
                itemNameText.text = recipe.resultItem.itemName;

            if (iconImage != null)
            {
                iconImage.sprite = recipe.resultItem.icon;
                iconImage.enabled = recipe.resultItem.icon != null;
                iconImage.preserveAspect = true;
            }
        }
        else
        {
            if (itemNameText != null)
                itemNameText.text = "Unknown Item";

            if (iconImage != null)
                iconImage.enabled = false;
        }

        if (ingredientsText != null)
            ingredientsText.text = BuildIngredientsText(set);

        // =========================
        // CLICK → SHOW GHOST / FILL
        // =========================
        RepeatablePointerButton handler = go.AddComponent<RepeatablePointerButton>();
        
        handler.onLeftClick = () => 
        {
            if (craftingGridController != null)
                craftingGridController.AutoFillOrGhost(set);
        };

        handler.onHoldAction = () =>
        {
            if (craftingGridController != null)
                craftingGridController.AutoFillOrGhost(set);
        };

        handler.onRightClick = () =>
        {
            if (craftingGridController == null) return;

            // Calculate max multiplier
            int maxPossible = GetMaxPossible(set);
            if (maxPossible > 0)
            {
                craftingGridController.AutoFillOrGhost(set, maxPossible);
            }
        };
    }

    private int GetMaxPossible(CraftingIngredientSet set)
    {
        if (craftingGridController == null || craftingGridController.inventoryUI == null) return 0;
        
        var inventory = craftingGridController.inventoryUI.inventory;
        
        // 1. Calculate total required of each item for ONE craft
        Dictionary<ItemData, int> requiredPerCraft = new Dictionary<ItemData, int>();
        foreach (var ing in set.ingredients)
        {
            if (ing.item == null) continue;
            if (requiredPerCraft.ContainsKey(ing.item))
                requiredPerCraft[ing.item] += ing.amount;
            else
                requiredPerCraft[ing.item] = ing.amount;
        }

        int maxMultiplier = int.MaxValue;

        // 2. For each unique item type, check total available (Inventory + Grid)
        foreach (var pair in requiredPerCraft)
        {
            ItemData item = pair.Key;
            int req = pair.Value;

            int totalAvailable = 0;

            // From Inventory
            foreach (var slot in inventory.items)
            {
                if (slot != null && slot.item == item)
                    totalAvailable += slot.amount;
            }

            // From Grid (only if they match the required item)
            foreach (var slotUI in craftingGridController.craftingSlots)
            {
                if (!slotUI.slot.IsEmpty && slotUI.slot.item == item)
                    totalAvailable += slotUI.slot.amount;
            }

            int possibleForThisItem = totalAvailable / req;
            if (possibleForThisItem < maxMultiplier)
                maxMultiplier = possibleForThisItem;
        }

        return maxMultiplier == int.MaxValue ? 0 : maxMultiplier;
    }

    private bool CanAddMore(CraftingIngredientSet set)
    {
        // This is now redundant since we use GetMaxPossible
        return GetMaxPossible(set) > 0;
    }

    // =========================
    // INGREDIENT TEXT
    // =========================
    private string BuildIngredientsText(CraftingIngredientSet set)
    {
        StringBuilder sb = new StringBuilder();

        Dictionary<ItemData, int> totals = new Dictionary<ItemData, int>();

        foreach (CraftingIngredient ing in set.ingredients)
        {
            if (ing.item == null)
                continue;

            if (totals.TryGetValue(ing.item, out int current))
                totals[ing.item] = current + ing.amount;
            else
                totals.Add(ing.item, ing.amount);
        }

        bool first = true;

        foreach (var pair in totals)
        {
            if (!first)
                sb.Append(", ");

            sb.Append(pair.Key.itemName)
              .Append(" x")
              .Append(pair.Value);

            first = false;
        }

        return sb.ToString();
    }

    // =========================
    // KEY FOR DEDUPLICATION
    // =========================
    private string BuildIngredientSetKey(CraftingIngredientSet set)
    {
        Dictionary<ItemData, int> totals = new Dictionary<ItemData, int>();

        foreach (CraftingIngredient ing in set.ingredients)
        {
            if (ing.item == null)
                continue;

            if (totals.TryGetValue(ing.item, out int current))
                totals[ing.item] = current + ing.amount;
            else
                totals.Add(ing.item, ing.amount);
        }

        List<string> parts = new List<string>();

        foreach (var pair in totals)
            parts.Add(pair.Key.name + ":" + pair.Value);

        parts.Sort();
        return string.Join("|", parts);
    }

    // =========================
    // CLEAR
    // =========================
    private void Clear()
    {
        for (int i = contentRoot.childCount - 1; i >= 0; i--)
            Destroy(contentRoot.GetChild(i).gameObject);
    }
}
