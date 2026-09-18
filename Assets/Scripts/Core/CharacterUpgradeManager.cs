using UnityEngine;

/// <summary>
/// CharacterUpgradeManager handles stat upgrades on the Character page.
///
/// Each stat has a level (stored in PlayerData).
/// Cost to upgrade grows with the current level, making later upgrades expensive.
///
/// Formula: cost = baseCost * level^costExponent
/// </summary>
public class CharacterUpgradeManager : MonoBehaviour
{
    [Header("Upgrade Cost Settings")]
    [Tooltip("Gold cost base for each stat at level 1.")]
    public int BaseCost = 100;

    [Tooltip("How steeply cost increases with level. 1.5 is a common sweet spot.")]
    public float CostExponent = 1.5f;

    [Header("Stat Increments per upgrade")]
    public int   HPPerLevel      = 20;
    public float DamagePerLevel  = 2f;
    public int   SpeedPerLevel   = 1;
    public float DefensePerLevel = 1f;
    public float LuckPerLevel    = 0.01f;

    // ── Upgrade methods (call from UI buttons) ─────────────────────────────

    public bool UpgradeHP()      => TryUpgrade(ref GetPD().HPLevel,      ApplyHP);
    public bool UpgradeDamage()  => TryUpgrade(ref GetPD().DamageLevel,  ApplyDamage);
    public bool UpgradeSpeed()   => TryUpgrade(ref GetPD().SpeedLevel,   ApplySpeed);
    public bool UpgradeDefense() => TryUpgrade(ref GetPD().DefenseLevel, ApplyDefense);
    public bool UpgradeLuck()    => TryUpgrade(ref GetPD().LuckLevel,    ApplyLuck);

    // ── Cost preview (call from UI to show next upgrade cost) ─────────────

    public int GetUpgradeCost(int currentLevel)
        => Mathf.RoundToInt(BaseCost * Mathf.Pow(currentLevel, CostExponent));

    // ── Private helpers ────────────────────────────────────────────────────

    private delegate void ApplyDelegate(PlayerData pd);

    private bool TryUpgrade(ref int levelField, ApplyDelegate apply)
    {
        int cost = GetUpgradeCost(levelField);
        if (!GameManager.Instance.SpendGold(cost)) return false;

        levelField++;
        apply(GameManager.Instance.PlayerData);
        GameManager.Instance.SaveGame();
        return true;
    }

    private void ApplyHP(PlayerData pd)
    {
        pd.MaxHP     += HPPerLevel;
        pd.CurrentHP  = pd.MaxHP;   // restore to new max on upgrade
    }
    private void ApplyDamage(PlayerData pd)  => pd.BaseDamage += DamagePerLevel;
    private void ApplySpeed(PlayerData pd)   => pd.Speed      += SpeedPerLevel;
    private void ApplyDefense(PlayerData pd) => pd.Defense    += DefensePerLevel;
    private void ApplyLuck(PlayerData pd)    => pd.Luck        = Mathf.Min(1f, pd.Luck + LuckPerLevel);

    private PlayerData GetPD() => GameManager.Instance.PlayerData;
}
