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
        // If the item is stackable and already exists with space, it's NOT blocked
        if (data.stackable)
        {
            foreach (var slot in items)
            {
                if (slot != null && slot.item == data && slot.amount < data.maxStack)
                    return false; // Can fit in an existing stack
            }
        }

        // Check if there's any truly empty slot in current list
        foreach (var slot in items)
        {
            if (slot == null || slot.item == null)
                return false;
        }

        // If no empty slots found, is it full?
        return items.Count >= maxSlots;
    }

    private void Awake()
    {
        EnsureSlotCapacity();
    }

    public void SetMaxSlots(int slotCount)
    {
        maxSlots = Mathf.Max(1, slotCount);
        EnsureSlotCapacity();
        OnInventoryChanged?.Invoke();
    }

    private void EnsureSlotCapacity()
    {
        maxSlots = Mathf.Max(1, maxSlots);

        if (items == null)
            items = new List<InventorySlot>();

        if (items.Count > maxSlots)
        {
            for (int i = items.Count - 1; i >= maxSlots; i--)
            {
                InventorySlot slot = items[i];
                if (slot != null && slot.item != null)
                {
                    Debug.LogWarning(
                        $"{name}: cannot shrink inventory to {maxSlots} slots because slot {i} still contains '{slot.item.itemName}'.");
                    maxSlots = items.Count;
                    break;
                }
            }
        }

        if (items.Count > maxSlots)
            items.RemoveRange(maxSlots, items.Count - maxSlots);

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

        // Fill empty slots (checking for null OR slots with no item data)
        for (int i = 0; i < items.Count && amount > 0; i++)
        {
            if (items[i] == null || items[i].item == null)
            {
                int addAmount = Mathf.Min(amount, data.maxStack);
                items[i] = new InventorySlot(data, addAmount);
                amount -= addAmount;
            }
        }

        // If we still have amount left, try adding new slots IF we are under maxSlots
        while (amount > 0 && items.Count < maxSlots)
        {
            int addAmount = Mathf.Min(amount, data.maxStack);
            items.Add(new InventorySlot(data, addAmount));
            amount -= addAmount;
        }

        OnInventoryChanged?.Invoke();
        return amount <= 0;
    }

    public bool HasItem(ItemData data, int amount)
    {
        int total = 0;
        foreach (var slot in items)
        {
            if (slot != null && slot.item == data)
            {
                total += slot.amount;
                if (total >= amount) return true;
            }
        }
        return false;
    }

    public void RemoveItem(ItemData data, int amount)
    {
        for (int i = items.Count - 1; i >= 0; i--)
        {
            if (items[i] != null && items[i].item == data)
            {
                int canRemove = Mathf.Min(amount, items[i].amount);
                items[i].amount -= canRemove;
                amount -= canRemove;

                if (items[i].amount <= 0)
                {
                    items[i] = null;
                }

                if (amount <= 0) break;
            }
        }
        OnInventoryChanged?.Invoke();
    }
}
