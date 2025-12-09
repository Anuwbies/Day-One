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
    public int maxSlots = 20; // Maximum DIFFERENT item stacks allowed
    public List<InventorySlot> items = new List<InventorySlot>();

    public System.Action OnInventoryChanged;

    public bool IsFullForNewItem(ItemData data)
    {
        // If the item is stackable and already exists, it's NOT blocked
        if (data.stackable)
        {
            foreach (var slot in items)
            {
                if (slot.item == data && slot.amount < data.maxStack)
                    return false; // Can fit in an existing stack
            }
        }

        // Otherwise picking this item requires a new slot
        return items.Count >= maxSlots;
    }

    private void Awake()
    {
        // Ensure inventory has maxSlots entries (null = empty slot)
        while (items.Count < maxSlots)
            items.Add(null);
    }

    public bool AddItem(ItemData data, int amount)
    {
        // stackable? Try adding to existing stacks first
        if (data.stackable)
        {
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i] != null && items[i].item == data && items[i].amount < data.maxStack)
                {
                    int spaceLeft = data.maxStack - items[i].amount;
                    int amountToAdd = Mathf.Min(spaceLeft, amount);

                    items[i].amount += amountToAdd;
                    amount -= amountToAdd;

                    if (amount <= 0)
                    {
                        OnInventoryChanged?.Invoke();
                        return true;
                    }
                }
            }
        }

        // Fill empty slots
        for (int i = 0; i < items.Count && amount > 0; i++)
        {
            if (items[i] == null)
            {
                int addAmount = Mathf.Min(amount, data.maxStack);
                items[i] = new InventorySlot(data, addAmount);
                amount -= addAmount;
            }
        }

        OnInventoryChanged?.Invoke();
        return amount <= 0;
    }
}
