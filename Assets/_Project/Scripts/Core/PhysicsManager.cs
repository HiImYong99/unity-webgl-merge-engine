using UnityEngine;

public class PhysicsManager : MonoBehaviour
{
    public static PhysicsManager Instance { get; private set; }

    [Header("Physics Settings")]
    public float Bounciness = 0.2f;
    public float Friction = 0.4f;

    private PhysicsMaterial2D defaultMaterial;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        defaultMaterial = new PhysicsMaterial2D("DessertMaterial");
        defaultMaterial.bounciness = Bounciness;
        defaultMaterial.friction = Friction;

        // Apply globally to newly created colliders by default if needed,
        // but normally this is applied via Inspector to prefabs.
    }

    public PhysicsMaterial2D GetDefaultMaterial()
    {
        return defaultMaterial;
    }
}
