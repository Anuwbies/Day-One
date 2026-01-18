using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(
    fileName = "NewCraftingRecipe",
    menuName = "Crafting/Crafting Recipe"
)]
public class CraftingRecipe : ScriptableObject
{
    [Header("Recipe Type")]
    [Tooltip("If true, ingredient positions do NOT matter")]
    public bool shapeless = false;

    [Header("Ingredients")]
    public List<CraftingIngredient> ingredients = new List<CraftingIngredient>();

    [Header("Result")]
    public ItemData resultItem;
    public int resultAmount = 1;
}
