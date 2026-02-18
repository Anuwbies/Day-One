using UnityEngine;

public class MenuUI : MonoBehaviour
{
    [Header("Menu UI References")]
    [Tooltip("The panel to show or hide when the Escape key is pressed.")]
    public GameObject menuPanel;

    private void Start()
    {
        // Ensure the menu panel is hidden on start
        if (menuPanel != null)
        {
            menuPanel.SetActive(false);
        }
        
        // Ensure game time is running (not paused) when the scene starts
        Time.timeScale = 1f;
    }

    private void Update()
    {
        // Toggle the menu panel when Escape is pressed
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            ToggleMenu();
        }
    }

    /// <summary>
    /// Toggles the menu visibility and pauses/unpauses the game.
    /// </summary>
    public void ToggleMenu()
    {
        if (menuPanel != null)
        {
            bool isOpening = !menuPanel.activeSelf;
            menuPanel.SetActive(isOpening);

            // Pause game time if the menu is open, resume if it's closed
            Time.timeScale = isOpening ? 0f : 1f;
        }
    }

    /// <summary>
    /// Specifically resumes the game and hides the menu.
    /// Use this for a "Resume" button.
    /// </summary>
    public void ResumeGame()
    {
        if (menuPanel != null)
        {
            menuPanel.SetActive(false);
            Time.timeScale = 1f;
        }
    }
}
