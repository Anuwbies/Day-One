using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

public class CraftingSlotUI : MonoBehaviour,
    IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler, IPointerClickHandler
{
    [Header("UI")]
    public Image itemIcon;
    public TMP_Text amountText;

    [Header("Data")]
    public CraftingSlot slot = new CraftingSlot();

    [Header("Controller")]
    public CraftingGridController craftingGridController;

    [Header("Grid Mapping")]
    [Tooltip("Which recipe slot index this UI slot represents. Leave at -1 to use the array position from the controller.")]
    [SerializeField] private int slotIndexOverride = -1;

    public int SlotIndexOverride => slotIndexOverride;

    private Canvas canvas;
    private CanvasGroup canvasGroup;

    // Drag ghost (cursor-following)
    private RectTransform ghostRect;
    private Image ghostImage;

    // Ghost preview state
    private bool isGhostPreview = false;

    private static readonly float REAL_ALPHA = 1f;
    private static readonly float GHOST_ALPHA = 0.4f;

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
    // POINTER CLICK
    // =========================
    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Right)
            return;

        if (slot.IsEmpty)
            return;

        ReturnItemToInventory();
    }

    private void ReturnItemToInventory()
    {
        if (craftingGridController == null ||
            craftingGridController.inventoryUI == null ||
            craftingGridController.inventoryUI.inventory == null)
            return;

        PlayerInventory inventory =
            craftingGridController.inventoryUI.inventory;

        bool added = inventory.AddItem(slot.item, slot.amount);
        if (!added)
            return;

        slot.Clear();
        Refresh();
        NotifyGridChanged();

        inventory.OnInventoryChanged?.Invoke();
    }

    // =========================
    // DRAG
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

    public void OnDrag(PointerEventData eventData)
    {
        if (ghostRect != null)
            ghostRect.position = eventData.position;
    }

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

        CraftingSlotUI otherCraft =
            eventData.pointerDrag.GetComponent<CraftingSlotUI>();

        if (otherCraft != null && otherCraft != this)
        {
            SwapWith(otherCraft);
            return;
        }

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

        InventorySlot invSlot = inventoryUI.inventory.items[invIndex];
        if (invSlot == null || invSlot.item == null)
            return;

        if (!slot.IsEmpty)
        {
            InventorySlot tempInv =
                new InventorySlot(slot.item, slot.amount);

            slot.Set(invSlot.item, invSlot.amount);
            inventoryUI.inventory.items[invIndex] = tempInv;
        }
        else
        {
            slot.Set(invSlot.item, invSlot.amount);
            inventoryUI.inventory.items[invIndex] = null;
        }

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
            SetAlpha(REAL_ALPHA);
            isGhostPreview = false;
            return;
        }

        itemIcon.enabled = true;
        itemIcon.sprite = slot.item.icon;
        amountText.text = slot.amount > 1 ? slot.amount.ToString() : "";

        SetAlpha(REAL_ALPHA);
        isGhostPreview = false;
    }

    public void Clear()
    {
        slot.Clear();
        Refresh();
        NotifyGridChanged();
    }

    // =========================
    // GHOST PREVIEW (RECIPE CLICK)
    // =========================
    public void ShowGhost(ItemData item, int amount)
    {
        if (item == null)
            return;

        itemIcon.enabled = true;
        itemIcon.sprite = item.icon;
        amountText.text = amount > 1 ? amount.ToString() : "";

        SetAlpha(GHOST_ALPHA);
        isGhostPreview = true;
    }

    public void ClearGhost()
    {
        if (!isGhostPreview)
            return;

        itemIcon.enabled = false;
        itemIcon.sprite = null;
        amountText.text = "";

        SetAlpha(REAL_ALPHA);
        isGhostPreview = false;
    }

    private void SetAlpha(float alpha)
    {
        Color c = itemIcon.color;
        c.a = alpha;
        itemIcon.color = c;

        c = amountText.color;
        c.a = alpha;
        amountText.color = c;
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
    // DRAG GHOST (CURSOR)
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
