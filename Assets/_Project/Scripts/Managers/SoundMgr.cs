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


    private bool _isBgmMuted = false;
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
            BGMSource.Play(); // [Task 5] 자동 재생 제거했으나, 유저 요청에 따라 로딩완료 직후 재생되도록 복구
        }
    }

    public void SetMute(bool mute)
    {
        _isBgmMuted = mute;
        if (BGMSource != null) BGMSource.mute = mute;
    }

    public void SetSfxMute(bool mute)
    {
        _isSfxMuted = mute;
        if (SFXSource != null) SFXSource.mute = mute;
    }

    // JS SendMessage 전용 (문자열 인자)
    public void SetBgmMuteFromJS(string mute)
    {
        if (GameMgr.Instance != null)
            GameMgr.Instance.SetBgmMuteFromJS(mute);
        else
            SetMute(mute == "1");
    }

    public void SetSfxMuteFromJS(string mute)
    {
        if (GameMgr.Instance != null)
            GameMgr.Instance.SetSfxMuteFromJS(mute);
        else
            SetSfxMute(mute == "1");
    }

    public void ForcePlayBGM()
    {
        if (BGMSource != null && !BGMSource.isPlaying && BgmClip != null)
        {
            BGMSource.Play();
        }
    }

    /// <summary>병합 효과음 재생</summary>
    public void PlayMerge(int level)
    {
        if (_isSfxMuted) return;

        AudioClip clip = null;
        if (MergeClips != null && MergeClips.Length > 0)
        {
            int idx = Mathf.Clamp(level - 1, 0, MergeClips.Length - 1);
            clip = MergeClips[idx];
        }

        if (clip != null)
            SFXSource.PlayOneShot(clip, 0.8f);
    }

    /// <summary>드롭(투하) 효과음 재생</summary>
    public void PlayDrop()
    {
        // 사용하지 않음 (요청에 따라 비활성화)
    }

    /// <summary>동물이 바닥/다른 동물에 착지할 때 효과음 재생 (DropClip 재사용)</summary>
    public void PlayLand(float intensity)
    {
        if (_isSfxMuted) return;
        if (DropClip == null) return;
        // intensity: 0~1, 충돌 세기에 따라 볼륨 조절 (착지는 드롭보다 조용하게)
        float volume = Mathf.Lerp(0.1f, 0.45f, intensity);
        SFXSource.PlayOneShot(DropClip, volume);
    }

    public void PlayScoreTick()
    {
        if (_isSfxMuted) return;
        if (ScoreTickClip != null)
            SFXSource.PlayOneShot(ScoreTickClip, 0.4f);
    }

    public void PlayGameOver()
    {
        if (_isSfxMuted) return;
        if (GameOverClip != null)
            SFXSource.PlayOneShot(GameOverClip, 1.0f);
    }
}
