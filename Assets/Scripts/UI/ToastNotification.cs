using UnityEngine;
using TMPro;

public class ToastNotification : MonoBehaviour
{
    public static ToastNotification Instance; // This allows ShopManager to find it

    [Header("UI References")]
    public GameObject toastPanel;
    public TextMeshProUGUI toastText;

    private void Awake()
    {
        // Singleton pattern
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        if (toastPanel != null) toastPanel.SetActive(false);
    }

    public void ShowSuccess(string message) => Show(message);
    public void ShowError(string message) => Show(message);
    public void ShowWarning(string message) => Show(message);

    private void Show(string message)
    {
        if (toastPanel == null || toastText == null) return;

        toastText.text = message;
        toastPanel.SetActive(true);

        // Auto-hide after 3 seconds
        CancelInvoke("Hide");
        Invoke("Hide", 3f);
    }

    private void Hide() => toastPanel.SetActive(false);
}