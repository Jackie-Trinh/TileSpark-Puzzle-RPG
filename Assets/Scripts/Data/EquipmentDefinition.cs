using UnityEngine;

/// <summary>
/// EquipmentDefinition stores the stats and effects provided by one piece of gear.
///
/// HOW TO CREATE:
///   Right-click in Project → Create → TileSpark → Equipment Definition
/// </summary>
[CreateAssetMenu(fileName = "Equip_New", menuName = "TileSpark/Equipment Definition")]
public class EquipmentDefinition : ScriptableObject
{
    [Header("Identity")]
    public string         ItemId;
    public string         ItemName;
    public Sprite         ItemIcon;
    public EquipmentSlot  Slot;
    public CardRarity     Rarity;

    [Header("Stat Bonuses (added to player stats while equipped)")]
    public int   BonusHP      = 0;
    public float BonusDamage  = 0f;
    public int   BonusSpeed   = 0;
    public float BonusDefense = 0f;
    public float BonusLuck    = 0f;

    [Header("Special Effect")]
    [Tooltip("Optional passive effect this item grants in battle.")]
    public BlockEffect PassiveEffect = BlockEffect.None;

    [Header("Set ID (items sharing the same SetId give a set bonus)")]
    public string SetId = "";     // e.g. "warrior_set" — empty = no set

    [Header("Description")]
    [TextArea] public string Description;
}

/// <summary>Which equipment slot this item belongs to.</summary>
public enum EquipmentSlot
{
    Helmet  = 0,
    Armor   = 1,
    Shoes   = 2,
    Weapon  = 3,
    Amulet  = 4,
    Ring    = 5,
}
