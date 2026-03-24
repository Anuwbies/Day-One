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

        if (IsTargetCollider(other))
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
        if (IsTargetCollider(other))
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

    private bool IsTargetCollider(Collider2D candidate)
    {
        if (candidate == null)
        {
            return false;
        }

        if (targetPlayerCollider != null)
        {
            Collider2D preferredAssignedCollider = ResolvePreferredPlayerBodyCollider(targetPlayerCollider);
            if (preferredAssignedCollider != null)
            {
                return candidate == preferredAssignedCollider;
            }

            return candidate == targetPlayerCollider;
        }

        Collider2D preferredCollider = ResolvePreferredPlayerBodyCollider(candidate);
        return preferredCollider != null && candidate == preferredCollider;
    }

    private Collider2D ResolvePreferredPlayerBodyCollider(Collider2D sourceCollider)
    {
        if (sourceCollider == null)
        {
            return null;
        }

        Transform taggedTransform = FindTaggedTransformInHierarchy(sourceCollider.transform, targetTag);
        if (taggedTransform == null && sourceCollider.attachedRigidbody != null)
        {
            taggedTransform = FindTaggedTransformInHierarchy(sourceCollider.attachedRigidbody.transform, targetTag);
        }

        if (taggedTransform == null)
        {
            return null;
        }

        return FindPreferredPlayerBodyCollider(taggedTransform.gameObject);
    }

    private Collider2D FindPreferredPlayerBodyCollider(GameObject playerObj)
    {
        if (playerObj == null)
        {
            return null;
        }

        PlayerAttack playerAttack = playerObj.GetComponentInChildren<PlayerAttack>(true);
        Collider2D attackAreaCollider = playerAttack != null ? playerAttack.attackCollider : null;

        Collider2D rootCollider = playerObj.GetComponent<Collider2D>();
        if (IsValidPlayerBodyCollider(rootCollider, attackAreaCollider))
        {
            return rootCollider;
        }

        Rigidbody2D playerRb = playerObj.GetComponent<Rigidbody2D>();
        Collider2D[] colliders = playerObj.GetComponentsInChildren<Collider2D>(true);
        Collider2D fallback = null;

        for (int i = 0; i < colliders.Length; i++)
        {
            Collider2D candidate = colliders[i];
            if (!IsValidPlayerBodyCollider(candidate, attackAreaCollider))
            {
                continue;
            }

            if (playerRb != null && candidate.attachedRigidbody == playerRb)
            {
                return candidate;
            }

            if (fallback == null)
            {
                fallback = candidate;
            }
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
        {
            return null;
        }

        Transform current = target;
        while (current != null)
        {
            if (current.CompareTag(tagToMatch))
            {
                return current;
            }

            current = current.parent;
        }

        return null;
    }
}
