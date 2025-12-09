using UnityEngine;

[CreateAssetMenu(menuName = "Survival/Item Data")]
public class ItemData : ScriptableObject
{
    public string itemName;
    public Sprite icon;
    public bool stackable = true;
    public int maxStack = 64;
}
