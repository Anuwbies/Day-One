using UnityEngine;
using UnityEngine.UI;

public class HotbarUI : MonoBehaviour
{
    public PlayerInventory playerInventory;
    public HotbarSlot[] slots = new HotbarSlot[8];

    public int selectedIndex = 0;

    [Header("Selection Visuals")]
    [Tooltip("Color of the currently selected hotbar slot")]
    public Color selectedColor = Color.white;

    [Tooltip("Color of non-selected hotbar slots")]
    public Color unselectedColor = new Color(1f, 1f, 1f, 0.5f);

    [Header("UI")]
    public RectTransform hotbarRoot;

    private void Start()
    {
        playerInventory.OnInventoryChanged += Refresh;

        for (int i = 0; i < slots.Length; i++)
        {
            slots[i].hotbarUI = this;
            slots[i].slotIndex = i;
        }

        Refresh();
        HighlightSelectedSlot();
    }

    private void Update()
    {
        CheckHotbarKeyPress();
        HandleScrollWheel();
    }

    public void Refresh()
    {
        for (int i = 0; i < slots.Length; i++)
        {
            if (i < playerInventory.items.Count)
                slots[i].SetSlot(playerInventory.items[i]);
            else
                slots[i].SetSlot(null);
        }
    }

    private void CheckHotbarKeyPress()
    {
        for (int i = 0; i < slots.Length; i++)
        {
            if (Input.GetKeyDown((i + 1).ToString()))
                SelectSlot(i);
        }
    }

    private void HandleScrollWheel()
    {
        float scroll = Input.GetAxis("Mouse ScrollWheel");

        if (scroll > 0f)
        {
            selectedIndex = (selectedIndex - 1 + slots.Length) % slots.Length;
            HighlightSelectedSlot();
        }
        else if (scroll < 0f)
        {
            selectedIndex = (selectedIndex + 1) % slots.Length;
            HighlightSelectedSlot();
        }
    }

    public void SelectSlot(int index)
    {
        selectedIndex = Mathf.Clamp(index, 0, slots.Length - 1);
        HighlightSelectedSlot();
    }

    private void HighlightSelectedSlot()
    {
        for (int i = 0; i < slots.Length; i++)
        {
            Image image = slots[i].GetComponent<Image>();
            if (image == null)
                continue;

            image.color = (i == selectedIndex)
                ? selectedColor
                : unselectedColor;
        }
    }

    public InventorySlot GetActiveSlot()
    {
        if (selectedIndex < playerInventory.items.Count)
            return playerInventory.items[selectedIndex];

        return null;
    }
}
