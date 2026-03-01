using UnityEngine;
using System.Collections;
using System;

public class ResultCard : MonoBehaviour
{
    public static ResultCard Instance { get; private set; }

    public Camera RenderCamera;
    public RenderTexture RenderTex;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public void CaptureAndShare()
    {
        StartCoroutine(CaptureRoutine());
    }

    private IEnumerator CaptureRoutine()
    {
        yield return new WaitForEndOfFrame();

        Texture2D tex = new Texture2D(RenderTex.width, RenderTex.height, TextureFormat.RGB24, false);

        RenderTexture.active = RenderTex;
        if (RenderCamera != null)
        {
            RenderCamera.Render();
        }

        tex.ReadPixels(new Rect(0, 0, RenderTex.width, RenderTex.height), 0, 0);
        tex.Apply();

        byte[] bytes = tex.EncodeToPNG();
        string base64Image = "data:image/png;base64," + Convert.ToBase64String(bytes);

        Destroy(tex);
        RenderTexture.active = null;

        // Share
        if (BridgeManager.Instance != null && GameManager.Instance != null)
        {
            int score = GameManager.Instance.Score;
            int level = GameManager.Instance.LastDiscoveryLevel;
            BridgeManager.Instance.RequestShare(score, level, base64Image);
        }
    }
}
