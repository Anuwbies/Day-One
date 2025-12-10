using UnityEngine;
using UnityEngine.Rendering.Universal;
using TMPro;

public class DayNightCycleURP : MonoBehaviour
{
    [Header("Time Settings")]
    public float dayLengthInMinutes = 10f;
    [Range(0, 24)]
    public float timeOfDay = 12f;
    private float timeMultiplier;
    private int currentDay = 1;

    [Header("Lighting")]
    public Light2D globalLight;
    public Gradient lightColorGradient; // You set this manually

    [Header("Intensity (4-phase values)")]
    [Tooltip("Night intensity (0.00 & 1.00)")]
    public float nightIntensity = 0.05f;

    [Tooltip("Sunrise intensity (0.25)")]
    public float sunriseIntensity = 0.22f;

    [Tooltip("Day intensity (0.50)")]
    public float dayIntensity = 0.45f;

    [Tooltip("Sunset intensity (0.75)")]
    public float sunsetIntensity = 0.22f;

    [Header("Moon Light (Optional)")]
    public Light2D moonLight;
    public float moonNightIntensity = 0.25f;
    public float moonDayIntensity = 0.0f;

    [Header("UI")]
    public TMP_Text timeText;
    public TMP_Text dayText;
    public GameObject sunIcon;
    public GameObject moonIcon;

    private void Start()
    {
        timeMultiplier = 24f / (dayLengthInMinutes * 60f);

        if (lightColorGradient == null || lightColorGradient.colorKeys.Length == 0)
            Debug.LogWarning("Light Color Gradient is NOT assigned.");
    }

    private void Update()
    {
        timeOfDay += Time.deltaTime * timeMultiplier;

        if (timeOfDay >= 24f)
        {
            timeOfDay -= 24f;
            currentDay++;
        }

        UpdateLighting();
        UpdateUI();
    }

    private void UpdateLighting()
    {
        float t = timeOfDay / 24f;

        // Color from gradient
        globalLight.color = lightColorGradient.Evaluate(t);

        // 4-phase manual intensity
        globalLight.intensity = Get4PhaseIntensity(timeOfDay);

        // Moonlight
        if (moonLight != null)
        {
            bool isDay = timeOfDay >= 6 && timeOfDay < 18;
            moonLight.intensity = isDay ? moonDayIntensity : moonNightIntensity;
        }
    }

    private float Get4PhaseIntensity(float hour)
    {
        float t = hour / 24f;

        // Night → Sunrise
        if (t < 0.25f)
            return Mathf.Lerp(nightIntensity, sunriseIntensity, t / 0.25f);

        // Sunrise → Day
        if (t < 0.50f)
            return Mathf.Lerp(sunriseIntensity, dayIntensity, (t - 0.25f) / 0.25f);

        // Day → Sunset
        if (t < 0.75f)
            return Mathf.Lerp(dayIntensity, sunsetIntensity, (t - 0.50f) / 0.25f);

        // Sunset → Night
        return Mathf.Lerp(sunsetIntensity, nightIntensity, (t - 0.75f) / 0.25f);
    }

    private void UpdateUI()
    {
        int hours = Mathf.FloorToInt(timeOfDay);
        int minutes = Mathf.FloorToInt((timeOfDay - hours) * 60);

        if (timeText != null)
            timeText.text = $"{hours:00}:{minutes:00}";

        if (dayText != null)
            dayText.text = $"Day {currentDay}";

        bool isDay = hours >= 6 && hours < 18;

        if (sunIcon != null)
            sunIcon.SetActive(isDay);

        if (moonIcon != null)
            moonIcon.SetActive(!isDay);
    }
}
