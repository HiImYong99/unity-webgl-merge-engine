using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 게임 컨테이너(용기)에 프리미엄 유리 디자인 자산을 적용합니다.
/// (배경 투명화 및 씬 정리 포함)
/// </summary>
public class VesselVisualEnhancer : MonoBehaviour
{
    private void Start()
    {
        FixAndResizeContainer();
        ApplyPremiumVessel();
        InvokeRepeating(nameof(AggressiveClear), 0.5f, 1.0f);
    }

    private void FixAndResizeContainer()
    {
        // 모바일 기기에서의 시인성과 디저트 부피감을 위해 컨테이너 크기를 재규격화(3.6 x 6.5)합니다.
        float targetW = 3.6f;
        float targetH = 6.5f;
        float wallT = 0.8f;
        float bY = -1.5f;

        Transform floor = transform.Find("Floor");
        if (floor != null)
        {
            floor.localPosition = new Vector3(0, bY - wallT / 2, 0);
            if (floor.TryGetComponent<BoxCollider2D>(out var col)) col.size = new Vector2(targetW + wallT * 2, wallT);
        }

        Transform left = transform.Find("LeftWall");
        if (left != null)
        {
            left.localPosition = new Vector3(-targetW / 2 - wallT / 2, bY + targetH / 2, 0);
            if (left.TryGetComponent<BoxCollider2D>(out var col)) col.size = new Vector2(wallT, targetH);
        }

        Transform right = transform.Find("RightWall");
        if (right != null)
        {
            right.localPosition = new Vector3(targetW / 2 + wallT / 2, bY + targetH / 2, 0);
            if (right.TryGetComponent<BoxCollider2D>(out var col)) col.size = new Vector2(wallT, targetH);
        }
    }

    private void AggressiveClear()
    {
        // ... (rest of the clear logic remains the same)
        Color premiumPastel = new Color(1.0f, 0.98f, 0.98f, 0.0f);
        Camera[] cams = Object.FindObjectsOfType<Camera>();
        foreach (var c in cams)
        {
            if (c == null) continue;
            c.clearFlags = CameraClearFlags.SolidColor;
            c.backgroundColor = premiumPastel;
        }

        string[] bgNames = { 
            "Background", "ContainerBackground", "ProceduralBackground", "Environment", 
            "BG", "Back", "Sky", "Ground", "ForestBackground", "SceneBackground"
        };
        foreach (var name in bgNames)
        {
            GameObject bg = GameObject.Find(name);
            if (bg != null && bg.activeSelf) bg.SetActive(false);
        }
    }

    public void ApplyPremiumVessel()
    {
        AggressiveClear();

        Transform footer = transform.Find("UIFooter");
        if (footer != null) footer.gameObject.SetActive(false);

        Transform tableBase = transform.Find("TableBase");
        if (tableBase != null)
        {
            Transform lattice = tableBase.Find("LatticeBackground");
            if (lattice != null) lattice.gameObject.SetActive(false);
            SpriteRenderer tbsr = tableBase.GetComponent<SpriteRenderer>();
            if (tbsr != null) tbsr.color = new Color(1, 1, 1, 0.05f);
        }

        // 4. 벽면 디자인 (Clear Pastel Rose) 적용
        Color wallColor = new Color(1.0f, 0.92f, 0.95f, 0.85f); // 시인성을 위해 불투명도 추가 보강

        Texture2D whiteTex = new Texture2D(1, 1);
        whiteTex.SetPixel(0, 0, Color.white);
        whiteTex.Apply();
        Sprite whiteSprite = Sprite.Create(whiteTex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f));

        foreach (Transform child in transform)
        {
            if (child.name.Contains("Wall") || child.name.Contains("Floor"))
            {
                BoxCollider2D boxCol = child.GetComponent<BoxCollider2D>();
                if (boxCol == null) continue;

                Transform visual = child.Find("Visual") ?? child;
                Renderer r = visual.GetComponent<Renderer>();
                
                if (r == null)
                {
                    SpriteRenderer sr = visual.gameObject.AddComponent<SpriteRenderer>();
                    sr.sprite = whiteSprite;
                    sr.color = wallColor;
                    sr.sortingOrder = -5;
                    visual.localScale = new Vector3(boxCol.size.x, boxCol.size.y, 1f);
                    r = sr;
                }
                else
                {
                    if (r is SpriteRenderer sr) sr.color = wallColor;
                    else if (r.material != null) r.material.color = wallColor;
                    visual.localScale = new Vector3(boxCol.size.x, boxCol.size.y, 1f);
                }
            }
        }

        DrawVesselOutline();
        CreateDecorativePearls();

        Debug.Log("[VesselVisualEnhancer] ✅ Premium Jar Resized and Visualized.");
    }

    private void CreateDecorativePearls()
    {
        // (기존 펄 생성 로직과 유사하나 확장된 사이즈에 맞춰 위치 보정)
        float w = 3.6f;
        float bY = -1.5f;

        Texture2D pearlTex = new Texture2D(32, 32);
        for (int y = 0; y < 32; y++)
        for (int x = 0; x < 32; x++)
        {
            float d = Vector2.Distance(new Vector2(x, y), new Vector2(16, 16));
            float alpha = Mathf.Clamp01(1f - (d - 14f));
            pearlTex.SetPixel(x, y, new Color(1, 1, 1, alpha));
        }
        pearlTex.Apply();
        Sprite pearlSprite = Sprite.Create(pearlTex, new Rect(0, 0, 32, 32), new Vector2(0.5f, 0.5f));

        int pearlCount = 10; // 사이즈가 커졌으므로 펄 개수 증가
        for (int i = 0; i < pearlCount; i++)
        {
            string pName = $"Pearl_{i}";
            GameObject pearl = transform.Find(pName)?.gameObject ?? new GameObject(pName);
            pearl.transform.SetParent(transform);
            
            float x = Mathf.Lerp(-w/2 + 0.35f, w/2 - 0.35f, (float)i / (pearlCount - 1));
            float y = bY + 0.15f + Mathf.Sin(i * 1.5f) * 0.06f;
            pearl.transform.localPosition = new Vector3(x, y, 0);

            SpriteRenderer sr = pearl.GetComponent<SpriteRenderer>() ?? pearl.AddComponent<SpriteRenderer>();
            sr.sprite = pearlSprite;
            sr.color = new Color(1f, 1f, 1f, 0.5f);
            sr.sortingOrder = -3;
            
            float s = Random.Range(0.14f, 0.25f);
            pearl.transform.localScale = new Vector3(s, s, 1);
        }
    }

    private void DrawVesselOutline()
    {
        // 5. LineRenderer를 이용한 세련된 외곽선 추가 (모서리가 둥근 유리병 형태)
        LineRenderer line = GetComponent<LineRenderer>();
        if (line == null) line = gameObject.AddComponent<LineRenderer>();

        float w = 3.6f, h = 6.5f, bY = -1.5f;
        float wallX = w / 2;
        float radius = 0.4f; // 둥근 모서리 반경
        float margin = 0.05f;

        List<Vector3> points = new List<Vector3>();
        
        // 1. 좌측 상단 -> 좌측 둥근 모서리 시작점
        points.Add(new Vector3(-wallX - margin, bY + h, 0));
        points.Add(new Vector3(-wallX - margin, bY + radius, 0));

        // 2. 좌측 하단 하단 둥근 모서리 (Arc)
        for (int i = 1; i <= 8; i++)
        {
            float angle = Mathf.PI + (Mathf.PI / 2) * (i / 8f);
            points.Add(new Vector3(-wallX + radius + Mathf.Cos(angle) * (radius + margin), 
                                   bY + radius + Mathf.Sin(angle) * (radius + margin), 0));
        }

        // 3. 바닥 -> 우측 둥근 모서리 시작점
        points.Add(new Vector3(wallX - radius, bY - margin, 0));

        // 4. 우측 하단 둥근 모서리 (Arc)
        for (int i = 1; i <= 8; i++)
        {
            float angle = (Mathf.PI * 1.5f) + (Mathf.PI / 2) * (i / 8f);
            points.Add(new Vector3(wallX - radius + Mathf.Cos(angle) * (radius + margin), 
                                   bY + radius + Mathf.Sin(angle) * (radius + margin), 0));
        }

        // 5. 우측 상단
        points.Add(new Vector3(wallX + margin, bY + h, 0));

        line.positionCount = points.Count;
        line.SetPositions(points.ToArray());
        line.useWorldSpace = false;
        line.startWidth = 0.16f; // 모바일 시인성을 위해 추가 확장 (0.12 -> 0.16)
        line.endWidth = 0.16f;
        
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new GradientColorKey[] { 
                new GradientColorKey(new Color(1f, 0.88f, 0.95f), 0.0f), 
                new GradientColorKey(new Color(0.88f, 0.96f, 1f), 0.5f),
                new GradientColorKey(new Color(1f, 0.88f, 0.95f), 1.0f) 
            },
            new GradientAlphaKey[] { 
                new GradientAlphaKey(0.95f, 0.0f), 
                new GradientAlphaKey(0.95f, 1.0f) 
            }
        );
        line.colorGradient = gradient;
        line.material = new Material(Shader.Find("Sprites/Default"));
        line.sortingOrder = -4; 
    }
}
