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
            while (!WalletConnectUnity.Core.WalletConnect.Instance.IsInitialized)
            {
                yield return null;
            }

            if (WalletConnectUnity.Core.WalletConnect.Instance.IsConnected)
            {
                var options = new WalletOptions(
                    provider: WalletProvider.WalletConnectWallet,
                    chainId: new BigInteger(chainId)
                );

                var connectTask = ThirdwebManager.Instance.ConnectWallet(options);
                while (!connectTask.IsCompleted) yield return null;

                if (!connectTask.IsFaulted)
                {
                    UpdateUI();
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
            }
        }

        private void UpdateUI()
        {
            StartCoroutine(UpdateUIRoutine());
        }

        private IEnumerator UpdateUIRoutine()
        {
            var wallet = ThirdwebManager.Instance.ActiveWallet;
            if (wallet == null)
            {
                ResetUI();
                yield break;
            }

            var addressTask = wallet.GetAddress();
            while (!addressTask.IsCompleted) yield return null;
            string address = addressTask.Result;

            var balanceTask = wallet.GetBalance(chainId: new BigInteger(chainId));
            while (!balanceTask.IsCompleted) yield return null;
            var balance = balanceTask.Result;

            if (addressText != null)
            {
                addressText.text = FormatAddress(address);
            }

            if (balanceText != null)
            {
                string balanceEth = Utils.ToEth(wei: balance.ToString(), decimalsToDisplay: 4, addCommas: true);
                balanceText.text = $"{balanceEth} ETH";
            }

            if (connectButton != null)
            {
                var btnText = connectButton.GetComponentInChildren<TextMeshProUGUI>();
                if (btnText != null) btnText.text = "Disconnect Wallet";
            }
        }

        private void OnConnectButtonClicked()
        {
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
                yield break;
            }

            ShowThirdwebManager();

            while (ThirdwebManager.Instance == null || !ThirdwebManager.Instance.Initialized)
            {
                yield return null;
            }

            // Wait for WalletConnect to be initialized by Thirdweb SDK
            while (!WalletConnectUnity.Core.WalletConnect.Instance.IsInitialized)
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

            Task connectTask = ThirdwebManager.Instance.ConnectWallet(options);
            while (!connectTask.IsCompleted)
            {
                yield return null;
            }

            if (!connectTask.IsFaulted)
            {
                UpdateUI();
                Debug.Log("<color=cyan>WalletConnect: Wallet Connected Successfully.</color>");
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
