using UnityEngine;

/// <summary>
/// Attach to the "GlobalLoadingSpinner" GameObject.
/// Other scripts call LoadingSpinnerController.Show() / .Hide().
/// </summary>
public class LoadingSpinnerController : MonoBehaviour
{
    public static LoadingSpinnerController Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        Hide(); // start hidden
    }

    /// <summary>Show the spinner (e.g. while a TX is pending).</summary>
    public void Show()
    {
        gameObject.SetActive(true);
        Debug.Log("[Spinner] Showing loading spinner");
    }

    /// <summary>Hide the spinner (e.g. when the TX completes).</summary>
    public void Hide()
    {
        gameObject.SetActive(false);
        Debug.Log("[Spinner] Hiding loading spinner");
    }
}
