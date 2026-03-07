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
    private const float COLOR_TOLERANCE = 0.18f;       // 초록색 개구리/도치 보호를 위해 관대하게 설정
    private const int   MAX_FILL_PIXELS = 8_000_000;  // BFS 안전 제한
    private const int   SQUARE_PADDING  = 2;           // 크롭 후 사방 최소 여백(px)

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
                     "배경 제거 + 타이트 크롭 + 섬 제거가 적용됐습니다.\n" +
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
            
            // 2.5) 마스크 팽창(Dilate) ─ 외곽 프린지 제거
            bgMask = Dilate(bgMask, W, H, 1);
            
            // 배경 적용 (일차 제거)
            for (int i = 0; i < pixels.Length; i++)
            {
                if (bgMask[i]) pixels[i].a = 0;
            }

            // 2.6) 섬 제거(Islands Removal) ─ 본체와 떨어져 있는 미세한 '그린 도트'들 완전 박멸
            // 가장 큰 덩어리(동물 본체)만 남기고 나머지는 투명화
            RemoveIsolatedIslands(pixels, W, H);

            // 3) 콘텐츠 영역(바운딩 박스) 계산 ─ Alpha > 150 기준
            int minX = W, maxX = 0, minY = H, maxY = 0;
            bool foundContent = false;
            byte alphaThresh = 150;

            for (int y = 0; y < H; y++)
            {
                for (int x = 0; x < W; x++)
                {
                    int i = y * W + x;


                    if (pixels[i].a > alphaThresh)
                    {
                        if (x < minX) minX = x;
                        if (x > maxX) maxX = x;
                        if (y < minY) minY = y;
                        if (y > maxY) maxY = y;
                        foundContent = true;
                    }
                }
            }

            if (!foundContent)
            {
                Debug.LogWarning($"[BgRemover] Animal_{level}: 콘텐츠를 찾을 수 없음.");
                Object.DestroyImmediate(src);
                return false;
            }

            // 4) 정사각형 크롭 영역 결정
            int contentW = maxX - minX + 1;
            int contentH = maxY - minY + 1;
            int maxDim = Mathf.Max(contentW, contentH);
            int cropSize = maxDim + (SQUARE_PADDING * 2);

            int centerX = (minX + maxX) / 2;
            int centerY = (minY + maxY) / 2;
            int startX = centerX - (cropSize / 2);
            int startY = centerY - (cropSize / 2);

            // 5) 새로운 텍스처 생성
            Texture2D dst = new Texture2D(cropSize, cropSize, TextureFormat.RGBA32, false);
            Color32[] dstPixels = new Color32[cropSize * cropSize];

            for (int dy = 0; dy < cropSize; dy++)
            for (int dx = 0; dx < cropSize; dx++)
            {
                int sx = startX + dx, sy = startY + dy;
                if (sx >= 0 && sx < W && sy >= 0 && sy < H)
                    dstPixels[dy * cropSize + dx] = pixels[sy * W + sx];
            }

            dst.SetPixels32(dstPixels);
            dst.Apply();
            byte[] png = dst.EncodeToPNG();

            File.WriteAllBytes(assetPath, png);
            string spriteDest = Path.Combine(SPRITES_PATH, $"Animal_{level}.png");
            if (Directory.Exists(SPRITES_PATH)) File.WriteAllBytes(spriteDest, png);

            Debug.Log($"[BgRemover] Animal_{level}: {W}x{H} -> {cropSize}x{cropSize} (그린 도트 박멸 완료)");
            
            Object.DestroyImmediate(src);
            Object.DestroyImmediate(dst);
            return true;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[BgRemover] 오류: {e.Message}");
            return false;
        }
    }

    // ── 고립된 픽셀(섬) 제거 로직 ──────────────────────────
    private static void RemoveIsolatedIslands(Color32[] pixels, int W, int H)
    {
        bool[] visited = new bool[W * H];
        var components = new List<List<int>>();
        int connAlpha = 10; // 아주 희미한 점까지 모두 감지

        for (int i = 0; i < pixels.Length; i++)
        {
            if (pixels[i].a >= connAlpha && !visited[i])
            {
                var comp = new List<int>();
                var q = new Queue<int>();
                q.Enqueue(i);
                visited[i] = true;
                while (q.Count > 0)
                {
                    int idx = q.Dequeue();
                    comp.Add(idx);
                    int px = idx % W, py = idx / W;
                    for (int ny = py-1; ny <= py+1; ny++)
                    for (int nx = px-1; nx <= px+1; nx++)
                    {
                        if (nx < 0 || nx >= W || ny < 0 || ny >= H) continue;
                        int ni = ny * W + nx;
                        if (!visited[ni] && pixels[ni].a >= connAlpha)
                        { visited[ni] = true; q.Enqueue(ni); }
                    }
                }
                components.Add(comp);
            }
        }

        if (components.Count <= 1) return;

        // 보존할 덩어리 판별
        int maxCount = 0;
        foreach (var c in components) if (c.Count > maxCount) maxCount = c.Count;

        float centerX = W / 2f, centerY = H / 2f;
        float safeRadius = Mathf.Min(W, H) * 0.40f; // 중앙에서 40% 이내는 모두 보존 (고슴도치 가시 등 안전 구역)

        for (int j = 0; j < components.Count; j++)
        {
            var comp = components[j];
            bool isLargest = (comp.Count == maxCount);
            
            // 덩어리의 어떤 점이라도 안전 구역(중앙 40%) 안에 있는지 확인
            bool isInsideSafeZone = false;
            foreach (int pi in comp)
            {
                float px = pi % W, py = pi / W;
                if (Vector2.Distance(new Vector2(px, py), new Vector2(centerX, centerY)) < safeRadius)
                {
                    isInsideSafeZone = true;
                    break;
                }
            }

            // 본체가 아니고 + 안전 구역 밖(Corner)에만 존재하는 덩어리는 처단
            if (!isLargest && !isInsideSafeZone)
            {
                foreach (int pi in comp) pixels[pi] = new Color32(0, 0, 0, 0);
            }
        }
    }

    // ── 4 모서리 평균 배경색 샘플링 ─────────────────────────
    private static Color32 SampleCornerColor(Color32[] pixels, int W, int H)
    {
        var samples = new List<Color32>();
        int m = 10;
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

    private static bool[] FloodFillBg(Color32[] pixels, int W, int H, Color32 bg, float tol)
    {
        bool[] mask = new bool[W * H], visited = new bool[W * H];
        var q = new Queue<int>();
        
        // 사방 끝과 중앙 모서리 총 12개의 씨앗 점 추가 (더 꼼꼼하게 배경 탐색)
        int[] seeds = { 
            0, W-1, (H-1)*W, (H-1)*W+W-1, // 모서리
            W/2, (H-1)*W+W/2,             // 상하 중앙
            H/2*W, H/2*W+W-1,             // 좌우 중앙
            W/4, W/4*3,                   // 추가 상단 포인트
            (H-1)*W + W/4, (H-1)*W + W/4*3 // 추가 하단 포인트
        };

        foreach (int s in seeds)
        {
            if (s >= 0 && s < pixels.Length && !visited[s] && ColorMatch(pixels[s], bg, tol))
            { visited[s] = mask[s] = true; q.Enqueue(s); }
        }
        int[] dx4 = { 1, -1, 0, 0 }, dy4 = { 0, 0, 1, -1 };
        while (q.Count > 0)
        {
            int idx = q.Dequeue(), px = idx % W, py = idx / W;
            for (int d = 0; d < 4; d++)
            {
                int nx = px + dx4[d], ny = py + dy4[d];
                if (nx < 0 || nx >= W || ny < 0 || ny >= H) continue;
                int ni = ny * W + nx;
                if (!visited[ni] && ColorMatch(pixels[ni], bg, tol))
                { visited[ni] = mask[ni] = true; q.Enqueue(ni); }
            }
        }
        return mask;
    }

    private static bool[] Dilate(bool[] mask, int W, int H, int r)
    {
        bool[] res = (bool[])mask.Clone();
        for (int y = 0; y < H; y++)
        for (int x = 0; x < W; x++)
        {
            if (!mask[y * W + x]) continue;
            for (int dy = -r; dy <= r; dy++)
            for (int dx = -r; dx <= r; dx++)
            {
                int nx = x + dx, ny = y + dy;
                if (nx >= 0 && nx < W && ny >= 0 && ny < H) res[ny * W + nx] = true;
            }
        }
        return res;
    }

    private static bool ColorMatch(Color32 a, Color32 b, float tol)
    {
        if (a.a < 10) return true;
        float dr = (a.r - b.r) / 255f, dg = (a.g - b.g) / 255f, db = (a.b - b.b) / 255f;
        return Mathf.Sqrt(dr*dr + dg*dg + db*db) <= tol;
    }
}
