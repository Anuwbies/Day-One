using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class IslandObjectiveProgressTracker : MonoBehaviour
{
    [Serializable]
    public class ObjectiveProgressState
    {
        public string objectiveTitle;
        public ObjectiveType type;
        public int currentAmount;
        public int requiredAmount;
        public bool isCompleted;
        public ItemData targetItem;
        public GameObject targetEnemyPrefab;

        public void ApplyObjective(IslandObjective objective)
        {
            objectiveTitle = objective != null ? objective.objectiveTitle : string.Empty;
            type = objective != null ? objective.type : ObjectiveType.Custom;
            requiredAmount = objective != null ? Mathf.Max(1, objective.targetAmount) : 0;
            targetItem = objective != null ? objective.targetItem : null;
            targetEnemyPrefab = objective != null ? objective.enemyPrefab : null;
            currentAmount = 0;
            isCompleted = false;
        }
    }

    public static IslandObjectiveProgressTracker Instance { get; private set; }

    [Header("Source")]
    public IslandData islandOverride;
    public bool useIslandSessionData = true;

    [Header("Scene References")]
    public DayNightCycleURP dayNightCycle;
    public PlayerInventory playerInventory;
    public SelectedIslandTaskListUI taskListUI;
    public string playerTag = "Player";

    [Header("Behaviour")]
    public bool initializeOnEnable = true;
    public bool autoFindSceneReferences = true;
    public bool syncCompletionToTaskList = true;

    [Header("Runtime")]
    [SerializeField] private List<ObjectiveProgressState> objectiveProgress = new List<ObjectiveProgressState>();

    [Header("Debug")]
    [SerializeField] private bool debugCompleteAll;

    private IslandData currentIsland;
    private bool hasInitialized;
    private bool needsTaskListSync;
    private SelectedIslandTaskListUI lastSyncedTaskListUI;
    private IslandData lastSyncedTaskListIsland;

    public event Action ProgressChanged;

    public IslandData CurrentIsland => currentIsland;
    public IReadOnlyList<ObjectiveProgressState> ObjectiveProgress => objectiveProgress;

    public int CompletedObjectiveCount
    {
        get
        {
            int completedCount = 0;

            for (int i = 0; i < objectiveProgress.Count; i++)
            {
                if (objectiveProgress[i] != null && objectiveProgress[i].isCompleted)
                {
                    completedCount++;
                }
            }

            return completedCount;
        }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning($"{nameof(IslandObjectiveProgressTracker)}: multiple instances found. The latest enabled instance will be used.");
        }

        Instance = this;
    }

    private void Reset()
    {
        TryResolveSceneReferences(true);
    }

    private void OnEnable()
    {
        SubscribeToGameplayEvents();

        if (initializeOnEnable && !hasInitialized)
        {
            InitializeTracker();
            return;
        }

        TryResolveSceneReferences(false);
        RefreshAllProgress();
    }

    private void Start()
    {
        if (!initializeOnEnable && !hasInitialized)
        {
            InitializeTracker();
        }
    }

    private void Update()
    {
        if (debugCompleteAll)
        {
            debugCompleteAll = false;
            DebugCompleteAllObjectives();
        }

        bool referencesChanged = TryResolveSceneReferences(false);

        if (GetSourceIsland() != currentIsland)
        {
            RebuildAndRefresh(true);
        }
        else if (referencesChanged)
        {
            bool progressChanged = RefreshLiveProgress();
            if (progressChanged)
            {
                NotifyProgressChanged();
            }
        }

        SyncTaskListCompletionStatesIfNeeded();
    }

    private void OnDisable()
    {
        UnsubscribeFromGameplayEvents();

        if (playerInventory != null)
        {
            playerInventory.OnInventoryChanged -= HandleInventoryChanged;
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    [ContextMenu("Initialize Tracker")]
    public void InitializeTracker()
    {
        hasInitialized = true;
        TryResolveSceneReferences(true);
        RebuildAndRefresh(true);
    }

    [ContextMenu("Refresh Objective Progress")]
    public void RefreshAllProgress()
    {
        RebuildAndRefresh(false);
    }

    [ContextMenu("Reset Objective Progress")]
    public void ResetProgress()
    {
        RebuildAndRefresh(true);
    }

    [ContextMenu("DEBUG: Complete All Objectives")]
    public void DebugCompleteAllObjectives()
    {
        for (int i = 0; i < objectiveProgress.Count; i++)
        {
            ObjectiveProgressState state = objectiveProgress[i];
            state.currentAmount = state.requiredAmount;
            state.isCompleted = true;
        }
        NotifyProgressChanged();
        Debug.Log("DEBUG: All objectives marked as completed.");
    }

    public int GetObjectiveProgress(int objectiveIndex)
    {
        return IsValidObjectiveIndex(objectiveIndex) ? objectiveProgress[objectiveIndex].currentAmount : 0;
    }

    public int GetObjectiveRequiredAmount(int objectiveIndex)
    {
        return IsValidObjectiveIndex(objectiveIndex) ? objectiveProgress[objectiveIndex].requiredAmount : 0;
    }

    public bool IsObjectiveCompleted(int objectiveIndex)
    {
        return IsValidObjectiveIndex(objectiveIndex) && objectiveProgress[objectiveIndex].isCompleted;
    }

    public void SetObjectiveProgress(int objectiveIndex, int progressAmount)
    {
        if (!IsValidObjectiveIndex(objectiveIndex))
        {
            return;
        }

        if (SetProgressValue(objectiveIndex, progressAmount))
        {
            NotifyProgressChanged();
        }
    }

    public void AddObjectiveProgress(int objectiveIndex, int amountToAdd)
    {
        if (!IsValidObjectiveIndex(objectiveIndex))
        {
            return;
        }

        if (AddProgressValue(objectiveIndex, amountToAdd))
        {
            NotifyProgressChanged();
        }
    }

    private void RebuildAndRefresh(bool forceRebuild)
    {
        bool referencesChanged = TryResolveSceneReferences(false);
        bool rebuilt = RefreshTrackedIsland(forceRebuild);
        bool progressChanged = RefreshLiveProgress();

        if (referencesChanged || rebuilt || progressChanged)
        {
            NotifyProgressChanged();
        }
    }

    private bool RefreshTrackedIsland(bool forceRebuild)
    {
        IslandData sourceIsland = GetSourceIsland();
        int sourceObjectiveCount = GetObjectiveCount(sourceIsland);

        if (!forceRebuild && currentIsland == sourceIsland && objectiveProgress.Count == sourceObjectiveCount)
        {
            return false;
        }

        currentIsland = sourceIsland;
        objectiveProgress.Clear();
        lastSyncedTaskListIsland = null;
        needsTaskListSync = true;

        if (currentIsland == null || currentIsland.objectives == null)
        {
            return true;
        }

        for (int i = 0; i < currentIsland.objectives.Count; i++)
        {
            ObjectiveProgressState progressState = new ObjectiveProgressState();
            progressState.ApplyObjective(currentIsland.objectives[i]);
            objectiveProgress.Add(progressState);
        }

        return true;
    }

    private bool RefreshLiveProgress()
    {
        bool progressChanged = false;

        progressChanged |= RefreshSurviveDaysProgress();
        progressChanged |= RefreshPossessItemProgress();

        return progressChanged;
    }

    private bool RefreshSurviveDaysProgress()
    {
        if (dayNightCycle == null)
        {
            return false;
        }

        int currentDay = Mathf.Max(0, dayNightCycle.CurrentDay);
        bool progressChanged = false;

        for (int i = 0; i < objectiveProgress.Count; i++)
        {
            if (objectiveProgress[i].type != ObjectiveType.SurviveDays)
            {
                continue;
            }

            progressChanged |= SetProgressValue(i, currentDay);
        }

        return progressChanged;
    }

    private bool RefreshPossessItemProgress()
    {
        if (playerInventory == null)
        {
            return false;
        }

        bool progressChanged = false;

        for (int i = 0; i < objectiveProgress.Count; i++)
        {
            if (objectiveProgress[i].type != ObjectiveType.PossessItem)
            {
                continue;
            }

            int possessedAmount = CountInventoryAmount(objectiveProgress[i].targetItem);
            progressChanged |= SetProgressValue(i, possessedAmount);
        }

        return progressChanged;
    }

    private void HandleDayChanged(int currentDay)
    {
        if (RefreshSurviveDaysProgress())
        {
            NotifyProgressChanged();
        }
    }

    private void HandleInventoryChanged()
    {
        if (RefreshPossessItemProgress())
        {
            NotifyProgressChanged();
        }
    }

    private void HandleItemCrafted(ItemData craftedItem, int craftedAmount)
    {
        bool progressChanged = false;

        for (int i = 0; i < objectiveProgress.Count; i++)
        {
            if (objectiveProgress[i].type != ObjectiveType.CraftItem)
            {
                continue;
            }

            if (!MatchesCraftObjective(objectiveProgress[i], craftedItem))
            {
                continue;
            }

            progressChanged |= AddProgressValue(i, craftedAmount);
        }

        if (progressChanged)
        {
            NotifyProgressChanged();
        }
    }

    private void HandleEnemyDied(EnemyHealth enemyHealth)
    {
        if (enemyHealth == null)
        {
            return;
        }

        bool progressChanged = false;

        for (int i = 0; i < objectiveProgress.Count; i++)
        {
            if (objectiveProgress[i].type != ObjectiveType.SlayEnemy)
            {
                continue;
            }

            if (!MatchesEnemyObjective(objectiveProgress[i], enemyHealth))
            {
                continue;
            }

            progressChanged |= AddProgressValue(i, 1);
        }

        if (progressChanged)
        {
            NotifyProgressChanged();
        }
    }

    private bool TryResolveSceneReferences(bool forceRefresh)
    {
        bool referencesChanged = false;

        if ((forceRefresh || dayNightCycle == null) && (autoFindSceneReferences || forceRefresh))
        {
            DayNightCycleURP foundDayNightCycle = FindFirstObjectByType<DayNightCycleURP>();
            if (foundDayNightCycle != null && foundDayNightCycle != dayNightCycle)
            {
                dayNightCycle = foundDayNightCycle;
                referencesChanged = true;
            }
        }

        PlayerInventory inventoryToBind = playerInventory;
        if ((forceRefresh || inventoryToBind == null || !IsLikelyPlayerInventory(inventoryToBind)) &&
            (autoFindSceneReferences || forceRefresh))
        {
            PlayerInventory foundInventory = ResolvePreferredPlayerInventory();
            if (foundInventory != null)
            {
                inventoryToBind = foundInventory;
            }
        }

        if (BindPlayerInventory(inventoryToBind))
        {
            referencesChanged = true;
        }

        if (syncCompletionToTaskList && (forceRefresh || taskListUI == null) && (autoFindSceneReferences || forceRefresh))
        {
            SelectedIslandTaskListUI foundTaskListUI = FindFirstObjectByType<SelectedIslandTaskListUI>();
            if (foundTaskListUI != null && foundTaskListUI != taskListUI)
            {
                taskListUI = foundTaskListUI;
                lastSyncedTaskListUI = null;
                lastSyncedTaskListIsland = null;
                needsTaskListSync = true;
                referencesChanged = true;
            }
        }

        return referencesChanged;
    }

    private bool BindPlayerInventory(PlayerInventory inventory)
    {
        if (playerInventory == inventory)
        {
            if (playerInventory != null)
            {
                playerInventory.OnInventoryChanged -= HandleInventoryChanged;
                playerInventory.OnInventoryChanged += HandleInventoryChanged;
            }

            return false;
        }

        if (playerInventory != null)
        {
            playerInventory.OnInventoryChanged -= HandleInventoryChanged;
        }

        playerInventory = inventory;

        if (playerInventory != null)
        {
            playerInventory.OnInventoryChanged -= HandleInventoryChanged;
            playerInventory.OnInventoryChanged += HandleInventoryChanged;
        }

        return true;
    }

    private PlayerInventory ResolvePreferredPlayerInventory()
    {
        PlayerInventory[] inventories =
            FindObjectsByType<PlayerInventory>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        PlayerInventory nonChestFallback = null;

        for (int i = 0; i < inventories.Length; i++)
        {
            PlayerInventory inventory = inventories[i];
            if (inventory == null)
            {
                continue;
            }

            if (nonChestFallback == null && !(inventory is ChestInventory))
            {
                nonChestFallback = inventory;
            }

            if (IsLikelyPlayerInventory(inventory))
            {
                return inventory;
            }
        }

        if (nonChestFallback != null)
        {
            return nonChestFallback;
        }

        return FindFirstObjectByType<PlayerInventory>();
    }

    private bool IsLikelyPlayerInventory(PlayerInventory inventory)
    {
        if (inventory == null || inventory is ChestInventory)
        {
            return false;
        }

        return HasTagInHierarchy(inventory.transform, playerTag);
    }

    private bool HasTagInHierarchy(Transform target, string tagToMatch)
    {
        if (target == null || string.IsNullOrWhiteSpace(tagToMatch))
        {
            return false;
        }

        Transform current = target;
        while (current != null)
        {
            if (string.Equals(current.gameObject.tag, tagToMatch, StringComparison.Ordinal))
            {
                return true;
            }

            current = current.parent;
        }

        return false;
    }

    private void SubscribeToGameplayEvents()
    {
        DayNightCycleURP.AnyDayChanged -= HandleDayChanged;
        DayNightCycleURP.AnyDayChanged += HandleDayChanged;

        CraftButtonConsume.AnyItemCrafted -= HandleItemCrafted;
        CraftButtonConsume.AnyItemCrafted += HandleItemCrafted;

        EnemyHealth.AnyEnemyDied -= HandleEnemyDied;
        EnemyHealth.AnyEnemyDied += HandleEnemyDied;
    }

    private void UnsubscribeFromGameplayEvents()
    {
        DayNightCycleURP.AnyDayChanged -= HandleDayChanged;
        CraftButtonConsume.AnyItemCrafted -= HandleItemCrafted;
        EnemyHealth.AnyEnemyDied -= HandleEnemyDied;
    }

    private void NotifyProgressChanged()
    {
        needsTaskListSync = true;
        SyncTaskListCompletionStatesIfNeeded();
        ProgressChanged?.Invoke();
    }

    private void SyncTaskListCompletionStatesIfNeeded()
    {
        if (!needsTaskListSync || !syncCompletionToTaskList)
        {
            return;
        }

        if (taskListUI == null)
        {
            return;
        }

        if (taskListUI != lastSyncedTaskListUI || currentIsland != lastSyncedTaskListIsland)
        {
            taskListUI.SetIsland(currentIsland);
            lastSyncedTaskListUI = taskListUI;
            lastSyncedTaskListIsland = currentIsland;
        }

        for (int i = 0; i < objectiveProgress.Count; i++)
        {
            taskListUI.SetTaskCompleted(i, objectiveProgress[i].isCompleted);
        }

        needsTaskListSync = false;
    }

    private bool SetProgressValue(int objectiveIndex, int progressAmount)
    {
        if (!IsValidObjectiveIndex(objectiveIndex))
        {
            return false;
        }

        ObjectiveProgressState progressState = objectiveProgress[objectiveIndex];
        int clampedAmount = Mathf.Clamp(progressAmount, 0, Mathf.Max(0, progressState.requiredAmount));
        bool isCompleted = progressState.requiredAmount > 0 && clampedAmount >= progressState.requiredAmount;

        if (progressState.currentAmount == clampedAmount && progressState.isCompleted == isCompleted)
        {
            return false;
        }

        progressState.currentAmount = clampedAmount;
        progressState.isCompleted = isCompleted;
        return true;
    }

    private bool AddProgressValue(int objectiveIndex, int amountToAdd)
    {
        if (!IsValidObjectiveIndex(objectiveIndex))
        {
            return false;
        }

        return SetProgressValue(objectiveIndex, objectiveProgress[objectiveIndex].currentAmount + Mathf.Max(0, amountToAdd));
    }

    private bool MatchesCraftObjective(ObjectiveProgressState progressState, ItemData craftedItem)
    {
        return progressState.targetItem == null || progressState.targetItem == craftedItem;
    }

    private bool MatchesEnemyObjective(ObjectiveProgressState progressState, EnemyHealth enemyHealth)
    {
        if (progressState.targetEnemyPrefab == null)
        {
            return true;
        }

        string expectedName = CleanObjectName(progressState.targetEnemyPrefab.name);
        string enemyName = CleanObjectName(enemyHealth.gameObject.name);

        // Runtime instances usually keep the prefab name plus "(Clone)".
        return string.Equals(expectedName, enemyName, StringComparison.Ordinal);
    }

    private int CountInventoryAmount(ItemData targetItem)
    {
        if (playerInventory == null || playerInventory.items == null)
        {
            return 0;
        }

        if (targetItem == null)
        {
            return 0;
        }

        int totalAmount = 0;

        for (int i = 0; i < playerInventory.items.Count; i++)
        {
            InventorySlot slot = playerInventory.items[i];
            if (slot == null || slot.item != targetItem)
            {
                continue;
            }

            totalAmount += slot.amount;
        }

        return totalAmount;
    }

    private IslandData GetSourceIsland()
    {
        if (islandOverride != null)
        {
            return islandOverride;
        }

        return useIslandSessionData ? IslandSessionData.SelectedIsland : null;
    }

    private int GetObjectiveCount(IslandData island)
    {
        return island != null && island.objectives != null ? island.objectives.Count : 0;
    }

    private bool IsValidObjectiveIndex(int objectiveIndex)
    {
        return objectiveIndex >= 0 && objectiveIndex < objectiveProgress.Count;
    }

    private string CleanObjectName(string objectName)
    {
        if (string.IsNullOrEmpty(objectName))
        {
            return string.Empty;
        }

        const string cloneSuffix = "(Clone)";
        return objectName.EndsWith(cloneSuffix, StringComparison.Ordinal)
            ? objectName.Substring(0, objectName.Length - cloneSuffix.Length)
            : objectName;
    }
}
