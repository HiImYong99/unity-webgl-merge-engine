using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 파티클 이펙트와 텍스트 연출(VFX)을 관리하는 매니저
/// </summary>
public class VFXMgr : MonoBehaviour
{
    public static VFXMgr Instance { get; private set; }

    [Header("Prefabs")]
    public GameObject MergeEffectPrefab;
    public GameObject SpawnEffectPrefab;
    public GameObject LandingEffectPrefab;
    public GameObject ScoreTextPrefab;

    [Header("Colors")]
    public Color[] LevelColors;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    /// <summary>병합 시 화려한 폭발 이펙트</summary>
    public void SpawnMergeEffect(Vector3 position, int level)
    {
        if (MergeEffectPrefab == null) return;

        GameObject go = Instantiate(MergeEffectPrefab, position, Quaternion.identity);
        
        // 레벨별 색상 적용 (ParticleSystem이 있다면)
        var ps = go.GetComponent<ParticleSystem>();
        if (ps != null && LevelColors != null && LevelColors.Length >= level)
        {
            var main = ps.main;
            main.startColor = LevelColors[level - 1];
        }

        // 특정 컴포넌트(페이드아웃용) 실행
        var fader = go.GetComponent<MergeEffectFader>();
        if (fader != null) fader.StartFade();
    }

    /// <summary>새로운 디저트 스폰 시 반짝임 이펙트</summary>
    public void SpawnSpawnEffect(Vector3 position)
    {
        if (SpawnEffectPrefab == null) return;
        Instantiate(SpawnEffectPrefab, position, Quaternion.identity);
    }

    /// <summary>바닥 또는 다른 디저트와 충격 시 먼지/파편 이펙트</summary>
    public void SpawnLandingEffect(Vector3 position, int level)
    {
        if (LandingEffectPrefab == null) return;
        
        GameObject go = Instantiate(LandingEffectPrefab, position, Quaternion.identity);
        var ps = go.GetComponent<ParticleSystem>();
        if (ps != null && LevelColors != null && LevelColors.Length >= level)
        {
            var main = ps.main;
            main.startColor = LevelColors[level - 1];
        }
    }

    /// <summary>획득 점수 텍스트 연출</summary>
    public void SpawnScoreEffect(Vector3 position, int score, int level)
    {
        if (ScoreTextPrefab == null) return;

        GameObject go = Instantiate(ScoreTextPrefab, position, Quaternion.identity);
        var fs = go.GetComponent<FloatingScore>();
        if (fs != null)
        {
            Color col = (LevelColors != null && LevelColors.Length >= level) 
                ? LevelColors[level - 1] 
                : Color.white;
            fs.Initialize(score, col);
        }
    }
}
