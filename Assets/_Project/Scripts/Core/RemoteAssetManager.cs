using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Collections.Generic;

public class RemoteAssetManager : MonoBehaviour
{
    public static RemoteAssetManager Instance { get; private set; }

    [Header("CDN Settings")]
    public bool UseCDN = false;
    public string CdnBaseUrl = "https://your-cdn-url.com/sprites/";
    
    private Dictionary<int, Sprite> spriteCache = new Dictionary<int, Sprite>();

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
    /// 디저트 객체에 맞는 스프라이트를 설정합니다. 
    /// CDN 사용 시 비동기로 로드하며, 로드 전에는 기존 스프라이트 혹은 플레이스홀더를 유지합니다.
    /// </summary>
    public void LoadDessertSprite(int level, System.Action<Sprite> callback)
    {
        if (spriteCache.TryGetValue(level, out Sprite cached))
        {
            callback?.Invoke(cached);
            return;
        }

        if (UseCDN)
        {
            StartCoroutine(DownloadSpriteCoroutine(level, callback));
        }
        else
        {
            // 로컬 리소스에서 로드 시도 (Assets/_Project/Sprites/Desserts/Dessert_{level})
            Sprite localSprite = Resources.Load<Sprite>($"Desserts/Dessert_{level}");
            if (localSprite != null)
            {
                spriteCache[level] = localSprite;
                callback?.Invoke(localSprite);
            }
        }
    }

    private IEnumerator DownloadSpriteCoroutine(int level, System.Action<Sprite> callback)
    {
        string url = $"{CdnBaseUrl}Dessert_{level}.png";
        
        using (UnityWebRequest uwr = UnityWebRequestTexture.GetTexture(url))
        {
            yield return uwr.SendWebRequest();

            if (uwr.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"[RemoteAssetManager] Failed to download sprite from {url}: {uwr.error}");
                callback?.Invoke(null);
            }
            else
            {
                Texture2D texture = DownloadHandlerTexture.GetContent(uwr);
                Sprite sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
                spriteCache[level] = sprite;
                callback?.Invoke(sprite);
            }
        }
    }
}
