using System;
using UnityEngine;
using System.Collections.Generic;

// 1. Define your product categories here
public enum ProductType
{
    Currency,   // Diamonds, Gold, Gems
    Bundle,     // Starter packs, Value packs
    Skin,       // Costumes, Weapon skins
    Consumable, // Energy, Potions
    Special     // Battle Pass, Remove Ads
}

// 2. Define the source of the reward
public enum RewardType
{
    ItemData,   // Uses the Survival ItemData
    Diamonds,   // Uses the local DiamondCurrency
    Currency,   // Virtual currency (Diamonds, Coins)
    Skin,       // Unlockable content
    Other       // Badges, Remove Ads, etc.
}

[CreateAssetMenu(fileName = "NewProduct", menuName = "Shop/IAP Product")]
public class IAPProductData : ScriptableObject
{
    [Header("Blockchain Connection")]
    [Tooltip("This MUST match the 'productId' inside your Smart Contract.")]
    public int contractProductId;

    [Header("Shop Settings")]
    [Tooltip("Used for filtering tabs in the Shop UI")]
    public ProductType productType;

    [Header("Shop UI")]
    public string displayName;
    public Sprite displayIcon;
    [TextArea] public string description;

    [Header("Rewards")]
    [Tooltip("List of rewards. Can be physical items, currency, or skins.")]
    public List<ItemEntry> itemsToGive;

    [System.Serializable]
    public struct ItemEntry
    {
        [Tooltip("Is this an inventory item or virtual currency/skin?")]
        public RewardType type;

        [Header("Option A: Inventory Item")]
        [Tooltip("Assign this if Type is 'ItemData'")]
        public ItemData item;

        [Header("Option B: Custom Reward")]
        [Tooltip("ID used for Currency or Skins (e.g., 'Coins', 'Skin_Blue'). Ignored for ItemData and Diamonds.")]
        public string customId;

        [Tooltip("Visual icon for custom rewards (since they have no ItemData). You can also use this for Diamonds.")]
        public Sprite customIcon;

        [Header("Amount")]
        public int quantity;

        // Helper: Automatically gets the correct Icon based on type
        public Sprite GetIcon()
        {
            if (type == RewardType.ItemData && item != null)
            {
                return item.icon;
            }
            return customIcon;
        }

        // Helper: Automatically gets the correct Name/ID
        public string GetName()
        {
            if (type == RewardType.ItemData && item != null)
            {
                return item.itemName;
            }

            if (IsDiamondReward())
            {
                return "Diamonds";
            }

            return customId;
        }

        public bool IsDiamondReward()
        {
            if (type == RewardType.Diamonds)
            {
                return true;
            }

            return type == RewardType.Currency &&
                   !string.IsNullOrWhiteSpace(customId) &&
                   (string.Equals(customId, "Diamond", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(customId, "Diamonds", StringComparison.OrdinalIgnoreCase));
        }
    }
}
