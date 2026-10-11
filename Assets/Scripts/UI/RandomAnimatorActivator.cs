using UnityEngine;
using System.Collections.Generic;

public class RandomSequentialAnimator : MonoBehaviour
{
    public Animator[] animators; // Assign all animator objects in the inspector

    private Animator firstAnimator;
    private Animator secondAnimator;

    private int step = 0;
    private bool isPlaying = false;

    void Start()
    {
        // Validate animator list
        if (animators.Length < 2)
        {
            Debug.LogWarning("Please assign at least 2 animator objects in the array.");
            return;
        }

        // Disable all animators at the start
        foreach (Animator anim in animators)
        {
            anim.enabled = false;
            anim.SetBool("PlayCloudAnim", false);
        }

        // Randomly choose 2 unique animators
        List<int> indices = new List<int>();
        for (int i = 0; i < animators.Length; i++) indices.Add(i);

        int firstIndex = indices[Random.Range(0, indices.Count)];
        indices.Remove(firstIndex);
        int secondIndex = indices[Random.Range(0, indices.Count)];

        firstAnimator = animators[firstIndex];
        secondAnimator = animators[secondIndex];

        Debug.Log($"[Start] Playing first animation: {firstAnimator.gameObject.name}");

        // Start the first animation
        PlayAnimator(firstAnimator);
    }

    void Update()
    {
        if (!isPlaying) return;

        // Get the currently playing animator
        Animator currentAnimator = (step == 0) ? firstAnimator : secondAnimator;

        // Log animation status each frame
        Debug.Log($"[Update] Animation playing on: {currentAnimator.gameObject.name}");

        // Check if the current animation (CloudAnimation) has finished playing
        if (currentAnimator.GetCurrentAnimatorStateInfo(0).IsName("CloudAnimation") &&
            currentAnimator.GetCurrentAnimatorStateInfo(0).normalizedTime >= 1f &&
            !currentAnimator.IsInTransition(0))
        {
            Debug.Log($"[Complete] Animation finished on: {currentAnimator.gameObject.name}");

            // Stop the current animation by setting bool to false
            currentAnimator.SetBool("PlayCloudAnim", false);
            currentAnimator.enabled = false;

            step++;

            // Play next animation if step == 1
            if (step == 1)
            {
                Debug.Log($"[Start] Playing second animation: {secondAnimator.gameObject.name}");
                PlayAnimator(secondAnimator);
            }
            else
            {
                // All animations done
                isPlaying = false;
                Debug.Log("[Done] Both animations finished.");
            }
        }
    }

    // Starts animation on given Animator by enabling and setting PlayCloudAnim to true
    void PlayAnimator(Animator animator)
    {
        animator.enabled = true;
        animator.SetBool("PlayCloudAnim", true); // triggers transition from Idle to CloudAnimation
        isPlaying = true;
    }
}
