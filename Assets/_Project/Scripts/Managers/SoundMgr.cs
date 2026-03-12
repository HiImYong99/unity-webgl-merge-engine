using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 효과음과 배경음을 관리하는 사운드 매니저
/// SFX AudioSource 풀을 사용하여 동시 재생 시 클리핑(노이즈) 방지
/// </summary>
public class SoundMgr : MonoBehaviour
{
    public static SoundMgr Instance { get; private set; }

    [Header("Audio Sources")]
    public AudioSource BGMSource;
    public AudioSource SFXSource; // 폴백용 (레거시)

    // ── SFX AudioSource 풀 ──────────────────────────────────────────
    private const int SFX_POOL_SIZE = 4;        // 동시 재생 상한
    private const float MAX_CONCURRENT_VOLUME = 0.8f; // 풀 전체 누적 볼륨 상한
    private AudioSource[] _sfxPool;
    private int _sfxPoolIndex = 0;
    private int _activeSfxCount = 0;            // 현재 재생 중인 SFX 수 추적

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

        BuildSFXPool();
    }

    /// <summary>
    /// SFX AudioSource 풀 생성
    /// 단일 AudioSource에 PlayOneShot이 쌓이면 클리핑 발생 → 독립 소스에 분산
    /// </summary>
    private void BuildSFXPool()
    {
        _sfxPool = new AudioSource[SFX_POOL_SIZE];
        for (int i = 0; i < SFX_POOL_SIZE; i++)
        {
            var go = new GameObject("SFXPool_" + i);
            go.transform.SetParent(transform);
            var src = go.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.loop = false;
            src.spatialBlend = 0f; // 2D 사운드
            _sfxPool[i] = src;
        }
    }

    /// <summary>
    /// 풀에서 빈 AudioSource를 가져와 1회 재생
    /// 동시 재생 수가 SFX_POOL_SIZE를 초과하거나 누적 볼륨이 클리핑 임계치를 넘으면 스킵
    /// </summary>
    private void PlaySFX(AudioClip clip, float volume)
    {
        if (clip == null || _isSfxMuted) return;

        // 현재 활성 소스 수 갱신
        _activeSfxCount = 0;
        foreach (var src in _sfxPool)
            if (src.isPlaying) _activeSfxCount++;

        // 동시 재생 상한 초과 시 스킵 (가장 낮은 볼륨으로 대체 방지)
        if (_activeSfxCount >= SFX_POOL_SIZE) return;

        // 누적 볼륨이 임계치 근처면 이번 소리 볼륨 감쇠
        float scaledVolume = volume;
        if (_activeSfxCount >= 2)
            scaledVolume *= Mathf.Lerp(1f, 0.4f, (_activeSfxCount - 1) / (float)(SFX_POOL_SIZE - 1));

        // 라운드로빈으로 다음 풀 소스 선택
        AudioSource target = null;
        for (int i = 0; i < SFX_POOL_SIZE; i++)
        {
            int idx = (_sfxPoolIndex + i) % SFX_POOL_SIZE;
            if (!_sfxPool[idx].isPlaying)
            {
                target = _sfxPool[idx];
                _sfxPoolIndex = (idx + 1) % SFX_POOL_SIZE;
                break;
            }
        }
        // 모두 사용 중이면 가장 오래된 소스 재사용
        if (target == null)
        {
            target = _sfxPool[_sfxPoolIndex];
            _sfxPoolIndex = (_sfxPoolIndex + 1) % SFX_POOL_SIZE;
        }

        target.clip = clip;
        target.volume = Mathf.Clamp01(scaledVolume);
        target.Play();
    }

    private bool _hasStartedBgm = false;

    private void Start()
    {
        if (BGMSource != null && BgmClip != null)
        {
            BGMSource.clip = BgmClip;
            BGMSource.loop = true;
            // Play() removed to prevent WebGL auto-play blocking errors. Mute is handled by SetMute later.
        }
    }

    private void Update()
    {
        if (!_hasStartedBgm && (Input.GetMouseButtonDown(0) || Input.touchCount > 0))
        {
            _hasStartedBgm = true;
            if (BGMSource != null && BgmClip != null && !BGMSource.isPlaying)
            {
                BGMSource.Play();
            }
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
        // 풀 전체에도 적용
        if (_sfxPool != null)
            foreach (var src in _sfxPool)
                if (src != null) src.mute = mute;
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

    private float _lastMergeTime = 0f;

    /// <summary>병합 효과음 재생 (레벨별 피치 변화)</summary>
    public void PlayMerge(int level)
    {
        if (_isSfxMuted) return;

        // 병합은 연쇄적으로 동시에 여러 번 발생 → 쿨다운으로 중복 억제
        if (Time.unscaledTime - _lastMergeTime < 0.08f) return;
        _lastMergeTime = Time.unscaledTime;

        AudioClip clip = null;
        if (MergeClips != null && MergeClips.Length > 0)
            clip = MergeClips[Mathf.Clamp(level - 1, 0, MergeClips.Length - 1)];

        PlaySFX(clip, 0.55f); // 볼륨 낮춰서 누적 클리핑 방지
    }

    /// <summary>드롭(투하) 효과음 재생</summary>
    public void PlayDrop()
    {
        // 사용하지 않음 (요청에 따라 비활성화)
    }

    private float _lastLandTime = 0f;

    /// <summary>착지 효과음 (충돌 세기에 따라 볼륨 조절)</summary>
    public void PlayLand(float intensity)
    {
        if (_isSfxMuted || DropClip == null) return;

        // 착지는 물리 콜백에서 매 프레임 호출될 수 있어 쿨다운 필수
        if (Time.unscaledTime - _lastLandTime < 0.08f) return;
        _lastLandTime = Time.unscaledTime;

        float volume = Mathf.Lerp(0.05f, 0.25f, intensity); // 최대치 낮춤
        PlaySFX(DropClip, volume);
    }

    private float _lastScoreTickTime = 0f;

    public void PlayScoreTick()
    {
        if (_isSfxMuted) return;

        if (Time.unscaledTime - _lastScoreTickTime < 0.06f) return;
        _lastScoreTickTime = Time.unscaledTime;

        PlaySFX(ScoreTickClip, 0.28f);
    }

    public void PlayGameOver()
    {
        if (_isSfxMuted) return;
        PlaySFX(GameOverClip, 0.85f);
    }
}
