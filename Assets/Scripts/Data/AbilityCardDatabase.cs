// ─────────────────────────────────────────────────────────────────────────────
// AbilityCardDatabase — central lookup for all cards
// ─────────────────────────────────────────────────────────────────────────────

using System.Collections.Generic;

using UnityEngine;

/// <summary>
/// AbilityCardDatabase is a ScriptableObject that holds references to EVERY
/// ability card in the game.  BattleManager and the Gacha system look cards
/// up here by ID.
///
/// HOW TO CREATE:
///   Right-click in Project → Create → TileSpark → Ability Card Database
/// </summary>
[CreateAssetMenu(fileName = "AbilityCardDatabase", menuName = "TileSpark/Ability Card Database")]
public class AbilityCardDatabase : ScriptableObject
{
    public List<AbilityCardData> Cards = new();

    // ── Lookup helpers ─────────────────────────────────────────────────────

    /// <summary>Returns the card with the given ID, or null if not found.</summary>
    public AbilityCardData GetCard(string id)
        => Cards.Find(c => c.CardId == id);

    /// <summary>Returns all cards of a given rarity.</summary>
    public List<AbilityCardData> GetByRarity(CardRarity rarity)
        => Cards.FindAll(c => c.Rarity == rarity);
}
