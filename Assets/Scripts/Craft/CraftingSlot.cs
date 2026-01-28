using UnityEngine;

[System.Serializable]
public class CraftingSlot
{
    public ItemData item;
    public int amount;

    public bool IsEmpty => item == null || amount <= 0;

    public void Set(ItemData newItem, int newAmount)
    {
        item = newItem;
        amount = newAmount;
    }

    public void Clear()
    {
        item = null;
        amount = 0;
    }
}
