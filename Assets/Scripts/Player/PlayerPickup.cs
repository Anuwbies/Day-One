using UnityEngine;
using System.Collections.Generic;

public class PlayerPickup : MonoBehaviour
{
    private PlayerInventory inventory;

    private List<Item> itemsInRange = new List<Item>();

    private void Awake()
    {
        inventory = GetComponent<PlayerInventory>();
    }

    private void Update()
    {
        // CLEANUP: Remove any items that were destroyed (e.g., by other scripts or duplicate references)
        // This prevents trying to pick up "ghost" items
        for (int i = itemsInRange.Count - 1; i >= 0; i--)
        {
            if (itemsInRange[i] == null)
            {
                itemsInRange.RemoveAt(i);
            }
        }

        if (itemsInRange.Count == 0) return;

        Item targetItem = itemsInRange[0];

        if (Input.GetKeyDown(KeyCode.F))
        {
            // DEBUG: Check if the item actually has data assigned
            if (targetItem.data == null)
            {
                Debug.LogWarning($"Cannot pick up '{targetItem.name}' — ItemData is missing in Inspector!");
                return;
            }

            bool pickedUp = inventory.AddItem(targetItem.data, targetItem.amount);

            if (!pickedUp)
            {
                Debug.Log("Cannot pick up item — inventory is full.");
                return;
            }

            // Success
            itemsInRange.Remove(targetItem);
            Destroy(targetItem.gameObject);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        Item item = other.GetComponent<Item>();

        // FIX: Check if list already contains this item to prevent duplicates
        if (item != null && !itemsInRange.Contains(item))
        {
            itemsInRange.Add(item);
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        Item item = other.GetComponent<Item>();
        if (item != null)
        {
            itemsInRange.Remove(item);
        }
    }
}