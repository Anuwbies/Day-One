using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class PondCanvasTrigger : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject interactionCanvas;

    [Header("Trigger Settings")]
    [SerializeField] private string targetTag = "Player";
    [SerializeField] private Collider2D targetPlayerCollider;

    private readonly HashSet<Collider2D> playerCollidersInRange = new HashSet<Collider2D>();

    private void Awake()
    {
        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Static;
        rb.simulated = true;

        if (interactionCanvas != null)
        {
            interactionCanvas.SetActive(false);
        }
    }

    private void OnDisable()
    {
        playerCollidersInRange.Clear();

        if (interactionCanvas != null)
        {
            interactionCanvas.SetActive(false);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!IsTargetCollider(other))
        {
            return;
        }

        if (playerCollidersInRange.Add(other) && interactionCanvas != null)
        {
            interactionCanvas.SetActive(true);
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!IsTargetCollider(other))
        {
            return;
        }

        if (!playerCollidersInRange.Remove(other))
        {
            return;
        }

        if (playerCollidersInRange.Count == 0 && interactionCanvas != null)
        {
            interactionCanvas.SetActive(false);
        }
    }

    private bool IsTargetCollider(Collider2D candidate)
    {
        if (candidate == null)
        {
            return false;
        }

        if (targetPlayerCollider != null)
        {
            return candidate == targetPlayerCollider;
        }

        if (candidate.isTrigger)
        {
            return false;
        }

        if (candidate.CompareTag(targetTag))
        {
            return true;
        }

        return candidate.attachedRigidbody != null &&
               candidate.attachedRigidbody.CompareTag(targetTag);
    }
}
