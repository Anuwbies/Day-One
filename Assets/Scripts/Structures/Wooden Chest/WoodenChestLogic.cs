using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// For the parent to receive trigger events from its children,
// the parent MUST have a Rigidbody2D component.
[RequireComponent(typeof(Rigidbody2D))]
public class WoodenChestLogic : MonoBehaviour
{
    private static bool isApplicationQuitting;

    [Header("References")]
    [Tooltip("Drag the child object with the range Trigger Collider here.")]
    [SerializeField] private Collider2D rangeTrigger;
    [SerializeField] private GameObject interactionCanvas;
    [SerializeField] private Button toggleCanvasButton;
    [Tooltip("Optional scene reference. Prefab assets cannot store scene objects, so this will auto-resolve at runtime when left empty.")]
    [SerializeField] private InventoryUI targetInventoryUI;
    [SerializeField] private ChestGridController chestGridController;
    [Min(1)]
    [SerializeField] private int chestSlotCount = 20;
    [Header("Destroyed Drops")]
    [SerializeField] private Vector2 destroyedDropOffset;
    [SerializeField] private Vector2 destroyedDropRadiusXY = new Vector2(0.5f, 0.25f);

    [Header("Trigger Settings")]
    [Tooltip("The tag of the player object (or its Rigidbody).")]
    [SerializeField] private string targetTag = "Player";
    [Tooltip("Optional: If assigned, only this specific player collider will trigger the UI. If left empty, any collider with the correct tag will work.")]
    [SerializeField] private Collider2D targetPlayerCollider;

    private readonly HashSet<Collider2D> playerCollidersInRange = new();
    private ChestInventory chestInventory;
    private bool hasDroppedContents;

    public int ChestSlotCount => chestSlotCount;

#if UNITY_EDITOR
    private void OnValidate()
    {
        ResolveChestInventory();
        ResolveTargetInventoryUI();
        ResolveChestGridController();
    }
#endif

    private void Reset()
    {
        ResolveChestInventory();
        ResolveTargetInventoryUI();
        ResolveChestGridController();
    }

    private void Awake()
    {
        ResolveChestInventory();
        ResolveTargetInventoryUI();
        ResolveChestGridController();
        ApplyChestSlotCount();
    }

    private void Start()
    {
        ResolveChestInventory();
        ResolveTargetInventoryUI();
        ResolveChestGridController();
        ApplyChestSlotCount();

        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Static;
        rb.simulated = true;

        if (interactionCanvas != null)
            interactionCanvas.SetActive(false);

        if (targetInventoryUI != null)
            targetInventoryUI.SetInventoryOpen(false);

        if (toggleCanvasButton != null)
            toggleCanvasButton.onClick.AddListener(ToggleTargetCanvas);
    }

    private void OnDisable()
    {
        if (playerCollidersInRange.Count > 0)
        {
            playerCollidersInRange.Clear();

            if (interactionCanvas != null)
                interactionCanvas.SetActive(false);
        }

        if (targetInventoryUI != null)
            targetInventoryUI.SetInventoryOpen(false);
    }

    private void OnDestroy()
    {
        DropStoredItemsIntoWorld();

        if (toggleCanvasButton != null)
            toggleCanvasButton.onClick.RemoveListener(ToggleTargetCanvas);
    }

    private void OnApplicationQuit()
    {
        isApplicationQuitting = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (rangeTrigger != null && !other.IsTouching(rangeTrigger))
            return;

        if (!IsTargetCollider(other))
            return;

        if (playerCollidersInRange.Add(other) && interactionCanvas != null)
            interactionCanvas.SetActive(true);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!IsTargetCollider(other))
            return;

        if (rangeTrigger != null && other.IsTouching(rangeTrigger))
            return;

        playerCollidersInRange.Remove(other);

        if (playerCollidersInRange.Count == 0)
        {
            if (interactionCanvas != null)
                interactionCanvas.SetActive(false);

            if (targetInventoryUI != null)
                targetInventoryUI.SetInventoryOpen(false);
        }
    }

    private bool IsTargetCollider(Collider2D other)
    {
        if (other == null)
            return false;

        if (targetPlayerCollider != null)
        {
            Collider2D preferredAssignedCollider = ResolvePreferredPlayerBodyCollider(targetPlayerCollider);
            if (preferredAssignedCollider != null)
                return other == preferredAssignedCollider;

            return other == targetPlayerCollider;
        }

        Collider2D preferredCollider = ResolvePreferredPlayerBodyCollider(other);
        return preferredCollider != null && other == preferredCollider;
    }

    public void ToggleTargetCanvas()
    {
        ResolveTargetInventoryUI();
        ResolveChestGridController();
        ApplyChestSlotCount();

        if (targetInventoryUI == null)
        {
            Debug.LogWarning($"No {nameof(InventoryUI)} found for {name}. Assign a scene instance on the placed object or keep one active in the scene.");
            return;
        }

        targetInventoryUI.ToggleInventoryOpen();
    }

    private void ResolveTargetInventoryUI()
    {
        if (targetInventoryUI != null &&
            targetInventoryUI.UIType == InventoryUIType.InventoryAndWoodenChest)
            return;

        InventoryUI[] inventoryUIs =
            Object.FindObjectsByType<InventoryUI>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        foreach (InventoryUI inventoryUI in inventoryUIs)
        {
            if (inventoryUI != null &&
                inventoryUI.UIType == InventoryUIType.InventoryAndWoodenChest)
            {
                targetInventoryUI = inventoryUI;
                return;
            }
        }

        if (targetInventoryUI != null)
            return;

        targetInventoryUI = Object.FindAnyObjectByType<InventoryUI>(FindObjectsInactive.Exclude);
    }

    private void ResolveChestInventory()
    {
        chestInventory = GetComponent<ChestInventory>();

        if (chestInventory == null)
            chestInventory = gameObject.AddComponent<ChestInventory>();
    }

    private void ResolveChestGridController()
    {
        if (chestGridController != null)
            return;

        if (targetInventoryUI == null)
            return;

        chestGridController = targetInventoryUI.GetComponentInChildren<ChestGridController>(true);

        if (chestGridController == null && targetInventoryUI.craftPanel != null)
            chestGridController = targetInventoryUI.craftPanel.GetComponentInChildren<ChestGridController>(true);
    }

    private void ApplyChestSlotCount()
    {
        if (targetInventoryUI == null || chestInventory == null)
            return;

        chestInventory.SetMaxSlots(chestSlotCount);

        ResolveChestGridController();

        if (chestGridController != null)
            chestGridController.BindChest(chestInventory, chestSlotCount, targetInventoryUI);
    }

    private void DropStoredItemsIntoWorld()
    {
        if (hasDroppedContents || !Application.isPlaying || isApplicationQuitting)
            return;

        ResolveChestInventory();
        hasDroppedContents = true;

        if (chestInventory == null || chestInventory.items == null)
            return;

        if (targetInventoryUI != null)
            targetInventoryUI.SetInventoryOpen(false);

        for (int i = 0; i < chestInventory.items.Count; i++)
        {
            InventorySlot slot = chestInventory.items[i];
            if (slot == null || slot.item == null || slot.amount <= 0)
                continue;

            ItemData itemData = slot.item;
            if (!itemData.canDrop)
                continue;

            if (itemData.worldPrefab == null)
            {
                Debug.LogWarning($"Chest '{name}' could not drop '{itemData.itemName}' because it has no world prefab assigned.");
                continue;
            }

            Vector2 randomUnit = Random.insideUnitCircle;
            Vector3 baseDropPosition = transform.position + new Vector3(
                destroyedDropOffset.x,
                destroyedDropOffset.y,
                0f);
            Vector3 spawnPosition = baseDropPosition + new Vector3(
                randomUnit.x * destroyedDropRadiusXY.x,
                randomUnit.y * destroyedDropRadiusXY.y,
                0f);

            GameObject droppedObject = Instantiate(itemData.worldPrefab, spawnPosition, Quaternion.identity);
            Item worldItem = droppedObject.GetComponent<Item>();
            if (worldItem != null)
            {
                worldItem.data = itemData;
                worldItem.amount = slot.amount;
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 center = transform.position + new Vector3(
            destroyedDropOffset.x,
            destroyedDropOffset.y,
            0f);

        Gizmos.color = new Color(1f, 0.6f, 0.1f, 1f);

        const int segments = 32;
        Vector3 prevPoint = center + new Vector3(destroyedDropRadiusXY.x, 0f, 0f);

        for (int i = 1; i <= segments; i++)
        {
            float angle = (i / (float)segments) * Mathf.PI * 2f;
            Vector3 nextPoint = center + new Vector3(
                Mathf.Cos(angle) * destroyedDropRadiusXY.x,
                Mathf.Sin(angle) * destroyedDropRadiusXY.y,
                0f);

            Gizmos.DrawLine(prevPoint, nextPoint);
            prevPoint = nextPoint;
        }

        Gizmos.color = Color.red;
        Gizmos.DrawSphere(center, 0.05f);
    }

    private Collider2D ResolvePreferredPlayerBodyCollider(Collider2D sourceCollider)
    {
        if (sourceCollider == null)
            return null;

        Transform taggedTransform = FindTaggedTransformInHierarchy(sourceCollider.transform, targetTag);
        if (taggedTransform == null && sourceCollider.attachedRigidbody != null)
            taggedTransform = FindTaggedTransformInHierarchy(sourceCollider.attachedRigidbody.transform, targetTag);

        if (taggedTransform == null)
            return null;

        return FindPreferredPlayerBodyCollider(taggedTransform.gameObject);
    }

    private Collider2D FindPreferredPlayerBodyCollider(GameObject playerObj)
    {
        if (playerObj == null)
            return null;

        PlayerAttack playerAttack = playerObj.GetComponentInChildren<PlayerAttack>(true);
        Collider2D attackAreaCollider = playerAttack != null ? playerAttack.attackCollider : null;

        Collider2D rootCollider = playerObj.GetComponent<Collider2D>();
        if (IsValidPlayerBodyCollider(rootCollider, attackAreaCollider))
            return rootCollider;

        Rigidbody2D playerRb = playerObj.GetComponent<Rigidbody2D>();
        Collider2D[] colliders = playerObj.GetComponentsInChildren<Collider2D>(true);
        Collider2D fallback = null;

        for (int i = 0; i < colliders.Length; i++)
        {
            Collider2D candidate = colliders[i];
            if (!IsValidPlayerBodyCollider(candidate, attackAreaCollider))
                continue;

            if (playerRb != null && candidate.attachedRigidbody == playerRb)
                return candidate;

            if (fallback == null)
                fallback = candidate;
        }

        return fallback;
    }

    private bool IsValidPlayerBodyCollider(Collider2D candidate, Collider2D attackAreaCollider)
    {
        return candidate != null &&
               candidate.enabled &&
               !candidate.isTrigger &&
               candidate != attackAreaCollider;
    }

    private Transform FindTaggedTransformInHierarchy(Transform target, string tagToMatch)
    {
        if (target == null || string.IsNullOrWhiteSpace(tagToMatch))
            return null;

        Transform current = target;
        while (current != null)
        {
            if (current.CompareTag(tagToMatch))
                return current;

            current = current.parent;
        }

        return null;
    }
}
