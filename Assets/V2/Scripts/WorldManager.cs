using System.Collections.Generic;
using UnityEngine;

public class WorldManager : MonoBehaviour
{
    [SerializeField] WorldService worldService;
    WorldMetaData worldMD;

    static int chunkSize = 32;

    [SerializeField] private GameObject chunkPrefab;

    [Header("Chunk Settings")]
    [SerializeField] private int loadRadius = 1;

    private Dictionary<Vector2Int, Chunk> activeChunks = new();

    private Dictionary<Vector2Int, ChunkData> cacheChunkData = new();

    private Vector2Int lastPlayerChunk;
    private Vector2Int currentPlayerChunk;
    public Vector2Int CurrentPlayerChunk => currentPlayerChunk;

    [Header("Player")]
    [SerializeField] private Transform player;

    [Header("WorldOptions")]
    [SerializeField] string worldId = "";
    public string WorldId => worldId;

    float RuntimeSize;


    [SerializeField] float timeIntervalToSave = 100f;
    float timer = 0;

    private void Awake()
    {
        LoadWorld(worldId);
    }

    void Start()
    {
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


        //Guardado en tiempo 
        timer += Time.deltaTime;
        if (timer >= timeIntervalToSave)
        {
            timer = 0f;
        }

    }

    void LoadWorld(string worldName)
    {
        worldMD = worldService.GetWorldMetaData(worldName);
        //Player.MoveToStartPosition(worldMD.playerPos); u otro
    }

    public int getBlockOfChunk(Vector2Int neighChunk, Vector2Int position)
    {
        // Caso 1: El chunk está activo y cargado. Perfecto.
        if (activeChunks.TryGetValue(neighChunk, out var chunk))
        {
            return chunk.GetBlockAtPosition(position);
        }

        // Caso 2: El chunk NO está activo.
        // NO DEVUELVAS 0. Carga los datos del disco y busca el bloque.
        ChunkData data = cacheChunkData.ContainsKey(position) ? cacheChunkData[neighChunk] : LoadChunkData(neighChunk, worldMD);


        // Devuelve el bloque correcto desde los datos del chunk
        return data.getBlockMatrix()[position.x, position.y];
    }

    #region Runtime functions
    void UpdateLoadedChunks(Vector2Int playerChunk)
    {
        // --- 1. Cargar nuevos chunks y marcar los que se quedan ---
        HashSet<Vector2Int> toKeep = new();

        for (int i = -loadRadius; i <= loadRadius; i++) // FIX: <=
        {
            for (int j = -loadRadius; j <= loadRadius; j++) // FIX: <=
            {
                // FIX: Calcular posición relativa al jugador
                Vector2Int chunkPos = new Vector2Int(playerChunk.x + i, playerChunk.y + j);
                toKeep.Add(chunkPos); // Marcar para MANTENER

                if (!activeChunks.ContainsKey(chunkPos))
                {
                    // Este chunk no está cargado, así que hay que cargarlo
                    ChunkData data = LoadChunkData(chunkPos, worldMD);
                    Chunk chunk = SpawnChunk(chunkPos, data);
                    activeChunks.Add(chunkPos, chunk);
                }
            }
        }

        // --- 2. Descargar chunks viejos (forma segura) ---
        List<Vector2Int> toRemove = new List<Vector2Int>();
        foreach (Vector2Int pos in activeChunks.Keys)
        {
            if (!toKeep.Contains(pos))
            {
                toRemove.Add(pos);
            }
        }

        foreach (Vector2Int pos in toRemove)
        {
            if (activeChunks[pos].IsDirty) SaveChunk(activeChunks[pos]);

            Destroy(activeChunks[pos].gameObject); // Destruir el GameObject
            activeChunks.Remove(pos); // Quitar del diccionario
            cacheChunkData.Remove(pos); // Quitar del caché
        }
    }

    //GENERATES OR LOAD A CACHE OF THE 8 ADJACENT CHUNKS
    private ChunkData LoadChunkData(Vector2Int pos, WorldMetaData wmd) 
    {
        ChunkData chunkData;
        for (int i = -1; i <= 1; i++)
        {
            for(int j = -1; j <= 1; j++)
            {
                if (i == 0 && j == 0) continue;
                Vector2Int neighborPos = new(pos.x + i, pos.y + j);
                
                if (cacheChunkData.ContainsKey(neighborPos)) continue;
                
                chunkData = worldService.GetChunk(wmd, neighborPos);
                cacheChunkData.TryAdd(neighborPos,chunkData);
            }
        }
        if (cacheChunkData.ContainsKey(pos))
            return cacheChunkData[pos];

        chunkData = worldService.GetChunk(wmd, pos);
        cacheChunkData.Add(pos, chunkData);
        
        return chunkData;
    }

    Chunk SpawnChunk(Vector2Int chunkPos, ChunkData chunkData)
    {
        GameObject obj = Instantiate(chunkPrefab, ChunkToWorldPos(chunkPos), Quaternion.identity, transform);
        Chunk chunk = obj.GetComponent<Chunk>();

        chunk.name = $"Chunk {chunkPos.x},{chunkPos.y}";
        chunk.SetData(chunkPos, worldMD, this, chunkData); 

        return chunk;
    }
    #endregion
    
    public void UpdateChunk(Vector2Int chunkPos, Vector2Int cursor, int newBlock)
    {
        if (!activeChunks.ContainsKey(chunkPos))
        {
            Debug.LogError("Mouse fuera de chunks");
            return;
        }
        activeChunks[chunkPos].PlaceBlock(cursor, newBlock);

        if(cursor.x == 0)
        {
            Vector2Int neigh = new(chunkPos.x - 1, chunkPos.y);
            activeChunks.TryGetValue(neigh, out var chunk);
            if (chunk != null) activeChunks[neigh].UpdateChunk();
        }
        else if(cursor.x == chunkSize -1)
        {
            Vector2Int neigh = new(chunkPos.x + 1, chunkPos.y);
            activeChunks.TryGetValue(neigh, out var chunk);
            if (chunk != null) activeChunks[neigh].UpdateChunk();
        }
        if (cursor.y == 0)
        {
            Vector2Int neigh = new(chunkPos.x , chunkPos.y - 1);
            activeChunks.TryGetValue(neigh, out var chunk);
            if (chunk != null) activeChunks[neigh].UpdateChunk();
        }
        else if (cursor.y == chunkSize - 1)
        {
            Vector2Int neigh = new(chunkPos.x , chunkPos.y + 1);
            activeChunks.TryGetValue(neigh, out var chunk);
            if (chunk != null) activeChunks[neigh].UpdateChunk();
        }

    }

    void SaveWorld()
    {
        foreach(Chunk chunk in activeChunks.Values)
        {
            SaveChunk(chunk);
        }
    }

    public void SaveChunk(Chunk chunk)
    {
        ChunkData data = new(chunk.Position, chunk.Blocks, chunk.Collisions);
        worldService.saveChunk(data, worldMD);
    }

    private void OnApplicationQuit()
    {
        SaveWorld();
    }


    #region Helpers
    Vector3 ChunkToWorldPos(Vector2Int chunkPos)
    {
        return new Vector3(chunkPos.x * chunkSize, chunkPos.y * chunkSize, 0);
    }
    public Vector2Int GetPlayerChunkPosition()
    {
        Vector2 pos = player.position;
        int cx = Mathf.FloorToInt(pos.x / chunkSize);
        int cy = Mathf.FloorToInt(pos.y / chunkSize);
        return new Vector2Int(cx, cy);
    }

    private string GetKey(Vector2Int pos)
    {
        return $"{pos.x}_{pos.y}";
    }


    #endregion
}
