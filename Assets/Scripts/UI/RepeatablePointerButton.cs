using UnityEngine;
using UnityEngine.EventSystems;
using System;

public class RepeatablePointerButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler, IPointerExitHandler
{
    public Action onLeftClick;
    public Action onRightClick;
    public Action onHoldAction;

    [Header("Hold Settings")]
    public float holdInitialDelay = 0.5f;
    public float holdRepeatInterval = 0.1f;

    private bool isHolding = false;
    private float holdTimer = 0f;
    private bool initialDelayPassed = false;

    private void Update()
    {
        if (isHolding && onHoldAction != null)
        {
            holdTimer += Time.unscaledDeltaTime;

            if (!initialDelayPassed)
            {
                if (holdTimer >= holdInitialDelay)
                {
                    initialDelayPassed = true;
                    holdTimer = 0f;
                    onHoldAction.Invoke();
                }
            }
            else
            {
                if (holdTimer >= holdRepeatInterval)
                {
                    holdTimer = 0f;
                    onHoldAction.Invoke();
                }
            }
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left)
        {
            isHolding = true;
            holdTimer = 0f;
            initialDelayPassed = false;
        }
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left)
        {
            ResetHold();
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        ResetHold();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left)
        {
            onLeftClick?.Invoke();
        }
        else if (eventData.button == PointerEventData.InputButton.Right)
        {
            onRightClick?.Invoke();
        }
    }

    private void ResetHold()
    {
        isHolding = false;
        holdTimer = 0f;
        initialDelayPassed = false;
    }
}
