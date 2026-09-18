using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// EquipmentDatabase is a ScriptableObject that holds every EquipmentDefinition
/// in the game. The EquipmentScreen looks up owned gear by ID here, exactly the
/// same way AbilityCardDatabase works for ability cards.
///
/// HOW TO CREATE:
///   Right-click in Project → Create → TileSpark → Equipment Database
/// </summary>
[CreateAssetMenu(fileName = "EquipmentDatabase", menuName = "TileSpark/Equipment Database")]
public class EquipmentDatabase : ScriptableObject
{
    public List<EquipmentDefinition> Items = new();

    /// <summary>Returns the equipment with the given ID, or null if not found.</summary>
    public EquipmentDefinition GetItem(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        return Items.Find(i => i.ItemId == id);
    }

    /// <summary>Returns all items that belong to a specific slot.</summary>
    public List<EquipmentDefinition> GetBySlot(EquipmentSlot slot)
        => Items.FindAll(i => i.Slot == slot);
}
