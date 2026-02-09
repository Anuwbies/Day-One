using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking; // Required for web requests
using TMPro;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Numerics; // Required for BigInteger

namespace Survival.Shop
{
    public class ShopCategoryDisplay : MonoBehaviour
    {
        [Header("Blockchain Config")]
        [Tooltip("Paste your deployed SimplePaymentGateway contract address here (0x...).")]
        public string contractAddress;
        [Tooltip("The RPC URL for Sepolia. You can use 'https://rpc.sepolia.org' or an Infura/Alchemy URL.")]
        public string rpcUrl = "https://rpc.sepolia.org";

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
            Debug.Log($"<color=cyan>Shop Category Selected:</color> {type}");
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

        private void DisplayProducts(ProductType type)
        {
            if (productItemTemplate == null || productContainer == null) return;

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
                                priceText.text = "..."; // Loading
                                StartCoroutine(FetchRealPrice(data.contractProductId, priceText));
                            }
                        }

                        // 4. Button
                        Transform btnTr = priceBtnGroup.Find("Button");
                        if (btnTr != null && btnTr.GetComponent<Button>() != null)
                        {
                            Button buyBtn = btnTr.GetComponent<Button>();
                            buyBtn.onClick.RemoveAllListeners();
                            buyBtn.onClick.AddListener(() => OnPurchaseClicked(data));
                        }
                    }
                }
            }
        }

        // =========================================================
        // REAL BLOCKCHAIN FETCHING (No SDK Required for Read-Only)
        // =========================================================

        private IEnumerator FetchRealPrice(int productId, TMP_Text targetText)
        {
            if (string.IsNullOrEmpty(contractAddress))
            {
                targetText.text = "No Contract";
                yield break;
            }

            // 1. Construct the payload
            // function selector for getProductInfo(uint256) is "0xbd02d0f5"
            // We pad the productId to 64 characters (32 bytes)
            string idHex = productId.ToString("X").PadLeft(64, '0');
            string data = "0xbd02d0f5" + idHex;

            string jsonPayload = $"{{\"jsonrpc\":\"2.0\",\"method\":\"eth_call\",\"params\":[{{\"to\":\"{contractAddress}\",\"data\":\"{data}\"}}, \"latest\"],\"id\":1}}";

            // 2. Send Request
            using (UnityWebRequest webRequest = new UnityWebRequest(rpcUrl, "POST"))
            {
                byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonPayload);
                webRequest.uploadHandler = new UploadHandlerRaw(bodyRaw);
                webRequest.downloadHandler = new DownloadHandlerBuffer();
                webRequest.SetRequestHeader("Content-Type", "application/json");

                yield return webRequest.SendWebRequest();

                if (webRequest.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError("RPC Error: " + webRequest.error);
                    targetText.text = "Err";
                }
                else
                {
                    // 3. Parse Response
                    string jsonResponse = webRequest.downloadHandler.text;

                    // Simple parse to find "result"
                    // Response format: {"jsonrpc":"2.0","id":1,"result":"0x0000..."}
                    int resultIndex = jsonResponse.IndexOf("\"result\":\"");
                    if (resultIndex != -1)
                    {
                        int start = resultIndex + 10;
                        int end = jsonResponse.IndexOf("\"", start);
                        string hexResult = jsonResponse.Substring(start, end - start);

                        if (hexResult.StartsWith("0x")) hexResult = hexResult.Substring(2);

                        if (hexResult.Length >= 64)
                        {
                            // The first 32 bytes (64 chars) is the Price
                            string priceHex = hexResult.Substring(0, 64);

                            // Parse BigInt
                            BigInteger priceWei = BigInteger.Parse("0" + priceHex, System.Globalization.NumberStyles.AllowHexSpecifier);

                            // Convert Wei to Eth (divide by 10^18)
                            double eth = (double)priceWei / 1000000000000000000d;

                            targetText.text = eth.ToString("0.####") + " ETH";
                        }
                        else
                        {
                            targetText.text = "Inv"; // Invalid product ID likely
                        }
                    }
                }
            }
        }

        private void OnPurchaseClicked(IAPProductData data)
        {
            Debug.Log($"Initiating purchase for: {data.displayName} (ID: {data.contractProductId})");
            // Connect this to your real SDK for the transaction part
        }
    }
}