using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 효과음과 배경음을 관리하는 사운드 매니저
/// </summary>
public class SoundMgr : MonoBehaviour
{
    public static SoundMgr Instance { get; private set; }

    [Header("Audio Sources")]
    public AudioSource BGMSource;
    public AudioSource SFXSource;

    [Header("Audio Clips")]
    public AudioClip BgmClip;
    public AudioClip DropClip;
    public AudioClip[] MergeClips; // 레벨별 또는 공통 병합 효과음
    public AudioClip LevelUpClip;
    public AudioClip GameOverClip;
    public AudioClip ScoreTickClip;

    private bool _isMuted = false;
    private bool _isSfxMuted = false;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            LoadDefaultClips();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void LoadDefaultClips()
    {
        // 인스펙터에서 할당되지 않은 경우 Resources에서 자동 로드 시도
        if (BgmClip == null) BgmClip = Resources.Load<AudioClip>("Audio/BGM/MainBGM");
        if (DropClip == null) DropClip = Resources.Load<AudioClip>("Audio/SFX/DropSFX");
        if (GameOverClip == null) GameOverClip = Resources.Load<AudioClip>("Audio/SFX/GameOverSFX");
        if (ScoreTickClip == null) ScoreTickClip = Resources.Load<AudioClip>("Audio/SFX/ScoreSFX");
        
        if (MergeClips == null || MergeClips.Length == 0)
        {
            AudioClip merge = Resources.Load<AudioClip>("Audio/SFX/MergeSFX");
            if (merge != null) MergeClips = new AudioClip[] { merge };
        }
        
        if (BGMSource == null) BGMSource = GetComponent<AudioSource>();
        if (SFXSource == null) SFXSource = transform.childCount > 0 ? GetComponentInChildren<AudioSource>() : GetComponent<AudioSource>();
        
        if (BGMSource == null || SFXSource == null) 
            Debug.LogWarning("[SoundMgr] AudioSource가 인스펙터에서 할당되지 않았으며, 컴포넌트를 찾을 수 없습니다.");
    }

    private void Start()
    {
        if (BGMSource != null && BgmClip != null)
        {
            BGMSource.clip = BgmClip;
            BGMSource.loop = true;
            BGMSource.Play();
        }
    }

    public void SetMute(bool mute)
    {
        _isMuted = mute;
        if (BGMSource != null) BGMSource.mute = mute;
        if (SFXSource != null) SFXSource.mute = mute || _isSfxMuted;
    }

    public void SetSfxMute(bool mute)
    {
        _isSfxMuted = mute;
        if (SFXSource != null) SFXSource.mute = _isMuted || _isSfxMuted;
    }

    /// <summary>병합 효과음 재생</summary>
    public void PlayMerge(int level)
    {
        if (_isMuted || _isSfxMuted) return;

        AudioClip clip = null;
        if (MergeClips != null && MergeClips.Length > 0)
        {
            int idx = Mathf.Clamp(level - 1, 0, MergeClips.Length - 1);
            clip = MergeClips[idx];
        }

        if (clip != null)
            SFXSource.PlayOneShot(clip, 0.8f);
    }

    /// <summary>낙하 효과음 재생</summary>
    public void PlayDrop()
    {
        if (_isMuted || _isSfxMuted) return;
        if (DropClip != null)
            SFXSource.PlayOneShot(DropClip, 0.6f);
    }

    public void PlayScoreTick()
    {
        if (_isMuted || _isSfxMuted) return;
        if (ScoreTickClip != null)
            SFXSource.PlayOneShot(ScoreTickClip, 0.4f);
    }

    public void PlayGameOver()
    {
        if (_isMuted || _isSfxMuted) return;
        if (GameOverClip != null)
            SFXSource.PlayOneShot(GameOverClip, 1.0f);
    }
}
