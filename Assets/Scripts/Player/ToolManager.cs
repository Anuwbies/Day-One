using UnityEngine;

public class ToolManager : MonoBehaviour
{
    [Header("Tool Container")]
    [Tooltip("If assigned, this entire container will be flipped. If not, individual tools will be flipped.")]
    public Transform toolVisualContainer;

    [Header("Tool GameObjects")]
    public GameObject stoneAxeObject;
    public GameObject pickaxeObject;
    public GameObject swordObject;

    [Header("Flipping Settings")]
    public bool flipXScale = true;
    public bool flipXPosition = true;

    [Header("References")]
    public SpriteRenderer playerSpriteRenderer;

    private void Awake()
    {
        if (playerSpriteRenderer == null)
        {
            // Try to find the Body or a SpriteRenderer in children
            playerSpriteRenderer = GetComponentInChildren<SpriteRenderer>();
        }
    }

    private void Start()
    {
        // Ensure all tools are disabled at start
        SetTool(ToolType.None);
    }

    private void LateUpdate()
    {
        HandleFlipping();
    }

    private void HandleFlipping()
    {
        if (playerSpriteRenderer == null) return;

        bool isFlipped = playerSpriteRenderer.flipX;

        if (toolVisualContainer != null)
        {
            FlipTransform(toolVisualContainer, isFlipped);
        }
        else
        {
            // If no container, flip individual tool objects if they are active
            if (stoneAxeObject != null && stoneAxeObject.activeSelf) FlipTransform(stoneAxeObject.transform, isFlipped);
            if (pickaxeObject != null && pickaxeObject.activeSelf) FlipTransform(pickaxeObject.transform, isFlipped);
            if (swordObject != null && swordObject.activeSelf) FlipTransform(swordObject.transform, isFlipped);
        }
    }

    private void FlipTransform(Transform target, bool isFlipped)
    {
        // Flip Scale
        if (flipXScale)
        {
            Vector3 scale = target.localScale;
            float targetScaleX = Mathf.Abs(scale.x) * (isFlipped ? -1f : 1f);
            if (Mathf.Abs(scale.x - targetScaleX) > 0.001f)
            {
                scale.x = targetScaleX;
                target.localScale = scale;
            }
        }

        // Flip Position (to move tool to other hand)
        if (flipXPosition)
        {
            Vector3 pos = target.localPosition;
            float targetPosX = Mathf.Abs(pos.x) * (isFlipped ? -1f : 1f);
            if (Mathf.Abs(pos.x - targetPosX) > 0.001f)
            {
                pos.x = targetPosX;
                target.localPosition = pos;
            }
        }
    }

    public void SetTool(ToolType type)
    {
        if (stoneAxeObject != null) stoneAxeObject.SetActive(type == ToolType.StoneAxe);
        if (pickaxeObject != null) pickaxeObject.SetActive(type == ToolType.Pickaxe);
        if (swordObject != null) swordObject.SetActive(type == ToolType.Sword);
    }

    public Transform GetActiveToolTransform()
    {
        if (stoneAxeObject != null && stoneAxeObject.activeSelf) return stoneAxeObject.transform;
        if (pickaxeObject != null && pickaxeObject.activeSelf) return pickaxeObject.transform;
        if (swordObject != null && swordObject.activeSelf) return swordObject.transform;
        return null;
    }
}
