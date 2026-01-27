using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

public class InventoryCraftableListUI : MonoBehaviour
{
    [Header("Data")]
    [SerializeField] private CraftingDatabase craftingDatabase;

    [Header("UI")]
    [SerializeField] private Transform contentRoot;
    [SerializeField] private GameObject craftableItemPrefab;

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

    private string BuildIngredientSetKey(CraftingIngredientSet set)
    {
        // Aggregate amounts by item
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

        // Build a deterministic, order-independent key
        List<string> parts = new List<string>();

        foreach (var pair in totals)
        {
            parts.Add(pair.Key.name + ":" + pair.Value);
        }

        parts.Sort();

        return string.Join("|", parts);
    }

    private void CreateEntry(CraftingRecipe recipe, CraftingIngredientSet set)
    {
        GameObject go = Instantiate(craftableItemPrefab, contentRoot);

        TextMeshProUGUI itemNameText =
            go.transform.Find("Text/Item Name")
            ?.GetComponent<TextMeshProUGUI>();

        TextMeshProUGUI ingredientsText =
            go.transform.Find("Text/Item Ingredients")
            ?.GetComponent<TextMeshProUGUI>();

        if (itemNameText != null)
            itemNameText.text = recipe.resultItem != null
                ? recipe.resultItem.itemName
                : "Unknown Item";

        if (ingredientsText != null)
            ingredientsText.text = BuildIngredientsText(set);
    }

    private string BuildIngredientsText(CraftingIngredientSet set)
    {
        StringBuilder sb = new StringBuilder();

        // Aggregate amounts by item
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

        sb.Append("- ");

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

    private void Clear()
    {
        for (int i = contentRoot.childCount - 1; i >= 0; i--)
        {
            Destroy(contentRoot.GetChild(i).gameObject);
        }
    }
}
