using UnityEngine;
using UnityEngine.UI;
using Thirdweb.Unity;
using System.Threading.Tasks;
using System.Numerics;
using System.Collections;
using System.Collections.Generic;
using WalletConnectUnity.Core;
using WalletConnectUnity.Modal;

namespace Survival.Shop
{
    public class WalletConnect : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("The UI Button that will trigger the ThirdwebManager display.")]
        public Button connectButton;

        [Tooltip("Drag the ThirdwebManager prefab here.")]
        public GameObject thirdwebManagerPrefab;

        [Header("Connection Settings")]
        [Tooltip("The Chain ID to connect to (e.g., 421614 for Arbitrum Sepolia).")]
        public ulong chainId = 421614;

        private void Start()
        {
            if (connectButton != null)
            {
                connectButton.onClick.AddListener(OnConnectButtonClicked);
            }
            else
            {
                Debug.LogWarning("WalletConnect: Connect Button is not assigned in the inspector!");
            }
        }

        private void OnConnectButtonClicked()
        {
            StartCoroutine(ConnectionFlowRoutine());
        }

        private IEnumerator ConnectionFlowRoutine()
        {
            Debug.Log("<color=yellow>WalletConnect: Initializing Flow...</color>");

            // 1. Ensure ThirdwebManager is present
            ShowThirdwebManager();

            // 2. Wait for ThirdwebManager to be fully initialized
            float timeout = 10f;
            float timer = 0f;
            while (ThirdwebManager.Instance == null || !ThirdwebManager.Instance.Initialized)
            {
                timer += Time.deltaTime;
                if (timer > timeout)
                {
                    Debug.LogError("WalletConnect: TIMEOUT - ThirdwebManager did not initialize in time!");
                    yield break;
                }
                yield return null;
            }

            // 3. Ensure WalletConnect Core is initialized
            if (!WalletConnectUnity.Core.WalletConnect.Instance.IsInitialized)
            {
                Debug.Log("WalletConnect: Initializing WalletConnect Core...");
                var initTask = WalletConnectUnity.Core.WalletConnect.Instance.InitializeAsync();
                while (!initTask.IsCompleted)
                {
                    yield return null;
                }
                
                if (initTask.IsFaulted)
                {
                    Debug.LogError($"WalletConnect: Core Init failed - {initTask.Exception.InnerException?.Message}");
                    yield break;
                }
            }

            // 3.1. Ensure WalletConnect Modal is ready
            while (!WalletConnectModal.IsReady)
            {
                Debug.Log("WalletConnect: Waiting for WalletConnectModal to be ready...");
                yield return null;
            }

            Debug.Log("<color=green>WalletConnect: Backend Ready!</color>");

            // 4. Trigger the Connection Flow
            Task connectTask = ConnectToWallet();
            
            while (!connectTask.IsCompleted)
            {
                yield return null;
            }

            if (connectTask.IsFaulted)
            {
                Debug.LogError($"WalletConnect: Connection Task failed - {connectTask.Exception.InnerException?.Message}");
            }
        }

        public void ShowThirdwebManager()
        {
            if (thirdwebManagerPrefab == null)
            {
                Debug.LogError("WalletConnect: ThirdwebManager prefab is not assigned!");
                return;
            }

            ThirdwebManager existingInstance = FindFirstObjectByType<ThirdwebManager>();

            if (existingInstance == null)
            {
                GameObject newInstance = Instantiate(thirdwebManagerPrefab);
                newInstance.name = "ThirdwebManager";
                Debug.Log("WalletConnect: ThirdwebManager prefab instantiated.");
            }
            else
            {
                existingInstance.gameObject.SetActive(true);
                Debug.Log("WalletConnect: Existing ThirdwebManager found and activated.");
            }
        }

        private async Task ConnectToWallet()
        {
            try
            {
                Debug.Log($"WalletConnect: Requesting Modal for Chain {chainId}...");

                var options = new WalletOptions(
                    provider: WalletProvider.WalletConnectWallet,
                    chainId: new BigInteger(chainId)
                );

                // This triggers the Thirdweb UI flow
                var wallet = await ThirdwebManager.Instance.ConnectWallet(options);
                
                var address = await wallet.GetAddress();
                Debug.Log($"<color=cyan>WalletConnect: SUCCESS! Connected Address: {address}</color>");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"WalletConnect: Error during connection - {e.Message}");
                if (e.InnerException != null)
                {
                    Debug.LogError($"WalletConnect Inner Error: {e.InnerException.Message}");
                }
            }
        }
    }
}
