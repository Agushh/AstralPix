using UnityEditor;
using UnityEngine;
using static UnityEditor.Progress;
using static UnityEngine.GraphicsBuffer;

[CreateAssetMenu(fileName = "ItemData", menuName = "Scriptable Objects/ItemData")]
public class ItemData : ScriptableObject
{
    public int itemID;
    public string itemName;
    public Sprite itemSprite;
    public ItemType itemType;
    public int damage;
    public int defense;
    public float angleOffset;
    public float itemAngle;
    public bool TwoHanded;
}
public enum ItemType
{
    Weapon,
    Armor,
    Consumable,
    Placeable
}