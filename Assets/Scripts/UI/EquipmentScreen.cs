using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// EquipmentScreen populates and drives the Equipment panel.
///
/// It shows two sections:
///
///   TOP — EQUIPPED SLOTS (6 slots: Helmet, Armor, Shoes, Weapon, Amulet, Ring)
///     Each slot shows the currently equipped item's icon, name, and a
///     small summary of its stat bonuses. Clicking a slot opens the
///     inventory section filtered to that slot type.
///
///   BOTTOM — INVENTORY (scrollable list of all gear the player owns)
///     Filtered to the currently selected slot. Tapping an item equips it
///     to the selected slot; if that item is already equipped it unequips it.
///     Shows item name, icon, rarity color, and stat bonuses.
///
///   STAT TOTAL PANEL (shown alongside the slots)
///     Adds up base stats + all equipped item bonuses and displays them.
///
///   EQUIPMENT SLOT PREFAB ("EquipSlotPrefab"):
///     Root (Button) + EquipSlot component
///       ├─ SlotNameLabel  (TMP — slot type, e.g. "Helmet")
///       ├─ ItemIcon       (Image — hidden when slot is empty)
///       ├─ ItemNameLabel  (TMP — hidden when slot is empty)
///       └─ EmptyLabel     (TMP — "Empty", hidden when slot is filled)
///
///   INVENTORY ITEM PREFAB ("InventoryItemPrefab"):
///     Root (Button) + InventoryItem component
///       ├─ RarityBorder   (Image — colored by rarity)
///       ├─ ItemIcon       (Image)
///       ├─ ItemNameLabel  (TMP)
///       ├─ StatsLabel     (TMP — condensed stat summary)
///       └─ EquippedBadge  (GameObject — shown when this item is equipped)
///
///   INSPECTOR FIELDS:
///     • SlotContainer       — Grid/Horizontal layout parent for 6 slot buttons
///     • InventoryContainer  — Vertical Layout Group + ScrollRect content
///     • StatTotalPanel      — panel with HP/Damage/Speed/Defense/Luck TMP labels
///     • EquipSlotPrefab     / InventoryItemPrefab
///     • EquipDB             — EquipmentDatabase ScriptableObject
///     • CloseButton
/// </summary>
public class EquipmentScreen : MonoBehaviour
{
    [Header("Layout Parents")]
    public Transform SlotContainer;       // holds the 6 equipment slot buttons
    public Transform InventoryContainer;  // scrollable list of owned gear

    [Header("Prefabs")]
    public GameObject EquipSlotPrefab;
    public GameObject InventoryItemPrefab;

    [Header("Stat Total Labels")]
    public TextMeshProUGUI TotalHPLabel;
    public TextMeshProUGUI TotalDamageLabel;
    public TextMeshProUGUI TotalSpeedLabel;
    public TextMeshProUGUI TotalDefenseLabel;
    public TextMeshProUGUI TotalLuckLabel;

    [Header("Data References")]
    public EquipmentDatabase EquipDB;

    [Header("UI")]
    public Button CloseButton;

    // Currently selected slot for inventory filtering (null = show all)
    private EquipmentSlot? _selectedSlot = null;

    private static readonly Color[] RarityColors =
    {
        new Color(0.7f, 0.7f, 0.7f),  // Normal    — grey
        new Color(0.2f, 0.8f, 0.2f),  // Uncommon  — green
        new Color(0.2f, 0.4f, 0.9f),  // Rare      — blue
        new Color(0.6f, 0.2f, 0.9f),  // Epic      — purple
        new Color(1.0f, 0.7f, 0.1f),  // Legendary — gold
    };

    // ──────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        CloseButton?.onClick.AddListener(() => gameObject.SetActive(false));
    }

    private void OnEnable()  => Refresh();
    private void OnScreenOpened() => Refresh();

    // ── Main refresh ───────────────────────────────────────────────────────

    public void Refresh()
    {
        BuildSlots();
        BuildInventory(_selectedSlot);
        RefreshStatTotals();
    }

    // ── Equipment slots ────────────────────────────────────────────────────

    private void BuildSlots()
    {
        foreach (Transform child in SlotContainer)
            Destroy(child.gameObject);

        var pd = GameManager.Instance.PlayerData;

        // Each slot type with its matching equipped item ID
        var slots = new (EquipmentSlot slot, string equippedId)[]
        {
            (EquipmentSlot.Helmet, pd.HelmetId),
            (EquipmentSlot.Armor,  pd.ArmorId),
            (EquipmentSlot.Shoes,  pd.ShoesId),
            (EquipmentSlot.Weapon, pd.WeaponId),
            (EquipmentSlot.Amulet, pd.AmuletId),
            (EquipmentSlot.Ring,   pd.RingId),
        };

        foreach (var (slotType, equippedId) in slots)
        {
            var go   = Instantiate(EquipSlotPrefab, SlotContainer);
            var slot = go.GetComponent<EquipSlot>();
            if (slot == null) continue;

            var equippedItem = EquipDB?.GetItem(equippedId);
            var capturedSlot = slotType;

            slot.Populate(slotType, equippedItem, isSelected: _selectedSlot == slotType);

            // Clicking a slot selects/deselects it and filters the inventory
            go.GetComponent<Button>()?.onClick.AddListener(() =>
            {
                _selectedSlot = (_selectedSlot == capturedSlot) ? null : capturedSlot;
                Refresh();
            });
        }
    }

    // ── Inventory ──────────────────────────────────────────────────────────

    private void BuildInventory(EquipmentSlot? filterSlot)
    {
        foreach (Transform child in InventoryContainer)
            Destroy(child.gameObject);

        var pd = GameManager.Instance.PlayerData;

        if (pd.OwnedEquipmentIds == null || pd.OwnedEquipmentIds.Count == 0)
        {
            // Show an empty-state message
            var emptyGO    = new GameObject("EmptyLabel", typeof(RectTransform), typeof(TextMeshProUGUI));
            emptyGO.transform.SetParent(InventoryContainer, false);
            emptyGO.GetComponent<TextMeshProUGUI>().text = "No gear owned yet.\nGet equipment from the Gacha or Dungeons!";
            return;
        }

        foreach (var itemId in pd.OwnedEquipmentIds)
        {
            var def = EquipDB?.GetItem(itemId);
            if (def == null) continue;

            // Filter by slot if one is selected
            if (filterSlot.HasValue && def.Slot != filterSlot.Value) continue;

            var go   = Instantiate(InventoryItemPrefab, InventoryContainer);
            var item = go.GetComponent<InventoryItem>();
            if (item == null) continue;

            bool isEquipped = IsEquipped(def, pd);
            var  capturedId = itemId;

            item.Populate(def, isEquipped, RarityColors[(int)def.Rarity]);

            go.GetComponent<Button>()?.onClick.AddListener(() =>
            {
                if (isEquipped)
                    UnequipItem(def, pd);
                else
                    EquipItem(def, pd);

                GameManager.Instance.SaveGame();
                Refresh();
            });
        }
    }

    // ── Equip / unequip ────────────────────────────────────────────────────

    private void EquipItem(EquipmentDefinition def, PlayerData pd)
    {
        switch (def.Slot)
        {
            case EquipmentSlot.Helmet: pd.HelmetId = def.ItemId; break;
            case EquipmentSlot.Armor:  pd.ArmorId  = def.ItemId; break;
            case EquipmentSlot.Shoes:  pd.ShoesId  = def.ItemId; break;
            case EquipmentSlot.Weapon: pd.WeaponId = def.ItemId; break;
            case EquipmentSlot.Amulet: pd.AmuletId = def.ItemId; break;
            case EquipmentSlot.Ring:   pd.RingId   = def.ItemId; break;
        }
    }

    private void UnequipItem(EquipmentDefinition def, PlayerData pd)
    {
        switch (def.Slot)
        {
            case EquipmentSlot.Helmet: if (pd.HelmetId == def.ItemId) pd.HelmetId = ""; break;
            case EquipmentSlot.Armor:  if (pd.ArmorId  == def.ItemId) pd.ArmorId  = ""; break;
            case EquipmentSlot.Shoes:  if (pd.ShoesId  == def.ItemId) pd.ShoesId  = ""; break;
            case EquipmentSlot.Weapon: if (pd.WeaponId == def.ItemId) pd.WeaponId = ""; break;
            case EquipmentSlot.Amulet: if (pd.AmuletId == def.ItemId) pd.AmuletId = ""; break;
            case EquipmentSlot.Ring:   if (pd.RingId   == def.ItemId) pd.RingId   = ""; break;
        }
    }

    private bool IsEquipped(EquipmentDefinition def, PlayerData pd) => def.Slot switch
    {
        EquipmentSlot.Helmet => pd.HelmetId == def.ItemId,
        EquipmentSlot.Armor  => pd.ArmorId  == def.ItemId,
        EquipmentSlot.Shoes  => pd.ShoesId  == def.ItemId,
        EquipmentSlot.Weapon => pd.WeaponId == def.ItemId,
        EquipmentSlot.Amulet => pd.AmuletId == def.ItemId,
        EquipmentSlot.Ring   => pd.RingId   == def.ItemId,
        _                    => false,
    };

    // ── Stat totals ────────────────────────────────────────────────────────

    /// <summary>
    /// Sums base stats and all equipped item bonuses and updates the labels.
    /// This is what the player actually brings into battle.
    /// </summary>
    private void RefreshStatTotals()
    {
        var pd = GameManager.Instance.PlayerData;

        int   hp      = pd.MaxHP;
        float damage  = pd.BaseDamage;
        int   speed   = pd.Speed;
        float defense = pd.Defense;
        float luck    = pd.Luck;

        // Add bonuses from each equipped item
        var ids = new[] { pd.HelmetId, pd.ArmorId, pd.ShoesId, pd.WeaponId, pd.AmuletId, pd.RingId };
        foreach (var id in ids)
        {
            var item = EquipDB?.GetItem(id);
            if (item == null) continue;
            hp      += item.BonusHP;
            damage  += item.BonusDamage;
            speed   += item.BonusSpeed;
            defense += item.BonusDefense;
            luck    += item.BonusLuck;
        }

        if (TotalHPLabel      != null) TotalHPLabel.text      = $"HP:      {hp}";
        if (TotalDamageLabel  != null) TotalDamageLabel.text  = $"Damage:  {damage:0.#}";
        if (TotalSpeedLabel   != null) TotalSpeedLabel.text   = $"Speed:   {speed}";
        if (TotalDefenseLabel != null) TotalDefenseLabel.text = $"Defense: {defense:0.#}";
        if (TotalLuckLabel    != null) TotalLuckLabel.text    = $"Luck:    {luck * 100f:0.#}%";
    }
}
