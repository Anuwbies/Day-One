using UnityEngine;

public class InventoryItemContextMenu : MonoBehaviour
{
    [Header("References")]
    public RectTransform panel;

    private Canvas canvas;
    private int currentSlotIndex = -1;
    private InventoryUI inventoryUI;

    private void Awake()
    {
        canvas = GetComponentInParent<Canvas>();
        Hide();
    }

    public void Show(InventoryUI ui, int slotIndex, Vector2 screenPosition)
    {
        inventoryUI = ui;
        currentSlotIndex = slotIndex;

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
        currentSlotIndex = -1;
        panel.gameObject.SetActive(false);
    }

    private void Update()
    {
        // Left click anywhere closes the menu
        if (panel.gameObject.activeSelf && Input.GetMouseButtonDown(0))
        {
            Hide();
        }
    }
}
