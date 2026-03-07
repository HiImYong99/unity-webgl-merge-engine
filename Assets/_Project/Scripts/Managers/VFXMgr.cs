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

    // 티어별 머지 이펙트 레퍼런스 (비활성 - Pop만 사용)
    private GameObject _vfxMergeNormal;
    private GameObject _vfxMergePremium;
    private GameObject _vfxMergeLegendary;
    private GameObject _vfxPop;

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
        // [중대 결단] VFX_Pop 패키지에 포함된 원본 에셋(vfx_pop_spawn_sheets.png)에 
        // 가이드 텍스트와 체크무늬가 그려져 있어 제거가 불가능하므로, 
        // 상대적으로 깨끗한 VFX_Merge_Normal 에셋을 Pop 효과로 대신 사용합니다.
        _vfxPop = Resources.Load<GameObject>("VFX/VFX_Merge_Normal");
        
        // 보험용 로드
        _vfxMergeNormal = _vfxPop;
        _vfxMergePremium = Resources.Load<GameObject>("VFX/VFX_Merge_Premium");
        _vfxMergeLegendary = Resources.Load<GameObject>("VFX/VFX_Merge_Legendary");
        
        if (MergeEffectPrefab == null) MergeEffectPrefab = _vfxPop;
    }

    /// <summary>병합 시 화려한 폭발 이펙트 (클린한 에셋으로 교체)</summary>
    public void SpawnMergeEffect(Vector3 position, int level)
    {
        GameObject targetPrefab = _vfxPop; // VFX_Merge_Normal이 할당됨
        if (targetPrefab == null) targetPrefab = MergeEffectPrefab;

        if (targetPrefab == null) return;

        Vector3 vfxPos = new Vector3(position.x, position.y, -1.0f);
        GameObject go = Instantiate(targetPrefab, vfxPos, Quaternion.identity);
        
        // 스케일 조정 (Pop 효과에 맞게 소폭 축소)
        go.transform.localScale = Vector3.one * 0.85f;

        // [딥 클린] 혹시라도 묻어있을 배경/가이드 요소 제거
        foreach (Transform child in go.GetComponentsInChildren<Transform>(true))
        {
            if (child == null || child == go.transform) continue;
            string n = child.name.ToLower();
            if (n.Contains("bg") || n.Contains("back") || n.Contains("guide") || n.Contains("magical") || n.Contains("spawn"))
            {
                Destroy(child.gameObject);
            }
        }

        // 레벨별 색상 적용
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

        var fader = go.GetComponent<MergeEffectFader>();
        if (fader == null) fader = go.GetComponentInChildren<MergeEffectFader>();
        if (fader != null) fader.StartFade();
    }

    /// <summary>새로운 디저트 스폰 시 반짝임 이펙트 (비활성)</summary>
    public void SpawnSpawnEffect(Vector3 position) { }

    /// <summary>바닥 또는 다른 디저트와 충격 시 먼지/파편 이펙트 (비활성)</summary>
    public void SpawnLandingEffect(Vector3 position, int level) { }

    /// <summary>획득 점수 텍스트 연출</summary>
    public void SpawnScoreEffect(Vector3 position, int score, int level)
    {
        Vector3 vfxPos = new Vector3(position.x, position.y, -2.0f);
        GameObject go;

        if (ScoreTextPrefab != null) go = Instantiate(ScoreTextPrefab, vfxPos, Quaternion.identity);
        else
        {
            go = new GameObject("DynamicScoreText");
            go.transform.position = vfxPos;
        }

        // 텍스트 외 모든 잡동사니 제거
        foreach (Transform child in go.GetComponentsInChildren<Transform>(true))
        {
            if (child == null || child == go.transform) continue;
            if (child.GetComponent<FloatingScore>() == null && child.GetComponentInChildren<FloatingScore>(true) == null)
            {
                Destroy(child.gameObject);
            }
        }

        var fs = go.GetComponent<FloatingScore>();
        if (fs == null) fs = go.AddComponent<FloatingScore>();
        
        if (fs != null)
        {
            // 백그라운드 대비 가독성을 위해 진한 베리/초콜릿 색상 적용
            Color defaultColor = new Color(0.35f, 0.15f, 0.25f);
            Color col = (LevelColors != null && LevelColors.Length >= level) ? LevelColors[level - 1] : defaultColor;
            if (col.r > 0.8f && col.g > 0.8f && col.b > 0.8f) col = defaultColor;
            
            fs.Initialize(score, col);
        }
    }
}
