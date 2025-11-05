using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class WorldDataSerializable
{
    public int seed;
    public List<ChunkEntry> chunks; // lista serializable que reemplaza al diccionario
}

[System.Serializable]
public class ChunkEntry
{
    public long key;
    public string path;
}

public class WorldManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private WorldDataScObj worldData;
    public WorldDataScObj WorldData => worldData;
    [SerializeField] private Blockdictionary blockDictionary;
    [SerializeField] private GameObject chunkPrefab;

    [Header("Chunk Settings")]
    [SerializeField] private int loadRadius = 1;

    private Dictionary<Vector2Int, Chunk> activeChunks = new();
    private Vector2Int lastPlayerChunk;
    private Vector2Int currentPlayerChunk;

    [Header("Player")]
    [SerializeField] private Transform player;

    [Header("WorldOptions")]
    [SerializeField] int worldId = 0;

    float RuntimeSize;

    void Start()
    {
        worldData = loadWorld(worldId);

        UpdateLoadedChunks(GetPlayerChunkPosition());
    }

    private void Update()
    {
        currentPlayerChunk = GetPlayerChunkPosition();

        if (currentPlayerChunk != lastPlayerChunk)
        {
            UpdateLoadedChunks(currentPlayerChunk);
            lastPlayerChunk = currentPlayerChunk;
        }
    }

    public int getBlockOfChunk(Chunk actualChunk, Vector2Int neighChunk, Vector2Int position)
    {

        if (activeChunks.TryGetValue(neighChunk, out var chunk))
        {
            return chunk.GetBlockAtLocalPosition(position);
        }
        else
        {
            return 0;
        } 
            
    }

    #region Runtime functions
    void UpdateLoadedChunks(Vector2Int playerChunk)
    {
        HashSet<Vector2Int> chunksToKeep = new();
        List<Chunk> chunksQueNecesitanMalla = new();
        List<Chunk> vecinosQueNecesitanUpdate = new(); // Clave

        // --- PASE 1: Cargar DATA ---
        // En este pase, solo creamos los chunks y generamos sus datos (blocks[,]).
        // NO generamos sus mallas.
        for (int y = -loadRadius; y <= loadRadius; y++)
        {
            for (int x = -loadRadius; x <= loadRadius; x++)
            {
                Vector2Int pos = new Vector2Int(playerChunk.x + x, playerChunk.y + y);
                chunksToKeep.Add(pos);

                if (!activeChunks.ContainsKey(pos))
                {
                    // SpawnChunk DEBE ser modificado para que:
                    // 1. Cree el objeto Chunk.
                    // 2. Genere/cargue su array blocks[,]
                    // 3. NO llame a GenerateMesh() o UpdateMesh().
                    // 4. Devuelva la instancia del chunk.
                    Chunk newChunk = SpawnChunk(pos);
                    activeChunks.Add(pos, newChunk);

                    // Este chunk es nuevo, necesitará una malla.
                    chunksQueNecesitanMalla.Add(newChunk);

                    // IMPORTANTE: Sus vecinos AHORA existen, pero sus mallas
                    // están obsoletas (muestran un borde vacío donde ahora estás tú).
                    // Necesitamos encontrar a los vecinos que YA existían y marcarlos
                    // para un update.
                    vecinosQueNecesitanUpdate.AddRange(GetActiveNeighbors(pos));
                }
            }
        }

        // --- PASE 2: Generar MALLAS ---
        // Ahora que TODOS los chunks en el radio de carga existen y
        // tienen sus datos listos, podemos construir las mallas de forma segura.

        // Primero, actualiza los vecinos antiguos
        foreach (Chunk neighbor in vecinosQueNecesitanUpdate)
        {
            // Asegúrate de no actualizar un chunk que también es nuevo
            if (!chunksQueNecesitanMalla.Contains(neighbor))
            {
                neighbor.UpdateMesh(-1, 0, 0); // Actualiza Uvs.
                neighbor.UpdateCollider(); // Actualiza Collider.
            }
        }

        // Luego, construye las mallas de los chunks nuevos
        foreach (Chunk newChunk in chunksQueNecesitanMalla)
        {
            // UpdateMesh también construye la malla visual por primera vez
            newChunk.UpdateMesh(-1, 0, 0); 
            // UpdateCollider construye el collider por primera vez
            newChunk.UpdateCollider();
        }


        // --- PASE 3: Descargar Chunks ---
        // Esto debería estar en su propio bucle, separado de la lógica de carga.
        List<Vector2Int> toRemove = new();
        foreach (var kvp in activeChunks)
        {
            if (!chunksToKeep.Contains(kvp.Key))
            {
                toRemove.Add(kvp.Key);
            }
        }

        foreach (var pos in toRemove)
        {
            Destroy(activeChunks[pos].gameObject);
            activeChunks.Remove(pos);
        }

        // SaveWorld() probablemente no debería llamarse en cada Update,
        // sino periódicamente o al cerrar el juego.
    }

    // Función auxiliar que necesitarás
    List<Chunk> GetActiveNeighbors(Vector2Int chunkPos)
    {
        List<Chunk> neighbors = new List<Chunk>();
        Vector2Int[] neighborPos = {
        chunkPos + Vector2Int.up,
        chunkPos + Vector2Int.down,
        chunkPos + Vector2Int.left,
        chunkPos + Vector2Int.right
        // (Puedes añadir diagonales si tu autotiling las afecta)
    };

        foreach (var pos in neighborPos)
        {
            if (activeChunks.TryGetValue(pos, out Chunk neighbor))
            {
                neighbors.Add(neighbor);
            }
        }
        return neighbors;
    }
    Chunk SpawnChunk(Vector2Int chunkPos)
    {
        GameObject obj = Instantiate(chunkPrefab, ChunkToWorldPos(chunkPos), Quaternion.identity, transform);
        Chunk chunk = obj.GetComponent<Chunk>();

        chunk.name = $"Chunk {chunkPos.x},{chunkPos.y}";
        chunk.SetData(chunkPos, worldData, blockDictionary, this); 

        return chunk;
    }
    #endregion
    
    //player function
    public void UpdateChunk(Vector2Int chunkPos, Vector2Int cursor, int newBlock)
    {
        activeChunks[chunkPos].UpdateChunk(cursor, newBlock);
    }

    #region World Save/Load
    WorldDataScObj loadWorld(int worldSeed)
    {
        string path = GetWorldPath(worldSeed);
        if (!File.Exists(path))
        {
            return CreateWorld();
        }

        string json = File.ReadAllText(path);

        WorldDataSerializable data = JsonUtility.FromJson<WorldDataSerializable>(json);

        var worldData = ScriptableObject.CreateInstance<WorldDataScObj>();
        
        worldData.Seed = data.seed;
        worldData.ChunkData = new Dictionary<long, string>();
        foreach (var chunk in data.chunks)
        {
            worldData.ChunkData[chunk.key] = chunk.path;
        }

        return worldData;
    }

    WorldDataScObj CreateWorld()
    {
        WorldDataScObj data = ScriptableObject.CreateInstance<WorldDataScObj>();
        data.Seed = Random.Range(int.MinValue, int.MaxValue);
        data.ChunkData = new Dictionary<long, string>();
        return data;
    }

    void SaveWorld()
    {
        WorldDataSerializable serializable = new WorldDataSerializable
        {
            seed = worldData.Seed,
            chunks = new List<ChunkEntry>()
        };

        foreach (var kvp in worldData.ChunkData)
        {
            serializable.chunks.Add(new ChunkEntry { key = kvp.Key, path = kvp.Value });
        }
        string path = GetWorldPath(worldId);
        string json = JsonUtility.ToJson(serializable, true);
        File.WriteAllText(path, json);
        //Debug.Log($" Mundo {worldData.Seed} guardado en {path}");
    }
    #endregion

    #region Helpers
    string GetWorldPath(int worldId)
    {
        string folder = Path.Combine(Application.persistentDataPath, "worlds");
        Directory.CreateDirectory(folder);
        return Path.Combine(folder, worldId + ".json");
    }
    Vector3 ChunkToWorldPos(Vector2Int chunkPos)
    {
        return new Vector3(chunkPos.x * worldData.ChunkSize, chunkPos.y * worldData.ChunkSize, 0);
    }
    Vector2Int GetPlayerChunkPosition()
    {
        Vector2 pos = player.position;
        int cx = Mathf.FloorToInt(pos.x / worldData.ChunkSize);
        int cy = Mathf.FloorToInt(pos.y / worldData.ChunkSize);
        return new Vector2Int(cx, cy);
    }
    #endregion
}
