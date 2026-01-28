using TMPro;
using UnityEngine;
using UnityEngine.UI;

[System.Serializable]
public class CraftingSlot
{
    [Header("Ghost Preview")]
    [SerializeField] private Image ghostIcon;
    [SerializeField] private TMP_Text ghostAmountText;

    public ItemData item;
    public int amount;

    public bool IsEmpty => item == null || amount <= 0;

    public void Set(ItemData newItem, int newAmount)
    {
        item = newItem;
        amount = newAmount;
    }

    public void Clear()
    {
        item = null;
        amount = 0;
    }
}
