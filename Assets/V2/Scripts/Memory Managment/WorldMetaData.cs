using UnityEngine;

[System.Serializable]
public class WorldMetaData
{
    public string worldName;
    public long seed;

    public WorldMetaData(string worldName, long seed)
    {
        this.worldName = worldName;
        this.seed = seed;
    }
}
