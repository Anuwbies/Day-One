using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Handles begin-drag / drag / end-drag / drop for inventory UI slots.
/// A ghost Image is created during drag, original slot GameObject remains the pointerDrag (so OnDrop uses pointerDrag).
/// </summary>
public class InventorySlotDragDrop : MonoBehaviour,
    IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler
{
    public int slotIndex;
    public InventoryUI inventoryUI;

    private Canvas canvas;
    private CanvasGroup canvasGroup;
    private RectTransform rectTransform;

    private Image draggedIcon;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();

        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();

        canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
            Debug.LogWarning($"Inventory slot '{gameObject.name}' has no parent Canvas. Drag visuals may fail.");
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        // Reject drag if this slot is empty
        if (inventoryUI == null || slotIndex >= inventoryUI.inventory.items.Count)
            return;

        // Create a ghost icon (separate GameObject so pointerDrag remains the original slot)
        draggedIcon = new GameObject("DraggedIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
        draggedIcon.transform.SetParent(canvas != null ? canvas.transform : transform.root, false);
        draggedIcon.raycastTarget = false;

        var slot = inventoryUI.inventory.items[slotIndex];
        if (slot == null || slot.item == null)
        {
            Destroy(draggedIcon.gameObject);
            draggedIcon = null;
            return;
        }

        draggedIcon.sprite = slot.item.icon;
        // match native size but cap to reasonable pixels so it fits UI
        draggedIcon.SetNativeSize();
        var rt = draggedIcon.rectTransform;
        if (rt.sizeDelta.x > 128f) rt.sizeDelta = new Vector2(128f, 128f);

        // make original slot slightly transparent while dragging
        canvasGroup.alpha = 0.4f;

        // set pointer drag so other handlers can see the original slot
        eventData.pointerDrag = gameObject;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (draggedIcon == null) return;
        draggedIcon.transform.position = eventData.position;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (draggedIcon != null)
            Destroy(draggedIcon.gameObject);

        canvasGroup.alpha = 1f;
    }

    public void OnDrop(PointerEventData eventData)
    {
        // pointerDrag should be the source slot GameObject that started the drag
        var sourceObj = eventData.pointerDrag;
        if (sourceObj == null) return;

        var source = sourceObj.GetComponent<InventorySlotDragDrop>();
        if (source == null) return;

        // If same slot, do nothing
        if (source.slotIndex == slotIndex) return;

        inventoryUI.SwapOrMove(source.slotIndex, slotIndex);
    }
}
