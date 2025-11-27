using System.Collections.Generic;
using System.Linq;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(PolygonCollider2D))]
public class ChunkManager : MonoBehaviour
{
    public static int chunkSize = 32;


    [SerializeField] TileConfig tileConfig;    //Contains all of the information about blocks, interaction, uvs, light, etc. As tileconfig.tiles[x] as a tile Struct with the data 

    WorldMetaData wmd;                         //Meta data of the world where is the chunk

    WorldManager worldManager;                 //Father controller that insantiate and let interact with other chunks.

    Color[,] LightMap = new Color[chunkSize, chunkSize];


    //ChunkData
    private Vector2Int position;
    public Vector2Int Position => position;

    private int[,] blocks;
    public int[,] Blocks => blocks;

    private int[,] backBlocks;
    public int[,] BackBlocks => backBlocks;

    private int[] surfaceHeight;
    public int[] SurfaceHeight => surfaceHeight;

    public bool isAir;

    //"Constructor de chunk". Se utiliza al deserealizar, luego de instanciar, y se setean sus valores desde WorldManager.
    public void SetData(Vector2Int index, WorldMetaData worldData, WorldManager worldManager, ChunkData cd)
    {
        position = index;
        wmd = worldData;
        this.worldManager = worldManager;

        meshFilter = GetComponent<MeshFilter>();
        meshRenderer = GetComponent<MeshRenderer>();
        polyCollider = GetComponent<PolygonCollider2D>();

        blocks = cd.getBlockMatrix();
        backBlocks = cd.getBackBlocksMatrix();
        surfaceHeight = cd.surfaceHeight;
        isAir = cd.isAir;


        if (cd.colliderPaths != null && cd.colliderPaths.Count == 0)
        {
            collisions.AddRange(cd.colliderPaths);
            polyCollider.pathCount = collisions.Count;
            for (int i = 0; i < collisions.Count; i++)
            {
                polyCollider.SetPath(i, SimplifyPath(collisions[i].ToList()));
            }
        }
        else
        {
            isDirty = true;
            GenerateCollider();
        }

        meshFilter.mesh = new Mesh();
        meshFilter.mesh.MarkDynamic();
        BG_meshFilter.mesh = new Mesh();
        BG_meshFilter.mesh.MarkDynamic();

        meshRenderer.sortingOrder = 1;
        BG_meshRenderer.sortingOrder = 0;


        GenerateMesh();


        //lighting texture 
        int paddedSize = chunkSize + 2;
        lightTexture = new Texture2D(paddedSize, paddedSize, TextureFormat.RGBA32, false, true);
        lightTexture.filterMode = FilterMode.Bilinear;
        lightTexture.wrapMode = TextureWrapMode.Clamp;
        meshRenderer.material.SetTexture("_LightTex", lightTexture);

        BG_meshRenderer.material.SetFloat("_DarkenFactor", 0.3f);

        BG_meshRenderer.material.SetTexture("_LightTex", lightTexture);

        // Inicializacion de array de pixeles
        lightPixels = new Color32[paddedSize * paddedSize];
    }

    #region Meshes

    MeshFilter meshFilter;
    MeshRenderer meshRenderer;
    PolygonCollider2D polyCollider;

    [SerializeField] MeshFilter BG_meshFilter;
    [SerializeField] MeshRenderer BG_meshRenderer;

    //Mesh data
    List<Vector3> vertices = new();
    List<int> triangles = new();
    List<Vector2> uvs = new();
    int vertexIndex = 0;

    //Background Mesh data
    List<Vector3> BG_vertices = new();
    List<int> BG_triangles = new();
    List<Vector2> BG_uvs = new();
    int BG_vertexIndex = 0;


    //Lightining
    List<Vector2> uvs2 = new(); // RealWorldUvs Position
    List<Vector2> BG_uvs2 = new();

    void GenerateMesh()
    {
        vertices.Clear();
        triangles.Clear();
        uvs.Clear();
        uvs2.Clear();
        vertexIndex = 0;

        BG_vertices.Clear();
        BG_triangles.Clear();
        BG_uvs.Clear();
        BG_uvs2.Clear();
        BG_vertexIndex = 0;

        if (isAir) return;

        // --- PASO 1: GENERAR BASES ---
        // Se dibujan por detras de los bordes.
        for (int y = 0; y < chunkSize; y++)
        {
            for (int x = 0; x < chunkSize; x++)
            {
                int blockID = blocks[x, y];
                int BackgroundID = backBlocks[x, y];
                if (BackgroundID != 0) GenerateQuad(BG_vertices, BG_triangles, BG_uvs, BG_uvs2, ref BG_vertexIndex,x, y, 1, 1, 1, BackgroundID, "base");
                if (blockID != 0 ) GenerateQuad(vertices, triangles, uvs, uvs2, ref vertexIndex, x, y, 1, 1, 1f, blockID, "base");

            }
        }

        // --- PASO 2: GENERAR BORDES ---
        //se dibujan encima de las bases
        for (int y = 0; y < chunkSize; y++)
        {
            for (int x = 0; x < chunkSize; x++)
            {
                int blockID = blocks[x, y];
                int BackgroundID = backBlocks[x, y];
                if (BackgroundID != 0) GenerateEdges(BG_vertices, BG_triangles, BG_uvs, BG_uvs2, ref BG_vertexIndex, x, y, BackgroundID, 0, false);
                if (blockID != 0) GenerateEdges(vertices, triangles, uvs, uvs2, ref vertexIndex, x, y, blockID, 0, true);
            }
        }
        SetMesh();
    }

    void GenerateEdges(List<Vector3> verts, List<int> trings, List<Vector2> targetUvs1, List<Vector2> targetUvs2, ref int vIndex, int x, int y, int blockID, float z, bool ifFrontBlock)
    {
        // --- DERECHA (x + 1) ---
        
        if (tileConfig.Tiles[GetBlock(x + 1, y, ifFrontBlock)].zOffset < tileConfig.Tiles[blockID].zOffset)
        {
            if (tileConfig.Tiles[GetBlock(x + 1, y + 1, ifFrontBlock)].zOffset < tileConfig.Tiles[blockID].zOffset && tileConfig.Tiles[GetBlock(x + 1, y - 1, ifFrontBlock)].zOffset < tileConfig.Tiles[blockID].zOffset)
                GenerateQuad(verts, trings, targetUvs1, targetUvs2, ref vIndex, x + 1, y, 0.5f, 1, z, blockID, "rightEdge");
            else if (tileConfig.Tiles[GetBlock(x + 1, y + 1, ifFrontBlock)].zOffset < tileConfig.Tiles[blockID].zOffset)
            {
                GenerateQuad(verts, trings, targetUvs1, targetUvs2, ref vIndex, x + 1, y + 0.5f, 0.5f, 0.5f, z, blockID, "rightTopEdge");
                GenerateQuad(verts, trings, targetUvs1, targetUvs2, ref vIndex, x + 1, y, 0.5f, 0.5f, z, blockID, "innerBL");
            }
            else if (tileConfig.Tiles[GetBlock(x + 1, y - 1, ifFrontBlock)].zOffset < tileConfig.Tiles[blockID].zOffset)
            {
                GenerateQuad(verts, trings, targetUvs1, targetUvs2, ref vIndex, x + 1, y + 0.5f, 0.5f, 0.5f, z, blockID, "innerTL");
                GenerateQuad(verts, trings, targetUvs1, targetUvs2, ref vIndex, x + 1, y, 0.5f, 0.5f, z, blockID, "rightBottomEdge");
            }
            else
            {
                GenerateQuad(verts, trings, targetUvs1, targetUvs2, ref vIndex, x + 1, y, 0.5f, 0.5f, z, blockID, "innerBL");
                GenerateQuad(verts, trings, targetUvs1, targetUvs2, ref vIndex, x + 1, y + 0.5f, 0.5f, 0.5f, z, blockID, "innerTL");
            }
        }

        // --- IZQUIERDA (x - 1) ---
        if (tileConfig.Tiles[GetBlock(x - 1, y, ifFrontBlock)].zOffset < tileConfig.Tiles[blockID].zOffset)
        {
            if (tileConfig.Tiles[GetBlock(x - 1, y + 1, ifFrontBlock)].zOffset < tileConfig.Tiles[blockID].zOffset && tileConfig.Tiles[GetBlock(x - 1, y - 1, ifFrontBlock)].zOffset < tileConfig.Tiles[blockID].zOffset)
                GenerateQuad(verts, trings, targetUvs1, targetUvs2, ref vIndex, x - 0.5f, y, 0.5f, 1, z, blockID, "leftEdge");
            else if (tileConfig.Tiles[GetBlock(x - 1, y + 1, ifFrontBlock)].zOffset < tileConfig.Tiles[blockID].zOffset)
            {
                GenerateQuad(verts, trings, targetUvs1, targetUvs2, ref vIndex, x - 0.5f, y + 0.5f, 0.5f, 0.5f, z, blockID, "leftTopEdge");
                GenerateQuad(verts, trings, targetUvs1, targetUvs2, ref vIndex, x - 0.5f, y, 0.5f, 0.5f, z, blockID, "innerBR");
            }
            else if (tileConfig.Tiles[GetBlock(x - 1, y - 1, ifFrontBlock)].zOffset < tileConfig.Tiles[blockID].zOffset)
            {
                GenerateQuad(verts, trings, targetUvs1, targetUvs2, ref vIndex, x - 0.5f, y + 0.5f, 0.5f, 0.5f, z, blockID, "innerTR");
                GenerateQuad(verts, trings, targetUvs1, targetUvs2, ref vIndex, x - 0.5f, y, 0.5f, 0.5f, z, blockID, "leftBottomEdge");
            }
            else
            {
                GenerateQuad(verts, trings, targetUvs1, targetUvs2, ref vIndex, x - 0.5f, y, 0.5f, 0.5f, z, blockID, "innerBR");
                GenerateQuad(verts, trings, targetUvs1, targetUvs2, ref vIndex, x - 0.5f, y + 0.5f, 0.5f, 0.5f, z, blockID, "innerTR");
            }
        }

        // --- ARRIBA (y + 1) ---
        if (tileConfig.Tiles[GetBlock(x, y + 1, ifFrontBlock)].zOffset < tileConfig.Tiles[blockID].zOffset)
        {
            if (tileConfig.Tiles[GetBlock(x - 1, y + 1, ifFrontBlock)].zOffset < tileConfig.Tiles[blockID].zOffset && tileConfig.Tiles[GetBlock(x + 1, y + 1, ifFrontBlock)].zOffset < tileConfig.Tiles[blockID].zOffset)
                GenerateQuad(verts, trings, targetUvs1, targetUvs2, ref vIndex, x, y + 1, 1, 0.5f, z, blockID, "topEdge");
            else if (tileConfig.Tiles[GetBlock(x + 1, y + 1, ifFrontBlock)].zOffset < tileConfig.Tiles[blockID].zOffset)
            {
                GenerateQuad(verts, trings, targetUvs1, targetUvs2, ref vIndex, x + 0.5f, y + 1, 0.5f, 0.5f, z, blockID, "topRightEdge");
                GenerateQuad(verts, trings, targetUvs1, targetUvs2, ref vIndex, x, y + 1, 0.5f, 0.5f, z, blockID, "innerBL");
            }
            else if (tileConfig.Tiles[GetBlock(x - 1, y + 1, ifFrontBlock)].zOffset < tileConfig.Tiles[blockID].zOffset)
            {
                GenerateQuad(verts, trings, targetUvs1, targetUvs2, ref vIndex, x + 0.5f, y + 1, 0.5f, 0.5f, z, blockID, "innerBR");
                GenerateQuad(verts, trings, targetUvs1, targetUvs2, ref vIndex, x, y + 1, 0.5f, 0.5f, z, blockID, "topLeftEdge");
            }
            else
            {
                GenerateQuad(verts, trings, targetUvs1, targetUvs2, ref vIndex, x, y + 1, 0.5f, 0.5f, z, blockID, "innerBL");
                GenerateQuad(verts, trings, targetUvs1, targetUvs2, ref vIndex, x + 0.5f, y + 1, 0.5f, 0.5f, z, blockID, "innerBR");
            }
        }

        // --- ABAJO (y - 1) ---
        if (tileConfig.Tiles[GetBlock(x, y - 1, ifFrontBlock)].zOffset < tileConfig.Tiles[blockID].zOffset)
        {
            if (tileConfig.Tiles[GetBlock(x - 1, y - 1, ifFrontBlock)].zOffset < tileConfig.Tiles[blockID].zOffset && tileConfig.Tiles[GetBlock(x + 1, y - 1, ifFrontBlock)].zOffset < tileConfig.Tiles[blockID].zOffset)
                GenerateQuad(verts, trings, targetUvs1, targetUvs2, ref vIndex, x, y - 0.5f, 1, 0.5f, z, blockID, "bottomEdge");
            else if (tileConfig.Tiles[GetBlock(x + 1, y - 1, ifFrontBlock)].zOffset < tileConfig.Tiles[blockID].zOffset)
            {
                GenerateQuad(verts, trings, targetUvs1, targetUvs2, ref vIndex, x + 0.5f, y - 0.5f, 0.5f, 0.5f, z, blockID, "bottomRightEdge");
                GenerateQuad(verts, trings, targetUvs1, targetUvs2, ref vIndex, x, y - 0.5f, 0.5f, 0.5f, z, blockID, "innerTL");
            }
            else if (tileConfig.Tiles[GetBlock(x - 1, y - 1, ifFrontBlock)].zOffset < tileConfig.Tiles[blockID].zOffset)
            {
                GenerateQuad(verts, trings, targetUvs1, targetUvs2, ref vIndex, x + 0.5f, y - 0.5f, 0.5f, 0.5f, z, blockID, "innerTR");
                GenerateQuad(verts, trings, targetUvs1, targetUvs2, ref vIndex, x, y - 0.5f, 0.5f, 0.5f, z, blockID, "bottomLeftEdge");
            }
            else
            {
                GenerateQuad(verts, trings, targetUvs1, targetUvs2, ref vIndex, x, y - 0.5f, 0.5f, 0.5f, z, blockID, "innerTL");
                GenerateQuad(verts, trings, targetUvs1, targetUvs2, ref vIndex, x + 0.5f, y - 0.5f, 0.5f, 0.5f, z, blockID, "innerTR");
            }
        }
    }


    void GenerateQuad(List<Vector3> verts, List<int> trings, List<Vector2> targetUvs1, List<Vector2> targetUvs2, ref int vIndex, float x, float y, float xSize, float ySize, float z, int id, string relation)
    {
        // Agregamos Z a los vértices
        verts.Add(new Vector3(x, y, z));
        verts.Add(new Vector3(x, y + ySize, z));
        verts.Add(new Vector3(x + xSize, y + ySize, z));
        verts.Add(new Vector3(x + xSize, y, z));

        // Triángulos
        trings.Add(vIndex);
        trings.Add(vIndex + 1);
        trings.Add(vIndex + 2);
        trings.Add(vIndex);
        trings.Add(vIndex + 2);
        trings.Add(vIndex + 3);

        // UVs
        // IMPORTANTE: Ajustar el ID para acceder a la lista correctamente.
        // Si tu lista de tiles empieza en indice 0 con "Aire", y tus bloques usan ID 0 para aire,
        // entonces id=1 es la posición 1 de la lista.
        int variationAmmount = tileConfig.Tiles[id].variations;
        int variation = Mathf.RoundToInt(Mathf.PerlinNoise(position.x * chunkSize + x + wmd.seed, position.y * chunkSize + y + wmd.seed) * (variationAmmount - 1));

        Vector2[] tileUVs = tileConfig.GetUVs(id, relation, variation);

        targetUvs1.Add(tileUVs[0]);
        targetUvs1.Add(tileUVs[1]);
        targetUvs1.Add(tileUVs[2]);
        targetUvs1.Add(tileUVs[3]);

        // UV2 para luces (sigue siendo plano 2D para el mapa de luz)
        if (targetUvs2 != null)
        {
            float offset = 1;
            float factor = (float)32 / 34;
            targetUvs2.Add(new Vector3(x * factor + offset, y * factor + offset, variation));
            targetUvs2.Add(new Vector3(x * factor + offset, (y + ySize) * factor + offset, variation));
            targetUvs2.Add(new Vector3((x + xSize) * factor + offset, (y + ySize) * factor + offset, variation));
            targetUvs2.Add(new Vector3((x + xSize) * factor + offset, y * factor + offset, variation));
        }

        vIndex += 4;
    }
    void SetMesh()
    {
        meshFilter.mesh.Clear();
        meshFilter.mesh.SetVertices(vertices.ToArray());
        meshFilter.mesh.SetTriangles(triangles.ToArray(), 0);
        meshFilter.mesh.SetUVs(0, uvs.ToArray());
        meshFilter.mesh.SetUVs(1, uvs2.ToArray());

        meshFilter.mesh.RecalculateNormals();
        meshFilter.mesh.RecalculateBounds();

        //Background mesh
        BG_meshFilter.mesh.Clear();
        BG_meshFilter.mesh.SetVertices(BG_vertices.ToArray());
        BG_meshFilter.mesh.SetTriangles(BG_triangles.ToArray(), 0);
        BG_meshFilter.mesh.SetUVs(0, BG_uvs.ToArray());
        BG_meshFilter.mesh.SetUVs(1, BG_uvs2.ToArray());

        BG_meshFilter.mesh.RecalculateNormals();
        BG_meshFilter.mesh.RecalculateBounds();
    }

    #endregion

    #region ColliderGeneration

    //Collision data
    Dictionary<Vector2, List<Vector2>> edges = new();
    List<Vector2[]> collisions = new();
    public List<Vector2[]> Collisions => collisions;

    void GenerateCollider()
    {
        edges.Clear();
        if(isAir) return;
        for (int y = 0; y < chunkSize; y++)
        {
            for (int x = 0; x < chunkSize; x++)
            {
                if (blocks[x, y] == 0 || !tileConfig.Tiles[blocks[x, y]].isSolid) continue;

                bool up = tileConfig.Tiles[GetBlock(x, y + 1, true)].isSolid;
                bool down = tileConfig.Tiles[GetBlock(x, y - 1, true)].isSolid;
                bool left = tileConfig.Tiles[GetBlock(x - 1, y, true)].isSolid;
                bool right = tileConfig.Tiles[GetBlock(x + 1, y, true)].isSolid;

                Vector2 topLeft = new Vector2(x, y + 1);
                Vector2 topRight = new Vector2(x + 1, y + 1);
                Vector2 bottomLeft = new Vector2(x, y);
                Vector2 bottomRight = new Vector2(x + 1, y);

                // Rampas diagonales antihorarias
                if (!up && !right && down && left)
                {
                    AddEdge(bottomRight, topLeft);     // up-right
                    up = true; right = true; // Evita duplicados
                }
                else if (!up && !left && down && right)
                {
                    AddEdge(topRight, bottomLeft);     // up-left
                    up = true; left = true; // Evita duplicados
                }
                else if (!down && !right && up && left)
                {
                    AddEdge(bottomLeft, topRight);     // down-right
                    down = true; right = true;
                }
                else if (!down && !left && up && right)
                {
                    AddEdge(topLeft, bottomRight);     // down-left
                    down = true; left = true;
                }

                // Laterales
                if (!up) AddEdge(topRight, topLeft);
                if (!down) AddEdge(bottomLeft, bottomRight);
                if (!left) AddEdge(topLeft, bottomLeft);
                if (!right) AddEdge(bottomRight, topRight);

                // Bordes del chunk
                if (y == chunkSize - 1 && up) AddEdge(topRight, topLeft);
                if (y == 0 && down) AddEdge(bottomLeft, bottomRight);
                if (x == 0 && left) AddEdge(topLeft, bottomLeft);
                if (x == chunkSize - 1 && right) AddEdge(bottomRight, topRight);
            }
        }

        List<List<Vector2>> paths = CreatePaths();
        polyCollider.pathCount = paths.Count;

        collisions.AddRange(paths.Select(p => p.ToArray()));

        for (int i = 0; i < paths.Count; i++)
        {
            polyCollider.SetPath(i, SimplifyPath(paths[i]).ToArray());
        }
    }
    List<List<Vector2>> CreatePaths()
    {
        List<List<Vector2>> allPaths = new List<List<Vector2>>();
        while (edges.Count > 0)
        {
            List<Vector2> path = new List<Vector2>();
            Vector2 start = edges.Keys.First();
            Vector2 current = start;

            path.Add(current);

            while (true)
            {
                if (!edges.ContainsKey(current)) break;

                Vector2 next = edges[current][0];
                edges[current].RemoveAt(0);
                if (edges[current].Count == 0) edges.Remove(current);

                current = next;
                path.Add(current);

                if (current == start) break;
            }

            allPaths.Add(path);
        }

        return allPaths;
    }
    void AddEdge(Vector2 from, Vector2 to)
    {
        if (!edges.ContainsKey(from))
            edges[from] = new List<Vector2>();

        edges[from].Add(to);
    }
    List<Vector2> SimplifyPath(List<Vector2> path)
    {
        if (path.Count < 3) return path; // No se puede simplificar

        List<Vector2> simplified = new List<Vector2>();
        simplified.Add(path[0]); // Añade siempre el primer punto

        for (int i = 1; i < path.Count - 1; i++)
        {
            Vector2 prev = path[i - 1];
            Vector2 curr = path[i];
            Vector2 next = path[i + 1];

            // Calcula la dirección de los dos segmentos
            Vector2 dir1 = (curr - prev).normalized;
            Vector2 dir2 = (next - curr).normalized;

            // Si las direcciones son casi idénticas, el punto es colineal (recto)
            // Lo saltamos.
            if (Vector2.Dot(dir1, dir2) < 0.999f)
            {
                // El ángulo es > 0, no es una línea recta. Conserva el punto.
                simplified.Add(curr);
            }
        }

        simplified.Add(path[path.Count - 1]); // Añade siempre el último punto
        return simplified;
    }
    #endregion

    #region Lighting

    // CACHÉ DE TEXTURA 
    private Texture2D lightTexture;
    private Color32[] lightPixels;
    public Queue<(Vector2Int pos, Color color)> getLightEmitters()
    {
        Queue<(Vector2Int pos, Color color)> emitters = new();

        int offsetX = position.x * chunkSize,
            offsetY = position.y * chunkSize;

        bool sunLight;
        if(isAir)
        {
            for(int i = 0; i < chunkSize; i++)
            {
                emitters.Enqueue((new(offsetX + i, offsetY), Color.white));
                emitters.Enqueue((new(offsetX, offsetY + i ), Color.white));
                emitters.Enqueue((new(offsetX + chunkSize, offsetY + i ), Color.white));
                emitters.Enqueue((new(offsetX + i, offsetY + chunkSize), Color.white));
            }

        }
        else
        {
            for (int i = 0; i < chunkSize; i++)
            {
                sunLight = true;
                for (int j = chunkSize - 1; j >= 0; j--)
                {
                    if (j + offsetY < surfaceHeight[i] - 10) sunLight = false;
                    if ((tileConfig.Tiles[blocks[i, j]].lightColor == Color.white && !tileConfig.Tiles[backBlocks[i, j]].isSolid) ||
                        (tileConfig.Tiles[blocks[i,j]].lightColor != Color.black && tileConfig.Tiles[blocks[i, j]].lightColor != Color.white) ||
                        (sunLight && !tileConfig.Tiles[blocks[i, j]].isSolid))
                    {
                            emitters.Enqueue((new(offsetX + i, offsetY + j), tileConfig.Tiles[blocks[i, j]].lightColor));
                    }
                    else sunLight = false;
            }
        }
        }
        
        return emitters;
    }

    public void UpdateLight(Color[,] lightMap, Color[] top, Color[] bottom, Color[] left, Color[] right)
    {
        if (isAir) return;
        int paddedSize = chunkSize + 2;
        for (int y = 0; y < chunkSize; y++)
        {
            for (int x = 0; x < chunkSize; x++)
            {
                lightPixels[(y + 1) * paddedSize + (x + 1)] = (Color32)lightMap[x, y];
            }
        }
        // Copiar bordes (padding de 1 pixel)
        // Top y bottom
        if (bottom != null)
        {
            for (int x = 0; x < chunkSize; x++)
                lightPixels[x + 1] = bottom[x];
        }
        else // Fallback: Copiar mi propia fila inferior (clamp)
        {
            for (int x = 0; x < chunkSize; x++)
                lightPixels[x + 1] = lightPixels[paddedSize + (x + 1)];
        }

        // --- TOP PADDING (Row 33 en textura) ---
        if (top != null)
        {
            int rowStart = (paddedSize - 1) * paddedSize;
            int prevRowStart = (paddedSize - 2) * paddedSize;
            for (int x = 0; x < chunkSize; x++)
                lightPixels[rowStart + (x + 1)] = top[x];
        }
        else // Fallback
        {
            int rowStart = (paddedSize - 1) * paddedSize;
            int prevRowStart = (paddedSize - 2) * paddedSize;
            for (int x = 0; x < chunkSize; x++)
                lightPixels[rowStart + (x + 1)] = lightPixels[prevRowStart + (x + 1)];
        }

        // --- LEFT PADDING (Col 0) ---
        if (left != null)
        {
            for (int y = 0; y < chunkSize; y++)
                lightPixels[(y + 1) * paddedSize] = left[y];
        }
        else // Fallback
        {
            for (int y = 0; y < chunkSize; y++)
                lightPixels[(y + 1) * paddedSize] = lightPixels[(y + 1) * paddedSize + 1];
        }

        // --- RIGHT PADDING (Col 33) ---
        if (right != null)
        {
            for (int y = 0; y < chunkSize; y++)
                lightPixels[(y + 1) * paddedSize + (paddedSize - 1)] = right[y];
        }
        else // Fallback
        {
            for (int y = 0; y < chunkSize; y++)
                lightPixels[(y + 1) * paddedSize + (paddedSize - 1)] = lightPixels[(y + 1) * paddedSize + (paddedSize - 2)];
        }

        //corners
        lightPixels[0] = (Color32)Color.Lerp((Color)lightPixels[1], (Color)lightPixels[paddedSize], 0.5f);
        lightPixels[paddedSize - 1] = (Color32)Color.Lerp((Color)lightPixels[paddedSize - 2], (Color)lightPixels[paddedSize + (paddedSize - 1)], 0.5f);
        lightPixels[(paddedSize - 1) * paddedSize] = (Color32)Color.Lerp((Color)lightPixels[(paddedSize - 1) * paddedSize + 1], (Color)lightPixels[(paddedSize - 2) * paddedSize], 0.5f);
        lightPixels[(paddedSize - 1) * paddedSize + (paddedSize - 1)] = (Color32)Color.Lerp((Color)lightPixels[(paddedSize - 1) * paddedSize + (paddedSize - 2)], (Color)lightPixels[(paddedSize - 2) * paddedSize + (paddedSize - 1)], 0.5f);

        lightTexture.SetPixels32(lightPixels);
        lightTexture.Apply(false); // false para no generar mipmaps
        meshRenderer.material.SetTexture("_LightTex", lightTexture);
    }

    #endregion

    #region CHUNK UPDATE

    private bool isDirty = false; // Modification Flag
    public bool IsDirty => isDirty;

    public void UpdateChunk()
    {
        GenerateMesh();
        GenerateCollider();
    }
    public bool PlaceBlock(Vector2Int pos, int newBlock, bool isFront)
    {
        int current = isFront ? blocks[pos.x, pos.y] : backBlocks[pos.x, pos.y];

        // Colocar aire en aire o colocar bloque donde ya hay un bloque.
        if ((current != 0 && newBlock != 0) || (current == 0 && newBlock == 0)) return false;

        isAir = false;
        isDirty = true;
        if (isFront)
            blocks[pos.x, pos.y] = newBlock;
        else
            backBlocks[pos.x, pos.y] = newBlock;

        UpdateChunk();

        return true;
    }
    #endregion

    #region Interactions
    
    /// <summary>
    /// Outside Script Interaction -- Get he block ID at local position
    /// </summary>
    /// <param name="position"></param>
    /// <returns></returns>
    public int GetBlockAtPosition(Vector2Int position, bool isFrontBlock)
    {
        return isFrontBlock ? blocks[position.x, position.y] : backBlocks[position.x, position.y];
    }

    /// <summary>
    /// Internal Script Interaction -- Get the block ID at offset position, including inmediate neighbor chunks
    /// </summary>
    /// <param name="offsetX"></param>
    /// <param name="offsetY"></param>
    /// <returns></returns>
    int GetBlock(int offsetX, int offsetY, bool isFrontBlock)
    {
        if (offsetX >= 0 && offsetX < chunkSize && offsetY >= 0 && offsetY < chunkSize)
        {
            return isFrontBlock ? blocks[offsetX, offsetY] : backBlocks[offsetX, offsetY];
        }

        int globalX = (position.x * chunkSize) + offsetX;
        int globalY = (position.y * chunkSize) + offsetY;

        int targetChunkX = Mathf.FloorToInt((float)globalX / chunkSize);
        int targetChunkY = Mathf.FloorToInt((float)globalY / chunkSize);

        int localX = globalX - (targetChunkX * chunkSize);
        int localY = globalY - (targetChunkY * chunkSize);

        return worldManager.getBlockOfChunk(new Vector2Int(targetChunkX, targetChunkY), new Vector2Int(localX, localY), isFrontBlock);
    }


    #endregion

    private void OnDestroy()
    {
        if (lightTexture != null)
        {
            Destroy(lightTexture);
        }
    }

}
