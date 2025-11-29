using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;


[System.Serializable]
public class ChunkData
{
    //  --  DATA  --  

    //Serialization Key - ID
    public int posX, posY;

    public int[] blocks;

    public int[] backBlocks;

    public int[] surfaceHeight;

    public List<Vector2[]> colliderPaths;

    public bool isAir;

    private const int CHUNK_SIZE = 32;

    public ChunkData() { }

    public ChunkData(Vector2Int pos, int[,] blockMatrix, int[,] backBlockMatrix, int[] surfaceHeight, bool isAir, List<Vector2[]> colliders = null)
    {
        posX = pos.x;
        posY = pos.y;

        // Aplanamos las matrices entrantes inmediatamente
        blocks = MatToVec(blockMatrix);
        backBlocks = MatToVec(backBlockMatrix);

        this.surfaceHeight = surfaceHeight;
        this.isAir = isAir;
        this.colliderPaths = colliders ?? new List<Vector2[]>();
    }

    public Vector2Int GetPosition()
    {
        return new Vector2Int(posX, posY);
    }

    public int[,] GetBlockMatrix()
    {
        return VecToMat(blocks);
    }

    public int[,] GetBackBlocksMatrix()
    {
        return VecToMat(backBlocks);
    }
    public int GetBlock(int x, int y, bool isFrontBlock)
    {
        if (x < 0 || x >= CHUNK_SIZE || y < 0 || y >= CHUNK_SIZE) return 0; // Protección de límites
        int index = x + (y * CHUNK_SIZE);
        return isFrontBlock ? blocks[index] : backBlocks[index];
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


    private int[,] VecToMat(int[] vec)
    {
        int[,] mat = new int[CHUNK_SIZE, CHUNK_SIZE];
        int index = 0;

        for (int y = 0; y < CHUNK_SIZE; y++)
        {
            for (int x = 0; x < CHUNK_SIZE; x++)
            {
                mat[x, y] = vec[index++];
            }
        }

        return mat;
    }

}
