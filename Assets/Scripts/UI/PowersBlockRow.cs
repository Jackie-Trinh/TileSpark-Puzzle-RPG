using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// PowersBlockRow is attached to the root of "PowersBlockRowPrefab".
/// Shows one block type's assignment status in the Powers screen.
///
/// EXPECTED CHILD STRUCTURE:
///   Root (Button + Image background) + PowersBlockRow component
///     ├─ ColorSwatch        (Image — tinted to block's color)
///     ├─ BlockNameLabel     (TMP — e.g. "Red Block")
///     ├─ AssignedCardLabel  (TMP — card name + stars, or "None")
///     └─ EffectLabel        (TMP — e.g. "Burn" or "Default: Freeze")
/// </summary>
public class PowersBlockRow : MonoBehaviour
{
    [Header("Child References (assign in prefab)")]
    public Image ColorSwatch;
    public TextMeshProUGUI BlockNameLabel;
    public TextMeshProUGUI AssignedCardLabel;
    public TextMeshProUGUI EffectLabel;
    public Image BackgroundImage;

    public Color SelectedColor = new Color(1f, 0.85f, 0.2f, 0.4f);
    public Color NormalColor = new Color(0.15f, 0.15f, 0.15f, 0.8f);

    public void Populate(BlockDefinition def, string assignedCardText,
                          string effectText, bool isSelected)
    {
        if (ColorSwatch != null) ColorSwatch.color = def.BlockColor;
        if (BlockNameLabel != null) BlockNameLabel.text = def.DisplayName;
        if (AssignedCardLabel != null) AssignedCardLabel.text = assignedCardText;
        if (EffectLabel != null) EffectLabel.text = effectText;
        if (BackgroundImage != null) BackgroundImage.color = isSelected ? SelectedColor : NormalColor;
    }
}