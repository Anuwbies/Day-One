using UnityEngine;
using System.Collections.Generic;

public enum ObjectiveType
{
    SurviveDays,
    CraftItem,   // Specifically the act of crafting
    PossessItem, // Having the item in the inventory
    SlayEnemy,
    Custom
}

[CreateAssetMenu(fileName = "New Island Data", menuName = "Survival/Island Data")]
public class IslandData : ScriptableObject
{
    [Header("Basic Info")]
    public string islandName;
    public string sceneName;
    public List<string> tags = new List<string>();
    public Sprite image;
    public bool isEndlessMode = false;

    [Header("Description")]
    [TextArea(3, 10)]
    public string description;

    [Header("Rewards")]
    public int diamondPrize = 0;
    
    [Header("Starting Gear")]
    public List<StartingItem> startingItems = new List<StartingItem>();

    [Header("Objectives")]
    public List<IslandObjective> objectives = new List<IslandObjective>();
}

[System.Serializable]
public class StartingItem
{
    public ItemData item;
    public int amount = 1;
}

[System.Serializable]
public class IslandObjective
{
    public string objectiveTitle;
    public ObjectiveType type;
    public bool isMainObjective;

    [Header("Target Values")]
    public int targetAmount; // Target day to reach, items to craft/possess, or enemies to slay
    public ItemData targetItem; // Used for CraftItem or GatherItem
    public GameObject enemyPrefab; // Prefab of enemy to slay
}
