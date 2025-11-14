using UnityEngine;

[System.Serializable]
public class WorldMetaData
{
    public string worldName;
    public float seed;
    public Vector2 spawnPosition;
    public Vector2 lastPlayerPosition;

    public WorldMetaData(string worldName, float seed, Vector2 spawnPosition, Vector2 lastPlayerPosition)
    {
        this.worldName = worldName;
        this.seed = seed;
        this.lastPlayerPosition = lastPlayerPosition;
    }
}
