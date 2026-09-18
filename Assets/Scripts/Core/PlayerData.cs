using System;
using System.Collections.Generic;

/// <summary>
/// PlayerData holds every piece of information we need to save about the player.
///
/// [Serializable] lets Unity's JsonUtility convert it to/from JSON
/// so we can write it to disk via SaveManager.
/// </summary>
[Serializable]
public class PlayerData
{
    // ── Identity ───────────────────────────────────────────────────────────
    public string PlayerName = "Hero";
    public int CurrentStage = 1;        // highest stage reached

    // ── Currency ───────────────────────────────────────────────────────────
    public int Gold = 0;
    public int Diamonds = 0;

    // ── Base stats (upgraded on the Character page with gold) ─────────────
    public int MaxHP = 100;
    public int CurrentHP = 100;
    public float BaseDamage = 10f;   // added to block-clear damage
    public int Speed = 5;     // moves allowed before enemy attacks
    public float Defense = 0f;    // flat damage reduction
    public float Luck = 0.05f; // crit chance 0..1

    // ── Upgrade levels (each costs gold to raise) ─────────────────────────
    public int HPLevel = 1;
    public int DamageLevel = 1;
    public int SpeedLevel = 1;
    public int DefenseLevel = 1;
    public int LuckLevel = 1;

    // ── Equipped items (slot name → item ID, empty string = nothing) ──────
    public string HelmetId = "";
    public string ArmorId = "";
    public string ShoesId = "";
    public string WeaponId = "";
    public string AmuletId = "";
    public string RingId = "";

    // ── Ability cards owned (card ID → star level 1..5) ───────────────────
    public List<OwnedCard> OwnedCards = new();

    // ── Equipment owned (item IDs the player has collected) ───────────────
    // The equipped IDs (HelmetId, ArmorId etc.) reference items from this list.
    public List<string> OwnedEquipmentIds = new();

    // ── Active ability assignment per block color/type ────────────────────
    // Key = BlockType enum value as int, Value = card ID string
    public List<BlockAbilityBinding> BlockBindings = new();

    // ── Ultimate ability ID (unlocked by max-combo clear) ─────────────────
    public string UltimateAbilityId = "";

    // ── Powers inventory (bomb count, omni count, potion count) ──────────
    public int BombCount = 3;
    public int OmniCount = 2;
    public int PotionCount = 2;

    // ── Daily dungeon tracking ─────────────────────────────────────────────
    public string LastDailyReset = "";  // date string for daily reset check
    public int GoldDungeonRunsToday = 0;
    public int EquipDungeonRunsToday = 0;
    public int CardDungeonRunsToday = 0;
}

/// <summary>Represents one card the player owns and its star level.</summary>
[Serializable]
public class OwnedCard
{
    public string CardId;
    public int Stars = 1;    // 1..5
    public int Copies = 1;   // how many copies they have
}

/// <summary>Maps a block type to whichever ability card is currently active on it.</summary>
[Serializable]
public class BlockAbilityBinding
{
    public int BlockTypeIndex; // matches BlockType enum int value
    public string ActiveCardId;
}
