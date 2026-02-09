using UnityEngine;
using System.Collections.Generic;
using System.Linq;

[CreateAssetMenu(fileName = "IAPProductDatabase", menuName = "Shop/Product Database")]
public class IAPProductDatabase : ScriptableObject
{
    [Header("Configuration")]
    [Tooltip("Drag all your created IAP Product assets here.")]
    public List<IAPProductData> allProducts;

    /// <summary>
    /// Finds a product by the ID defined in your Smart Contract.
    /// </summary>
    public IAPProductData GetProductById(int contractId)
    {
        return allProducts.FirstOrDefault(p => p.contractProductId == contractId);
    }

    /// <summary>
    /// Returns a list of products filtered by type (e.g., just Skins).
    /// Useful for populating different tabs in your Shop UI.
    /// </summary>
    public List<IAPProductData> GetProductsByType(ProductType type)
    {
        return allProducts.Where(p => p.productType == type).ToList();
    }

    /// <summary>
    /// Validates that there are no duplicate IDs in the database.
    /// Run this via Context Menu if you suspect setup errors.
    /// </summary>
    [ContextMenu("Check for Duplicates")]
    public void CheckForDuplicates()
    {
        var duplicates = allProducts.GroupBy(p => p.contractProductId)
                                    .Where(g => g.Count() > 1)
                                    .Select(g => g.Key);

        foreach (var id in duplicates)
        {
            Debug.LogError($"Duplicate Product ID detected: {id}. Please fix in the IAP Database.");
        }

        if (!duplicates.Any()) Debug.Log("Database check passed: No duplicate IDs found.");
    }
}