using System.Collections.Generic;
using TMPro;
using Unity.Collections;
using UnityEngine;

public class StatManager : MonoBehaviour
{
    [Header("Ui Element :")]
    [SerializeField] TMP_Text statText;

    [Header("Objects :")]
    [SerializeField] WorldManager worldManager;
    [SerializeField] PlayerScript playerManager;
    [SerializeField] WorldService worldService;
    TileConfig tileConfig;
    WorldMetaData WorldMetaData;

    [SerializeField] Color red;
    [SerializeField] Color green;
    [SerializeField] Color blue;
    [SerializeField] Color yellow;
    [SerializeField] Color cyan;
    [SerializeField] Color magenta;
    [SerializeField] Color white;
    [SerializeField] Color black;
    Color rainbow;
    [SerializeField] float speedOfRainbow;
    struct colorSTR
    {
        string name;
        Color color;
    }

    private void Awake()
    {
        tileConfig = TileConfig.instance;
    }

    private void LateUpdate()
    {
        if (WorldMetaData == null)
        {
            WorldMetaData = worldService.GetWorldMetaData(worldManager.WorldId);
        }
        Vector2Int BlockCusor = playerManager.BlockCursor, chunkCursor = playerManager.ChunkCursor, blockCursorRelative = playerManager.BlockRelativeToChunk;

        float hue = Mathf.Repeat(Time.time * speedOfRainbow, 1f);
        rainbow = Color.HSVToRGB(hue, 1f, 1f);

        statText.text =
            addColour(yellow, "World Name: ") + WorldMetaData.worldName + "\n" +
            addColour(yellow, "Seed: ") + WorldMetaData.seed + "\n" +
            addColour(red, "Player Position: ") + playerManager.transform.position + "\n" +
            addColour(red, "Chunck At: ") + worldManager.CurrentPlayerChunk + "\n" +
            addColour(blue, "Cursor At : ") + BlockCusor + "\n" +
            addColour(blue, "Block: ") + tileConfig.Tiles[worldManager.getBlockOfChunk(chunkCursor, blockCursorRelative, true)].name + "\n" +
            addColour(blue, "Block In Hand: ") + tileConfig.Tiles[playerManager.SelectedBlockIndex].name + "\n" + 
            addColour(rainbow, "   Astral Pix V0.1")
            ;
    }
    string addColour(Color color, string text)
    {
        return "<color=#" + ColorUtility.ToHtmlStringRGBA(color) + ">" + text + "</color>";
    }
}
