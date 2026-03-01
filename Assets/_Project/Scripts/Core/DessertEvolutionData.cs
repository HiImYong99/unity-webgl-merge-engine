using UnityEngine;

[CreateAssetMenu(fileName = "DessertEvolutionData", menuName = "DessertPop/Dessert Evolution Data", order = 1)]
public class DessertEvolutionData : ScriptableObject
{
    [System.Serializable]
    public struct DessertLevelData
    {
        public int Level;
        public string Name;
        public float ScaleMultiplier;
        public int ScorePoint;
        public GameObject Prefab;
    }

    public DessertLevelData[] Levels;
}
