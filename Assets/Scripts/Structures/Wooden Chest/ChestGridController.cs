using UnityEngine;
using System.Collections.Generic;

public class ChestGridController : MonoBehaviour
{
    [SerializeField] private InventoryUI targetInventoryUI;
    [SerializeField] private ChestSlotUI slotPrefab;
    [SerializeField] private Transform slotParent;

    private readonly List<InventorySlotUI> generatedSlots = new();
    private InventoryUI chestInventoryUI;

    private void Start()
    {
        ResolveReferences();
    }

    public void BindChest(ChestInventory chestInventory, int slotCount, InventoryUI playerInventoryUI = null)
    {
        if (playerInventoryUI != null)
            targetInventoryUI = playerInventoryUI;

        ResolveReferences();

        if (targetInventoryUI == null || chestInventory == null)
            return;

        ChestSlotUI prefab = ResolveSlotPrefab();
        if (prefab == null)
        {
            Debug.LogWarning($"{nameof(ChestGridController)} on {name} needs a slot prefab or an existing InventorySlotUI child to use as a template.");
            return;
        }

        EnsureSlotCount(Mathf.Max(0, slotCount), prefab);
        EnsureChestInventoryUI();

        chestInventory.SetMaxSlots(slotCount);
        chestInventoryUI.BindInventory(chestInventory);
        chestInventoryUI.SetSlots(generatedSlots.ToArray());
    }

    private void ResolveReferences()
    {
        if (targetInventoryUI == null)
            targetInventoryUI = GetComponentInParent<InventoryUI>(true);

        if (slotParent == null)
            slotParent = transform;
    }

    private ChestSlotUI ResolveSlotPrefab()
    {
        if (slotPrefab != null)
            return slotPrefab;

        ChestSlotUI template = GetComponentInChildren<ChestSlotUI>(true);
        if (template != null)
        {
            slotPrefab = template;
            slotPrefab.gameObject.SetActive(false);
        }

        return slotPrefab;
    }

    private void EnsureSlotCount(int slotCount, ChestSlotUI prefab)
    {
        while (generatedSlots.Count > slotCount)
        {
            int lastIndex = generatedSlots.Count - 1;
            InventorySlotUI slot = generatedSlots[lastIndex];
            if (slot != null)
                Destroy(slot.gameObject);

            generatedSlots.RemoveAt(lastIndex);
        }

        while (generatedSlots.Count < slotCount)
        {
            ChestSlotUI slot = Instantiate(prefab, slotParent);
            slot.gameObject.name = $"{prefab.gameObject.name} ({generatedSlots.Count + 1})";
            slot.gameObject.SetActive(true);
            slot.ClearSlot();
            generatedSlots.Add(slot);
        }
    }

    private void EnsureChestInventoryUI()
    {
        if (chestInventoryUI == null)
            chestInventoryUI = GetComponent<InventoryUI>();

        if (chestInventoryUI == null)
            chestInventoryUI = gameObject.AddComponent<InventoryUI>();

        chestInventoryUI.enabled = false;
        chestInventoryUI.inventoryGrid = (slotParent as RectTransform) ?? GetComponent<RectTransform>();
        chestInventoryUI.craftPanel = null;
        chestInventoryUI.hotbar = null;
        chestInventoryUI.contextMenu = targetInventoryUI.contextMenu;
        chestInventoryUI.splitUI = targetInventoryUI.splitUI;
        chestInventoryUI.destroyUI = targetInventoryUI.destroyUI;
        chestInventoryUI.dropOrigin = targetInventoryUI.dropOrigin;
        chestInventoryUI.dropOriginOffset = targetInventoryUI.dropOriginOffset;
        chestInventoryUI.dropRadiusXY = targetInventoryUI.dropRadiusXY;
        chestInventoryUI.SetAdditionalSafePanels(new[]
        {
            targetInventoryUI.inventoryGrid,
            targetInventoryUI.craftPanel
        });
    }
}
