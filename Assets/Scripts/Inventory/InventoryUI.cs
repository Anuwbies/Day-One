using UnityEngine;

public class InventoryUI : MonoBehaviour
{
    public PlayerInventory inventory;
    public InventorySlotUI[] slots;
    public GameObject inventoryWindow;
    public HotbarUI hotbar;

    [Header("World Drop")]
    public Transform dropOrigin;          // usually the player
    public float dropRadius = 0.5f;

    [Header("UI")]
    public RectTransform inventoryGrid;

    private bool isOpen = false;

    private void Start()
    {
        if (inventoryWindow != null)
            inventoryWindow.SetActive(false);

        if (inventory != null)
            inventory.OnInventoryChanged += RefreshUI;

        SetupSlotIndices();
        RefreshUI();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.I))
        {
            isOpen = !isOpen;

            if (inventoryWindow != null)
                inventoryWindow.SetActive(isOpen);

            if (isOpen)
                RefreshUI();
        }
    }

    private void SetupSlotIndices()
    {
        if (slots == null) return;

        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] == null) continue;

            var drag = slots[i].GetComponent<InventorySlotDragDrop>();
            if (drag == null)
                drag = slots[i].gameObject.AddComponent<InventorySlotDragDrop>();

            drag.slotIndex = i;
            drag.inventoryUI = this;
        }
    }

    public void RefreshUI()
    {
        if (inventory == null || inventory.items == null || slots == null)
            return;

        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] == null) continue;

            if (i < inventory.items.Count &&
                inventory.items[i] != null &&
                inventory.items[i].item != null)
            {
                slots[i].SetSlot(
                    inventory.items[i].item.icon,
                    inventory.items[i].amount
                );
            }
            else
            {
                slots[i].ClearSlot();
            }
        }

        if (hotbar != null)
            hotbar.Refresh();
    }

    public void SwapOrMove(int from, int to)
    {
        if (inventory == null || inventory.items == null)
            return;

        if (from < 0 || from >= inventory.items.Count) return;
        if (to < 0 || to >= inventory.items.Count) return;

        var temp = inventory.items[from];
        inventory.items[from] = inventory.items[to];
        inventory.items[to] = temp;

        inventory.OnInventoryChanged?.Invoke();
    }

    public void DropItemFromSlot(int slotIndex)
    {
        if (inventory == null ||
            inventory.items == null ||
            slotIndex < 0 ||
            slotIndex >= inventory.items.Count)
            return;

        var invSlot = inventory.items[slotIndex];
        if (invSlot == null || invSlot.item == null)
            return;

        ItemData data = invSlot.item;

        if (data.worldPrefab == null)
        {
            Debug.LogError($"Item '{data.itemName}' has no worldPrefab assigned.");
            return;
        }

        Vector3 origin = dropOrigin != null ? dropOrigin.position : Vector3.zero;
        Vector2 offset = Random.insideUnitCircle * dropRadius;

        GameObject go = Instantiate(
            data.worldPrefab,
            origin + new Vector3(offset.x, offset.y, 0f),
            Quaternion.identity
        );

        Item worldItem = go.GetComponent<Item>();
        if (worldItem != null)
        {
            worldItem.data = data;
            worldItem.amount = invSlot.amount;
        }

        inventory.items[slotIndex] = null;
        inventory.OnInventoryChanged?.Invoke();
    }
}
