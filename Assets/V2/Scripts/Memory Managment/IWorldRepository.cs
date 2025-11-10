using UnityEngine;

public interface IWorldRepository
{
    // METODOS DE METADATA
    void SaveWorldMetaData(WorldMetaData wmd);
    WorldMetaData LoadWorldMetaData(string WorldName);
    bool DoesWorldExist(string worldName);

    //METODOS DE CHUNKS 

    void SaveChunk(ChunkData chunk, string worldName);
    ChunkData LoadChunk(Vector2Int chunkPosition, string worldName);
    bool DoesChunkExist(Vector2Int chunkPosition, string worldName);

}
