using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class WorldManager : MonoBehaviour
{
    [SerializeField] WorldService worldService;
    [SerializeField] ChunkLightingManager lightingManager;
    WorldMetaData worldMD;

    static int chunkSize = 32;

    [SerializeField] private GameObject chunkPrefab;

    [Header("Chunk Settings")]
    [SerializeField] private int loadRadius = 1;

    private Dictionary<Vector2Int, ChunkManager> activeChunks = new();
    

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
        UpdateLoadedChunks(new(0,0)); 
        if (worldMD.lastPlayerPosition != Vector2.zero)
        {
            player.transform.position = worldMD.lastPlayerPosition;
        }
        if (worldMD.spawnPosition == Vector2.zero)
        {
            Vector2 spawnPosition = GetSpawnPosition();
            worldMD.spawnPosition = spawnPosition;
            player.transform.position = worldMD.spawnPosition;
        }

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

    Vector2 GetSpawnPosition()
    {
        Vector2 SpawnPos = Vector2.zero;
        foreach (ChunkManager chunk in activeChunks.Values)
        {
            int i = 0;
            int j = 0;
            while (i < chunkSize)
            {
                for(j = 0; j < chunkSize; j++)
                {
                    if (getBlockOfChunk(chunk.Position, new(j, i), true) == 0) break;
                }
                if(j < chunkSize) break;
                i++;
            }
            if (i < chunkSize)
            {
                SpawnPos = new(i, j);
                break;
            }
        }
        return SpawnPos;
    }
    void LoadWorld(string worldName)
    {
        worldMD = worldService.GetWorldMetaData(worldName);
    }

    public int getBlockOfChunk(Vector2Int neighChunk, Vector2Int position, bool isFrontBlock)
    {
        // Caso 1: El chunk está activo y cargado. Perfecto.
        if (activeChunks.TryGetValue(neighChunk, out var chunk))
        {
            return chunk.GetBlockAtPosition(position, isFrontBlock);
        }

        // Caso 2: El chunk NO está activo.
        // Carga los datos del disco y busca el bloque.
        ChunkData data = cacheChunkData.ContainsKey(neighChunk) ? cacheChunkData[neighChunk] : LoadChunkData(neighChunk, worldMD);


        // Devuelve el bloque correcto desde los datos del chunk
        return data.getBlock(position.x, position.y, isFrontBlock);
    }

    
    #region Runtime functions
    void UpdateLoadedChunks(Vector2Int playerChunk)
    {
        // --- 1. Cargar nuevos chunks y marcar los que se quedan ---
        HashSet<Vector2Int> toKeep = new();
        HashSet<Vector2Int> newChunks = new();

        for (int i = -loadRadius; i <= loadRadius; i++) 
        {
            for (int j = -loadRadius; j <= loadRadius; j++)
            {
                Vector2Int chunkPos = new Vector2Int(playerChunk.x + i, playerChunk.y + j);
                toKeep.Add(chunkPos); // Marcar para MANTENER

                if (!activeChunks.ContainsKey(chunkPos))
                {
                    ChunkData data = LoadChunkData(chunkPos, worldMD);
                    ChunkManager chunk = SpawnChunk(chunkPos, data);
                    activeChunks.Add(chunkPos, chunk);

                    //For load light in only one call for all the new chunks
                    newChunks.Add(chunkPos);
                }
            }
        }
        //Descargar chunks viejos (forma segura) --- Two Steps ---
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
            ChunkData updatedData;
            cacheChunkData.TryGetValue(pos, out updatedData);
            if (activeChunks[pos].IsDirty) updatedData = SaveChunk(activeChunks[pos]);

            Destroy(activeChunks[pos].gameObject); // Destruir el GameObject
            activeChunks.Remove(pos); // Quitar del diccionario
            cacheChunkData[pos] = updatedData; // Actualizar cache
        }

        lightingManager.SetRenderedChunks(activeChunks);
        lightingManager.UpdateLight(newChunks);
    }

    //GENERATES OR LOAD A CACHE OF THE 8 ADJACENT CHUNKSDATAS
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

    ChunkManager SpawnChunk(Vector2Int chunkPos, ChunkData chunkData)
    {
        GameObject obj = Instantiate(chunkPrefab, ChunkToWorldPos(chunkPos), Quaternion.identity, transform);
        ChunkManager chunk = obj.GetComponent<ChunkManager>();

        chunk.name = $"Chunk {chunkPos.x},{chunkPos.y}";
        chunk.SetData(chunkPos, worldMD, this, chunkData);

        return chunk;
    }
    #endregion

    public void PlaceBlock(Vector2Int chunkPos, Vector2Int cursor, int newBlock, bool isFront)
    {
        if (!activeChunks.ContainsKey(chunkPos))
        {
            Debug.LogError("Mouse fuera de chunks");
            return;
        }
        //Place block
        activeChunks[chunkPos].PlaceBlock(cursor, newBlock, isFront);


        lightingManager.UpdateLight(chunkPos);

        //Update neighboring chunks if the interaction was on edge
        if (cursor.x == 0)
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


    #region lighting

    public void SendLightMap(Vector2Int chunkPos, Color[,] center, Color[] top, Color[] bottom, Color[] left, Color[] right)
    {
        if(activeChunks.TryGetValue(chunkPos, out var chunk))
        {
            chunk.UpdateLight(center, top, bottom, left, right);
        }
    }

    #endregion

    void SaveWorld()
    {
        foreach(ChunkManager chunk in activeChunks.Values)
        {
            SaveChunk(chunk);
        }
        worldMD.lastPlayerPosition = player.transform.position;
        worldService.SaveWorldMetaData(worldMD);
    }

    public ChunkData SaveChunk(ChunkManager chunk)
    {
        ChunkData data = new(chunk.Position, chunk.Blocks, chunk.BackBlocks, chunk.Collisions, chunk.SurfaceHeight, chunk.isAir);
        worldService.saveChunk(data, worldMD);
        return data;
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

    static Vector2Int AdjustCoordinates(Vector2Int position)
    {
        int x = position.x;
        int y = position.y;

        if (x >= chunkSize) x -= chunkSize;
        else if (x < 0) x += chunkSize;

        if (y >= chunkSize) y -= chunkSize;
        else if (y < 0) y += chunkSize;

        return new Vector2Int(x, y);
    }
    #endregion
}
