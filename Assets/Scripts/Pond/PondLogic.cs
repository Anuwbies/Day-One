using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Rigidbody2D))]
public class PondLogic : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject interactionCanvas;
    [SerializeField] private Button drinkButton;

    [Header("Pond Settings")]
    [SerializeField] private float thirstRestoreAmount = 20f;
    [SerializeField, Range(0f, 100f)] private float damageChancePercent = 25f;
    [SerializeField] private float damageAmount = 5f;

    [Header("Movement Settings")]
    [SerializeField, Range(0f, 1f)] private float movementSpeedMultiplier = 0.6f;

    [Header("Trigger Settings")]
    [SerializeField] private string targetTag = "Player";
    [SerializeField] private Collider2D targetPlayerCollider;

    private readonly HashSet<Collider2D> playerCollidersInRange = new HashSet<Collider2D>();
    private PlayerStats playerStats;
    private PlayerMovement playerMovement;
    private bool isDrinkButtonBound;

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
        RemoveMovementSlowdown();
        playerCollidersInRange.Clear();
        playerStats = null;
        playerMovement = null;
        UnbindDrinkButton();

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

        if (!playerCollidersInRange.Add(other))
        {
            return;
        }

        playerStats = ResolvePlayerStats(other);
        playerMovement = ResolvePlayerMovement(other);
        ApplyMovementSlowdown();
        BindDrinkButton();

        if (interactionCanvas != null)
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

        if (playerCollidersInRange.Count != 0)
        {
            return;
        }

        playerStats = null;
        RemoveMovementSlowdown();
        playerMovement = null;
        UnbindDrinkButton();

        if (interactionCanvas != null)
        {
            interactionCanvas.SetActive(false);
        }
    }

    public void DrinkFromPond()
    {
        if (playerCollidersInRange.Count == 0 || playerStats == null)
        {
            return;
        }

        playerStats.AddThirst(thirstRestoreAmount);

        if (damageChancePercent > 0f && Random.value * 100f < damageChancePercent)
        {
            playerStats.TakeDamage(damageAmount);
        }
    }

    private void BindDrinkButton()
    {
        if (isDrinkButtonBound)
        {
            return;
        }

        ResolveDrinkButton();

        if (drinkButton == null)
        {
            Debug.LogWarning($"No drink button assigned or found for pond '{name}'.");
            return;
        }

        drinkButton.onClick.AddListener(DrinkFromPond);
        isDrinkButtonBound = true;
    }

    private void UnbindDrinkButton()
    {
        if (!isDrinkButtonBound || drinkButton == null)
        {
            return;
        }

        drinkButton.onClick.RemoveListener(DrinkFromPond);
        isDrinkButtonBound = false;
    }

    private void ResolveDrinkButton()
    {
        if (drinkButton != null || interactionCanvas == null)
        {
            return;
        }

        drinkButton = interactionCanvas.GetComponentInChildren<Button>(true);
    }

    private PlayerStats ResolvePlayerStats(Collider2D sourceCollider)
    {
        if (sourceCollider == null)
        {
            return null;
        }

        if (sourceCollider.attachedRigidbody != null)
        {
            PlayerStats rigidbodyStats = sourceCollider.attachedRigidbody.GetComponent<PlayerStats>();
            if (rigidbodyStats != null)
            {
                return rigidbodyStats;
            }
        }

        PlayerStats parentStats = sourceCollider.GetComponentInParent<PlayerStats>();
        if (parentStats != null)
        {
            return parentStats;
        }

        if (targetPlayerCollider == null)
        {
            return null;
        }

        if (targetPlayerCollider.attachedRigidbody != null)
        {
            return targetPlayerCollider.attachedRigidbody.GetComponent<PlayerStats>();
        }

        return targetPlayerCollider.GetComponentInParent<PlayerStats>();
    }

    private PlayerMovement ResolvePlayerMovement(Collider2D sourceCollider)
    {
        if (sourceCollider == null)
        {
            return null;
        }

        if (sourceCollider.attachedRigidbody != null)
        {
            PlayerMovement rigidbodyMovement = sourceCollider.attachedRigidbody.GetComponent<PlayerMovement>();
            if (rigidbodyMovement != null)
            {
                return rigidbodyMovement;
            }
        }

        PlayerMovement parentMovement = sourceCollider.GetComponentInParent<PlayerMovement>();
        if (parentMovement != null)
        {
            return parentMovement;
        }

        if (targetPlayerCollider == null)
        {
            return null;
        }

        if (targetPlayerCollider.attachedRigidbody != null)
        {
            return targetPlayerCollider.attachedRigidbody.GetComponent<PlayerMovement>();
        }

        return targetPlayerCollider.GetComponentInParent<PlayerMovement>();
    }

    private void ApplyMovementSlowdown()
    {
        if (playerMovement == null)
        {
            return;
        }

        playerMovement.SetMovementSpeedMultiplier(this, movementSpeedMultiplier);
    }

    private void RemoveMovementSlowdown()
    {
        if (playerMovement == null)
        {
            return;
        }

        playerMovement.ClearMovementSpeedMultiplier(this);
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
