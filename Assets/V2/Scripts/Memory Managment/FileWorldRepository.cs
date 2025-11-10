using System.IO;
using UnityEngine;

public class FileWorldRepository : MonoBehaviour, IWorldRepository
{
    // --- Rutas de Guardado ---
    private string GetWorldBasePath(string worldName)
    {
        return Path.Combine(Application.persistentDataPath, "worlds", worldName);
    }

    // El archivo de metadata
    private string GetWorldInfoPath(string worldName)
    {
        return Path.Combine(GetWorldBasePath(worldName), "world.json");
    }

    // La carpeta para los chunks
    private string GetChunksPath(string worldName)
    {
        return Path.Combine(GetWorldBasePath(worldName), "chunks");
    }

    // El archivo para un chunk
    private string GetChunkPath(Vector2Int pos, string worldName)
    {
        string fileName = $"{pos.x}_{pos.y}.chunk"; // .chunk o .json
        return Path.Combine(GetChunksPath(worldName), fileName);
    }

    //Implementaciones de guaardado 

    public void SaveWorldMetaData(WorldMetaData data)
    {
        string path = GetWorldInfoPath(data.worldName);
        Directory.CreateDirectory(Path.GetDirectoryName(path)); // Crea .../worlds/MiMundo/

        string json = JsonUtility.ToJson(data, true);
        File.WriteAllText(path, json);
    }

    public WorldMetaData LoadWorldMetaData(string worldName)
    {
        string path = GetWorldInfoPath(worldName);
        if (!File.Exists(path)) return null;

        string json = File.ReadAllText(path);
        return JsonUtility.FromJson<WorldMetaData>(json);
    }

    public bool DoesWorldExist(string worldName)
    {
        return File.Exists(GetWorldInfoPath(worldName));
    }

    // --- Implementación de Chunks (LO IMPORTANTE) ---

    public void SaveChunk(ChunkData data, string worldName)
    {
        string path = GetChunkPath(data.GetPosition(), worldName);
        // Aseguramos que la carpeta .../chunks/ exista
        Directory.CreateDirectory(Path.GetDirectoryName(path));

        // Aquí podrías usar JSON, pero para arrays grandes es mejor binario.
        // Por simplicidad, usemos JSON. Para optimizar, usaríamos BinaryWriter.
        string json = JsonUtility.ToJson(data);
        File.WriteAllText(path, json);
    }

    public ChunkData LoadChunk(Vector2Int chunkPosition, string worldName)
    {
        string path = GetChunkPath(chunkPosition, worldName);
        if (!File.Exists(path))
        {
            // ¡Esto NO es un error! Significa que el chunk es virgen.
            return null;
        }

        string json = File.ReadAllText(path);
        return JsonUtility.FromJson<ChunkData>(json);
    }

    public bool DoesChunkExist(Vector2Int chunkPosition, string worldName)
    {
        return File.Exists(GetChunkPath(chunkPosition, worldName));
    }
}
