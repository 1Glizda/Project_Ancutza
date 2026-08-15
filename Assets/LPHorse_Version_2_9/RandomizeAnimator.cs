using UnityEngine;

[RequireComponent(typeof(Animator))]
public class RandomizeAnimator : MonoBehaviour
{
    public float minSpeed = 0.8f;
    public float maxSpeed = 1.2f;

    void Start()
    {
        Animator anim = GetComponent<Animator>();
        if (anim != null)
        {
            anim.speed = Random.Range(minSpeed, maxSpeed);
            AnimatorStateInfo state = anim.GetCurrentAnimatorStateInfo(0);
            anim.Play(state.fullPathHash, 0, Random.Range(0f, 1f));
        }
    }
}
