using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(menuName = "Survival/Item Data")]
public class ItemData : ScriptableObject
{
    public string itemName;
    public Sprite icon;

    [Header("World")]
    public GameObject worldPrefab;

    [Header("Stacking")]
    public bool stackable = true;
    public int maxStack = 64;
}
