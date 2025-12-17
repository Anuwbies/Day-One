using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class InventorySlotUI : MonoBehaviour
{
    public Image icon;
    public TMP_Text amount;

    public void ClearSlot()
    {
        icon.enabled = false;
        icon.sprite = null;
        amount.text = "";
    }

    public void SetSlot(Sprite sprite, int itemAmount)
    {
        icon.enabled = true;

        icon.type = Image.Type.Simple;
        icon.preserveAspect = true;
        icon.sprite = sprite;

        amount.text = itemAmount > 1 ? itemAmount.ToString() : "";
    }
}
