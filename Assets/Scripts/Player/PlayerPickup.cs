using UnityEngine;
using System.Collections.Generic;

public class PlayerPickup : MonoBehaviour
{
    private PlayerInventory inventory;

    private List<Item> itemsInRange = new List<Item>();

    [Header("Pickup Settings")]
    [SerializeField] private float pickupInterval = 0.15f;
    private float nextPickupTime;

    private static int totalItemsPickedUp = 0;
    private const int MaxHintsToShow = 10;

    // This ensures the counter resets whenever you press Play in the editor,
    // even if "Domain Reload" is disabled in Enter Play Mode settings.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetCounter()
    {
        totalItemsPickedUp = 0;
    }

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

        if (Input.GetKey(KeyCode.Space))
        {
            if (Time.time < nextPickupTime) return;

            Item targetItem = itemsInRange[0];

            // DEBUG: Check if the item actually has data assigned
            if (targetItem.data == null)
            {
                Debug.LogWarning($"Cannot pick up '{targetItem.name}' - ItemData is missing in Inspector!");
                itemsInRange.RemoveAt(0); // Remove invalid item to avoid getting stuck
                return;
            }

            bool pickedUp = inventory.AddItem(targetItem.data, targetItem.amount);

            if (!pickedUp)
            {
                // Only log when inventory is full, but with a cooldown to avoid log spam
                if (Time.time > nextPickupTime + 1f)
                {
                    Debug.Log("Cannot pick up item - inventory is full.");
                    nextPickupTime = Time.time;
                }
                return;
            }

            // Success
            totalItemsPickedUp++;
            nextPickupTime = Time.time + pickupInterval;
            itemsInRange.Remove(targetItem);
            Destroy(targetItem.gameObject);

            // If we just hit the limit, hide hints for all other items currently in range
            if (totalItemsPickedUp >= MaxHintsToShow)
            {
                foreach (Item item in itemsInRange)
                {
                    if (item != null) item.ToggleHint(false);
                }
            }
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        Item item = other.GetComponentInParent<Item>();

        // FIX: Check if list already contains this item to prevent duplicates
        if (item != null && !itemsInRange.Contains(item))
        {
            itemsInRange.Add(item);
            
            // Only show hint if we haven't reached the tutorial limit
            if (totalItemsPickedUp < MaxHintsToShow)
            {
                item.ToggleHint(true);
            }
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        Item item = other.GetComponentInParent<Item>();
        if (item != null)
        {
            item.ToggleHint(false); // Hide hint when out of range
            itemsInRange.Remove(item);
        }
    }
}
