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
        // Resources/VFX 폴더에서 자동 로드 시도
        _vfxPop = Resources.Load<GameObject>("VFX/VFX_Pop");
        
        // 이전 프리팹들 (백업용 로드는 유지하되 사용처에서 차단)
        _vfxMergeNormal = Resources.Load<GameObject>("VFX/VFX_Merge_Normal");
        _vfxMergePremium = Resources.Load<GameObject>("VFX/VFX_Merge_Premium");
        _vfxMergeLegendary = Resources.Load<GameObject>("VFX/VFX_Merge_Legendary");
        
        if (MergeEffectPrefab == null) MergeEffectPrefab = _vfxPop;
    }

    /// <summary>병합 시 화려한 폭발 이펙트 (Pop 이펙트로 통일)</summary>
    public void SpawnMergeEffect(Vector3 position, int level)
    {
        // 사용자 요청: Pop 말고는 효과 없앰
        GameObject targetPrefab = _vfxPop;
        if (targetPrefab == null) targetPrefab = MergeEffectPrefab;

        if (targetPrefab == null)
        {
            Debug.LogWarning($"[VFXMgr] Pop 이펙트 프리팹을 찾을 수 없습니다.");
            return;
        }

        // VFX가 디저트보다 앞에 보이도록 Z축 보정 (-1.0f)
        Vector3 vfxPos = new Vector3(position.x, position.y, -1.0f);
        GameObject go = Instantiate(targetPrefab, vfxPos, Quaternion.identity);
        
        // [보정] 불필요한 가이드/배경 요소 정밀 제거 (분석된 스크린샷 기반)
        foreach (Transform child in go.GetComponentsInChildren<Transform>(true))
        {
            string n = child.name.ToLower();
            
            // 1. 이름 기반 차단 강화 (Checker, Plane, Quad 등 가이드용 도형 포함)
            if (n.Contains("portal") || n.Contains("guide") || n.Contains("bg") || n.Contains("back") || 
                n.Contains("f3") || n.Contains("checker") || n.Contains("plane") || n.Contains("quad"))
            {
                if (!n.Contains("effect") && !n.Contains("pop"))
                {
                    child.gameObject.SetActive(false);
                    continue;
                }
            }

            // 2. 모든 렌더러(Sprite, Mesh 등)의 자산 이름 검사
            if (child.TryGetComponent<Renderer>(out var r))
            {
                // 파티클 렌더러는 살려둠
                if (r is ParticleSystemRenderer) continue;

                // 머티리얼이나 스프라이트에 'checker'나 'guide'가 들어가면 비활성화
                bool isGuide = false;
                if (r.sharedMaterial != null && r.sharedMaterial.name.ToLower().Contains("checker")) isGuide = true;
                
                if (r is SpriteRenderer sr && sr.sprite != null)
                {
                    string sName = sr.sprite.name.ToLower();
                    if (sName.Contains("checker") || sName.Contains("guide") || (sName.Contains("portal") && !sName.Contains("effect")))
                    {
                        isGuide = true;
                    }
                }

                if (isGuide) r.enabled = false;
            }

            // 3. UI/텍스트 요소 무조건 차단
            if (n.Contains("text") || child.GetComponent("Text") != null || child.GetComponent("TMP_Text") != null)
            {
                child.gameObject.SetActive(false);
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
        // 점수 텍스트는 가장 앞에 보이도록 Z축 보정 (-2.0f)
        Vector3 vfxPos = new Vector3(position.x, position.y, -2.0f);
        GameObject go;

        if (ScoreTextPrefab != null) go = Instantiate(ScoreTextPrefab, vfxPos, Quaternion.identity);
        else
        {
            go = new GameObject("DynamicScoreText");
            go.transform.position = vfxPos;
        }

        var fs = go.GetComponent<FloatingScore>();
        if (fs == null) fs = go.AddComponent<FloatingScore>();
        
        if (fs != null)
        {
            // 백그라운드 대비 가독성을 위해 흰색이 아닌 진한 베리/초콜릿 색상 적용
            Color defaultColor = new Color(0.35f, 0.15f, 0.25f); // 진한 베리색
            Color col = (LevelColors != null && LevelColors.Length >= level) ? LevelColors[level - 1] : defaultColor;
            
            // 만약 선택된 색상이 너무 밝으면 가독성을 위해 살짝 보정 (흰색 계열 방지)
            if (col.r > 0.8f && col.g > 0.8f && col.b > 0.8f) col = defaultColor;
            
            fs.Initialize(score, col);
        }
    }
}
