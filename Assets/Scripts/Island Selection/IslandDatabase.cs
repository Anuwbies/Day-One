using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "IslandDatabase", menuName = "Survival/Island Database")]
public class IslandDatabase : ScriptableObject
{
    public List<IslandData> islands = new List<IslandData>();

    public IslandData GetIslandByName(string name)
    {
        return islands.Find(i => i.islandName == name);
    }
}
