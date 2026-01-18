using UnityEngine;

[System.Serializable]
public class CraftingIngredient
{
    [Tooltip("Grid index 0–8 (left to right, top to bottom)")]
    [Range(0, 8)]
    public int slotIndex;

    public ItemData item;
    public int amount = 1;
}
