using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

public class HotbarSlot : MonoBehaviour, IPointerClickHandler
{
    public Image icon;
    public TextMeshProUGUI amountText;
    public InventorySlot currentSlot;

    public HotbarUI hotbarUI;
    public int slotIndex;

    public void SetSlot(InventorySlot slot)
    {
        currentSlot = slot;

        if (slot == null || slot.item == null)
        {
            icon.sprite = null;
            icon.color = new Color(1, 1, 1, 0);
            amountText.text = "";
            return;
        }

        // Display icon
        icon.sprite = slot.item.icon;
        icon.color = Color.white;
        icon.preserveAspect = true;

        // Display amount (hide if only 1)
        if (slot.amount > 1)
            amountText.text = slot.amount.ToString();
        else
            amountText.text = "";
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left)
        {
            hotbarUI.SelectSlot(slotIndex);
        }
        else if (eventData.button == PointerEventData.InputButton.Right)
        {
            if (currentSlot == null || currentSlot.item == null)
                return;

            if (hotbarUI.playerStats != null && hotbarUI.playerStats.EatItem(currentSlot))
            {
                // If consumed and empty, clear it in the shared inventory list
                if (currentSlot.item == null)
                {
                    hotbarUI.playerInventory.items[slotIndex] = null;
                }

                hotbarUI.playerInventory.OnInventoryChanged?.Invoke();
            }
        }
    }
}
