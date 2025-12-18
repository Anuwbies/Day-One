using UnityEngine;

[CreateAssetMenu(menuName = "Survival/Item Data")]
public class ItemData : ScriptableObject
{
    [Header("Basic Info")]
    public string itemName;
    public Sprite icon;

    [Header("World")]
    public GameObject worldPrefab;

    [Header("Stacking")]
    public bool stackable = true;
    public int maxStack = 64;

    [Header("Actions")]
    public bool canEat = false;
    public bool canDestroy = true;
    public bool canDrop = true;
    public bool canSplit = true;

    [Header("Consume Effects")]
    [Tooltip("Health restored when eaten")]
    public int healthRestore = 0;

    [Tooltip("Hunger restored when eaten")]
    public int hungerRestore = 0;

    [Tooltip("Thirst restored when eaten")]
    public int thirstRestore = 0;

    [Tooltip("Energy restored when eaten")]
    public int energyRestore = 0;
}
