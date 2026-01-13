using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class InventoryDestroyUI : MonoBehaviour
{
    [Header("UI References")]
    public RectTransform panel;
    public TMP_Text itemNameText;
    public TMP_Text destroyAmountText;
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
    // SHOW DESTROY UI
    // =========================
    public void Show(InventoryUI ui, InventorySlot slot, Vector2 screenPosition)
    {
        if (slot == null || slot.item == null || slot.amount <= 0)
            return;

        inventoryUI = ui;
        sourceSlot = slot;

        panel.gameObject.SetActive(true);

        itemNameText.text = slot.item.itemName;

        slider.minValue = 1;
        slider.maxValue = slot.amount;
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

        int destroyAmount = Mathf.RoundToInt(slider.value);
        int remainingAmount = sourceSlot.amount - destroyAmount;

        destroyAmountText.text = destroyAmount.ToString();
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

        int destroyAmount = Mathf.RoundToInt(slider.value);

        sourceSlot.amount -= destroyAmount;

        if (sourceSlot.amount <= 0)
        {
            sourceSlot.item = null;
            sourceSlot.amount = 0;
        }

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

    private void Update()
    {
        if (panel == null || !panel.gameObject.activeSelf)
            return;

        // =========================
        // KEYBOARD SHORTCUTS
        // =========================
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            Confirm();
            return;
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Cancel();
            return;
        }

        // =========================
        // CLICK OUTSIDE TO CLOSE
        // =========================
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
