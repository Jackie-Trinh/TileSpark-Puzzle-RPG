using System.IO;
using UnityEngine;

/// <summary>
/// SaveManager handles reading and writing PlayerData to disk as JSON.
///
/// We use Application.persistentDataPath so the file is stored in the
/// correct OS-specific location on every platform (Android, iOS, PC, etc.).
///
/// This is a static utility class.
/// </summary>
public static class SaveManager
{
    // File will be at: Application.persistentDataPath/TileSpark_save.json
    private static string SavePath => Path.Combine(Application.persistentDataPath, "TileSpark_save.json");

    /// <summary>
    /// Converts PlayerData to JSON and writes it to disk.
    /// Called automatically after any data change in GameManager.
    /// </summary>
    public static void Save(PlayerData data)
    {
        string json = JsonUtility.ToJson(data, prettyPrint: true);
        File.WriteAllText(SavePath, json);
        Debug.Log($"[SaveManager] Game saved to {SavePath}");
    }

    /// <summary>
    /// Reads JSON from disk and returns a PlayerData object.
    /// If no save file exists, returns fresh default data.
    /// </summary>
    public static PlayerData Load()
    {
        if (!File.Exists(SavePath))
        {
            Debug.Log("[SaveManager] No save found — creating new player data.");
            return new PlayerData();   // first launch defaults
        }

        string json = File.ReadAllText(SavePath);
        PlayerData data = JsonUtility.FromJson<PlayerData>(json);
        Debug.Log("[SaveManager] Game loaded successfully.");
        return data;
    }

    /// <summary>
    /// Deletes the save file. Used by the "Reset" option in settings.
    /// </summary>
    public static void DeleteSave()
    {
        if (File.Exists(SavePath))
            File.Delete(SavePath);
        Debug.Log("[SaveManager] Save deleted.");
    }
}
