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
    public bool canPlace = false;
    public bool canEat = false;
    public bool canDestroy = true;
    public bool canDrop = true;
    public bool canSplit = true;

    [Header("Consume Effects")]
    public int healthRestore = 0;
    public int hungerRestore = 0;
    public int thirstRestore = 0;
    public int energyRestore = 0;

    [Header("Damage Per Target")]
    [Tooltip("Bonus damage dealt to enemies")]
    public int damageToEnemy = 0;

    [Tooltip("Bonus damage dealt to trees")]
    public int damageToTree = 0;

    [Tooltip("Bonus damage dealt to rocks")]
    public int damageToRock = 0;
}