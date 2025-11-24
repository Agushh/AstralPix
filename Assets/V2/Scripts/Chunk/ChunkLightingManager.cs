using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using static UnityEditor.PlayerSettings;

public class ChunkLightingManager : MonoBehaviour
{
    const int chunkSize = 32;
    static float Luminance(Color c) => 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;


    [SerializeField] float LIGHT_FALLOFF = 0.85f; // 0.85–0.95 típico
    [SerializeField] float LIGHT_THRESHOLD = 0.01f; // Optimización: ignorar luces muy tenues

    [Header("Settings")]
    [Tooltip("Segundos entre cada actualización de luz. 0.1s = 10 veces por segundo.")]
    [SerializeField] float refreshRate = 0.08f;

    [Header("References")]
    [SerializeField] WorldManager worldManager;
    [SerializeField] TileConfig tileConfig;
    Camera mainCamera; // Cache de la cámara

    Coroutine lightingCoroutine;

    Dictionary<Vector2Int, ChunkManager> renderedChunks = new();

    bool isDirty = true;
    private void Start()
    {
        lightingCoroutine = StartCoroutine(LightingLoop());
    }


    //Ilumination Refresh Loop
    private IEnumerator LightingLoop()
    {
        // Cacheamos la espera para no generar basura (garbage) en memoria cada ciclo
        WaitForSeconds wait = new WaitForSeconds(refreshRate);

        while (true)
        {
            if (isDirty)
            {
                CalculateLighting();
                isDirty = false;
            }
            yield return wait;
        }
    }
    public void MarkDirty()
    {
        isDirty = true;
    }

    RectInt GetVisibleChunkBounds()
    {
        if (mainCamera == null) mainCamera = Camera.main;
        if (mainCamera == null) return new RectInt(0, 0, 0, 0);

        float height = mainCamera.orthographicSize * 2f;
        float width = height * mainCamera.aspect;
        Vector3 pos = mainCamera.transform.position;

        // Calculamos esquinas en mundo
        float minX = pos.x - (width / 2f);
        float minY = pos.y - (height / 2f);
        float maxX = pos.x + (width / 2f);
        float maxY = pos.y + (height / 2f);

        // Convertimos a coordenadas de Chunk
        int cMinX = Mathf.FloorToInt(minX / chunkSize) - 1;
        int cMinY = Mathf.FloorToInt(minY / chunkSize) - 1;
        int cMaxX = Mathf.FloorToInt(maxX / chunkSize) + 1;
        int cMaxY = Mathf.FloorToInt(maxY / chunkSize) + 1;

        // Creamos el rectángulo (X, Y, Ancho, Alto)
        // Nota: Sumamos +1 al ancho/alto para asegurar que el borde derecho/superior esté incluido
        return new RectInt(cMinX, cMinY, (cMaxX - cMinX) + 1, (cMaxY - cMinY) + 1);
    }

    Queue<(Vector2Int pos, Color color)> emitters = new();
    Dictionary<Vector2Int, Color[,]> lightMaps = new();
    Queue<Vector2Int> bfsQueue = new();
    void CalculateLighting()
    {
        lightMaps.Clear();
        emitters.Clear();

        RectInt visibleArea = GetVisibleChunkBounds();


        foreach (var kvp in renderedChunks)
        {
            if (!visibleArea.Contains(kvp.Key)) continue;

            if (lightMaps.TryGetValue(kvp.Key, out Color[,] color))
                System.Array.Clear(color, 0, chunkSize * chunkSize);
            else
                lightMaps.Add(kvp.Key, new Color[chunkSize, chunkSize]);
        }

        foreach (ChunkManager chunk in renderedChunks.Values)
        {
            if (!visibleArea.Contains(chunk.Position)) continue;
            foreach (var emitter in chunk.getLightEmitters())
            {
                emitters.Enqueue(emitter);
            }
        }

        bfsQueue.Clear();
        while (emitters.Count > 0)
        {
            var (pos, color) = emitters.Dequeue();

            // Si logramos poner la luz (es más brillante que lo que había), la añadimos a la cola
            if (TrySetLight(pos, color))
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
                if (TrySetLight(neighborPos, newLight))
                {
                    bfsQueue.Enqueue(neighborPos);
                }
            }
        }
        SendLights();

    }
    Color ApplyDecay(Color c)
    {
        return new Color(c.r * LIGHT_FALLOFF * LIGHT_FALLOFF, c.g * LIGHT_FALLOFF * LIGHT_FALLOFF, c.b * LIGHT_FALLOFF * LIGHT_FALLOFF, c.a);
    }



    void SendLights()
    {
        foreach(var kvp in lightMaps)
        {
            Vector2Int chunkPos = kvp.Key;
            Color[,] center = kvp.Value;

            if (!renderedChunks.ContainsKey(chunkPos)) continue;

            Color[] top = GetRow(chunkPos + Vector2Int.up, 0);

            Color[] bottom = GetRow(chunkPos + Vector2Int.down, chunkSize - 1);

            Color[] left = GetCol(chunkPos + Vector2Int.left, chunkSize - 1);

            Color[] right = GetCol(chunkPos + Vector2Int.right, 0);

            //Se realiza este paso extra en vez de llamar directamente de renderedChunks para prevenir errores de sincronismo.
            worldManager.SendLightMap(chunkPos, center, top, bottom, left, right);
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
    private bool TrySetLight(Vector2Int globalPos, Color newColor)
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
            
            Color oldColor = map[lx, ly];

            if (AlmostEqual(oldColor, newColor)) return false;

            // MEZCLA DE COLORES (Estilo Starbound/Terraria)
            Color mergedColor = new Color(
                Mathf.Max(oldColor.r, newColor.r),
                Mathf.Max(oldColor.g, newColor.g),
                Mathf.Max(oldColor.b, newColor.b),
                1f
            );

            // Si no hubo cambio o la luz nueva es más débil, no hacemos nada
            if (AlmostEqual(oldColor, mergedColor)) return false;

            map[lx, ly] = mergedColor;
            return true;
        }
        return false; // El chunk no está cargado/visible
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
    public void SetRenderedChunks(Dictionary<Vector2Int, ChunkManager> renderedChunks)
    {
        this.renderedChunks = renderedChunks;
        MarkDirty();
    }

}

