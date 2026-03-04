using System;
using System.IO;
using UnityEngine;

/// <summary>
/// Reads blockchain_config.json from StreamingAssets and exposes its values
/// to every other script that needs RPC URL, ABI, contract address, etc.
/// Attach this to a persistent GameObject (e.g. "BlockchainManager").
/// </summary>
public class BlockchainConfig : MonoBehaviour
{
    // ---------- Singleton ----------
    public static BlockchainConfig Instance { get; private set; }

    // ---------- Exposed data ----------
    public string RpcUrl           { get; private set; }
    public int    ChainId          { get; private set; }
    public string ContractAddress  { get; private set; }
    public string PrivateKey       { get; private set; }
    public int    GasLimit         { get; private set; }
    public string AbiJson          { get; private set; }

    // ---------- Raw model that mirrors the JSON ----------
    [Serializable]
    private class ConfigData
    {
        public string rpcUrl;
        public int    chainId;
        public string contractAddress;
        public string privateKey;
        public int    gasLimit;
        // abi is parsed separately because it is an array of objects
    }

    // ---------- Unity lifecycle ----------
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        LoadConfig();
    }

    // ---------- Config loader ----------
    private void LoadConfig()
    {
        string path = Path.Combine(Application.streamingAssetsPath, "blockchain_config.json");

        if (!File.Exists(path))
        {
            Debug.LogError($"[BlockchainConfig] Config file not found at: {path}");
            return;
        }

        string json = File.ReadAllText(path);
        Debug.Log("[BlockchainConfig] Raw JSON loaded successfully.");

        // Deserialize simple fields
        ConfigData data = JsonUtility.FromJson<ConfigData>(json);
        RpcUrl          = data.rpcUrl;
        ChainId         = data.chainId;
        ContractAddress = data.contractAddress;
        PrivateKey      = data.privateKey;
        GasLimit        = data.gasLimit;

        // Extract the "abi" array as a raw JSON string for Nethereum
        int abiStart = json.IndexOf("\"abi\"", StringComparison.Ordinal);
        if (abiStart == -1)
        {
            Debug.LogError("[BlockchainConfig] 'abi' key not found in config.");
            return;
        }
        int bracketStart = json.IndexOf('[', abiStart);
        int depth = 0;
        int bracketEnd = bracketStart;
        for (int i = bracketStart; i < json.Length; i++)
        {
            if (json[i] == '[') depth++;
            else if (json[i] == ']') depth--;
            if (depth == 0) { bracketEnd = i; break; }
        }
        AbiJson = json.Substring(bracketStart, bracketEnd - bracketStart + 1);

        Debug.Log($"[BlockchainConfig] RPC URL       : {RpcUrl}");
        Debug.Log($"[BlockchainConfig] Chain ID       : {ChainId}");
        Debug.Log($"[BlockchainConfig] Contract Addr  : {ContractAddress}");
        Debug.Log($"[BlockchainConfig] Gas Limit      : {GasLimit}");
        Debug.Log($"[BlockchainConfig] ABI length     : {AbiJson.Length} chars");
    }
}
