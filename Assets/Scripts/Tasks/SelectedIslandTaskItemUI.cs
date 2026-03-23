using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class SelectedIslandTaskItemUI : MonoBehaviour
{
    [Header("References")]
    public TMP_Text taskText;
    public Image statusIcon;

    [Header("Status")]
    public Sprite completedSprite;
    public Sprite incompleteSprite;
    public Color completedColor = new Color(0.11f, 0.74f, 0f, 1f);
    public Color incompleteColor = new Color(0.84f, 0.28f, 0.22f, 1f);

    public void SetTask(IslandObjective objective, bool isCompleted)
    {
        SetTask(objective != null ? objective.objectiveTitle : string.Empty, isCompleted);
    }

    public void SetTask(string taskTitle, bool isCompleted)
    {
        if (taskText != null)
        {
            taskText.text = taskTitle;
        }

        if (statusIcon != null)
        {
            Sprite spriteToUse = isCompleted ? completedSprite : incompleteSprite;
            if (spriteToUse != null)
            {
                statusIcon.sprite = spriteToUse;
                statusIcon.color = isCompleted ? completedColor : incompleteColor;
                statusIcon.enabled = true;
            }
            else
            {
                statusIcon.enabled = false;
            }
        }
    }

    private void Reset()
    {
        AutoAssignReferences();
    }

    private void OnValidate()
    {
        AutoAssignReferences();
    }

    private void AutoAssignReferences()
    {
        if (taskText == null)
        {
            taskText = GetComponentInChildren<TMP_Text>(true);
        }

        if (statusIcon == null)
        {
            Transform iconTransform = transform.Find("Icon");
            if (iconTransform != null)
            {
                statusIcon = iconTransform.GetComponent<Image>();
            }
            else
            {
                statusIcon = GetComponentInChildren<Image>(true);
            }
        }

        if (completedSprite == null && statusIcon != null)
        {
            completedSprite = statusIcon.sprite;
        }
    }
}
