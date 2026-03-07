using UnityEngine;

[CreateAssetMenu(fileName = "AnimalEvolutionData", menuName = "AnimalPop/Animal Evolution Data", order = 1)]
public class AnimalEvolutionData : ScriptableObject
{
    [System.Serializable]
    public struct AnimalLevelData
    {
        public int Level;
        public string Name;
        public string NameKR;      // Korean Display Name
        public string FlavorText;   // Korean Description
        public float ScaleMultiplier;
        public int ScorePoint;
        public GameObject Prefab;
    }

    public string Version = "1.0";
    public AnimalLevelData[] Levels;
}
