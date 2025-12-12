using UnityEngine;

public class PlayerStats : MonoBehaviour
{
    public float Health = 100f;
    public float Hunger = 100f;
    public float Thirst = 100f;
    public float Energy = 100f;

    public float hungerDecay = 1f;
    public float thirstDecay = 1.5f;

    [Header("Energy Recovery")]
    public float energyRegenRate = 8f;          // energy per second
    public float energyRegenDelay = 2f;         // seconds after last attack before regen starts

    private float lastEnergyUseTime = 0f;

    private void Update()
    {
        float dt = Time.deltaTime / 60f;

        // Regular decays
        Hunger = Mathf.Max(0, Hunger - hungerDecay * dt);
        Thirst = Mathf.Max(0, Thirst - thirstDecay * dt);

        // Energy regeneration if enough time has passed
        HandleEnergyRegen();

        // Health decay from low Hunger/Thirst/Energy
        if (Hunger <= 0 || Thirst <= 0 || Energy <= 0)
        {
            Health = Mathf.Max(0, Health - 5f * Time.deltaTime);
        }
    }

    public void UseEnergy(float amount)
    {
        Energy = Mathf.Max(0, Energy - amount);
        lastEnergyUseTime = Time.time;   // reset regen delay
    }

    private void HandleEnergyRegen()
    {
        if (Energy >= 100f)
            return;

        // Not enough time passed since last energy usage
        if (Time.time < lastEnergyUseTime + energyRegenDelay)
            return;

        // Regenerate energy smoothly
        Energy = Mathf.Min(100f, Energy + energyRegenRate * Time.deltaTime);
    }
}
