using UnityEngine;
using TMPro;
using UnityEngine.UI;
using UnityEngine.Rendering.Universal;

// For the parent to receive trigger events from its children, 
// the parent MUST have a Rigidbody2D component.
[RequireComponent(typeof(Rigidbody2D))]
public class CampfireLogic : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Drag the child object with the range Trigger Collider here.")]
    [SerializeField] private Collider2D rangeTrigger;
    
    [SerializeField] private GameObject timePanel;
    [SerializeField] private GameObject logButtonPanel;
    [SerializeField] private TextMeshProUGUI timeText;
    [SerializeField] private Button addLogButton;
    [SerializeField] private ItemData logItemData;

    [Header("Trigger Settings")]
    [Tooltip("The tag of the player object (or its Rigidbody).")]
    [SerializeField] private string targetTag = "Player";
    [Tooltip("Optional: If assigned, only this specific player collider will trigger the UI. If left empty, any collider with the correct tag will work.")]
    [SerializeField] private Collider2D targetPlayerCollider;

    [Header("Visuals")]
    [Tooltip("The SpriteRenderer of the campfire.")]
    [SerializeField] private SpriteRenderer campfireSR;
    [SerializeField] private Sprite litSprite;
    [SerializeField] private Sprite unlitSprite;

    [Header("Lighting")]
    [SerializeField] private Light2D campfireLight;
    [SerializeField] private float minIntensity = 0.7f;
    [SerializeField] private float maxIntensity = 1.1f;
    [SerializeField] private float flickerSpeed = 5f;
    [SerializeField] private Color lightColor = new Color(1f, 0.6f, 0.2f);

    [Header("Light Levels (Radius)")]
    [Tooltip("Used when logs are between 1 and Medium Threshold - 1")]
    [SerializeField] private float lowInner = 0.2f;
    [SerializeField] private float lowOuter = 2.5f;
    
    [Space]
    [SerializeField] private int mediumThreshold = 4;
    [SerializeField] private float medInner = 0.5f;
    [SerializeField] private float medOuter = 4.5f;

    [Space]
    [SerializeField] private int highThreshold = 8;
    [SerializeField] private float highInner = 1.0f;
    [SerializeField] private float highOuter = 7.0f;

    [Header("Settings")]
    [SerializeField] private int startingLogs = 3;
    [SerializeField] private int maxLogs = 10;
    [SerializeField] private float timePerLog = 24f;
    [SerializeField] private float radiusLerpSpeed = 3f;
    [SerializeField] private float intensityFadeSpeed = 2f;
    [SerializeField] private float fadeOutThreshold = 10f;

    private int currentLogs = 0;
    private float burnTime = 0f;
    private PlayerInventory playerInventory;
    
    private float targetInner;
    private float targetOuter;
    private float intensityMultiplier = 0f;

    // Use a HashSet to track unique player colliders currently in the range trigger.
    // This is much more robust than a simple counter.
    private System.Collections.Generic.HashSet<Collider2D> playerCollidersInRange = new System.Collections.Generic.HashSet<Collider2D>();

    public static System.Collections.Generic.List<CampfireLogic> AllCampfires = new System.Collections.Generic.List<CampfireLogic>();

    [Header("Runtime State")]
    public bool isPaused = false;

    private void OnEnable()
    {
        if (!AllCampfires.Contains(this))
            AllCampfires.Add(this);
    }

    private void OnDisable()
    {
        AllCampfires.Remove(this);
        UnbindPlayerInventory();
        UnbindAddLogButton();
        playerCollidersInRange.Clear();
        SetInteractionPanelsActive(false);
    }

    public void ConsumeTime(float seconds)
    {
        if (burnTime > 0)
        {
            burnTime -= seconds;
            if (burnTime < 0) burnTime = 0;
            
            // Trigger visuals/UI update if needed, but Update() will handle it next frame
        }
    }

    private void Awake()
    {
        ResolveUIReferences();
        ConfigureAddLogButton();
        InitializeLight();
        ConfigureLight();
    }

    private void OnValidate()
    {
        if (campfireLight == null)
            campfireLight = GetComponentInChildren<Light2D>();

        if (campfireLight != null)
            ConfigureLight();
    }

    private void InitializeLight()
    {
        // Automatically find or create the light if not assigned
        if (campfireLight == null)
        {
            campfireLight = GetComponentInChildren<Light2D>();
            if (campfireLight == null)
            {
                GameObject lightObj = new GameObject("Campfire Light");
                lightObj.transform.SetParent(transform);
                lightObj.transform.localPosition = Vector3.zero;
                
                // Set the layer to "Item"
                int itemLayer = LayerMask.NameToLayer("Item");
                if (itemLayer != -1)
                {
                    lightObj.layer = itemLayer;
                }
                
                campfireLight = lightObj.AddComponent<Light2D>();
            }
        }
    }

    private void ConfigureLight()
    {
        campfireLight.lightType = Light2D.LightType.Point;
        campfireLight.color = lightColor;
        ApplyRadiusByLevel();

        if (!Application.isPlaying)
        {
            campfireLight.intensity = maxIntensity;
            campfireLight.enabled = true;
        }
    }

    private void ApplyRadiusByLevel()
    {
        if (burnTime <= 0)
        {
            targetInner = 0;
            targetOuter = 0;
        }
        else if (currentLogs >= highThreshold)
        {
            targetInner = highInner;
            targetOuter = highOuter;
        }
        else if (currentLogs >= mediumThreshold)
        {
            targetInner = medInner;
            targetOuter = medOuter;
        }
        else
        {
            targetInner = lowInner;
            targetOuter = lowOuter;
        }

        // Snap immediately if in editor
        if (!Application.isPlaying && campfireLight != null)
        {
            campfireLight.pointLightInnerRadius = targetInner;
            campfireLight.pointLightOuterRadius = targetOuter;
        }
    }

    private void HandleLightTransitions()
    {
        if (campfireLight == null) return;

        // Calculate target multiplier
        // If burnTime is 0, target is 0.
        // If burnTime is between 0 and fadeOutThreshold, it scales linearly (e.g. 5s left = 0.5 intensity).
        // If burnTime is above fadeOutThreshold, it stays at 1.0.
        float targetMult = Mathf.Clamp01(burnTime / fadeOutThreshold);
        
        // Use MoveTowards for smooth transition (primarily for ignition/fade-in)
        intensityMultiplier = Mathf.MoveTowards(intensityMultiplier, targetMult, Time.deltaTime * intensityFadeSpeed);

        if (intensityMultiplier > 0)
        {
            if (!campfireLight.enabled) campfireLight.enabled = true;
            
            // Radii
            ApplyRadiusByLevel();
            SmoothRadiusTransition();
            
            // Flicker + Fade
            FlickerLight(); 
        }
        else
        {
            if (campfireLight.enabled) campfireLight.enabled = false;
        }
    }

    private void SmoothRadiusTransition()
    {
        if (campfireLight == null) return;

        campfireLight.pointLightInnerRadius = Mathf.MoveTowards(campfireLight.pointLightInnerRadius, targetInner, Time.deltaTime * radiusLerpSpeed);
        campfireLight.pointLightOuterRadius = Mathf.MoveTowards(campfireLight.pointLightOuterRadius, targetOuter, Time.deltaTime * radiusLerpSpeed);
    }

    private void Start()
    {
        // Check for EventSystem as it is required for UI interactions
        if (UnityEngine.EventSystems.EventSystem.current == null)
        {
            Debug.LogError($"[Campfire] '{name}' No EventSystem found in scene! UI buttons will not work. Please add an EventSystem to your scene.");
        }

        // Setup Rigidbody2D to ensure it's static and doesn't interfere with physics
        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Static;
        rb.simulated = true;

        SetInteractionPanelsActive(false);

        ConfigureAddLogButton();

        // Initialize with starting logs
        currentLogs = startingLogs;
        burnTime = currentLogs * timePerLog;

        UpdateVisuals();
        UpdateUI();
    }

    private void Update()
    {
        if (burnTime > 0 && !isPaused)
        {
            burnTime -= Time.deltaTime;
            if (burnTime < 0) burnTime = 0;
        }

        // Always update UI if the canvas is active to show ticking seconds
        if (AreInteractionPanelsVisible())
        {
            UpdateUI();
        }

        // Recalculate logs and update visuals if there's a change
        int nextLogs = Mathf.CeilToInt(burnTime / timePerLog);
        if (nextLogs != currentLogs)
        {
            currentLogs = nextLogs;
            UpdateVisuals();
            
            // If the UI wasn't updated above, update it now to reflect log count change
            if (!AreInteractionPanelsVisible())
            {
                UpdateUI();
            }
        }

        HandleLightTransitions();
    }

    private void FlickerLight()
    {
        if (campfireLight != null)
        {
            // Use PerlinNoise for a more natural flicker
            float noise = Mathf.PerlinNoise(Time.time * flickerSpeed, 0);
            float baseIntensity = Mathf.Lerp(minIntensity, maxIntensity, noise);
            campfireLight.intensity = baseIntensity * intensityMultiplier;
        }
    }

    public void AddLog()
    {
        Debug.Log(
            $"[Campfire] AddLog invoked on '{name}'. burnTime={burnTime}, currentLogs={currentLogs}, " +
            $"playerInventory={(playerInventory != null ? playerInventory.name : "null")}, " +
            $"logItem={(logItemData != null ? logItemData.itemName : "null")}");

        // Check if we have room for at least some fuel. 
        // We cap it at maxLogs * timePerLog.
        if (burnTime >= maxLogs * timePerLog)
        {
            Debug.Log($"[Campfire] '{name}' is already full. No log added.");
            return;
        }

        if (playerInventory == null)
        {
            BindPlayerInventory(ResolvePreferredPlayerInventory());
            Debug.Log(
                $"[Campfire] '{name}' resolved player inventory to " +
                $"{(playerInventory != null ? playerInventory.name : "null")}.");
        }

        if (playerInventory != null && logItemData != null)
        {
            bool hasLog = playerInventory.HasItem(logItemData, 1);
            Debug.Log(
                $"[Campfire] '{name}' checking for '{logItemData.itemName}' in " +
                $"'{playerInventory.name}': hasLog={hasLog}.");

            if (hasLog)
            {
                playerInventory.RemoveItem(logItemData, 1);
                
                // Add time and cap it
                burnTime = Mathf.Min(burnTime + timePerLog, maxLogs * timePerLog);
                currentLogs = Mathf.CeilToInt(burnTime / timePerLog);
                
                UpdateVisuals();
                UpdateUI();
                Debug.Log($"[Campfire] '{name}' added one log successfully. burnTime={burnTime}, currentLogs={currentLogs}.");
            }
            else
            {
                Debug.Log($"[Campfire] '{name}' could not add a log because the player does not have '{logItemData.itemName}'.");
            }
        }
        else if (logItemData == null)
        {
            // If no data is assigned, just add it for testing (optional)
            burnTime = Mathf.Min(burnTime + timePerLog, maxLogs * timePerLog);
            currentLogs = Mathf.CeilToInt(burnTime / timePerLog);
            
            UpdateVisuals();
            UpdateUI();
            Debug.Log($"[Campfire] '{name}' added a test log because no ItemData is assigned.");
        }
        else
        {
            Debug.Log($"[Campfire] '{name}' could not add a log because playerInventory is null.");
        }
    }

    private void UpdateVisuals()
    {
        if (campfireSR != null)
        {
            campfireSR.sprite = (burnTime > 0) ? litSprite : unlitSprite;
        }

        if (burnTime > 0)
        {
            ApplyRadiusByLevel();
        }
    }

    private void UpdateUI()
    {
        // Try to recover references if they go missing
        if (addLogButton == null || timeText == null)
        {
            ResolveUIReferences();
        }

        if (playerInventory == null && playerCollidersInRange.Count > 0)
        {
            BindPlayerInventory(ResolvePreferredPlayerInventory());
        }

        if (timeText != null)
        {
            int minutes = Mathf.FloorToInt(burnTime / 60);
            int seconds = Mathf.FloorToInt(burnTime % 60);
            string logDisplay = currentLogs >= maxLogs ? "Max" : string.Format("{0}/{1}", currentLogs, maxLogs);
            timeText.text = string.Format("{0}:{1:00}\n{2}", minutes, seconds, logDisplay);
        }

        if (addLogButton != null)
        {
            bool hasSpace = burnTime < (maxLogs * timePerLog);
            bool isInRange = playerCollidersInRange.Count > 0;
            addLogButton.interactable = hasSpace && isInRange && HasLogAvailableForCampfire();
        }

        UpdateInteractionPanelVisibility();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // 1. If a range trigger is specified on THIS object, ensure this collision involves it
        if (rangeTrigger != null && !other.IsTouching(rangeTrigger)) return;

        if (IsTargetPlayerCollider(other))
        {
            if (playerCollidersInRange.Add(other)) // Only proceed if this collider wasn't already tracked
            {
                if (playerInventory == null)
                {
                    BindPlayerInventory(ResolvePlayerInventory(other));
                }

                // Force a UI refresh and reference check when opening
                UpdateUI();
                EnsureAddLogButtonReceivesClicks();
            }
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (IsTargetPlayerCollider(other))
        {
            // 2. Only decrement if the collider is actually leaving the SPECIFIC range trigger
            // (Unity fires Exit when leaving ANY trigger on this object)
            if (rangeTrigger == null || !other.IsTouching(rangeTrigger))
            {
                playerCollidersInRange.Remove(other);

                // Only hide UI if ALL colliders of the player have left the range
                if (playerCollidersInRange.Count == 0)
                {
                    SetInteractionPanelsActive(false);
                    UnbindPlayerInventory();
                }
            }
        }
    }

    private void HandleInventoryChanged()
    {
        UpdateUI();
    }

    private void ResolveUIReferences()
    {
        Canvas canvas = ResolveInteractionCanvas();
        if (canvas != null)
        {
            // Ensure the Canvas is properly configured for raycasting
            if (canvas.renderMode == RenderMode.WorldSpace && canvas.worldCamera == null)
            {
                canvas.worldCamera = Camera.main;
            }

            GraphicRaycaster raycaster = canvas.GetComponent<GraphicRaycaster>();
            if (raycaster == null)
            {
                raycaster = canvas.gameObject.AddComponent<GraphicRaycaster>();
            }
            
            // CRITICAL: Ensure physical colliders on the campfire don't block UI clicks
            raycaster.blockingObjects = GraphicRaycaster.BlockingObjects.None;
            raycaster.ignoreReversedGraphics = false;
        }

        if (addLogButton == null && canvas != null)
        {
            addLogButton = canvas.GetComponentInChildren<Button>(true);
        }

        if (timeText == null && canvas != null)
        {
            TextMeshProUGUI[] textComponents = canvas.GetComponentsInChildren<TextMeshProUGUI>(true);
            for (int i = 0; i < textComponents.Length; i++)
            {
                TextMeshProUGUI textComponent = textComponents[i];
                if (textComponent == null) continue;

                // Skip if it's child of the button (likely the button's own text)
                if (addLogButton != null && textComponent.transform.IsChildOf(addLogButton.transform))
                {
                    continue;
                }

                timeText = textComponent;
                break;
            }
        }

        if (timePanel == null && timeText != null)
        {
            timePanel = ResolvePanelRoot(timeText.transform);
        }

        if (logButtonPanel == null && addLogButton != null)
        {
            logButtonPanel = ResolvePanelRoot(addLogButton.transform);
        }
    }

    private void ConfigureAddLogButton()
    {
        ResolveUIReferences();

        if (addLogButton == null)
        {
            Debug.LogWarning($"[Campfire] '{name}' has no Add Log button assigned or found in children.");
            return;
        }

        EnsureAddLogButtonReceivesClicks();
        
        // Use a more robust listener setup
        addLogButton.onClick.RemoveAllListeners();
        addLogButton.onClick.AddListener(() => AddLog());
    }

    private void UnbindAddLogButton()
    {
        if (addLogButton != null)
        {
            addLogButton.onClick.RemoveAllListeners();
        }
    }

    private void EnsureAddLogButtonReceivesClicks()
    {
        if (addLogButton == null) return;

        // Ensure the button and its GameObject are active and enabled
        addLogButton.gameObject.SetActive(true);
        addLogButton.enabled = true;

        // Ensure the button has an Image to catch clicks
        Image buttonImage = addLogButton.GetComponent<Image>();
        if (buttonImage == null)
        {
            buttonImage = addLogButton.gameObject.AddComponent<Image>();
            buttonImage.color = new Color(1, 1, 1, 0.01f); // Almost invisible but exists
        }
        buttonImage.raycastTarget = true;
        
        if (addLogButton.targetGraphic == null)
        {
            addLogButton.targetGraphic = buttonImage;
        }

        // Ensure all children graphics are ALSO raycast targets
        Graphic[] childGraphics = addLogButton.GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < childGraphics.Length; i++)
        {
            if (childGraphics[i] != null)
            {
                childGraphics[i].raycastTarget = true;
            }
        }

        // Ensure no CanvasGroup is blocking interaction
        CanvasGroup[] groups = addLogButton.GetComponentsInParent<CanvasGroup>(true);
        foreach (var group in groups)
        {
            group.interactable = true;
            group.blocksRaycasts = true;
        }
    }

    private void SetInteractionPanelsActive(bool isActive)
    {
        if (timePanel != null)
        {
            timePanel.SetActive(isActive);
        }

        if (logButtonPanel != null && logButtonPanel != timePanel)
        {
            logButtonPanel.SetActive(isActive);
        }
    }

    private void UpdateInteractionPanelVisibility()
    {
        bool isInRange = playerCollidersInRange.Count > 0;

        if (timePanel != null)
        {
            timePanel.SetActive(isInRange);
        }

        if (logButtonPanel != null && logButtonPanel != timePanel)
        {
            logButtonPanel.SetActive(isInRange && HasLogAvailableForCampfire());
        }
    }

    private bool HasLogAvailableForCampfire()
    {
        if (logItemData == null)
        {
            return true;
        }

        return playerInventory != null && playerInventory.HasItem(logItemData, 1);
    }

    private bool AreInteractionPanelsVisible()
    {
        bool timeVisible = timePanel != null && timePanel.activeSelf;
        bool buttonVisible = logButtonPanel != null && logButtonPanel.activeSelf;
        return timeVisible || buttonVisible;
    }

    private Canvas ResolveInteractionCanvas()
    {
        Canvas canvas = GetCanvasFromObject(timePanel);
        if (canvas != null)
        {
            return canvas;
        }

        canvas = GetCanvasFromObject(logButtonPanel);
        if (canvas != null)
        {
            return canvas;
        }

        if (timeText != null)
        {
            canvas = timeText.GetComponentInParent<Canvas>(true);
            if (canvas != null)
            {
                return canvas;
            }
        }

        if (addLogButton != null)
        {
            canvas = addLogButton.GetComponentInParent<Canvas>(true);
            if (canvas != null)
            {
                return canvas;
            }
        }

        return GetComponentInChildren<Canvas>(true);
    }

    private Canvas GetCanvasFromObject(GameObject target)
    {
        return target != null ? target.GetComponentInParent<Canvas>(true) : null;
    }

    private GameObject ResolvePanelRoot(Transform target)
    {
        if (target == null)
        {
            return null;
        }

        Canvas canvas = target.GetComponentInParent<Canvas>(true);
        if (canvas == null)
        {
            return target.gameObject;
        }

        Transform current = target;
        Transform panelRoot = target;
        while (current != null && current != canvas.transform)
        {
            panelRoot = current;
            current = current.parent;
        }

        return panelRoot != null ? panelRoot.gameObject : target.gameObject;
    }

    private void BindPlayerInventory(PlayerInventory inventory)
    {
        if (playerInventory == inventory)
        {
            return;
        }

        if (playerInventory != null)
        {
            playerInventory.OnInventoryChanged -= HandleInventoryChanged;
        }

        playerInventory = inventory;

        if (playerInventory != null)
        {
            playerInventory.OnInventoryChanged -= HandleInventoryChanged;
            playerInventory.OnInventoryChanged += HandleInventoryChanged;
        }
    }

    private void UnbindPlayerInventory()
    {
        if (playerInventory != null)
        {
            playerInventory.OnInventoryChanged -= HandleInventoryChanged;
            playerInventory = null;
        }
    }

    private PlayerInventory ResolvePlayerInventory(Collider2D other)
    {
        if (other == null)
        {
            return ResolvePreferredPlayerInventory();
        }

        PlayerInventory resolvedInventory = GetValidPlayerInventory(other.GetComponent<PlayerInventory>());
        if (resolvedInventory != null)
        {
            return resolvedInventory;
        }

        resolvedInventory = GetValidPlayerInventory(other.GetComponentInParent<PlayerInventory>());
        if (resolvedInventory != null)
        {
            return resolvedInventory;
        }

        if (other.attachedRigidbody != null)
        {
            resolvedInventory = GetValidPlayerInventory(other.attachedRigidbody.GetComponent<PlayerInventory>());
            if (resolvedInventory != null)
            {
                return resolvedInventory;
            }

            resolvedInventory = GetValidPlayerInventory(other.attachedRigidbody.GetComponentInParent<PlayerInventory>());
            if (resolvedInventory != null)
            {
                return resolvedInventory;
            }

            PlayerInventory[] inventories = other.attachedRigidbody.GetComponentsInChildren<PlayerInventory>(true);
            for (int i = 0; i < inventories.Length; i++)
            {
                resolvedInventory = GetValidPlayerInventory(inventories[i]);
                if (resolvedInventory != null)
                {
                    return resolvedInventory;
                }
            }
        }

        return ResolvePreferredPlayerInventory();
    }

    private PlayerInventory ResolvePreferredPlayerInventory()
    {
        PlayerInventory[] inventories =
            FindObjectsByType<PlayerInventory>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        PlayerInventory fallback = null;

        for (int i = 0; i < inventories.Length; i++)
        {
            PlayerInventory candidate = inventories[i];
            if (candidate == null || candidate is ChestInventory)
            {
                continue;
            }

            if (fallback == null)
            {
                fallback = candidate;
            }

            if (HasTagInHierarchy(candidate.transform, targetTag))
            {
                return candidate;
            }
        }

        return fallback;
    }

    private PlayerInventory GetValidPlayerInventory(PlayerInventory candidate)
    {
        if (candidate == null || candidate is ChestInventory)
        {
            return null;
        }

        if (HasTagInHierarchy(candidate.transform, targetTag))
        {
            return candidate;
        }

        return candidate;
    }

    private bool IsTargetPlayerCollider(Collider2D candidate)
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

    private bool HasTagInHierarchy(Transform target, string tagToMatch)
    {
        if (target == null || string.IsNullOrWhiteSpace(tagToMatch))
        {
            return false;
        }

        Transform current = target;
        while (current != null)
        {
            if (current.CompareTag(tagToMatch))
            {
                return true;
            }

            current = current.parent;
        }

        return false;
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
