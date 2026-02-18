using UnityEngine;

public class HealthSlider : MonoBehaviour
{
    [Header("References")]
    public RectTransform track;
    public RectTransform fill;

    [Header("Target Sources (Auto-Assigned)")]
    [Tooltip("If used for the Player HUD.")]
    public PlayerStats playerStats;
    [Tooltip("If used for Enemies, Trees, Rocks (World Space UI).")]
    public EnemyHealth enemyHealth;
    [Tooltip("If used for Enemies, will stay visible during aggression.")]
    public EnemyController enemyController;

    [Header("Settings")]
    [Tooltip("The root object of the health bar to enable/disable.")]
    public GameObject uiRoot;
    [Tooltip("How long to stay visible after taking damage.")]
    public float hideDelay = 10f;
    public bool smoothTransition = true;
    public float transitionSpeed = 10f;

    private float currentDisplayHealth;
    private float lastHealth;
    private float lastDamageTime = -999f;

    private void Start()
    {
        // 1. Try to find EnemyHealth (common for World Space UI on objects)
        if (enemyHealth == null)
        {
            enemyHealth = GetComponentInParent<EnemyHealth>();
        }

        // 2. Try to find EnemyController
        if (enemyController == null)
        {
            enemyController = GetComponentInParent<EnemyController>();
        }

        // 3. If no EnemyHealth, try finding PlayerStats (common for HUD)
        if (enemyHealth == null && playerStats == null)
        {
            // Check parent first
            playerStats = GetComponentInParent<PlayerStats>();

            // Fallback to searching by tag
            if (playerStats == null)
            {
                GameObject player = GameObject.FindGameObjectWithTag("Player");
                if (player != null)
                    playerStats = player.GetComponent<PlayerStats>();
            }
        }

        // Initialize immediate value to prevent sliding from 0 at start
        if (enemyHealth != null)
        {
            currentDisplayHealth = enemyHealth.currentHealth;
        }
        else if (playerStats != null)
        {
            currentDisplayHealth = playerStats.Health;
        }

        lastHealth = currentDisplayHealth;

        UpdateVisuals();

        // Initial visibility check
        if (uiRoot != null)
        {
            float max = enemyHealth != null ? enemyHealth.maxHealth : (playerStats != null ? playerStats.MaxHealth : 1);
            
            // Start visible only for the Player HUD if currently damaged.
            // Enemies and Resources (Trees/Rocks) stay hidden until hit.
            bool isDamaged = currentDisplayHealth < max;
            bool isPlayerHUD = playerStats != null;
            bool shouldShowInitial = isDamaged && isPlayerHUD;
            uiRoot.SetActive(shouldShowInitial && currentDisplayHealth > 0);
        }
    }

    private void Update()
    {
        float targetHealth = 0f;
        float maxHealth = 0f;

        if (enemyHealth != null)
        {
            targetHealth = enemyHealth.currentHealth;
            maxHealth = enemyHealth.maxHealth;
        }
        else if (playerStats != null)
        {
            targetHealth = playerStats.Health;
            maxHealth = playerStats.MaxHealth;
        }
        else
        {
            return; // No health source found
        }

        // Detect damage to reset the timer
        if (targetHealth < lastHealth)
        {
            lastDamageTime = Time.time;
        }
        lastHealth = targetHealth;

        bool hasBeenHit = lastDamageTime > 0;
        bool isAggroed = enemyController != null && enemyController.IsAggroed;

        // If currently aggroed and we've been hit before, keep the timer refreshed
        // so that the hideDelay only starts counting down AFTER aggro is lost.
        if (isAggroed && hasBeenHit)
        {
            lastDamageTime = Time.time;
        }

        // Visibility logic: 
        // 1. Must be alive (health > 0)
        // 2. For Player HUD: Show if hit recently OR currently damaged.
        // 3. For Everything else (Enemies, Resources): Show ONLY if hit recently (and hit at least once).
        bool isAlive = targetHealth > 0;
        bool recentlyActive = Time.time < lastDamageTime + hideDelay;
        bool isPlayerHUD = playerStats != null;

        bool shouldShow = isAlive && (
            (isPlayerHUD && (recentlyActive || targetHealth < maxHealth)) ||
            (!isPlayerHUD && hasBeenHit && recentlyActive)
        );

        if (uiRoot != null && uiRoot.activeSelf != shouldShow)
        {
            uiRoot.SetActive(shouldShow);
        }

        if (!shouldShow)
        {
            currentDisplayHealth = targetHealth; // Keep in sync while hidden
            return;
        }

        if (smoothTransition)
        {
            currentDisplayHealth = Mathf.Lerp(currentDisplayHealth, targetHealth, Time.deltaTime * transitionSpeed);
        }
        else
        {
            currentDisplayHealth = targetHealth;
        }

        UpdateVisuals();
    }

    private void UpdateVisuals()
    {
        if (track == null || fill == null) return;

        float maxHealth = 1f;

        // Get max health based on source
        if (enemyHealth != null)
        {
            maxHealth = enemyHealth.maxHealth;
        }
        else if (playerStats != null)
        {
            maxHealth = playerStats.MaxHealth;
        }

        // Prevent division by zero
        if (maxHealth <= 0) maxHealth = 1;

        float pct = Mathf.Clamp01(currentDisplayHealth / maxHealth);
        float trackWidth = track.rect.width;

        // Set the width of the fill based on the track's width * percentage
        fill.sizeDelta = new Vector2(trackWidth * pct, fill.sizeDelta.y);
    }
}