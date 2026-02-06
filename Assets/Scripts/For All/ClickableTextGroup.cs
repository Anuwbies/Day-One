using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections.Generic;

public class ClickableTextGroup : MonoBehaviour
{
    [Header("Colors")]
    public Color normalColor = Color.white;
    public Color selectedColor = Color.yellow;

    [Header("Default Selection (0-based index)")]
    [Tooltip("0 = first child, 1 = second, 2 = third")]
    public int defaultSelectedIndex = 0;

    TMP_Text[] texts;

    void Awake()
    {
        texts = GetComponentsInChildren<TMP_Text>();

        // Safety clamp
        defaultSelectedIndex = Mathf.Clamp(defaultSelectedIndex, 0, texts.Length - 1);

        ApplySelection(texts[defaultSelectedIndex]);
    }

    void Update()
    {
        if (!Input.GetMouseButtonDown(0))
            return;

        PointerEventData eventData = new PointerEventData(EventSystem.current)
        {
            position = Input.mousePosition
        };

        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, results);

        foreach (var r in results)
        {
            TMP_Text clickedText = r.gameObject.GetComponent<TMP_Text>();
            if (clickedText != null && clickedText.transform.IsChildOf(transform))
            {
                ApplySelection(clickedText);
                break;
            }
        }
    }

    void ApplySelection(TMP_Text selected)
    {
        foreach (var t in texts)
            t.color = (t == selected) ? selectedColor : normalColor;
    }
}
