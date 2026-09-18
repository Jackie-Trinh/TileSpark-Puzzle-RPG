using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// StatRow is attached to the root of "StatRowPrefab" and populates
/// all child UI elements when CharacterScreen calls Populate().
///
/// EXPECTED CHILD STRUCTURE:
///   Root (Horizontal Layout Group) + StatRow component
///     ├─ NameLabel     (TMP — name of the stat)
///     ├─ ValueLabel    (TMP — current value, e.g. "120")
///     ├─ LevelLabel    (TMP — level badge, e.g. "Lv.5")
///     ├─ CostLabel     (TMP — gold cost to upgrade next level)
///     └─ UpgradeButton (Button — calls Populate's upgradeAction on click)
///
/// Each label is found by the field names below — drag them in the prefab
/// Inspector, or rename your child objects to match.
/// </summary>
public class StatRow : MonoBehaviour
{
    [Header("Child References (assign in prefab)")]
    public TextMeshProUGUI NameLabel;
    public TextMeshProUGUI ValueLabel;
    public TextMeshProUGUI LevelLabel;
    public TextMeshProUGUI CostLabel;
    public Button UpgradeButton;

    /// <summary>Fills this row with the given stat data and wires the upgrade button.</summary>
    public void Populate(string statName, string currentValue, int level,
                          int upgradeCost, System.Action onUpgrade)
    {
        if (NameLabel != null) NameLabel.text = statName;
        if (ValueLabel != null) ValueLabel.text = currentValue;
        if (LevelLabel != null) LevelLabel.text = $"Lv.{level}";
        if (CostLabel != null) CostLabel.text = $"Cost: {upgradeCost}";

        if (UpgradeButton != null)
        {
            UpgradeButton.onClick.RemoveAllListeners();
            UpgradeButton.onClick.AddListener(() => onUpgrade?.Invoke());
        }
    }
}