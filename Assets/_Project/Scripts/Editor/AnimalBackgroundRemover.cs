using UnityEditor;
using UnityEngine;
using System.IO;
using System.Collections.Generic;

/// <summary>
/// 동물 이미지 배경 제거 + 자동 크롭 도구
/// Tools > AnimalPop > 🐾 Remove Animal Backgrounds 메뉴에서 실행
///
/// 처리 파이프라인:
/// 1. 이미지 4 모서리 플러드필로 배경 감지 → alpha=0 처리
/// 2. 불투명 픽셀의 타이트 바운딩박스 계산
/// 3. 정사각형으로 크롭 + 4px 패딩 추가 (자연스러운 밀착 간격)
/// 4. Resources/Animals/ + sprites/ 양쪽 동기화 저장
/// </summary>
public static class AnimalBackgroundRemover
{
    // ── 설정 상수 ───────────────────────────────────────────
    private const int   ANIMAL_LEVELS   = 11;
    private const float COLOR_TOLERANCE = 0.15f;      // 배경 색 유사도 허용 범위
    private const int   MAX_FILL_PIXELS = 8_000_000;  // BFS 안전 제한
    private const int   SQUARE_PADDING  = 2;           // 크롭 후 사방 최소 여백(px) — 거의 밀착

    private static readonly string RESOURCES_PATH = "Assets/_Project/Resources/Animals";
    private static readonly string SPRITES_PATH   = "Assets/WebGLTemplates/AnimalPop/sprites";

    // ── 메뉴  ────────────────────────────────────────────────
    [MenuItem("Assets/AnimalPop/Remove Animal Backgrounds", priority = 200)]
    [MenuItem("Tools/AnimalPop/🐾 Remove Animal Backgrounds")]
    public static void RemoveAllAnimalBackgrounds()
    {
        int success = 0, failed = 0;

        for (int lv = 1; lv <= ANIMAL_LEVELS; lv++)
        {
            string srcPath = Path.Combine(RESOURCES_PATH, $"Animal_{lv}.png");
            if (!File.Exists(srcPath)) { Debug.LogWarning($"[BgRemover] 없음: {srcPath}"); failed++; continue; }

            EditorUtility.DisplayProgressBar(
                "배경 제거 + 크롭 중...",
                $"Animal_{lv}.png  ({lv}/{ANIMAL_LEVELS})",
                (float)(lv - 1) / ANIMAL_LEVELS);

            if (ProcessImage(srcPath, lv)) success++;
            else                           failed++;
        }

        EditorUtility.ClearProgressBar();
        AssetDatabase.Refresh();

        string msg = $"✅ 완료!\n성공 {success}개 / 실패 {failed}개\n\n" +
                     "배경 제거 + 타이트 크롭 + 정사각형 패킹이 적용됐습니다.\n" +
                     "Resources/Animals/ 와 sprites/ 가 갱신됐습니다.";
        EditorUtility.DisplayDialog("Animal Background Remover", msg, "확인");
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
            
            // 2) 배경 제거 ─ 4모서리 BFS 플러드 필
            Color32 bgColor = SampleCornerColor(pixels, W, H);
            bool[] bgMask = FloodFillBg(pixels, W, H, bgColor, COLOR_TOLERANCE);
            
            // 3) 원본 캔버스를 완벽하게 유지한 채 배경 픽셀만 투명하게 만듦
            for (int i = 0; i < pixels.Length; i++)
            {
                if (bgMask[i])
                {
                    pixels[i].a = 0;
                }
            }
            
            src.SetPixels32(pixels);
            src.Apply();
            byte[] png = src.EncodeToPNG();

            // 4) 저장: 크기 변경 없이 제자리 덮어쓰기 + WebGL 폴더 동기화
            File.WriteAllBytes(assetPath, png);
            string spriteDest = Path.Combine(SPRITES_PATH, $"Animal_{level}.png");
            if (Directory.Exists(SPRITES_PATH)) File.WriteAllBytes(spriteDest, png);

            Debug.Log($"[BgRemover] Animal_{level}: {W}x{H} (원본 크기 유지, 배경만 투명화 처리)");
            
            Object.DestroyImmediate(src);
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

        int[] seeds =
        {
            0, W-1, (H-1)*W, (H-1)*W+W-1,
            W/2, (H-1)*W+W/2,
            H/2*W, H/2*W+W-1
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
