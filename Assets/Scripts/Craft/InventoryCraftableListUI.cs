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
        // CLICK → SHOW GHOST
        // =========================
        button.onClick.AddListener(() =>
        {
            if (craftingGridController == null)
                return;

            // Auto place real items if available,
            // show ghost only for missing ingredients
            craftingGridController.AutoFillOrGhost(set);
        });
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
