using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class InventorySlotDragDrop : MonoBehaviour,
    IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler, IPointerClickHandler
{
    public int slotIndex;
    public InventoryUI inventoryUI;

    [Header("Ghost Settings")]
    [Tooltip("Manual width for the drag ghost.")]
    public float ghostWidth = 80f;
    [Tooltip("Manual height for the drag ghost.")]
    public float ghostHeight = 80f;

    private Canvas canvas;
    private CanvasGroup slotCanvasGroup;

    private float lastClickTime;
    private const float doubleClickThreshold = 0.25f;

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
    // DOUBLE LEFT CLICK
    // =========================
    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left)
            return;

        // Prevent interaction if UI has priority
        if (inventoryUI == null ||
            inventoryUI.ConsumeClickThisFrame)
            return;

        float time = Time.unscaledTime;

        if (time - lastClickTime <= doubleClickThreshold)
        {
            inventoryUI.CombineAllSameItems(slotIndex);
            lastClickTime = 0f;
        }
        else
        {
            lastClickTime = time;
        }
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

        if (IsPointerInsideSafeUI(eventData))
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

        // =========================
        // CRAFT SLOT → INVENTORY SLOT
        // =========================
        CraftingSlotUI craftSource =
            eventData.pointerDrag.GetComponent<CraftingSlotUI>();

        if (craftSource != null)
        {
            if (craftSource.slot == null || craftSource.slot.IsEmpty)
                return;

            InventorySlot invSlot =
                inventoryUI.inventory.items[slotIndex];

            // =========================
            // SWAP: CRAFT <-> INVENTORY
            // =========================
            if (invSlot != null)
            {
                InventorySlot tempInv =
                    new InventorySlot(
                        craftSource.slot.item,
                        craftSource.slot.amount
                    );

                craftSource.slot.Set(invSlot.item, invSlot.amount);
                inventoryUI.inventory.items[slotIndex] = tempInv;
                craftSource.Refresh();
            }
            else
            {
                // MOVE craft → inventory
                inventoryUI.inventory.items[slotIndex] =
                    new InventorySlot(
                        craftSource.slot.item,
                        craftSource.slot.amount
                    );

                craftSource.Clear();
            }

            inventoryUI.inventory.OnInventoryChanged?.Invoke();
            return;
        }

        // =========================
        // INVENTORY SLOT → INVENTORY SLOT
        // =========================
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
        ghostRect.sizeDelta = new Vector2(ghostWidth, ghostHeight);

        ghostImage = ghost.GetComponent<Image>();
        ghostImage.type = Image.Type.Simple;
        ghostImage.preserveAspect = true;
        ghostImage.sprite = icon;
        ghostImage.raycastTarget = false;
        ghostImage.color = new Color(1f, 1f, 1f, 1f);

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
    private bool IsPointerInsideSafeUI(PointerEventData eventData)
    {
        if (inventoryUI == null)
            return false;

        Camera cam = canvas.renderMode == RenderMode.ScreenSpaceOverlay
            ? null
            : canvas.worldCamera;

        // Inventory Grid
        if (inventoryUI.inventoryGrid != null &&
            RectTransformUtility.RectangleContainsScreenPoint(
                inventoryUI.inventoryGrid,
                eventData.position,
                cam))
            return true;

        // Craft Panel
        if (inventoryUI.craftPanel != null &&
            RectTransformUtility.RectangleContainsScreenPoint(
                inventoryUI.craftPanel,
                eventData.position,
                cam))
            return true;

        return false;
    }
}
