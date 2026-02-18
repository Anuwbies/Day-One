using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class HoverBackgroundRotate : MonoBehaviour,
    IPointerEnterHandler,
    IPointerExitHandler
{
    [SerializeField] private Image backgroundImage;

    [Header("Rotation")]
    [SerializeField] private float maxRotation = 1f;        // degrees
    [SerializeField] private float oscillationSpeed = 1f;   // cycles per second

    [Header("Position Drift")]
    [SerializeField] private float maxOffset = 2f;          // pixels
    [SerializeField] private float offsetSpeed = 0.8f;      // cycles per second

    private bool isHovering;
    private float time;

    private Vector2 basePosition;
    private float xPhase;
    private float yPhase;

    void Awake()
    {
        if (backgroundImage == null)
            return;

        backgroundImage.enabled = false;

        RectTransform rt = backgroundImage.rectTransform;
        basePosition = rt.anchoredPosition;
        rt.localRotation = Quaternion.identity;
    }

    void Update()
    {
        if (!isHovering || backgroundImage == null)
            return;

        time += Time.unscaledDeltaTime;

        // Rotation (smooth)
        float angle =
            Mathf.Sin(time * oscillationSpeed * Mathf.PI * 2f) * maxRotation;

        // Position drift (organic)
        float offsetX =
            Mathf.Sin((time + xPhase) * offsetSpeed * Mathf.PI * 2f) * maxOffset;

        float offsetY =
            Mathf.Sin((time + yPhase) * offsetSpeed * Mathf.PI * 2f) * maxOffset;

        RectTransform rt = backgroundImage.rectTransform;

        rt.localRotation = Quaternion.Euler(0f, 0f, angle);
        rt.anchoredPosition = basePosition + new Vector2(offsetX, offsetY);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (backgroundImage == null)
            return;

        isHovering = true;

        // Randomize phases so it doesn't look synchronized
        time = Random.value;
        xPhase = Random.value * 2f;
        yPhase = Random.value * 2f;

        backgroundImage.enabled = true;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        ResetHover();
    }

    private void OnDisable()
    {
        ResetHover();
    }

    private void ResetHover()
    {
        if (backgroundImage == null)
            return;

        isHovering = false;

        RectTransform rt = backgroundImage.rectTransform;
        rt.localRotation = Quaternion.identity;
        rt.anchoredPosition = basePosition;

        backgroundImage.enabled = false;
    }
}
