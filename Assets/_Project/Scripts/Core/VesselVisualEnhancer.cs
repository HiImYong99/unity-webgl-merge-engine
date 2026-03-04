using UnityEngine;

/// <summary>
/// 게임 컨테이너(용기)에 프리미엄 유리 디자인 자산을 적용합니다.
/// (배경 투명화 및 씬 정리 포함)
/// </summary>
public class VesselVisualEnhancer : MonoBehaviour
{
    private void Start()
    {
        ApplyPremiumVessel();
        // 런타임에 카메라나 배경이 복구되는 것을 방지하기 위해 주기적으로 체크 (강력한 투명화 유지)
        InvokeRepeating(nameof(AggressiveClear), 0.5f, 1.0f);
    }

    private void AggressiveClear()
    {
        // 1. 모든 카메라 투명화 및 파스텔 배경색 설정
        // 투명화(Alpha=0)가 웹에서 동작하지 않을 경우를 대비해 연한 파스텔 핑크/화이트를 기본 배경으로 사용합니다.
        Color premiumPastel = new Color(1.0f, 0.98f, 0.98f, 0.0f); // RGBA(255, 250, 250, 0)
        
        Camera[] cams = Object.FindObjectsOfType<Camera>();
        foreach (var c in cams)
        {
            if (c == null) continue;
            c.clearFlags = CameraClearFlags.SolidColor;
            c.backgroundColor = premiumPastel;
        }

        // 2. 혹시 살아난 배경 오브젝트들 있으면 모두 비활성화
        string[] bgNames = { 
            "Background", "ContainerBackground", "ProceduralBackground", "Environment", 
            "BG", "Back", "Sky", "Ground", "ForestBackground", "SceneBackground"
        };
        foreach (var name in bgNames)
        {
            GameObject bg = GameObject.Find(name);
            if (bg != null && bg.activeSelf) 
            {
                Debug.Log($"[VesselVisualEnhancer] Hiding background object: {name}");
                bg.SetActive(false);
            }
        }
    }

    public void ApplyPremiumVessel()
    {
        AggressiveClear();

        // 3. 지저분한 요소 제거 (UIFooter, Lattice)
        Transform footer = transform.Find("UIFooter");
        if (footer != null) footer.gameObject.SetActive(false);

        Transform tableBase = transform.Find("TableBase");
        if (tableBase != null)
        {
            Transform lattice = tableBase.Find("LatticeBackground");
            if (lattice != null) lattice.gameObject.SetActive(false);
            
            // 테이블 베이스 자체도 배경과 어우러지게 투명화
            SpriteRenderer tbsr = tableBase.GetComponent<SpriteRenderer>();
            if (tbsr != null) tbsr.color = new Color(1, 1, 1, 0.02f);
        }

        // 4. 벽면 디자인 (Clear Pastel Rose) 적용
        Color wallColor = new Color(1.0f, 0.92f, 0.95f, 0.58f); // 파스텔 로즈 톤 보강 및 불투명도 증가 (0.4 -> 0.58)

        foreach (Transform child in transform)
        {
            if (child.name.Contains("Wall") || child.name.Contains("Floor"))
            {
                Transform visual = child.Find("Visual") ?? child; // Visual 자식이 없으면 자신에게 적용
                if (visual != null)
                {
                    Renderer r = visual.GetComponent<Renderer>();
                    if (r != null)
                    {
                        // SpriteRenderer 면 .color, MeshRenderer 면 .material.color
                        if (r is SpriteRenderer sr) sr.color = wallColor;
                        else if (r.material != null) r.material.color = wallColor;
                    }
                }
            }
        }

        Debug.Log("[VesselVisualEnhancer] ✅ Aggressive background clearing applied.");
    }
}
