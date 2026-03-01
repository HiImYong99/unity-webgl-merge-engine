using UnityEngine;
using System.Collections;

public class Dessert : MonoBehaviour
{
    public int Level { get; private set; }
    public bool IsMerged { get; private set; } = false;
    public bool IsDropped { get { return isDropped; } }
    private bool isDropped = false;

    private void Awake()
    {
        gameObject.tag = "Dessert";
    }

    private void Start()
    {
        if (PhysicsManager.Instance != null)
        {
            Collider2D col = GetComponent<Collider2D>();
            if (col != null && col.sharedMaterial == null)
            {
                col.sharedMaterial = PhysicsManager.Instance.GetDefaultMaterial();
            }
        }
    }

    // Update logic moved to GameManager for centralized tracking

    public void Initialize(int level, bool dropped)
    {
        Level = level;
        isDropped = dropped;

        if (GameManager.Instance != null && GameManager.Instance.EvolutionData != null)
        {
            DessertEvolutionData data = GameManager.Instance.EvolutionData;
            if (level <= data.Levels.Length)
            {
                float scaleMulti = data.Levels[level - 1].ScaleMultiplier;
                transform.localScale = new Vector3(scaleMulti, scaleMulti, 1f);
            }
        }

        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.gravityScale = isDropped ? 1f : 0f;
        }
    }

    public void SetDropped(bool state)
    {
        isDropped = state;
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (IsMerged) return;

        Dessert other = collision.gameObject.GetComponent<Dessert>();
        if (other != null && other.Level == Level && !other.IsMerged)
        {
            // Determine who merges into who based on ID or Position
            if (gameObject.GetInstanceID() > other.gameObject.GetInstanceID())
            {
                Merge(other);
            }
        }
    }

    private void Merge(Dessert other)
    {
        if (GameManager.Instance == null || GameManager.Instance.EvolutionData == null) return;

        DessertEvolutionData data = GameManager.Instance.EvolutionData;
        if (Level >= data.Levels.Length) return; // Max level reached

        IsMerged = true;
        other.IsMerged = true;

        Vector3 spawnPos = (transform.position + other.transform.position) / 2f;
        int nextLevel = Level + 1;

        // Score Calculation
        int score = data.Levels[Level - 1].ScorePoint;
        GameManager.Instance.AddScore(score, nextLevel);

        // Instantiate Next Level
        GameObject nextPrefab = data.Levels[nextLevel - 1].Prefab;
        if (nextPrefab != null)
        {
            GameObject newDessert = Instantiate(nextPrefab, spawnPos, Quaternion.identity);
            Dessert newDessertScript = newDessert.GetComponent<Dessert>();
            if (newDessertScript != null)
            {
                newDessertScript.Initialize(nextLevel, true);
            }
        }

        // Add some fancy effects here (DOTween / Particles) if available

        Destroy(gameObject);
        Destroy(other.gameObject);
    }
}
