using System;
using UnityEngine;

/// <summary>
/// DailyDungeonManager handles the Event Dungeon system.
///
/// Three dungeon types exist:
///   • Gold Dungeon   — drops gold
///   • Equip Dungeon  — drops equipment
///   • Card Dungeon   — drops ability cards
///
/// Each has a max daily run count that resets at midnight.
/// The last reset date is stored in PlayerData as a date string.
/// </summary>
public class DailyDungeonManager : MonoBehaviour
{
    [Header("Daily Run Limits")]
    public int MaxGoldRuns  = 5;
    public int MaxEquipRuns = 3;
    public int MaxCardRuns  = 3;

    // ──────────────────────────────────────────────────────────────────────
    private void Start()
    {
        CheckDailyReset();
    }

    // ── Public API ─────────────────────────────────────────────────────────

    public bool CanRunGoldDungeon()  => GetPD().GoldDungeonRunsToday  < MaxGoldRuns;
    public bool CanRunEquipDungeon() => GetPD().EquipDungeonRunsToday < MaxEquipRuns;
    public bool CanRunCardDungeon()  => GetPD().CardDungeonRunsToday  < MaxCardRuns;

    public int RemainingGoldRuns()   => MaxGoldRuns  - GetPD().GoldDungeonRunsToday;
    public int RemainingEquipRuns()  => MaxEquipRuns - GetPD().EquipDungeonRunsToday;
    public int RemainingCardRuns()   => MaxCardRuns  - GetPD().CardDungeonRunsToday;

    /// <summary>
    /// Starts a Gold Dungeon run if the daily limit hasn't been reached.
    /// Returns false if no runs remain.
    /// </summary>
    public bool StartGoldDungeon()
    {
        if (!CanRunGoldDungeon()) return false;
        GetPD().GoldDungeonRunsToday++;
        GameManager.Instance.SaveGame();
        // Launch as a special battle stage — extend BattleManager with dungeon mode
        GameManager.Instance.StartBattle(-1); // negative = dungeon signal
        return true;
    }

    public bool StartEquipDungeon()
    {
        if (!CanRunEquipDungeon()) return false;
        GetPD().EquipDungeonRunsToday++;
        GameManager.Instance.SaveGame();
        GameManager.Instance.StartBattle(-2);
        return true;
    }

    public bool StartCardDungeon()
    {
        if (!CanRunCardDungeon()) return false;
        GetPD().CardDungeonRunsToday++;
        GameManager.Instance.SaveGame();
        GameManager.Instance.StartBattle(-3);
        return true;
    }

    // ── Daily reset ────────────────────────────────────────────────────────

    /// <summary>
    /// Checks if the last saved reset date differs from today.
    /// If yes, resets all daily counters.
    /// Called at game start and whenever the game is foregrounded.
    /// </summary>
    public void CheckDailyReset()
    {
        var pd       = GetPD();
        string today = DateTime.UtcNow.ToString("yyyy-MM-dd");

        if (pd.LastDailyReset == today) return; // already reset today

        pd.GoldDungeonRunsToday  = 0;
        pd.EquipDungeonRunsToday = 0;
        pd.CardDungeonRunsToday  = 0;
        pd.LastDailyReset        = today;

        GameManager.Instance.SaveGame();
        Debug.Log("[DailyDungeon] Daily counters reset.");
    }

    private PlayerData GetPD() => GameManager.Instance.PlayerData;
}
