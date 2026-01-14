using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

public class CraftingSlotUI : MonoBehaviour, IDropHandler
{
    [Header("UI")]
    public Image itemIcon;
    public TMP_Text amountText;

    [Header("Data")]
    public CraftingSlot slot = new CraftingSlot();

    private void Awake()
    {
        // Start visually empty
        itemIcon.enabled = false;
        amountText.text = "";
    }

    public void OnDrop(PointerEventData eventData)
    {
        if (eventData.pointerDrag == null)
            return;

        InventorySlotDragDrop drag =
            eventData.pointerDrag.GetComponent<InventorySlotDragDrop>();

        if (drag == null)
            return;

        InventoryUI inventoryUI = drag.inventoryUI;
        int invIndex = drag.slotIndex;

        if (inventoryUI == null ||
            inventoryUI.inventory == null ||
            invIndex < 0 ||
            invIndex >= inventoryUI.inventory.items.Count)
            return;

        InventorySlot invSlot = inventoryUI.inventory.items[invIndex];
        if (invSlot == null || invSlot.item == null)
            return;

        // Do not overwrite an occupied craft slot
        if (!slot.IsEmpty)
            return;

        // MOVE item into crafting slot
        slot.Set(invSlot.item, invSlot.amount);

        // Remove from inventory
        inventoryUI.inventory.items[invIndex] = null;
        inventoryUI.inventory.OnInventoryChanged?.Invoke();

        Refresh();
    }

    public void Refresh()
    {
        if (slot.IsEmpty)
        {
            itemIcon.enabled = false;
            amountText.text = "";
            return;
        }

        itemIcon.enabled = true;
        itemIcon.sprite = slot.item.icon;

        // Show amount only if > 1
        amountText.text = slot.amount > 1
            ? slot.amount.ToString()
            : "";
    }

    public void Clear()
    {
        slot.Clear();
        Refresh();
    }
}
