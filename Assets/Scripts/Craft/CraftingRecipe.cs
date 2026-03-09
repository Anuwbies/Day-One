using UnityEngine;
using System.Collections.Generic;

[System.Flags]
public enum CraftingLocation
{
    Inventory = 1 << 0,
    CraftingTable = 1 << 1
}

[CreateAssetMenu(
    fileName = "NewCraftingRecipe",
    menuName = "Crafting/Crafting Recipe"
)]
public class CraftingRecipe : ScriptableObject
{
    [Header("Craftable Locations")]
    public CraftingLocation craftableLocations =
        CraftingLocation.Inventory | CraftingLocation.CraftingTable;

    [Header("Ingredient Variants (OR)")]
    public List<CraftingIngredientSet> ingredientSets = new();

    [Header("Result")]
    public ItemData resultItem;
    public int resultAmount = 1;

    public bool IsCraftableAt(CraftingLocation location)
    {
        return (craftableLocations & location) != 0;
    }
}
