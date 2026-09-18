using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// InventoryItem is attached to the root of "InventoryItemPrefab".
/// Shows one gear item in the EquipmentScreen's scrollable inventory list.
///
/// EXPECTED CHILD STRUCTURE:
///   Root (Button) + InventoryItem component
///     ├─ RarityBorder   (Image — colored to rarity)
///     ├─ ItemIcon       (Image)
///     ├─ ItemNameLabel  (TMP)
///     ├─ StatsLabel     (TMP — short stat summary)
///     └─ EquippedBadge  (GameObject — "EQUIPPED" overlay, shown only when equipped)
/// </summary>
public class InventoryItem : MonoBehaviour
{
    [Header("Child References (assign in prefab)")]
    public Image           RarityBorder;
    public Image           ItemIcon;
    public TextMeshProUGUI ItemNameLabel;
    public TextMeshProUGUI StatsLabel;
    public GameObject      EquippedBadge;

    /// <summary>Populates this item row from a definition and current equip state.</summary>
    public void Populate(EquipmentDefinition def, bool isEquipped, Color rarityColor)
    {
        if (RarityBorder  != null) RarityBorder.color  = rarityColor;
        if (ItemIcon      != null) ItemIcon.sprite      = def.ItemIcon;
        if (ItemNameLabel != null) ItemNameLabel.text   = def.ItemName;
        if (EquippedBadge != null) EquippedBadge.SetActive(isEquipped);

        if (StatsLabel != null)
            StatsLabel.text = BuildStatSummary(def);
    }

    /// <summary>
    /// Builds a compact stat summary string showing only non-zero bonuses,
    /// e.g. "+25 HP  +3 Def  Burn".
    /// </summary>
    private string BuildStatSummary(EquipmentDefinition def)
    {
        var parts = new List<string>();

        if (def.BonusHP      != 0) parts.Add($"+{def.BonusHP} HP");
        if (def.BonusDamage  != 0) parts.Add($"+{def.BonusDamage:0.#} Dmg");
        if (def.BonusSpeed   != 0) parts.Add($"+{def.BonusSpeed} Spd");
        if (def.BonusDefense != 0) parts.Add($"+{def.BonusDefense:0.#} Def");
        if (def.BonusLuck    != 0) parts.Add($"+{def.BonusLuck * 100f:0.#}% Luck");
        if (def.PassiveEffect != BlockEffect.None) parts.Add(def.PassiveEffect.ToString());
        if (!string.IsNullOrEmpty(def.SetId))      parts.Add($"[{def.SetId}]");

        return parts.Count > 0 ? string.Join("  ", parts) : "No bonuses";
    }
}
