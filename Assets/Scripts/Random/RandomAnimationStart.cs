using UnityEngine;

public class RandomAnimationStart : MonoBehaviour
{
    void Start()
    {
        Animator anim = GetComponent<Animator>();

        // Randomize start time ONLY within the first half of the cycle
        float offset = Random.Range(0f, 0.5f);

        anim.Play(0, 0, offset);
    }
}
