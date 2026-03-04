using UnityEngine;
using TMPro;
using UnityEngine.UI;

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

    [Header("Settings")]
    [SerializeField] private int maxLogs = 10;
    [SerializeField] private float timePerLog = 24f;

    private int currentLogs = 0;
    private float burnTime = 0f;
    private PlayerInventory playerInventory;

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
            
            // Recalculate logs based on remaining time
            currentLogs = Mathf.CeilToInt(burnTime / timePerLog);

            if (burnTime <= 0)
            {
                burnTime = 0;
                currentLogs = 0;
                UpdateVisuals();
            }
            UpdateUI();
        }
    }

    public void AddLog()
    {
        if (currentLogs >= maxLogs) return;

        bool wasOut = burnTime <= 0;

        if (playerInventory != null && logItemData != null)
        {
            if (playerInventory.HasItem(logItemData, 1))
            {
                playerInventory.RemoveItem(logItemData, 1);
                currentLogs++;
                burnTime += timePerLog;
                
                if (wasOut) UpdateVisuals();
                UpdateUI();
            }
            else
            {
                Debug.Log("Player does not have a log!");
            }
        }
        else if (logItemData == null)
        {
            // If no data is assigned, just add it for testing (optional)
            currentLogs++;
            burnTime += timePerLog;
            
            if (wasOut) UpdateVisuals();
            UpdateUI();
        }
    }

    private void UpdateVisuals()
    {
        if (campfireSR != null)
        {
            campfireSR.sprite = (burnTime > 0) ? litSprite : unlitSprite;
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

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            playerInventory = other.GetComponent<PlayerInventory>();
            if (interactionCanvas != null)
            {
                interactionCanvas.SetActive(true);
            }
            UpdateUI();
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            if (interactionCanvas != null)
            {
                interactionCanvas.SetActive(false);
            }
            playerInventory = null;
        }
    }
}
