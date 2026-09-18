using UnityEngine;

/// <summary>
/// EnemyAbilityType is an enum listing every special action an enemy can
/// perform in addition to their normal attack.
///
/// Each value maps directly to a handler method in BattleManager so adding
/// a new ability only requires:
///   1. Adding the enum value here.
///   2. Adding a case in BattleManager.ExecuteEnemyAbility().
///   3. Creating a ScriptableObject asset for it.
/// </summary>
public enum EnemyAbilityType
{
    /// <summary>
    /// Places a single 1×1 block on a random EMPTY cell in the grid.
    /// Makes the grid harder to clear by cluttering it with junk blocks.
    /// </summary>
    PlaceJunkBlock = 0,

    /// <summary>
    /// Replaces one of the player's three tray blocks with a randomly
    /// chosen different block.  Forces the player to work with a shape
    /// they did not plan around.
    /// </summary>
    TransformTrayBlock = 1,

    /// <summary>
    /// Applies a poison debuff to the player that lasts for a set number
    /// of player moves.  Each time the player places a block, they lose
    /// a percentage of their max HP as damage-over-time.
    /// </summary>
    PoisonPlayer = 2,

    /// <summary>
    /// Removes a random PLACED block from the grid, leaving an empty cell.
    /// Disrupts combos the player was building.
    /// </summary>
    RemovePlacedBlock = 3,

    /// <summary>
    /// Reduces one or more of the player's combat stats by a percentage
    /// for a set duration (measured in player moves).
    /// </summary>
    DebuffPlayerStat = 4,

    /// <summary>
    /// Blinds the player: their blocks still clear lines but deal ZERO
    /// damage to the enemy for a set number of moves.
    /// </summary>
    BlindPlayer = 5,
}

// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// EnemyAbility is a ScriptableObject asset that configures ONE specific
/// ability an enemy can use.  Attach one or more of these to an
/// EnemyDefinition to make that enemy use special moves.
///
/// HOW TO CREATE:
///   Right-click in Project → Create → TileSpark → Enemy Ability
///
/// EXAMPLE SETUPS:
///   • "Grid Clogger"  → Type=PlaceJunkBlock,  JunkBlockCount=2
///   • "Shape Shifter" → Type=TransformTrayBlock
///   • "Venom Snake"   → Type=PoisonPlayer,    PoisonDuration=3, PoisonDamagePercent=0.05
///   • "Block Eraser"  → Type=RemovePlacedBlock, BlocksToRemove=1
///   • "Cursed Witch"  → Type=DebuffPlayerStat, DebuffedStats flags, DebuffAmount=0.25, DebuffDuration=4
///   • "Blinder"       → Type=BlindPlayer,      BlindDuration=3
/// </summary>
[CreateAssetMenu(fileName = "Ability_New", menuName = "TileSpark/Enemy Ability")]
public class EnemyAbility : ScriptableObject
{
    // ── Identity ──────────────────────────────────────────────────────────
    [Header("Identity")]
    public string AbilityName = "New Ability";

    [Tooltip("Short description shown in the enemy tooltip / battle log.")]
    [TextArea(2, 4)]
    public string Description;

    // ── Type ──────────────────────────────────────────────────────────────
    [Header("Ability Type")]
    public EnemyAbilityType AbilityType;

    // ── Cooldown ──────────────────────────────────────────────────────────
    [Header("Timing")]
    [Tooltip("How many times the enemy must attack before they can use this " +
             "ability again.  0 = use every attack turn.  " +
             "1 = use every other attack turn.")]
    public int CooldownAttacks = 1;

    // ── PlaceJunkBlock parameters ─────────────────────────────────────────
    [Header("PlaceJunkBlock Settings")]
    [Tooltip("How many 1×1 junk blocks to place on the grid per use.")]
    public int JunkBlockCount = 1;

    // ── PoisonPlayer parameters ───────────────────────────────────────────
    [Header("PoisonPlayer Settings")]
    [Tooltip("Number of player moves poison lasts.")]
    public int   PoisonDurationMoves = 3;

    [Tooltip("Damage per tick as a fraction of the player's MAX HP.  " +
             "0.05 = 5% max HP per move while poisoned.")]
    [Range(0f, 0.5f)]
    public float PoisonDamagePercent = 0.05f;

    // ── RemovePlacedBlock parameters ──────────────────────────────────────
    [Header("RemovePlacedBlock Settings")]
    [Tooltip("How many random placed blocks to erase per use.")]
    public int BlocksToRemove = 1;

    // ── DebuffPlayerStat parameters ───────────────────────────────────────
    [Header("DebuffPlayerStat Settings")]
    [Tooltip("Which stats this debuff affects.  Tick multiple boxes to " +
             "debuff several stats at once.")]
    public DebuffTargetFlags DebuffedStats = DebuffTargetFlags.Damage;

    [Tooltip("Fraction of the stat to remove.  0.25 = −25% of current value.")]
    [Range(0f, 0.9f)]
    public float DebuffAmount = 0.25f;

    [Tooltip("How many player moves the debuff lasts before it expires.")]
    public int DebuffDurationMoves = 4;

    // ── BlindPlayer parameters ────────────────────────────────────────────
    [Header("BlindPlayer Settings")]
    [Tooltip("Number of player moves the blind effect lasts.")]
    public int BlindDurationMoves = 3;
}

// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Bit-flag enum so a single DebuffPlayerStat ability can debuff multiple
/// stats at the same time.  In the Inspector you can tick each box you want.
///
/// [System.Flags] lets you write: DebuffTargetFlags.Damage | DebuffTargetFlags.Speed
/// </summary>
[System.Flags]
public enum DebuffTargetFlags
{
    None    = 0,
    Damage  = 1 << 0,   // reduces BaseDamage
    Speed   = 1 << 1,   // reduces Speed (fewer moves before enemy attacks)
    Defense = 1 << 2,   // reduces Defense
    Luck    = 1 << 3,   // reduces Luck (crit chance)
    MaxHP   = 1 << 4,   // reduces MaxHP (and CurrentHP by same amount if above new max)
}
