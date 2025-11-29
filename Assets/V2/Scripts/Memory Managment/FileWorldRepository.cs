using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class FileWorldRepository : MonoBehaviour, IWorldRepository
{
    // --- Rutas de Guardado ---
    private string GetWorldBasePath(string worldName) => Path.Combine(Application.persistentDataPath, "worlds", worldName);
    private string GetWorldInfoPath(string worldName) => Path.Combine(GetWorldBasePath(worldName), "world.json"); // Metadata general JSON
    private string GetChunksPath(string worldName) => Path.Combine(GetWorldBasePath(worldName), "chunks");
    private string GetChunkPath(Vector2Int pos, string worldName)
    {
        return Path.Combine(GetChunksPath(worldName), $"{pos.x}_{pos.y}.bin");
    }
    public bool DoesWorldExist(string worldName) => File.Exists(GetWorldInfoPath(worldName));
    public bool DoesChunkExist(Vector2Int pos, string worldName) => File.Exists(GetChunkPath(pos, worldName));

    // El archivo para un chunk
    

    //Implementaciones de guaardado 

    public void SaveWorldMetaData(WorldMetaData data)
    {
        string path = GetWorldInfoPath(data.worldName);
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllText(path, JsonUtility.ToJson(data, true));
    }

    public WorldMetaData LoadWorldMetaData(string worldName)
    {
        string path = GetWorldInfoPath(worldName);
        if (!File.Exists(path)) return null;
        return JsonUtility.FromJson<WorldMetaData>(File.ReadAllText(path));
    }


    // --- Implementación de Chunks (LO IMPORTANTE) ---

    public void SaveChunk(ChunkData data, string worldName)
    {
        string path = GetChunkPath(new Vector2Int(data.posY, data.posY), worldName);
        Directory.CreateDirectory(Path.GetDirectoryName(path));

        // Usamos FileStream y BinaryWriter
        using (FileStream stream = new FileStream(path, FileMode.Create))
        using (BinaryWriter writer = new BinaryWriter(stream))
        {
            // 1. Posición
            writer.Write(data.posX);
            writer.Write(data.posY);

            // 2. Is Air
            writer.Write(data.isAir);

            // 3. Blocks (Guardamos longitud por seguridad, luego los datos)
            writer.Write(data.blocks.Length);
            foreach (int blockId in data.blocks) writer.Write(blockId);

            // 4. BackBlocks
            writer.Write(data.backBlocks.Length);
            foreach (int blockId in data.backBlocks) writer.Write(blockId);

            // 5. Surface Height
            if (data.surfaceHeight != null)
            {
                writer.Write(data.surfaceHeight.Length);
                foreach (int h in data.surfaceHeight) writer.Write(h);
            }
            else
            {
                writer.Write(0);
            }

            // 6. Collider Paths (Complejo: Lista de Arrays de Vector2)
            if (data.colliderPaths != null)
            {
                writer.Write(data.colliderPaths.Count); // Cuántos paths hay
                foreach (var pathArray in data.colliderPaths)
                {
                    writer.Write(pathArray.Length); // Cuántos puntos tiene este path
                    foreach (Vector2 point in pathArray)
                    {
                        writer.Write(point.x);
                        writer.Write(point.y);
                    }
                }
            }
            else
            {
                writer.Write(0);
            }
        }
    }

    public ChunkData LoadChunk(Vector2Int chunkPosition, string worldName)
    {
        string path = GetChunkPath(chunkPosition, worldName);
        if (!File.Exists(path)) return null;

        ChunkData data = new ChunkData();

        using (FileStream stream = new FileStream(path, FileMode.Open))
        using (BinaryReader reader = new BinaryReader(stream))
        {
            // LEER EN EL MISMO ORDEN EXACTO QUE SE GUARDÓ

            // 1. Posición
            data.posX = reader.ReadInt32();
            data.posY = reader.ReadInt32();

            // 2. Is Air
            data.isAir = reader.ReadBoolean();

            // 3. Blocks
            int blocksLength = reader.ReadInt32();
            data.blocks = new int[blocksLength];
            for (int i = 0; i < blocksLength; i++) data.blocks[i] = reader.ReadInt32();

            // 4. BackBlocks
            int backBlocksLength = reader.ReadInt32();
            data.backBlocks = new int[backBlocksLength];
            for (int i = 0; i < backBlocksLength; i++) data.backBlocks[i] = reader.ReadInt32();

            // 5. Surface Height
            int surfaceLength = reader.ReadInt32();
            data.surfaceHeight = new int[surfaceLength];
            for (int i = 0; i < surfaceLength; i++) data.surfaceHeight[i] = reader.ReadInt32();

            // 6. Collider Paths
            int pathsCount = reader.ReadInt32();
            data.colliderPaths = new List<Vector2[]>(pathsCount);

            for (int i = 0; i < pathsCount; i++)
            {
                int pointCount = reader.ReadInt32();
                Vector2[] pathPoints = new Vector2[pointCount];
                for (int j = 0; j < pointCount; j++)
                {
                    float x = reader.ReadSingle(); // float se lee como Single
                    float y = reader.ReadSingle();
                    pathPoints[j] = new Vector2(x, y);
                }
                data.colliderPaths.Add(pathPoints);
            }
        }

        return data;
    }

}
