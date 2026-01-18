using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(
    fileName = "CraftingDatabase",
    menuName = "Crafting/Crafting Database"
)]
public class CraftingDatabase : ScriptableObject
{
    public List<CraftingRecipe> recipes = new List<CraftingRecipe>();
}
