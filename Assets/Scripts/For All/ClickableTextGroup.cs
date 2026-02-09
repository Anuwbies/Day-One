using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Linq;

public class ShopCategoryDisplay : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Drag your IAP Product Database asset here.")]
    public IAPProductDatabase productDatabase;

    [Tooltip("The template button that is a child of this object. It will be cloned for each category.")]
    public Button categoryButtonTemplate;

    [Tooltip("Optional: Where to spawn the buttons. If empty, they will spawn inside this GameObject.")]
    public Transform containerOverride;

    [Header("Selection Styling")]
    public Color normalColor = Color.white;
    public Color selectedColor = Color.yellow;

    // Keep track of created buttons to update their state later
    private List<Button> _createdButtons = new List<Button>();

    private void Start()
    {
        GenerateCategoryTabs();
    }

    public void GenerateCategoryTabs()
    {
        if (productDatabase == null)
        {
            Debug.LogError("ShopCategoryDisplay: Database is missing!");
            return;
        }

        if (categoryButtonTemplate == null)
        {
            Debug.LogError("ShopCategoryDisplay: Button Template is missing!");
            return;
        }

        // Determine parent for the new buttons
        Transform spawnParent = containerOverride != null ? containerOverride : this.transform;

        // 1. Get all unique product types explicitly used in your database
        List<ProductType> activeTypes = productDatabase.allProducts
            .Select(p => p.productType)
            .Distinct()
            .OrderBy(t => t.ToString()) // Keep them organized alphabetically
            .ToList();

        // 2. Hide the original template so it doesn't look like a real button
        categoryButtonTemplate.gameObject.SetActive(false);

        // Clear previous list if regenerating
        _createdButtons.Clear();

        // 3. Loop through types and create buttons
        foreach (ProductType type in activeTypes)
        {
            CreateCategoryButton(type, spawnParent);
        }

        // 4. Select the first category by default if available
        if (_createdButtons.Count > 0)
        {
            // Trigger the click on the first button to set initial color and state
            _createdButtons[0].onClick.Invoke();
        }
    }

    private void CreateCategoryButton(ProductType type, Transform parent)
    {
        // Instantiate copy of the template
        Button newBtn = Instantiate(categoryButtonTemplate, parent);
        newBtn.gameObject.name = $"Tab_{type}";
        newBtn.gameObject.SetActive(true);

        // Track it so we can loop through it later for color changes
        _createdButtons.Add(newBtn);

        // Find the TMP Text inside the button
        TMP_Text btnText = newBtn.GetComponentInChildren<TMP_Text>();
        if (btnText != null)
        {
            btnText.text = type.ToString();
            // Set initial color to normal
            btnText.color = normalColor;
        }
        else
        {
            Debug.LogWarning($"ShopCategoryDisplay: No TMP_Text found in children of {newBtn.name}");
        }

        // Add functionality when clicked
        // We pass the specific button reference so we know which one to highlight
        newBtn.onClick.AddListener(() => OnCategoryClicked(type, newBtn));
    }

    private void OnCategoryClicked(ProductType type, Button selectedButton)
    {
        Debug.Log($"<color=cyan>Shop Category Selected:</color> {type}");

        // Update visuals (Highlight selected, dim others)
        UpdateSelectionVisuals(selectedButton);

        // TODO: Connect this to your Shop Manager to filter the grid
        // e.g., ShopManager.Instance.FilterItemsByType(type);
    }

    private void UpdateSelectionVisuals(Button selectedButton)
    {
        foreach (Button btn in _createdButtons)
        {
            if (btn == null) continue;

            TMP_Text btnText = btn.GetComponentInChildren<TMP_Text>();
            if (btnText != null)
            {
                // If this is the button we just clicked, make it yellow (selectedColor)
                // Otherwise, make it white (normalColor)
                btnText.color = (btn == selectedButton) ? selectedColor : normalColor;
            }
        }
    }
}