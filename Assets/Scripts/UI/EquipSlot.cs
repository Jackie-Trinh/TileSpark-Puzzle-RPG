using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// EquipSlot is attached to the root of "EquipSlotPrefab".
/// It represents ONE of the 6 equipment slots (Helmet, Armor, etc.)
/// and shows whether that slot is currently filled or empty.
///
/// EXPECTED CHILD STRUCTURE:
///   Root (Button + Image for background) + EquipSlot component
///     ├─ SlotNameLabel  (TMP — always visible, e.g. "Helmet")
///     ├─ ItemIcon       (Image — hidden when empty)
///     ├─ ItemNameLabel  (TMP — hidden when empty)
///     └─ EmptyLabel     (TMP — shown when empty, hidden when filled)
/// </summary>
public class EquipSlot : MonoBehaviour
{
    [Header("Child References (assign in prefab)")]
    public TextMeshProUGUI SlotNameLabel;
    public Image ItemIcon;
    public TextMeshProUGUI ItemNameLabel;
    public TextMeshProUGUI EmptyLabel;

    [Header("Selection highlight (optional)")]
    [Tooltip("Image on the root — tinted when this slot is selected.")]
    public Image BackgroundImage;

    public Color SelectedColor = new Color(1f, 0.85f, 0.2f, 0.4f);  // yellow tint
    public Color NormalColor = new Color(0.2f, 0.2f, 0.2f, 0.6f); // dark grey

    /// <summary>
    /// Fills this slot tile from a definition (or null if empty) and
    /// highlights it if it is the currently selected slot.
    /// </summary>
    public void Populate(EquipmentSlot slotType, EquipmentDefinition equipped, bool isSelected)
    {
        if (SlotNameLabel != null)
            SlotNameLabel.text = slotType.ToString();

        bool hasItem = equipped != null;

        if (ItemIcon != null) ItemIcon.gameObject.SetActive(hasItem);
        if (ItemNameLabel != null) ItemNameLabel.gameObject.SetActive(hasItem);
        if (EmptyLabel != null) EmptyLabel.gameObject.SetActive(!hasItem);

        if (hasItem)
        {
            if (ItemIcon != null) ItemIcon.sprite = equipped.ItemIcon;
            if (ItemNameLabel != null) ItemNameLabel.text = equipped.ItemName;
        }

        if (BackgroundImage != null)
            BackgroundImage.color = isSelected ? SelectedColor : NormalColor;
    }
}