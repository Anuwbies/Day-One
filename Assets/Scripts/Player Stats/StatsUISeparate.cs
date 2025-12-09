using UnityEngine;
using TMPro;

public class StatsUISeparate : MonoBehaviour
{
    public PlayerStats playerStats;

    public TMP_Text healthText;
    public TMP_Text hungerText;
    public TMP_Text thirstText;
    public TMP_Text energyText;

    private void Update()
    {
        healthText.text = Mathf.RoundToInt(playerStats.Health).ToString();
        hungerText.text = Mathf.RoundToInt(playerStats.Hunger).ToString();
        thirstText.text = Mathf.RoundToInt(playerStats.Thirst).ToString();
        energyText.text = Mathf.RoundToInt(playerStats.Energy).ToString();
    }
}
