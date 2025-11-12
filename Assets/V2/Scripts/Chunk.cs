using NUnit.Framework.Constraints;
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.VisualScripting;
using Unity.VisualScripting.Antlr3.Runtime.Tree;
using UnityEditor.Searcher;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.LightTransport;
using UnityEngine.UIElements;
using static Unity.Collections.AllocatorManager;



[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(PolygonCollider2D))]
public class Chunk : MonoBehaviour
{
    public static int chunkSize = 32;

    MeshFilter meshFilter;
    MeshRenderer meshRenderer;
    PolygonCollider2D polyCollider;

    [SerializeField]Blockdictionary blockdictionary;
    WorldMetaData wmd;
    WorldManager worldManager;

    //Se creara al instanciar el chunk.
    Dictionary<long, Vector3> vertices = new();
    Dictionary<long, int> triangles = new();
    Dictionary<long, Vector2> uvs = new();
    Dictionary<Vector2, List<Vector2>> edges = new();

    List<Vector2[]> collisions = new();
    public List<Vector2[]> Collisions => collisions;

    int vertexIndex = 0;


    private Vector2Int position;
    public Vector2Int Position => position;

    private int[,] blocks;
    public int[,] Blocks => blocks;

    private bool isDirty = false; // Modification Flag
    public bool IsDirty => isDirty;

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
                polyCollider.SetPath(i, SimplifyPath(collisions.First().ToList()));
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
    }

    #region CREATION OF CHUNK
    //Generador de mesh y collider En primera instancia.
    void GenerateMesh()
    {
        vertices.Clear();
        triangles.Clear();
        uvs.Clear();

        vertexIndex = 0;

        for (int y = 0; y < chunkSize; y++)
        {
            for (int x = 0; x < chunkSize; x++)
            {
                long key = GetKey(new(x, y));
                int blockID = blocks[x, y];
                if (blockID == 0) continue; // 0 = vacío o aire

                // agrega los vértices del quad
                vertices.Add(key, new Vector3(x, y, 0));
                vertices.Add(key + 1, new Vector3(x, y + 1, 0));
                vertices.Add(key + 2, new Vector3(x + 1, y + 1, 0));
                vertices.Add(key + 3, new Vector3(x + 1, y, 0));

                // triángulos (dos por quad)
                triangles.Add(key, vertexIndex);
                triangles.Add(key + 1, vertexIndex + 1);
                triangles.Add(key + 2, vertexIndex + 2);
                triangles.Add(key + 3, vertexIndex);
                triangles.Add(key + 4, vertexIndex + 2);
                triangles.Add(key + 5, vertexIndex + 3);

                // UVs según atlas
                Vector2[] tileUVs = getUvs(new(x, y), blockID);

                uvs.Add(key, tileUVs[0]);
                uvs.Add(key + 1, tileUVs[1]);
                uvs.Add(key + 2, tileUVs[2]);
                uvs.Add(key + 3, tileUVs[3]);


                vertexIndex += 4;
            }
        }
        SetMesh();

    }

    void GenerateMeshAndCollider()
    {
        edges.Clear();
        vertices.Clear();
        triangles.Clear();
        uvs.Clear();

        vertexIndex = 0;

        for (int y = 0; y < chunkSize; y++)
        {
            for (int x = 0; x < chunkSize; x++)
            {

                //MESHES
                long key = GetKey(new(x, y));
                int blockID = blocks[x, y];
                if (blockID == 0) continue; // 0 = vacío o aire

                // agrega los vértices del quad
                vertices.Add(key, new Vector3(x, y, 0));
                vertices.Add(key + 1, new Vector3(x, y + 1, 0));
                vertices.Add(key + 2, new Vector3(x + 1, y + 1, 0));
                vertices.Add(key + 3, new Vector3(x + 1, y, 0));

                // triángulos (dos por quad)
                triangles.Add(key, vertexIndex);
                triangles.Add(key + 1, vertexIndex + 1);
                triangles.Add(key + 2, vertexIndex + 2);
                triangles.Add(key + 3, vertexIndex);
                triangles.Add(key + 4, vertexIndex + 2);
                triangles.Add(key + 5, vertexIndex + 3);

                // UVs según atlas
                Vector2[] tileUVs = getUvs(new(x, y), blockID);

                uvs.Add(key, tileUVs[0]);
                uvs.Add(key + 1, tileUVs[1]);
                uvs.Add(key + 2, tileUVs[2]);
                uvs.Add(key + 3, tileUVs[3]);


                vertexIndex += 4;

                //COLLIDERS



            }
        }
        
        //MESHES
        SetMesh();

        //COLLIDERS
        List<List<Vector2>> paths = CreatePaths();
        polyCollider.pathCount = paths.Count;
        for (int i = 0; i < paths.Count; i++)
        {
            polyCollider.SetPath(i, paths[i].ToArray());
        }
    }
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
                else if(!down && !right && up && left)
                {
                    AddEdge(bottomLeft, topRight);     // down-right
                    down = true; right = true;
                }
                else if(!down && !left && up && right)
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

    #region CHUNK UPDATE
    //Funcion principal para actualizar el chunk al colocar o quitar un bloque.

    public void UpdateChunk()
    {
        GenerateMesh();
        GenerateCollider();
    }

    public void PlaceBlock(Vector2Int position, int newBlock)
    {
        // Verifica que la posición esté dentro del chunk
        int current = blocks[position.x, position.y];
        if (current == newBlock || position.x < 0 ||
           position.x >= chunkSize || position.y < 0 || position.y >= chunkSize) return;

        isDirty = true;

        if (newBlock == 0 || (current == 0 && newBlock != 0) )
        {
            blocks[position.x, position.y] = newBlock;
            GenerateMesh();
            GenerateCollider();
            return;
        }
        if (current != 0 && newBlock != 0) //actualizacion dinamica (genera un pequeño aumento de rendimiento al cambiar un bloque por otro que no sea aire)
        {
            blocks[position.x, position.y] = newBlock;
            long key = GetKey(position);
            var tileUVs = getUvs(position, newBlock);
            uvs[key] = tileUVs[0];
            uvs[key + 1] = tileUVs[1];
            uvs[key + 2] = tileUVs[2];
            uvs[key + 3] = tileUVs[3];
            meshFilter.mesh.SetUVs(0, uvs.Values.ToArray());
        }
    }

    #endregion


    //Actualiza los valores del mesh. Triangles, vertices and uvs
    void SetMesh()
    {
        meshFilter.mesh.Clear();
        meshFilter.mesh.SetVertices(vertices.Values.ToArray());
        meshFilter.mesh.SetTriangles(triangles.Values.ToArray(), 0);
        meshFilter.mesh.SetUVs(0, uvs.Values.ToArray());

        Vector3[] normals = new Vector3[vertices.Count];
        for (int i = 0; i < normals.Length; i++) normals[i] = Vector3.forward;
        meshFilter.mesh.normals = normals;

    }

    Vector2[] getUvs(Vector2Int position, int block)
    {
        int x = position.x, y = position.y;
        byte relation = 0;

        // --- Patrones internos + externos combinados ---
        relation |= (byte)(IsNeighborSolid(x - 1, y + 1)  ? 1 << 0 : 0);
        relation |= (byte)(IsNeighborSolid(x, y + 1)      ? 1 << 1 : 0);
        relation |= (byte)(IsNeighborSolid(x + 1, y + 1)  ? 1 << 2 : 0);
        relation |= (byte)(IsNeighborSolid(x + 1, y)      ? 1 << 3 : 0);
        relation |= (byte)(IsNeighborSolid(x + 1, y - 1)  ? 1 << 4 : 0);
        relation |= (byte)(IsNeighborSolid(x, y - 1)      ? 1 << 5 : 0);
        relation |= (byte)(IsNeighborSolid(x - 1, y - 1)  ? 1 << 6 : 0);
        relation |= (byte)(IsNeighborSolid(x - 1, y)      ? 1 << 7 : 0);


        return blockdictionary.GetTileUVs(block, relation);
    }
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

    private long GetKey(Vector2Int position)
    {
        // Empaquetamos x e y en un long de 64 bits
    long x_long = (long)position.x << 32;
    long y_long = (long)position.y & 0xFFFFFFFF;

    // Clave base sin colisiones
    long baseKey = x_long | y_long;

    // Reservamos 6 posiciones para cada clave
    return baseKey * 6;
    }

}
