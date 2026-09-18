using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// CharacterScreen populates and drives the Character panel.
///
/// It shows:
///   • Current player stats (HP, Damage, Speed, Defense, Luck) with their
///     current level and the gold cost to upgrade each one.
///   • A "Damage per block type" breakdown showing how much damage each
///     block color deals per line clear, factoring in BaseDamage,
///     the block's DamageMultiplier, any equipment bonuses, and the
///     currently equipped ability card's star multiplier.
///   • Active powers (Bomb / Omni / Potion) with their current counts.
///   • Total gold display so the player knows if they can afford an upgrade.
/// </summary>
public class CharacterScreen : MonoBehaviour
{
    [Header("Layout Parents")]
    public Transform StatContainer;   // Vertical Layout Group for stat rows
    public Transform BlockContainer;  // Vertical Layout Group for block damage rows

    [Header("Prefabs")]
    public GameObject StatRowPrefab;
    public GameObject BlockRowPrefab;

    [Header("Data References")]
    public BlockDefinition[]   BlockDefinitions;  // all 6 non-Mold blocks
    public AbilityCardDatabase AbilityCardDB;

    [Header("UI")]
    public TextMeshProUGUI GoldLabel;
    public Button          CloseButton;

    [Header("Upgrade Manager")]
    public CharacterUpgradeManager UpgradeManager;

    // ──────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        CloseButton?.onClick.AddListener(() => gameObject.SetActive(false));
    }

    /// <summary>
    /// Called by BottomBarController.Toggle() via SendMessage each time
    /// this panel is opened, guaranteeing the display is always up to date.
    /// </summary>
    private void OnScreenOpened()
    {
        Refresh();
    }

    private void OnEnable()
    {
        Refresh();
    }

    // ── Main refresh ───────────────────────────────────────────────────────

    /// <summary>
    /// Clears and rebuilds both the stat rows and the block damage rows from
    /// current PlayerData. Called whenever the panel is opened or after any
    /// upgrade completes.
    /// </summary>
    public void Refresh()
    {
        var pd = GameManager.Instance.PlayerData;

        if (GoldLabel != null)
            GoldLabel.text = $"Gold: {pd.Gold}";

        BuildStatRows(pd);
        BuildBlockRows(pd);
    }

    // ── Stat rows ──────────────────────────────────────────────────────────

    private void BuildStatRows(PlayerData pd)
    {
        // Clear previous rows
        foreach (Transform child in StatContainer)
            Destroy(child.gameObject);

        // Define each stat as a tuple.
        // (display name, current value string, level, upgrade callback)
        var stats = new (string name, string value, int level, System.Action upgrade)[]
        {
            ("Max HP",     $"{pd.MaxHP}",             pd.HPLevel,
                () => { if (UpgradeManager.UpgradeHP())      Refresh(); }),

            ("Damage",     $"{pd.BaseDamage:0.#}",    pd.DamageLevel,
                () => { if (UpgradeManager.UpgradeDamage())  Refresh(); }),

            ("Speed",      $"{pd.Speed} moves",       pd.SpeedLevel,
                () => { if (UpgradeManager.UpgradeSpeed())   Refresh(); }),

            ("Defense",   $"{pd.Defense:0.#}",        pd.DefenseLevel,
                () => { if (UpgradeManager.UpgradeDefense()) Refresh(); }),

            ("Luck",      $"{pd.Luck * 100f:0.#}%",  pd.LuckLevel,
                () => { if (UpgradeManager.UpgradeLuck())    Refresh(); }),
        };

        foreach (var (name, value, level, upgradeCallback) in stats)
        {
            var go  = Instantiate(StatRowPrefab, StatContainer);
            var row = go.GetComponent<StatRow>();
            if (row == null)
            {
                Debug.LogError("[CharacterScreen] StatRowPrefab has no StatRow component!", StatRowPrefab);
                continue;
            }

            int cost = UpgradeManager != null
                ? UpgradeManager.GetUpgradeCost(level)
                : 0;

            // Pass in a local copy of the callback to avoid closure issues
            var cb = upgradeCallback;
            row.Populate(name, value, level, cost, cb);
        }
    }

    // ── Block damage breakdown rows ────────────────────────────────────────

    private void BuildBlockRows(PlayerData pd)
    {
        foreach (Transform child in BlockContainer)
            Destroy(child.gameObject);

        if (BlockDefinitions == null) return;

        foreach (var def in BlockDefinitions)
        {
            if (def == null || def.BlockType == BlockType.Mold) continue;

            var go = Instantiate(BlockRowPrefab, BlockContainer);
            var row = go.GetComponent<BlockDamageRow>();
            if (row == null)
            {
                Debug.LogError("[CharacterScreen] BlockRowPrefab has no BlockDamageRow component!", BlockRowPrefab);
                continue;
            }

            // Calculate actual damage this block contributes per line clear:
            // BaseDamage × block's DamageMultiplier × active card star multiplier
            float damagePerClear = pd.BaseDamage * def.DamageMultiplier;
            float cardMultiplier = GetCardMultiplier(def.BlockType, pd);
            damagePerClear *= cardMultiplier;

            // Find the active effect for this block
            string effectText = GetActiveEffectText(def.BlockType, pd);

            row.Populate(def, damagePerClear, effectText);
        }
    }

    /// <summary>
    /// Returns the star multiplier from the currently equipped ability card
    /// for the given block type. Returns 1.0 if no card is assigned.
    /// </summary>
    private float GetCardMultiplier(BlockType blockType, PlayerData pd)
    {
        var binding = pd.BlockBindings?.Find(b => b.BlockTypeIndex == (int)blockType);
        if (binding == null || string.IsNullOrEmpty(binding.ActiveCardId)) return 1f;

        var card  = AbilityCardDB?.GetCard(binding.ActiveCardId);
        var owned = pd.OwnedCards?.Find(c => c.CardId == binding.ActiveCardId);
        if (card == null || owned == null) return 1f;

        int starIndex = Mathf.Clamp(owned.Stars - 1, 0, card.StarMultipliers.Length - 1);
        return card.StarMultipliers[starIndex];
    }

    /// <summary>
    /// Returns a display string for the active effect on the given block type.
    /// Shows the card name if one is equipped, otherwise the block's default effect.
    /// </summary>
    private string GetActiveEffectText(BlockType blockType, PlayerData pd)
    {
        var binding = pd.BlockBindings?.Find(b => b.BlockTypeIndex == (int)blockType);
        if (binding != null && !string.IsNullOrEmpty(binding.ActiveCardId))
        {
            var card  = AbilityCardDB?.GetCard(binding.ActiveCardId);
            var owned = pd.OwnedCards?.Find(c => c.CardId == binding.ActiveCardId);
            if (card != null && owned != null)
                return $"{card.CardName} Stars: {owned.Stars}";
        }

        // Fall back to the block's built-in default effect
        var def = System.Array.Find(BlockDefinitions, d => d != null && d.BlockType == blockType);
        return def != null && def.DefaultEffect != BlockEffect.None
            ? def.DefaultEffect.ToString()
            : "No Effect";
    }
}
