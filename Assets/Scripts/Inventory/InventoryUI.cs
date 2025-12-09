using UnityEngine;

public class InventoryUI : MonoBehaviour
{
    public PlayerInventory inventory;
    public InventorySlotUI[] slots;
    public GameObject inventoryWindow; // The child window panel

    private bool isOpen = false;

    private void Start()
    {
        inventoryWindow.SetActive(false); // Hide only the window

        inventory.OnInventoryChanged += RefreshUI;
        RefreshUI();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.I))
        {
            isOpen = !isOpen;
            inventoryWindow.SetActive(isOpen);

            if (isOpen)
                RefreshUI();
        }
    }

    public void RefreshUI()
    {
        for (int i = 0; i < slots.Length; i++)
        {
            if (i < inventory.items.Count)
            {
                var slot = inventory.items[i];
                slots[i].SetSlot(slot.item.icon, slot.amount);
            }
            else
            {
                slots[i].ClearSlot();
            }
        }
    }
}
