using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// GachaSystem handles both Normal pulls (paid with gold) and
/// Premium pulls (paid with diamonds).
///
/// Pull rates are configurable in the Inspector.
/// After a pull, the result card is added to the player's OwnedCards
/// list (or its copy count is incremented if already owned).
/// </summary>
public class GachaSystem : MonoBehaviour
{
    // ── Configuration ──────────────────────────────────────────────────────
    [Header("Card Database")]
    public AbilityCardDatabase CardDatabase;

    [Header("Pull Costs")]
    public int NormalPullCost  = 200;   // gold
    public int PremiumPullCost = 100;   // diamonds

    [Header("Normal Pull Rates (must sum to 1.0)")]
    public float NormalRate_Normal    = 0.60f;
    public float NormalRate_Uncommon  = 0.25f;
    public float NormalRate_Rare      = 0.10f;
    public float NormalRate_Epic      = 0.04f;
    public float NormalRate_Legendary = 0.01f;

    [Header("Premium Pull Rates (must sum to 1.0)")]
    public float PremiumRate_Normal    = 0.40f;
    public float PremiumRate_Uncommon  = 0.25f;
    public float PremiumRate_Rare      = 0.20f;
    public float PremiumRate_Epic      = 0.10f;
    public float PremiumRate_Legendary = 0.05f;

    // ── Public API ─────────────────────────────────────────────────────────

    /// <summary>
    /// Attempts a normal pull.  Deducts gold and returns the pulled card.
    /// Returns null if the player can't afford it.
    /// </summary>
    public AbilityCardData DoNormalPull()
    {
        if (!GameManager.Instance.SpendGold(NormalPullCost)) return null;
        return Pull(NormalPullRates());
    }

    /// <summary>
    /// Attempts a premium pull.  Deducts diamonds and returns the pulled card.
    /// Returns null if the player can't afford it.
    /// </summary>
    public AbilityCardData DoPremiumPull()
    {
        if (!GameManager.Instance.SpendDiamonds(PremiumPullCost)) return null;
        return Pull(PremiumPullRates());
    }

    /// <summary>
    /// Tries to upgrade a card from its current star level to the next.
    /// Returns true if the upgrade succeeded.
    /// The player must own enough copies (as defined by CardData.CopiesNeeded).
    /// </summary>
    public bool TryUpgradeCard(string cardId)
    {
        var pd   = GameManager.Instance.PlayerData;
        var owned = pd.OwnedCards.Find(c => c.CardId == cardId);
        if (owned == null)       return false;
        if (owned.Stars >= 5)    return false;   // already max star

        var def          = CardDatabase.GetCard(cardId);
        int copiesNeeded = def.CopiesNeeded[owned.Stars]; // index = current stars

        if (owned.Copies < copiesNeeded) return false;

        owned.Copies -= copiesNeeded;
        owned.Stars++;
        GameManager.Instance.SaveGame();
        return true;
    }

    // ── Private helpers ────────────────────────────────────────────────────

    private AbilityCardData Pull(float[] rates)
    {
        // Roll a random rarity based on rates[0..4]
        float roll   = Random.value;
        float cumul  = 0f;
        CardRarity rarity = CardRarity.Normal;

        for (int i = 0; i < rates.Length; i++)
        {
            cumul += rates[i];
            if (roll < cumul)
            {
                rarity = (CardRarity)i;
                break;
            }
        }

        // Pick a random card of that rarity
        var pool = CardDatabase.GetByRarity(rarity);
        if (pool == null || pool.Count == 0)
        {
            Debug.LogWarning($"[Gacha] No cards of rarity {rarity}. Falling back to Normal.");
            pool = CardDatabase.GetByRarity(CardRarity.Normal);
        }

        var card = pool[Random.Range(0, pool.Count)];
        GrantCardToPlayer(card);
        return card;
    }

    private void GrantCardToPlayer(AbilityCardData card)
    {
        var pd    = GameManager.Instance.PlayerData;
        var owned = pd.OwnedCards.Find(c => c.CardId == card.CardId);

        if (owned != null)
        {
            owned.Copies++;   // already have it — add a copy for upgrading
        }
        else
        {
            pd.OwnedCards.Add(new OwnedCard { CardId = card.CardId, Stars = 1, Copies = 1 });
        }

        GameManager.Instance.SaveGame();
    }

    private float[] NormalPullRates() => new[]
    {
        NormalRate_Normal, NormalRate_Uncommon, NormalRate_Rare,
        NormalRate_Epic,   NormalRate_Legendary
    };

    private float[] PremiumPullRates() => new[]
    {
        PremiumRate_Normal, PremiumRate_Uncommon, PremiumRate_Rare,
        PremiumRate_Epic,   PremiumRate_Legendary
    };
}
