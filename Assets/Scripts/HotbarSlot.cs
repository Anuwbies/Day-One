using UnityEngine;
using UnityEngine.UI;

public class HotbarSlot : MonoBehaviour
{
    public Image icon;
    public InventorySlot currentSlot; // connects to PlayerInventory slot

    // Show or hide the item icon
    public void SetSlot(InventorySlot slot)
    {
        currentSlot = slot;

        if (slot == null || slot.item == null)
        {
            icon.sprite = null;
            icon.color = new Color(1, 1, 1, 0); // hide
        }
        else
        {
            icon.sprite = slot.item.icon;
            icon.color = Color.white; // show
        }
    }
}
