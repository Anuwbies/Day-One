using System.Numerics;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Represents one shop item card. Each card reads its per-item price from
/// the blockchain (getItemPrice) and checks ownership (isOwned), then
/// purchases via a single buyItem transaction with ETH value.
///
/// PREFAB STRUCTURE (inside ShopItemCard):
///   - TextMeshProUGUI  "ItemNameText"   (the item title)
///   - TextMeshProUGUI  "PriceText"      (displays blockchain price)
///   - TextMeshProUGUI  "StatusLabel"    (shows "Available" / "Owned ✓" / error)
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
    private bool       _purchased    = false;
    private bool       _priceLoaded  = false;
    private BigInteger _cachedPrice  = BigInteger.Zero;

    // ---------- Initialisation ----------

    /// <summary>Called by ShopBlockchainBridge after the blockchain connects.</summary>
    public void Initialise(BlockchainInteraction blockchain)
    {
        _blockchain = blockchain;
        itemNameText.text = itemName;
        buyButton.interactable = false;   // disabled until RefreshFromBlockchain succeeds
        buyButton.onClick.AddListener(OnBuyClicked);
    }

    /// <summary>
    /// Read per-item price and ownership from the blockchain and update the card UI.
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

            _cachedPrice = await _blockchain.GetItemPrice(itemId);
            _priceLoaded = true;
            // Convert wei to ETH for display (divide by 10^18)
            decimal ethPrice = (decimal)_cachedPrice / 1_000_000_000_000_000_000m;
            priceText.text = $"{ethPrice:0.####} ETH";

            bool owned = await _blockchain.IsOwned(itemId);
            if (owned)
            {
                _purchased = true;
                statusLabel.text = "Owned";
                buyButton.interactable = false;
                buyButton.GetComponentInChildren<TextMeshProUGUI>().text = "Owned";
            }
            else
            {
                statusLabel.text = "Available";
                buyButton.interactable = true;  // safe to buy now that price is confirmed
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

        if (!_priceLoaded)
        {
            statusLabel.text = "Price not loaded — refresh first";
            Debug.LogWarning($"[ShopItemCard] Buy attempted before price loaded for '{itemName}' (itemId={itemId})");
            return;
        }

        buyButton.interactable = false;
        statusLabel.text = "Processing TX...";
        LoadingSpinnerController.Instance?.Show();

        try
        {
            // Use price cached during RefreshFromBlockchain() — no extra contract call needed.
            string txHash = await _blockchain.BuyItem(itemId, _cachedPrice);
            Debug.Log($"[ShopItemCard] Purchase TX: {txHash}");

            if (txHash != null)
            {
                _purchased = true;
                statusLabel.text = "Owned";
                buyButton.GetComponentInChildren<TextMeshProUGUI>().text = "Owned";
            }
            else
            {
                statusLabel.text = "TX Failed";
                buyButton.interactable = true;
            }
        }
        catch (System.Exception ex)
        {
            statusLabel.text = "Error";
            buyButton.interactable = true;
            Debug.LogError($"[ShopItemCard] Buy failed for '{itemName}' (itemId={itemId}): {ex.Message}\n{ex.StackTrace}");
        }
        finally
        {
            LoadingSpinnerController.Instance?.Hide();
        }
    }
}
