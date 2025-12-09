using UnityEngine;

public class RandomAnimationStart : MonoBehaviour
{
    void Start()
    {
        Animator anim = GetComponent<Animator>();
        anim.Play(0, 0, Random.value);
    }
}
