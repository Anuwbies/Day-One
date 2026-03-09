using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(
    fileName = "CraftingDatabase",
    menuName = "Crafting/Crafting Database"
)]
public class CraftingDatabase : ScriptableObject
{
    public List<CraftingRecipe> recipes = new List<CraftingRecipe>();

    public IEnumerable<CraftingRecipe> GetRecipesForLocation(CraftingLocation location)
    {
        foreach (CraftingRecipe recipe in recipes)
        {
            if (recipe == null)
                continue;

            if (!recipe.IsCraftableAt(location))
                continue;

            yield return recipe;
        }
    }
}
