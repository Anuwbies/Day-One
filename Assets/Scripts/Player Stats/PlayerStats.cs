using UnityEngine;

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

    [Header("Energy Recovery")]
    public float energyRegenRate = 8f;
    public float energyRegenDelay = 2f;

    [Header("Combat")]
    public int baseAttackDamage = 1;

    private ItemData currentItem;
    private float lastEnergyUseTime = 0f;

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
            Health = Mathf.Clamp(Health - 5f * Time.deltaTime, 0, MaxHealth);
        }
    }

    public void TakeDamage(float amount)
    {
        Health = Mathf.Clamp(Health - amount, 0, MaxHealth);

        Debug.Log($"Player took {amount} damage. Current Health: {Health}");

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