// ============================================================
//  BlockchainManager.cs — Wallet Connection & SDK Setup
// ============================================================
//  Singleton MonoBehaviour that wraps the ChainSafe web3.unity
//  SDK (v3.x) for use by SurvivalCoinService and ShopBlockchain.
//
//  SCENE SETUP:
//   1. In the Project window search for "Web3Unity" (filter by
//      Packages). Drag Web3Unity.prefab into your Hierarchy.
//      Path: Packages/io.chainsafe.web3-unity/Runtime/Prefabs/
//   2. Select the Web3Unity object → Inspector → ConnectionHandler
//      → add your connection provider (MetaMask for WebGL,
//      WalletConnect for Editor/desktop).
//   3. Attach THIS script to your "BlockchainServices" GameObject.
//   4. Fill in the contract addresses below after deploying.
// ============================================================

using System;
using System.Threading.Tasks;
using UnityEngine;
using ChainSafe.Gaming.Web3;
using ChainSafe.Gaming.UnityPackage;
using ChainSafe.Gaming.UnityPackage.Connection;
using ChainSafe.Gaming.Evm.Contracts;

public class BlockchainManager : MonoBehaviour
{
    // ── Singleton ────────────────────────────────────────────
    public static BlockchainManager Instance { get; private set; }

    // ── Events ───────────────────────────────────────────────
    /// <summary>Fires after the wallet connects and Web3 is ready.</summary>
    public event Action OnWalletConnected;

    // ── Inspector Fields ─────────────────────────────────────
    [Header("Contract Addresses (fill after deploying to Sepolia)")]
    public string survivalCoinAddress = "";
    public string survivalShopAddress = "";

    // ── State ────────────────────────────────────────────────
    [HideInInspector] public string PlayerWalletAddress;
    [HideInInspector] public bool IsConnected;

    // ── ABIs ─────────────────────────────────────────────────
    public static readonly string SurvivalCoinABI = @"[
        {""inputs"":[],""name"":""claimTokens"",""outputs"":[],""stateMutability"":""nonpayable"",""type"":""function""},
        {""inputs"":[{""name"":""account"",""type"":""address""}],""name"":""balanceOf"",""outputs"":[{""name"":"""",""type"":""uint256""}],""stateMutability"":""view"",""type"":""function""},
        {""inputs"":[{""name"":""spender"",""type"":""address""},{""name"":""amount"",""type"":""uint256""}],""name"":""approve"",""outputs"":[{""name"":"""",""type"":""bool""}],""stateMutability"":""nonpayable"",""type"":""function""},
        {""inputs"":[{""name"":""owner"",""type"":""address""},{""name"":""spender"",""type"":""address""}],""name"":""allowance"",""outputs"":[{""name"":"""",""type"":""uint256""}],""stateMutability"":""view"",""type"":""function""},
        {""inputs"":[{""name"":"""",""type"":""address""}],""name"":""hasClaimed"",""outputs"":[{""name"":"""",""type"":""bool""}],""stateMutability"":""view"",""type"":""function""}
    ]";

    public static readonly string SurvivalShopABI = @"[
        {""inputs"":[{""name"":""_itemId"",""type"":""uint256""}],""name"":""buyItem"",""outputs"":[],""stateMutability"":""nonpayable"",""type"":""function""},
        {""inputs"":[{""name"":""_itemId"",""type"":""uint256""}],""name"":""getItem"",""outputs"":[{""name"":""id"",""type"":""uint256""},{""name"":""name"",""type"":""string""},{""name"":""price"",""type"":""uint256""},{""name"":""active"",""type"":""bool""}],""stateMutability"":""view"",""type"":""function""},
        {""inputs"":[{""name"":""_player"",""type"":""address""}],""name"":""getPurchaseHistory"",""outputs"":[{""name"":"""",""type"":""uint256[]""}],""stateMutability"":""view"",""type"":""function""},
        {""inputs"":[],""name"":""nextItemId"",""outputs"":[{""name"":"""",""type"":""uint256""}],""stateMutability"":""view"",""type"":""function""}
    ]";

    // ── Unity Lifecycle ──────────────────────────────────────
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnEnable()
    {
        // Listen for when the SDK finishes connecting a wallet
        Web3Unity.Web3Initialized += HandleWeb3Initialized;
    }

    private void OnDisable()
    {
        Web3Unity.Web3Initialized -= HandleWeb3Initialized;
    }

    // ── SDK Event Handler ────────────────────────────────────

    /// <summary>
    /// Called by the SDK whenever a wallet finishes connecting.
    /// isLightweight = true means no signer (read-only), false means fully connected.
    /// </summary>
    private void HandleWeb3Initialized((ChainSafe.Gaming.Web3.Web3 web3, bool isLightweight) args)
    {
        if (args.isLightweight)
        {
            // Read-only (no wallet) — not fully connected yet
            IsConnected = false;
            return;
        }

        try
        {
            PlayerWalletAddress = args.web3.Signer.PublicAddress;
            IsConnected = true;
            Debug.Log($"[BlockchainManager] Wallet connected: {PlayerWalletAddress}");
            OnWalletConnected?.Invoke();
        }
        catch (Exception ex)
        {
            Debug.LogError($"[BlockchainManager] Failed to read wallet address: {ex.Message}");
            IsConnected = false;
        }
    }

    // ── Public API ───────────────────────────────────────────

    /// <summary>
    /// Wire this to your "Connect Wallet" button OnClick in the Inspector.
    /// Opens the ChainSafe connection modal so the player can pick their wallet.
    /// </summary>
    public async void ConnectWalletButton()
    {
        try
        {
            Debug.Log("[BlockchainManager] Opening wallet connection modal…");

            // Initialize the SDK (sets up providers, restores saved sessions)
            await Web3Unity.Instance.Initialize(rememberConnection: false);

            // Open the built-in connection modal (MetaMask / WalletConnect picker)
            Web3Unity.ConnectModal.Open();
        }
        catch (Exception ex)
        {
            Debug.LogError($"[BlockchainManager] ConnectWalletButton failed: {ex.Message}");
        }
    }

    // ── Contract Helpers ─────────────────────────────────────

    /// <summary>Get a contract instance. Requires a connected wallet.</summary>
    public Contract GetContract(string address, string abi)
    {
        if (Web3Unity.Web3 == null)
            throw new InvalidOperationException("[BlockchainManager] Web3 is not initialized. Connect wallet first.");

        return Web3Unity.Web3.ContractBuilder.Build(abi, address);
    }

    public Contract GetCoinContract() => GetContract(survivalCoinAddress, SurvivalCoinABI);
    public Contract GetShopContract() => GetContract(survivalShopAddress, SurvivalShopABI);
}
