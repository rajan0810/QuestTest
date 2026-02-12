using UnityEngine;
using System.Collections;

public class OppositionLawyer : MonoBehaviour
{
    [Header("Components")]
    public Animator animator;
    public AudioSource audioSource;

    [Header("Settings")]
    public float standUpDuration = 1.5f;

    private void Start()
    {
        // Ensure initial state
        animator.SetBool("IsStanding", false);
        animator.SetBool("IsSpeaking", false);
        animator.SetFloat("Emotion", 0f); // Changed to SetFloat
    }

    public void PlayResponse(AudioClip clip, string emotion)
    {
        StartCoroutine(ActingRoutine(clip, emotion));
    }

    private IEnumerator ActingRoutine(AudioClip clip, string emotion)
    {
        // 1. Determine Emotion Value (0.0 = Neutral, 1.0 = Aggressive)
        // We use floats now for the Blend Tree
        float emotionVal = (emotion != null && (emotion.ToLower().Contains("aggressive") || emotion.ToLower().Contains("yell"))) ? 1.0f : 0.0f;

        // 2. Set the Float Parameter
        animator.SetFloat("Emotion", emotionVal);

        // 3. Start Standing Up
        animator.SetBool("IsStanding", true);

        // 4. Wait for him to stand up completely
        animator.SetBool("IsSpeaking", true);
        yield return new WaitForSeconds(standUpDuration);

        // 5. Play Audio
        if (clip != null)
        {
            audioSource.clip = clip;
            audioSource.Play();
            yield return new WaitForSeconds(clip.length);
        }
        else
        {
            // Fallback if audio failed (so he doesn't get stuck standing)
            yield return new WaitForSeconds(2.0f);
        }

        // 6. Stop Speaking and Sit Down
        animator.SetBool("IsSpeaking", false);
        animator.SetBool("IsStanding", false);
    }
}