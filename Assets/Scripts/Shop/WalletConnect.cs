using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using Thirdweb.Unity;
using Thirdweb;
using System.Threading.Tasks;
using System.Numerics;
using System.Collections;
using System.Collections.Generic;
using WalletConnectUnity.Core;
using WalletConnectUnity.Modal;
using TMPro;

namespace Survival.Shop
{
    public class WalletConnect : MonoBehaviour
    {
        public static WalletConnect Instance { get; private set; }

        [Header("References")]
        [Tooltip("The UI Button that will trigger the ThirdwebManager display.")]
        public Button connectButton;

        [Tooltip("Text to display the connected wallet address.")]
        public TextMeshProUGUI addressText;

        [Tooltip("Text to display the connected wallet balance.")]
        public TextMeshProUGUI balanceText;

        [Tooltip("Drag the ThirdwebManager prefab here.")]
        public GameObject thirdwebManagerPrefab;

        [Header("Connection Settings")]
        [Tooltip("The Chain ID to connect to (e.g., 421614 for Arbitrum Sepolia).")]
        public ulong chainId = 421614;

        private bool _isConnecting = false;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private IEnumerator Start()
        {
            if (connectButton != null)
            {
                connectButton.onClick.AddListener(OnConnectButtonClicked);
            }

            ResetUI();

            yield return null;

            yield return StartCoroutine(AutoConnectRoutine());
        }

        private IEnumerator AutoConnectRoutine()
        {
            if (_isConnecting) yield break;

            ShowThirdwebManager();

            while (ThirdwebManager.Instance == null || !ThirdwebManager.Instance.Initialized)
            {
                yield return null;
            }

            if (ThirdwebManager.Instance.ActiveWallet != null)
            {
                UpdateUI();
                yield break;
            }

            // Wait for WalletConnect to be initialized by Thirdweb SDK
            while (WalletConnectUnity.Core.WalletConnect.Instance == null || !WalletConnectUnity.Core.WalletConnect.Instance.IsInitialized)
            {
                yield return null;
            }

            if (WalletConnectUnity.Core.WalletConnect.Instance.IsConnected)
            {
                Debug.Log("<color=orange>WalletConnect: Existing session found, attempting to re-connect...</color>");
                _isConnecting = true;
                var options = new WalletOptions(
                    provider: WalletProvider.WalletConnectWallet,
                    chainId: new BigInteger(chainId)
                );

                var connectTask = ThirdwebManager.Instance.ConnectWallet(options);
                while (!connectTask.IsCompleted) yield return null;

                _isConnecting = false;

                if (!connectTask.IsFaulted && ThirdwebManager.Instance.ActiveWallet != null)
                {
                    UpdateUI();
                }
                else
                {
                    Debug.LogWarning("WalletConnect: Failed to re-connect to existing session.");
                }
            }
        }

        private void ResetUI()
        {
            if (addressText != null) addressText.text = "Wallet is";
            if (balanceText != null) balanceText.text = "not connected";
            if (connectButton != null)
            {
                var btnText = connectButton.GetComponentInChildren<TextMeshProUGUI>();
                if (btnText != null) btnText.text = "Connect Wallet";
                connectButton.interactable = true;
            }
        }

        public void UpdateUI()
        {
            if (this == null || !gameObject.activeInHierarchy) return;
            StartCoroutine(UpdateUIRoutine());
        }

        private IEnumerator UpdateUIRoutine()
        {
            if (ThirdwebManager.Instance == null) yield break;

            var wallet = ThirdwebManager.Instance.ActiveWallet;
            if (wallet == null)
            {
                ResetUI();
                yield break;
            }

            var addressTask = wallet.GetAddress();
            while (!addressTask.IsCompleted) yield return null;
            
            if (addressTask.IsFaulted)
            {
                ResetUI();
                yield break;
            }

            string address = addressTask.Result;

            var balanceTask = wallet.GetBalance(chainId: new BigInteger(chainId));
            while (!balanceTask.IsCompleted) yield return null;
            
            string balanceEth = "0.0000";
            if (!balanceTask.IsFaulted)
            {
                var balance = balanceTask.Result;
                balanceEth = Utils.ToEth(wei: balance.ToString(), decimalsToDisplay: 4, addCommas: true);
            }

            if (addressText != null)
            {
                addressText.text = FormatAddress(address);
            }

            if (balanceText != null)
            {
                balanceText.text = $"{balanceEth} ETH";
            }

            if (connectButton != null)
            {
                var btnText = connectButton.GetComponentInChildren<TextMeshProUGUI>();
                if (btnText != null) btnText.text = "Disconnect Wallet";
                connectButton.interactable = true;
            }
        }

        private void OnConnectButtonClicked()
        {
            if (_isConnecting) return;

            if (ThirdwebManager.Instance != null && ThirdwebManager.Instance.ActiveWallet != null)
            {
                StartCoroutine(DisconnectFlowRoutine());
            }
            else
            {
                StartCoroutine(ConnectionFlowRoutine());
            }
        }

        private IEnumerator DisconnectFlowRoutine()
        {
            if (connectButton != null) connectButton.interactable = false;

            var wallet = ThirdwebManager.Instance.ActiveWallet;
            if (wallet != null)
            {
                Task disconnectTask = wallet.Disconnect();
                while (!disconnectTask.IsCompleted)
                {
                    yield return null;
                }
            }

            ThirdwebManager.Instance.SetActiveWallet(null);
            ResetUI();
            
            Debug.Log("<color=green>WalletConnect: Disconnected.</color>");
        }

        private IEnumerator ConnectionFlowRoutine()
        {
            if (Application.internetReachability == NetworkReachability.NotReachable)
            {
                Debug.LogWarning("WalletConnect: No internet connection.");
                yield break;
            }

            if (_isConnecting) yield break;
            _isConnecting = true;

            if (connectButton != null) connectButton.interactable = false;

            ShowThirdwebManager();

            while (ThirdwebManager.Instance == null || !ThirdwebManager.Instance.Initialized)
            {
                yield return null;
            }

            // Wait for WalletConnect to be initialized by Thirdweb SDK
            while (WalletConnectUnity.Core.WalletConnect.Instance == null || !WalletConnectUnity.Core.WalletConnect.Instance.IsInitialized)
            {
                yield return null;
            }

            while (!WalletConnectModal.IsReady)
            {
                yield return null;
            }

            var options = new WalletOptions(
                provider: WalletProvider.WalletConnectWallet,
                chainId: new BigInteger(chainId)
            );

            Task<IThirdwebWallet> connectTask = ThirdwebManager.Instance.ConnectWallet(options);
            while (!connectTask.IsCompleted)
            {
                yield return null;
            }

            _isConnecting = false;

            if (!connectTask.IsFaulted && connectTask.Result != null)
            {
                UpdateUI();
                Debug.Log("<color=cyan>WalletConnect: Wallet Connected Successfully.</color>");
            }
            else
            {
                if (connectButton != null) connectButton.interactable = true;
                Debug.LogError($"WalletConnect: Connection failed. {connectTask.Exception?.Message}");
            }
        }

        public void ShowThirdwebManager()
        {
            if (thirdwebManagerPrefab == null) return;

            ThirdwebManager existingInstance = FindFirstObjectByType<ThirdwebManager>();

            if (existingInstance == null)
            {
                GameObject newInstance = Instantiate(thirdwebManagerPrefab);
                newInstance.name = "ThirdwebManager";
                newInstance.transform.SetParent(null); // Ensure it is a root object for DontDestroyOnLoad
            }
            else
            {
                existingInstance.gameObject.SetActive(true);
            }
        }

        private string FormatAddress(string address)
        {
            if (string.IsNullOrEmpty(address) || address.Length < 10) return address;
            return address.Substring(0, 6) + "..." + address.Substring(address.Length - 4);
        }
    }
}
