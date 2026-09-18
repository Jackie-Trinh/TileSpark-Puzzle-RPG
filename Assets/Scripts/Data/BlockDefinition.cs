using UnityEngine;

/// <summary>
/// BlockType identifies each unique block shape/color combination.
/// Each value maps to a specific BlockDefinition ScriptableObject.
/// </summary>
public enum BlockType
{
    Red    = 0,   // e.g. L-shape
    Blue   = 1,   // e.g. T-shape
    Green  = 2,   // e.g. S-shape
    Yellow = 3,   // e.g. square
    Purple = 4,   // e.g. I-shape
    Orange = 5,   // e.g. Z-shape
    Mold   = 6,   // special 3x3 fill block
}

/// <summary>
/// BlockDefinition is a ScriptableObject asset you create in the Unity Editor.
/// Each BlockType gets its own asset (e.g. "Block_Red.asset").
///
/// ScriptableObjects are great for game data: they live as project assets,
/// </summary>
[CreateAssetMenu(fileName = "Block_New", menuName = "TileSpark/Block Definition")]
public class BlockDefinition : ScriptableObject
{
    [Header("Identity")]
    public BlockType  BlockType;        // which type this asset represents
    public string     DisplayName;      // shown in UI

    [Header("Visual")]
    public Color      BlockColor = Color.white;
    public Sprite     BlockSprite;      // the sprite for each cell of this block

    [Header("Shape")]
    [Tooltip("The grid cells this block occupies relative to its pivot (0,0). " +
             "Each Vector2Int is (column, row) offset.")]
    public Vector2Int[] Shape;          // e.g. L-shape = (0,0),(0,1),(0,2),(1,0)

    [Header("Default Effect")]
    [Tooltip("What happens when a line containing this block is cleared.")]
    public BlockEffect DefaultEffect;   // see BlockEffect enum below

    [Header("Base Damage Multiplier")]
    [Tooltip("Multiplied against the player's BaseDamage when this block is in a clear.")]
    public float DamageMultiplier = 1f;
}

/// <summary>
/// BlockEffect defines what secondary effect fires when a line containing
/// this block type is cleared.  Players can override this via ability cards.
/// </summary>
public enum BlockEffect
{
    None        = 0,
    Burn        = 1,   // damage over time to target
    Freeze      = 2,   // slows enemy attack timer
    Poison      = 3,   // stacking damage per enemy move
    Shield      = 4,   // gives player a temporary shield
    Heal        = 5,   // heals player a small amount
    Stun        = 6,   // enemy skips next attack
    Amplify     = 7,   // next clear does double damage
    GoldBonus   = 8,   // drops bonus gold after battle
}
