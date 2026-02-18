using UnityEngine;
using UnityEngine.SceneManagement;

public class MenuButtons : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Reference to the MenuUI script to handle resuming.")]
    public MenuUI menuUI;

    [Header("Scene Settings")]
    [Tooltip("The name of the Main Menu scene to load.")]
    public string mainMenuSceneName = "Main Menu";
    [Tooltip("The name of the Settings scene to load.")]
    public string settingsSceneName = "Settings";

    /// <summary>
    /// Resumes the game by hiding the menu and resetting time scale.
    /// </summary>
    public void Resume()
    {
        if (menuUI != null)
        {
            menuUI.ResumeGame();
        }
        else
        {
            Debug.LogWarning("MenuUI reference is missing on MenuButtons script.");
            // Fallback
            Time.timeScale = 1f;
        }
    }

    /// <summary>
    /// Placeholder for the Save functionality.
    /// </summary>
    public void SaveGame()
    {
        Debug.Log("Save Game button clicked. Persistence system not yet implemented.");
    }

    /// <summary>
    /// Placeholder for opening the Settings menu.
    /// </summary>
    public void OpenSettings()
    {
        Debug.Log("Settings button clicked. Scene navigation for settings is currently disabled.");
    }

    /// <summary>
    /// Placeholder for returning from the Settings menu.
    /// </summary>
    public void GoBackFromSettings()
    {
        Debug.Log("Back button clicked from settings. Placeholder logic only.");
    }

    /// <summary>
    /// Returns the player to the Main Menu.
    /// </summary>
    public void QuitToMainMenu()
    {
        // CRITICAL: Reset time scale before changing scenes!
        Time.timeScale = 1f;
        SceneManager.LoadScene(mainMenuSceneName);
    }

    /// <summary>
    /// Completely closes the application.
    /// </summary>
    public void ExitToDesktop()
    {
        Debug.Log("Exiting to Desktop...");
        Application.Quit();

        #if UNITY_EDITOR
        // This allows testing the quit functionality within the Unity Editor
        UnityEditor.EditorApplication.isPlaying = false;
        #endif
    }
}
