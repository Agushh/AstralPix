using UnityEngine;

public class WorldService : MonoBehaviour
{
    //a posterior cambiar por interface
    [SerializeField] FileWorldRepository fileWorldRepository;

    static int chunkSize = 32;

    //Procedural Generation
    [Header("Simple World Generation")]
    [SerializeField] float scale = 0.05f, maxHeightSimple = 32;

    [Header("Fractal Complex World Generation")]
    [SerializeField] float maxHeightComplex = 128;
    [SerializeField] float minHeight = 1;
    [SerializeField] float frequency = 0.005f;
    [SerializeField] float maxAmplitude = 100;
    [SerializeField] float minAmplitude = 1;
    [SerializeField] float divisionForContinentalness = 300;
    [SerializeField] float octaves = 5;
    [SerializeField] float lacunarity = 2;
    [SerializeField] float persistence = 0.5f;

    [SerializeField] int TerrainHeight = 128;

    [Header("Switch Generation :")]
    [SerializeField] generationType genType;

    enum generationType
    {
        simple, fractal
    }

    //Create and save chunk
    private int[,] SimpleProceduralGeneration(Vector2Int position, float seed)
    {
        int[,] chunk = new int[chunkSize, chunkSize];

        for (int x = 0; x < chunkSize; x++)
        {
            float height = Mathf.PerlinNoise(1000 + (position.x * chunkSize + x) * scale, seed * 0.001f);
            for (int y = 0; y < chunkSize; y++)
            {
                if (y + chunkSize * position.y < height * maxHeightSimple - 4)
                    chunk[x, y] = 3;
                else if (y + chunkSize * position.y < height * maxHeightSimple - 2)
                    chunk[x, y] = 1;
                else if (y + chunkSize * position.y < height * maxHeightSimple)
                    chunk[x, y] = 2;
                else
                    chunk[x, y] = 0;
            }
        }

        return chunk;
    }

    private int[,] ComplexProceduralGeneration(Vector2Int position, float seed)
    {
        int[,] chunk = new int[chunkSize, chunkSize];

        int[] fractalNoiseHeight = FractalNoise(chunkSize * position.x, seed);

        for (int x = 0; x < chunkSize; x++)
        {
            for (int y = 0; y < chunkSize; y++)
            {
                if (y + chunkSize * position.y < fractalNoiseHeight[x]- 7)
                    chunk[x, y] = 3;
                else if (y + chunkSize * position.y < fractalNoiseHeight[x] - 2)
                    chunk[x, y] = 1;
                else if (y + chunkSize * position.y < fractalNoiseHeight[x])
                    chunk[x, y] = 2;
                else
                    chunk[x, y] = 0;
            }
        }
        return chunk;
    }
    private int[] FractalNoise(int offsetX, float seed)
    {
        float[] AmplitudeArray = continentalness1D(offsetX, seed);
        int[] grid = new int[chunkSize];
        for (int j = 0; j < chunkSize; j++)
        {
            float elevation = 0;
            float TFrequency = frequency;
            float TAmplitude = AmplitudeArray[j];
            for (int k = 0; k < octaves; k++)
            {
                float sampleX = (offsetX + j ) * TFrequency;
                elevation += Mathf.PerlinNoise(sampleX, seed) * TAmplitude;
                TFrequency *= lacunarity;
                TAmplitude *= persistence;
            }
            elevation = Mathf.Clamp(Mathf.Round(elevation), minHeight, maxHeightComplex);

            grid[j] = (int) elevation + chunkSize - TerrainHeight;
        }
        return grid;
    }

    private float[] continentalness1D(int offsetX, float seed)
    {
        float[] continentalNoiseMap = new float[chunkSize];
        float[] frequencyNoiseMap = new float[chunkSize];
        for (int i = 0; i < chunkSize; i++)
        {
            continentalNoiseMap[i] = Mathf.PerlinNoise((i + offsetX) / divisionForContinentalness, seed + 1000);
            frequencyNoiseMap[i] = minAmplitude + continentalNoiseMap[i] * maxAmplitude;
        }
        return frequencyNoiseMap;
    }

    private float generateSeed()
    {
        return Random.Range(0.11111f, 10000000.111111f);
    }

    public int[,] generateChunkProcedurally(Vector2Int pos, float seed)
    {
        switch(genType)
        {
            case generationType.simple:
                return SimpleProceduralGeneration(pos, seed);
            case generationType.fractal:
                return ComplexProceduralGeneration(pos, seed);
            default:
                return SimpleProceduralGeneration(pos, seed);
        }
    }

    public ChunkData GetChunk(WorldMetaData wmd, Vector2Int pos)
    {
        if (fileWorldRepository.DoesChunkExist(pos, wmd.worldName))
            return fileWorldRepository.LoadChunk(pos, wmd.worldName);

        ChunkData newChunk = new(pos, generateChunkProcedurally(pos, wmd.seed));
        fileWorldRepository.SaveChunk(newChunk, wmd.worldName);
        return newChunk;
    }

    public WorldMetaData GetWorldMetaData(string name)
    {
        if (fileWorldRepository.DoesWorldExist(name))
            return fileWorldRepository.LoadWorldMetaData(name);
        return new(name, generateSeed(), Vector2.zero, Vector2.zero);
    }

    public void SaveWorldMetaData(WorldMetaData wmd)
    {
        if (fileWorldRepository.DoesWorldExist(name))
            return;
        
        fileWorldRepository.SaveWorldMetaData(wmd);
    }

    public void saveChunk(ChunkData chunk, WorldMetaData wmd)
    {
        fileWorldRepository.SaveChunk(chunk, wmd.worldName);
    }

}
