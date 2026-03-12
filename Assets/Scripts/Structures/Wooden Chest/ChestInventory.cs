using UnityEngine;

public class ChestInventory : PlayerInventory
{
    protected override void Awake()
    {
        addStartingItemsFromSession = false;
        base.Awake();
    }
}
