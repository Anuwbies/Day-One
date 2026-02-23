using UnityEngine;
using TMPro;

public class BalanceDisplay : MonoBehaviour
{
    [Header("Drag your balance Text (TMP) here")]
    public TMP_Text balanceText;

    // Start runs after ALL Awake() calls, so BlockchainManager.Instance is safe
    void Start()
    {
        // Auto-refresh when wallet connects
        BlockchainManager.Instance.OnWalletConnected += RefreshBalance;
    }

    void OnDisable()
    {
        if (BlockchainManager.Instance != null)
            BlockchainManager.Instance.OnWalletConnected -= RefreshBalance;
    }

    // Call this after wallet connects and after every purchase
    public async void RefreshBalance()
    {
        if (BlockchainManager.Instance == null || !BlockchainManager.Instance.IsConnected)
        {
            Debug.LogWarning("[BalanceDisplay] Wallet not connected yet — skipping refresh.");
            return;
        }

        decimal bal = await SurvivalCoinService.Instance.GetBalance();
        balanceText.text = $"{bal:F2} SURV";
    }
}