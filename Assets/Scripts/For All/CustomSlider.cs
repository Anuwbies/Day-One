using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;

[RequireComponent(typeof(RectTransform))]
public class CustomSlider : MonoBehaviour,
    IPointerDownHandler, IDragHandler
{
    [Header("Range")]
    public float min = 0f;
    public float max = 100f;
    public float value = 50f;

    [Header("References")]
    public RectTransform track;
    public RectTransform fill;
    public RectTransform handle;
    public TMP_Text valueText;

    Canvas canvas;
    Camera uiCamera;

    float TrackWidth => track.rect.width;
    float HandleWidth => handle.rect.width;
    float UsableWidth => TrackWidth - HandleWidth;

    void Awake()
    {
        canvas = GetComponentInParent<Canvas>();
        uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay
            ? null
            : canvas.worldCamera;

        UpdateVisuals();
    }

    void UpdateVisuals()
    {
        float t = Mathf.InverseLerp(min, max, value);

        // Handle center moves only within usable width
        float x = t * UsableWidth;

        // Handle position (offset by half handle width)
        handle.anchoredPosition = new Vector2(x + HandleWidth * 0.5f, 0f);

        // Fill reaches center of handle
        fill.sizeDelta = new Vector2(x + HandleWidth * 0.5f, fill.sizeDelta.y);

        if (valueText != null)
            valueText.text = Mathf.RoundToInt(value).ToString();
    }

    void SetValueFromPointer(Vector2 screenPos)
    {
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
            track, screenPos, uiCamera, out Vector2 localPos))
            return;

        float halfTrack = TrackWidth * 0.5f;

        // Convert centered local position → left-based
        float x = localPos.x + halfTrack;

        // Clamp so handle edges never leave the track
        x = Mathf.Clamp(x - HandleWidth * 0.5f, 0f, UsableWidth);

        float t = x / UsableWidth;
        value = Mathf.Lerp(min, max, t);

        UpdateVisuals();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        SetValueFromPointer(eventData.position);
    }

    public void OnDrag(PointerEventData eventData)
    {
        SetValueFromPointer(eventData.position);
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        value = Mathf.Clamp(value, min, max);
        if (track != null && handle != null)
            UpdateVisuals();
    }
#endif
}
