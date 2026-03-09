using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// For the parent to receive trigger events from its children,
// the parent MUST have a Rigidbody2D component.
[RequireComponent(typeof(Rigidbody2D))]
public class WoodenChestLogic : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Drag the child object with the range Trigger Collider here.")]
    [SerializeField] private Collider2D rangeTrigger;
    [SerializeField] private GameObject interactionCanvas;
    [SerializeField] private Button toggleCanvasButton;
    [Tooltip("Optional scene reference. Prefab assets cannot store scene objects, so this will auto-resolve at runtime when left empty.")]
    [SerializeField] private InventoryUI targetInventoryUI;

    [Header("Trigger Settings")]
    [Tooltip("The tag of the player object (or its Rigidbody).")]
    [SerializeField] private string targetTag = "Player";
    [Tooltip("Optional: If assigned, only this specific player collider will trigger the UI. If left empty, any collider with the correct tag will work.")]
    [SerializeField] private Collider2D targetPlayerCollider;

    private readonly HashSet<Collider2D> playerCollidersInRange = new();

    private void Awake()
    {
        ResolveTargetInventoryUI();
    }

    private void Start()
    {
        ResolveTargetInventoryUI();

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
        if (toggleCanvasButton != null)
            toggleCanvasButton.onClick.RemoveListener(ToggleTargetCanvas);
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
        if (targetPlayerCollider != null)
            return other == targetPlayerCollider;

        return other.attachedRigidbody != null &&
               other.attachedRigidbody.CompareTag(targetTag);
    }

    public void ToggleTargetCanvas()
    {
        ResolveTargetInventoryUI();

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
}
