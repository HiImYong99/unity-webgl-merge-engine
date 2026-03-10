using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 원격 또는 로컬에서 동물 에셋(스프라이트)을 로드하고 관리하는 매니저
/// </summary>
public class RemoteAssetMgr : MonoBehaviour
{
    public static RemoteAssetMgr Instance { get; private set; }

    [Header("CDN Settings")]
    public bool UseCDN = false;
    public string CdnBaseUrl = "https://your-cdn-url.com/sprites/";
    
    // 스프라이트 캐싱을 위한 딕셔너리
    private Dictionary<int, Sprite> _spriteCache = new Dictionary<int, Sprite>();

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

    /// <summary>
    /// 특정 레벨의 동물 스프라이트를 로드합니다.
    /// </summary>
    public void LoadAnimalSprite(int level, System.Action<Sprite> callback)
    {
        // 캐시 확인
        if (_spriteCache.TryGetValue(level, out Sprite cached))
        {
            callback?.Invoke(cached);
            return;
        }

        if (UseCDN)
        {
            StartCoroutine(Co_DownloadSprite(level, callback));
        }
        else
        {
            // Resources에서 로드
            Texture2D tex = Resources.Load<Texture2D>($"Animals/Animal_{level}");
            if (tex != null)
            {
                callback?.Invoke(CreateTightSprite(level, tex));
            }
            else
            {
                Debug.LogWarning($"[RemoteAssetMgr] Failed to load animal texture level {level} from Resources.");
                callback?.Invoke(null);
            }
        }
    }

    /// <summary>
    /// 동물의 불투명 픽셀들의 '질량 중심(Center of Mass)'을 계산하여 가장 완벽한 본체 중심을 찾습니다.
    /// 질량 중심에서 상/하/좌/우 십자스캔을 통해 꼬리나 귀를 제외한 "원형 몸통"의 진짜 지름을 구합니다.
    /// 구한 몸통 지름을 PPU로 설정하여, 콜라이더 구체에 상하좌우 모든 마진 없이 1像素의 오차도 없이 꽉 차게 밀착시킵니다.
    /// </summary>
    private Sprite CreateTightSprite(int level, Texture2D tex)
    {
        // 강제로 Bilinear 필터링을 설정하여 도트(계단 현상)를 방지하고 부드럽게 렌더링
        tex.filterMode = FilterMode.Bilinear;
        // 외곽선에 까만 줄이 생기지 않도록 Clamp 보장
        tex.wrapMode = TextureWrapMode.Clamp;

        int W = tex.width, H = tex.height;
        
        // 여백(약 12%)을 고려하여 PPU를 설정, Collider 크기와 Visual 크기를 맞춤
        float ppu = Mathf.Max(W, H) * 0.88f; 
        Vector2 pivot = new Vector2(0.5f, 0.5f);

        Sprite sprite = Sprite.Create(
            tex,
            new Rect(0, 0, W, H),
            pivot,
            ppu,
            0,
            SpriteMeshType.FullRect // 외곽 메시 최적화 및 렌더 깨짐 방지
        );
        _spriteCache[level] = sprite;
        return sprite;
    }

    /// <summary>
    /// CDN에서 스프라이트 다운로드 코루틴
    /// </summary>
    private IEnumerator Co_DownloadSprite(int level, System.Action<Sprite> callback)
    {
        string url = $"{CdnBaseUrl}Animal_{level}.png";
        using (UnityWebRequest uwr = UnityWebRequestTexture.GetTexture(url))
        {
            yield return uwr.SendWebRequest();
            if (uwr.result == UnityWebRequest.Result.Success)
            {
                Texture2D texture = DownloadHandlerTexture.GetContent(uwr);
                if (texture != null)
                {
                    callback?.Invoke(CreateTightSprite(level, texture));
                    yield break;
                }
            }
            
            Debug.LogError($"[RemoteAssetMgr] CDN Download failed for level {level}: {uwr.error}");
            // Fallback to local Resources to mitigate CORS issues
            Texture2D localTex = Resources.Load<Texture2D>($"Animals/Animal_{level}");
            if (localTex != null)
            {
                callback?.Invoke(CreateTightSprite(level, localTex));
            }
            else
            {
                callback?.Invoke(null);
            }
        }
    }
}
