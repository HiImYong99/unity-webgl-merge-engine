using UnityEngine;
using System.Collections;
using System;

/// <summary>
/// 결과 화면 캡처 및 공유를 담당하는 매니저
/// </summary>
public class ResultCardMgr : MonoBehaviour
{
    public static ResultCardMgr Instance { get; private set; }

    public Camera RenderCamera;
    public RenderTexture RenderTex;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    /// <summary>화면 캡처 후 브리지를 통해 공유 요청</summary>
    public void CaptureAndShare()
    {
        StartCoroutine(Co_Capture());
    }

    private IEnumerator Co_Capture()
    {
        yield return new WaitForEndOfFrame();

        Texture2D tex = new Texture2D(RenderTex.width, RenderTex.height, TextureFormat.RGB24, false);
        RenderTexture.active = RenderTex;
        
        if (RenderCamera != null) RenderCamera.Render();

        tex.ReadPixels(new Rect(0, 0, RenderTex.width, RenderTex.height), 0, 0);
        tex.Apply();

        byte[] bytes = tex.EncodeToPNG();
        string base64 = "data:image/png;base64," + Convert.ToBase64String(bytes);

        Destroy(tex);
        RenderTexture.active = null;

        if (BridgeMgr.Instance != null && GameMgr.Instance != null)
        {
            BridgeMgr.Instance.RequestShare(GameMgr.Instance.Score, GameMgr.Instance.LastDiscoveryLevel, base64);
        }
    }
}
