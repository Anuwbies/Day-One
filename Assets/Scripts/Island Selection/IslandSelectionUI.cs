using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using System.Collections;

public class IslandSelectionUI : MonoBehaviour
{
    [Header("Database")]
    public IslandDatabase islandDatabase;

    [Header("UI Selection Parent")]
    public Transform buttonParent;
    public GameObject islandButtonPrefab;

    [Header("Island Detail Displays")]
    public TMP_Text nameDisplay;
    public Image imageDisplay;
    public TMP_Text descriptionDisplay;

    [Header("Navigation")]
    public Button enterIslandButton;

    [Header("Tags")]
    public Transform tagsParent;
    public GameObject tagPrefab;

    [Header("Starting Items")]
    public Transform itemsParent;
    public GameObject itemPrefab;

    [Header("Objectives")]
    public Transform objectivesParent;
    public GameObject objectivePrefab;

    [Header("Selection Visuals")]
    public Color selectedTextColor = Color.black;
    public Color unselectedTextColor = Color.white;
    public float selectedFontSize = 45f;
    public float unselectedFontSize = 30f;
    public float transitionDuration = 0.2f;

    private GameObject currentSelectedButton;
    private IslandData currentSelectedIsland;
    private Dictionary<GameObject, Coroutine> activeTransitions = new Dictionary<GameObject, Coroutine>();

    public IslandData CurrentSelectedIsland => currentSelectedIsland;
    public event System.Action<IslandData> SelectedIslandChanged;

    private void Start()
    {
        if (islandDatabase == null)
        {
            Debug.LogError("IslandSelectionUI: IslandDatabase is not assigned!");
            return;
        }

        if (enterIslandButton != null)
        {
            enterIslandButton.onClick.AddListener(EnterSelectedIsland);
        }

        PopulateIslandList();
    }

    private void PopulateIslandList()
    {
        // Clear existing buttons if any
        if (buttonParent != null)
        {
            foreach (Transform child in buttonParent)
            {
                Destroy(child.gameObject);
            }
        }
        activeTransitions.Clear();

        for (int i = 0; i < islandDatabase.islands.Count; i++)
        {
            IslandData island = islandDatabase.islands[i];
            if (island == null) continue;

            GameObject btnObj = Instantiate(islandButtonPrefab, buttonParent);
            
            // Set the name text
            TMP_Text nameText = btnObj.GetComponentInChildren<TMP_Text>();
            if (nameText != null)
            {
                nameText.text = island.islandName;
                nameText.fontSize = unselectedFontSize;
            }

            // Setup button click
            Button btn = btnObj.GetComponent<Button>();
            if (btn != null)
            {
                btn.onClick.AddListener(() => OnIslandSelected(island, btnObj));
            }

            // Initialize visual state
            UpdateButtonVisuals(btnObj, false, true);

            // Optional: Select the first island by default
            if (i == 0)
            {
                OnIslandSelected(island, btnObj);
            }
        }
    }

    private void OnIslandSelected(IslandData island, GameObject buttonObj)
    {
        // Reset previous selection visuals
        if (currentSelectedButton != null && currentSelectedButton != buttonObj)
        {
            UpdateButtonVisuals(currentSelectedButton, false);
        }

        // Apply new selection visuals
        currentSelectedButton = buttonObj;
        currentSelectedIsland = island;
        UpdateButtonVisuals(currentSelectedButton, true);

        // Update enter button interactivity
        if (enterIslandButton != null)
        {
            enterIslandButton.interactable = !string.IsNullOrEmpty(island.sceneName);
        }

        // Update basic info
        if (nameDisplay != null)
        {
            nameDisplay.text = island.islandName;
            ConfigureSingleLineAutoSize(nameDisplay);
        }

        if (imageDisplay != null)
        {
            imageDisplay.sprite = island.image;
            imageDisplay.enabled = island.image != null;
        }
        if (descriptionDisplay != null) descriptionDisplay.text = island.description;

        // Clear and populate tags
        PopulateList(tagsParent, island.tags, tagPrefab, (obj, tag) => {
            TMP_Text t = obj.GetComponentInChildren<TMP_Text>();
            if (t != null)
            {
                t.text = tag;
                t.textWrappingMode = TextWrappingModes.NoWrap;
                t.overflowMode = TextOverflowModes.Overflow;
                // Force TMP to calculate its size immediately
                t.ForceMeshUpdate();
            }

            // Force layout update on the tag itself
            LayoutRebuilder.ForceRebuildLayoutImmediate(obj.GetComponent<RectTransform>());
        });

        // Force layout update on the container holding all tags
        if (tagsParent != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(tagsParent.GetComponent<RectTransform>());
        }

        // Clear and populate starting items
        PopulateList(itemsParent, island.startingItems, itemPrefab, (obj, sItem) => {
            TMP_Text t = obj.GetComponentInChildren<TMP_Text>();
            if (t != null)
            {
                if (sItem.item != null)
                    t.text = $"{sItem.item.itemName} x{sItem.amount}";
                else
                    t.text = "None";
                
                t.textWrappingMode = TextWrappingModes.NoWrap;
            }
        }, "None");

        // Clear and populate objectives
        PopulateList(objectivesParent, island.objectives, objectivePrefab, (obj, objective) => {
            TMP_Text t = obj.GetComponentInChildren<TMP_Text>();
            if (t != null)
            {
                t.text = objective.objectiveTitle;
                t.textWrappingMode = TextWrappingModes.NoWrap;
            }
        }, "Just survive");

        SelectedIslandChanged?.Invoke(currentSelectedIsland);

        Debug.Log($"Selected Island: {island.islandName}");
    }

    private void EnterSelectedIsland()
    {
        if (currentSelectedIsland != null && !string.IsNullOrEmpty(currentSelectedIsland.sceneName))
        {
            IslandSessionData.SelectedIsland = currentSelectedIsland;
            SceneManager.LoadScene(currentSelectedIsland.sceneName);
        }
    }

    private void ConfigureSingleLineAutoSize(TMP_Text text)
    {
        text.enableAutoSizing = true;
        text.fontSizeMin = 6; // Minimum font size to shrink to
        // Keep current font size as max if possible, or use a reasonable default
        text.textWrappingMode = TextWrappingModes.NoWrap;
    }

    private void PopulateList<T>(Transform parent, List<T> items, GameObject prefab, System.Action<GameObject, T> setupAction, string emptyText = "")
    {
        if (parent == null || prefab == null) return;

        foreach (Transform child in parent)
        {
            Destroy(child.gameObject);
        }

        if (items == null || items.Count == 0)
        {
            if (!string.IsNullOrEmpty(emptyText))
            {
                GameObject obj = Instantiate(prefab, parent);
                TMP_Text t = obj.GetComponentInChildren<TMP_Text>();
                if (t != null) t.text = emptyText;
            }
            return;
        }

        foreach (T item in items)
        {
            GameObject obj = Instantiate(prefab, parent);
            setupAction?.Invoke(obj, item);
        }
    }

    private void UpdateButtonVisuals(GameObject buttonObj, bool isSelected, bool immediate = false)
    {
        TMP_Text btnText = buttonObj.GetComponentInChildren<TMP_Text>();
        if (btnText == null) return;

        Color targetColor = isSelected ? selectedTextColor : unselectedTextColor;
        float targetFontSize = isSelected ? selectedFontSize : unselectedFontSize;

        if (immediate)
        {
            btnText.color = targetColor;
            btnText.fontSize = targetFontSize;
            return;
        }

        // Stop any existing transition for this button
        if (activeTransitions.TryGetValue(buttonObj, out Coroutine existing))
        {
            if (existing != null) StopCoroutine(existing);
        }

        // Start new transition
        activeTransitions[buttonObj] = StartCoroutine(TransitionButtonVisuals(btnText, targetColor, targetFontSize));
    }

    private IEnumerator TransitionButtonVisuals(TMP_Text text, Color targetColor, float targetFontSize)
    {
        Color startColor = text.color;
        float startFontSize = text.fontSize;
        float elapsed = 0f;

        while (elapsed < transitionDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / transitionDuration;
            
            text.color = Color.Lerp(startColor, targetColor, t);
            text.fontSize = Mathf.Lerp(startFontSize, targetFontSize, t);
            
            yield return null;
        }

        text.color = targetColor;
        text.fontSize = targetFontSize;
    }
}
