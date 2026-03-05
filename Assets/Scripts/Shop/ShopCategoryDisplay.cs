using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Thirdweb;
using Thirdweb.Unity;
using System.Threading.Tasks;
using System.Numerics;

namespace Survival.Shop
{
    /// <summary>
    /// Handles the UI display for the Shop, generating category tabs and populating
    /// the product grid based on the IAPProductDatabase.
    /// </summary>
    public class ShopCategoryDisplay : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Drag your IAP Product Database asset here.")]
        public IAPProductDatabase productDatabase;

        [Tooltip("The template button that is a child of this object. It will be cloned for each category.")]
        public Button categoryButtonTemplate;

        [Tooltip("Optional: Where to spawn the buttons. If empty, they will spawn inside this GameObject.")]
        public Transform containerOverride;

        [Header("Product Display")]
        [Tooltip("The prefab for a single product entry (must match your specific hierarchy: Icon, Vertical/Description, etc).")]
        public GameObject productItemTemplate;
        [Tooltip("The container (e.g., Grid Layout Group) where product items will be spawned.")]
        public Transform productContainer;

        [Header("Selection Styling")]
        public Color normalColor = Color.white;
        public Color selectedColor = Color.yellow;

        private List<Button> _createdButtons = new List<Button>();
        private List<GameObject> _spawnedProducts = new List<GameObject>();

        private void Start()
        {
            if (productItemTemplate != null)
                productItemTemplate.SetActive(false);

            GenerateCategoryTabs();
        }

        public void GenerateCategoryTabs()
        {
            if (productDatabase == null) { Debug.LogError("ShopCategoryDisplay: Database is missing!"); return; }
            if (categoryButtonTemplate == null) { Debug.LogError("ShopCategoryDisplay: Button Template is missing!"); return; }

            Transform spawnParent = containerOverride != null ? containerOverride : this.transform;

            // Get all unique categories present in the database
            List<ProductType> activeTypes = productDatabase.allProducts
                .Select(p => p.productType)
                .Distinct()
                .OrderBy(t => t.ToString())
                .ToList();

            categoryButtonTemplate.gameObject.SetActive(false);
            _createdButtons.Clear();

            foreach (ProductType type in activeTypes)
            {
                CreateCategoryButton(type, spawnParent);
            }

            // Select the first category by default
            if (_createdButtons.Count > 0)
                _createdButtons[0].onClick.Invoke();
        }

        private void CreateCategoryButton(ProductType type, Transform parent)
        {
            Button newBtn = Instantiate(categoryButtonTemplate, parent);
            newBtn.gameObject.name = $"Tab_{type}";
            newBtn.gameObject.SetActive(true);
            _createdButtons.Add(newBtn);

            TMP_Text btnText = newBtn.GetComponentInChildren<TMP_Text>();
            if (btnText != null)
            {
                btnText.text = type.ToString();
                btnText.color = normalColor;
            }

            newBtn.onClick.AddListener(() => OnCategoryClicked(type, newBtn));
        }

        private void OnCategoryClicked(ProductType type, Button selectedButton)
        {
            UpdateSelectionVisuals(selectedButton);
            DisplayProducts(type);
        }

        private void UpdateSelectionVisuals(Button selectedButton)
        {
            foreach (Button btn in _createdButtons)
            {
                if (btn == null) continue;
                TMP_Text btnText = btn.GetComponentInChildren<TMP_Text>();
                if (btnText != null)
                    btnText.color = (btn == selectedButton) ? selectedColor : normalColor;
            }
        }

        private void Update()
        {
            // Update button alphas based on wallet connection status
            bool isConnected = ThirdwebManager.Instance != null && ThirdwebManager.Instance.ActiveWallet != null;
            foreach (GameObject productObj in _spawnedProducts)
            {
                if (productObj == null) continue;
                // Target path: Vertical/Price and Button/Button
                Transform verticalTr = productObj.transform.Find("Vertical");
                if (verticalTr != null)
                {
                    Transform priceBtnGroup = verticalTr.Find("Price and Button");
                    if (priceBtnGroup != null)
                    {
                        Transform btnTr = priceBtnGroup.Find("Button");
                        if (btnTr != null)
                        {
                            CanvasGroup cg = btnTr.GetComponent<CanvasGroup>();
                            if (cg != null)
                            {
                                cg.alpha = isConnected ? 1.0f : 0.5f;
                            }
                        }
                    }
                }
            }
        }

        private void DisplayProducts(ProductType type)
        {
            if (productItemTemplate == null || productContainer == null) return;

            // Clear old items
            foreach (GameObject obj in _spawnedProducts) Destroy(obj);
            _spawnedProducts.Clear();

            List<IAPProductData> filteredProducts = productDatabase.GetProductsByType(type);

            foreach (IAPProductData data in filteredProducts)
            {
                GameObject newItem = Instantiate(productItemTemplate, productContainer);
                newItem.name = $"Product_{data.displayName}";
                newItem.SetActive(true);
                _spawnedProducts.Add(newItem);

                // 1. Icon
                Transform iconTr = newItem.transform.Find("Icon");
                if (iconTr != null && iconTr.GetComponent<Image>() != null)
                    iconTr.GetComponent<Image>().sprite = data.displayIcon;

                Transform verticalTr = newItem.transform.Find("Vertical");
                if (verticalTr != null)
                {
                    // 2. Description
                    Transform descTr = verticalTr.Find("Description");
                    if (descTr != null && descTr.GetComponent<TMP_Text>() != null)
                        descTr.GetComponent<TMP_Text>().text = data.description;

                    Transform priceBtnGroup = verticalTr.Find("Price and Button");
                    if (priceBtnGroup != null)
                    {
                        // 3. Price
                        Transform priceContainer = priceBtnGroup.Find("Price");
                        if (priceContainer != null)
                        {
                            TMP_Text priceText = priceContainer.GetComponentInChildren<TMP_Text>();
                            if (priceText != null)
                            {
                                priceText.text = "Loading...";
                                UpdatePriceDisplay(data, priceText);
                            }
                        }

                        // 4. Button
                        Transform btnTr = priceBtnGroup.Find("Button");
                        if (btnTr != null && btnTr.GetComponent<Button>() != null)
                        {
                            Button buyBtn = btnTr.GetComponent<Button>();
                            buyBtn.onClick.RemoveAllListeners();
                            buyBtn.onClick.AddListener(() => OnPurchaseClicked(data, buyBtn));
                        }
                    }
                }
            }
        }

        private async void UpdatePriceDisplay(IAPProductData data, TMP_Text priceText)
        {
            if (priceText == null || BlockchainConnect.Instance == null) return;

            try
            {
                var contract = await BlockchainConnect.Instance.GetContract();
                if (contract == null) return;

                // getProductInfo(uint256 id) returns (uint256 price, bool isEnabled, string name)
                var result = await contract.Read<List<object>>("getProductInfo", data.contractProductId);

                if (result != null && result.Count > 0 && priceText != null)
                {
                    string rawPrice = result[0].ToString();
                    string ethPrice = Utils.ToEth(rawPrice, 4, true);
                    priceText.text = $"{ethPrice} ETH";
                }
            }
            catch (System.Exception ex)
            {
                if (priceText != null)
                {
                    Debug.LogWarning($"[Shop] Failed to fetch price for {data.displayName}: {ex.Message}");
                    priceText.text = "---";
                }
            }
        }

        private async void OnPurchaseClicked(IAPProductData data, Button buyBtn)
        {
            if (BlockchainConnect.Instance == null) return;

            // 1. Check if wallet is connected
            if (ThirdwebManager.Instance == null || ThirdwebManager.Instance.ActiveWallet == null)
            {
                Debug.LogWarning("[Shop] Wallet not connected! Please connect your wallet first.");
                return;
            }

            TMP_Text btnText = buyBtn.GetComponentInChildren<TMP_Text>();
            string originalText = btnText != null ? btnText.text : "BUY";
            float originalFontSize = btnText != null ? btnText.fontSize : 0;

            try
            {
                if (btnText != null)
                {
                    btnText.text = "Processing";
                    btnText.fontSize = 18;
                }
                buyBtn.interactable = false;

                Debug.Log($"[Shop] Starting purchase for: {data.displayName} (Contract Product ID: {data.contractProductId})");

                var contract = await BlockchainConnect.Instance.GetContract();
                if (contract == null) return;

                // 2. Fetch the latest price and status from the contract
                var info = await contract.Read<List<object>>("getProductInfo", data.contractProductId);
                if (info == null || info.Count < 2)
                {
                    Debug.LogError("[Shop] Could not fetch product info from contract.");
                    return;
                }

                string priceWeiString = info[0].ToString();
                BigInteger priceWei = BigInteger.Parse(priceWeiString);
                bool isEnabled = (bool)info[1];

                if (!isEnabled)
                {
                    Debug.LogWarning($"[Shop] Product '{data.displayName}' is currently disabled in the contract.");
                    return;
                }

                // 3. Prepare and Send Transaction
                Debug.Log($"[Shop] Sending transaction for {data.displayName} with value {priceWei} Wei...");
                
                var tx = await contract.Prepare(ThirdwebManager.Instance.ActiveWallet, "purchaseProduct", priceWei, data.contractProductId);
                string txHash = await ThirdwebTransaction.Send(tx);

                Debug.Log($"[Shop] Transaction broadcasted. Hash: {txHash}. Waiting for receipt...");
                
                // Wait for the transaction to be mined
                BigInteger chainId = new BigInteger(BlockchainConnect.Instance.chainId);
                await ThirdwebTransaction.WaitForTransactionReceipt(ThirdwebManager.Instance.Client, chainId, txHash);

                Debug.Log($"<color=green>[Shop] Purchase Successful! Transaction Hash: {txHash}</color>");

                // Refresh the wallet UI
                if (WalletConnect.Instance != null)
                {
                    WalletConnect.Instance.UpdateUI();
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[Shop] Purchase failed for {data.displayName}: {ex.Message}");
            }
            finally
            {
                if (btnText != null)
                {
                    btnText.text = originalText;
                    btnText.fontSize = originalFontSize;
                }
                buyBtn.interactable = true;
            }
        }
    }
}
