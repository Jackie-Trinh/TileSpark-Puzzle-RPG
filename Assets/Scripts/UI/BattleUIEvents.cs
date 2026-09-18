using System;
using UnityEngine;

/// <summary>
/// BattleUIEvents is a lightweight event bus that lets BattleManager
/// communicate with UI panels without direct references.
///
/// Pattern: BattleManager fires events → UI panels subscribe and react.
/// This keeps the battle logic cleanly separated from UI code.
///
/// BattleManager can push plain-text feedback messages (ability used, debuff applied, etc.)
/// to any UI panel that wants to display them — without needing a
/// direct reference to that panel.
/// </summary>
public class BattleUIEvents : MonoBehaviour
{
    public static BattleUIEvents Instance { get; private set; }

    // ── Events ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Fired when the player achieves a max-combo (row + column cleared at once).
    /// The UI should prompt the player to change their ultimate ability.
    /// </summary>
    public event Action OnMaxCombo;

    /// <summary>Fired when the battle ends. UI shows the result screen.</summary>
    public event Action<bool, int> OnBattleResult;   // (playerWon, stageNumber)

    /// <summary>
    /// Fired whenever BattleManager wants to show a short feedback message.
    /// Examples: "You are poisoned!", "Enemy placed a junk block!", "Blind expired."
    ///
    /// Subscribe in any UI component that shows a battle log or popup text.
    /// The string payload is the message to display.
    /// </summary>
    public event Action<string> OnStatusMessage;

    // ──────────────────────────────────────────────────────────────────────
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public void NotifyMaxCombo()
        => OnMaxCombo?.Invoke();

    public void ShowResultScreen(bool won, int stage)
        => OnBattleResult?.Invoke(won, stage);

    /// <summary>
    /// Pushes a status message to all subscribed UI components.
    /// Call this from BattleManager any time something noteworthy happens
    /// (debuff applied, ability used, etc.) to keep the player informed.
    /// </summary>
    public void NotifyStatusMessage(string message)
        => OnStatusMessage?.Invoke(message);
}
