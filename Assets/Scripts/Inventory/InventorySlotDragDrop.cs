using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class InventorySlotDragDrop : MonoBehaviour,
    IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler
{
    public int slotIndex;
    public InventoryUI inventoryUI;

    private Canvas canvas;
    private CanvasGroup slotCanvasGroup;

    // Drag ghost
    private RectTransform ghostRect;
    private Image ghostImage;

    private bool droppedOnSlot;
    private bool dragBlocked;

    private void Awake()
    {
        slotCanvasGroup = GetComponent<CanvasGroup>();
        if (slotCanvasGroup == null)
            slotCanvasGroup = gameObject.AddComponent<CanvasGroup>();

        canvas = GetComponentInParent<Canvas>();
    }

    // =========================
    // BEGIN DRAG
    // =========================
    public void OnBeginDrag(PointerEventData eventData)
    {
        dragBlocked = false;

        // =========================
        // HARD BLOCK DRAG CONDITIONS
        // =========================
        if (inventoryUI != null)
        {
            if (inventoryUI.splitUI != null && inventoryUI.splitUI.IsOpen)
                dragBlocked = true;

            if (inventoryUI.contextMenu != null && inventoryUI.contextMenu.IsOpen)
                dragBlocked = true;
        }

        if (dragBlocked)
        {
            // This is CRITICAL — cancels Unity drag internally
            eventData.pointerDrag = null;
            return;
        }

        droppedOnSlot = false;

        if (inventoryUI == null ||
            inventoryUI.inventory == null ||
            slotIndex >= inventoryUI.inventory.items.Count)
            return;

        InventorySlot slot = inventoryUI.inventory.items[slotIndex];
        if (slot == null || slot.item == null)
            return;

        CreateGhost(slot.item.icon);

        slotCanvasGroup.alpha = 0.4f;
        slotCanvasGroup.blocksRaycasts = false;

        eventData.pointerDrag = gameObject;
    }

    // =========================
    // DRAG
    // =========================
    public void OnDrag(PointerEventData eventData)
    {
        if (dragBlocked)
            return;

        if (ghostRect != null)
            ghostRect.position = eventData.position;
    }

    // =========================
    // END DRAG
    // =========================
    public void OnEndDrag(PointerEventData eventData)
    {
        if (dragBlocked)
            return;

        DestroyGhost();

        slotCanvasGroup.alpha = 1f;
        slotCanvasGroup.blocksRaycasts = true;

        if (droppedOnSlot)
            return;

        if (IsPointerInsideInventoryGrid(eventData))
            return;

        inventoryUI.DropItemFromSlot(slotIndex);
    }

    // =========================
    // DROP ON SLOT
    // =========================
    public void OnDrop(PointerEventData eventData)
    {
        if (dragBlocked)
            return;

        if (eventData.pointerDrag == null)
            return;

        InventorySlotDragDrop source =
            eventData.pointerDrag.GetComponent<InventorySlotDragDrop>();

        if (source == null || source == this)
            return;

        droppedOnSlot = true;
        source.droppedOnSlot = true;

        inventoryUI.TryMergeOrSwap(source.slotIndex, slotIndex);
    }

    // =========================
    // GHOST CREATION
    // =========================
    private void CreateGhost(Sprite icon)
    {
        GameObject ghost = new GameObject(
            "DragGhost",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image)
        );

        ghost.transform.SetParent(canvas.transform, false);

        ghostRect = ghost.GetComponent<RectTransform>();
        ghostRect.sizeDelta = new Vector2(48f, 48f);

        ghostImage = ghost.GetComponent<Image>();
        ghostImage.sprite = icon;
        ghostImage.raycastTarget = false;
        ghostImage.color = new Color(1f, 1f, 1f, 0.9f);

        ghost.transform.SetAsLastSibling();
    }

    private void DestroyGhost()
    {
        if (ghostRect != null)
            Destroy(ghostRect.gameObject);

        ghostRect = null;
        ghostImage = null;
    }

    // =========================
    // HELPERS
    // =========================
    private bool IsPointerInsideInventoryGrid(PointerEventData eventData)
    {
        if (inventoryUI == null || inventoryUI.inventoryGrid == null)
            return false;

        return RectTransformUtility.RectangleContainsScreenPoint(
            inventoryUI.inventoryGrid,
            eventData.position,
            canvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null
                : canvas.worldCamera
        );
    }
}
