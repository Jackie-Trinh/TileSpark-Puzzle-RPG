using UnityEngine;

// ─────────────────────────────────────────────────────────────────────────────
// AbilityCardData — one asset per card
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// AbilityCardData is a ScriptableObject representing one ability card.
///
/// HOW TO CREATE:
///   Right-click in Project → Create → TileSpark → Ability Card
/// </summary>
[CreateAssetMenu(fileName = "Card_New", menuName = "TileSpark/Ability Card")]
public class AbilityCardData : ScriptableObject
{
    [Header("Identity")]
    public string      CardId;         // unique string ID, e.g. "card_burn_01"
    public string      CardName;
    public Sprite      CardArtwork;

    [Header("Rarity")]
    public CardRarity  Rarity = CardRarity.Normal;

    [Header("Effect")]
    [Tooltip("Which BlockEffect this card grants to the assigned block type.")]
    public BlockEffect GrantedEffect;

    [Header("Per-Star Scaling")]
    [Tooltip("Effect power multiplier at each star level (index 0 = 1 star).")]
    public float[] StarMultipliers = { 1f, 1.2f, 1.5f, 1.8f, 2.2f };

    [Header("Upgrade Costs (copies needed per star upgrade)")]
    public int[] CopiesNeeded = { 0, 2, 4, 8, 16 }; // index 0 unused; 1→2 needs 2 copies

    [Header("Description")]
    [TextArea] public string Description;
}