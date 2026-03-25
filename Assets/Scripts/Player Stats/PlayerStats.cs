using UnityEngine;
using System.Collections;

public class PlayerStats : MonoBehaviour
{
    [Header("Current Stats")]
    public float Health = 100f;
    public float Hunger = 100f;
    public float Thirst = 100f;
    public float Energy = 100f;

    [Header("Max Stats")]
    public float MaxHealth = 100f;
    public float MaxHunger = 100f;
    public float MaxThirst = 100f;
    public float MaxEnergy = 100f;

    [Header("Decay")]
    public float hungerDecay = 1f;
    public float thirstDecay = 1.5f;
    public float sprintEnergyCost = 15f;

    [Header("Energy Recovery")]
    public float energyRegenRate = 8f;
    public float energyRegenDelay = 2f;

    [Header("Combat")]
    public int baseAttackDamage = 1;

    [Header("Damage Feedback")]
    [SerializeField] private SpriteRenderer playerSpriteRenderer;
    [SerializeField] private Color damageFlashColor = Color.red;
    [SerializeField] private float damageFlashDuration = 0.12f;

    private ItemData currentItem;
    private float lastEnergyUseTime = 0f;
    private Color defaultSpriteColor = Color.white;
    private Coroutine damageFlashRoutine;

    private void Awake()
    {
        if (playerSpriteRenderer == null)
        {
            playerSpriteRenderer = GetComponent<SpriteRenderer>();
        }

        if (playerSpriteRenderer != null)
        {
            defaultSpriteColor = playerSpriteRenderer.color;
        }
    }

    private void OnDisable()
    {
        if (damageFlashRoutine != null)
        {
            StopCoroutine(damageFlashRoutine);
            damageFlashRoutine = null;
        }

        if (playerSpriteRenderer != null)
        {
            Color restoredColor = defaultSpriteColor;
            restoredColor.a = playerSpriteRenderer.color.a;
            playerSpriteRenderer.color = restoredColor;
        }
    }

    // =========================
    // HOTBAR HOOK
    // =========================
    public void SetCurrentItem(ItemData item)
    {
        currentItem = item;
    }

    // =========================
    // DAMAGE RESOLUTION
    // =========================
    public int GetDamage(DamageTarget target)
    {
        if (currentItem == null)
            return baseAttackDamage;

        return target switch
        {
            DamageTarget.Enemy => baseAttackDamage + currentItem.damageToEnemy,
            DamageTarget.Tree => baseAttackDamage + currentItem.damageToTree,
            DamageTarget.Rock => baseAttackDamage + currentItem.damageToRock,
            _ => baseAttackDamage
        };
    }

    private void Update()
    {
        float dt = Time.deltaTime / 60f;

        Hunger = Mathf.Clamp(Hunger - hungerDecay * dt, 0, MaxHunger);
        Thirst = Mathf.Clamp(Thirst - thirstDecay * dt, 0, MaxThirst);

        HandleEnergyRegen();

        if (Hunger <= 0 || Thirst <= 0)
        {
            ApplyDamage(5f * Time.deltaTime, false);
        }
    }

    public void TakeDamage(float amount)
    {
        ApplyDamage(amount, true);
    }

    private void ApplyDamage(float amount, bool logDamage)
    {
        if (amount <= 0f)
        {
            return;
        }

        Health = Mathf.Clamp(Health - amount, 0, MaxHealth);
        TriggerDamageFlash();

        if (logDamage)
        {
            Debug.Log($"Player took {amount} damage. Current Health: {Health}");
        }

        if (Health <= 0)
        {
            // Optional: Handle Player Death here
            Debug.Log("Player has died!");
        }
    }

    public void AddHealth(float amount)
    {
        Health = Mathf.Clamp(Health + amount, 0, MaxHealth);
    }

    public void AddHunger(float amount)
    {
        Hunger = Mathf.Clamp(Hunger + amount, 0, MaxHunger);
    }

    public void AddThirst(float amount)
    {
        Thirst = Mathf.Clamp(Thirst + amount, 0, MaxThirst);
    }

    public void AddEnergy(float amount)
    {
        Energy = Mathf.Clamp(Energy + amount, 0, MaxEnergy);
    }

    public void UseEnergy(float amount)
    {
        Energy = Mathf.Clamp(Energy - amount, 0, MaxEnergy);
        lastEnergyUseTime = Time.time;
    }

    /// <summary>
    /// Consumes the provided inventory slot item if it is edible.
    /// Returns true if the item was consumed.
    /// </summary>
    public bool EatItem(InventorySlot slot)
    {
        if (slot == null || slot.item == null || !slot.item.canEat)
            return false;

        ItemData data = slot.item;

        // Apply effects
        AddHealth(data.healthRestore);
        AddHunger(data.hungerRestore);
        AddThirst(data.thirstRestore);
        AddEnergy(data.energyRestore);

        // Reduce amount
        slot.amount--;
        if (slot.amount <= 0)
        {
            slot.item = null;
            slot.amount = 0;
        }

        return true;
    }

    private void TriggerDamageFlash()
    {
        if (playerSpriteRenderer == null)
        {
            return;
        }

        defaultSpriteColor = new Color(defaultSpriteColor.r, defaultSpriteColor.g, defaultSpriteColor.b, playerSpriteRenderer.color.a);

        if (damageFlashRoutine != null)
        {
            StopCoroutine(damageFlashRoutine);
        }

        damageFlashRoutine = StartCoroutine(DamageFlashRoutine());
    }

    private IEnumerator DamageFlashRoutine()
    {
        Color flashColor = damageFlashColor;
        flashColor.a = playerSpriteRenderer.color.a;
        playerSpriteRenderer.color = flashColor;

        yield return new WaitForSeconds(damageFlashDuration);

        Color restoredColor = defaultSpriteColor;
        restoredColor.a = playerSpriteRenderer.color.a;
        playerSpriteRenderer.color = restoredColor;
        damageFlashRoutine = null;
    }

    private void HandleEnergyRegen()
    {
        if (Energy >= MaxEnergy)
            return;

        if (Time.time < lastEnergyUseTime + energyRegenDelay)
            return;

        Energy = Mathf.Clamp(
            Energy + energyRegenRate * Time.deltaTime,
            0,
            MaxEnergy
        );
    }
}
