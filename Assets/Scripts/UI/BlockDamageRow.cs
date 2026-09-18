using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// BlockDamageRow is attached to the root of "BlockRowPrefab" and shows
/// damage-per-clear and active effect for ONE block type.
///
/// Used by CharacterScreen to build the block damage breakdown section.
///
/// EXPECTED CHILD STRUCTURE:
///   Root (Horizontal Layout Group) + BlockDamageRow component
///     ├─ ColorSwatch    (Image — tinted to the block's color)
///     ├─ BlockNameLabel (TMP — e.g. "Red Block")
///     ├─ DamageLabel    (TMP — e.g. "12.5 dmg / clear")
///     └─ EffectLabel    (TMP — e.g. "Ember Surge ★3" or "Burn")
/// </summary>
public class BlockDamageRow : MonoBehaviour
{
    [Header("Child References (assign in prefab)")]
    public Image           ColorSwatch;
    public TextMeshProUGUI BlockNameLabel;
    public TextMeshProUGUI DamageLabel;
    public TextMeshProUGUI EffectLabel;

    /// <summary>Fills this row from a BlockDefinition and computed values.</summary>
    public void Populate(BlockDefinition def, float damagePerClear, string effectText)
    {
        if (ColorSwatch    != null) ColorSwatch.color   = def.BlockColor;
        if (BlockNameLabel != null) BlockNameLabel.text = def.DisplayName;
        if (DamageLabel    != null) DamageLabel.text    = $"{damagePerClear:0.#} dmg / clear";
        if (EffectLabel    != null) EffectLabel.text    = effectText;
    }
}
