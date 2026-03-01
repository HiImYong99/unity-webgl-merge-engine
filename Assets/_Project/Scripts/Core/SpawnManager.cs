using UnityEngine;
using UnityEngine.EventSystems;

public class SpawnManager : MonoBehaviour
{
    public static SpawnManager Instance { get; private set; }

    public Transform SpawnPoint;
    public float SpawnCooldown = 1.0f;
    public bool CanSpawn = true;

    private GameObject currentDessert;
    private DessertEvolutionData evolutionData;
    private int nextDessertLevel = 1;

    private float minX = -2.5f;
    private float maxX = 2.5f;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        if (GameManager.Instance != null)
        {
            evolutionData = GameManager.Instance.EvolutionData;
        }

        PrepareNextDessert();
    }

    private void Update()
    {
        if (GameManager.Instance != null && GameManager.Instance.IsGameOver) return;
        if (!CanSpawn || currentDessert == null) return;

        // Follow Touch or Mouse
        Vector3 inputPos = Vector3.zero;
        bool isInput = false;

        // Check UI interaction
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            // Ignore if touching UI, except if we are releasing the mouse we might still want to clear the state without dropping.
            // A simple implementation for MVP: ignore inputs over UI.
            return;
        }

        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);

            // For touches, IsPointerOverGameObject needs fingerId
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(touch.fingerId))
            {
                return;
            }

            inputPos = Camera.main.ScreenToWorldPoint(touch.position);
            isInput = true;

            if (touch.phase == TouchPhase.Ended)
            {
                DropDessert();
                isInput = false;
            }
        }
        else if (Input.GetMouseButton(0))
        {
            inputPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            isInput = true;
        }
        else if (Input.GetMouseButtonUp(0))
        {
            DropDessert();
            isInput = false;
        }

        if (isInput && currentDessert != null)
        {
            float targetX = Mathf.Clamp(inputPos.x, minX, maxX);
            currentDessert.transform.position = new Vector3(targetX, SpawnPoint.position.y, 0f);
        }
    }

    private void PrepareNextDessert()
    {
        if (evolutionData == null || evolutionData.Levels == null || evolutionData.Levels.Length == 0) return;

        // Determine level (1 to 3 randomly for MVP)
        nextDessertLevel = Random.Range(1, 4);

        if (UIManager.Instance != null)
        {
            UIManager.Instance.UpdateNextGuide(nextDessertLevel);
        }

        SpawnDessertAtCursor(nextDessertLevel);
    }

    private void SpawnDessertAtCursor(int level)
    {
        if (evolutionData == null || level > evolutionData.Levels.Length) return;

        GameObject prefab = evolutionData.Levels[level - 1].Prefab;
        if (prefab != null)
        {
            currentDessert = Instantiate(prefab, SpawnPoint.position, Quaternion.identity);

            // Disable physics while holding
            Rigidbody2D rb = currentDessert.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.isKinematic = true;
                rb.velocity = Vector2.zero;
            }

            Dessert dessertScript = currentDessert.GetComponent<Dessert>();
            if (dessertScript != null)
            {
                dessertScript.Initialize(level, false);
            }
        }
    }

    private void DropDessert()
    {
        if (currentDessert == null) return;

        Rigidbody2D rb = currentDessert.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.isKinematic = false;
            rb.gravityScale = 1f; // Standard gravity
        }

        Dessert dessertScript = currentDessert.GetComponent<Dessert>();
        if (dessertScript != null)
        {
            dessertScript.SetDropped(true);
        }

        currentDessert = null;
        CanSpawn = false;

        Invoke(nameof(ResetSpawn), SpawnCooldown);
    }

    private void ResetSpawn()
    {
        CanSpawn = true;
        PrepareNextDessert();
    }
}
