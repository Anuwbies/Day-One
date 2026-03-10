using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class InventorySlotDragDrop : MonoBehaviour,
    IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler, IPointerClickHandler,
    IPointerDownHandler
{
    private static bool suppressNextLeftClick;
    private static bool suppressNextRightClick;
    private static InventorySlotDragDrop activeSplitDragSource;
    private static InventorySlotDragDrop pendingSplitDragSource;
    private static Vector2 pendingSplitDragStartPosition;

    private const float DoubleClickThreshold = 0.25f;
    private const float SplitDragStartDistance = 8f;

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

    private RectTransform ghostRect;
    private Image ghostImage;

    private bool droppedOnSlot;
    private bool dragBlocked;

    public static bool ConsumeSuppressedRightClick()
    {
        if (!suppressNextRightClick)
            return false;

        suppressNextRightClick = false;
        return true;
    }

    private void Awake()
    {
        slotCanvasGroup = GetComponent<CanvasGroup>();
        if (slotCanvasGroup == null)
            slotCanvasGroup = gameObject.AddComponent<CanvasGroup>();

        canvas = GetComponentInParent<Canvas>();
    }

    private void Update()
    {
        if (pendingSplitDragSource == this)
        {
            if (!Input.GetMouseButton(1))
            {
                pendingSplitDragSource = null;
            }
            else if (Vector2.Distance(Input.mousePosition, pendingSplitDragStartPosition) >= SplitDragStartDistance)
            {
                BeginSplitDrag();
            }
        }

        if (activeSplitDragSource != this)
            return;

        if (ghostRect != null)
            ghostRect.position = Input.mousePosition;

        if (Input.GetMouseButtonDown(0))
        {
            TryDropSingleItemAtCursor();
        }

        if (Input.GetMouseButtonUp(1))
        {
            EndSplitDrag();
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Right)
        {
            if (CanStartSplitDrag())
            {
                pendingSplitDragSource = this;
                pendingSplitDragStartPosition = eventData.position;
            }

            return;
        }

        if (eventData.button != PointerEventData.InputButton.Left)
            return;

        if (activeSplitDragSource == null || activeSplitDragSource == this)
            return;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (suppressNextLeftClick && eventData.button == PointerEventData.InputButton.Left)
        {
            suppressNextLeftClick = false;
            return;
        }

        if (eventData.button != PointerEventData.InputButton.Left)
            return;

        if (inventoryUI == null || inventoryUI.ConsumeClickThisFrame)
            return;

        float time = Time.unscaledTime;
        if (time - lastClickTime <= DoubleClickThreshold)
        {
            inventoryUI.CombineAllSameItems(slotIndex);
            lastClickTime = 0f;
        }
        else
        {
            lastClickTime = time;
        }
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        dragBlocked = false;

        if (eventData.button != PointerEventData.InputButton.Left)
        {
            dragBlocked = true;
            eventData.pointerDrag = null;
            return;
        }

        if (inventoryUI != null)
        {
            if (inventoryUI.splitUI != null && inventoryUI.splitUI.IsOpen)
                dragBlocked = true;

            if (inventoryUI.contextMenu != null && inventoryUI.contextMenu.IsOpen)
                dragBlocked = true;
        }

        if (dragBlocked)
        {
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

    public void OnDrag(PointerEventData eventData)
    {
        if (dragBlocked)
            return;

        if (ghostRect != null)
            ghostRect.position = eventData.position;
    }

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

    public void OnDrop(PointerEventData eventData)
    {
        if (dragBlocked)
            return;

        if (eventData.pointerDrag == null)
            return;

        CraftingSlotUI craftSource = eventData.pointerDrag.GetComponent<CraftingSlotUI>();
        if (craftSource != null)
        {
            if (craftSource.slot == null || craftSource.slot.IsEmpty)
                return;

            InventorySlot invSlot = inventoryUI.inventory.items[slotIndex];
            if (invSlot != null)
            {
                InventorySlot tempInv = new InventorySlot(craftSource.slot.item, craftSource.slot.amount);
                craftSource.slot.Set(invSlot.item, invSlot.amount);
                inventoryUI.inventory.items[slotIndex] = tempInv;
                craftSource.Refresh();
            }
            else
            {
                inventoryUI.inventory.items[slotIndex] =
                    new InventorySlot(craftSource.slot.item, craftSource.slot.amount);
                craftSource.Clear();
            }

            inventoryUI.inventory.OnInventoryChanged?.Invoke();
            return;
        }

        InventorySlotDragDrop source = eventData.pointerDrag.GetComponent<InventorySlotDragDrop>();
        if (source == null || source == this)
            return;

        droppedOnSlot = true;
        source.droppedOnSlot = true;

        if (source.inventoryUI == null ||
            source.inventoryUI.inventory == null ||
            inventoryUI == null ||
            inventoryUI.inventory == null)
            return;

        if (source.inventoryUI != inventoryUI)
        {
            MoveBetweenInventories(
                source.inventoryUI.inventory,
                source.slotIndex,
                inventoryUI.inventory,
                slotIndex
            );
            return;
        }

        inventoryUI.TryMergeOrSwap(source.slotIndex, slotIndex);
    }

    private bool CanStartSplitDrag()
    {
        if (activeSplitDragSource != null)
            return false;

        if (inventoryUI == null || inventoryUI.inventory == null)
            return false;

        if (slotIndex < 0 || slotIndex >= inventoryUI.inventory.items.Count)
            return false;

        if (inventoryUI.splitUI != null && inventoryUI.splitUI.IsOpen)
            return false;

        if (inventoryUI.contextMenu != null && inventoryUI.contextMenu.IsOpen)
            return false;

        InventorySlot slot = inventoryUI.inventory.items[slotIndex];
        return slot != null && slot.item != null && slot.amount > 0;
    }

    private void BeginSplitDrag()
    {
        pendingSplitDragSource = null;
        activeSplitDragSource = this;
        suppressNextRightClick = true;
        CreateGhostForCurrentSlot();
        slotCanvasGroup.alpha = 0.4f;
        slotCanvasGroup.blocksRaycasts = false;
    }

    private void EndSplitDrag()
    {
        if (activeSplitDragSource == this)
            activeSplitDragSource = null;

        pendingSplitDragSource = null;
        DestroyGhost();
        slotCanvasGroup.alpha = 1f;
        slotCanvasGroup.blocksRaycasts = true;
    }

    private void CreateGhostForCurrentSlot()
    {
        if (inventoryUI == null || inventoryUI.inventory == null)
            return;

        if (slotIndex < 0 || slotIndex >= inventoryUI.inventory.items.Count)
            return;

        InventorySlot slot = inventoryUI.inventory.items[slotIndex];
        if (slot == null || slot.item == null)
            return;

        DestroyGhost();
        CreateGhost(slot.item.icon);

        if (ghostRect != null)
            ghostRect.position = Input.mousePosition;
    }

    private void CreateGhost(Sprite icon)
    {
        if (canvas == null)
            return;

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

    private void TryDropSingleItemAtCursor()
    {
        if (EventSystem.current == null)
            return;

        PointerEventData pointerData = new PointerEventData(EventSystem.current)
        {
            position = Input.mousePosition
        };

        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pointerData, results);

        foreach (RaycastResult result in results)
        {
            InventorySlotDragDrop inventoryTarget =
                result.gameObject.GetComponentInParent<InventorySlotDragDrop>();
            if (inventoryTarget != null && inventoryTarget != this)
            {
                TryDropSingleItemToInventoryTarget(inventoryTarget);
                return;
            }

            CraftingSlotUI craftTarget =
                result.gameObject.GetComponentInParent<CraftingSlotUI>();
            if (craftTarget != null)
            {
                TryDropSingleItemToCraftTarget(craftTarget);
                return;
            }
        }
    }

    private void TryDropSingleItemToInventoryTarget(InventorySlotDragDrop target)
    {
        if (inventoryUI == null ||
            inventoryUI.inventory == null ||
            target == null ||
            target == this ||
            target.inventoryUI == null ||
            target.inventoryUI.inventory == null)
            return;

        bool moved = MoveSingleItemBetweenInventories(
            inventoryUI.inventory,
            slotIndex,
            target.inventoryUI.inventory,
            target.slotIndex
        );

        if (!moved)
            return;

        suppressNextLeftClick = true;
        CreateGhostForCurrentSlot();

        InventorySlot sourceSlot = inventoryUI.inventory.items[slotIndex];
        if (sourceSlot == null || sourceSlot.item == null)
            EndSplitDrag();
    }

    private void TryDropSingleItemToCraftTarget(CraftingSlotUI target)
    {
        if (target == null)
            return;

        bool moved = target.TryReceiveSingleFromInventory(this);
        if (!moved)
            return;

        suppressNextLeftClick = true;
        CreateGhostForCurrentSlot();

        InventorySlot sourceSlot = inventoryUI.inventory.items[slotIndex];
        if (sourceSlot == null || sourceSlot.item == null)
            EndSplitDrag();
    }

    private bool IsPointerInsideSafeUI(PointerEventData eventData)
    {
        if (inventoryUI == null)
            return false;

        Camera cam = canvas != null && canvas.renderMode == RenderMode.ScreenSpaceOverlay
            ? null
            : canvas != null ? canvas.worldCamera : null;

        return inventoryUI.IsPointerInsideSafeUI(eventData.position, cam);
    }

    private static void MoveBetweenInventories(
        PlayerInventory sourceInventory,
        int sourceIndex,
        PlayerInventory targetInventory,
        int targetIndex)
    {
        if (sourceInventory == null || targetInventory == null)
            return;

        if (sourceIndex < 0 || sourceIndex >= sourceInventory.items.Count ||
            targetIndex < 0 || targetIndex >= targetInventory.items.Count)
            return;

        InventorySlot sourceSlot = sourceInventory.items[sourceIndex];
        if (sourceSlot == null || sourceSlot.item == null)
            return;

        InventorySlot targetSlot = targetInventory.items[targetIndex];

        if (targetSlot == null || targetSlot.item == null)
        {
            targetInventory.items[targetIndex] =
                new InventorySlot(sourceSlot.item, sourceSlot.amount);
            sourceInventory.items[sourceIndex] = null;
            sourceInventory.OnInventoryChanged?.Invoke();
            targetInventory.OnInventoryChanged?.Invoke();
            return;
        }

        if (targetSlot.item == sourceSlot.item && sourceSlot.item.stackable)
        {
            int spaceLeft = sourceSlot.item.maxStack - targetSlot.amount;
            if (spaceLeft > 0)
            {
                int transferAmount = Mathf.Min(spaceLeft, sourceSlot.amount);
                targetSlot.amount += transferAmount;
                sourceSlot.amount -= transferAmount;

                if (sourceSlot.amount <= 0)
                    sourceInventory.items[sourceIndex] = null;

                sourceInventory.OnInventoryChanged?.Invoke();
                targetInventory.OnInventoryChanged?.Invoke();
                return;
            }
        }

        sourceInventory.items[sourceIndex] =
            new InventorySlot(targetSlot.item, targetSlot.amount);
        targetInventory.items[targetIndex] =
            new InventorySlot(sourceSlot.item, sourceSlot.amount);

        sourceInventory.OnInventoryChanged?.Invoke();
        targetInventory.OnInventoryChanged?.Invoke();
    }

    private static bool MoveSingleItemBetweenInventories(
        PlayerInventory sourceInventory,
        int sourceIndex,
        PlayerInventory targetInventory,
        int targetIndex)
    {
        if (sourceInventory == null || targetInventory == null)
            return false;

        if (sourceIndex < 0 || sourceIndex >= sourceInventory.items.Count ||
            targetIndex < 0 || targetIndex >= targetInventory.items.Count)
            return false;

        InventorySlot sourceSlot = sourceInventory.items[sourceIndex];
        if (sourceSlot == null || sourceSlot.item == null || sourceSlot.amount <= 0)
            return false;

        InventorySlot targetSlot = targetInventory.items[targetIndex];

        if (targetSlot == null || targetSlot.item == null)
        {
            targetInventory.items[targetIndex] = new InventorySlot(sourceSlot.item, 1);
        }
        else
        {
            if (targetSlot.item != sourceSlot.item || !sourceSlot.item.stackable)
                return false;

            if (targetSlot.amount >= sourceSlot.item.maxStack)
                return false;

            targetSlot.amount += 1;
        }

        sourceSlot.amount -= 1;
        if (sourceSlot.amount <= 0)
            sourceInventory.items[sourceIndex] = null;

        sourceInventory.OnInventoryChanged?.Invoke();
        if (targetInventory != sourceInventory)
            targetInventory.OnInventoryChanged?.Invoke();

        return true;
    }
}
