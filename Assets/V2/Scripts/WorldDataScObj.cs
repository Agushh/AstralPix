using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.UIElements;
public class DeserializableChunk
{
    public int[,] blocksData;
    public int[,] collisionData;

    public DeserializableChunk(int[,] blocksData, int[,] collisionData)
    {
        this.blocksData = blocksData;
        this.collisionData = collisionData;
    }
}
[CreateAssetMenu(fileName = "WorldDataScObj", menuName = "Scriptable Objects/WorldDataScObj")]

public class WorldDataScObj : ScriptableObject
{
    

    [System.Serializable]
    private class SerializableChunk
    {
        public int[] blocksData;
        public int[] collisionData;
    }

    static int chunkSize = 32;

    Dictionary<long, string> chunkData = new();


    [SerializeField] private int seed;
    public int Seed
    {
        get => seed;
        set => seed = value;
    }

    public int ChunkSize => chunkSize;

    public Dictionary<long, string> ChunkData
    {
        get => chunkData;
        set => chunkData = value;
    } // hash -- path


    [SerializeField] float scale = 0.05f, maxHeight = 32;

    //get or create chunk
    public DeserializableChunk GetBlocksData(Vector2Int position)
    {
        long key = GetKey(position);

        if (!chunkData.ContainsKey(key))
        {
            string folder = Application.persistentDataPath + "/worldChunks-" + seed;
            string path = Path.Combine(folder, key + ".json");
            if (File.Exists(path))
                chunkData[key] = path;
        }

        if (!chunkData.ContainsKey(key))
            return new(GenerateAndSaveChunk(position, key), null);

        string json = File.ReadAllText(chunkData[key]);

        SerializableChunk chunk = JsonUtility.FromJson<SerializableChunk>(json);
        if(chunk.collisionData.Length != 0)
            return new(VecToMat(chunk.blocksData, chunkSize, chunkSize), VecToMat(chunk.collisionData, chunkSize, chunkSize));
        return new(VecToMat(chunk.blocksData, chunkSize, chunkSize), null);
    }

    public void UpdateChunk(Vector2Int position, int[,] blocks, int[,] collisionComp)
    {
        
        long key = GetKey(position);

        int[] blockData = MatToVec(blocks);

        int[] collisionData = MatToVec(collisionComp);

        SerializableChunk serializable = new SerializableChunk
        {
            collisionData = collisionData ?? Array.Empty<int>(),
            blocksData = blockData
        };

        string folder = Application.persistentDataPath + "/worldChunks-" + seed;
        Directory.CreateDirectory(folder);
        string path = Path.Combine(folder, key + ".json");
        string json = JsonUtility.ToJson(serializable, true);
        File.WriteAllText(path, json);

        //Mantener path actualizado
        chunkData[key] = path;
    }
     
    //Create and save chunk
    private int[,] GenerateAndSaveChunk(Vector2Int position, long key)
    {
        int[,] chunk = new int[ChunkSize, ChunkSize];

        for (int x = 0; x < ChunkSize; x++)
        {
            float height = Mathf.PerlinNoise(10000 + (position.x * ChunkSize + x) * scale, seed * 0.001f );
            for (int y = 0; y < ChunkSize; y++)
            {
                if (y + chunkSize * position.y < height * maxHeight -3)
                    chunk[x, y] = 3;
                else if (y + chunkSize * position.y < height * maxHeight - 1)
                    chunk[x,y ] = 1;
                else if (y + chunkSize * position.y < height * maxHeight)
                    chunk[x, y] = 2;
                else 
                    chunk[x, y] = 0;
            }
        }

        // Convertir a formato serializable
        int[] vec = MatToVec(chunk);
        SerializableChunk serializable = new SerializableChunk
        {
            collisionData = Array.Empty<int>(),
            blocksData = vec
        };

        // Guardar en disco
        string folder = Application.persistentDataPath + "/worldChunks-" + seed;
        Directory.CreateDirectory(folder);
        string path = Path.Combine(folder, key + ".json");
        string json = JsonUtility.ToJson(serializable, true);
        File.WriteAllText(path, json);

        // Registrar en diccionario
        chunkData[key] = path;

        return chunk;
    }

    #region Helpers
    private int[] MatToVec(int[,] mat)
    {
        int width = mat.GetLength(0);
        int height = mat.GetLength(1);

        int[] vec = new int[width * height];
        int index = 0;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                vec[index++] = mat[x, y];
            }
        }

        return vec;
    }
    private int[,] VecToMat(int[] vec, int width, int height)
    {
        Debug.Log("Vector : " + vec.Length + "  width * height :" + width * height);
        int[,] mat = new int[width, height];
        int index = 0;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                mat[x, y] = vec[index++];
            }
        }

        return mat;
    }
    private long GetKey(Vector2Int position)
    {
        // Asegúrate de que las coordenadas x e y sean tratadas como long de 64 bits.
        // Esto es crucial para el desplazamiento de bits (Bit Shifting).

        // 1. Mueve 'x' (32 bits) 32 posiciones a la izquierda, ocupando la mitad alta (bits 32-63) del long.
        long x_long = (long)position.x << 32;

        // 2. Mueve 'y' (32 bits) 0 posiciones, ocupando la mitad baja (bits 0-31) del long.
        long y_long = (long)position.y & 0xFFFFFFFF; // Usa AND para asegurar solo los 32 bits inferiores

        // 3. Usa OR (|) para combinar las dos mitades. ¡No hay colisiones!
        return x_long | y_long;
    }


    #endregion

}
