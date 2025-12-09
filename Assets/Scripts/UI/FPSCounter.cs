using UnityEngine;
using TMPro;

public class FPSCounter : MonoBehaviour
{
    public TextMeshProUGUI fpsText;

    private float timer;

    void Update()
    {
        timer += Time.deltaTime;

        if (timer >= 0.2f) // Update 5 times per second
        {
            int fps = (int)(1f / Time.deltaTime);
            fpsText.text = fps + " FPS";
            timer = 0f;
        }
    }
}
