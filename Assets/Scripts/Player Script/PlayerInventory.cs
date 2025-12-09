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

    public bool AddItem(ItemData data, int amount)
    {
        // First check if adding a new stack is allowed
        if (IsFullForNewItem(data))
        {
            Debug.Log("Inventory full — cannot pick up new item type.");
            return false;
        }

        if (data.stackable)
        {
            // Try adding to an existing stack
            foreach (var slot in items)
            {
                if (slot.item == data)
                {
                    int spaceLeft = data.maxStack - slot.amount;

                    if (spaceLeft >= amount)
                    {
                        slot.amount += amount;
                        OnInventoryChanged?.Invoke();
                        return true;
                    }
                    else
                    {
                        slot.amount = data.maxStack;
                        amount -= spaceLeft;
                    }
                }
            }
        }

        // Add new stacks if needed (and if there's free slot space)
        while (amount > 0)
        {
            if (items.Count >= maxSlots)
            {
                Debug.Log("Inventory full — cannot add more stacks.");
                OnInventoryChanged?.Invoke();
                return false;
            }

            int addAmount = Mathf.Min(amount, data.maxStack);
            items.Add(new InventorySlot(data, addAmount));
            amount -= addAmount;
        }

        OnInventoryChanged?.Invoke();
        return true;
    }
}
