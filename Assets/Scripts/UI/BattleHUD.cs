using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// BattleHUD manages the top status bar and the result screen overlay.
///
/// It subscribes to BattleUIEvents and updates text/buttons accordingly.
///
/// Assign all UI references in the Inspector.
/// </summary>
public class BattleHUD : MonoBehaviour
{
    [Header("Top Bar")]
    public TextMeshProUGUI StageLabel;
    public TextMeshProUGUI HPLabel;
    public TextMeshProUGUI TotalEnemyHPLabel;
    public Button PauseButton;

    [Header("Pause / Menu Panel")]
    public GameObject PausePanel;
    public Button ResumeButton;
    public Button SettingsButton;
    public Button ResetButton;
    public Button LeaveButton;

    [Header("Result Screen")]
    public GameObject ResultPanel;
    public TextMeshProUGUI ResultLabel;    // "Victory!" or "Defeat"
    public TextMeshProUGUI GoldEarnedLabel;
    public Button NextStageButton;
    public Button RetryButton;
    public Button MapButton;

    [Header("Max Combo Alert")]
    public GameObject MaxComboPanel;
    public Button MaxComboDismissButton;

    // ──────────────────────────────────────────────────────────────────────
    private void Start()
    {
        // Subscribe to events
        BattleUIEvents.Instance.OnBattleResult += ShowResult;
        BattleUIEvents.Instance.OnMaxCombo += ShowMaxComboAlert;

        // Wire buttons
        PauseButton.onClick.AddListener(() => PausePanel.SetActive(true));
        ResumeButton.onClick.AddListener(() => PausePanel.SetActive(false));
        LeaveButton.onClick.AddListener(() => GameManager.Instance.ReturnToMap());
        ResetButton.onClick.AddListener(OnReset);

        NextStageButton.onClick.AddListener(OnNextStage);
        RetryButton.onClick.AddListener(OnRetry);
        MapButton.onClick.AddListener(() => GameManager.Instance.ReturnToMap());

        MaxComboDismissButton.onClick.AddListener(() => MaxComboPanel.SetActive(false));

        // Initial state
        PausePanel.SetActive(false);
        ResultPanel.SetActive(false);
        MaxComboPanel.SetActive(false);

        RefreshTopBar();
    }

    private void RefreshTopBar()
    {
        var pd = GameManager.Instance.PlayerData;
        if (StageLabel) StageLabel.text = $"Stage {GameManager.Instance.CurrentStage}";
        if (HPLabel) HPLabel.text = $"HP {pd.CurrentHP}/{pd.MaxHP}";
    }

    private void ShowResult(bool playerWon, int stage)
    {
        ResultPanel.SetActive(true);
        ResultLabel.text = playerWon ? "Victory!" : "Defeat";

        int goldEarned = playerWon ? 50 + stage * 10 : 0;
        GoldEarnedLabel.text = playerWon ? $"+{goldEarned} Gold" : "";

        NextStageButton.gameObject.SetActive(playerWon);
        RetryButton.gameObject.SetActive(!playerWon);
    }

    private void ShowMaxComboAlert()
    {
        MaxComboPanel.SetActive(true);
        // TODO: show the ability-swap UI inside this panel
    }

    private void OnReset()
    {
        PausePanel.SetActive(false);
        GameManager.Instance.StartBattle(GameManager.Instance.CurrentStage);
    }

    private void OnNextStage()
    {
        // GameManager.CurrentStage was already advanced to (completedStage + 1)
        // by BattleManager.EndBattle() when the player won, so reading it here
        // correctly starts the next stage — not a repeat of the one just beaten.
        GameManager.Instance.StartBattle(GameManager.Instance.CurrentStage);
    }

    private void OnRetry()
    {
        // On a loss, CurrentStage was NOT advanced, so retrying the same
        // stage is correct. Restore HP so the player starts fresh.
        GameManager.Instance.PlayerData.CurrentHP = GameManager.Instance.PlayerData.MaxHP;
        GameManager.Instance.StartBattle(GameManager.Instance.CurrentStage);
    }

    private void OnDestroy()
    {
        if (BattleUIEvents.Instance == null) return;
        BattleUIEvents.Instance.OnBattleResult -= ShowResult;
        BattleUIEvents.Instance.OnMaxCombo -= ShowMaxComboAlert;
    }
}