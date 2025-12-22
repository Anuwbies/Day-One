using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class HotbarSlotDragDrop : MonoBehaviour,
    IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler
{
    public HotbarSlot hotbarSlot;

    private Canvas canvas;
    private CanvasGroup canvasGroup;

    private RectTransform ghostRect;
    private Image ghostImage;

    private bool dragBlocked;
    private bool droppedOnSlot;

    private InventoryUI inventoryUI;

    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();

        canvas = GetComponentInParent<Canvas>();

        if (hotbarSlot == null)
            hotbarSlot = GetComponent<HotbarSlot>();

        inventoryUI = FindObjectOfType<InventoryUI>();

        ResetState();
    }

    private void OnEnable() => ResetState();
    private void OnDisable() => ResetState();

    private void ResetState()
    {
        dragBlocked = false;
        droppedOnSlot = false;

        DestroyGhost();

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.blocksRaycasts = true;
            canvasGroup.interactable = true;
        }
    }

    // =========================
    // BEGIN DRAG
    // =========================
    public void OnBeginDrag(PointerEventData eventData)
    {
        ResetState();

        if (hotbarSlot.currentSlot == null ||
            hotbarSlot.currentSlot.item == null)
        {
            BlockDrag(eventData);
            return;
        }

        // Block drag if modal UI is open
        if (inventoryUI != null)
        {
            if ((inventoryUI.contextMenu != null && inventoryUI.contextMenu.IsOpen) ||
                (inventoryUI.splitUI != null && inventoryUI.splitUI.IsOpen))
            {
                BlockDrag(eventData);
                return;
            }
        }

        CreateGhost(hotbarSlot.currentSlot.item.icon);

        canvasGroup.alpha = 0.4f;
        canvasGroup.blocksRaycasts = false;

        eventData.pointerDrag = gameObject;
    }

    // =========================
    // DRAG
    // =========================
    public void OnDrag(PointerEventData eventData)
    {
        if (dragBlocked || ghostRect == null)
            return;

        ghostRect.position = eventData.position;
    }

    // =========================
    // END DRAG
    // =========================
    public void OnEndDrag(PointerEventData eventData)
    {
        CleanupAfterDrag();

        if (dragBlocked || droppedOnSlot)
            return;

        // Dropped outside hotbar → drop to world
        if (!IsPointerInsideHotbar(eventData))
        {
            DropToWorld();
        }
    }

    // =========================
    // DROP ON SLOT
    // =========================
    public void OnDrop(PointerEventData eventData)
    {
        if (dragBlocked || eventData.pointerDrag == null)
            return;

        HotbarSlotDragDrop source =
            eventData.pointerDrag.GetComponent<HotbarSlotDragDrop>();

        if (source == null || source == this)
            return;

        droppedOnSlot = true;
        source.droppedOnSlot = true;

        TryMergeOrSwap(hotbarSlot, source.hotbarSlot);
    }

    // =========================
    // MERGE OR SWAP
    // =========================
    private void TryMergeOrSwap(HotbarSlot targetSlot, HotbarSlot sourceSlot)
    {
        PlayerInventory inv = targetSlot.hotbarUI.playerInventory;

        int t = targetSlot.slotIndex;
        int s = sourceSlot.slotIndex;

        InventorySlot target = inv.items[t];
        InventorySlot source = inv.items[s];

        // MERGE
        if (target != null &&
            source != null &&
            target.item == source.item &&
            target.item.stackable)
        {
            int space = target.item.maxStack - target.amount;
            if (space > 0)
            {
                int transfer = Mathf.Min(space, source.amount);
                target.amount += transfer;
                source.amount -= transfer;

                if (source.amount <= 0)
                    inv.items[s] = null;

                inv.OnInventoryChanged?.Invoke();
                return;
            }
        }

        // SWAP
        InventorySlot temp = inv.items[t];
        inv.items[t] = inv.items[s];
        inv.items[s] = temp;

        inv.OnInventoryChanged?.Invoke();
    }

    // =========================
    // WORLD DROP
    // =========================
    private void DropToWorld()
    {
        if (inventoryUI == null)
            return;

        inventoryUI.DropItemFromSlot(hotbarSlot.slotIndex);
    }

    // =========================
    // HELPERS
    // =========================
    private bool IsPointerInsideHotbar(PointerEventData eventData)
    {
        RectTransform hotbarRect = hotbarSlot.hotbarUI.hotbarRoot;
        if (hotbarRect == null)
            return false;

        return RectTransformUtility.RectangleContainsScreenPoint(
            hotbarRect,
            eventData.position,
            canvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null
                : canvas.worldCamera
        );
    }

    private void CleanupAfterDrag()
    {
        DestroyGhost();

        canvasGroup.alpha = 1f;
        canvasGroup.blocksRaycasts = true;
        canvasGroup.interactable = true;
    }

    private void BlockDrag(PointerEventData eventData)
    {
        dragBlocked = true;
        eventData.pointerDrag = null;
        CleanupAfterDrag();
    }

    private void CreateGhost(Sprite icon)
    {
        GameObject ghost = new GameObject(
            "HotbarGhost",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image)
        );

        ghost.transform.SetParent(canvas.transform, false);
        ghost.transform.SetAsLastSibling();

        ghostRect = ghost.GetComponent<RectTransform>();
        ghostRect.sizeDelta = new Vector2(48f, 48f);

        ghostImage = ghost.GetComponent<Image>();
        ghostImage.sprite = icon;
        ghostImage.raycastTarget = false;
        ghostImage.color = new Color(1f, 1f, 1f, 0.9f);
    }

    private void DestroyGhost()
    {
        if (ghostRect != null)
            Destroy(ghostRect.gameObject);

        ghostRect = null;
        ghostImage = null;
    }
}
