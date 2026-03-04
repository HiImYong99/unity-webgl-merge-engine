using UnityEngine;
using System.Collections;

/// <summary>
/// Simple script to play a sequence of sprites as an animation.
/// Useful for one-shot VFX like merge stars or pops.
/// </summary>
public class SpriteAnimator : MonoBehaviour
{
    public Sprite[] Frames;
    public float FrameRate = 12f;
    public bool Loop = false;
    public bool DestroyOnFinish = true;

    private SpriteRenderer sr;

    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
    }

    private void Start()
    {
        if (Frames != null && Frames.Length > 0)
        {
            StartCoroutine(PlayAnimation());
        }
    }

    private IEnumerator PlayAnimation()
    {
        int index = 0;
        float interval = 1f / FrameRate;

        while (true)
        {
            sr.sprite = Frames[index];
            yield return new WaitForSeconds(interval);
            index++;

            if (index >= Frames.Length)
            {
                if (Loop)
                {
                    index = 0;
                }
                else
                {
                    break;
                }
            }
        }

        if (DestroyOnFinish)
        {
            Destroy(gameObject);
        }
    }

    public void SetFrames(Sprite[] frames)
    {
        Frames = frames;
    }
}
