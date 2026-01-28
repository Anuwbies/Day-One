using UnityEngine;
using UnityEngine.UI;

public class HotbarUI : MonoBehaviour
{
    public PlayerStats playerStats;
    public PlayerInventory playerInventory;
    public HotbarSlot[] slots = new HotbarSlot[8];

    public int selectedIndex = 0;

    [Header("Selection Visuals")]
    public Color selectedColor = Color.white;
    public Color unselectedColor = new Color(1f, 1f, 1f, 0.5f);

    [Header("UI")]
    public RectTransform hotbarRoot;

    private void Start()
    {
        playerInventory.OnInventoryChanged += OnInventoryChanged;

        for (int i = 0; i < slots.Length; i++)
        {
            slots[i].hotbarUI = this;
            slots[i].slotIndex = i;
        }

        Refresh();
        ApplySelection(); // initial sync
    }

    private void Update()
    {
        CheckHotbarKeyPress();
        HandleScrollWheel();
    }

    // =========================
    // INVENTORY SYNC
    // =========================
    private void OnInventoryChanged()
    {
        Refresh();
        ApplySelection(); // 🔴 CRITICAL FIX
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

    // =========================
    // INPUT
    // =========================
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
            SelectSlot((selectedIndex - 1 + slots.Length) % slots.Length);
        else if (scroll < 0f)
            SelectSlot((selectedIndex + 1) % slots.Length);
    }

    // =========================
    // SELECTION
    // =========================
    public void SelectSlot(int index)
    {
        selectedIndex = Mathf.Clamp(index, 0, slots.Length - 1);
        ApplySelection();
    }

    private void ApplySelection()
    {
        HighlightSelectedSlot();

        InventorySlot activeSlot = GetActiveSlot();

        if (playerStats != null)
        {
            playerStats.SetCurrentItem(
                activeSlot != null ? activeSlot.item : null
            );
        }
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

    // =========================
    // DATA ACCESS
    // =========================
    public InventorySlot GetActiveSlot()
    {
        if (selectedIndex >= playerInventory.items.Count)
            return null;

        InventorySlot slot = playerInventory.items[selectedIndex];
        return (slot != null && slot.item != null) ? slot : null;
    }
}