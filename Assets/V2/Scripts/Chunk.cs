using NUnit.Framework.Constraints;
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
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
    struct dir
    {
        public Vector2Int from, to;
        public dir(Vector2Int from, Vector2Int to)
        {
            this.from = from;
            this.to = to;
        }

        // override object.Equals
        // Implementación de IEquatable<T>
        public bool Equals(dir other)
        {
            return from == other.from && to == other.to;
        }
    }


    MeshFilter meshFilter;
    MeshRenderer meshRenderer;
    PolygonCollider2D polyCollider;

    Blockdictionary blockdictionary;
    WorldDataScObj worldData;
    WorldManager worldManager;

    //Se creara al instanciar el chunk.
    Dictionary<long, Vector3> vertices = new();
    Dictionary<long, int> triangles = new();
    Dictionary<long, Vector2> uvs = new();
    int vertexIndex = 0;

    Dictionary<int, List<dir>> colliderPoints;

    Vector2Int chunkIndex;
    public Vector2Int ChunkIndex => chunkIndex;

    int[,] blocks;
    int[,] blockCollisionComponents;
    int PathCounts;
    int[] parent;


    [SerializeField] float timeIntervalToSave = 100f;
    float timer = 0;

    bool hasBeenModified = false;

    //"Constructor de chunk". Se utiliza al deserealizar, luego de instanciar, y se setean sus valores desde WorldManager.
    public void SetData(Vector2Int index, WorldDataScObj worldData, Blockdictionary blockdictionary, WorldManager worldManager)
    {
        chunkIndex = index;
        this.worldData = worldData;
        this.blockdictionary = blockdictionary;
        this.worldManager = worldManager;

        meshFilter = GetComponent<MeshFilter>();
        meshRenderer = GetComponent<MeshRenderer>();
        polyCollider = GetComponent<PolygonCollider2D>();

        DeserializableChunk chunk = worldData.GetBlocksData(chunkIndex);
        blocks = chunk.blocksData;
        blockCollisionComponents = chunk.collisionData;

        if(blockCollisionComponents == null)
            CalculateColliderMatrix();
        else
        {
            PathCounts = blockCollisionComponents.Cast<int>().Max();
            initUnionFind();
            LazyUpdateChunk(); // compress paths so parent[] matches
        }
        GenerateMeshAndCollider();
    }

    #region CREATION OF CHUNK
    //Generador de mesh y collider En primera instancia.
    void GenerateMeshAndCollider()
    {
        vertices.Clear();
        triangles.Clear();
        uvs.Clear();
        int width = worldData.ChunkSize, height = worldData.ChunkSize;

        colliderPoints = new();

        vertexIndex = 0;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
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

        CollectColliderEdges();

        meshFilter.mesh = new Mesh();
        meshFilter.mesh.MarkDynamic();
        SetMesh();

        GenerateCollider();

    }
    
    //Generador de matriz de componentes conexas.
    public void CalculateColliderMatrix()
    {
        Debug.Log("Generating...");
        int width = worldData.ChunkSize, height = worldData.ChunkSize;
        List<Vector2Int> unionsBuffer = new();

        blockCollisionComponents = new int[width, height];

        PathCounts = 0;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (blocks[x, y] != 0)
                {
                    int up = 0, left = 0;
                    if (x == 0 && y == 0)
                    {
                        PathCounts++;
                        blockCollisionComponents[x, y] = PathCounts;
                        continue;
                    }
                    if (x == 0)
                    {
                        if (blockCollisionComponents[x, y - 1] != 0)
                            blockCollisionComponents[x, y] = blockCollisionComponents[x, y - 1];
                        else
                        {
                            PathCounts++;
                            blockCollisionComponents[x, y] = PathCounts;
                        }
                        continue;
                    }
                    if (y == 0)
                    {
                        if (blockCollisionComponents[x - 1, y] != 0)
                            blockCollisionComponents[x, y] = blockCollisionComponents[x - 1, y];
                        else
                        {
                            PathCounts++;
                            blockCollisionComponents[x, y] = PathCounts;
                        }
                        continue;
                    }

                    left = blockCollisionComponents[x - 1, y];
                    up = blockCollisionComponents[x, y - 1];
                    if (left != 0 && up != 0)
                    {
                        if (left != up)
                        {
                            // Unir componentes
                            int oldComponent = left;
                            unionsBuffer.Add(new(left, up));
                        }
                        blockCollisionComponents[x, y] = up;
                    }
                    else if (left != 0)
                    {
                        blockCollisionComponents[x, y] = left;
                    }
                    else if (up != 0)
                    {
                        blockCollisionComponents[x, y] = up;
                    }
                    else
                    {
                        PathCounts++;
                        blockCollisionComponents[x, y] = PathCounts;
                    }
                }
                else
                {
                    blockCollisionComponents[x, y] = 0;
                }
            }
        }
        initUnionFind();
        foreach (Vector2Int union in unionsBuffer)
        {
            Union(union.x, union.y);
        }
    }
    
    #endregion


    //Algoritmos para cambios en la matriz de componentes conexas.
    #region FloodFill limitado
    void RemoveBlock(int x, int y)
    {
        int oldID = Find(blockCollisionComponents[x, y]);
        blockCollisionComponents[x, y] = 0;

        // vecinos sólidos
        var solidNeighbors = GetSolidNeighbors(x, y);

        // si no hay sólidos cerca, no hay nada más que hacer
        if (solidNeighbors.Count == 0)
            return;

        // exploramos las posibles nuevas regiones
        RecalculateSplitRegions(solidNeighbors, oldID);
    }
    List<(int, int)> GetSolidNeighbors(int x, int y)
    {
        var result = new List<(int, int)>();

        int[,] dirs = {
            {  1,  0 },
            { -1,  0 },
            {  0,  1 },
            {  0, -1 }
        };

        for (int d = 0; d < 4; d++)
        {
            int nx = x + dirs[d, 0], ny = y + dirs[d, 1];
            if (nx < 0 || ny < 0 || nx >= worldData.ChunkSize || ny >= worldData.ChunkSize)
                continue;
            if (blockCollisionComponents[nx, ny] != 0)
                result.Add((nx, ny));
        }
        return result;
    }
    void RecalculateSplitRegions(List<(int, int)> seeds, int oldID)
    {
        HashSet<(int, int)> visited = new();
        List<List<(int, int)>> foundRegions = new();

        foreach (var seed in seeds)
        {
            // si ya fue visitado por otro flood-fill, saltamos
            if (visited.Contains(seed))
                continue;

            // solo consideramos bloques del mismo componente
            if (Find(blockCollisionComponents[seed.Item1, seed.Item2]) != oldID)
                continue;

            // nueva region detectada
            List<(int, int)> region = new();
            FloodFillCollect(seed.Item1, seed.Item2, oldID, region, visited);
            foundRegions.Add(region);
        }

        // si hay mas de una region -> se partio el componente
        if (foundRegions.Count > 1)
        {
            for (int i = 1; i < foundRegions.Count; i++)
            {
                PathCounts++;
                int newID = PathCounts;

                if (PathCounts >= parent.Length)
                    Array.Resize(ref parent, PathCounts + 1);

                parent[newID] = newID;

                foreach (var (rx, ry) in foundRegions[i])
                    blockCollisionComponents[rx, ry] = newID;
            }
        }
    }
    void FloodFillCollect(int x, int y, int oldID, List<(int, int)> region, HashSet<(int, int)> visited)
    {
        Queue<(int, int)> queue = new();
        queue.Enqueue((x, y));
        visited.Add((x, y));

        int[,] dirs = { { 1, 0 }, { -1, 0 }, { 0, 1 }, { 0, -1 } };

        while (queue.Count > 0)
        {
            var (cx, cy) = queue.Dequeue();
            region.Add((cx, cy));

            for (int d = 0; d < 4; d++)
            {
                int nx = cx + dirs[d, 0], ny = cy + dirs[d, 1];
                if (nx < 0 || ny < 0 || nx >= worldData.ChunkSize || ny >= worldData.ChunkSize)
                    continue;

                if (visited.Contains((nx, ny))) continue;

                if (Find(blockCollisionComponents[nx, ny]) == oldID)
                {
                    visited.Add((nx, ny));
                    queue.Enqueue((nx, ny));
                }
            }
        }
    }

    #endregion

    #region Union-Find
    //Algoritmo de Unión-Find para manejar componentes conectados. Optimizacion de colocacion de bloques mediante una tabla de indices conectados. Funcion Recursiva
    int Find(int a)
    {
        if (parent[a] != a)
            parent[a] = Find(parent[a]); // compresión de caminos
        return parent[a];
    }
    //Generacion de conexion entre componentes/indices.
    void Union(int a, int b)
    {
        int rootA = Find(a);
        int rootB = Find(b);
        if (rootA != rootB)
            parent[rootB] = rootA;
    }
    //inicializacion de tabla de componentes/indices. Se genera despues de CalculateColliders(), se actualiza al crear nuevos componentes.
    void initUnionFind()
    {
        parent = new int[PathCounts + 1];
        for (int i = 0; i <= PathCounts; i++)
            parent[i] = i;  //Eliminar los ceros. Cambio de indices.
    }
    #endregion


    
    #region CHUNK UPDATE
    //Funcion principal para actualizar el chunk al colocar o quitar un bloque.
    public void UpdateChunk(Vector2Int position, int newBlock)
    {
        // Verifica que la posición esté dentro del chunk
        int current = blocks[position.x, position.y];
        if (current == newBlock || position.x < 0 ||
           position.x >= worldData.ChunkSize || position.y < 0 || position.y >= worldData.ChunkSize) return;

        hasBeenModified = true;

        if (newBlock == 0)
        {
            UpdateMesh(position.x, position.y, 0);
            UpdateColliderMatrix(position.x, position.y);
            UpdateCollider();
            return;
        }

        // 2) Colocar sólido en aire
        if (current == 0 && newBlock != 0)
        {
            UpdateMesh(position.x, position.y, newBlock);
            UpdateColliderMatrix(position.x, position.y);
            UpdateCollider();
            return;
        }
        if (current != 0 && newBlock != 0)
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

    // Actualiza la matriz de componentes conexas al colocar o quitar un bloque. 
    void UpdateColliderMatrix(int i, int j)
    {
        //comprobacion del cambio a realizar
        bool toSolid = blocks[i, j] != 0;

        List<int> neighbourIDs = new();

        //Comprobacion de vecinos y rangos de matriz.
        int
            NeighbourLeft = (i == 0) ? 0 : blockCollisionComponents[i - 1, j],
            NeighbourRight = (i == worldData.ChunkSize - 1) ? 0 : blockCollisionComponents[i + 1, j],
            NeighbourDown = (j == 0) ? 0 : blockCollisionComponents[i, j - 1],
            NeighbourUp = (j == worldData.ChunkSize - 1) ? 0 : blockCollisionComponents[i, j + 1];

        //Comprobacion de indices virtuales. Tabla de Union-find.
        if (NeighbourLeft != 0) NeighbourLeft = Find(NeighbourLeft);
        if (NeighbourRight != 0) NeighbourRight = Find(NeighbourRight);
        if (NeighbourUp != 0) NeighbourUp = Find(NeighbourUp);
        if (NeighbourDown != 0) NeighbourDown = Find(NeighbourDown);

        neighbourIDs.Add(NeighbourLeft);
        neighbourIDs.Add(NeighbourRight);
        neighbourIDs.Add(NeighbourUp);
        neighbourIDs.Add(NeighbourDown);

        neighbourIDs.RemoveAll(id => id == 0); // Elimina los vacíos

        if (toSolid)
        {
            // Nuevo bloque sólido. No tiene vecinos sólidos
            if (neighbourIDs.Count == 0)
            {
                PathCounts++;
                blockCollisionComponents[i, j] = PathCounts; // Nuevo componente

                //resize de tabla de componentes.
                if (PathCounts + 1 >= parent.Length)
                    Array.Resize(ref parent, PathCounts + 1);

                parent[PathCounts] = PathCounts;
            }
            else
            {
                int newID = neighbourIDs.First();
                blockCollisionComponents[i, j] = newID;

                foreach (var id in neighbourIDs)
                {
                    if (id != newID)
                        Union(id, newID);
                }
            }
        }
        else
        {
            RemoveBlock(i, j);
        }
    }

    //Actualizador del mesh para Updates
    public void UpdateMesh(int i, int j, int block)
    {
        int width = worldData.ChunkSize, height = worldData.ChunkSize;
        if(i != -1)blocks[i, j] = block;

        vertices.Clear();
        triangles.Clear();
        uvs.Clear();
        vertexIndex = 0;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
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

    //Actualizador del collider para Updates
    public void UpdateCollider()
    {
        CollectColliderEdges();

        GenerateCollider();
    }
    void CollectColliderEdges()
    {
        int width = worldData.ChunkSize, height = worldData.ChunkSize;
        colliderPoints.Clear();

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int blockID = blocks[x, y];
                if (blockID == 0) continue;

                int id = Find(blockCollisionComponents[x, y]);

                if (id != 0)
                {
                    int
                        NeighbourLeft = (x == 0) ? 0 : blockCollisionComponents[x - 1, y],
                        NeighbourRight = (x == worldData.ChunkSize - 1) ? 0 : blockCollisionComponents[x + 1, y],
                        NeighbourDown = (y == 0) ? 0 : blockCollisionComponents[x, y - 1],
                        NeighbourUp = (y == worldData.ChunkSize - 1) ? 0 : blockCollisionComponents[x, y + 1];

                    if (NeighbourLeft != 0) NeighbourLeft = Find(NeighbourLeft);
                    if (NeighbourRight != 0) NeighbourRight = Find(NeighbourRight);
                    if (NeighbourUp != 0) NeighbourUp = Find(NeighbourUp);
                    if (NeighbourDown != 0) NeighbourDown = Find(NeighbourDown);

                    if (colliderPoints.TryGetValue(id, out var list) == false)
                        colliderPoints[id] = new List<dir>();

                    if (NeighbourUp == 0)
                        colliderPoints[id].Add(new(new (x, y + 1), new (x + 1, y + 1)));
                    if (NeighbourDown == 0)
                        colliderPoints[id].Add(new(new (x + 1, y), new (x, y)));
                    if (NeighbourLeft == 0)
                        colliderPoints[id].Add(new(new (x, y), new (x, y + 1)));
                    if (NeighbourRight == 0)
                        colliderPoints[id].Add(new(new (x + 1, y + 1), new (x + 1, y)));
                }
            }
        }
    }
    void GenerateCollider()
    {
        List<List<Vector2>> edgeOutput = new();

        // 1. Itera sobre las CLAVES del diccionario.
        //    (Iterar sobre 'parent' es incorrecto, tiene IDs duplicados y no-raíz)
        foreach (var id in colliderPoints.Keys)
        {
            if (id == 0) continue;
            if (!colliderPoints.ContainsKey(id)) continue;

            List<dir> edges = new List<dir>(colliderPoints[id]);

            // 2. Bucle EXTERNO: mientras queden aristas, sigue creando caminos
            //    (Esto maneja islas y agujeros en el mismo componente)
            while (edges.Count > 0)
            {
                // 3. Inicia un NUEVO camino
                List<Vector2> localOutput = new();
                dir edge = edges.First();
                edges.RemoveAt(0);

                localOutput.Add(edge.from);
                Vector2 startPoint = edge.from; // Guarda el inicio

                // 4. Bucle INTERNO: construye UN camino hasta cerrarlo
                while (edge.to != startPoint)
                {
                    Vector2 puntoAnterior = edge.from; // De dónde venimos
                    Vector2 puntoActual = edge.to;     // Dónde estamos

                    // 1. Encuentra TODOS los candidatos que salen de nuestro punto actual
                    var candidatosIndices = new List<int>();
                    for (int i = 0; i < edges.Count; i++)
                    {
                        if (edges[i].from == puntoActual)
                        {
                            candidatosIndices.Add(i);
                        }
                    }

                    int idx = -1; // El índice del ganador

                    if (candidatosIndices.Count == 0)
                    {
                        // No hay salida. Camino roto (tu lógica 'else' actual)
                    }
                    else if (candidatosIndices.Count == 1)
                    {
                        // El caso fácil. Solo hay un camino.
                        idx = candidatosIndices[0];
                    }
                    else
                    {
                        // ¡El caso difícil! El vértice no-múltiple (forma de 8).
                        // Tenemos que ELEGIR el camino correcto.

                        float mejorAngulo = -361; // Empezamos con un ángulo imposible
                        int mejorIdx = -1;

                        // Vector del camino por el que llegamos
                        Vector2 vectorEntrada = (puntoActual - puntoAnterior).normalized;

                        foreach (int candIdx in candidatosIndices)
                        {
                            Vector2 puntoCandidato = edges[candIdx].to;

                            // Vector del camino candidato de salida
                            Vector2 vectorSalida = (puntoCandidato - puntoActual).normalized;

                            // Calcula el ángulo con signo.
                            // Un giro a la "izquierda" será positivo, a la "derecha" será negativo.
                            float angulo = Vector2.SignedAngle(vectorEntrada, vectorSalida);

                            // Si el ángulo es ~0 o ~360, es una línea recta (probablemente la misma arista)
                            // Lo que buscamos es el giro MÁS POSITIVO (el giro más "a la izquierda")
                            // O el MÁS NEGATIVO (el giro más "a la derecha"), 
                            // ¡pero sé consistente!

                            // Usemos "siempre a la izquierda" (el ángulo positivo más grande)
                            // Nota: Los ángulos van de -180 a 180.
                            // Un ángulo de 179 (casi 180) es un giro más "a la izquierda" que 90.

                            // Para "siempre a la izquierda" (CCW), queremos el ángulo más positivo.
                            // Para "siempre a la derecha" (CW), queremos el ángulo más negativo.

                            // Vamos a asumir CCW (Counter-Clockwise):
                            // Ajustamos los ángulos negativos para que estén en el rango 0-360
                            float anguloNormalizado = angulo <= 0 ? 360 + angulo : angulo;

                            if (anguloNormalizado > mejorAngulo)
                            {
                                mejorAngulo = anguloNormalizado;
                                mejorIdx = candIdx;
                            }
                        }
                        idx = mejorIdx;
                    }
                    if (idx >= 0)
                    {

                        edge = edges[idx];

                        localOutput.Add(edge.from); // Añade el vértice de unión

                        edges.RemoveAt(idx);
                    }
                    else
                    {
                        // ¡Camino roto! No se cerró el bucle.
                        // Esto no debería pasar si la lógica de aristas es correcta,
                        // pero es bueno manejarlo.
                        localOutput.Add(edge.to); // Añade el punto final "abierto"
                        Debug.LogWarning($"Collider path component {id} is not a closed loop!");
                        break; // Rompe el bucle INTERNO
                    }
                }

                // 5. El bucle interno terminó (cerrado o roto).
                //    Guarda el camino SÍ O SÍ.
                edgeOutput.Add(localOutput);

            } // El bucle EXTERNO vuelve a empezar si quedan aristas (un agujero)
        }


        //implementar un ssistema de while(recto) delete Intermediate Points, para simplificcar vertices en el collider.
        //Implementar el sistema de deteccion de esquinas para redondearlas siempre que sean esquinas externas, no internas.




        // Asigna los caminos al collider

        polyCollider.pathCount = edgeOutput.Count;
        for (int i = 0; i < edgeOutput.Count; i++)
        {
            List<Vector2> simplifiedPath = SimplifyPath(edgeOutput[i]);
            polyCollider.SetPath(i, simplifiedPath);
        }

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



    private void Update()
    {
        timer += Time.deltaTime;
        if (timer >= timeIntervalToSave)
        {
            SaveChunk();
            timer = 0f;
        }
    }


    //Actualiza los valores del mesh. Triangles, vertices and uvs
    void SetMesh()
    {
        meshFilter.mesh.Clear();
        meshFilter.mesh = new Mesh();
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

        int UL = 0, U = 0, UR = 0,
            L = 0,          R = 0,
            DL = 0, D = 0, DR = 0;

        // --- Vecinos externos ---
        if (x < 0)
        {
            L = worldManager.getBlockOfChunk(this, chunkIndex + new Vector2Int(-1, 0),
                                             new Vector2Int(worldData.ChunkSize + x, y));
            if (y < 0)
                DL = worldManager.getBlockOfChunk(this, chunkIndex + new Vector2Int(-1, -1),
                                                  new Vector2Int(worldData.ChunkSize + x, worldData.ChunkSize + y));
            else if (y >= worldData.ChunkSize)
                UL = worldManager.getBlockOfChunk(this, chunkIndex + new Vector2Int(-1, 1),
                                                  new Vector2Int(worldData.ChunkSize + x, y - worldData.ChunkSize));
        }
        else if (x >= worldData.ChunkSize)
        {
            R = worldManager.getBlockOfChunk(this, chunkIndex + new Vector2Int(1, 0),
                                             new Vector2Int(x - worldData.ChunkSize, y));
            if (y < 0)
                DR = worldManager.getBlockOfChunk(this, chunkIndex + new Vector2Int(1, -1),
                                                  new Vector2Int(x - worldData.ChunkSize, worldData.ChunkSize + y));
            else if (y >= worldData.ChunkSize)
                UR = worldManager.getBlockOfChunk(this, chunkIndex + new Vector2Int(1, 1),
                                                  new Vector2Int(x - worldData.ChunkSize, y - worldData.ChunkSize));
        }

        if (y < 0)
            D = worldManager.getBlockOfChunk(this, chunkIndex + new Vector2Int(0, -1),
                                             new Vector2Int(x, worldData.ChunkSize + y));
        else if (y >= worldData.ChunkSize)
            U = worldManager.getBlockOfChunk(this, chunkIndex + new Vector2Int(0, 1),
                                             new Vector2Int(x, y - worldData.ChunkSize));

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
    // Pon esta función donde estaba "IsSolid"
    bool IsNeighborSolid(int checkX, int checkY)
    {
        // Caso 1: El vecino está DENTRO del chunk actual
        if (checkX >= 0 && checkX < worldData.ChunkSize &&
            checkY >= 0 && checkY < worldData.ChunkSize)
        {
            return blocks[checkX, checkY] != 0; // O quizás '== block' ? (ver punto extra)
        }

        // Caso 2: El vecino está FUERA del chunk actual
        // Necesitamos averiguar en qué chunk vecino está y qué coordenada local tiene.

        // worldPos es la posición global del bloque que estamos comprobando
        Vector2Int worldPos = new Vector2Int(
            chunkIndex.x * worldData.ChunkSize + checkX,
            chunkIndex.y * worldData.ChunkSize + checkY
        );

        // worldManager.getBlock(worldPos) debería ser una función que te 
        // devuelva el bloque en una POSICIÓN GLOBAL. 
        // Esto es mucho más simple que calcular índices de chunks a mano.

        // Si no tienes una función así, tendrás que calcularlo:
        Vector2Int neighborChunkIndex = chunkIndex;
        Vector2Int localIndexInNeighbor = new Vector2Int(checkX, checkY);

        if (checkX < 0)
        {
            neighborChunkIndex.x -= 1;
            localIndexInNeighbor.x = worldData.ChunkSize + checkX; // p.ej., 16 + (-1) = 15
        }
        else if (checkX >= worldData.ChunkSize)
        {
            neighborChunkIndex.x += 1;
            localIndexInNeighbor.x = checkX - worldData.ChunkSize; // p.ej., 16 - 16 = 0
        }

        if (checkY < 0)
        {
            neighborChunkIndex.y -= 1;
            localIndexInNeighbor.y = worldData.ChunkSize + checkY;
        }
        else if (checkY >= worldData.ChunkSize)
        {
            neighborChunkIndex.y += 1;
            localIndexInNeighbor.y = checkY - worldData.ChunkSize;
        }

        // ¡OJO! Asumo que worldManager.getBlockOfChunk puede manejar que 'this' (el chunk actual)
        // no sea el mismo que el del 'neighborChunkIndex'. 
        // Pasarle 'null' o buscar el chunk por índice podría ser más seguro.
        int neighborBlock = worldManager.getBlockOfChunk(null, neighborChunkIndex, localIndexInNeighbor);

        return neighborBlock != 0; // O '== block'
    }
    //Momentos donde se guarda el chunk.
    private void OnApplicationQuit()
    {
        SaveChunk();
    }
    private void OnDestroy()
    {
        // Por si el objeto se destruye antes de salir del juego
        SaveChunk();
    }
    private void SaveChunk()
    {
        if (worldData != null && blocks != null && hasBeenModified)
        {
            LazyUpdateChunk();
            worldData.UpdateChunk(chunkIndex, blocks, blockCollisionComponents);
            Debug.Log($"Chunk {chunkIndex} guardado.");
            hasBeenModified = false;
        }
    }

    private void LazyUpdateChunk()
    {
        for(int y = 0; y < worldData.ChunkSize; y ++)
        {
            for (int x = 0; x < worldData.ChunkSize; x++)
            {
                blockCollisionComponents[x, y] = Find(blockCollisionComponents[x, y]);
            }
        }
    }

    public int GetBlockAtLocalPosition(Vector2Int position)
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
