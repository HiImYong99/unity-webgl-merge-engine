using UnityEditor;
using UnityEngine;
using System.IO;
using System.Collections.Generic;

/// <summary>
/// 디저트 이미지 배경 제거 + 자동 크롭 도구
/// Tools > DessertPop > 🍩 Remove Dessert Backgrounds 메뉴에서 실행
///
/// 처리 파이프라인:
/// 1. 이미지 4 모서리 플러드필로 배경 감지 → alpha=0 처리
/// 2. 불투명 픽셀의 타이트 바운딩박스 계산
/// 3. 정사각형으로 크롭 + 4px 패딩 추가 (자연스러운 밀착 간격)
/// 4. Resources/Desserts/ + sprites/ 양쪽 동기화 저장
/// </summary>
public static class DessertBackgroundRemover
{
    // ── 설정 상수 ───────────────────────────────────────────
    private const int   DESSERT_LEVELS  = 11;
    private const float COLOR_TOLERANCE = 0.15f;      // 배경 색 유사도 허용 범위
    private const int   MAX_FILL_PIXELS = 8_000_000;  // BFS 안전 제한
    private const int   SQUARE_PADDING  = 2;           // 크롭 후 사방 최소 여백(px) — 거의 밀착

    private static readonly string RESOURCES_PATH = "Assets/_Project/Resources/Desserts";
    private static readonly string SPRITES_PATH   = "Assets/WebGLTemplates/DessertPop/sprites";

    // ── 메뉴  ────────────────────────────────────────────────
    [MenuItem("Assets/DessertPop/Remove Dessert Backgrounds", priority = 200)]
    [MenuItem("Tools/DessertPop/🍩 Remove Dessert Backgrounds")]
    public static void RemoveAllDessertBackgrounds()
    {
        int success = 0, failed = 0;

        for (int lv = 1; lv <= DESSERT_LEVELS; lv++)
        {
            string srcPath = Path.Combine(RESOURCES_PATH, $"Dessert_{lv}.png");
            if (!File.Exists(srcPath)) { Debug.LogWarning($"[BgRemover] 없음: {srcPath}"); failed++; continue; }

            EditorUtility.DisplayProgressBar(
                "배경 제거 + 크롭 중...",
                $"Dessert_{lv}.png  ({lv}/{DESSERT_LEVELS})",
                (float)(lv - 1) / DESSERT_LEVELS);

            if (ProcessImage(srcPath, lv)) success++;
            else                           failed++;
        }

        EditorUtility.ClearProgressBar();
        AssetDatabase.Refresh();

        string msg = $"✅ 완료!\n성공 {success}개 / 실패 {failed}개\n\n" +
                     "배경 제거 + 타이트 크롭 + 정사각형 패킹이 적용됐습니다.\n" +
                     "Resources/Desserts/ 와 sprites/ 가 갱신됐습니다.";
        EditorUtility.DisplayDialog("Dessert Background Remover", msg, "확인");
        Debug.Log("[BgRemover] " + msg);
    }

    // ── 단일 이미지 처리 ────────────────────────────────────
    private static bool ProcessImage(string assetPath, int level)
    {
        try
        {
            // 1) 원본 로드
            byte[] raw = File.ReadAllBytes(assetPath);
            Texture2D src = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!src.LoadImage(raw))
            {
                Debug.LogError($"[BgRemover] 로드 실패: {assetPath}");
                return false;
            }

            int W = src.width, H = src.height;
            Color32[] pixels = src.GetPixels32();
            Debug.Log($"[BgRemover] Dessert_{level}: {W}×{H}px");

            // 2) 배경 제거 ─ 4모서리 색 샘플 후 BFS
            Color32 bgColor = SampleCornerColor(pixels, W, H);
            bool[]  mask    = FloodFillBg(pixels, W, H, bgColor, COLOR_TOLERANCE);
            mask = Dilate(mask, W, H, 1); // 1px 팽창으로 잔여 가장자리 제거

            for (int i = 0; i < pixels.Length; i++)
                if (mask[i]) pixels[i] = new Color32(pixels[i].r, pixels[i].g, pixels[i].b, 0);

            // 3) 불투명 픽셀 바운딩박스 계산
            int minX = W, maxX = 0, minY = H, maxY = 0;
            for (int i = 0; i < pixels.Length; i++)
            {
                if (pixels[i].a < 16) continue;
                int x = i % W, y = i / W;
                if (x < minX) minX = x;
                if (x > maxX) maxX = x;
                if (y < minY) minY = y;
                if (y > maxY) maxY = y;
            }

            if (maxX < minX || maxY < minY)
            {
                Debug.LogWarning($"[BgRemover] Dessert_{level}: 불투명 픽셀 없음, 스킵");
                Object.DestroyImmediate(src);
                return false;
            }

            int contentW = maxX - minX + 1;
            int contentH = maxY - minY + 1;
            Debug.Log($"[BgRemover]   콘텐츠: {contentW}×{contentH}px (원본의 {100f*contentW/W:F1}% × {100f*contentH/H:F1}%)");

            // 4) 정사각형 크롭: 가장 긴 축 기준 + 패딩
            int squareContent = Mathf.Max(contentW, contentH);
            int squareSize    = squareContent + SQUARE_PADDING * 2;

            Texture2D dst = new Texture2D(squareSize, squareSize, TextureFormat.RGBA32, false);
            Color32[] dstPx = new Color32[squareSize * squareSize]; // 기본 투명
            // 콘텐츠를 정사각형 중앙에 배치
            int offX = (squareSize - contentW) / 2;
            int offY = (squareSize - contentH) / 2;

            for (int sy = minY; sy <= maxY; sy++)
            for (int sx = minX; sx <= maxX; sx++)
            {
                int srcIdx = sy * W + sx;
                int dx     = sx - minX + offX;
                int dy     = sy - minY + offY;
                if (dx >= 0 && dx < squareSize && dy >= 0 && dy < squareSize)
                    dstPx[dy * squareSize + dx] = pixels[srcIdx];
            }

            dst.SetPixels32(dstPx);
            dst.Apply();
            byte[] png = dst.EncodeToPNG();

            Object.DestroyImmediate(src);
            Object.DestroyImmediate(dst);

            Debug.Log($"[BgRemover]   출력: {squareSize}×{squareSize}px (콘텐츠 비율 {100f*squareContent/squareSize:F1}%)");

            // 5) 저장
            File.WriteAllBytes(assetPath, png);
            string spriteDest = Path.Combine(SPRITES_PATH, $"Dessert_{level}.png");
            if (Directory.Exists(SPRITES_PATH)) File.WriteAllBytes(spriteDest, png);

            return true;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[BgRemover] 오류 ({assetPath}): {e.Message}\n{e.StackTrace}");
            return false;
        }
    }

    // ── 4 모서리 평균 배경색 샘플링 ─────────────────────────
    private static Color32 SampleCornerColor(Color32[] pixels, int W, int H)
    {
        var samples = new List<Color32>();
        int m = 6; // 모서리 주변 m×m 픽셀 평균

        var corners = new (int cx, int cy)[] { (0, 0), (W-1, 0), (0, H-1), (W-1, H-1) };
        foreach (var (cx, cy) in corners)
        for (int dy = 0; dy < m; dy++)
        for (int dx = 0; dx < m; dx++)
        {
            int x = Mathf.Clamp(cx == 0 ? dx : cx - dx, 0, W-1);
            int y = Mathf.Clamp(cy == 0 ? dy : cy - dy, 0, H-1);
            samples.Add(pixels[y * W + x]);
        }

        float r = 0, g = 0, b = 0, a = 0;
        foreach (var c in samples) { r += c.r; g += c.g; b += c.b; a += c.a; }
        int n = samples.Count;
        return new Color32((byte)(r/n), (byte)(g/n), (byte)(b/n), (byte)(a/n));
    }

    // ── BFS 플러드 필 ────────────────────────────────────────
    private static bool[] FloodFillBg(Color32[] pixels, int W, int H, Color32 bg, float tol)
    {
        bool[] mask    = new bool[W * H];
        bool[] visited = new bool[W * H];
        var    queue   = new Queue<int>();

        // 4 모서리 + 4 변 중앙도 씨드로 추가 (더 안정적인 배경 감지)
        int[] seeds =
        {
            0, W-1, (H-1)*W, (H-1)*W+W-1,      // 모서리
            W/2, (H-1)*W+W/2,                   // 상단/하단 중앙
            H/2*W, H/2*W+W-1                    // 좌/우 중앙
        };
        foreach (int s in seeds)
        {
            if (s < 0 || s >= pixels.Length || visited[s]) continue;
            if (!ColorMatch(pixels[s], bg, tol)) continue;
            visited[s] = mask[s] = true;
            queue.Enqueue(s);
        }

        int[] dx4 = { 1, -1, 0,  0 };
        int[] dy4 = { 0,  0, 1, -1 };
        int safety = 0;

        while (queue.Count > 0 && safety < MAX_FILL_PIXELS)
        {
            safety++;
            int idx = queue.Dequeue();
            int px  = idx % W, py = idx / W;

            for (int d = 0; d < 4; d++)
            {
                int nx = px + dx4[d], ny = py + dy4[d];
                if (nx < 0 || nx >= W || ny < 0 || ny >= H) continue;
                int ni = ny * W + nx;
                if (visited[ni]) continue;
                visited[ni] = true;
                if (ColorMatch(pixels[ni], bg, tol)) { mask[ni] = true; queue.Enqueue(ni); }
            }
        }
        return mask;
    }

    // ── 마스크 팽창 ─────────────────────────────────────────
    private static bool[] Dilate(bool[] mask, int W, int H, int r)
    {
        bool[] result = (bool[])mask.Clone();
        for (int y = 0; y < H; y++)
        for (int x = 0; x < W; x++)
        {
            if (!mask[y * W + x]) continue;
            for (int dy2 = -r; dy2 <= r; dy2++)
            for (int dx2 = -r; dx2 <= r; dx2++)
            {
                int nx = x + dx2, ny = y + dy2;
                if (nx >= 0 && nx < W && ny >= 0 && ny < H)
                    result[ny * W + nx] = true;
            }
        }
        return result;
    }

    // ── 색상 유사도 ─────────────────────────────────────────
    private static bool ColorMatch(Color32 a, Color32 b, float tol)
    {
        if (a.a < 10) return true; // 이미 투명한 픽셀
        float dr = (a.r - b.r) / 255f;
        float dg = (a.g - b.g) / 255f;
        float db = (a.b - b.b) / 255f;
        return Mathf.Sqrt(dr*dr + dg*dg + db*db) <= tol;
    }
}
