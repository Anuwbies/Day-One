using UnityEngine;
using System.Collections.Generic;
using UnityEngine.UI;

// For the parent to receive trigger events from its children, 
// the parent MUST have a Rigidbody2D component.
[RequireComponent(typeof(Rigidbody2D))]
public class CraftingTableLogic : MonoBehaviour
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

    // Use a HashSet to track unique player colliders currently in the range trigger.
    private HashSet<Collider2D> playerCollidersInRange = new HashSet<Collider2D>();

    private void Awake()
    {
        ResolveTargetInventoryUI();
    }

    private void OnDisable()
    {
        // Clean up UI state if the object is disabled
        if (playerCollidersInRange.Count > 0)
        {
            playerCollidersInRange.Clear();
            if (interactionCanvas != null)
            {
                interactionCanvas.SetActive(false);
            }
        }

        if (targetInventoryUI != null)
            targetInventoryUI.SetInventoryOpen(false);
    }

    private void Start()
    {
        ResolveTargetInventoryUI();

        // Setup Rigidbody2D to ensure it's static and doesn't interfere with physics
        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Static;
        rb.simulated = true;

        if (interactionCanvas != null)
        {
            interactionCanvas.SetActive(false);
        }

        if (targetInventoryUI != null)
            targetInventoryUI.SetInventoryOpen(false);

        if (toggleCanvasButton != null)
        {
            toggleCanvasButton.onClick.AddListener(ToggleTargetCanvas);
        }
    }

    private void OnDestroy()
    {
        if (toggleCanvasButton != null)
        {
            toggleCanvasButton.onClick.RemoveListener(ToggleTargetCanvas);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // 1. If a range trigger is specified on THIS object, ensure this collision involves it
        if (rangeTrigger != null && !other.IsTouching(rangeTrigger)) return;

        // 2. Check if the entering collider matches our target requirements
        bool isTarget = false;
        if (targetPlayerCollider != null)
        {
            isTarget = (other == targetPlayerCollider);
        }
        else if (other.attachedRigidbody != null && other.attachedRigidbody.CompareTag(targetTag))
        {
            isTarget = true;
        }

        if (isTarget)
        {
            if (playerCollidersInRange.Add(other)) // Only proceed if this collider wasn't already tracked
            {
                // Only show UI if this is the first collider entering
                if (interactionCanvas != null)
                {
                    interactionCanvas.SetActive(true);
                }
            }
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        // 1. Check if this collider matches our target requirements
        bool isTarget = false;
        if (targetPlayerCollider != null)
        {
            isTarget = (other == targetPlayerCollider);
        }
        else if (other.attachedRigidbody != null && other.attachedRigidbody.CompareTag(targetTag))
        {
            isTarget = true;
        }

        if (isTarget)
        {
            // 2. Only decrement if the collider is actually leaving the SPECIFIC range trigger
            if (rangeTrigger == null || !other.IsTouching(rangeTrigger))
            {
                playerCollidersInRange.Remove(other);

                // Only hide UI if ALL colliders of the player have left the range
                if (playerCollidersInRange.Count == 0)
                {
                    if (interactionCanvas != null)
                    {
                        interactionCanvas.SetActive(false);
                    }

                    if (targetInventoryUI != null)
                        targetInventoryUI.SetInventoryOpen(false);
                }
            }
        }
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
            targetInventoryUI.UIType == InventoryUIType.InventoryAndCraftingTable)
            return;

        InventoryUI[] inventoryUIs =
            Object.FindObjectsByType<InventoryUI>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        foreach (InventoryUI inventoryUI in inventoryUIs)
        {
            if (inventoryUI != null &&
                inventoryUI.UIType == InventoryUIType.InventoryAndCraftingTable)
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
