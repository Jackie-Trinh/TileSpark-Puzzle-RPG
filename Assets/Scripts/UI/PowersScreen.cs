using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// PowersScreen populates and drives the Powers panel.
///
/// It has two sections:
///
///   TOP — BLOCK ASSIGNMENT GRID
///     Shows each block type (Red, Blue, Green…) as a row with:
///       • The block's color swatch
///       • The block's name
///       • Which ability card is currently assigned ("None" if empty)
///       • The active effect that will trigger when that block is in a clear
///     Clicking a block row opens the CARD PICKER for that block.
///
///   BOTTOM — CARD PICKER (shown when a block row is selected)
///     Shows all ability cards the player OWNS as a scrollable grid.
///     Cards are shown with their artwork, name, star level, rarity, and effect.
///     Tapping a card assigns it to the selected block.
///     Tapping the currently assigned card UNASSIGNS it (sets to None).
///     Cards already assigned to ANOTHER block are shown in a greyed-out state
///     so the player knows they're in use but can still be reassigned.
/// </summary>
public class PowersScreen : MonoBehaviour
{
    [Header("Layout Parents")]
    public Transform BlockRowContainer;  // holds one row per block type
    public Transform CardContainer;      // holds card tiles in the picker

    [Header("Card Picker")]
    public GameObject      CardPickerPanel;
    public TextMeshProUGUI SelectedBlockLabel;

    [Header("Prefabs")]
    public GameObject PowersBlockRowPrefab;
    public GameObject CardTilePrefab;

    [Header("Data References")]
    public BlockDefinition[]   BlockDefinitions;  // 6 non-Mold blocks
    public AbilityCardDatabase AbilityCardDB;

    [Header("UI")]
    public Button CloseButton;

    // Which block type the player is currently picking a card for
    private int? _selectedBlockIndex = null;

    private static readonly Color[] RarityColors =
    {
        new Color(0.7f, 0.7f, 0.7f),  // Normal
        new Color(0.2f, 0.8f, 0.2f),  // Uncommon
        new Color(0.2f, 0.4f, 0.9f),  // Rare
        new Color(0.6f, 0.2f, 0.9f),  // Epic
        new Color(1.0f, 0.7f, 0.1f),  // Legendary
    };

    // ──────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        CloseButton?.onClick.AddListener(() => gameObject.SetActive(false));
    }

    private void OnEnable()       => Refresh();
    private void OnScreenOpened() => Refresh();

    // ── Main refresh ───────────────────────────────────────────────────────

    public void Refresh()
    {
        BuildBlockRows();
        RefreshCardPicker();
    }

    // ── Block assignment rows ──────────────────────────────────────────────

    private void BuildBlockRows()
    {
        foreach (Transform child in BlockRowContainer)
            Destroy(child.gameObject);

        if (BlockDefinitions == null) return;

        var pd = GameManager.Instance.PlayerData;

        foreach (var def in BlockDefinitions)
        {
            if (def == null || def.BlockType == BlockType.Mold) continue;

            var go  = Instantiate(PowersBlockRowPrefab, BlockRowContainer);
            var row = go.GetComponent<PowersBlockRow>();
            if (row == null) continue;

            int blockIndex = (int)def.BlockType;

            // Find what card is currently assigned to this block (if any)
            var binding  = pd.BlockBindings?.Find(b => b.BlockTypeIndex == blockIndex);
            var cardData = binding != null ? AbilityCardDB?.GetCard(binding.ActiveCardId) : null;
            var owned    = cardData != null ? pd.OwnedCards?.Find(c => c.CardId == cardData.CardId) : null;

            string assignedText = cardData != null ? $"{cardData.CardName} ★{owned?.Stars ?? 1}" : "None";
            string effectText   = cardData != null
                ? cardData.GrantedEffect.ToString()
                : (def.DefaultEffect != BlockEffect.None ? $"Default: {def.DefaultEffect}" : "No effect");

            bool isSelected = _selectedBlockIndex == blockIndex;
            var  captured   = blockIndex;

            row.Populate(def, assignedText, effectText, isSelected);

            go.GetComponent<Button>()?.onClick.AddListener(() =>
            {
                _selectedBlockIndex = (_selectedBlockIndex == captured) ? null : captured;
                Refresh();
            });
        }
    }

    // ── Card picker ────────────────────────────────────────────────────────

    private void RefreshCardPicker()
    {
        bool pickerOpen = _selectedBlockIndex.HasValue;
        if (CardPickerPanel != null) CardPickerPanel.SetActive(pickerOpen);

        if (!pickerOpen) return;

        // Update the "Assigning for: Red Block" label
        if (SelectedBlockLabel != null)
        {
            var selDef = System.Array.Find(BlockDefinitions,
                d => d != null && (int)d.BlockType == _selectedBlockIndex.Value);
            SelectedBlockLabel.text = selDef != null
                ? $"Assign card to: {selDef.DisplayName}"
                : "Select a card";
        }

        BuildCardTiles();
    }

    private void BuildCardTiles()
    {
        foreach (Transform child in CardContainer)
            Destroy(child.gameObject);

        var pd = GameManager.Instance.PlayerData;
        if (pd.OwnedCards == null || pd.OwnedCards.Count == 0)
        {
            var emptyGO = new GameObject("EmptyLabel", typeof(RectTransform), typeof(TextMeshProUGUI));
            emptyGO.transform.SetParent(CardContainer, false);
            emptyGO.GetComponent<TextMeshProUGUI>().text = "No ability cards owned yet.\nPull from the Gacha to get cards!";
            return;
        }

        foreach (var ownedCard in pd.OwnedCards)
        {
            var cardData = AbilityCardDB?.GetCard(ownedCard.CardId);
            if (cardData == null) continue;

            var go   = Instantiate(CardTilePrefab, CardContainer);
            var tile = go.GetComponent<CardTile>();
            if (tile == null) continue;

            // Is this card assigned to the currently selected block?
            var  binding        = pd.BlockBindings?.Find(b => b.BlockTypeIndex == _selectedBlockIndex.Value);
            bool assignedToThis = binding?.ActiveCardId == ownedCard.CardId;

            // Is this card assigned to a DIFFERENT block?
            bool usedElsewhere = IsCardUsedElsewhere(ownedCard.CardId, _selectedBlockIndex.Value, pd);

            Color rarityColor = RarityColors[Mathf.Clamp((int)cardData.Rarity, 0, RarityColors.Length - 1)];
            var   capturedId  = ownedCard.CardId;

            tile.Populate(cardData, ownedCard.Stars, assignedToThis, usedElsewhere, rarityColor);

            go.GetComponent<Button>()?.onClick.AddListener(() =>
            {
                if (assignedToThis)
                    UnassignCard(_selectedBlockIndex.Value, pd);
                else
                    AssignCard(_selectedBlockIndex.Value, capturedId, pd);

                GameManager.Instance.SaveGame();
                Refresh();
            });
        }
    }

    // ── Assignment helpers ─────────────────────────────────────────────────

    /// <summary>Assigns a card to the given block type, creating a binding if needed.</summary>
    private void AssignCard(int blockIndex, string cardId, PlayerData pd)
    {
        if (pd.BlockBindings == null) pd.BlockBindings = new List<BlockAbilityBinding>();

        var existing = pd.BlockBindings.Find(b => b.BlockTypeIndex == blockIndex);
        if (existing != null)
        {
            existing.ActiveCardId = cardId;
        }
        else
        {
            pd.BlockBindings.Add(new BlockAbilityBinding
            {
                BlockTypeIndex = blockIndex,
                ActiveCardId   = cardId,
            });
        }
    }

    /// <summary>Removes the card assignment from the given block type.</summary>
    private void UnassignCard(int blockIndex, PlayerData pd)
    {
        var binding = pd.BlockBindings?.Find(b => b.BlockTypeIndex == blockIndex);
        if (binding != null) binding.ActiveCardId = "";
    }

    /// <summary>
    /// Returns true if the given card is assigned to ANY block OTHER THAN
    /// the currently selected one — used to show the "in use" greyed state.
    /// </summary>
    private bool IsCardUsedElsewhere(string cardId, int selectedBlock, PlayerData pd)
    {
        if (pd.BlockBindings == null) return false;
        foreach (var b in pd.BlockBindings)
        {
            if (b.BlockTypeIndex != selectedBlock && b.ActiveCardId == cardId)
                return true;
        }
        return false;
    }
}
