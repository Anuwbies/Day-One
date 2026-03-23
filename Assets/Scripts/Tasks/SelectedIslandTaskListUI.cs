using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class SelectedIslandTaskListUI : MonoBehaviour
{
    [Header("Source")]
    public IslandSelectionUI islandSelectionUI;
    public IslandData islandOverride;
    public bool useIslandSessionData = true;

    [Header("UI")]
    public GameObject panelRoot;
    public CanvasGroup panelCanvasGroup;
    public Transform taskParent;
    public GameObject taskItemPrefab;
    public TMP_Text taskCountText;

    [Header("Layout")]
    public List<Transform> footerItems = new List<Transform>();
    public bool preserveDirectChildButtons = true;

    [Header("Behaviour")]
    public bool rebuildOnEnable = true;
    public bool toggleContentOnPanelClick = true;

    private readonly List<GameObject> spawnedTaskItems = new List<GameObject>();
    private readonly List<bool> completionStates = new List<bool>();
    private readonly List<Transform> preservedFooterItems = new List<Transform>();
    private readonly List<Button> trackedButtons = new List<Button>();
    private IslandData currentIsland;
    private bool isTaskContentVisible = true;
    private GameObject clickTogglePanelRoot;
    private EventTrigger clickToggleTrigger;
    private EventTrigger.Entry clickToggleEntry;

    private void Reset()
    {
        taskParent = ResolveTaskParent();
        panelRoot = ResolvePanelRoot();
        panelCanvasGroup = ResolvePanelCanvasGroup();
    }

    private void OnEnable()
    {
        SubscribeToSelectionUI();
        EnsurePanelClickHandler();

        if (rebuildOnEnable)
        {
            RefreshTasks();
        }
    }

    private void Start()
    {
        if (!rebuildOnEnable)
        {
            RefreshTasks();
        }
    }

    private void OnDisable()
    {
        UnsubscribeFromSelectionUI();
        RemovePanelClickHandler();
    }

    [ContextMenu("Refresh Tasks")]
    public void RefreshTasks()
    {
        IslandData sourceIsland = GetSourceIsland();
        bool islandChanged = currentIsland != sourceIsland;
        BuildTaskList(sourceIsland, islandChanged);
    }

    public void SetIsland(IslandData island)
    {
        bool islandChanged = currentIsland != island;
        BuildTaskList(island, islandChanged);
    }

    public void SetTaskCompleted(int taskIndex, bool isCompleted)
    {
        if (currentIsland == null || currentIsland.objectives == null)
        {
            return;
        }

        if (taskIndex < 0 || taskIndex >= currentIsland.objectives.Count)
        {
            Debug.LogWarning($"{nameof(SelectedIslandTaskListUI)}: Task index {taskIndex} is out of range.");
            return;
        }

        EnsureCompletionStateCount(currentIsland.objectives.Count, false);
        completionStates[taskIndex] = isCompleted;

        if (taskIndex < spawnedTaskItems.Count && spawnedTaskItems[taskIndex] != null)
        {
            ApplyTaskData(spawnedTaskItems[taskIndex], currentIsland.objectives[taskIndex], isCompleted);
        }

        RefreshTaskStateUI();
    }

    private void HandleIslandSelected(IslandData island)
    {
        SetIsland(island);
    }

    private IslandData GetSourceIsland()
    {
        if (islandOverride != null)
        {
            return islandOverride;
        }

        if (islandSelectionUI != null && islandSelectionUI.CurrentSelectedIsland != null)
        {
            return islandSelectionUI.CurrentSelectedIsland;
        }

        return useIslandSessionData ? IslandSessionData.SelectedIsland : null;
    }

    private void BuildTaskList(IslandData island, bool resetCompletionStates)
    {
        currentIsland = island;
        taskParent = ResolveTaskParent();
        panelRoot = ResolvePanelRoot();
        EnsurePanelClickHandler();

        int taskCount = currentIsland != null && currentIsland.objectives != null ? currentIsland.objectives.Count : 0;

        UpdatePanelVisibility(taskCount > 0);

        ClearTaskItemsFromParent();

        if (taskCount == 0)
        {
            completionStates.Clear();
            RefreshTaskStateUI();
            isTaskContentVisible = false;
            UpdateTaskContentVisibility();
            EnsureFooterItemsAreLast();
            return;
        }

        isTaskContentVisible = true;
        EnsureCompletionStateCount(taskCount, resetCompletionStates);
        RefreshTaskStateUI();

        if (taskParent == null || taskItemPrefab == null)
        {
            return;
        }

        for (int i = 0; i < taskCount; i++)
        {
            GameObject taskInstance = Instantiate(taskItemPrefab, taskParent);
            taskInstance.name = $"Task_{i + 1}";
            ForceTaskItemActive(taskInstance);

            spawnedTaskItems.Add(taskInstance);
            ApplyTaskData(taskInstance, currentIsland.objectives[i], completionStates[i]);
        }

        UpdateTaskContentVisibility();
        EnsureFooterItemsAreLast();
    }

    private void ApplyTaskData(GameObject taskInstance, IslandObjective objective, bool isCompleted)
    {
        SelectedIslandTaskItemUI taskItemUI = taskInstance.GetComponent<SelectedIslandTaskItemUI>();
        if (taskItemUI != null)
        {
            taskItemUI.SetTask(objective, isCompleted);
            return;
        }

        TMP_Text taskText = taskInstance.GetComponentInChildren<TMP_Text>(true);
        if (taskText != null)
        {
            taskText.text = objective != null ? objective.objectiveTitle : string.Empty;
        }
    }

    private void EnsureCompletionStateCount(int taskCount, bool resetCompletionStates)
    {
        if (resetCompletionStates)
        {
            completionStates.Clear();
        }

        while (completionStates.Count < taskCount)
        {
            completionStates.Add(false);
        }

        if (completionStates.Count > taskCount)
        {
            completionStates.RemoveRange(taskCount, completionStates.Count - taskCount);
        }
    }

    private void ClearTaskItemsFromParent()
    {
        preservedFooterItems.Clear();

        if (taskParent == null)
        {
            return;
        }

        List<Transform> currentChildren = new List<Transform>();
        foreach (Transform child in taskParent)
        {
            currentChildren.Add(child);
        }

        foreach (Transform child in currentChildren)
        {
            if (child == null)
            {
                continue;
            }

            if (ShouldTreatAsFooter(child))
            {
                preservedFooterItems.Add(child);
                continue;
            }

            if (!IsTaskItemChild(child))
            {
                continue;
            }

            if (Application.isPlaying)
            {
                child.SetParent(null, false);
                Destroy(child.gameObject);
            }
            else
            {
                DestroyImmediate(child.gameObject);
            }
        }

        spawnedTaskItems.Clear();
        EnsureFooterItemsAreLast();
    }

    private void SubscribeToSelectionUI()
    {
        if (islandSelectionUI == null)
        {
            return;
        }

        islandSelectionUI.SelectedIslandChanged -= HandleIslandSelected;
        islandSelectionUI.SelectedIslandChanged += HandleIslandSelected;
    }

    private void UnsubscribeFromSelectionUI()
    {
        if (islandSelectionUI == null)
        {
            return;
        }

        islandSelectionUI.SelectedIslandChanged -= HandleIslandSelected;
    }

    private bool ShouldTreatAsFooter(Transform child)
    {
        if (child == null)
        {
            return false;
        }

        for (int i = 0; i < footerItems.Count; i++)
        {
            if (footerItems[i] == child)
            {
                return true;
            }
        }

        return preserveDirectChildButtons && child.GetComponent<Button>() != null;
    }

    private bool IsTaskItemChild(Transform child)
    {
        if (child == null)
        {
            return false;
        }

        if (child.GetComponent<SelectedIslandTaskItemUI>() != null)
        {
            return true;
        }

        return spawnedTaskItems.Contains(child.gameObject);
    }

    private void EnsureFooterItemsAreLast()
    {
        if (taskParent == null)
        {
            return;
        }

        for (int i = 0; i < preservedFooterItems.Count; i++)
        {
            Transform footerItem = preservedFooterItems[i];
            if (footerItem == null || footerItem.parent != taskParent)
            {
                continue;
            }

            footerItem.SetAsLastSibling();
        }
    }

    private void ForceTaskItemActive(GameObject taskInstance)
    {
        if (taskInstance == null)
        {
            return;
        }

        SetHierarchyActive(taskInstance.transform);

        Behaviour[] behaviours = taskInstance.GetComponentsInChildren<Behaviour>(true);
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] != null)
            {
                behaviours[i].enabled = true;
            }
        }

        TMP_Text[] textComponents = taskInstance.GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < textComponents.Length; i++)
        {
            if (textComponents[i] != null)
            {
                textComponents[i].enabled = true;
            }
        }
    }

    private void SetHierarchyActive(Transform root)
    {
        if (root == null)
        {
            return;
        }

        root.gameObject.SetActive(true);

        for (int i = 0; i < root.childCount; i++)
        {
            SetHierarchyActive(root.GetChild(i));
        }
    }

    private void UpdateTaskCountDisplay()
    {
        if (taskCountText == null)
        {
            return;
        }

        int totalTaskCount = currentIsland != null && currentIsland.objectives != null
            ? currentIsland.objectives.Count
            : completionStates.Count;
        int completedTaskCount = GetCompletedTaskCount(totalTaskCount);

        taskCountText.text = $"{completedTaskCount}/{Mathf.Max(0, totalTaskCount)}";
    }

    private void RefreshTaskStateUI()
    {
        UpdateTaskCountDisplay();
        UpdateTrackedButtons();
        UpdateButtonVisibilityState();
    }

    private int GetCompletedTaskCount(int totalTaskCount)
    {
        int completedTaskCount = 0;
        int countToCheck = Mathf.Min(Mathf.Max(0, totalTaskCount), completionStates.Count);

        for (int i = 0; i < countToCheck; i++)
        {
            if (completionStates[i])
            {
                completedTaskCount++;
            }
        }

        return completedTaskCount;
    }

    private void UpdateTrackedButtons()
    {
        trackedButtons.Clear();

        for (int i = 0; i < footerItems.Count; i++)
        {
            Transform footerItem = footerItems[i];
            if (footerItem == null)
            {
                continue;
            }

            RegisterButtons(footerItem.GetComponentsInChildren<Button>(true));
        }

        GameObject targetPanel = ResolvePanelRoot();
        if (targetPanel == null)
        {
            return;
        }

        RegisterButtons(targetPanel.GetComponentsInChildren<Button>(true));
    }

    private void RegisterButtons(Button[] buttons)
    {
        if (buttons == null)
        {
            return;
        }

        for (int i = 0; i < buttons.Length; i++)
        {
            Button button = buttons[i];
            if (button == null || trackedButtons.Contains(button))
            {
                continue;
            }

            trackedButtons.Add(button);
        }
    }

    private void UpdateButtonVisibilityState()
    {
        bool shouldShowButtons = isTaskContentVisible && AreAllTasksCompleted();

        for (int i = 0; i < trackedButtons.Count; i++)
        {
            if (trackedButtons[i] != null)
            {
                trackedButtons[i].gameObject.SetActive(shouldShowButtons);
            }
        }
    }

    private bool AreAllTasksCompleted()
    {
        int totalTaskCount = currentIsland != null && currentIsland.objectives != null
            ? currentIsland.objectives.Count
            : completionStates.Count;

        return totalTaskCount > 0 && GetCompletedTaskCount(totalTaskCount) >= totalTaskCount;
    }

    private void UpdatePanelVisibility(bool hasTasks)
    {
        CanvasGroup targetCanvasGroup = ResolvePanelCanvasGroup();
        if (targetCanvasGroup != null)
        {
            targetCanvasGroup.alpha = hasTasks ? 1f : 0f;
            targetCanvasGroup.interactable = hasTasks;
            targetCanvasGroup.blocksRaycasts = hasTasks;
            return;
        }

        GameObject targetPanel = ResolvePanelRoot();

        if (targetPanel != gameObject)
        {
            targetPanel.SetActive(hasTasks);
            return;
        }

        SetDirectChildrenActive(hasTasks);
    }

    public void ToggleTaskContentVisibility()
    {
        if (!toggleContentOnPanelClick)
        {
            return;
        }

        if (currentIsland == null || currentIsland.objectives == null || currentIsland.objectives.Count == 0)
        {
            return;
        }

        isTaskContentVisible = !isTaskContentVisible;
        UpdateTaskContentVisibility();
    }

    private void SetDirectChildrenActive(bool isActive)
    {
        for (int i = 0; i < transform.childCount; i++)
        {
            transform.GetChild(i).gameObject.SetActive(isActive);
        }
    }

    private void UpdateTaskContentVisibility()
    {
        if (taskParent != null && taskParent != transform)
        {
            taskParent.gameObject.SetActive(isTaskContentVisible);
        }

        GameObject targetPanel = ResolvePanelRoot();
        if (targetPanel == null)
        {
            return;
        }

        List<GameObject> toggledObjects = new List<GameObject>();

        for (int i = 0; i < footerItems.Count; i++)
        {
            Transform footerItem = footerItems[i];
            if (footerItem == null)
            {
                continue;
            }

            footerItem.gameObject.SetActive(isTaskContentVisible);
            toggledObjects.Add(footerItem.gameObject);
        }

        if (!preserveDirectChildButtons)
        {
            UpdateTrackedButtons();
            UpdateButtonVisibilityState();
            return;
        }

        for (int i = 0; i < targetPanel.transform.childCount; i++)
        {
            Transform child = targetPanel.transform.GetChild(i);
            if (child == null || child == taskParent)
            {
                continue;
            }

            if (child.GetComponent<Button>() == null)
            {
                continue;
            }

            if (toggledObjects.Contains(child.gameObject))
            {
                continue;
            }

            child.gameObject.SetActive(isTaskContentVisible);
        }

        UpdateTrackedButtons();
        UpdateButtonVisibilityState();
    }

    private GameObject ResolvePanelRoot()
    {
        if (panelRoot != null && panelRoot != gameObject)
        {
            return panelRoot;
        }

        Transform panelTransform = transform.Find("Panel");
        if (panelTransform != null)
        {
            return panelTransform.gameObject;
        }

        if (taskParent != null && taskParent != transform && taskParent.parent != null && taskParent.parent.gameObject != gameObject)
        {
            return taskParent.parent.gameObject;
        }

        if (transform.childCount > 0)
        {
            return transform.GetChild(0).gameObject;
        }

        if (panelRoot != null)
        {
            return panelRoot;
        }

        return gameObject;
    }

    private Transform ResolveTaskParent()
    {
        if (taskParent != null && taskParent != transform)
        {
            return taskParent;
        }

        Transform panelTransform = transform.Find("Panel");
        if (panelTransform != null)
        {
            Transform tasksTransform = panelTransform.Find("Tasks");
            if (tasksTransform != null)
            {
                return tasksTransform;
            }
        }

        return taskParent != null ? taskParent : transform;
    }

    private CanvasGroup ResolvePanelCanvasGroup()
    {
        GameObject targetPanel = ResolvePanelRoot();

        if (panelCanvasGroup != null && panelCanvasGroup.gameObject == targetPanel)
        {
            return panelCanvasGroup;
        }

        return targetPanel != null ? targetPanel.GetComponent<CanvasGroup>() : null;
    }

    private void EnsurePanelClickHandler()
    {
        RemovePanelClickHandler();

        if (!toggleContentOnPanelClick)
        {
            return;
        }

        GameObject targetPanel = ResolvePanelRoot();
        if (targetPanel == null || targetPanel == gameObject)
        {
            return;
        }

        clickTogglePanelRoot = targetPanel;
        clickToggleTrigger = targetPanel.GetComponent<EventTrigger>();
        if (clickToggleTrigger == null)
        {
            clickToggleTrigger = targetPanel.AddComponent<EventTrigger>();
        }

        if (clickToggleTrigger.triggers == null)
        {
            clickToggleTrigger.triggers = new List<EventTrigger.Entry>();
        }

        clickToggleEntry = new EventTrigger.Entry
        {
            eventID = EventTriggerType.PointerClick
        };
        clickToggleEntry.callback.AddListener(_ => ToggleTaskContentVisibility());
        clickToggleTrigger.triggers.Add(clickToggleEntry);
    }

    private void RemovePanelClickHandler()
    {
        if (clickToggleTrigger != null && clickToggleEntry != null)
        {
            clickToggleTrigger.triggers.Remove(clickToggleEntry);
        }

        clickToggleEntry = null;
        clickToggleTrigger = null;
        clickTogglePanelRoot = null;
    }
}
