using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// For the parent to receive trigger events from its children,
// the parent MUST have a Rigidbody2D component.
[RequireComponent(typeof(Rigidbody2D))]
public class BushBerryLogic : MonoBehaviour
{
    private static bool isApplicationQuitting;

    [Header("References")]
    [Tooltip("Drag the child object with the range Trigger Collider here.")]
    [SerializeField] private Collider2D rangeTrigger;
    [SerializeField] private GameObject interactionCanvas;
    [SerializeField] private Button harvestButton;
    [SerializeField] private SpriteRenderer bushSpriteRenderer;

    [Header("Harvest Settings")]
    [SerializeField] private ItemData berryItem;
    [SerializeField] private int minHarvestAmount = 1;
    [SerializeField] private int maxHarvestAmount = 3;
    [SerializeField] private Sprite bushWithBerrySprite;
    [SerializeField] private Sprite bushWithoutBerrySprite;
    [SerializeField] private float minRegrowTimeMinutes = 0.5f;
    [SerializeField] private float maxRegrowTimeMinutes = 1.5f;

    [Header("Destroyed Drops")]
    [SerializeField] private Vector2 destroyedDropOffset;
    [SerializeField] private Vector2 destroyedDropRadiusXY = new Vector2(0.35f, 0.2f);

    [Header("Trigger Settings")]
    [Tooltip("The tag of the player object (or its Rigidbody).")]
    [SerializeField] private string targetTag = "Player";
    [Tooltip("Optional: If assigned, only this specific player collider will trigger the UI. If left empty, any collider with the correct tag will work.")]
    [SerializeField] private Collider2D targetPlayerCollider;

    private readonly HashSet<Collider2D> playerCollidersInRange = new HashSet<Collider2D>();
    private PlayerInventory playerInventory;
    private bool hasBerries = true;
    private float regrowTimer;
    private bool isRegrowing;

    private void Start()
    {
        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Static;
        rb.simulated = true;

        if (interactionCanvas != null)
        {
            interactionCanvas.SetActive(false);
        }

        if (harvestButton != null)
        {
            harvestButton.onClick.AddListener(HarvestBerry);
        }

        UpdateBushVisuals();
    }

    private void OnDestroy()
    {
        DropBerriesIfGrown();

        if (harvestButton != null)
        {
            harvestButton.onClick.RemoveListener(HarvestBerry);
        }
    }

    private void OnApplicationQuit()
    {
        isApplicationQuitting = true;
    }

    private void Update()
    {
        if (!isRegrowing)
        {
            return;
        }

        regrowTimer -= Time.deltaTime;
        if (regrowTimer <= 0f)
        {
            hasBerries = true;
            isRegrowing = false;
            UpdateBushVisuals();
        }
    }

    private void OnDisable()
    {
        playerCollidersInRange.Clear();
        playerInventory = null;

        if (interactionCanvas != null)
        {
            interactionCanvas.SetActive(false);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (rangeTrigger != null && !other.IsTouching(rangeTrigger))
        {
            return;
        }

        if (!IsTargetCollider(other))
        {
            return;
        }

        if (playerCollidersInRange.Add(other))
        {
            if (playerInventory == null && other.attachedRigidbody != null)
            {
                playerInventory = other.attachedRigidbody.GetComponent<PlayerInventory>();
            }

            if (interactionCanvas != null)
            {
                interactionCanvas.SetActive(true);
            }
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!IsTargetCollider(other))
        {
            return;
        }

        if (rangeTrigger == null || !other.IsTouching(rangeTrigger))
        {
            playerCollidersInRange.Remove(other);

            if (playerCollidersInRange.Count == 0 && interactionCanvas != null)
            {
                interactionCanvas.SetActive(false);
            }

            if (playerCollidersInRange.Count == 0)
            {
                playerInventory = null;
            }
        }
    }

    private bool IsTargetCollider(Collider2D other)
    {
        if (targetPlayerCollider != null)
        {
            return other == targetPlayerCollider;
        }

        return other.attachedRigidbody != null && other.attachedRigidbody.CompareTag(targetTag);
    }

    public void HarvestBerry()
    {
        if (!hasBerries)
        {
            return;
        }

        if (playerInventory == null)
        {
            Debug.LogWarning($"Cannot harvest from {name}: no player inventory is in range.");
            return;
        }

        if (berryItem == null)
        {
            Debug.LogWarning($"Cannot harvest from {name}: {nameof(berryItem)} is not assigned.");
            return;
        }

        int minAmount = Mathf.Max(1, minHarvestAmount);
        int maxAmount = Mathf.Max(minAmount, maxHarvestAmount);
        int amountToHarvest = Random.Range(minAmount, maxAmount + 1);
        bool addedAll = playerInventory.AddItem(berryItem, amountToHarvest);

        if (!addedAll)
        {
            Debug.Log($"Inventory full while harvesting from {name}. Only part of the harvest may have been added.");
        }

        hasBerries = false;
        StartRegrowTimer();
        UpdateBushVisuals();
    }

    private void StartRegrowTimer()
    {
        float minTimeMinutes = Mathf.Max(0f, minRegrowTimeMinutes);
        float maxTimeMinutes = Mathf.Max(minTimeMinutes, maxRegrowTimeMinutes);
        regrowTimer = Random.Range(minTimeMinutes, maxTimeMinutes) * 60f;
        isRegrowing = true;
    }

    private void DropBerriesIfGrown()
    {
        if (!Application.isPlaying || isApplicationQuitting || !hasBerries || berryItem == null)
        {
            return;
        }

        if (!berryItem.canDrop)
        {
            return;
        }

        if (berryItem.worldPrefab == null)
        {
            Debug.LogWarning($"Bush '{name}' could not drop '{berryItem.itemName}' because it has no world prefab assigned.");
            return;
        }

        int minAmount = Mathf.Max(1, minHarvestAmount);
        int maxAmount = Mathf.Max(minAmount, maxHarvestAmount);
        int amountToDrop = Random.Range(minAmount, maxAmount + 1);

        Vector2 randomUnit = Random.insideUnitCircle;
        Vector3 baseDropPosition = transform.position + new Vector3(
            destroyedDropOffset.x,
            destroyedDropOffset.y,
            0f);
        Vector3 spawnPosition = baseDropPosition + new Vector3(
            randomUnit.x * destroyedDropRadiusXY.x,
            randomUnit.y * destroyedDropRadiusXY.y,
            0f);

        GameObject droppedObject = Instantiate(berryItem.worldPrefab, spawnPosition, Quaternion.identity);
        Item worldItem = droppedObject.GetComponent<Item>();
        if (worldItem != null)
        {
            worldItem.data = berryItem;
            worldItem.amount = amountToDrop;
        }
    }

    private void UpdateBushVisuals()
    {
        if (bushSpriteRenderer != null)
        {
            bushSpriteRenderer.sprite = hasBerries ? bushWithBerrySprite : bushWithoutBerrySprite;
        }

        if (harvestButton != null)
        {
            harvestButton.interactable = hasBerries;
        }
    }
}
