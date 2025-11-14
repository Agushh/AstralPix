using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public struct tileData
{
    public string name;
    public int id; // Order key. Represents the height on the atlas
    public float hardness; // 0 instant break, -1 unbreakable, 1,2,3,4 = hardness level.
    public bool EmitLight; 
    public Color lightColor;
    public string flag; //for future uses like water or platforms.
}


/// <summary>
/// Scriptable Object de unica instancia utilizado para almacenar la informacion de los bloques y traducir direcciones entre el atlas y el bloque y su relacion.
/// </summary>
[CreateAssetMenu(fileName = "Blockdictionary", menuName = "Scriptable Objects/Blockdictionary")]
public class Blockdictionary : ScriptableObject
{

    public Texture2D atlas;
    public int tilesPerRow = 64; // Alto del atlas
    const int tilesPerColumn = 128; //Ancho del atlas
    public tileData[] tiles;
    private Vector2[,] uvCache; // [blockID, relation] = UVs[4]


    //TileRelations
    static string[] codes = new string[]
    {
        "x0x111x0",
        "x0x11111",
        "x0x0x111",
        "x0x0x1x0",
        "11110111",
        "11111101",
        "x11111x0",
        "11111111",
        "11x0x111",
        "x1x0x1x0",
        "11011111",
        "01111111",
        "x111x0x0",
        "1111x0x1",
        "11x0x0x1",
        "x1x0x0x0",
        "x0x101x0",
        "x0x0x1x1",
        "x0x1x0x0",
        "x0x1x0x1",
        "x0x0x0x1",
        "x0x0x0x0",
        "x101x0x0",
        "01x0x0x1",
        "x10101x0",
        "x0x1x1x1",
        "11110101",
        "01111101",
        "01011101",
        "01010111",
        "0101x0x1",
        "01x0x101",
        "11010111",
        "01011111",
        "01110101",
        "11010101",
        "x11101x0",
        "x0x11101",
        "x0x10111",
        "11x0x101",
        "11011101",
        "01110111",
        "1101x0x1",
        "01x0x111",
        "x10111x0",
        "0111x0x1"
    };
    
    int totalRelations = 46;

    struct Pattern
    {
        public byte mask;
        public byte value;
        public int returnValue;
    }
    static List<Pattern> patterns = new List<Pattern>();

    #region Start Functions
    /// <summary>
    /// Metodo que se encarga de leer las relaciones de tipo "x10x10x1" el cual representa los bloques vecinos, y genera un desplazamiento en 
    /// el atlas acorde al tile correspondiente para la relacion con vecinos
    /// Futura mejora : Guardarlo como asset y que no se tenga que generar en RunTime.
    /// </summary>
    static void GeneratePatterns()
    {
        patterns.Clear(); // IMPORTANTE: limpiar
        for (int i = 0; i < codes.Length; i++)
        {
            string code = codes[i];
            byte mask = 0;
            byte value = 0;

            //se comprueban los 8 vecinos adyacentes. Se comparan con mask y se genera el valor
            for (int b = 0; b < 8; b++)
            {
                char c = code[b]; // bit b usa el carácter b
                if (c != 'x' && c != 'X')
                {
                    mask |= (byte)(1 << b);
                    if (c == '1') value |= (byte)(1 << b);
                }
            }

            patterns.Add(new Pattern { mask = mask, value = value, returnValue = i });
        }
    }

    public static int CheckPattern(byte relation)
    {
        foreach (var p in patterns)
        {
            if ((relation & p.mask) == p.value)
                return p.returnValue;
        }
        return -1; // Ningún patrón coincide
    }

    private void OnEnable()
    {
        GeneratePatterns();
        GenerateUVs();
    }
    private void GenerateUVs()
    {
        uvCache = new Vector2[tiles.Length, totalRelations * 4]; // cada tile tiene 4 UVs por relación

        float tileWidth = 1f / tilesPerRow;
        float tileHeight = 1f / tilesPerColumn;

        for (int blockID = 0; blockID < tiles.Length; blockID++)
        {
            int y = tilesPerColumn - tiles[blockID].id; // altura en filas del atlas (invertido para mantener orden de que el id 0 sea el mas alto, es decir orden de lectura vertical inverso)
            for (int rel = 0; rel < totalRelations; rel++)
            {
                int x = rel; // posición horizontal en el atlas

                float uMin = x * tileWidth + 0.0001f;
                float vMin = y * tileHeight + 0.0001f;
                float uMax = uMin + tileWidth - 0.0001f;
                float vMax = vMin + tileHeight - 0.0001f;

                // bottom-left, top-left, top-right, bottom-right
                uvCache[blockID, rel * 4 + 0] = new Vector2(uMin, vMin);
                uvCache[blockID, rel * 4 + 1] = new Vector2(uMin, vMax);
                uvCache[blockID, rel * 4 + 2] = new Vector2(uMax, vMax);
                uvCache[blockID, rel * 4 + 3] = new Vector2(uMax, vMin);
            }
        }
    }

    #endregion

    #region Runtime Functions
    
    
    /// <summary>
    /// Busqueda de UVs en el atlas segun el bloque y su relacion con los vecinos
    /// </summary>
    /// <param name="blockID"> Id del bloque(altura en el atlas)</param>
    /// <param name="relation">byte con flags de relacion con vecinos</param>
    /// <returns></returns>
    public Vector2[] GetTileUVs(int blockID, byte relation)
    {
        if (blockID < 0 || blockID >= tiles.Length)
        {
            return DefaultUV(blockID);
        }

        int pattern = CheckPattern(relation);
        if (pattern == -1)
        {
            return DefaultUV(blockID);
        }

        var uvs = new Vector2[4];
        for (int i = 0; i < 4; i++)
            uvs[i] = uvCache[blockID, pattern * 4 + i];
        return uvs;
    }
    
    
    Vector2[] DefaultUV(int blockID)
    {
        // Usa pattern 0 como default, o una columna fija
        var uvs = new Vector2[4];
        for (int i = 0; i < 4; i++)
            uvs[i] = uvCache[blockID, 0 * 4 + i];
        return uvs;
    }

    #endregion
}
