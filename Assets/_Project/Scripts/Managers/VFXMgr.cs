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

    // 티어별 머지 이펙트 레퍼런스
    private GameObject _vfxMergeNormal;
    private GameObject _vfxMergePremium;
    private GameObject _vfxMergeLegendary;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            LoadDefaultVFX();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void LoadDefaultVFX()
    {
        // Resources/VFX 폴더에서 자동 로드 시도 (인스펙터 할당 누락 대비)
        if (MergeEffectPrefab == null) MergeEffectPrefab = Resources.Load<GameObject>("VFX/VFX_Merge_Normal");
        if (SpawnEffectPrefab == null) SpawnEffectPrefab = Resources.Load<GameObject>("VFX/VFX_Spawn");
        if (LandingEffectPrefab == null) LandingEffectPrefab = Resources.Load<GameObject>("VFX/VFX_Landing");
        
        // 티어별 로드
        _vfxMergeNormal = Resources.Load<GameObject>("VFX/VFX_Merge_Normal");
        _vfxMergePremium = Resources.Load<GameObject>("VFX/VFX_Merge_Premium");
        _vfxMergeLegendary = Resources.Load<GameObject>("VFX/VFX_Merge_Legendary");
        
        if (MergeEffectPrefab == null && _vfxMergeNormal != null) MergeEffectPrefab = _vfxMergeNormal;
    }

    /// <summary>병합 시 화려한 폭발 이펙트</summary>
    public void SpawnMergeEffect(Vector3 position, int level)
    {
        // 레벨별 티어 결정
        GameObject targetPrefab = MergeEffectPrefab;
        if (level >= 9 && _vfxMergeLegendary != null) targetPrefab = _vfxMergeLegendary;
        else if (level >= 5 && _vfxMergePremium != null) targetPrefab = _vfxMergePremium;
        else if (_vfxMergeNormal != null) targetPrefab = _vfxMergeNormal;

        if (targetPrefab == null)
        {
            Debug.LogWarning($"[VFXMgr] Lv{level} 머지 이펙트 프리팹을 찾을 수 없습니다.");
            return;
        }

        // VFX가 디저트보다 앞에 보이도록 Z축 보정 (-1.0f)
        Vector3 vfxPos = new Vector3(position.x, position.y, -1.0f);
        GameObject go = Instantiate(targetPrefab, vfxPos, Quaternion.identity);
        
        // 레벨별 색상 적용 (자식 오브젝트 포함하여 ParticleSystem 검색)
        var ps = go.GetComponentInChildren<ParticleSystem>();
        if (ps != null)
        {
            if (LevelColors != null && LevelColors.Length >= level)
            {
                var main = ps.main;
                main.startColor = LevelColors[level - 1];
            }
            ps.Play(); // 명시적 실행
        }

        // 특정 컴포넌트(페이드아웃용) 실행
        var fader = go.GetComponent<MergeEffectFader>();
        if (fader == null) fader = go.GetComponentInChildren<MergeEffectFader>();
        
        if (fader != null) fader.StartFade();
    }

    /// <summary>새로운 디저트 스폰 시 반짝임 이펙트 (비활성 - 머지 이펙트만 사용)</summary>
    public void SpawnSpawnEffect(Vector3 position)
    {
        // 사용자 요청: 머지할 때만 VFX 노출 (스폰 이펙트 제거)
        /*
        if (SpawnEffectPrefab == null) return;
        Vector3 vfxPos = new Vector3(position.x, position.y, -0.5f);
        GameObject go = Instantiate(SpawnEffectPrefab, vfxPos, Quaternion.identity);
        var ps = go.GetComponentInChildren<ParticleSystem>();
        if (ps != null) ps.Play();
        */
    }

    /// <summary>바닥 또는 다른 디저트와 충격 시 먼지/파편 이펙트 (비활성 - 머지 이펙트만 사용)</summary>
    public void SpawnLandingEffect(Vector3 position, int level)
    {
        // 사용자 요청: 머지할 때만 VFX 노출 (착지 이펙트 제거)
        /*
        if (LandingEffectPrefab == null) return;
        Vector3 vfxPos = new Vector3(position.x, position.y, -0.5f);
        GameObject go = Instantiate(LandingEffectPrefab, vfxPos, Quaternion.identity);
        var ps = go.GetComponentInChildren<ParticleSystem>();
        if (ps != null)
        {
            if (LevelColors != null && LevelColors.Length >= level)
            {
                var main = ps.main;
                main.startColor = LevelColors[level - 1];
            }
            ps.Play();
        }
        */
    }

    /// <summary>획득 점수 텍스트 연출 (비활성 - Pop 이펙트만 사용)</summary>
    public void SpawnScoreEffect(Vector3 position, int score, int level)
    {
        // 사용자 요청: Pop(머지 파티클) 외의 효과 제거
        /*
        // 점수 텍스트는 가장 앞에 보이도록 Z축 보정 (-2.0f)
        Vector3 vfxPos = new Vector3(position.x, position.y, -2.0f);
        GameObject go;

        if (ScoreTextPrefab != null)
        {
            go = Instantiate(ScoreTextPrefab, vfxPos, Quaternion.identity);
        }
        else
        {
            // 프리팹이 없을 경우 동적으로 GameObject 생성하여 FloatingScore 추가
            go = new GameObject("DynamicScoreText");
            go.transform.position = vfxPos;
        }

        var fs = go.GetComponent<FloatingScore>();
        if (fs == null) fs = go.AddComponent<FloatingScore>();
        
        if (fs != null)
        {
            Color col = (LevelColors != null && LevelColors.Length >= level) 
                ? LevelColors[level - 1] 
                : Color.white;
            fs.Initialize(score, col);
        }
        */
    }
}
