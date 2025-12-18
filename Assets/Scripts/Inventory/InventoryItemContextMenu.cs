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

    private void Awake()
    {
        canvas = GetComponentInParent<Canvas>();
        Hide();
    }

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

    public void Hide()
    {
        currentSlot = null;
        panel.gameObject.SetActive(false);
    }

    private void Update()
    {
        if (!panel.gameObject.activeSelf)
            return;

        if (!Input.GetMouseButtonDown(0))
            return;

        // Close ONLY if clicking outside the panel
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

    private void UpdateButtons(InventorySlot slot)
    {
        ItemData data = slot.item;

        SetButtonState(eatButton, data.canEat);
        SetButtonState(dropButton, data.canDrop);
        SetButtonState(destroyButton, data.canDestroy);

        bool canSplit =
            data.stackable &&
            data.canSplit &&
            slot.amount > 1;

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
    // STEP 4.1 — EAT ACTION
    // =========================
    public void Eat()
    {
        if (currentSlot == null || currentSlot.item == null)
            return;

        ItemData data = currentSlot.item;

        if (!data.canEat || playerStats == null)
            return;

        // Apply stat effects
        playerStats.AddHealth(data.healthRestore);
        playerStats.AddHunger(data.hungerRestore);
        playerStats.AddThirst(data.thirstRestore);
        playerStats.AddEnergy(data.energyRestore);

        // Consume 1 item
        currentSlot.amount--;

        // Remove slot if empty
        if (currentSlot.amount <= 0)
        {
            inventoryUI.inventory.items.Remove(currentSlot);
        }

        inventoryUI.inventory.OnInventoryChanged?.Invoke();
        Hide(); // close because button was pressed
    }
}
