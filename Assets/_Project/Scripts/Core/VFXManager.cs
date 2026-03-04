using UnityEngine;

public class VFXManager : MonoBehaviour
{
    public static VFXManager Instance { get; private set; }

    [Header("Merge Effects")]
    public GameObject MergeNormalPrefab;
    public GameObject MergePremiumPrefab;
    public GameObject MergeLegendaryPrefab;

    [Header("Pop & Spawn")]
    public GameObject PopPrefab;
    public GameObject SpawnPrefab;
    public GameObject LandingPrefab;

    [Header("Particles")]
    public GameObject StarParticles;
    public GameObject HeartParticles;
    public GameObject CrumbParticles;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public void SpawnMergeEffect(Vector3 position, int level)
    {
        GameObject prefab = MergeNormalPrefab;
        if (level >= 8) prefab = MergeLegendaryPrefab;
        else if (level >= 4) prefab = MergePremiumPrefab;

        if (prefab != null)
        {
            Instantiate(prefab, position, Quaternion.identity);
        }
    }

    public void SpawnPopEffect(Vector3 position)
    {
        if (PopPrefab != null)
            Instantiate(PopPrefab, position, Quaternion.identity);
    }

    public void SpawnSpawnEffect(Vector3 position)
    {
        if (SpawnPrefab != null)
            Instantiate(SpawnPrefab, position, Quaternion.identity);
    }

    public void SpawnLandingEffect(Vector3 position)
    {
        if (LandingPrefab != null)
            Instantiate(LandingPrefab, position, Quaternion.identity);
    }

    public void SpawnParticles(Vector3 position, string type)
    {
        GameObject prefab = null;
        switch (type.ToLower())
        {
            case "star": prefab = StarParticles; break;
            case "heart": prefab = HeartParticles; break;
            case "crumb": prefab = CrumbParticles; break;
        }

        if (prefab != null)
            Instantiate(prefab, position, Quaternion.identity);
    }

    public void SpawnScoreEffect(Vector3 position, int score)
    {
        GameObject go = new GameObject("FloatingScore");
        go.transform.position = position;
        var fs = go.AddComponent<FloatingScore>();
        fs.Initialize(score);
    }
}
