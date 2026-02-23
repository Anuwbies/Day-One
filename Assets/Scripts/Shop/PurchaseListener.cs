using UnityEngine;
using TMPro;

public class PurchaseListener : MonoBehaviour
{
    [Header("Optional — drag a UI Text here to show error messages")]
    public TMP_Text feedbackText;

    // ── Subscribe when this object becomes active ─────────────
    void Start()
    {
        ShopBlockchain.Instance.OnPurchaseSuccess += HandlePurchaseSuccess;
        ShopBlockchain.Instance.OnPurchaseFailed += HandlePurchaseFailed;
    }

    // ── Unsubscribe when this object is disabled/destroyed ────
    void OnDisable()
    {
        ShopBlockchain.Instance.OnPurchaseSuccess -= HandlePurchaseSuccess;
        ShopBlockchain.Instance.OnPurchaseFailed -= HandlePurchaseFailed;
    }

    // ── Called automatically after a SUCCESSFUL purchase ──────
    void HandlePurchaseSuccess(int itemId, string itemName, decimal price)
    {
        Debug.Log($"[PurchaseListener] Bought {itemName} for {price} SURV!");

        // TODO: Add the item to your player inventory here
        // e.g. PlayerInventory.Instance.AddItem(itemId);

        if (feedbackText != null)
            feedbackText.text = $"Purchased {itemName}!";
    }

    // ── Called automatically after a FAILED purchase ──────────
    void HandlePurchaseFailed(int itemId, string errorMessage)
    {
        Debug.LogWarning($"[PurchaseListener] Purchase failed: {errorMessage}");

        if (feedbackText != null)
            feedbackText.text = $"Error: {errorMessage}";
    }
}