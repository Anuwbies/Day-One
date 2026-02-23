// ============================================================
//  ShopBlockchain.cs — Shop UI ↔ Smart Contract (ChainSafe SDK)
// ============================================================
//  Call ShopBlockchain.Instance.BuyItem(itemId) from your
//  existing Shop UI button OnClick handlers.
//
//  Requires: BlockchainManager.cs, SurvivalCoinService.cs
// ============================================================

using System;
using System.Numerics;
using System.Threading.Tasks;
using UnityEngine;

public class ShopBlockchain : MonoBehaviour
{
    // ── Singleton ────────────────────────────────────────────
    public static ShopBlockchain Instance { get; private set; }

    // ── Events (subscribe from UI scripts) ───────────────────
    /// <summary>Fires after a successful purchase. Args: (itemId, itemName, price).</summary>
    public event Action<int, string, decimal> OnPurchaseSuccess;
    /// <summary>Fires when a purchase fails. Args: (itemId, errorMessage).</summary>
    public event Action<int, string> OnPurchaseFailed;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    // ── Public API ───────────────────────────────────────────

    /// <summary>
    /// Full purchase flow: check balance → approve → buy.
    /// Wire this to your shop item "BUY" button OnClick.
    /// </summary>
    public async void BuyItem(int itemId)
    {
        // ── Guard: wallet must be connected first ─────────────
        if (!BlockchainManager.Instance.IsConnected)
        {
            string msg = "Please connect your wallet before purchasing.";
            Debug.LogWarning($"[ShopBlockchain] {msg}");
            OnPurchaseFailed?.Invoke(itemId, msg);
            return;
        }

        try
        {
            Debug.Log($"[ShopBlockchain] Buying item #{itemId}…");

            // 1. Read item info from the blockchain
            var (name, price, active) = await GetItemInfo(itemId);


            if (!active)
            {
                string msg = $"Item #{itemId} is no longer available.";
                Debug.LogWarning($"[ShopBlockchain] {msg}");
                OnPurchaseFailed?.Invoke(itemId, msg);
                return;
            }

            // 2. Check player has enough SURV
            decimal balance = await SurvivalCoinService.Instance.GetBalance();
            if (balance < price)
            {
                string msg = $"Not enough SURV. Need {price}, you have {balance}.";
                Debug.LogWarning($"[ShopBlockchain] {msg}");
                OnPurchaseFailed?.Invoke(itemId, msg);
                return;
            }

            // 3. Approve the shop to spend the item price
            bool approved = await SurvivalCoinService.Instance.ApproveShop(price);
            if (!approved)
            {
                OnPurchaseFailed?.Invoke(itemId, "Token approval was rejected.");
                return;
            }

            // 4. Call buyItem on the SurvivalShop contract
            var shopContract = BlockchainManager.Instance.GetShopContract();
            await shopContract.Send("buyItem", new object[] { itemId });

            Debug.Log($"[ShopBlockchain] Purchased: {name} for {price} SURV ✓");
            OnPurchaseSuccess?.Invoke(itemId, name, price);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ShopBlockchain] BuyItem failed: {ex.Message}");
            OnPurchaseFailed?.Invoke(itemId, ex.Message);
        }
    }

    /// <summary>Read an item's price (for displaying in the UI).</summary>
    public async Task<decimal> GetItemPrice(int itemId)
    {
        var (_, price, _) = await GetItemInfo(itemId);
        return price;
    }

    /// <summary>Get the purchase history of the connected player.</summary>
    public async Task<int[]> GetPurchaseHistory()
    {
        try
        {
            var bm = BlockchainManager.Instance;
            var shopContract = bm.GetShopContract();

            var result = await shopContract.Call("getPurchaseHistory",
                new object[] { bm.PlayerWalletAddress });

            var raw = result[0] as object[];
            if (raw == null || raw.Length == 0) return Array.Empty<int>();

            int[] history = new int[raw.Length];
            for (int i = 0; i < raw.Length; i++)
                history[i] = int.Parse(raw[i].ToString());

            return history;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ShopBlockchain] GetPurchaseHistory failed: {ex.Message}");
            return Array.Empty<int>();
        }
    }

    // ── Internal ─────────────────────────────────────────────
    private async Task<(string name, decimal price, bool active)> GetItemInfo(int itemId)
    {
        var shopContract = BlockchainManager.Instance.GetShopContract();
        var result = await shopContract.Call("getItem", new object[] { itemId });

        string name = result[1].ToString();
        BigInteger rawPrice = BigInteger.Parse(result[2].ToString());
        bool active = bool.Parse(result[3].ToString());

        decimal price = (decimal)rawPrice / 1_000_000_000_000_000_000m;
        return (name, price, active);
    }
}
