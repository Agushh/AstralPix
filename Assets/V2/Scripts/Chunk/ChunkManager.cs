using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;



[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(PolygonCollider2D))]
public class ChunkManager : MonoBehaviour
{
    public static int chunkSize = 32;

    MeshFilter meshFilter;
    MeshRenderer meshRenderer;
    PolygonCollider2D polyCollider;

    [SerializeField] Blockdictionary blockdictionary; //loaded by inspector

    WorldMetaData wmd; //Meta data of the world where is the chunk

    WorldManager worldManager; //Father controller that insantiate and let interact with other chunks.

    //Mesh data
    List<Vector3> vertices = new();
    List<int> triangles = new();
    List<Vector2> uvs = new();
    int vertexIndex = 0;

    //Lightining
    List<Vector2> uvs2 = new(); // RealWorldUvs Position
    Queue<Vector2Int> emmiters = new();
    HashSet<Vector2Int> externalEmmiters = new();
    Dictionary<Vector2Int, List<Vector2Int>> externalRelation = new();
    const float DECAY = 0.5f;         // caída por bloque (10%)
    const float THRESHOLD = 0.05f;    // debajo de esto, se considera 0


    //Collision data
    Dictionary<Vector2, List<Vector2>> edges = new();
    List<Vector2[]> collisions = new();
    public List<Vector2[]> Collisions => collisions;




    //ChunkData
    private Vector2Int position;
    private int[,] blocks;
    private Color[,] lightMap = new Color[chunkSize,chunkSize];

    private bool isDirty = false; // Modification Flag
    public Vector2Int Position => position;
    public int[,] Blocks => blocks;
    public bool IsDirty => isDirty;

    bool dirtyLight;

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
        GenerateMesh();
        dirtyLight = true;
    }
    private void Update()
    {
        if(dirtyLight)
        {

            CalculateLightning();
            dirtyLight = false;
        }
    }

    #region Meshes
    void GenerateMesh()
    {
        vertices.Clear();
        triangles.Clear();
        uvs.Clear();
        uvs2.Clear();

        vertexIndex = 0;

        for (int y = 0; y < chunkSize; y++)
        {
            for (int x = 0; x < chunkSize; x++)
            {
                int blockID = blocks[x, y];
                if (blockID == 0) continue; // 0 = vacío o aire

                // agrega los vértices del quad
                vertices.Add(new Vector3(x, y, 0));
                vertices.Add(new Vector3(x, y + 1, 0));
                vertices.Add(new Vector3(x + 1, y + 1, 0));
                vertices.Add(new Vector3(x + 1, y, 0));

                // triángulos (dos por quad)
                triangles.Add(vertexIndex);
                triangles.Add(vertexIndex + 1);
                triangles.Add(vertexIndex + 2);
                triangles.Add(vertexIndex);
                triangles.Add(vertexIndex + 2);
                triangles.Add(vertexIndex + 3);

                // UVs según atlas
                Vector2[] tileUVs = getUvs(new(x, y), blockID);

                uvs.Add(tileUVs[0]);
                uvs.Add(tileUVs[1]);
                uvs.Add(tileUVs[2]);
                uvs.Add(tileUVs[3]);

                //Uvs in world (for shader and lightning.
                uvs2.Add(new Vector3(x, y, 0));
                uvs2.Add(new Vector3(x, y + 1, 0));
                uvs2.Add(new Vector3(x + 1, y + 1, 0));
                uvs2.Add(new Vector3(x + 1, y, 0));

                vertexIndex += 4;
            }
        }
        SetMesh();
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
    }
    Vector2[] getUvs(Vector2Int position, int block)
    {
        int x = position.x, y = position.y;
        byte relation = 0;

        // --- Patrones internos + externos combinados ---
        relation |= (byte)(IsNeighborSolid(x - 1, y + 1) ? 1 << 0 : 0);
        relation |= (byte)(IsNeighborSolid(x, y + 1) ? 1 << 1 : 0);
        relation |= (byte)(IsNeighborSolid(x + 1, y + 1) ? 1 << 2 : 0);
        relation |= (byte)(IsNeighborSolid(x + 1, y) ? 1 << 3 : 0);
        relation |= (byte)(IsNeighborSolid(x + 1, y - 1) ? 1 << 4 : 0);
        relation |= (byte)(IsNeighborSolid(x, y - 1) ? 1 << 5 : 0);
        relation |= (byte)(IsNeighborSolid(x - 1, y - 1) ? 1 << 6 : 0);
        relation |= (byte)(IsNeighborSolid(x - 1, y) ? 1 << 7 : 0);


        return blockdictionary.GetTileUVs(block, relation);
    }
    #endregion

    #region ColliderGeneration
    void GenerateCollider()
    {
        edges.Clear();

        for (int y = 0; y < chunkSize; y++)
        {
            for (int x = 0; x < chunkSize; x++)
            {
                if (blocks[x, y] == 0) continue;

                bool up = IsNeighborSolid(x, y + 1);
                bool down = IsNeighborSolid(x, y - 1);
                bool left = IsNeighborSolid(x - 1, y);
                bool right = IsNeighborSolid(x + 1, y);

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

    #region Lightning
    void CalculateLightning()
    {
        emmiters.Clear();
        for (int x = 0; x < chunkSize; x++)
        {
            for(int y = 0; y < chunkSize; y++)
            {
                if (getBlockData(x, y).EmitLight)
                {
                    lightMap[x, y] = getBlockData(x, y).lightColor;
                    emmiters.Enqueue(new Vector2Int(x, y));
                }
                else if(!externalEmmiters.Contains(new Vector2Int(x, y)))
                    lightMap[x, y] = Color.black;
            }
        }

        // Cola local para BFS (usá una lista o cola ya declarada)
        Queue<Vector2Int> q = new Queue<Vector2Int>();

        // Inicial: encolá todos los emisores que ya estén en emmiters
        foreach (var e in externalEmmiters)
        {
            // sanity check
            if (e.x < 0 || e.y < 0 || e.x >= chunkSize || e.y >= chunkSize) continue;
            q.Enqueue(e);
        }
        while (emmiters.Count > 0)
        {
            var e = emmiters.Dequeue();
            // sanity check
            if (e.x < 0 || e.y < 0 || e.x >= chunkSize || e.y >= chunkSize) continue;
            q.Enqueue(e);
        }
        while (q.Count > 0)
        {
            Vector2Int pos = q.Dequeue();
            Color currentColor = lightMap[pos.x, pos.y];

            // si current es muy débil, saltear, es decir una luz inicial muy debil, que no aguantaria ni un ciclo.
            if (MaxComponent(currentColor) < THRESHOLD)
            {
                continue;
            }

            foreach (Vector2Int n in Neighbors4(pos.x, pos.y))
            {
                Color proposed = currentColor * (1f - DECAY);
                if (n.x < 0 || n.y < 0 || n.x >= chunkSize || n.y >= chunkSize)
                {
                    //delegate to world manager
                    //save a List that contains every neighbour tile that a tile interact with, because when it is deleted, it has to notify all of them.
                    externalRelation.TryGetValue(pos, out var list);
                    if (list == null || list.Count == 0)
                    {
                        list = new() { n };
                    }
                    else if(!list.Contains(n))
                    {
                        list.Add(n);
                    }
                    bool loaded = worldManager.sendLight(n, position, proposed);
                    externalRelation[pos] = list;
                    continue;
                }

                if (blocks[n.x, n.y] == 0) continue;
                if (MaxComponent(proposed) < THRESHOLD)
                {
                    continue;
                }
                
                if (MaxComponent(proposed) > MaxComponent(lightMap[n.x, n.y]))
                {
                    lightMap[n.x, n.y] = CombineColors(lightMap[n.x, n.y], proposed);
                    q.Enqueue(new Vector2Int(n.x, n.y));
                }
            }
        }

        UpdateLightTextureFromLightMap();
    }

    void UpdateLightTextureFromLightMap()
    {
        int paddedSize = chunkSize + 2;
        Texture2D lightTex = new Texture2D(paddedSize, paddedSize, TextureFormat.RGBA32, false, true);
        lightTex.filterMode = FilterMode.Bilinear;
        lightTex.wrapMode = TextureWrapMode.Clamp;
        Color32[] pixels = new Color32[paddedSize * paddedSize];
        for (int y = 0; y < chunkSize; y++)
        {
            for (int x = 0; x < chunkSize; x++)
            {
                Color c = lightMap[x, y];
                c.r = Mathf.Min(c.r, 1f);
                c.g = Mathf.Min(c.g, 1f);
                c.b = Mathf.Min(c.b, 1f);
                pixels[(y + 1) * paddedSize + (x + 1)] = (Color32)c;
            }
        }
        // Copiar bordes (padding de 1 píxel)
         // Top y bottom
        for (int x = 0; x < chunkSize; x++)
        {
            pixels[0 * paddedSize + (x + 1)] = pixels[1 * paddedSize + (x + 1)];                               // fila superior
            pixels[(paddedSize - 1) * paddedSize + (x + 1)] = pixels[(paddedSize - 2) * paddedSize + (x + 1)]; // fila inferior
        }

        // Left y right
        for (int y = 0; y < paddedSize; y++)
        {
            pixels[y * paddedSize + 0] = pixels[y * paddedSize + 1];                               // columna izquierda
            pixels[y * paddedSize + (paddedSize - 1)] = pixels[y * paddedSize + (paddedSize - 2)]; // columna derecha
        }

        lightTex.SetPixels32(pixels);
        lightTex.Apply(false); // false para no generar mipmaps
        meshRenderer.material.SetTexture("_LightTex", lightTex);
    }

    float MaxComponent(Color c) => Mathf.Max(c.r, Mathf.Max(c.g, c.b));

    IEnumerable<Vector2Int> Neighbors4(int x, int y)
    {
        yield return new Vector2Int(x + 1, y);
        yield return new Vector2Int(x - 1, y);
        yield return new Vector2Int(x, y + 1);
        yield return new Vector2Int(x, y - 1);
    }
    Color CombineColors(Color existing, Color incoming)
    {
        float blend = 0.5f;
        return new Color(
            Mathf.Lerp(existing.r, incoming.r, blend),
            Mathf.Lerp(existing.g, incoming.g, blend),
            Mathf.Lerp(existing.b, incoming.b, blend)
        );
    }

    //Neighbour comunication

    public bool ReceiveExternalLight(Vector2Int localPos, Color incoming)
    {
        // si la luz entrante supera el existente, actualizá y encolá para procesar
        if (MaxComponent(incoming) > MaxComponent(lightMap[localPos.x, localPos.y]))
        {
            lightMap[localPos.x, localPos.y] = incoming;
            externalEmmiters.Add(localPos);
            dirtyLight = true;
            return true;
        }
        return false;
    }
    public void deleteExternalLight(Vector2Int localPos)
    {
        if (getBlockData(localPos.x, localPos.y).EmitLight)
            lightMap[localPos.x, localPos.y] = getBlockData(localPos.x, localPos.y).lightColor;
        else
            lightMap[localPos.x, localPos.y] = Color.black;

        externalEmmiters.Remove(localPos);
        dirtyLight = true;
    }

    #endregion

    #region CHUNK UPDATE
    public void UpdateChunk()
    {
        GenerateMesh();
        GenerateCollider();
        dirtyLight = true;
    }
    public bool PlaceBlock(Vector2Int pos, int newBlock)
    {
        // Verifica que la posición esté dentro del chunk
        int current = blocks[pos.x, pos.y];
        if (current == newBlock || pos.x < 0 ||
            pos.x >= chunkSize || pos.y < 0 || pos.y >= chunkSize)
            return false;

        // Colocar aire en aire o colocar bloque donde ya hay un bloque.
        if (current != 0 && newBlock != 0 || current == 0 && newBlock == 0) return false;
        
        if(externalRelation.ContainsKey(pos) && externalRelation[pos] != null && externalRelation[pos].Count > 0)
        {
            foreach (var external in externalRelation[pos])
            {
                worldManager.deleteLight(external, position);
            }
        }

        isDirty = true;
        blocks[pos.x, pos.y] = newBlock;
        UpdateChunk();
        return true;
    }

    #endregion

    #region Interactions I/O
    bool IsNeighborSolid(int checkX, int checkY)
    {
        // Caso 1: El vecino está DENTRO del chunk actual
        if (checkX >= 0 && checkX < chunkSize &&
            checkY >= 0 && checkY < chunkSize)
        {
            return blocks[checkX, checkY] != 0; // O quizás '== block' ? (ver punto extra)
        }

        // Caso 2: El vecino está FUERA del chunk actual
        // Necesitamos averiguar en qué chunk vecino está y qué coordenada local tiene.

        // worldPos es la posición global del bloque que estamos comprobando
        Vector2Int worldPos = new Vector2Int(
            position.x * chunkSize + checkX,
            position.y * chunkSize + checkY
        );

        // worldManager.getBlock(worldPos) debería ser una función que te 
        // devuelva el bloque en una POSICIÓN GLOBAL. 
        // Esto es mucho más simple que calcular índices de chunks a mano.

        // Si no tienes una función así, tendrás que calcularlo:
        Vector2Int neighborChunkIndex = position;
        Vector2Int localIndexInNeighbor = new Vector2Int(checkX, checkY);

        if (checkX < 0)
        {
            neighborChunkIndex.x -= 1;
            localIndexInNeighbor.x = chunkSize + checkX; // p.ej., 16 + (-1) = 15
        }
        else if (checkX >= chunkSize)
        {
            neighborChunkIndex.x += 1;
            localIndexInNeighbor.x = checkX - chunkSize; // p.ej., 16 - 16 = 0
        }

        if (checkY < 0)
        {
            neighborChunkIndex.y -= 1;
            localIndexInNeighbor.y = chunkSize + checkY;
        }
        else if (checkY >= chunkSize)
        {
            neighborChunkIndex.y += 1;
            localIndexInNeighbor.y = checkY - chunkSize;
        }

        int neighborBlock = worldManager.getBlockOfChunk(neighborChunkIndex, localIndexInNeighbor);

        return neighborBlock != 0; // O '== block'
    }

    public int GetBlockAtPosition(Vector2Int position)
    {
        return blocks[position.x, position.y];
    }

    tileData getBlockData(int x, int y)
    {
        return blockdictionary.tiles[blocks[x, y]];
    }

    #endregion
}
