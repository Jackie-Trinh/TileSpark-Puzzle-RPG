using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// GameManager is the central singleton that persists across all scenes.
/// It holds references to the player's current progress, currency, and 
/// coordinates transitions between the Map scene and the Battle scene.
///
/// DontDestroyOnLoad keeps it alive when loading new scenes.
/// </summary>
public class GameManager : MonoBehaviour
{
    // ── Singleton ──────────────────────────────────────────────────────────
    public static GameManager Instance { get; private set; }

    // ── Scene names (match exactly what you name them in Build Settings) ──
    public const string SCENE_MAP = "MapScene";
    public const string SCENE_BATTLE = "BattleScene";

    // ── Player persistent data ─────────────────────────────────────────────
    [Header("Player Save Data (loaded from SaveManager on start)")]
    public PlayerData PlayerData;          // HP, stats, stage, currency …

    // ── Current battle context (set before loading BattleScene) ───────────
    [HideInInspector] public int CurrentStage = 1;   // which stage we're fighting

    // ──────────────────────────────────────────────────────────────────────
    private void Awake()
    {
        // Classic singleton guard
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject); // survive scene loads

        LoadGame();
    }

    // ── Public API ─────────────────────────────────────────────────────────

    /// <summary>Starts the battle for the given stage number.</summary>
    public void StartBattle(int stageNumber)
    {
        // Secondary safety guard: ensure the player always enters a battle
        // at full HP. The primary restore happens in ReturnToMap() and
        // EndBattle(won), but this catches any other code path that calls
        // StartBattle without restoring HP first.
        if (PlayerData != null && PlayerData.CurrentHP <= 0)
            PlayerData.CurrentHP = PlayerData.MaxHP;

        CurrentStage = stageNumber;
        SceneManager.LoadScene(SCENE_BATTLE);
    }

    /// <summary>Returns to the main map scene.</summary>
    public void ReturnToMap()
    {
        // Always restore the player to full HP before saving and returning
        // to the map. Without this, leaving after a defeat saves the
        // player's 0 (or near-zero) HP to disk. When they return to the
        // map and tap a stage again, the battle starts with 0 HP and
        // ends immediately before a single block can be placed.
        //
        // Restoring HP here (rather than in StartBattle) keeps the logic
        // in one place regardless of which path the player takes back to
        // the map (Map button on result screen, pause → Leave, etc.).
        if (PlayerData != null)
            PlayerData.CurrentHP = PlayerData.MaxHP;

        SaveGame();
        SceneManager.LoadScene(SCENE_MAP);
    }

    /// <summary>Adds gold to the player wallet and saves.</summary>
    public void AddGold(int amount)
    {
        PlayerData.Gold += amount;
        SaveGame();
    }

    /// <summary>Adds premium diamond currency and saves.</summary>
    public void AddDiamonds(int amount)
    {
        PlayerData.Diamonds += amount;
        SaveGame();
    }

    /// <summary>Spends gold. Returns false if not enough.</summary>
    public bool SpendGold(int amount)
    {
        if (PlayerData.Gold < amount) return false;
        PlayerData.Gold -= amount;
        SaveGame();
        return true;
    }

    /// <summary>Spends diamonds. Returns false if not enough.</summary>
    public bool SpendDiamonds(int amount)
    {
        if (PlayerData.Diamonds < amount) return false;
        PlayerData.Diamonds -= amount;
        SaveGame();
        return true;
    }

    // ── Save / Load ────────────────────────────────────────────────────────

    public void SaveGame() => SaveManager.Save(PlayerData);
    public void LoadGame() => PlayerData = SaveManager.Load();
}