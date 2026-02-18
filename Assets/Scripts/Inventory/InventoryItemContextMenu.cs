using UnityEngine;
using UnityEngine.EventSystems;

public class InventoryItemContextMenu : MonoBehaviour
{
    [Header("References")]
    public RectTransform panel;

    [Header("Action Buttons (CanvasGroups)")]
    public CanvasGroup eatButton;
    public CanvasGroup dropButton;
    public CanvasGroup splitButton;
    public CanvasGroup destroyButton;
    public CanvasGroup placeButton;

    [Header("Player")]
    public PlayerStats playerStats;

    private Canvas canvas;
    private InventorySlot currentSlot;
    private InventoryUI inventoryUI;

    public bool IsOpen => panel != null && panel.gameObject.activeSelf;

    private void Awake()
    {
        canvas = GetComponentInParent<Canvas>();
        Hide();
    }

    // =========================
    // SHOW CONTEXT MENU AT MOUSE
    // =========================
    public void Show(InventoryUI ui, InventorySlot slot, Vector2 screenPosition)
    {
        inventoryUI = ui;
        currentSlot = slot;

        if (currentSlot == null || currentSlot.item == null)
            return;

        UpdateButtons(currentSlot);

        panel.gameObject.SetActive(true);

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvas.transform as RectTransform,
            screenPosition,
            canvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null
                : canvas.worldCamera,
            out Vector2 localPoint
        );

        panel.anchoredPosition = localPoint;
    }

    // =========================
    // HIDE
    // =========================
    public void Hide()
    {
        currentSlot = null;
        panel.gameObject.SetActive(false);
    }

    // =========================
    // CLICK OUTSIDE TO CLOSE
    // =========================
    private void Update()
    {
        if (!panel.gameObject.activeSelf)
            return;

        if (!Input.GetMouseButtonDown(0))
            return;

        if (!RectTransformUtility.RectangleContainsScreenPoint(
                panel,
                Input.mousePosition,
                canvas.renderMode == RenderMode.ScreenSpaceOverlay
                    ? null
                    : canvas.worldCamera))
        {
            Hide();
        }
    }

    // =========================
    // BUTTON STATES (HIDE / SHOW)
    // =========================
    private void UpdateButtons(InventorySlot slot)
    {
        ItemData data = slot.item;

        SetButtonVisible(eatButton, data.canEat);
        SetButtonVisible(dropButton, data.canDrop);
        SetButtonVisible(destroyButton, data.canDestroy);

        bool canSplit =
            data.stackable &&
            data.canSplit &&
            slot.amount > 1 &&
            inventoryUI != null &&
            inventoryUI.HasEmptySlot();

        SetButtonVisible(splitButton, canSplit);
        SetButtonVisible(placeButton, data.canPlace);
    }

    private void SetButtonVisible(CanvasGroup group, bool visible)
    {
        if (group == null)
            return;

        group.gameObject.SetActive(visible);
    }

    // =========================
    // ACTIONS
    // =========================

    // PLACE
    public void Place()
    {
        Debug.Log($"Place button clicked for {currentSlot?.item?.itemName}");

        if (currentSlot == null || currentSlot.item == null)
        {
            Debug.LogWarning("Place failed: currentSlot or item is null.");
            return;
        }

        if (!currentSlot.item.canPlace)
        {
            Debug.LogWarning($"Place failed: {currentSlot.item.itemName} canPlace is false.");
            return;
        }

        if (PlacementManager.Instance != null)
        {
            Debug.Log($"Calling PlacementManager for {currentSlot.item.itemName}");
            PlacementManager.Instance.StartPlacement(currentSlot.item, currentSlot, inventoryUI);
            
            // Close the inventory UI immediately
            if (inventoryUI != null)
            {
                inventoryUI.SetOpen(false);
            }
        }
        else
        {
            Debug.LogError("Place failed: PlacementManager.Instance is null! Is there a PlacementManager in the scene?");
        }

        Hide();
    }

    // EAT
    public void Eat()
    {
        if (currentSlot == null || playerStats == null)
            return;

        if (playerStats.EatItem(currentSlot))
        {
            // If the item was consumed and the slot is now empty, clear it in the inventory list
            if (currentSlot.item == null && inventoryUI != null)
            {
                int index = inventoryUI.inventory.items.IndexOf(currentSlot);
                if (index != -1)
                {
                    inventoryUI.inventory.items[index] = null;
                }
            }

            inventoryUI.inventory.OnInventoryChanged?.Invoke();
        }

        Hide();
    }

    // DROP
    public void Drop()
    {
        if (currentSlot == null || currentSlot.item == null)
            return;

        if (!currentSlot.item.canDrop)
            return;

        inventoryUI.DropSlot(currentSlot);
        Hide();
    }

    // DESTROY
    public void Destroy()
    {
        if (currentSlot == null || currentSlot.item == null)
            return;

        if (!currentSlot.item.canDestroy)
            return;

        // AUTO DESTROY IF AMOUNT == 1
        if (currentSlot.amount <= 1)
        {
            int index = inventoryUI.inventory.items.IndexOf(currentSlot);
            if (index != -1)
            {
                inventoryUI.inventory.items[index] = null;
            }

            inventoryUI.inventory.OnInventoryChanged?.Invoke();
            Hide();
            return;
        }

        // Otherwise open Destroy UI
        if (inventoryUI == null || inventoryUI.destroyUI == null)
            return;

        inventoryUI.OpenDestroyUI(currentSlot, Input.mousePosition);
        Hide();
    }

    // SPLIT
    public void Split()
    {
        if (currentSlot == null || currentSlot.item == null)
            return;

        if (!currentSlot.item.stackable ||
            !currentSlot.item.canSplit ||
            currentSlot.amount <= 1)
            return;

        if (inventoryUI == null || inventoryUI.splitUI == null)
            return;

        inventoryUI.splitUI.Show(
            inventoryUI,
            currentSlot,
            Input.mousePosition
        );

        Hide();
    }
}