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

    public int[] backBlocks;

    public int[] surfaceHeight;

    public List<Vector2[]> colliderPaths;

    public bool isAir;

    public int[,] getBlockMatrix()
    {
        return VecToMat(blocks, 32, 32);
    }
    public int getBlock(int x, int y ,bool isFrontBlock)
    {
        return isFrontBlock ? blocks[x + y * 32] : backBlocks[x + y * 32];
    }
    public int[,] getBackBlocksMatrix()
    {
        return VecToMat(backBlocks, 32, 32);
    }

    // --  METHODS  --  
    public ChunkData(Vector2Int pos, int[,] blockArray, int[,] backBlocks, int[] surfaceHeight, bool isAir)
    {
        positionKey = $"{pos.x}_{pos.y}";
        blocks = MatToVec(blockArray);
        this.backBlocks = MatToVec(backBlocks);
        this.surfaceHeight = surfaceHeight;
        this.isAir = isAir;
    }
    public ChunkData(Vector2Int pos, int[,] blockArray, int[,] backBlocks, List<Vector2[]> collider, int[] surfaceHeight, bool isAir)
    {
        positionKey = $"{pos.x}_{pos.y}";
        blocks = MatToVec(blockArray);
        colliderPaths = collider;
        this.backBlocks = MatToVec(backBlocks);
        this.surfaceHeight = surfaceHeight;
        this.isAir = isAir;
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
