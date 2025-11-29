using NUnit.Framework;
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static System.Net.Mime.MediaTypeNames;


[Serializable]
public enum TileType
{
    Common,
    Water,
    Pipe
}

[Serializable]
public struct LightData
{
    public Color color;
    public float intensity;
    public float outerRadius;
    public float innerRadius;
}

[Serializable]
public struct tile
{
    public string name;
    public string description;
    public bool isSolid;
    public TileType tileType;
    public int variations;
    public int hardness;
    public float zOffset; //needs to be primary key but keep on float so it can be asigned without changing other ones

    public bool isLightEmitter;
    public LightData lightData;
}

[CreateAssetMenu(fileName = "TileConfig", menuName = "Scriptable Objects/TileConfig")]
public class TileConfig : ScriptableSingleton<TileConfig>
{
    [Header("Texture Atlas")]
    [SerializeField]Texture2D commonTileTexture;

    [SerializeField] public float zOffset = 0.1f;

    // padding for Texture Bleeding
    [SerializeField] float offset = 0.0001f;

    // Diccionario para acceder a las UVs por nombre (ej: "cornerUL", "base")

    //sistema de clave tipo key = type + name + variation 
    //Variation starts on 0, type 0 is air, and name is the name
    [System.NonSerialized] Dictionary<string, Vector2[]> uvs = new();

    

    [SerializeField] tile[] tiles = new tile[0];
    public tile[] Tiles => tiles;

    private void OnEnable()
    {
        if (commonTileTexture != null)
        {
            GenerateUvs();
        }
    }
    public void GenerateUvs()
    {
        if (commonTileTexture == null)
        {
            Debug.LogError("TileConfig: No hay textura asignada.");
            return;
        }

        uvs.Clear();

        // 1. Calculamos cuánto mide un píxel en espacio UV (0 a 1)
        int commonTexW = commonTileTexture.width;
        int commonTexH = commonTileTexture.height;

        for(int i = 0; i < tiles.Length; i++)
        {
            for(int j = 0; j < tiles[i].variations; j++)
            {
                // NOTA IMPORTANTE SOBRE COORDENADAS:
                switch(tiles[i].tileType)
                {
                    case TileType.Common:
                        GenerateCommonUvs(i, j, commonTexW, commonTexH);
                        break;
                    case TileType.Water:
                        // Implementar generación de UVs para agua si es necesario
                        break;
                    case TileType.Pipe:
                        // Implementar generación de UVs para tuberías si es necesario
                        break;
                }

                Debug.Log($"TileConfig: UVs generadas. Total piezas: {uvs.Count}");
            }
        }
    }

    void GenerateCommonUvs(int i, int j, int texW, int texH)
    {
        int offsetY = texH - (i * 24);
        int offsetX = j * 16;
        string key = tiles[i].name + j.ToString();

        // --- BLOQUES PRINCIPALES (8x8) ---
        AddPiece("base" + key, offsetX + 4, offsetY + 4, 8, 8, texW, texH);
        AddPiece("chunk" + key, offsetX + 0, offsetY + 0, 16, 16, texW, texH); // usado para items o iconos

        // --- BORDES (4x4) --- 
        AddPiece("leftTopEdge" + key, offsetX + 0, offsetY + 8, 4, 4, texW, texH);
        AddPiece("leftBottomEdge" + key, offsetX + 0, offsetY + 4, 4, 4, texW, texH);
        AddPiece("rightTopEdge" + key, offsetX + 12, offsetY + 8, 4, 4, texW, texH);
        AddPiece("rightBottomEdge" + key, offsetX + 12, offsetY + 4, 4, 4, texW, texH);

        AddPiece("topLeftEdge" + key, offsetX + 4, offsetY + 12, 4, 4, texW, texH);
        AddPiece("topRightEdge" + key, offsetX + 8, offsetY + 12, 4, 4, texW, texH);
        AddPiece("bottomLeftEdge" + key, offsetX + 4, offsetY + 0, 4, 4, texW, texH);
        AddPiece("bottomRightEdge" + key, offsetX + 8, offsetY + 0, 4, 4, texW, texH);

        // --- ESQUINAS INTERIORES (4x4) ---
        AddPiece("innerTL" + key, offsetX + 0, offsetY + 20, 4, 4, texW, texH);
        AddPiece("innerTR" + key, offsetX + 4, offsetY + 20, 4, 4, texW, texH);
        AddPiece("innerBL" + key, offsetX + 0, offsetY + 16, 4, 4, texW, texH);
        AddPiece("innerBR" + key, offsetX + 4, offsetY + 16, 4, 4, texW, texH);

        // Bordes Largos 
        AddPiece("topEdge" + key, offsetX + 4, offsetY + 12, 8, 4, texW, texH);
        AddPiece("bottomEdge" + key, offsetX + 4, offsetY + 0, 8, 4, texW, texH);
        AddPiece("rightEdge" + key, offsetX + 12, offsetY + 4, 4, 8, texW, texH);
        AddPiece("leftEdge" + key, offsetX + 0, offsetY + 4, 4, 8, texW, texH);
    }


    /// <summary>
    /// Calcula las UVs normalizadas (0-1) para un rectángulo de píxeles específico.
    /// </summary>
    /// <param name="name">Nombre de la pieza (ej: "cornerUL")</param>
    /// <param name="x">Posición X en píxeles en la textura</param>
    /// <param name="y">Posición Y en píxeles en la textura</param>
    /// <param name="w">Ancho de la pieza en píxeles</param>
    /// <param name="h">Alto de la pieza en píxeles</param>
    /// <param name="texW">Ancho total de la textura</param>
    /// <param name="texH">Alto total de la textura</param>
    void AddPiece(string name, int x, int y, int w, int h, float texW, float texH)
    {
        // pixelSize in UVs
        float pX = 1.0f / texW;
        float pY = 1.0f / texH;

        // offset for texture bleeding
        float uMin = (x * pX) + offset;
        float vMin = (y * pY) + offset;
        float uMax = ((x + w) * pX) - offset;
        float vMax = ((y + h) * pY) - offset;

        Vector2[] tempUvs = new Vector2[4];

        tempUvs[0] = new Vector2(uMin, vMin); // BL
        tempUvs[1] = new Vector2(uMin, vMax); // TL
        tempUvs[2] = new Vector2(uMax, vMax); // TR
        tempUvs[3] = new Vector2(uMax, vMin); // BR

        if (uvs.ContainsKey(name))
        {
            uvs[name] = tempUvs;
        }
        else
        {
            uvs.Add(name, tempUvs);
        }
    }

    public Texture2D GetSpriteTextureOfBlock(int tileId)
    {
        switch(tiles[tileId].tileType)
        {
            case TileType.Common:
                return CommonSpriteTexture(commonTileTexture, GetUVs(tileId, "", 0));
                //case TileType.Water:
                //    return WaterSpriteTexture();
                //case TileType.Pipe:
                //    return PipeSpriteTexture();
            default:
                Debug.LogError("TileConfig: Tipo de tile no soportado para extraer textura.");
                return null;
        }
    }

    static Texture2D CommonSpriteTexture(Texture2D source, Vector2[] uvs)
    {
        int texW = source.width;
        int texH = source.height;


        int startX = Mathf.FloorToInt(uvs[0].x * texW);
        int startY = Mathf.FloorToInt(uvs[0].y * texH);

        int width = Mathf.RoundToInt((uvs[2].x - uvs[0].x) * texW);
        int height = Mathf.RoundToInt((uvs[2].y - uvs[0].y) * texH);

        if (width <= 0 || height <= 0)
        {
            Debug.LogError("UVs inválidos o ancho/alto es 0");
            return null;
        }
        Color[] pixels = source.GetPixels(startX, startY, width, height);

        // Crear textura nueva
        Texture2D result = new Texture2D(width, height);
        result.filterMode = FilterMode.Point;
        result.SetPixels(pixels);
        result.Apply();

        return result;
    }

    // Método para obtener UVs de forma segura desde otros scripts
    public Vector2[] GetUVs(int id, string name, int variation)
    {
        if (uvs.Count == 0) GenerateUvs();

        string key = name + tiles[id].name + variation.ToString();

        if (uvs.TryGetValue(key, out Vector2[] result))
        {
            return result;
        }
        Debug.LogWarning($"Pieza '{key}' no encontrada en TileConfig.");
        return new Vector2[4]; // Retorna vacío para no crashear
    }

}
