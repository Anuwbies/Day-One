using System.Numerics;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Represents one shop item card. Each card reads its price from the
/// blockchain (getNumber) and records purchases (setNumber / setMessage).
///
/// PREFAB STRUCTURE (inside ShopItemCard):
///   - TextMeshProUGUI  "ItemNameText"   (the item title)
///   - TextMeshProUGUI  "PriceText"      (displays blockchain price)
///   - TextMeshProUGUI  "StatusLabel"    (shows "Available" / "Owned" / error)
///   - Button           "BuyButton"      (triggers the purchase TX)
///
/// Drag each child into the matching Inspector slot.
/// </summary>
public class ShopItemCard : MonoBehaviour
{
    [Header("UI References (inside this prefab)")]
    [SerializeField] private TextMeshProUGUI itemNameText;
    [SerializeField] private TextMeshProUGUI priceText;
    [SerializeField] private TextMeshProUGUI statusLabel;
    [SerializeField] private Button          buyButton;

    [Header("Item Data (set per-instance in Inspector)")]
    [Tooltip("Friendly name shown on the card")]
    public string itemName = "Item";

    [Tooltip("A unique item ID used to record the purchase on-chain")]
    public int itemId = 0;

    private BlockchainInteraction _blockchain;
    private bool _purchased = false;

    // ---------- Initialisation ----------

    /// <summary>Called by ShopBlockchainBridge after the blockchain connects.</summary>
    public void Initialise(BlockchainInteraction blockchain)
    {
        _blockchain = blockchain;
        itemNameText.text = itemName;
        buyButton.onClick.AddListener(OnBuyClicked);
    }

    /// <summary>
    /// Read blockchain data and update the card UI.
    /// Uses getNumber() as a demo (you could map itemId to a
    /// mapping in a real contract).
    /// </summary>
    public async void RefreshFromBlockchain()
    {
        if (_blockchain == null || !_blockchain.IsConnected)
        {
            statusLabel.text = "Not connected";
            return;
        }

        try
        {
            statusLabel.text = "Loading...";

            // READ price from blockchain (stored number = price in wei/units)
            BigInteger price = await _blockchain.ReadNumber();
            priceText.text = $"{price} wei";

            // READ ownership message (stored message = last buyer info)
            string message = await _blockchain.ReadMessage();

            if (!string.IsNullOrEmpty(message) && message.Contains($"ITEM#{itemId}:OWNED"))
            {
                _purchased = true;
                statusLabel.text = "Owned";
                buyButton.interactable = false;
                buyButton.GetComponentInChildren<TextMeshProUGUI>().text = "Owned";
            }
            else
            {
                statusLabel.text = "Available";
            }
        }
        catch (System.Exception ex)
        {
            statusLabel.text = "Error loading";
            Debug.LogError($"[ShopItemCard] Refresh failed for '{itemName}': {ex.Message}");
        }
    }

    // ---------- Purchase ----------

    private async void OnBuyClicked()
    {
        if (_purchased || _blockchain == null || !_blockchain.IsConnected) return;

        buyButton.interactable = false;
        statusLabel.text = "Processing TX...";

        // Show the global loading spinner
        LoadingSpinnerController.Instance?.Show();

        try
        {
            // WRITE 1: Record the item ID on-chain as the stored number
            string txHash1 = await _blockchain.WriteNumber(new BigInteger(itemId));
            Debug.Log($"[ShopItemCard] Purchase TX1 (setNumber): {txHash1}");

            // WRITE 2: Record ownership message on-chain
            string purchaseMsg = $"ITEM#{itemId}:OWNED by {_blockchain.WalletAddress}";
            string txHash2 = await _blockchain.WriteMessage(purchaseMsg);
            Debug.Log($"[ShopItemCard] Purchase TX2 (setMessage): {txHash2}");

            if (txHash1 != null && txHash2 != null)
            {
                _purchased = true;
                statusLabel.text = "Owned";
                buyButton.GetComponentInChildren<TextMeshProUGUI>().text = "Owned";
                Debug.Log($"[ShopItemCard] '{itemName}' purchased successfully!");
            }
            else
            {
                statusLabel.text = "TX Failed";
                buyButton.interactable = true;
                Debug.LogError($"[ShopItemCard] Purchase failed for '{itemName}'");
            }
        }
        catch (System.Exception ex)
        {
            statusLabel.text = "Error";
            buyButton.interactable = true;
            Debug.LogError($"[ShopItemCard] Buy failed: {ex.Message}");
        }
        finally
        {
            // Always hide the spinner when done
            LoadingSpinnerController.Instance?.Hide();
        }
    }
}
