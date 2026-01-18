using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

public class CraftingSlotUI : MonoBehaviour,
    IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler
{
    [Header("UI")]
    public Image itemIcon;
    public TMP_Text amountText;

    [Header("Data")]
    public CraftingSlot slot = new CraftingSlot();

    [Header("Controller")]
    public CraftingGridController craftingGridController;

    private Canvas canvas;
    private CanvasGroup canvasGroup;

    // Drag ghost
    private RectTransform ghostRect;
    private Image ghostImage;

    private void Awake()
    {
        canvas = GetComponentInParent<Canvas>();

        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();

        itemIcon.type = Image.Type.Simple;
        itemIcon.preserveAspect = true;

        itemIcon.enabled = false;
        amountText.text = "";
    }

    // =========================
    // BEGIN DRAG
    // =========================
    public void OnBeginDrag(PointerEventData eventData)
    {
        if (slot.IsEmpty)
        {
            eventData.pointerDrag = null;
            return;
        }

        CreateGhost(slot.item.icon);

        canvasGroup.alpha = 0.4f;
        canvasGroup.blocksRaycasts = false;

        eventData.pointerDrag = gameObject;
    }

    // =========================
    // DRAG
    // =========================
    public void OnDrag(PointerEventData eventData)
    {
        if (ghostRect != null)
            ghostRect.position = eventData.position;
    }

    // =========================
    // END DRAG
    // =========================
    public void OnEndDrag(PointerEventData eventData)
    {
        DestroyGhost();

        canvasGroup.alpha = 1f;
        canvasGroup.blocksRaycasts = true;
    }

    // =========================
    // DROP
    // =========================
    public void OnDrop(PointerEventData eventData)
    {
        if (eventData.pointerDrag == null)
            return;

        // =========================
        // CRAFT → CRAFT (SWAP)
        // =========================
        CraftingSlotUI otherCraft =
            eventData.pointerDrag.GetComponent<CraftingSlotUI>();

        if (otherCraft != null && otherCraft != this)
        {
            SwapWith(otherCraft);
            return;
        }

        // =========================
        // INVENTORY → CRAFT
        // =========================
        InventorySlotDragDrop invDrag =
            eventData.pointerDrag.GetComponent<InventorySlotDragDrop>();

        if (invDrag != null)
        {
            TryReceiveFromInventory(invDrag);
        }
    }

    // =========================
    // LOGIC
    // =========================
    private void SwapWith(CraftingSlotUI other)
    {
        if (other.slot.IsEmpty && slot.IsEmpty)
            return;

        CraftingSlot temp = new CraftingSlot();
        temp.Set(other.slot.item, other.slot.amount);

        other.slot.Set(slot.item, slot.amount);
        slot.Set(temp.item, temp.amount);

        other.Refresh();
        Refresh();

        NotifyGridChanged();
    }

    private void TryReceiveFromInventory(InventorySlotDragDrop invDrag)
    {
        InventoryUI inventoryUI = invDrag.inventoryUI;
        int invIndex = invDrag.slotIndex;

        if (inventoryUI == null ||
            inventoryUI.inventory == null ||
            invIndex < 0 ||
            invIndex >= inventoryUI.inventory.items.Count)
            return;

        InventorySlot invSlot =
            inventoryUI.inventory.items[invIndex];

        if (invSlot == null || invSlot.item == null)
            return;

        if (!slot.IsEmpty)
            return;

        slot.Set(invSlot.item, invSlot.amount);

        inventoryUI.inventory.items[invIndex] = null;
        inventoryUI.inventory.OnInventoryChanged?.Invoke();

        Refresh();
        NotifyGridChanged();
    }

    // =========================
    // UI
    // =========================
    public void Refresh()
    {
        if (slot.IsEmpty)
        {
            itemIcon.enabled = false;
            itemIcon.sprite = null;
            amountText.text = "";
            return;
        }

        itemIcon.enabled = true;
        itemIcon.sprite = slot.item.icon;

        amountText.text = slot.amount > 1
            ? slot.amount.ToString()
            : "";
    }

    public void Clear()
    {
        slot.Clear();
        Refresh();
        NotifyGridChanged();
    }

    // =========================
    // GRID NOTIFY
    // =========================
    private void NotifyGridChanged()
    {
        if (craftingGridController != null)
            craftingGridController.UpdateResultPreview();
    }

    // =========================
    // GHOST
    // =========================
    private void CreateGhost(Sprite icon)
    {
        GameObject ghost = new GameObject(
            "CraftDragGhost",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image)
        );

        ghost.transform.SetParent(canvas.transform, false);

        ghostRect = ghost.GetComponent<RectTransform>();
        ghostRect.sizeDelta = new Vector2(80f, 80f);

        ghostImage = ghost.GetComponent<Image>();
        ghostImage.type = Image.Type.Simple;
        ghostImage.preserveAspect = true;
        ghostImage.sprite = icon;
        ghostImage.raycastTarget = false;
        ghostImage.color = Color.white;

        ghost.transform.SetAsLastSibling();
    }

    private void DestroyGhost()
    {
        if (ghostRect != null)
            Destroy(ghostRect.gameObject);

        ghostRect = null;
        ghostImage = null;
    }
}