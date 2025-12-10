using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class DayNightCycle2D : MonoBehaviour
{
    [Header("Time Settings")]
    public float dayLengthInMinutes = 10f;
    [Range(0, 24)]
    public float timeOfDay = 12f;  // Start at noon
    private float timeMultiplier;
    private int currentDay = 1;
    private int lastHour = -1; // Used to detect midnight rollover

    [Header("References")]
    public Image nightOverlay;

    [Header("UI")]
    public TMP_Text timeText;      // HH:MM display
    public TMP_Text dayText;       // "Day X"
    public GameObject sunIcon;     // Enable/Disable
    public GameObject moonIcon;    // Enable/Disable

    // Internal gradient used for day/night transitions
    private Gradient overlayColorGradient;

    private void Awake()
    {
        CreateDefaultGradient();
    }

    private void Start()
    {
        timeMultiplier = 24f / (dayLengthInMinutes * 60f);
        UpdateUI();
    }

    private void Update()
    {
        float previousTime = timeOfDay;

        timeOfDay += Time.deltaTime * timeMultiplier;
        if (timeOfDay >= 24f)
        {
            timeOfDay -= 24f;   // wrap to 0–24
            currentDay++;       // new day
        }

        UpdateOverlay();
        UpdateUI();
    }

    private void UpdateOverlay()
    {
        float t = timeOfDay / 24f;
        nightOverlay.color = overlayColorGradient.Evaluate(t);
    }

    private void UpdateUI()
    {
        // Convert to hours & minutes
        int hours = Mathf.FloorToInt(timeOfDay);
        int minutes = Mathf.FloorToInt((timeOfDay - hours) * 60f);

        // Time format HH:MM
        timeText.text = $"{hours:00}:{minutes:00}";

        // Day text
        dayText.text = $"Day {currentDay}";

        // Sun/Moon switching
        if (hours >= 6 && hours < 18)
        {
            sunIcon.SetActive(true);
            moonIcon.SetActive(false);
        }
        else
        {
            sunIcon.SetActive(false);
            moonIcon.SetActive(true);
        }
    }

    private void CreateDefaultGradient()
    {
        overlayColorGradient = new Gradient();

        GradientColorKey[] colorKeys = new GradientColorKey[5];
        GradientAlphaKey[] alphaKeys = new GradientAlphaKey[5];

        // VERY DARK Midnight (0.0)
        colorKeys[0].color = new Color(0.005f, 0.01f, 0.02f);
        colorKeys[0].time = 0.0f;
        alphaKeys[0].alpha = 0.95f;
        alphaKeys[0].time = 0.0f;

        // Dark Sunrise (0.2)
        colorKeys[1].color = new Color(0.28f, 0.20f, 0.15f);
        colorKeys[1].time = 0.2f;
        alphaKeys[1].alpha = 0.55f;
        alphaKeys[1].time = 0.2f;

        // Noon (0.5)
        colorKeys[2].color = Color.white;
        colorKeys[2].time = 0.5f;
        alphaKeys[2].alpha = 0.0f;
        alphaKeys[2].time = 0.5f;

        // Dark Sunset (0.8)
        colorKeys[3].color = new Color(0.30f, 0.16f, 0.12f);
        colorKeys[3].time = 0.8f;
        alphaKeys[3].alpha = 0.60f;
        alphaKeys[3].time = 0.8f;

        // VERY DARK Midnight again (1.0)
        colorKeys[4].color = new Color(0.005f, 0.01f, 0.02f);
        colorKeys[4].time = 1.0f;
        alphaKeys[4].alpha = 0.95f;
        alphaKeys[4].time = 1.0f;

        overlayColorGradient.SetKeys(colorKeys, alphaKeys);
    }
}
