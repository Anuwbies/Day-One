using UnityEngine;
using System.Collections.Generic;

public class PlayerPickup : MonoBehaviour
{
    private PlayerInventory inventory;

    private List<Item> itemsInRange = new List<Item>();
    private Item currentTargetItem;
    [SerializeField] private Collider2D pickupBodyCollider;

    [Header("Pickup Settings")]
    [SerializeField] private float pickupInterval = 0.15f;
    private float nextPickupTime;

    [Header("Pickup Hint Settings")]
    [SerializeField] private bool showPickupHintsPermanently = false;
    [SerializeField, Min(0)] private int maxHintsToShow = 10;

    private static int totalItemsPickedUp = 0;

    // This ensures the counter resets whenever you press Play in the editor,
    // even if "Domain Reload" is disabled in Enter Play Mode settings.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetCounter()
    {
        totalItemsPickedUp = 0;
    }

    private void Awake()
    {
        inventory = GetComponent<PlayerInventory>();
        ResolvePickupBodyCollider();
    }

    private void OnValidate()
    {
        ResolvePickupBodyCollider();
    }

    private bool CanShowPickupHints()
    {
        return showPickupHintsPermanently || totalItemsPickedUp < maxHintsToShow;
    }

    private void Update()
    {
        // CLEANUP: Remove any items that were destroyed (e.g., by other scripts or duplicate references)
        // This prevents trying to pick up "ghost" items
        bool removedDestroyedItem = false;
        for (int i = itemsInRange.Count - 1; i >= 0; i--)
        {
            if (itemsInRange[i] == null)
            {
                itemsInRange.RemoveAt(i);
                removedDestroyedItem = true;
            }
        }

        if (removedDestroyedItem)
        {
            RefreshHintVisibility();
        }

        if (itemsInRange.Count == 0) return;

        currentTargetItem = GetClosestItemInRange();
        RefreshHintVisibility();

        if (Input.GetKey(KeyCode.Space))
        {
            if (Time.time < nextPickupTime) return;

            Item targetItem = currentTargetItem;
            if (targetItem == null)
            {
                return;
            }

            // DEBUG: Check if the item actually has data assigned
            if (targetItem.data == null)
            {
                Debug.LogWarning($"Cannot pick up '{targetItem.name}' - ItemData is missing in Inspector!");
                itemsInRange.Remove(targetItem); // Remove invalid item to avoid getting stuck
                RefreshHintVisibility();
                return;
            }

            bool pickedUp = inventory.AddItem(targetItem.data, targetItem.amount);

            if (!pickedUp)
            {
                // Only log when inventory is full, but with a cooldown to avoid log spam
                if (Time.time > nextPickupTime + 1f)
                {
                    Debug.Log("Cannot pick up item - inventory is full.");
                    nextPickupTime = Time.time;
                }
                return;
            }

            // Success
            totalItemsPickedUp++;
            nextPickupTime = Time.time + pickupInterval;
            itemsInRange.Remove(targetItem);
            if (currentTargetItem == targetItem)
            {
                currentTargetItem = null;
            }
            Destroy(targetItem.gameObject);

            // If we just hit the limit, hide hints for all other items currently in range
            if (!showPickupHintsPermanently && totalItemsPickedUp >= maxHintsToShow)
            {
                foreach (Item item in itemsInRange)
                {
                    if (item != null) item.ToggleHint(false);
                }
            }
            else
            {
                RefreshHintVisibility();
            }
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // Filter: only allow if it's touching the body collider (if we have one defined)
        if (pickupBodyCollider != null && !other.IsTouching(pickupBodyCollider))
        {
            return;
        }

        Item item = other.GetComponentInParent<Item>();

        // FIX: Check if list already contains this item to prevent duplicates
        if (item != null && !itemsInRange.Contains(item))
        {
            itemsInRange.Add(item);
            RefreshHintVisibility();
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        // Filter: if we have a body collider and it's STILL touching it, don't remove yet
        // (this Exit event might have been triggered by the attack collider exiting)
        if (pickupBodyCollider != null && other.IsTouching(pickupBodyCollider))
        {
            return;
        }

        Item item = other.GetComponentInParent<Item>();
        if (item != null)
        {
            item.ToggleHint(false); // Hide hint when out of range
            itemsInRange.Remove(item);
            if (currentTargetItem == item)
            {
                currentTargetItem = null;
            }
            RefreshHintVisibility();
        }
    }

    private Item GetClosestItemInRange()
    {
        Item closestItem = null;
        float closestDistanceSqr = float.MaxValue;
        Vector3 playerPosition = transform.position;

        for (int i = 0; i < itemsInRange.Count; i++)
        {
            Item item = itemsInRange[i];
            if (item == null)
            {
                continue;
            }

            float distanceSqr = (item.transform.position - playerPosition).sqrMagnitude;
            if (distanceSqr < closestDistanceSqr)
            {
                closestDistanceSqr = distanceSqr;
                closestItem = item;
            }
        }

        return closestItem;
    }

    private void RefreshHintVisibility()
    {
        if (!CanShowPickupHints())
        {
            currentTargetItem = null;
            for (int i = 0; i < itemsInRange.Count; i++)
            {
                if (itemsInRange[i] != null)
                {
                    itemsInRange[i].ToggleHint(false);
                }
            }

            return;
        }

        currentTargetItem = GetClosestItemInRange();

        for (int i = 0; i < itemsInRange.Count; i++)
        {
            Item item = itemsInRange[i];
            if (item != null)
            {
                item.ToggleHint(item == currentTargetItem);
            }
        }
    }

    private void ResolvePickupBodyCollider()
    {
        if (pickupBodyCollider != null)
        {
            return;
        }

        Transform colliderTransform = transform.Find("Collider");
        if (colliderTransform != null)
        {
            pickupBodyCollider = colliderTransform.GetComponent<Collider2D>();
            if (pickupBodyCollider != null)
            {
                return;
            }
        }

        PlayerAttack playerAttack = GetComponentInChildren<PlayerAttack>(true);
        Collider2D attackCollider = playerAttack != null ? playerAttack.attackCollider : null;
        Collider2D[] colliders = GetComponentsInChildren<Collider2D>(true);

        for (int i = 0; i < colliders.Length; i++)
        {
            Collider2D candidate = colliders[i];
            if (candidate == null || candidate == attackCollider || candidate.isTrigger)
            {
                continue;
            }

            pickupBodyCollider = candidate;
            return;
        }
    }
}
