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
        if (itemsInRange.Count == 0) return;

        Item targetItem = itemsInRange[0];

        if (Input.GetKeyDown(KeyCode.F))
        {
            bool pickedUp = inventory.AddItem(targetItem.data, targetItem.amount);

            if (!pickedUp)
            {
                Debug.Log("Cannot pick up item — inventory is full.");
                return;
            }

            itemsInRange.Remove(targetItem);
            Destroy(targetItem.gameObject);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        Item item = other.GetComponent<Item>();
        if (item != null)
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
