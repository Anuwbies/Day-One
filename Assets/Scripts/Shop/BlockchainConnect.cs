using UnityEngine;
using Thirdweb;
using Thirdweb.Unity;
using System.Threading.Tasks;
using System.Numerics;
using System.Collections;

namespace Survival.Shop
{
    /// <summary>
    /// Central manager for blockchain contract interactions.
    /// Handles connecting to the deployed SimplePaymentGateway on Sepolia.
    /// </summary>
    public class BlockchainConnect : MonoBehaviour
    {
        public static BlockchainConnect Instance { get; private set; }

        [Header("Contract Settings")]
        [Tooltip("The address of your deployed SimplePaymentGateway contract.")]
        public string contractAddress;
        
        [Tooltip("Sepolia Ethereum chain ID is 11155111.")]
        public ulong chainId = 11155111;

        private ThirdwebContract _contract;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            transform.SetParent(null); // Ensure it is a root object for DontDestroyOnLoad
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            // Trigger initial connection log
            _ = GetContract();
        }

        /// <summary>
        /// Gets the contract instance, initializing it if necessary.
        /// </summary>
        public async Task<ThirdwebContract> GetContract()
        {
            if (_contract != null) return _contract;

            // Wait for ThirdwebManager to be ready and initialized
            while (ThirdwebManager.Instance == null || !ThirdwebManager.Instance.Initialized)
            {
                await Task.Delay(100);
            }

            Debug.Log($"[BlockchainConnect] Initializing contract at {contractAddress} on chain {chainId}");
            
            // Thirdweb handles the RPC and connection internally once the manager is set up
            _contract = await ThirdwebManager.Instance.GetContract(contractAddress, chainId);
            
            if (_contract != null)
            {
                Debug.Log($"<color=green>[BlockchainConnect] Contract Connected Successfully at {contractAddress}</color>");
            }

            return _contract;
        }
    }
}
