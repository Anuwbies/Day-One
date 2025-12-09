using UnityEngine;

public class HotbarUI : MonoBehaviour
{
    public PlayerInventory playerInventory;
    public HotbarSlot[] slots = new HotbarSlot[8];

    public int selectedIndex = 0;

    private void Start()
    {
        playerInventory.OnInventoryChanged += Refresh;
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
            {
                selectedIndex = i;
                HighlightSelectedSlot();
            }
        }
    }

    private void HandleScrollWheel()
    {
        float scroll = Input.GetAxis("Mouse ScrollWheel");

        if (scroll > 0f)
        {
            // Scroll up → go to previous slot
            selectedIndex--;
            if (selectedIndex < 0) selectedIndex = 7;

            HighlightSelectedSlot();
        }
        else if (scroll < 0f)
        {
            // Scroll down → go to next slot
            selectedIndex++;
            if (selectedIndex > 7) selectedIndex = 0;

            HighlightSelectedSlot();
        }
    }

    private void HighlightSelectedSlot()
    {
        for (int i = 0; i < slots.Length; i++)
        {
            var image = slots[i].GetComponent<UnityEngine.UI.Image>();

            if (i == selectedIndex)
                image.color = new Color(1, 1, 1, 1);
            else
                image.color = new Color(1, 1, 1, 0.5f);
        }
    }

    public InventorySlot GetActiveSlot()
    {
        if (selectedIndex < playerInventory.items.Count)
            return playerInventory.items[selectedIndex];

        return null;
    }
}
