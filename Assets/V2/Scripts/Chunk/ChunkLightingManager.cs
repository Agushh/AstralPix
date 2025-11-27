using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using static UnityEditor.PlayerSettings;

public class ChunkLightingManager : MonoBehaviour
{
    const int chunkSize = 32;


    [SerializeField] float LIGHT_FALLOFF = 0.85f; // 0.85–0.95 típico
    [SerializeField] float LIGHT_THRESHOLD = 0.01f; // Optimización: ignorar luces muy tenues

    [Header("References")]
    [SerializeField] WorldManager worldManager;
    [SerializeField] TileConfig tileConfig;
    Camera mainCamera; // Cache de la cámara

    Dictionary<Vector2Int, ChunkManager> renderedChunksReferences = new();


    Queue<(Vector2Int pos, Color color)> emitters = new();
    Dictionary<Vector2Int, Color[,]> lightMaps = new();
    Queue<Vector2Int> bfsQueue = new();
    HashSet<Vector2Int> chunksToRender = new();
    public void UpdateLight(Vector2Int chunk)
    {
        HashSet<Vector2Int> chunkPos = new() { chunk };
        UpdateLight(chunkPos);
    }
    public void UpdateLight(HashSet<Vector2Int> selectedChunks)
    {
        // Si limpio lightMaps antes de ejecutar, se me borran todos los chunks vecinos de los que quiero actualizar y no voy a poder obtener los paddings a la hora de enviar sus datos.
        //lightMaps.Clear();
        emitters.Clear();

        if(selectedChunks != null)
            selectedChunks = AddNeighbours(selectedChunks);

        selectedChunks ??= renderedChunksReferences.Keys.ToHashSet(); // si es nulo, no afecta, y carga todo lo que se renderiza en camara.

        chunksToRender.Clear();
        //Filtro doble ->
        // 1 - Debe estar Instanciado y In Game (renderedChunksReferences)
        // 3 - Debe estar seleccionado (selectedChunks) Esto significa actualizar solo el necesario, o si es = null, actualizar todo sin tener que hacer uno por uno, y poder actualizar lotes.
        foreach (Vector2Int pos in renderedChunksReferences.Keys)  
        {
            if (selectedChunks.Contains(pos))
            {
                chunksToRender.Add(pos);
            }
        }

        //Limpieza de los chunks a actualizar, los otros se mantienen por los paddings.
        //Tambien lectura de emisores de luz, solo en el rango.
        foreach (Vector2Int pos in chunksToRender)
        {
            if(lightMaps.TryGetValue(pos, out Color[,] map))
                System.Array.Clear(map, 0, map.Length);
            else 
                lightMaps.Add(pos, new Color[chunkSize, chunkSize]);

            ChunkManager chunk = renderedChunksReferences[pos];
            foreach (var emitter in chunk.getLightEmitters())
            {
                emitters.Enqueue(emitter);
            }
        }

        bfsQueue.Clear();
        while (emitters.Count > 0)
        {
            var (pos, color) = emitters.Dequeue();

            // Si logramos poner la luz (es más brillante que lo que había y el chunk existe), la añadimos a la cola
            if (TrySetLight(pos, color, true))
            {
                bfsQueue.Enqueue(pos);
            }
        }

        Vector2Int[] directions = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
        while(bfsQueue.Count > 0)
        {
            Vector2Int currentPos = bfsQueue.Dequeue();

            if (!TryGetLight(currentPos, out Color currentColor)) continue;

            foreach (var dir in directions)
            {
                Vector2Int neighborPos = currentPos + dir;

                // Lógica Simplificada: 
                // No verificamos bloque, simplemente aplicamos decaimiento a la luz actual
                Color newLight = ApplyDecay(currentColor);

                // Optimización de umbral
                if (Luminance(newLight) <= LIGHT_THRESHOLD) continue;

                // TrySetLight se encarga de verificar si el vecino existe en los chunks cargados
                // y de mezclar los colores si es necesario.
                if (TrySetLight(neighborPos, newLight, false))
                {
                    bfsQueue.Enqueue(neighborPos);
                }
            }
        }
        SendLights(chunksToRender);

    }


    HashSet<Vector2Int> AddNeighbours(HashSet<Vector2Int> selectedChunks)
    {
        foreach(var chunkPos in selectedChunks.ToList())
        {
            selectedChunks.Add(chunkPos + Vector2Int.up);
            selectedChunks.Add(chunkPos + Vector2Int.down);
            selectedChunks.Add(chunkPos + Vector2Int.left);
            selectedChunks.Add(chunkPos + Vector2Int.right);
            selectedChunks.Add(chunkPos + Vector2Int.up + Vector2Int.left);
            selectedChunks.Add(chunkPos + Vector2Int.up + Vector2Int.right);
            selectedChunks.Add(chunkPos + Vector2Int.down + Vector2Int.left);
            selectedChunks.Add(chunkPos + Vector2Int.down + Vector2Int.right);
        }
        return selectedChunks;
    }

    void SendLights(HashSet<Vector2Int> positions)
    {
        foreach(var pos in positions)
        {
            Color[,] center = lightMaps[pos];

            if (!renderedChunksReferences.ContainsKey(pos)) continue;

            Color[] top = GetRow(pos + Vector2Int.up, 0);

            Color[] bottom = GetRow(pos + Vector2Int.down, chunkSize - 1);

            Color[] left = GetCol(pos + Vector2Int.left, chunkSize - 1);

            Color[] right = GetCol(pos + Vector2Int.right, 0);

            //Se realiza este paso extra en vez de llamar directamente de renderedChunks para prevenir errores de sincronismo.
            worldManager.SendLightMap(pos, center, top, bottom, left, right);
        }
    }
    // --- Helpers para extraer bordes ---
    Color[] GetRow(Vector2Int chunkPos, int yIndex)
    {
        if (lightMaps.TryGetValue(chunkPos, out Color[,] map))
        {
            Color[] row = new Color[32];
            for (int x = 0; x < 32; x++) row[x] = map[x, yIndex];
            return row;
        }
        return null; 
    }

    Color[] GetCol(Vector2Int chunkPos, int xIndex)
    {
        if (lightMaps.TryGetValue(chunkPos, out Color[,] map))
        {
            Color[] col = new Color[32];
            for (int y = 0; y < 32; y++) col[y] = map[xIndex, y];
            return col;
        }
        return null;
    }


    /// <summary>
    /// Intenta establecer un color en la posición global.
    /// Realiza una mezcla MAX(old, new) por canal.
    /// Retorna TRUE si el nuevo color iluminó más el bloque (merece propagación).
    /// </summary>
    private bool TrySetLight(Vector2Int globalPos, Color newColor, bool emitter)
    {
        int chunkSize = 32; // Usar constante real

        // Calcular ID del Chunk
        int cx = Mathf.FloorToInt((float)globalPos.x / chunkSize);
        int cy = Mathf.FloorToInt((float)globalPos.y / chunkSize);
        Vector2Int chunkKey = new Vector2Int(cx, cy);

        if (lightMaps.TryGetValue(chunkKey, out Color[,] map))
        {
            // Calcular coordenadas locales
            int lx = globalPos.x - (cx * chunkSize);
            int ly = globalPos.y - (cy * chunkSize);

            // Seguridad de arrays
            if (lx < 0 || lx >= chunkSize || ly < 0 || ly >= chunkSize) return false;

            if (emitter)
            {
                map[lx, ly] = newColor;
                return true;
            } 

            Color oldColor = map[lx, ly];
            
            if (AlmostEqual(oldColor, newColor)) return false;

            // MEZCLA DE COLORES (Estilo Starbound/Terraria)
            Color mergedColor = CombineColors(newColor, oldColor);

            // Si no hubo cambio o la luz nueva es más débil, no hacemos nada
            if (AlmostEqual(oldColor, mergedColor, 0.005f)) return false;

            map[lx, ly] = mergedColor;
            return true;
        }
        return false; // El chunk no está cargado/visible
    }
    Color CombineColors(Color existing, Color incoming)
    {
        return new Color(Mathf.Max(existing.r, incoming.r), Mathf.Max(existing.g, incoming.g), Mathf.Max(existing.b, incoming.b));
    }

    Color ApplyDecay(Color c)
    {
        return new Color(c.r - LIGHT_FALLOFF, c.g - LIGHT_FALLOFF, c.b - LIGHT_FALLOFF, c.a);
    }
    private bool TryGetLight(Vector2Int globalPos, out Color color)
    {
        int chunkSize = 32;
        int cx = Mathf.FloorToInt((float)globalPos.x / chunkSize);
        int cy = Mathf.FloorToInt((float)globalPos.y / chunkSize);

        if (lightMaps.TryGetValue(new Vector2Int(cx, cy), out Color[,] map))
        {
            int lx = globalPos.x - (cx * chunkSize);
            int ly = globalPos.y - (cy * chunkSize);

            if (lx >= 0 && lx < chunkSize && ly >= 0 && ly < chunkSize)
            {
                color = map[lx, ly];
                return true;
            }
        }
        color = Color.black;
        return false;
    }
    bool AlmostEqual(Color a, Color b, float eps = 1e-4f)
    {
        return Mathf.Abs(a.r - b.r) < eps &&
               Mathf.Abs(a.g - b.g) < eps &&
               Mathf.Abs(a.b - b.b) < eps;
    }
    static float Luminance(Color c) => 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;
    public void SetRenderedChunks(Dictionary<Vector2Int, ChunkManager> renderedChunks)
    {
        this.renderedChunksReferences = renderedChunks;
    }

}

