using UnityEngine;
using UnityEngine.EventSystems;

public class InventorySlotRightClick : MonoBehaviour, IPointerClickHandler
{
    public int slotIndex;
    public InventoryUI inventoryUI;

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Right)
            return;

        if (inventoryUI == null)
            return;

        inventoryUI.OpenContextMenu(slotIndex, eventData.position);
    }
}
