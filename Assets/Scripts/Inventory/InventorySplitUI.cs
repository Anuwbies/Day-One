using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class InventorySplitUI : MonoBehaviour
{
    [Header("UI References")]
    public RectTransform panel;
    public TMP_Text itemNameText;
    public TMP_Text splitAmountText;
    public TMP_Text remainingAmountText;
    public Slider slider;

    private InventorySlot sourceSlot;
    private InventoryUI inventoryUI;
    private Canvas canvas;

    public bool IsOpen => panel != null && panel.gameObject.activeSelf;

    private void Awake()
    {
        canvas = GetComponentInParent<Canvas>();

        // Start disabled
        if (panel != null)
            panel.gameObject.SetActive(false);
    }

    // =========================
    // SHOW SPLIT UI
    // =========================
    public void Show(InventoryUI ui, InventorySlot slot, Vector2 screenPosition)
    {
        if (slot == null || slot.item == null || slot.amount <= 1)
            return;

        inventoryUI = ui;
        sourceSlot = slot;

        panel.gameObject.SetActive(true);

        itemNameText.text = slot.item.itemName;

        slider.minValue = 1;
        slider.maxValue = slot.amount - 1;
        slider.wholeNumbers = true;
        slider.value = 1;

        UpdateTexts();
        PositionPanel(screenPosition);
    }

    // =========================
    // POSITIONING
    // =========================
    private void PositionPanel(Vector2 screenPosition)
    {
        if (panel == null || canvas == null)
            return;

        RectTransform canvasRect = canvas.transform as RectTransform;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect,
            screenPosition,
            canvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null
                : canvas.worldCamera,
            out Vector2 localPoint
        );

        panel.anchoredPosition = ClampToCanvas(localPoint);
    }

    private Vector2 ClampToCanvas(Vector2 pos)
    {
        RectTransform canvasRect = canvas.transform as RectTransform;
        Vector2 canvasSize = canvasRect.rect.size;
        Vector2 panelSize = panel.rect.size;

        float x = Mathf.Clamp(
            pos.x,
            -canvasSize.x / 2 + panelSize.x / 2,
            canvasSize.x / 2 - panelSize.x / 2
        );

        float y = Mathf.Clamp(
            pos.y,
            -canvasSize.y / 2 + panelSize.y / 2,
            canvasSize.y / 2 - panelSize.y / 2
        );

        return new Vector2(x, y);
    }

    // =========================
    // SLIDER CALLBACK
    // =========================
    public void OnSliderChanged(float value)
    {
        UpdateTexts();
    }

    private void UpdateTexts()
    {
        if (sourceSlot == null)
            return;

        int splitAmount = Mathf.RoundToInt(slider.value);
        int remainingAmount = sourceSlot.amount - splitAmount;

        splitAmountText.text = splitAmount.ToString();
        remainingAmountText.text = remainingAmount.ToString();
    }

    // =========================
    // CONFIRM / CANCEL
    // =========================
    public void Confirm()
    {
        if (sourceSlot == null || inventoryUI == null)
        {
            Hide();
            return;
        }

        int splitAmount = Mathf.RoundToInt(slider.value);

        inventoryUI.SplitSlot(sourceSlot, splitAmount);
        inventoryUI.inventory.OnInventoryChanged?.Invoke();

        Hide();
    }

    public void Cancel()
    {
        Hide();
    }

    private void Hide()
    {
        sourceSlot = null;
        inventoryUI = null;

        if (panel != null)
            panel.gameObject.SetActive(false);
    }

    // =========================
    // CLICK OUTSIDE TO CLOSE
    // =========================
    private void Update()
    {
        if (panel == null || !panel.gameObject.activeSelf)
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
}
