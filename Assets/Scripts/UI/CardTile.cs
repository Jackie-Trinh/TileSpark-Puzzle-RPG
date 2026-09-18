using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// CardTile is attached to the root of "CardTilePrefab".
/// Shows one ability card in the PowersScreen card picker grid.
///
/// EXPECTED CHILD STRUCTURE:
///   Root (Button + Image for rarity background) + CardTile component
///     ├─ CardArtwork    (Image — from AbilityCardData.CardArtwork)
///     ├─ CardNameLabel  (TMP)
///     ├─ StarsLabel     (TMP — e.g. "★★★☆☆")
///     ├─ EffectLabel    (TMP — e.g. "Burn")
///     ├─ AssignedBadge  (GameObject — "ON" badge, shown when assigned to selected block)
///     └─ UsedBadge      (GameObject — "IN USE" badge, shown when used on another block)
///
/// A CanvasGroup is added automatically in code to dim the tile when the
/// card is already assigned to a different block (still tappable to reassign).
/// </summary>
public class CardTile : MonoBehaviour
{
    [Header("Child References (assign in prefab)")]
    public Image           RarityBackground;
    public Image           CardArtwork;
    public TextMeshProUGUI CardNameLabel;
    public TextMeshProUGUI StarsLabel;
    public TextMeshProUGUI EffectLabel;
    public GameObject      AssignedBadge;  // shown when this IS the current block's card
    public GameObject      UsedBadge;      // shown when this is assigned to another block

    [Tooltip("Alpha applied to the whole tile when the card is used on a different block.")]
    public float UsedElsewhereAlpha = 0.5f;

    /// <summary>Populates this card tile from a card definition and its current assignment state.</summary>
    public void Populate(AbilityCardData card, int stars,
                          bool assignedToSelectedBlock, bool usedElsewhere,
                          Color rarityColor)
    {
        if (RarityBackground != null) RarityBackground.color = rarityColor;

        if (CardArtwork != null)
        {
            CardArtwork.sprite  = card.CardArtwork;
            CardArtwork.enabled = card.CardArtwork != null;
        }

        if (CardNameLabel != null) CardNameLabel.text = card.CardName;
        if (EffectLabel   != null) EffectLabel.text   = card.GrantedEffect.ToString();

        if (StarsLabel != null)
        {
            // Build a filled/empty star string: e.g. stars=3 → "★★★☆☆"
            string starStr = "";
            for (int i = 1; i <= 5; i++)
                starStr += (i <= stars) ? "★" : "☆";
            StarsLabel.text = starStr;
        }

        if (AssignedBadge != null) AssignedBadge.SetActive(assignedToSelectedBlock);
        if (UsedBadge     != null) UsedBadge.SetActive(usedElsewhere && !assignedToSelectedBlock);

        // Dim the whole tile if used elsewhere so the player can see it is
        // taken — they can still tap it to reassign it to the current block.
        var canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();
        canvasGroup.alpha = (usedElsewhere && !assignedToSelectedBlock)
            ? UsedElsewhereAlpha
            : 1f;
    }
}
