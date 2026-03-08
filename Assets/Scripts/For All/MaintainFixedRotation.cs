using UnityEngine;

/// <summary>
/// Keeps the object at a fixed world rotation regardless of its parent's rotation.
/// Ideal for child cameras (like minimaps) or UI elements that should always stay upright.
/// </summary>
public class MaintainFixedRotation : MonoBehaviour
{
    [Tooltip("The fixed world rotation to maintain. Defaults to Quaternion.identity (Upright).")]
    [SerializeField] private Vector3 fixedWorldRotation = Vector3.zero;

    private void LateUpdate()
    {
        transform.rotation = Quaternion.Euler(fixedWorldRotation);
    }
}
