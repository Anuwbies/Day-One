using System.Collections.Generic;
using UnityEngine;

// For the parent to receive trigger events from its children,
// the parent MUST have a Rigidbody2D component.
[RequireComponent(typeof(Rigidbody2D))]
public class WoodenChestLogic : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Drag the child object with the range Trigger Collider here.")]
    [SerializeField] private Collider2D rangeTrigger;
    [SerializeField] private GameObject interactionCanvas;

    [Header("Trigger Settings")]
    [Tooltip("The tag of the player object (or its Rigidbody).")]
    [SerializeField] private string targetTag = "Player";
    [Tooltip("Optional: If assigned, only this specific player collider will trigger the UI. If left empty, any collider with the correct tag will work.")]
    [SerializeField] private Collider2D targetPlayerCollider;

    private readonly HashSet<Collider2D> playerCollidersInRange = new();

    private void Start()
    {
        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Static;
        rb.simulated = true;

        if (interactionCanvas != null)
            interactionCanvas.SetActive(false);
    }

    private void OnDisable()
    {
        if (playerCollidersInRange.Count == 0)
            return;

        playerCollidersInRange.Clear();

        if (interactionCanvas != null)
            interactionCanvas.SetActive(false);
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

        if (playerCollidersInRange.Count == 0 && interactionCanvas != null)
            interactionCanvas.SetActive(false);
    }

    private bool IsTargetCollider(Collider2D other)
    {
        if (targetPlayerCollider != null)
            return other == targetPlayerCollider;

        return other.attachedRigidbody != null &&
               other.attachedRigidbody.CompareTag(targetTag);
    }
}
