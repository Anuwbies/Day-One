using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Thirdweb;
using Thirdweb.Unity;

public class WalletConnector : MonoBehaviour
{
    [Header("UI References")]
    public Button googleSignInButton;
    public Button walletConnectButton;
    public TextMeshProUGUI statusText;   // Shows "Connected: 0x1a2B..." or "Sign In"
    public GameObject shopButton;        // Shown after connection
    public GameObject connectionPanel;   // Panel with both buttons (hidden after connect)

    // Wallet state
    private IThirdwebWallet connectedWallet;
    private string connectedAddress;

    void Start()
    {
        Debug.Log("WalletConnector.Start() running");

        // Wire up button clicks
        if (googleSignInButton != null)
            googleSignInButton.onClick.AddListener(OnGoogleSignIn);
        else
            Debug.LogWarning("WalletConnector: googleSignInButton is NOT assigned in Inspector");

        if (walletConnectButton != null)
            walletConnectButton.onClick.AddListener(OnWalletConnect);
        else
            Debug.LogWarning("WalletConnector: walletConnectButton is NOT assigned in Inspector");

        // Hide shop button until connected
        if (shopButton != null) shopButton.SetActive(false);

        // Optional: show default status
        if (statusText != null) statusText.text = "Sign In";
    }

    /// <summary>
    /// Sign in with Google using InAppWallet.
    /// This creates a wallet for the player behind the scenes — they don't
    /// need MetaMask or any crypto knowledge! Works on ALL platforms.
    /// </summary>
    public async void OnGoogleSignIn()
    {
        Debug.Log("WalletConnector.OnGoogleSignIn() clicked");

        SetButtonsInteractable(false);
        UpdateStatus("Signing in...");

        try
        {
            // InAppWallet + Google auth — works on every platform!
            var inAppWalletOptions = new InAppWalletOptions(
                authprovider: AuthProvider.Google
            );

            var walletOptions = new WalletOptions(
                provider: WalletProvider.InAppWallet,
                chainId: 80002, // Polygon Amoy testnet (change to 137 for mainnet)
                inAppWalletOptions: inAppWalletOptions
            );

            Debug.Log("WalletConnector: calling ThirdwebManager.Instance.ConnectWallet (Google)");
            connectedWallet = await ThirdwebManager.Instance.ConnectWallet(walletOptions);
            connectedAddress = await connectedWallet.GetAddress();

            OnWalletConnected();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Google sign-in failed: {e.Message}");
            UpdateStatus("Sign-in failed. Try again!");
            SetButtonsInteractable(true);
        }
    }

    /// <summary>
    /// Connect an external wallet (MetaMask, Trust Wallet, Rainbow, etc.)
    /// using WalletConnect. Shows a QR code on desktop or deep-links on mobile.
    /// </summary>
    public async void OnWalletConnect()
    {
        Debug.Log("WalletConnector.OnWalletConnect() clicked");

        SetButtonsInteractable(false);
        UpdateStatus("Connecting...");

        try
        {
            var walletOptions = new WalletOptions(
                provider: WalletProvider.WalletConnectWallet,
                chainId: 80002 // Polygon Amoy testnet (change to 137 for mainnet)
            );

            Debug.Log("WalletConnector: calling ThirdwebManager.Instance.ConnectWallet (WalletConnect)");
            // This opens the WalletConnect modal automatically
            connectedWallet = await ThirdwebManager.Instance.ConnectWallet(walletOptions);
            connectedAddress = await connectedWallet.GetAddress();

            OnWalletConnected();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"WalletConnect failed: {e.Message}");
            UpdateStatus("Connection failed. Try again!");
            SetButtonsInteractable(true);
        }
    }

    private void OnWalletConnected()
    {
        if (string.IsNullOrEmpty(connectedAddress))
        {
            Debug.LogWarning("WalletConnector.OnWalletConnected called but connectedAddress is empty");
            UpdateStatus("Connection error");
            SetButtonsInteractable(true);
            return;
        }

        // Shorten the address for display (0x1a2B...9f4C)
        string shortAddress = connectedAddress.Substring(0, 6) + "..."
            + connectedAddress.Substring(connectedAddress.Length - 4);

        UpdateStatus($"Connected: {shortAddress}");
        Debug.Log($"WalletConnector: Wallet connected: {connectedAddress}");

        // Hide sign-in buttons, show shop button
        if (connectionPanel != null) connectionPanel.SetActive(false);
        if (shopButton != null) shopButton.SetActive(true);

        // If you add OwnershipManager later, you can re-enable this:
        // FindObjectOfType<OwnershipManager>()?.CheckOwnership(connectedAddress);
    }

    private void SetButtonsInteractable(bool interactable)
    {
        if (googleSignInButton != null) googleSignInButton.interactable = interactable;
        if (walletConnectButton != null) walletConnectButton.interactable = interactable;
    }

    private void UpdateStatus(string message)
    {
        if (statusText != null) statusText.text = message;
        Debug.Log($"WalletConnector status: {message}");
    }

    // Other scripts call these to get the wallet info
    public string GetConnectedAddress() => connectedAddress;
    public IThirdwebWallet GetWallet() => connectedWallet;
}