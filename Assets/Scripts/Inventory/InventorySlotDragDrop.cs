using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class InventorySlotDragDrop : MonoBehaviour,
    IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler
{
    public int slotIndex;
    public InventoryUI inventoryUI;

    private Canvas canvas;
    private CanvasGroup canvasGroup;
    private Image draggedIcon;
    private bool droppedOnSlot;

    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();

        canvas = GetComponentInParent<Canvas>();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        droppedOnSlot = false;

        if (inventoryUI == null ||
            inventoryUI.inventory == null ||
            slotIndex >= inventoryUI.inventory.items.Count)
            return;

        var slot = inventoryUI.inventory.items[slotIndex];
        if (slot == null || slot.item == null)
            return;

        draggedIcon = new GameObject(
            "DraggedIcon",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image)
        ).GetComponent<Image>();

        draggedIcon.transform.SetParent(canvas.transform, false);
        draggedIcon.raycastTarget = false;
        draggedIcon.sprite = slot.item.icon;
        draggedIcon.SetNativeSize();

        canvasGroup.alpha = 0.4f;
        eventData.pointerDrag = gameObject;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (draggedIcon != null)
            draggedIcon.transform.position = eventData.position;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (draggedIcon != null)
            Destroy(draggedIcon.gameObject);

        canvasGroup.alpha = 1f;

        // If dropped on another slot, do nothing
        if (droppedOnSlot)
            return;

        // If still inside inventory grid, do NOT drop to world
        if (IsPointerInsideInventoryGrid(eventData))
            return;

        // Otherwise, drop item to world
        inventoryUI.DropItemFromSlot(slotIndex);
    }

    public void OnDrop(PointerEventData eventData)
    {
        var sourceObj = eventData.pointerDrag;
        if (sourceObj == null) return;

        var source = sourceObj.GetComponent<InventorySlotDragDrop>();
        if (source == null) return;
        if (source.slotIndex == slotIndex) return;

        droppedOnSlot = true;
        source.droppedOnSlot = true;

        inventoryUI.SwapOrMove(source.slotIndex, slotIndex);
    }

    private bool IsPointerInsideInventoryGrid(PointerEventData eventData)
    {
        if (inventoryUI.inventoryGrid == null)
        {
            Debug.LogWarning("InventoryUI.inventoryGrid is not assigned.");
            return false;
        }

        return RectTransformUtility.RectangleContainsScreenPoint(
            inventoryUI.inventoryGrid,
            eventData.position,
            canvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null
                : canvas.worldCamera
        );
    }
}
