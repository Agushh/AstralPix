using System.Collections.Generic;
using UnityEngine;

public class WorldService : MonoBehaviour
{
    //a posterior cambiar por interface
    [SerializeField] FileWorldRepository fileWorldRepository;

    static int chunkSize = 32;

    //Procedural Generation
    [SerializeField] float scale = 0.05f, maxHeight = 32;


    //Create and save chunk
    private int[,] GenerateChunk(Vector2Int position, long seed)
    {
        int[,] chunk = new int[chunkSize, chunkSize];

        for (int x = 0; x < chunkSize; x++)
        {
            float height = Mathf.PerlinNoise(10000 + (position.x * chunkSize + x) * scale, seed * 0.001f);
            for (int y = 0; y < chunkSize; y++)
            {
                if (y + chunkSize * position.y < height * maxHeight - 3)
                    chunk[x, y] = 3;
                else if (y + chunkSize * position.y < height * maxHeight - 1)
                    chunk[x, y] = 1;
                else if (y + chunkSize * position.y < height * maxHeight)
                    chunk[x, y] = 2;
                else
                    chunk[x, y] = 0;
            }
        }

        return chunk;
    }

    private int generateSeed()
    {
        return Random.Range(int.MinValue, int.MaxValue);
    }

    public ChunkData GetChunk(WorldMetaData wmd, Vector2Int pos)
    {
        if (fileWorldRepository.DoesChunkExist(pos, wmd.worldName))
            return fileWorldRepository.LoadChunk(pos, wmd.worldName);

        ChunkData newChunk = new(pos, GenerateChunk(pos, wmd.seed));
        fileWorldRepository.SaveChunk(newChunk, wmd.worldName);
        return newChunk;
    }

    public WorldMetaData GetWorldMetaData(string name)
    {
        if (fileWorldRepository.DoesWorldExist(name))
            return fileWorldRepository.LoadWorldMetaData(name);
        return new(name, generateSeed());
    }

    public void saveChunk(ChunkData chunk, WorldMetaData wmd)
    {
        fileWorldRepository.SaveChunk(chunk, wmd.worldName);
    }

}
