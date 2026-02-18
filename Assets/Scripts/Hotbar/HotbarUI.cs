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

    private InventoryUI inventoryUI;

    private void Start()
    {
        inventoryUI = Object.FindAnyObjectByType<InventoryUI>();

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
        if (Time.timeScale == 0) return;

        // If the inventory is open, do not process hotbar input
        if (inventoryUI != null && inventoryUI.IsOpen)
            return;

        CheckHotbarKeyPress();
        HandleScrollWheel();
        HandleRightClick();
    }

    private void HandleRightClick()
    {
        if (Input.GetMouseButtonDown(1)) // Right-click
        {
            // Do not eat if the mouse is over the hotbar itself
            if (IsPointerInsideHotbar()) return;

            if (selectedIndex < 0 || selectedIndex >= playerInventory.items.Count)
                return;

            InventorySlot slot = playerInventory.items[selectedIndex];
            if (slot == null || slot.item == null)
                return;

            if (playerStats != null && playerStats.EatItem(slot))
            {
                // If the item was consumed and the slot is now empty, clear it in the inventory list
                if (slot.item == null)
                {
                    playerInventory.items[selectedIndex] = null;
                }

                // Refresh UI
                playerInventory.OnInventoryChanged?.Invoke();
            }
        }
    }

    private bool IsPointerInsideHotbar()
    {
        if (hotbarRoot == null) return false;

        Canvas canvas = GetComponentInParent<Canvas>();
        Camera cam = (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            ? canvas.worldCamera
            : null;

        return RectTransformUtility.RectangleContainsScreenPoint(hotbarRoot, Input.mousePosition, cam);
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