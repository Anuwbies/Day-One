using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SelectedButton : MonoBehaviour
{
    [Header("Colors")]
    public Color selectedColor = Color.white;
    public Color unselectedColor = new Color(0.7f, 0.7f, 0.7f, 1f);

    [Header("Default Selection")]
    [Tooltip("The button that will be highlighted by default when the object is enabled.")]
    public Button defaultButton;

    private Button currentSelected;

    private void Start()
    {
        // Setup all child buttons
        Button[] buttons = GetComponentsInChildren<Button>(true);
        foreach (Button btn in buttons)
        {
            // Use a local variable to avoid closure issues in the loop
            Button targetBtn = btn;
            targetBtn.onClick.AddListener(() => SelectButton(targetBtn));
            
            // Initialize all to unselected color
            SetButtonColor(targetBtn, unselectedColor);
        }

        // Select the default button if assigned
        if (defaultButton != null)
        {
            SelectButton(defaultButton);
        }
    }

    /// <summary>
    /// Highlights the specified button and reverts the previously selected one.
    /// </summary>
    public void SelectButton(Button btn)
    {
        // Reset the previous selection
        if (currentSelected != null)
        {
            SetButtonColor(currentSelected, unselectedColor);
        }

        // Apply the new selection
        currentSelected = btn;
        if (currentSelected != null)
        {
            SetButtonColor(currentSelected, selectedColor);
        }
    }

    private void SetButtonColor(Button btn, Color color)
    {
        // Try to find TMP_Text in the button or its children
        TMP_Text text = btn.GetComponentInChildren<TMP_Text>();
        if (text != null)
        {
            text.color = color;
        }
    }
}
