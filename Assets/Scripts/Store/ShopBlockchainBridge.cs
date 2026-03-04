using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Bridges the Shop UI with the blockchain layer.
/// Attach to the "Shop" GameObject (parent of Canvas).
///
/// INSPECTOR REFERENCES:
///   - shopPanel      → ShopPanel (the panel to show/hide)
///   - closeButton    → CloseShopButton
///   - contentParent  → Content (the layout group that holds ShopItemCards)
/// </summary>
public class ShopBlockchainBridge : MonoBehaviour
{
    [Header("Scene References")]
    [SerializeField] private GameObject shopPanel;
    [SerializeField] private Button     closeButton;
    [SerializeField] private Transform  contentParent;

    private BlockchainInteraction _blockchain;

    private void Start()
    {
        // Find the blockchain service in the scene
        _blockchain = FindObjectOfType<BlockchainInteraction>();
        if (_blockchain == null)
        {
            Debug.LogError("[ShopBridge] BlockchainInteraction not found! " +
                           "Make sure a BlockchainManager GameObject exists.");
            return;
        }

        // Wire the Close button to hide the shop panel
        if (closeButton != null)
            closeButton.onClick.AddListener(CloseShop);

        // Initialise every ShopItemCard that is already in the Content area
        InitialiseAllCards();

        Debug.Log("[ShopBridge] Shop blockchain bridge ready.");
    }

    // ---------- Public API ----------

    /// <summary>Call this from any script or button to open the shop.</summary>
    public void OpenShop()
    {
        shopPanel.SetActive(true);
        RefreshAllCards();
    }

    /// <summary>Called by CloseShopButton.</summary>
    public void CloseShop()
    {
        shopPanel.SetActive(false);
    }

    // ---------- Internal ----------

    /// <summary>
    /// Find all ShopItemCard components under the Content parent
    /// and give each one its blockchain reference.
    /// </summary>
    private void InitialiseAllCards()
    {
        if (contentParent == null) return;

        ShopItemCard[] cards = contentParent.GetComponentsInChildren<ShopItemCard>();
        foreach (ShopItemCard card in cards)
        {
            card.Initialise(_blockchain);
        }
        Debug.Log($"[ShopBridge] Initialised {cards.Length} shop item card(s).");
    }

    /// <summary>Tell every card to re-read its data from the blockchain.</summary>
    private void RefreshAllCards()
    {
        if (contentParent == null) return;

        ShopItemCard[] cards = contentParent.GetComponentsInChildren<ShopItemCard>();
        foreach (ShopItemCard card in cards)
        {
            card.RefreshFromBlockchain();
        }
    }
}
