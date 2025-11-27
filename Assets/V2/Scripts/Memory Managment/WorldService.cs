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
    private int[,] SimpleProceduralGeneration(Vector2Int position, float seed, out int[] heightmap, out bool isAir)
    {
        int[,] chunk = new int[chunkSize, chunkSize];
        heightmap = new int[chunkSize];
        isAir = true;
        for (int x = 0; x < chunkSize; x++)
        {
            float height = Mathf.PerlinNoise(1000 + (position.x * chunkSize + x) * scale, seed * 0.001f);
            heightmap[x] = Mathf.RoundToInt(height * maxHeightSimple);
            for (int y = 0; y < chunkSize; y++)
            {
                if (y + chunkSize * position.y < height * maxHeightSimple - 4)
                    chunk[x, y] = 4;
                else if (y + chunkSize * position.y < height * maxHeightSimple - 2)
                    chunk[x, y] = 2;
                else if (y + chunkSize * position.y < height * maxHeightSimple)
                    chunk[x, y] = 3;
                else
                {
                    chunk[x, y] = 0;
                    continue;
                }
                isAir = false;
            }
        }

        return chunk;
    }

    private int[,] ComplexProceduralGeneration(Vector2Int position, float seed, out int[] heightmap, out bool isAir, out int[,] background)
    {
        int[,] chunk = new int[chunkSize, chunkSize];
        background = new int[chunkSize, chunkSize];
        heightmap = FractalNoise(chunkSize * position.x, seed);

        isAir = false;

        int[,] caveMap = GenerateCaves(chunkSize * position.x, chunkSize * position.y, seed);


        for (int x = 0; x < chunkSize; x++)
        {
            for (int y = 0; y < chunkSize; y++)
            {
                
                if (y + chunkSize * position.y < heightmap[x]- 7)
                {
                    chunk[x, y] = 4;
                    background[x, y] = 4;
                }
                else if (y + chunkSize * position.y < heightmap[x] - 1)
                {
                    chunk[x, y] = 2;
                    background[x, y] = 2;
                }
                else if (y + chunkSize * position.y < heightmap[x])
                {
                    chunk[x, y] = 3;
                    background[x, y] = 3;
                }
                else
                {
                    chunk[x, y] = 0;
                    background[x, y] = 0;
                    continue;
                }

                if (caveMap[x, y] == 0)
                {
                    chunk[x, y] = 0;
                    continue;
                }
            }
        }

        return chunk;


    }
    [Header("Cave Generation : ")]
    [SerializeField] float caveScale = 0.1f;
    [SerializeField] float caveThreshold = 0.5f;
    [SerializeField] float caveLacunarity = 2.0f;

    private int[,] GenerateCaves(int offsetX, int offsetY, float seed)
    {
        // 1. Obtenemos el mapa de "continentalness".
        // Esto nos dice qué tan "intensa" es la zona geográficamente.
        float[,] AmplitudeMatrix = continentalness2D(offsetX, offsetY, seed);

        int[,] caveMap = new int[chunkSize, chunkSize];

        for (int x = 0; x < chunkSize; x++)
        {
            for (int y = 0; y < chunkSize; y++)
            {
                // Ruido base para la forma de la cueva
                float sampleX = (offsetX + x) * caveScale;
                float sampleY = (offsetY + y) * caveScale;
                float noiseValue = Mathf.PerlinNoise(sampleX + seed * 0.01f, sampleY + seed * 0.01f);

                // 2. Normalizamos el valor de AmplitudeMatrix para que esté entre 0 y 1.
                // AmplitudeMatrix va teóricamente de [minAmplitude] a [minAmplitude + maxAmplitude].
                // Usamos InverseLerp para convertir ese rango gigante a un flotante 0.0f - 1.0f.
                float continentalFactor = Mathf.InverseLerp(minAmplitude, minAmplitude + maxAmplitude, AmplitudeMatrix[x, y]);

                // 3. Modificamos el Threshold dinámicamente.
                // LÓGICA:
                // - Si continentalFactor es alto (1.0), restamos al threshold -> Threshold baja -> Más fácil que noiseValue lo supere -> CUEVAS MÁS GRANDES.
                // - Si continentalFactor es bajo (0.0), el threshold se mantiene alto -> CUEVAS PEQUEÑAS O INEXISTENTES.

                // El valor '0.4f' es el "Poder de influencia". 
                // Si pones 0.4f, el threshold bajará hasta un 40% en zonas de alta continentalness.
                float dynamicThreshold = caveThreshold - (continentalFactor * 0.4f);

                // Aseguramos que el threshold no se rompa (opcional pero recomendado)
                // dynamicThreshold = Mathf.Clamp(dynamicThreshold, 0.2f, 0.8f);

                if (noiseValue > dynamicThreshold)
                    caveMap[x, y] = 1; // Aire (Cueva)
                else
                    caveMap[x, y] = 0; // Sólido (Pared)
            }
        }
        return caveMap;
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
    private float[,] continentalness2D(int offsetX, int offsetY, float seed)
    {
        float[,] continentalNoiseMap = new float[chunkSize, chunkSize];
        float[,] frequencyNoiseMap = new float[chunkSize, chunkSize];
        for (int i = 0; i < chunkSize; i++)
        {
            for (int j = 0; j < chunkSize; j++)
            {
                continentalNoiseMap[i, j] = Mathf.PerlinNoise(((i + offsetX) / divisionForContinentalness) + seed, ((j + offsetY) / divisionForContinentalness) + seed);
                frequencyNoiseMap[i, j] = minAmplitude + continentalNoiseMap[i, j] * maxAmplitude;
            }
        }
        return frequencyNoiseMap;
    }
    private float generateSeed()
    {
        return Random.Range(0.11111f, 10000000.111111f);
    }

    public int[,] generateChunkProcedurally(Vector2Int pos, float seed, out int[] heightmap, out bool isAir, out int[,] background)
    {
        background = new int[32, 32];
        switch(genType)
        {
            case generationType.simple:
                return SimpleProceduralGeneration(pos, seed, out heightmap, out isAir);
            case generationType.fractal:
                return ComplexProceduralGeneration(pos, seed, out heightmap, out isAir, out background);
            default:
                return SimpleProceduralGeneration(pos, seed, out heightmap, out isAir);
        }
    }

    public ChunkData GetChunk(WorldMetaData wmd, Vector2Int pos)
    {
        if (fileWorldRepository.DoesChunkExist(pos, wmd.worldName))
            return fileWorldRepository.LoadChunk(pos, wmd.worldName);

        int[,] newChunk = generateChunkProcedurally(pos, wmd.seed, out int[] chunkHeight, out bool isAir, out int[,] background);
        ChunkData newChunkData = new(pos, newChunk, background, chunkHeight, isAir);
        fileWorldRepository.SaveChunk(newChunkData, wmd.worldName);
        return newChunkData;
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
