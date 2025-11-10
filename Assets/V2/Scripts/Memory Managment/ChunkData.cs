using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;


[System.Serializable]
public class ChunkData
{
    //  --  DATA  --  

    //Serialization Key - ID
    public string positionKey;

    public int[] blocks;

    public List<Vector2[]> colliderPaths;


    public int[,] getBlockMatrix()
    {
        return VecToMat(blocks, 32, 32);
    }


    // --  METHODS  --  
    public ChunkData(Vector2Int pos, int[,] blockArray)
    {
        positionKey = $"{pos.x}_{pos.y}";
        blocks = MatToVec(blockArray);
    }
    public ChunkData(Vector2Int pos, int[,] blockArray, List<Vector2[]> collider)
    {
        positionKey = $"{pos.x}_{pos.y}";
        blocks = MatToVec(blockArray);
        colliderPaths = collider;
    }

    public Vector2Int GetPosition()
    {
        string[] parts = positionKey.Split('_');
        return new Vector2Int(
            int.Parse(parts[0]),
            int.Parse(parts[1])
        );
    }

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

}
