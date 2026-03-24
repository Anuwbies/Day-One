using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

// For the parent to receive trigger events from its children,
// the parent MUST have a Rigidbody2D component.
[DefaultExecutionOrder(1000)]
[RequireComponent(typeof(Rigidbody2D))]
public class BushBerryLogic : MonoBehaviour
{
    private struct BerryLocalBounds
    {
        public Vector2 Min;
        public Vector2 Max;

        public Vector2 Size => Max - Min;
    }

    private static bool isApplicationQuitting;

    [Header("References")]
    [Tooltip("Drag the child object with the range Trigger Collider here.")]
    [SerializeField] private Collider2D rangeTrigger;
    [SerializeField] private GameObject interactionCanvas;
    [SerializeField] private Button harvestButton;
    [SerializeField] private SpriteRenderer bushSpriteRenderer;
    [SerializeField] private SpriteRenderer berrySpriteRenderer;

    [Header("Harvest Settings")]
    [SerializeField] private ItemData berryItem;
    [SerializeField] private int minHarvestAmount = 1;
    [SerializeField] private int maxHarvestAmount = 3;
    [Header("Berry Visuals")]
    [SerializeField] private Sprite berrySprite;
    [SerializeField] private int berrySortOrderOffset = 1;
    [FormerlySerializedAs("berrySpriteOffset")]
    [SerializeField] private Vector2 berrySpawnAreaOffset = new Vector2(0.02f, 0.03f);
    [FormerlySerializedAs("berrySpriteSize")]
    [SerializeField] private Vector2 berrySpawnAreaSize = new Vector2(0.18f, 0.12f);
    [Header("Regrow Settings")]
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
    private readonly List<SpriteRenderer> berryRenderers = new List<SpriteRenderer>();
    private readonly List<Vector3> berryLocalOffsets = new List<Vector3>();
    private PlayerInventory playerInventory;
    private bool hasBerries = true;
    private float regrowTimer;
    private bool isRegrowing;
    private int currentHarvestAmount;
    private Quaternion berryTemplateLocalRotation = Quaternion.identity;
    private Vector3 berryTemplateLocalScale = Vector3.one;

    private void Start()
    {
        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Static;
        rb.simulated = true;

        if (bushSpriteRenderer == null)
        {
            bushSpriteRenderer = GetComponent<SpriteRenderer>();
        }

        if (interactionCanvas != null)
        {
            interactionCanvas.SetActive(false);
        }

        if (harvestButton != null)
        {
            harvestButton.onClick.AddListener(HarvestBerry);
        }

        CacheBerryTemplateState();
        SetCurrentHarvestAmount(hasBerries ? GetRandomHarvestAmount() : 0);
        UpdateBushVisuals();
        SyncBerryRendererSorting();
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
            SetCurrentHarvestAmount(GetRandomHarvestAmount());
            UpdateBushVisuals();
        }
    }

    private void LateUpdate()
    {
        SyncBerryRendererSorting();
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

            UpdateInteractionCanvasVisibility();
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

            if (playerCollidersInRange.Count == 0)
            {
                playerInventory = null;
            }

            UpdateInteractionCanvasVisibility();
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
        int amountToHarvest = Mathf.Clamp(currentHarvestAmount, minAmount, maxAmount);
        bool addedAll = playerInventory.AddItem(berryItem, amountToHarvest);

        if (!addedAll)
        {
            Debug.Log($"Inventory full while harvesting from {name}. Only part of the harvest may have been added.");
        }

        hasBerries = false;
        SetCurrentHarvestAmount(0);
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
        int amountToDrop = Mathf.Clamp(currentHarvestAmount > 0 ? currentHarvestAmount : GetRandomHarvestAmount(), minAmount, maxAmount);

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
        int visibleBerryCount = hasBerries ? Mathf.Clamp(currentHarvestAmount, 0, GetMaxBerryVisualCount()) : 0;
        EnsureBerryRenderers(visibleBerryCount);
        UpdateBerryRendererTransforms(visibleBerryCount);

        for (int i = 0; i < berryRenderers.Count; i++)
        {
            SpriteRenderer renderer = berryRenderers[i];
            if (renderer == null)
            {
                continue;
            }

            bool isVisible = berrySprite != null && i < visibleBerryCount;
            SetBerryHierarchyVisible(renderer, isVisible);
        }

        if (harvestButton != null)
        {
            harvestButton.interactable = hasBerries && visibleBerryCount > 0;
        }

        UpdateInteractionCanvasVisibility();
    }

    private void UpdateInteractionCanvasVisibility()
    {
        if (interactionCanvas == null)
        {
            return;
        }

        bool hasHarvestableBerries = hasBerries && currentHarvestAmount > 0;
        interactionCanvas.SetActive(playerCollidersInRange.Count > 0 && hasHarvestableBerries);
    }

    private void SyncBerryRendererSorting()
    {
        if (bushSpriteRenderer == null)
        {
            return;
        }

        int berrySortingOrder = bushSpriteRenderer.sortingOrder + berrySortOrderOffset;

        for (int i = 0; i < berryRenderers.Count; i++)
        {
            SpriteRenderer renderer = berryRenderers[i];
            if (renderer == null)
            {
                continue;
            }

            SyncBerryHierarchySorting(renderer, berrySortingOrder);
        }
    }

    private int GetRandomHarvestAmount()
    {
        int minAmount = Mathf.Max(1, minHarvestAmount);
        int maxAmount = Mathf.Max(minAmount, maxHarvestAmount);
        return Random.Range(minAmount, maxAmount + 1);
    }

    private int GetMaxBerryVisualCount()
    {
        return Mathf.Max(1, maxHarvestAmount);
    }

    private void CacheBerryTemplateState()
    {
        if (berrySpriteRenderer == null)
        {
            return;
        }

        berryTemplateLocalRotation = berrySpriteRenderer.transform.localRotation;
        berryTemplateLocalScale = berrySpriteRenderer.transform.localScale;
    }

    private void EnsureBerryRenderers(int visibleBerryCount)
    {
        if (berrySpriteRenderer == null)
        {
            return;
        }

        for (int i = berryRenderers.Count - 1; i >= 0; i--)
        {
            if (berryRenderers[i] == null)
            {
                berryRenderers.RemoveAt(i);
            }
        }

        if (berryRenderers.Count == 0 || berryRenderers[0] == null || berryRenderers[0] != berrySpriteRenderer)
        {
            berryRenderers.Clear();
            berryRenderers.Add(berrySpriteRenderer);
        }

        CacheBerryTemplateState();

        int desiredRendererCount = Mathf.Max(1, visibleBerryCount);
        while (berryRenderers.Count < desiredRendererCount)
        {
            SpriteRenderer newRenderer = Instantiate(berrySpriteRenderer, berrySpriteRenderer.transform.parent);
            berryRenderers.Add(newRenderer);
        }

        for (int i = berryRenderers.Count - 1; i >= desiredRendererCount; i--)
        {
            SpriteRenderer extraRenderer = berryRenderers[i];
            berryRenderers.RemoveAt(i);
            if (extraRenderer != null)
            {
                Destroy(extraRenderer.gameObject);
            }
        }

        UpdateBerryRendererNames();
    }

    private void UpdateBerryRendererNames()
    {
        for (int i = 0; i < berryRenderers.Count; i++)
        {
            SpriteRenderer renderer = berryRenderers[i];
            if (renderer == null)
            {
                continue;
            }

            renderer.gameObject.name = $"Berry {i + 1}";
        }
    }

    private void SetBerryHierarchyVisible(SpriteRenderer rootRenderer, bool isVisible)
    {
        SpriteRenderer[] hierarchyRenderers = rootRenderer.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < hierarchyRenderers.Length; i++)
        {
            SpriteRenderer hierarchyRenderer = hierarchyRenderers[i];
            if (hierarchyRenderer == null)
            {
                continue;
            }

            if (hierarchyRenderer == rootRenderer)
            {
                hierarchyRenderer.sprite = berrySprite;
                hierarchyRenderer.enabled = isVisible;
                continue;
            }

            hierarchyRenderer.enabled = isVisible;
        }
    }

    private void SyncBerryHierarchySorting(SpriteRenderer rootRenderer, int berrySortingOrder)
    {
        SpriteRenderer[] hierarchyRenderers = rootRenderer.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < hierarchyRenderers.Length; i++)
        {
            SpriteRenderer hierarchyRenderer = hierarchyRenderers[i];
            if (hierarchyRenderer == null)
            {
                continue;
            }

            hierarchyRenderer.sortingLayerID = bushSpriteRenderer.sortingLayerID;
            hierarchyRenderer.sortingOrder = berrySortingOrder;
            hierarchyRenderer.flipX = bushSpriteRenderer.flipX;
            hierarchyRenderer.flipY = bushSpriteRenderer.flipY;
        }
    }

    private void UpdateBerryRendererTransforms(int visibleBerryCount)
    {
        if (berryRenderers.Count == 0)
        {
            return;
        }

        if (visibleBerryCount > berryLocalOffsets.Count)
        {
            RebuildBerryLocalOffsets(visibleBerryCount);
        }

        Vector3 anchorLocalPosition = new Vector3(berrySpawnAreaOffset.x, berrySpawnAreaOffset.y, 0f);

        for (int i = 0; i < berryRenderers.Count; i++)
        {
            SpriteRenderer renderer = berryRenderers[i];
            if (renderer == null)
            {
                continue;
            }

            renderer.transform.localRotation = berryTemplateLocalRotation;
            renderer.transform.localScale = berryTemplateLocalScale;
            renderer.transform.localPosition = anchorLocalPosition + GetBerryLocalOffset(i);
        }
    }

    private void SetCurrentHarvestAmount(int amount)
    {
        currentHarvestAmount = Mathf.Clamp(amount, 0, GetMaxBerryVisualCount());
        RebuildBerryLocalOffsets(currentHarvestAmount);
    }

    private void RebuildBerryLocalOffsets(int berryCount)
    {
        berryLocalOffsets.Clear();

        if (berryCount <= 0)
        {
            return;
        }

        PopulateBerryLocalOffsets(berryLocalOffsets, berryCount, null);
    }

    private Vector3 GetBerryLocalOffset(int index)
    {
        if (index < 0 || index >= berryLocalOffsets.Count)
        {
            return Vector3.zero;
        }

        return berryLocalOffsets[index];
    }

    private void PopulateBerryLocalOffsets(List<Vector3> targetOffsets, int berryCount, System.Random previewRandom)
    {
        BerryLocalBounds berryBounds = GetBerryLocalBounds();
        Vector2 berryFootprint = berryBounds.Size;
        Vector2 capsuleSize = GetBerrySpawnCapsuleSize();
        int maxAttemptsPerBerry = 64;

        for (int i = 0; i < berryCount; i++)
        {
            Vector3 candidate = Vector3.zero;
            Vector3 bestCandidate = Vector3.zero;
            float bestClearanceScore = float.MinValue;
            bool foundAnyValidCandidate = false;
            bool foundValidPosition = false;

            for (int attempt = 0; attempt < maxAttemptsPerBerry; attempt++)
            {
                candidate = GetRandomBerryLocalOffset(previewRandom);
                if (!IsBerryBoundsInsideCapsule(candidate, berryBounds, capsuleSize))
                {
                    continue;
                }

                foundAnyValidCandidate = true;
                float clearanceScore = GetBerryClearanceScore(candidate, berryFootprint, targetOffsets);
                if (clearanceScore > bestClearanceScore)
                {
                    bestClearanceScore = clearanceScore;
                    bestCandidate = candidate;
                }

                if (!DoesBerryOverlapExisting(candidate, berryFootprint, targetOffsets))
                {
                    foundValidPosition = true;
                    break;
                }
            }

            if (!foundValidPosition)
            {
                candidate = foundAnyValidCandidate
                    ? bestCandidate
                    : GetFallbackBerryLocalOffset(berryBounds, capsuleSize);
            }

            targetOffsets.Add(candidate);
        }

        ResolveBerryOverlaps(targetOffsets, berryBounds);
    }

    private BerryLocalBounds GetBerryLocalBounds()
    {
        if (berrySpriteRenderer == null)
        {
            return new BerryLocalBounds
            {
                Min = new Vector2(-0.025f, -0.025f),
                Max = new Vector2(0.025f, 0.025f)
            };
        }

        SpriteRenderer[] hierarchyRenderers = berrySpriteRenderer.GetComponentsInChildren<SpriteRenderer>(true);
        bool foundAnyBounds = false;
        Vector2 min = Vector2.zero;
        Vector2 max = Vector2.zero;

        for (int i = 0; i < hierarchyRenderers.Length; i++)
        {
            SpriteRenderer hierarchyRenderer = hierarchyRenderers[i];
            if (hierarchyRenderer == null)
            {
                continue;
            }

            Sprite sourceSprite = hierarchyRenderer == berrySpriteRenderer && berrySprite != null
                ? berrySprite
                : hierarchyRenderer.sprite;
            if (sourceSprite == null)
            {
                continue;
            }

            Vector3 localCenter = berrySpriteRenderer.transform.InverseTransformPoint(hierarchyRenderer.transform.position);
            Vector3 rendererScale = hierarchyRenderer.transform.lossyScale;
            Vector2 spriteSize = sourceSprite.bounds.size;
            Vector2 halfSize = new Vector2(
                spriteSize.x * Mathf.Max(0.001f, Mathf.Abs(rendererScale.x)) * 0.5f,
                spriteSize.y * Mathf.Max(0.001f, Mathf.Abs(rendererScale.y)) * 0.5f);

            Vector2 rendererMin = (Vector2)localCenter - halfSize;
            Vector2 rendererMax = (Vector2)localCenter + halfSize;

            if (!foundAnyBounds)
            {
                min = rendererMin;
                max = rendererMax;
                foundAnyBounds = true;
                continue;
            }

            min = Vector2.Min(min, rendererMin);
            max = Vector2.Max(max, rendererMax);
        }

        if (!foundAnyBounds)
        {
            return new BerryLocalBounds
            {
                Min = new Vector2(-0.025f, -0.025f),
                Max = new Vector2(0.025f, 0.025f)
            };
        }

        return new BerryLocalBounds
        {
            Min = min,
            Max = max
        };
    }

    private Vector3 GetRandomBerryLocalOffset(System.Random previewRandom)
    {
        Vector2 capsuleSize = GetBerrySpawnCapsuleSize();
        if (capsuleSize.x <= 0.0001f && capsuleSize.y <= 0.0001f)
        {
            return Vector3.zero;
        }

        Vector2 halfSize = capsuleSize * 0.5f;
        const int maxAttempts = 64;
        Vector2 point = Vector2.zero;

        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            point = new Vector2(
                SampleBerryRandomRange(-halfSize.x, halfSize.x, previewRandom),
                SampleBerryRandomRange(-halfSize.y, halfSize.y, previewRandom));

            if (IsPointInsideCapsule(point, capsuleSize))
            {
                return new Vector3(point.x, point.y, 0f);
            }
        }

        return new Vector3(point.x, point.y, 0f);
    }

    private bool DoesBerryOverlapExisting(Vector3 candidate, Vector2 berryFootprint, List<Vector3> existingOffsets)
    {
        float requiredXSeparation = berryFootprint.x;
        float requiredYSeparation = berryFootprint.y;

        for (int i = 0; i < existingOffsets.Count; i++)
        {
            Vector3 existingOffset = existingOffsets[i];
            bool overlapsX = Mathf.Abs(candidate.x - existingOffset.x) < requiredXSeparation;
            bool overlapsY = Mathf.Abs(candidate.y - existingOffset.y) < requiredYSeparation;
            if (overlapsX && overlapsY)
            {
                return true;
            }
        }

        return false;
    }

    private float GetBerryClearanceScore(Vector3 candidate, Vector2 berryFootprint, List<Vector3> existingOffsets)
    {
        if (existingOffsets.Count == 0)
        {
            return float.MaxValue;
        }

        float requiredXSeparation = berryFootprint.x;
        float requiredYSeparation = berryFootprint.y;
        float bestScore = float.MaxValue;

        for (int i = 0; i < existingOffsets.Count; i++)
        {
            Vector3 existingOffset = existingOffsets[i];
            float normalizedX = Mathf.Abs(candidate.x - existingOffset.x) / Mathf.Max(0.0001f, requiredXSeparation);
            float normalizedY = Mathf.Abs(candidate.y - existingOffset.y) / Mathf.Max(0.0001f, requiredYSeparation);
            float score = Mathf.Min(normalizedX, normalizedY);
            bestScore = Mathf.Min(bestScore, score);
        }

        return bestScore;
    }

    private Vector2 GetBerrySpawnCapsuleSize()
    {
        return new Vector2(
            Mathf.Max(0f, berrySpawnAreaSize.x),
            Mathf.Max(0f, berrySpawnAreaSize.y));
    }

    private float SampleBerryRandomRange(float min, float max, System.Random previewRandom)
    {
        return previewRandom == null
            ? Random.Range(min, max)
            : Mathf.Lerp(min, max, (float)previewRandom.NextDouble());
    }

    private void ResolveBerryOverlaps(List<Vector3> offsets, BerryLocalBounds berryBounds)
    {
        if (offsets.Count <= 1)
        {
            return;
        }

        Vector2 berryFootprint = berryBounds.Size;
        Vector2 capsuleSize = GetBerrySpawnCapsuleSize();
        const int maxIterations = 32;

        for (int iteration = 0; iteration < maxIterations; iteration++)
        {
            bool movedAny = false;

            for (int i = 0; i < offsets.Count - 1; i++)
            {
                for (int j = i + 1; j < offsets.Count; j++)
                {
                    Vector2 a = offsets[i];
                    Vector2 b = offsets[j];
                    float overlapX = berryFootprint.x - Mathf.Abs(b.x - a.x);
                    float overlapY = berryFootprint.y - Mathf.Abs(b.y - a.y);

                    if (overlapX <= 0f || overlapY <= 0f)
                    {
                        continue;
                    }

                    Vector2 push;
                    if (overlapX < overlapY)
                    {
                        float direction = Mathf.Approximately(a.x, b.x) ? (i <= j ? -1f : 1f) : Mathf.Sign(a.x - b.x);
                        push = new Vector2(direction * ((overlapX * 0.5f) + 0.001f), 0f);
                    }
                    else
                    {
                        float direction = Mathf.Approximately(a.y, b.y) ? (i <= j ? -1f : 1f) : Mathf.Sign(a.y - b.y);
                        push = new Vector2(0f, direction * ((overlapY * 0.5f) + 0.001f));
                    }

                    a = ClampPointToCapsule(a + push, capsuleSize, berryBounds);
                    b = ClampPointToCapsule(b - push, capsuleSize, berryBounds);
                    offsets[i] = new Vector3(a.x, a.y, 0f);
                    offsets[j] = new Vector3(b.x, b.y, 0f);
                    movedAny = true;
                }
            }

            if (!movedAny)
            {
                break;
            }
        }
    }

    private Vector2 ClampPointToCapsule(Vector2 point, Vector2 capsuleSize, BerryLocalBounds berryBounds)
    {
        if (IsBerryBoundsInsideCapsule(point, berryBounds, capsuleSize))
        {
            return point;
        }

        if (!TryGetAnyValidBerryPoint(berryBounds, capsuleSize, out Vector2 insidePoint))
        {
            return Vector2.zero;
        }

        Vector2 outsidePoint = point;

        for (int i = 0; i < 16; i++)
        {
            Vector2 middlePoint = (insidePoint + outsidePoint) * 0.5f;
            if (IsBerryBoundsInsideCapsule(middlePoint, berryBounds, capsuleSize))
            {
                insidePoint = middlePoint;
            }
            else
            {
                outsidePoint = middlePoint;
            }
        }

        return insidePoint;
    }

    private Vector3 GetFallbackBerryLocalOffset(BerryLocalBounds berryBounds, Vector2 capsuleSize)
    {
        if (TryGetAnyValidBerryPoint(berryBounds, capsuleSize, out Vector2 validPoint))
        {
            return new Vector3(validPoint.x, validPoint.y, 0f);
        }

        return Vector3.zero;
    }

    private bool TryGetAnyValidBerryPoint(BerryLocalBounds berryBounds, Vector2 capsuleSize, out Vector2 validPoint)
    {
        validPoint = Vector2.zero;
        if (IsBerryBoundsInsideCapsule(validPoint, berryBounds, capsuleSize))
        {
            return true;
        }

        Vector2 halfSize = capsuleSize * 0.5f;
        const int gridSteps = 8;

        for (int y = 0; y <= gridSteps; y++)
        {
            for (int x = 0; x <= gridSteps; x++)
            {
                Vector2 candidate = new Vector2(
                    Mathf.Lerp(-halfSize.x, halfSize.x, x / (float)gridSteps),
                    Mathf.Lerp(-halfSize.y, halfSize.y, y / (float)gridSteps));

                if (IsBerryBoundsInsideCapsule(candidate, berryBounds, capsuleSize))
                {
                    validPoint = candidate;
                    return true;
                }
            }
        }

        return false;
    }

    private bool IsBerryBoundsInsideCapsule(Vector2 center, BerryLocalBounds berryBounds, Vector2 capsuleSize)
    {
        Vector2[] corners =
        {
            center + new Vector2(berryBounds.Min.x, berryBounds.Min.y),
            center + new Vector2(berryBounds.Min.x, berryBounds.Max.y),
            center + new Vector2(berryBounds.Max.x, berryBounds.Min.y),
            center + new Vector2(berryBounds.Max.x, berryBounds.Max.y)
        };

        for (int i = 0; i < corners.Length; i++)
        {
            if (!IsPointInsideCapsule(corners[i], capsuleSize))
            {
                return false;
            }
        }

        return true;
    }

    private bool IsPointInsideCapsule(Vector2 point, Vector2 capsuleSize)
    {
        float width = Mathf.Max(0.0001f, capsuleSize.x);
        float height = Mathf.Max(0.0001f, capsuleSize.y);

        if (Mathf.Abs(width - height) <= 0.0001f)
        {
            float radius = width * 0.5f;
            return point.sqrMagnitude <= radius * radius;
        }

        if (width > height)
        {
            float radius = height * 0.5f;
            float straightHalfWidth = Mathf.Max(0f, (width * 0.5f) - radius);

            if (Mathf.Abs(point.x) <= straightHalfWidth && Mathf.Abs(point.y) <= radius)
            {
                return true;
            }

            Vector2 leftCenter = new Vector2(-straightHalfWidth, 0f);
            Vector2 rightCenter = new Vector2(straightHalfWidth, 0f);
            return (point - leftCenter).sqrMagnitude <= radius * radius ||
                   (point - rightCenter).sqrMagnitude <= radius * radius;
        }

        float verticalRadius = width * 0.5f;
        float straightHalfHeight = Mathf.Max(0f, (height * 0.5f) - verticalRadius);

        if (Mathf.Abs(point.y) <= straightHalfHeight && Mathf.Abs(point.x) <= verticalRadius)
        {
            return true;
        }

        Vector2 topCenter = new Vector2(0f, straightHalfHeight);
        Vector2 bottomCenter = new Vector2(0f, -straightHalfHeight);
        return (point - topCenter).sqrMagnitude <= verticalRadius * verticalRadius ||
               (point - bottomCenter).sqrMagnitude <= verticalRadius * verticalRadius;
    }

    private List<Vector3> BuildPreviewBerryLocalOffsets(int previewCount)
    {
        if (Application.isPlaying && berryLocalOffsets.Count >= previewCount)
        {
            return berryLocalOffsets;
        }

        List<Vector3> previewOffsets = new List<Vector3>(previewCount);
        int seed = name.GetHashCode();
        seed = (seed * 397) ^ Mathf.RoundToInt(transform.position.x * 100f);
        seed = (seed * 397) ^ Mathf.RoundToInt(transform.position.y * 100f);
        System.Random random = new System.Random(seed);
        PopulateBerryLocalOffsets(previewOffsets, previewCount, random);
        return previewOffsets;
    }

    private void OnDrawGizmosSelected()
    {
        int previewCount = GetBerryGizmoPreviewCount();
        if (previewCount <= 0)
        {
            return;
        }

        Quaternion templateRotation = berrySpriteRenderer != null
            ? berrySpriteRenderer.transform.localRotation
            : Quaternion.identity;

        Sprite previewSprite = berrySprite != null
            ? berrySprite
            : berrySpriteRenderer != null ? berrySpriteRenderer.sprite : null;

        Vector3 previewScale = berrySpriteRenderer != null
            ? berrySpriteRenderer.transform.localScale
            : berryTemplateLocalScale;

        Vector3 previewSize = previewSprite != null
            ? (Vector3)previewSprite.bounds.size
            : new Vector3(0.12f, 0.12f, 0.01f);

        previewSize.x *= Mathf.Max(0.001f, previewScale.x);
        previewSize.y *= Mathf.Max(0.001f, previewScale.y);
        previewSize.z = 0.01f;

        Matrix4x4 previousMatrix = Gizmos.matrix;
        Vector3 anchorLocalPosition = new Vector3(berrySpawnAreaOffset.x, berrySpawnAreaOffset.y, 0f);

        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(anchorLocalPosition, 0.015f);
        Gizmos.DrawLine(Vector3.zero, anchorLocalPosition);
        Gizmos.color = Color.green;
        DrawCapsuleWireGizmo(anchorLocalPosition, berrySpawnAreaSize);

        List<Vector3> previewOffsets = BuildPreviewBerryLocalOffsets(previewCount);
        for (int i = 0; i < previewCount; i++)
        {
            Vector3 localPosition = anchorLocalPosition + previewOffsets[i];
            Gizmos.matrix = transform.localToWorldMatrix * Matrix4x4.TRS(localPosition, templateRotation, Vector3.one);
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireCube(Vector3.zero, previewSize);
        }

        Gizmos.matrix = previousMatrix;
    }

    private int GetBerryGizmoPreviewCount()
    {
        if (Application.isPlaying && hasBerries)
        {
            return Mathf.Clamp(currentHarvestAmount, 1, GetMaxBerryVisualCount());
        }

        return GetMaxBerryVisualCount();
    }

    private void DrawCapsuleWireGizmo(Vector3 center, Vector2 capsuleSize)
    {
        float width = Mathf.Max(0.001f, capsuleSize.x);
        float height = Mathf.Max(0.001f, capsuleSize.y);

        if (Mathf.Abs(width - height) <= 0.0001f)
        {
            DrawCircleWireGizmo(center, width * 0.5f);
            return;
        }

        if (width > height)
        {
            float radius = height * 0.5f;
            float straightHalfWidth = Mathf.Max(0f, (width * 0.5f) - radius);
            Vector3 leftCenter = center + Vector3.left * straightHalfWidth;
            Vector3 rightCenter = center + Vector3.right * straightHalfWidth;

            Gizmos.DrawLine(
                new Vector3(leftCenter.x, center.y + radius, center.z),
                new Vector3(rightCenter.x, center.y + radius, center.z));
            Gizmos.DrawLine(
                new Vector3(leftCenter.x, center.y - radius, center.z),
                new Vector3(rightCenter.x, center.y - radius, center.z));

            DrawArcWireGizmo(leftCenter, radius, 90f, 270f);
            DrawArcWireGizmo(rightCenter, radius, -90f, 90f);
            return;
        }

        float verticalRadius = width * 0.5f;
        float straightHalfHeight = Mathf.Max(0f, (height * 0.5f) - verticalRadius);
        Vector3 topCenter = center + Vector3.up * straightHalfHeight;
        Vector3 bottomCenter = center + Vector3.down * straightHalfHeight;

        Gizmos.DrawLine(
            new Vector3(center.x - verticalRadius, topCenter.y, center.z),
            new Vector3(center.x - verticalRadius, bottomCenter.y, center.z));
        Gizmos.DrawLine(
            new Vector3(center.x + verticalRadius, topCenter.y, center.z),
            new Vector3(center.x + verticalRadius, bottomCenter.y, center.z));

        DrawArcWireGizmo(topCenter, verticalRadius, 0f, 180f);
        DrawArcWireGizmo(bottomCenter, verticalRadius, 180f, 360f);
    }

    private void DrawCircleWireGizmo(Vector3 center, float radius)
    {
        DrawArcWireGizmo(center, radius, 0f, 360f);
    }

    private void DrawArcWireGizmo(Vector3 center, float radius, float startAngleDegrees, float endAngleDegrees)
    {
        const int segmentCount = 20;
        Vector3 previousPoint = center + GetCirclePoint(radius, startAngleDegrees);

        for (int i = 1; i <= segmentCount; i++)
        {
            float t = i / (float)segmentCount;
            float angle = Mathf.Lerp(startAngleDegrees, endAngleDegrees, t);
            Vector3 nextPoint = center + GetCirclePoint(radius, angle);
            Gizmos.DrawLine(previousPoint, nextPoint);
            previousPoint = nextPoint;
        }
    }

    private Vector3 GetCirclePoint(float radius, float angleDegrees)
    {
        float angleRadians = angleDegrees * Mathf.Deg2Rad;
        return new Vector3(
            Mathf.Cos(angleRadians) * radius,
            Mathf.Sin(angleRadians) * radius,
            0f);
    }
}
