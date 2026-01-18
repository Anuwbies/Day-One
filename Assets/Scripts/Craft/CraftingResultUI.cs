using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CraftingResultUI : MonoBehaviour
{
    [Header("UI")]
    public Image itemIcon;
    public TMP_Text amountText;

    [Header("Visual")]
    [Range(0f, 1f)]
    public float blockedAlpha = 0.4f;

    private ItemData currentItem;
    private int currentAmount;

    private void Awake()
    {
        Clear();
    }

    public void Show(ItemData item, int amount, bool blocked)
    {
        currentItem = item;
        currentAmount = amount;

        itemIcon.enabled = true;
        itemIcon.sprite = item.icon;
        itemIcon.preserveAspect = true;

        SetBlocked(blocked);

        amountText.text = amount > 1 ? amount.ToString() : "";
    }

    public void SetBlocked(bool blocked)
    {
        if (!itemIcon.enabled)
            return;

        Color c = itemIcon.color;
        c.a = blocked ? blockedAlpha : 1f;
        itemIcon.color = c;
    }

    public void Clear()
    {
        currentItem = null;
        currentAmount = 0;

        itemIcon.enabled = false;
        itemIcon.sprite = null;
        amountText.text = "";
    }

    public ItemData GetItem() => currentItem;
    public int GetAmount() => currentAmount;
}
