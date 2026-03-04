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
/// Handles all communication with the SimpleStorage smart contract on Ganache.
///  - Reading: getNumber(), getMessage(), getAll()
///  - Writing: setNumber(uint256), setMessage(string)
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

    /// <summary>Read the stored number from the contract.</summary>
    public async Task<BigInteger> ReadNumber()
    {
        try
        {
            var function = _contract.GetFunction("getNumber");
            BigInteger result = await function.CallAsync<BigInteger>();
            Debug.Log($"[Blockchain] getNumber() => {result}");
            return result;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[Blockchain] ReadNumber failed: {ex.Message}");
            return -1;
        }
    }

    /// <summary>Read the stored message from the contract.</summary>
    public async Task<string> ReadMessage()
    {
        try
        {
            var function = _contract.GetFunction("getMessage");
            string result = await function.CallAsync<string>();
            Debug.Log($"[Blockchain] getMessage() => {result}");
            return result;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[Blockchain] ReadMessage failed: {ex.Message}");
            return $"Error: {ex.Message}";
        }
    }

    // =====================================================================
    //  WRITE FUNCTIONS  (cost gas — create transactions)
    // =====================================================================

    /// <summary>Store a new number on-chain.</summary>
    public async Task<string> WriteNumber(BigInteger number)
    {
        try
        {
            var function = _contract.GetFunction("setNumber");
            var cfg = BlockchainConfig.Instance;

            TransactionReceipt receipt = await function.SendTransactionAndWaitForReceiptAsync(
                _account.Address,
                new HexBigInteger(cfg.GasLimit),
                new HexBigInteger(0),   // value (no ETH sent)
                null,                   // cancellation token
                number
            );

            Debug.Log($"[Blockchain] setNumber TX hash : {receipt.TransactionHash}");
            Debug.Log($"[Blockchain] Block number      : {receipt.BlockNumber}");
            Debug.Log($"[Blockchain] Gas used           : {receipt.GasUsed}");
            return receipt.TransactionHash;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[Blockchain] WriteNumber failed: {ex.Message}");
            return null;
        }
    }

    /// <summary>Store a new message on-chain.</summary>
    public async Task<string> WriteMessage(string message)
    {
        try
        {
            var function = _contract.GetFunction("setMessage");
            var cfg = BlockchainConfig.Instance;

            TransactionReceipt receipt = await function.SendTransactionAndWaitForReceiptAsync(
                _account.Address,
                new HexBigInteger(cfg.GasLimit),
                new HexBigInteger(0),
                null,
                message
            );

            Debug.Log($"[Blockchain] setMessage TX hash: {receipt.TransactionHash}");
            Debug.Log($"[Blockchain] Block number      : {receipt.BlockNumber}");
            Debug.Log($"[Blockchain] Gas used           : {receipt.GasUsed}");
            return receipt.TransactionHash;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[Blockchain] WriteMessage failed: {ex.Message}");
            return null;
        }
    }
}
