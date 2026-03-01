using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    public AudioSource BGMSource;
    public AudioSource SFXSource;

    public bool IsMuted { get; private set; }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void SetMute(bool mute)
    {
        IsMuted = mute;
        if (BGMSource != null)
        {
            BGMSource.mute = IsMuted;
            if (!IsMuted && !BGMSource.isPlaying && BGMSource.clip != null)
            {
                BGMSource.Play(); // Ensure it resumes if it was paused while muted
            }
        }
        if (SFXSource != null) SFXSource.mute = IsMuted;
    }

    public void PlayBGM(AudioClip clip)
    {
        if (BGMSource == null || clip == null) return;
        BGMSource.clip = clip;
        BGMSource.loop = true;
        BGMSource.Play();
    }

    public void PlaySFX(AudioClip clip)
    {
        if (SFXSource == null || clip == null || IsMuted) return;
        SFXSource.PlayOneShot(clip);
    }

    // Handles OS background / foreground
    private void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus)
        {
            PauseAllAudio();
        }
        else
        {
            ResumeAllAudio();
        }
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus)
        {
            PauseAllAudio();
        }
        else
        {
            ResumeAllAudio();
        }
    }

    private void PauseAllAudio()
    {
        if (BGMSource != null && BGMSource.isPlaying) BGMSource.Pause();
        // Option to pause SFX as well, usually leaving them is fine as they are short, but we pause BGM.
    }

    private void ResumeAllAudio()
    {
        if (BGMSource != null && !IsMuted && !BGMSource.isPlaying)
        {
            if (BGMSource.clip != null)
            {
                BGMSource.UnPause();
                if (!BGMSource.isPlaying) BGMSource.Play();
            }
        }
    }
}
