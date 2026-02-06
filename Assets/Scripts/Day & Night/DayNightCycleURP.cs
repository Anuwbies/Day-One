using UnityEngine;
using UnityEngine.Rendering.Universal;
using TMPro;

public class DayNightCycleURP : MonoBehaviour
{
    [Header("Save System")]
    [Tooltip("If true, time passes between scenes. Uncheck this for gameplay scenes if you want them to always start fresh.")]
    public bool enableTimeTransfer = true;

    [Header("Time Settings")]
    public float dayLengthInMinutes = 10f;
    [Range(0, 24)]
    public float timeOfDay = 12f;
    private float timeMultiplier;
    private int currentDay = 1;

    [Header("Lighting")]
    public Light2D globalLight;
    public Gradient lightColorGradient;

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

        // LOAD: Only load if transfer is enabled and we have data
        if (enableTimeTransfer && TimeTransfer.HasData)
        {
            timeOfDay = TimeTransfer.SavedTime;
            currentDay = TimeTransfer.SavedDay;
        }
    }

    // SAVE: Runs automatically when scene changes
    private void OnDestroy()
    {
        if (enableTimeTransfer)
        {
            TimeTransfer.SavedTime = timeOfDay;
            TimeTransfer.SavedDay = currentDay;
            TimeTransfer.HasData = true;
        }
    }

    // Call this function when starting a completely NEW GAME to reset time
    public void ResetCycle()
    {
        TimeTransfer.HasData = false;
        timeOfDay = 8f; // Reset to morning (or your preferred start time)
        currentDay = 1;
        UpdateLighting(); // Apply immediately
        UpdateUI();
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

        if (globalLight != null && lightColorGradient != null)
            globalLight.color = lightColorGradient.Evaluate(t);

        if (globalLight != null)
            globalLight.intensity = Get4PhaseIntensity(timeOfDay);

        if (moonLight != null)
        {
            bool isDay = timeOfDay >= 6 && timeOfDay < 18;
            moonLight.intensity = isDay ? moonDayIntensity : moonNightIntensity;
        }
    }

    private float Get4PhaseIntensity(float hour)
    {
        float t = hour / 24f;

        if (t < 0.25f)
            return Mathf.Lerp(nightIntensity, sunriseIntensity, t / 0.25f);
        if (t < 0.50f)
            return Mathf.Lerp(sunriseIntensity, dayIntensity, (t - 0.25f) / 0.25f);
        if (t < 0.75f)
            return Mathf.Lerp(dayIntensity, sunsetIntensity, (t - 0.50f) / 0.25f);

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