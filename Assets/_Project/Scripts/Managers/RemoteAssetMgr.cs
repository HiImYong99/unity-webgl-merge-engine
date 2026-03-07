using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 원격 또는 로컬에서 디저트 에셋(스프라이트)을 로드하고 관리하는 매니저
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
    /// 특정 레벨의 디저트 스프라이트를 로드합니다.
    /// </summary>
    public void LoadDessertSprite(int level, System.Action<Sprite> callback)
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
            Texture2D tex = Resources.Load<Texture2D>($"Desserts/Dessert_{level}");
            if (tex != null)
            {
                callback?.Invoke(CreateTightSprite(level, tex));
            }
            else
            {
                Debug.LogWarning($"[RemoteAssetMgr] Failed to load dessert texture level {level} from Resources.");
                callback?.Invoke(null);
            }
        }
    }

    /// <summary>
    /// 텍스처를 기반으로 타이트한 스프라이트를 생성합니다.
    /// </summary>
    private Sprite CreateTightSprite(int level, Texture2D tex)
    {
        // DessertBackgroundRemover가 이미 중앙 정렬된 정사각형 텍스처를 생성하므로
        // PPU는 긴 축(모두 한쪽)으로 잡고, 피벗은 0.5, 0.5로 고정합니다.
        float ppu = Mathf.Max(tex.width, tex.height);
        Vector2 pivot = new Vector2(0.5f, 0.5f);

        Sprite sprite = Sprite.Create(
            tex,
            new Rect(0, 0, tex.width, tex.height),
            pivot,
            ppu
        );
        _spriteCache[level] = sprite;
        return sprite;
    }

    /// <summary>
    /// CDN에서 스프라이트 다운로드 코루틴
    /// </summary>
    private IEnumerator Co_DownloadSprite(int level, System.Action<Sprite> callback)
    {
        string url = $"{CdnBaseUrl}Dessert_{level}.png";
        using (UnityWebRequest uwr = UnityWebRequestTexture.GetTexture(url))
        {
            yield return uwr.SendWebRequest();
            if (uwr.result == UnityWebRequest.Result.Success)
            {
                Texture2D texture = DownloadHandlerTexture.GetContent(uwr);
                callback?.Invoke(CreateTightSprite(level, texture));
            }
            else
            {
                Debug.LogError($"[RemoteAssetMgr] CDN Download failed for level {level}: {uwr.error}");
                callback?.Invoke(null);
            }
        }
    }
}
