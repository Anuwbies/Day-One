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

        // Convert screen position → canvas local position
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
    // BUTTON STATES
    // =========================
    private void UpdateButtons(InventorySlot slot)
    {
        ItemData data = slot.item;

        SetButtonState(eatButton, data.canEat);
        SetButtonState(dropButton, data.canDrop);
        SetButtonState(destroyButton, data.canDestroy);

        bool canSplit =
            data.stackable &&
            data.canSplit &&
            slot.amount > 1 &&
            inventoryUI != null &&
            inventoryUI.HasEmptySlot();

        SetButtonState(splitButton, canSplit);
    }

    private void SetButtonState(CanvasGroup group, bool enabled)
    {
        if (group == null)
            return;

        group.alpha = enabled ? 1f : 0.35f;
        group.interactable = enabled;
        group.blocksRaycasts = enabled;
    }

    // =========================
    // ACTIONS
    // =========================

    // EAT
    public void Eat()
    {
        if (currentSlot == null || currentSlot.item == null)
            return;

        ItemData data = currentSlot.item;

        if (!data.canEat || playerStats == null)
            return;

        playerStats.AddHealth(data.healthRestore);
        playerStats.AddHunger(data.hungerRestore);
        playerStats.AddThirst(data.thirstRestore);
        playerStats.AddEnergy(data.energyRestore);

        currentSlot.amount--;

        if (currentSlot.amount <= 0)
            inventoryUI.inventory.items.Remove(currentSlot);

        inventoryUI.inventory.OnInventoryChanged?.Invoke();
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

        inventoryUI.DestroySlot(currentSlot);
        Hide();
    }

    // SPLIT (OPEN SPLIT UI AT MOUSE)
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
