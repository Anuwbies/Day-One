using UnityEngine;
using UnityEngine.UI;

// For the parent to receive trigger events from its children, 
// the parent MUST have a Rigidbody2D component.
[RequireComponent(typeof(Rigidbody2D))]
public class LeafBedLogic : MonoBehaviour
{
    private const float PositionGizmoRadius = 0.05f;
    private const float HeadDirectionGizmoLength = 0.1f;

    [Header("References")]
    [Tooltip("Drag the child object with the range Trigger Collider here.")]
    [SerializeField] private Collider2D rangeTrigger;

    [SerializeField] private GameObject interactionCanvas;
    [SerializeField] private Button sleepButton;

    [Header("Trigger Settings")]
    [Tooltip("The tag of the player object (or its Rigidbody).")]
    [SerializeField] private string targetTag = "Player";
    [Tooltip("Optional: If assigned, only this specific player collider will trigger the UI. If left empty, any collider with the correct tag will work.")]
    [SerializeField] private Collider2D targetPlayerCollider;

    [Header("Sleep Settings")]
    [SerializeField] private float sleepTimeMultiplier = 20f;
    [SerializeField] private float maxSleepHours = 8f;
    [Tooltip("Multiplier for hunger/thirst decay while sleeping (e.g., 0.5 means half decay).")]
    [SerializeField] private float sleepDecayMultiplier = 0.5f;
    [SerializeField] private float minHungerToSleep = 10f;
    [SerializeField] private float minThirstToSleep = 10f;
    [Tooltip("The offset from the bed's center where the player will snap to.")]
    [SerializeField] private Vector3 sleepOffset = Vector3.zero;
    [Tooltip("The offset from the bed's center where the player will appear when waking up.")]
    [SerializeField] private Vector3 wakeUpOffset = Vector3.zero;
    
    private PlayerInventory playerInventory;
    private PlayerStats playerStats;
    private PlayerMovement playerMovement;
    private PlayerAttack playerAttack;
    private DayNightCycleURP dayNightCycle;
    private bool isFastForwarding = false;
    private float hoursSlept = 0f;

    // Use a HashSet to track unique player colliders currently in the range trigger.
    private System.Collections.Generic.HashSet<Collider2D> playerCollidersInRange = new System.Collections.Generic.HashSet<Collider2D>();

    // To restore player state
    private Vector3 originalPosition;
    private Quaternion originalRotation;
    private bool wasUsingGravity; // In case we need to disable physics components

    // Minimap fix
    private Transform minimapCameraTransform;
    private Quaternion minimapOriginalRotation;

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

        if (sleepButton != null)
        {
            sleepButton.onClick.AddListener(StartFastForward);
        }

        dayNightCycle = Object.FindFirstObjectByType<DayNightCycleURP>();
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
            playerInventory = null;
            playerStats = null;
            playerMovement = null;
            playerAttack = null;
        }

        // Ensure fast forwarding stops if bed is disabled
        StopFastForward();
    }

    private void Update()
    {
        if (isFastForwarding)
        {
            float deltaHours = Time.deltaTime * sleepTimeMultiplier;

            // Fast forward time
            if (dayNightCycle != null)
            {
                dayNightCycle.AdvanceTime(deltaHours);

                // Fast forward campfires
                // 1 world hour normally takes (1 / timeMultiplier) real seconds.
                // So deltaHours world hours is equivalent to (deltaHours / timeMultiplier) real seconds.
                float equivalentRealSeconds = deltaHours / dayNightCycle.timeMultiplier;
                
                // We subtract the extra time, because CampfireLogic.Update() still runs and handles Time.deltaTime.
                float extraSecondsToConsume = equivalentRealSeconds - Time.deltaTime;
                if (extraSecondsToConsume > 0)
                {
                    foreach (var campfire in CampfireLogic.AllCampfires)
                    {
                        campfire.ConsumeTime(extraSecondsToConsume);
                    }
                }
            }

            // Minimap rotation fix
            if (minimapCameraTransform != null)
            {
                minimapCameraTransform.rotation = minimapOriginalRotation;
            }

            // Reduce hunger and thirst
            if (playerStats != null)
            {
                // gameMinutes = deltaHours * 60
                float gameMinutes = deltaHours * 60f;
                
                // Subtract the decay for the "skipped" time. 
                // Note: PlayerStats.Update still runs normally, but for a very small Time.deltaTime.
                playerStats.Hunger = Mathf.Clamp(playerStats.Hunger - playerStats.hungerDecay * gameMinutes * sleepDecayMultiplier, 0, playerStats.MaxHunger);
                playerStats.Thirst = Mathf.Clamp(playerStats.Thirst - playerStats.thirstDecay * gameMinutes * sleepDecayMultiplier, 0, playerStats.MaxThirst);

                // Automatic wake up if thresholds reached
                if (playerStats.Hunger < minHungerToSleep || playerStats.Thirst < minThirstToSleep)
                {
                    Debug.Log("Woke up because of hunger or thirst!");
                    StopFastForward();
                }
            }

            // Track sleep duration
            hoursSlept += deltaHours;
            if (hoursSlept >= maxSleepHours)
            {
                StopFastForward();
            }

            // Cancel on movement
            if (Mathf.Abs(Input.GetAxisRaw("Horizontal")) > 0.1f || Mathf.Abs(Input.GetAxisRaw("Vertical")) > 0.1f)
            {
                StopFastForward();
            }

            // Cancel on mouse click
            if (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1) || Input.GetMouseButtonDown(2))
            {
                StopFastForward();
            }
        }
    }

    public void StartFastForward()
    {
        if (interactionCanvas != null)
        {
            interactionCanvas.SetActive(false);
        }

        if (playerStats != null)
        {
            if (playerStats.Hunger < minHungerToSleep || playerStats.Thirst < minThirstToSleep)
            {
                Debug.Log("Too hungry or thirsty to sleep!");
                return;
            }
        }

        if (playerStats != null)
        {
            // Find minimap camera if not already found
            foreach (Camera c in playerStats.GetComponentsInChildren<Camera>())
            {
                if (!c.CompareTag("MainCamera"))
                {
                    minimapCameraTransform = c.transform;
                    minimapOriginalRotation = minimapCameraTransform.rotation;
                    break;
                }
            }

            // Snap player and rotate
            originalRotation = playerStats.transform.rotation;
            playerStats.transform.position = transform.position + sleepOffset;
            playerStats.transform.rotation = Quaternion.Euler(0, 0, 90f);

            // Disable movement and attack if possible
            if (playerMovement != null) playerMovement.enabled = false;
            if (playerAttack != null) playerAttack.enabled = false;
        }

        isFastForwarding = true;
        hoursSlept = 0f;
        
        if (dayNightCycle != null)
        {
            dayNightCycle.isPaused = true;
        }

        // Pause all campfires normal countdown
        foreach (var campfire in CampfireLogic.AllCampfires)
        {
            campfire.isPaused = true;
        }

        // Optionally hide the canvas or disable the button while fast forwarding
        if (sleepButton != null) sleepButton.interactable = false;
    }

    private void OnDrawGizmosSelected()
    {
        // 1. Sleep Position (Cyan)
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position + sleepOffset, PositionGizmoRadius);
        
        // Draw a small line indicating the "head" direction (90 deg rotation)
        Vector3 headDir = Quaternion.Euler(0, 0, 90f) * Vector3.up * HeadDirectionGizmoLength;
        Gizmos.DrawRay(transform.position + sleepOffset, headDir);

        // 2. Wake Up Position (Yellow)
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position + wakeUpOffset, PositionGizmoRadius);
        Gizmos.DrawLine(transform.position + sleepOffset, transform.position + wakeUpOffset);
    }

    public void StopFastForward()
    {
        // Only re-enable the UI if the player is still in range
        if (interactionCanvas != null && playerCollidersInRange.Count > 0)
        {
            interactionCanvas.SetActive(true);
        }

        if (isFastForwarding)
        {
            if (playerStats != null)
            {
                // Restore rotation, apply wake-up offset, and re-enable movement/attack
                playerStats.transform.rotation = originalRotation;
                playerStats.transform.position = transform.position + wakeUpOffset;

                if (playerMovement != null) playerMovement.enabled = true;
                if (playerAttack != null)
                {
                    playerAttack.enabled = true;
                    playerAttack.BlockAttackUntilMouseRelease();
                }

                // Restore minimap rotation
                if (minimapCameraTransform != null)
                {
                    minimapCameraTransform.rotation = minimapOriginalRotation;
                    minimapCameraTransform = null;
                }
            }

            isFastForwarding = false;
            
            if (dayNightCycle != null)
            {
                dayNightCycle.isPaused = false;
            }

            // Resume all campfires normal countdown
            foreach (var campfire in CampfireLogic.AllCampfires)
            {
                campfire.isPaused = false;
            }

            if (sleepButton != null) sleepButton.interactable = true;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // 1. If a range trigger is specified on THIS object, ensure this collision involves it
        if (rangeTrigger != null && !other.IsTouching(rangeTrigger)) return;

        if (IsTargetCollider(other))
        {
            if (playerCollidersInRange.Add(other))
            {
                // Only initialize and show UI if this is the first collider entering
                if (playerInventory == null)
                {
                    playerInventory = other.attachedRigidbody.GetComponent<PlayerInventory>();
                    playerStats = other.attachedRigidbody.GetComponent<PlayerStats>();
                    playerMovement = other.attachedRigidbody.GetComponent<PlayerMovement>();
                    playerAttack = other.attachedRigidbody.GetComponent<PlayerAttack>();
                    
                    if (interactionCanvas != null)
                    {
                        interactionCanvas.SetActive(true);
                    }
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
                    playerInventory = null;
                    playerStats = null;
                    playerMovement = null;
                    playerAttack = null;
                    
                    // Also stop fast forwarding if player leaves range
                    StopFastForward();
                }
            }
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

        PlayerAttack attackComponent = playerObj.GetComponentInChildren<PlayerAttack>(true);
        Collider2D attackAreaCollider = attackComponent != null ? attackComponent.attackCollider : null;

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
