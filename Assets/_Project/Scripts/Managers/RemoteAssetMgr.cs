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
        int W = tex.width, H = tex.height;
        float ppu = 100f;
        Vector2 pivot = new Vector2(0.5f, 0.5f);

        try
        {
            Color32[] pixels = tex.GetPixels32();
            
            // 1) 픽셀 질량 중심(Center of Mass) 계산
            long sumX = 0, sumY = 0;
            int count = 0;
            byte alphaThresh = 150;

            for (int y = 0; y < H; y++)
            {
                for (int x = 0; x < W; x++)
                {
                    if (pixels[y * W + x].a > alphaThresh)
                    {
                        sumX += x;
                        sumY += y;
                        count++;
                    }
                }
            }

            if (count > 0)
            {
                int cx = (int)(sumX / count);
                int cy = (int)(sumY / count);

                // 2) 질량 중심에서 십자(Cross) 스캔하여 본체 반지름 도출
                float rRight = 0, rLeft = 0, rUp = 0, rDown = 0;

                for (int x = cx; x < W; x++) { if (pixels[cy * W + x].a < alphaThresh) { rRight = x - cx; break; } if (x == W - 1) rRight = x - cx; }
                for (int x = cx; x >= 0; x--) { if (pixels[cy * W + x].a < alphaThresh) { rLeft = cx - x; break; } if (x == 0) rLeft = cx; }
                for (int y = cy; y < H; y++) { if (pixels[y * W + cx].a < alphaThresh) { rUp = y - cy; break; } if (y == H - 1) rUp = y - cy; }
                for (int y = cy; y >= 0; y--) { if (pixels[y * W + cx].a < alphaThresh) { rDown = cy - y; break; } if (y == 0) rDown = cy; }

                float avgR = (rRight + rLeft + rUp + rDown) / 4f;

                // 3) 피벗 및 PPU 설정
                // 본체의 지름(avgR * 2)을 PPU로 설정 → 본체의 시각적 크기가 정확히 1.0 unit이 됨
                ppu = avgR * 2f;
                pivot = new Vector2((float)cx / W, (float)cy / H);

                Debug.Log($"[RemoteAssetMgr] Lv{level}: CoM=({cx},{cy}), R={avgR:F1}, PPU={ppu:F1}, Pivot=({pivot.x:F3},{pivot.y:F3})");
            }
            else
            {
                ppu = Mathf.Max(W, H);
                Debug.LogWarning($"[RemoteAssetMgr] Lv{level}: 픽셀 감지 실패. 기본값 사용");
            }
        }
        catch (System.Exception e) 
        { 
            Debug.LogError($"[RemoteAssetMgr] Lv{level}: GetPixels32 FAILED - {e.Message}");
            ppu = Mathf.Max(W, H);
        }

        Sprite sprite = Sprite.Create(
            tex,
            new Rect(0, 0, W, H),
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
        string url = $"{CdnBaseUrl}Animal_{level}.png";
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
