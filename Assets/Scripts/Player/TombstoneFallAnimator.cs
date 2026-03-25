using System;
using System.Collections.Generic;
using UnityEngine;

public class TombstoneFallAnimator : MonoBehaviour
{
    [SerializeField] private AnimationCurve fallCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    private Vector3 startPosition;
    private Vector3 landingPosition;
    private float fallDuration = 0.35f;
    private float elapsedTime;
    private float canvasEnableDelay;
    private float canvasEnableElapsedTime;
    private float panelScaleDuration = 0.2f;
    private float panelScaleElapsedTime;
    private bool isFalling;
    private bool isWaitingToEnableCanvas;
    private bool isScalingPanels;
    private Canvas[] tombstoneCanvases = System.Array.Empty<Canvas>();
    private RectTransform[] tombstonePanels = Array.Empty<RectTransform>();
    private Vector3[] tombstonePanelOriginalScales = Array.Empty<Vector3>();

    public void BeginFall(
        Vector3 targetPosition,
        float height,
        float duration,
        float delayBeforeCanvasEnable,
        float panelScaleInDuration
    )
    {
        landingPosition = targetPosition;
        startPosition = targetPosition + Vector3.up * Mathf.Max(0f, height);
        fallDuration = Mathf.Max(0.01f, duration);
        elapsedTime = 0f;
        canvasEnableDelay = Mathf.Max(fallDuration, delayBeforeCanvasEnable);
        canvasEnableElapsedTime = 0f;
        panelScaleDuration = Mathf.Max(0f, panelScaleInDuration);
        panelScaleElapsedTime = 0f;
        isFalling = true;
        PrepareTombstoneCanvases();

        transform.position = startPosition;
        enabled = true;
    }

    private void Update()
    {
        if (!isFalling)
        {
            UpdateCanvasEnableDelay();
            UpdatePanelScaleAnimation();

            if (!isWaitingToEnableCanvas && !isScalingPanels)
            {
                enabled = false;
            }

            return;
        }

        elapsedTime += Time.deltaTime;
        float normalizedTime = Mathf.Clamp01(elapsedTime / fallDuration);
        float curveTime = fallCurve.Evaluate(normalizedTime);

        transform.position = Vector3.LerpUnclamped(startPosition, landingPosition, curveTime);

        if (normalizedTime >= 1f)
        {
            transform.position = landingPosition;
            isFalling = false;
        }

        UpdateCanvasEnableDelay();
    }

    private void PrepareTombstoneCanvases()
    {
        tombstoneCanvases = GetComponentsInChildren<Canvas>(true);
        isWaitingToEnableCanvas = tombstoneCanvases.Length > 0;
        CacheCanvasPanels();

        for (int i = 0; i < tombstoneCanvases.Length; i++)
        {
            Canvas canvas = tombstoneCanvases[i];
            if (canvas == null)
            {
                continue;
            }

            if (canvas.transform == transform)
            {
                canvas.enabled = false;
                continue;
            }

            canvas.gameObject.SetActive(false);
        }
    }

    private void UpdateCanvasEnableDelay()
    {
        if (!isWaitingToEnableCanvas)
        {
            return;
        }

        canvasEnableElapsedTime += Time.deltaTime;
        if (canvasEnableElapsedTime < canvasEnableDelay)
        {
            return;
        }

        for (int i = 0; i < tombstoneCanvases.Length; i++)
        {
            Canvas canvas = tombstoneCanvases[i];
            if (canvas == null)
            {
                continue;
            }

            canvas.gameObject.SetActive(true);
            canvas.enabled = true;
        }

        isWaitingToEnableCanvas = false;
        StartPanelScaleAnimation();
    }

    private void CacheCanvasPanels()
    {
        List<RectTransform> panelList = new List<RectTransform>();
        List<Vector3> originalScaleList = new List<Vector3>();

        for (int i = 0; i < tombstoneCanvases.Length; i++)
        {
            Canvas canvas = tombstoneCanvases[i];
            if (canvas == null)
            {
                continue;
            }

            RectTransform panel = FindPanelRectTransform(canvas);
            if (panel == null)
            {
                continue;
            }

            panelList.Add(panel);
            originalScaleList.Add(panel.localScale);
            panel.localScale = Vector3.zero;
        }

        tombstonePanels = panelList.ToArray();
        tombstonePanelOriginalScales = originalScaleList.ToArray();
        isScalingPanels = false;
    }

    private static RectTransform FindPanelRectTransform(Canvas canvas)
    {
        RectTransform[] rectTransforms = canvas.GetComponentsInChildren<RectTransform>(true);

        for (int i = 0; i < rectTransforms.Length; i++)
        {
            RectTransform rectTransform = rectTransforms[i];
            if (rectTransform == null || rectTransform == canvas.transform)
            {
                continue;
            }

            if (string.Equals(rectTransform.name, "Panel", StringComparison.OrdinalIgnoreCase))
            {
                return rectTransform;
            }
        }

        return null;
    }

    private void StartPanelScaleAnimation()
    {
        if (tombstonePanels.Length == 0)
        {
            isScalingPanels = false;
            return;
        }

        if (panelScaleDuration <= 0f)
        {
            for (int i = 0; i < tombstonePanels.Length; i++)
            {
                RectTransform panel = tombstonePanels[i];
                if (panel == null)
                {
                    continue;
                }

                panel.localScale = tombstonePanelOriginalScales[i];
            }

            isScalingPanels = false;
            return;
        }

        panelScaleElapsedTime = 0f;
        isScalingPanels = true;
    }

    private void UpdatePanelScaleAnimation()
    {
        if (!isScalingPanels)
        {
            return;
        }

        panelScaleElapsedTime += Time.deltaTime;
        float normalizedTime = Mathf.Clamp01(panelScaleElapsedTime / panelScaleDuration);

        for (int i = 0; i < tombstonePanels.Length; i++)
        {
            RectTransform panel = tombstonePanels[i];
            if (panel == null)
            {
                continue;
            }

            panel.localScale = Vector3.LerpUnclamped(Vector3.zero, tombstonePanelOriginalScales[i], normalizedTime);
        }

        if (normalizedTime >= 1f)
        {
            isScalingPanels = false;
        }
    }
}
