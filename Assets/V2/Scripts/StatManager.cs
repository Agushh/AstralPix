using TMPro;
using UnityEngine;

public class StatManager : MonoBehaviour
{
    [Header("Ui Element :")]
    [SerializeField] TMP_Text statText;

    [Header("Objects :")]
    [SerializeField] WorldManager worldManager;
    [SerializeField] PlayerScript playerManager;
    [SerializeField] WorldService worldService;
    [SerializeField] Blockdictionary Blockdictionary;
    WorldMetaData WorldMetaData;
    private void LateUpdate()
    {
        if (WorldMetaData == null)
        {
            WorldMetaData = worldService.GetWorldMetaData(worldManager.WorldId);
        }
    }
    void Update()
    {
        Vector2Int BlockCusor = playerManager.BlockCursor, chunkCursor = playerManager.ChunkCursor, blockCursorRelative = playerManager.BlockRelativeToChunk;

        statText.text =
            addColour(Color.cyan, "World Name: ") + WorldMetaData.worldName + "\n" +
            addColour(Color.cyan, "World Id: ") + WorldMetaData.worldName + "\n" +
            addColour(Color.cyan, "Seed: ") + WorldMetaData.seed + "\n" +
            addColour(Color.cyan, "Player Position: ") + playerManager.transform.position + "\n" +
            addColour(Color.cyan, "Chunck At: ") + worldManager.CurrentPlayerChunk + "\n" +
            addColour(Color.cyan, "Cursor At : ") + BlockCusor + "\n" +
            addColour(Color.cyan, "Block: ") + Blockdictionary.tiles[worldManager.getBlockOfChunk(chunkCursor, blockCursorRelative)].name + "\n"


            ;

    }

    string addColour(Color color, string text)
    {
        return "<color=#" + ColorUtility.ToHtmlStringRGBA(color) + ">" + text + "</color>";
    }
}
