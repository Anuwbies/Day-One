// ============================================================
//  SurvivalCoinService.cs — ERC-20 Helpers (ChainSafe SDK)
// ============================================================
//  Wraps all SurvivalCoin (SURV) blockchain calls.
//  Requires: BlockchainManager.cs in the same scene.
// ============================================================

using System;
using System.Numerics;
using System.Threading.Tasks;
using UnityEngine;

public class SurvivalCoinService : MonoBehaviour
{
    // ── Singleton ────────────────────────────────────────────
    public static SurvivalCoinService Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    // ── Helpers ──────────────────────────────────────────────
    private static decimal ToDecimal(BigInteger raw)
        => (decimal)raw / 1_000_000_000_000_000_000m;

    private static BigInteger ToWei(decimal amount)
        => new BigInteger(amount) * BigInteger.Pow(10, 18);

    // ── Public API ───────────────────────────────────────────

    /// <summary>Read the connected wallet's SURV balance.</summary>
    public async Task<decimal> GetBalance()
    {
        try
        {
            var bm = BlockchainManager.Instance;
            var contract = bm.GetCoinContract();

            var result = await contract.Call("balanceOf", new object[] { bm.PlayerWalletAddress });
            BigInteger raw = BigInteger.Parse(result[0].ToString());

            decimal balance = ToDecimal(raw);
            Debug.Log($"[SurvivalCoinService] Balance: {balance} SURV");
            return balance;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[SurvivalCoinService] GetBalance failed: {ex.Message}");
            return 0;
        }
    }

    /// <summary>Approve the shop to spend `amount` SURV. Call before BuyItem().</summary>
    public async Task<bool> ApproveShop(decimal amount)
    {
        try
        {
            var bm = BlockchainManager.Instance;
            var contract = bm.GetCoinContract();
            BigInteger weiAmount = ToWei(amount);

            Debug.Log($"[SurvivalCoinService] Approving {amount} SURV for shop…");

            await contract.Send("approve", new object[]
            {
                bm.survivalShopAddress,
                weiAmount.ToString()
            });

            Debug.Log("[SurvivalCoinService] Approval confirmed ✓");
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[SurvivalCoinService] ApproveShop failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>Claim 100 free SURV from the faucet (one-time per wallet).</summary>
    public async Task<bool> ClaimTokens()
    {
        try
        {
            Debug.Log("[SurvivalCoinService] Claiming faucet tokens…");
            var contract = BlockchainManager.Instance.GetCoinContract();
            await contract.Send("claimTokens");
            Debug.Log("[SurvivalCoinService] Tokens claimed ✓");
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[SurvivalCoinService] ClaimTokens failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>Check if the player already used the faucet.</summary>
    public async Task<bool> HasClaimed()
    {
        try
        {
            var bm = BlockchainManager.Instance;
            var contract = bm.GetCoinContract();
            var result = await contract.Call("hasClaimed", new object[] { bm.PlayerWalletAddress });
            return bool.Parse(result[0].ToString());
        }
        catch (Exception ex)
        {
            Debug.LogError($"[SurvivalCoinService] HasClaimed failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>Read how much SURV the Shop is allowed to spend.</summary>
    public async Task<decimal> GetShopAllowance()
    {
        try
        {
            var bm = BlockchainManager.Instance;
            var contract = bm.GetCoinContract();
            var result = await contract.Call("allowance", new object[]
            {
                bm.PlayerWalletAddress,
                bm.survivalShopAddress
            });
            BigInteger raw = BigInteger.Parse(result[0].ToString());
            return ToDecimal(raw);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[SurvivalCoinService] GetShopAllowance failed: {ex.Message}");
            return 0;
        }
    }
}
