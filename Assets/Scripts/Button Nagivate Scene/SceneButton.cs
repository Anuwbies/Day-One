using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneButton : MonoBehaviour
{
    [Header("Scene Settings")]
    [Tooltip("Name of the scene to load")]
    public string sceneName;

    /// <summary>
    /// Loads the target scene.
    /// Assign this method to a UI Button OnClick event.
    /// </summary>
    public void LoadScene()
    {
        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogWarning("Scene name is empty. Cannot load scene.");
            return;
        }

        SceneManager.LoadScene(sceneName);
    }

    /// <summary>
    /// Optional: Load scene by build index instead of name.
    /// </summary>
    public void LoadSceneByIndex(int buildIndex)
    {
        SceneManager.LoadScene(buildIndex);
    }

    /// <summary>
    /// Optional: Quit application (useful for Exit buttons).
    /// </summary>
    public void QuitGame()
    {
        Debug.Log("Exiting to Desktop...");
        Application.Quit();

        #if UNITY_EDITOR
        // This allows testing the quit functionality within the Unity Editor
        UnityEditor.EditorApplication.isPlaying = false;
        #endif
    }
}
