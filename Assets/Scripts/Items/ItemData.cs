using UnityEngine;

[CreateAssetMenu(menuName = "Survival/Item Data")]
public class ItemData : ScriptableObject
{
    public string itemName;
    public Sprite icon;

    [Header("World")]
    public GameObject worldPrefab;   // ACTUAL prefab with correct collider

    public bool stackable = true;
    public int maxStack = 64;
}
