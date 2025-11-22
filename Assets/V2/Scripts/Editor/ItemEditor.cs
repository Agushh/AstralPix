using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ItemData))]
public class ItemEditor : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        ItemData item = (ItemData)target;

        if (item.itemSprite != null)
        {
            Texture2D tex = GetCroppedTextureFromSprite(item.itemSprite);
            EditorGUIUtility.SetIconForObject(item, tex);
        }
    }
    public static Texture2D GetCroppedTextureFromSprite(Sprite sprite)
    {
        if (sprite == null) return null;

        Texture2D source = sprite.texture;

        Rect rect = sprite.textureRect;
        int x = Mathf.FloorToInt(rect.x);
        int y = Mathf.FloorToInt(rect.y);
        int w = Mathf.FloorToInt(rect.width);
        int h = Mathf.FloorToInt(rect.height);

        // Crear textura recortada
        Texture2D cropped = new Texture2D(w, h, TextureFormat.RGBA32, false);
        cropped.filterMode = FilterMode.Point;

        Color[] pixels = source.GetPixels(x, y, w, h);
        cropped.SetPixels(pixels);
        cropped.Apply();

        return cropped;
    }
}