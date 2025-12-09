using UnityEngine;

public class HotbarUI : MonoBehaviour
{
    public PlayerInventory playerInventory;
    public HotbarSlot[] slots = new HotbarSlot[8];

    public int selectedIndex = 0;

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
        for (int i = 0; i < 8; i++)
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
            selectedIndex--;
            if (selectedIndex < 0) selectedIndex = 7;
            HighlightSelectedSlot();
        }
        else if (scroll < 0f)
        {
            selectedIndex++;
            if (selectedIndex > 7) selectedIndex = 0;
            HighlightSelectedSlot();
        }
    }

    public void SelectSlot(int index)
    {
        selectedIndex = index;
        HighlightSelectedSlot();
    }

    private void HighlightSelectedSlot()
    {
        for (int i = 0; i < slots.Length; i++)
        {
            var image = slots[i].GetComponent<UnityEngine.UI.Image>();

            image.color = (i == selectedIndex)
                ? new Color(1, 1, 1, 1)
                : new Color(1, 1, 1, 0.5f);
        }
    }

    public InventorySlot GetActiveSlot()
    {
        if (selectedIndex < playerInventory.items.Count)
            return playerInventory.items[selectedIndex];

        return null;
    }
}
