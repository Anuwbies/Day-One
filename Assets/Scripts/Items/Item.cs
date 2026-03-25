using UnityEngine;

public class Item : MonoBehaviour
{
    public ItemData data;
    public int amount = 1;

    [SerializeField] private GameObject hintCanvas;

    private void Awake()
    {
        if (hintCanvas != null) hintCanvas.SetActive(false);
    }

    public void ToggleHint(bool show)
    {
        if (hintCanvas != null && hintCanvas.activeSelf != show)
        {
            hintCanvas.SetActive(show);
        }
    }
}
