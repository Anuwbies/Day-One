using System;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.Web3;
using Nethereum.Web3.Accounts;
using Nethereum.Contracts;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using UnityEngine;

/// <summary>
/// Handles all communication with the shop smart contract on Ganache.
///  - Reading: getItemPrice(itemId), isOwned(address, itemId)
///  - Writing: buyItem(itemId) with ETH value
/// Attach to the same GameObject as BlockchainConfig.
/// </summary>
public class BlockchainInteraction : MonoBehaviour
{
    // ---------- Nethereum objects ----------
    private Web3     _web3;
    private Account  _account;
    private Contract _contract;

    // ---------- Public state ----------
    public bool   IsConnected      { get; private set; }
    public string WalletAddress    { get; private set; }
    public string ContractAddress  { get; private set; }

    // ---------- Unity lifecycle ----------
    private void Start()
    {
        ConnectToBlockchain();
    }

    // =====================================================================
    //  CONNECTION
    // =====================================================================

    /// <summary>
    /// Initialise Web3, the wallet account, and the contract handle.
    /// </summary>
    public void ConnectToBlockchain()
    {
        try
        {
            var cfg = BlockchainConfig.Instance;
            if (cfg == null)
            {
                Debug.LogError("[Blockchain] BlockchainConfig.Instance is null. " +
                               "Make sure BlockchainConfig is on an active GameObject.");
                return;
            }

            // 1. Create account from private key
            _account = new Account(cfg.PrivateKey, cfg.ChainId);
            WalletAddress = _account.Address;
            Debug.Log($"[Blockchain] Wallet address: {WalletAddress}");

            // 2. Create Web3 instance pointing at Ganache RPC
            _web3 = new Web3(_account, cfg.RpcUrl);
            Debug.Log($"[Blockchain] Connected to RPC: {cfg.RpcUrl}");

            // 3. Create contract handle from ABI + deployed address
            ContractAddress = cfg.ContractAddress;
            _contract = _web3.Eth.GetContract(cfg.AbiJson, ContractAddress);
            Debug.Log($"[Blockchain] Contract loaded at: {ContractAddress}");

            IsConnected = true;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[Blockchain] Connection failed: {ex.Message}\n{ex.StackTrace}");
            IsConnected = false;
        }
    }

    // =====================================================================
    //  READ FUNCTIONS  (no gas cost)
    // =====================================================================

    /// <summary>Gets the price of an item in wei from the contract.</summary>
    public async Task<BigInteger> GetItemPrice(int itemId)
    {
        var getItemPriceFunction = _contract.GetFunction("getItemPrice");
        return await getItemPriceFunction.CallAsync<BigInteger>(new BigInteger(itemId));
    }

    /// <summary>Checks if the current wallet owns a specific item.</summary>
    public async Task<bool> IsOwned(int itemId)
    {
        var isOwnedFunction = _contract.GetFunction("isOwned");
        return await isOwnedFunction.CallAsync<bool>(WalletAddress, new BigInteger(itemId));
    }

    // =====================================================================
    //  WRITE FUNCTIONS  (cost gas — create transactions)
    // =====================================================================

    /// <summary>Sends ETH + calls buyItem on the contract.</summary>
    public async Task<string> BuyItem(int itemId, BigInteger priceWei)
    {
        var buyItemFunction = _contract.GetFunction("buyItem");
        var receipt = await buyItemFunction.SendTransactionAndWaitForReceiptAsync(
            WalletAddress,
            new Nethereum.Hex.HexTypes.HexBigInteger(300000), // gas limit
            new Nethereum.Hex.HexTypes.HexBigInteger(priceWei), // ETH value sent
            null,
            new BigInteger(itemId)
        );
        return receipt?.TransactionHash;
    }
}
