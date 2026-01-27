using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class CraftingIngredientSet
{
    [Tooltip("If true, ingredient slots do not matter")]
    public bool shapeless = true;

    [Tooltip("Ingredients required for this variant")]
    public List<CraftingIngredient> ingredients = new();
}
