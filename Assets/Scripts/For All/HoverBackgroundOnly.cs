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

    private bool isHovering;
    private float time;

    void Awake()
    {
        if (backgroundImage != null)
        {
            backgroundImage.enabled = false;
            backgroundImage.rectTransform.localRotation = Quaternion.identity;
        }
    }

    void Update()
    {
        if (!isHovering || backgroundImage == null)
            return;

        time += Time.unscaledDeltaTime;

        // Proper sinusoidal oscillation
        float angle =
            Mathf.Sin(time * oscillationSpeed * Mathf.PI * 2f) * maxRotation;

        backgroundImage.rectTransform.localRotation =
            Quaternion.Euler(0f, 0f, angle);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (backgroundImage == null)
            return;

        isHovering = true;
        time = 0f;
        backgroundImage.enabled = true;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (backgroundImage == null)
            return;

        isHovering = false;
        backgroundImage.enabled = false;
        backgroundImage.rectTransform.localRotation = Quaternion.identity;
    }
}
