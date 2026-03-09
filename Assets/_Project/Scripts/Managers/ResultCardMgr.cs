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

    /// <summary>결과 공유 요청 (이미지 캡처 생략)</summary>
    public void CaptureAndShare()
    {
        if (BridgeMgr.Instance != null && GameMgr.Instance != null)
        {
            BridgeMgr.Instance.RequestShare(GameMgr.Instance.Score, 0, "");
        }
    }
}
