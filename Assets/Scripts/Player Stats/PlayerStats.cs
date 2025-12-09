using UnityEngine;

public class PlayerStats : MonoBehaviour
{
    public float Health = 100f;
    public float Hunger = 100f;
    public float Thirst = 100f;
    public float Energy = 100f;

    public float hungerDecay = 1f;
    public float thirstDecay = 1.5f;
    public float energyDecay = 0.5f;

    private void Update()
    {
        float dt = Time.deltaTime / 60f;

        Hunger = Mathf.Max(0, Hunger - hungerDecay * dt);
        Thirst = Mathf.Max(0, Thirst - thirstDecay * dt);
        Energy = Mathf.Max(0, Energy - energyDecay * dt);

        if (Hunger <= 0 || Thirst <= 0 || Energy <= 0)
        {
            Health = Mathf.Max(0, Health - 5f * Time.deltaTime);
        }
    }
}
