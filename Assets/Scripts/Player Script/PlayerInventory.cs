using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class InventorySlot
{
    public ItemData item;
    public int amount;

    public InventorySlot(ItemData item, int amount)
    {
        this.item = item;
        this.amount = amount;
    }
}

public class PlayerInventory : MonoBehaviour
{
    public List<InventorySlot> items = new List<InventorySlot>();

    // Event fired when inventory updates (HotbarUI listens to this)
    public System.Action OnInventoryChanged;

    public void AddItem(ItemData data, int amount)
    {
        if (data.stackable)
        {
            // Try to add to an existing stack
            foreach (var slot in items)
            {
                if (slot.item == data)
                {
                    int spaceLeft = data.maxStack - slot.amount;

                    if (spaceLeft >= amount)
                    {
                        // All items fit into this stack
                        slot.amount += amount;

                        // Notify UI
                        OnInventoryChanged?.Invoke();
                        return;
                    }
                    else
                    {
                        // Fill this stack, continue with leftovers
                        slot.amount = data.maxStack;
                        amount -= spaceLeft;
                    }
                }
            }
        }

        // If still have leftover items or no stack exists
        while (amount > 0)
        {
            int addAmount = Mathf.Min(amount, data.maxStack);
            items.Add(new InventorySlot(data, addAmount));
            amount -= addAmount;
        }

        // Notify UI after adding new stacks
        OnInventoryChanged?.Invoke();
    }
}
