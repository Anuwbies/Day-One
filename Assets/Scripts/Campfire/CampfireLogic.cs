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
    
    [SerializeField] private GameObject interactionCanvas;
    [SerializeField] private TextMeshProUGUI timeText;
    [SerializeField] private Button addLogButton;
    [SerializeField] private ItemData logItemData;

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

    private void Awake()
    {
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
        // Setup Rigidbody2D to ensure it's static and doesn't interfere with physics
        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Static;
        rb.simulated = true;

        if (interactionCanvas != null)
        {
            interactionCanvas.SetActive(false);
        }

        if (addLogButton != null)
        {
            addLogButton.onClick.AddListener(AddLog);
        }

        UpdateVisuals();
        UpdateUI();
    }

    private void Update()
    {
        if (burnTime > 0)
        {
            burnTime -= Time.deltaTime;
            
            int previousLogs = currentLogs;
            currentLogs = Mathf.CeilToInt(burnTime / timePerLog);

            if (burnTime <= 0)
            {
                burnTime = 0;
                currentLogs = 0;
                UpdateVisuals();
            }
            else if (previousLogs != currentLogs)
            {
                UpdateVisuals();
            }

            UpdateUI();
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
        if (currentLogs >= maxLogs) return;

        if (playerInventory != null && logItemData != null)
        {
            if (playerInventory.HasItem(logItemData, 1))
            {
                playerInventory.RemoveItem(logItemData, 1);
                currentLogs++;
                burnTime += timePerLog;
                
                UpdateVisuals();
                UpdateUI();
            }
        }
        else if (logItemData == null)
        {
            // If no data is assigned, just add it for testing (optional)
            currentLogs++;
            burnTime += timePerLog;
            
            UpdateVisuals();
            UpdateUI();
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
        if (timeText != null)
        {
            int minutes = Mathf.FloorToInt(burnTime / 60);
            int seconds = Mathf.FloorToInt(burnTime % 60);
            timeText.text = string.Format("{0}:{1:00}\n{2}/{3}", minutes, seconds, currentLogs, maxLogs);
        }

        if (addLogButton != null)
        {
            // Button is interactable if we have space AND the player has a log
            bool hasSpace = currentLogs < maxLogs;
            bool hasLog = playerInventory != null && logItemData != null && playerInventory.HasItem(logItemData, 1);
            
            // If logItemData is not set, we allow it for easier setup/testing
            if (logItemData == null) hasLog = true;

            addLogButton.interactable = hasSpace && hasLog;
        }
    }

    private int playerCollidersInRange = 0;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.attachedRigidbody != null && other.attachedRigidbody.CompareTag("Player"))
        {
            playerCollidersInRange++;
            
            // Only initialize and show UI if this is the first collider entering
            if (playerInventory == null)
            {
                playerInventory = other.attachedRigidbody.GetComponent<PlayerInventory>();
                if (interactionCanvas != null)
                {
                    interactionCanvas.SetActive(true);
                }
                UpdateUI();
            }
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.attachedRigidbody != null && other.attachedRigidbody.CompareTag("Player"))
        {
            playerCollidersInRange--;

            // Only hide UI if ALL colliders of the player have left the range
            if (playerCollidersInRange <= 0)
            {
                playerCollidersInRange = 0; // Guard against potential negative values
                if (interactionCanvas != null)
                {
                    interactionCanvas.SetActive(false);
                }
                playerInventory = null;
            }
        }
    }
}
