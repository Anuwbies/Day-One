using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Thirdweb;
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
                            buyBtn.onClick.AddListener(() => OnPurchaseClicked(data));
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

        private void OnPurchaseClicked(IAPProductData data)
        {
            Debug.Log($"[Shop] Product Clicked: {data.displayName}. Implement purchase logic elsewhere.");
        }
    }
}
