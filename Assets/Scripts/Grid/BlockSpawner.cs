using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// BlockSpawner manages the three block choices shown at the bottom of the screen.
/// 
/// It picks random blocks from the available pool, displays them in the tray,
/// handles the Mold block insertion (after 3 consecutive clears), and 
/// coordinates the Omni block selector overlay.
///
/// Each displayed block can be dragged from the tray onto the grid.
/// DraggableBlock handles the actual drag; Spawner handles the state.
/// </summary>
public class BlockSpawner : MonoBehaviour
{
    [Header("Scene References")]
    [Tooltip("Drag in the GameObject that has the BlockGrid component. " +
             "Passed down to every DraggableBlock so they never need to " +
             "search the scene for it at runtime.")]
    public BlockGrid Grid;

    [Tooltip("Drag in the GameObject that has the GridDropZone component " +
             "(usually the same object as the Grid Container).")]
    public GridDropZone DropZone;

    [Tooltip("Drag in the GameObject that has the GridRenderer component. " +
             "Used so dragged blocks can show a green/red placement preview. " +
             "Optional — leave empty if you don't want the preview.")]
    public GridRenderer GridRendererRef;

    [Header("Block Pool (assign all BlockDefinition assets)")]
    public List<BlockDefinition> BlockPool;     // all non-mold blocks

    [Header("Special Blocks")]
    public BlockDefinition MoldBlockDefinition; // the special mold block

    [Header("Tray Slots (assign 3 UI slot transforms in Inspector)")]
    public List<Transform> TraySlots;           // 3 parent transforms for block previews

    [Header("Prefabs")]
    public GameObject DraggableBlockPrefab;     // draggable block UI prefab

    [Header("Omni Selector UI")]
    public GameObject OmniSelectorPanel;        // panel shown when Omni power is used

    // ── Runtime state ──────────────────────────────────────────────────────
    /// <summary>The three currently active block definitions in the tray.</summary>
    public List<BlockDefinition> CurrentBlocks { get; private set; } = new();

    private bool _moldQueued = false;  // true when a mold block is waiting to be added

    // ──────────────────────────────────────────────────────────────────────
    private void Start()
    {
        if (OmniSelectorPanel != null)
            OmniSelectorPanel.SetActive(false);
    }

    // ── Public API ─────────────────────────────────────────────────────────

    /// <summary>
    /// Picks 3 random blocks from the pool and populates the tray.
    /// Called at battle start and after a full set of 3 blocks is used.
    /// </summary>
    public void GenerateNewBlocks()
    {
        CurrentBlocks.Clear();
        ClearTray();

        for (int i = 0; i < 3; i++)
        {
            // If a mold block is queued, put it in the first available slot
            BlockDefinition chosen;
            if (_moldQueued && i == 0)
            {
                chosen = MoldBlockDefinition;
                _moldQueued = false;
            }
            else
            {
                chosen = PickRandom();
            }

            CurrentBlocks.Add(chosen);
            SpawnPreviewInSlot(chosen, TraySlots[i], i);
        }
    }

    /// <summary>
    /// Called by BattleManager after 3 consecutive line clears.
    /// Queues a mold block to appear in the next tray refresh.
    /// </summary>
    public void AddMoldBlock()
    {
        _moldQueued = true;
        // Immediately replace first slot if a slot is still available
        // (simplification: add on next generate)
        Debug.Log("[BlockSpawner] Mold block queued for next tray.");
    }

    /// <summary>
    /// Removes a block from the tray by SLOT INDEX after the player places it.
    ///
    /// IMPORTANT: this used to look up the slot via CurrentBlocks.IndexOf(def),
    /// but that breaks whenever two tray slots happen to hold the SAME
    /// BlockDefinition asset (e.g. two Red blocks at once) — IndexOf always
    /// returns the FIRST matching slot, which can clear the wrong one and
    /// leave the block you actually placed still sitting in the tray, fully
    /// draggable. Using the slot index directly (assigned to each
    /// DraggableBlock when it was spawned) makes this unambiguous.
    ///
    /// By design, the tray only refills once ALL THREE slots have been used
    /// (matching classic Block Blast behaviour).
    /// </summary>
    public void ConsumeBlock(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= CurrentBlocks.Count)
        {
            Debug.LogWarning($"[BlockSpawner] ConsumeBlock got an invalid slot index: {slotIndex}");
            return;
        }

        CurrentBlocks[slotIndex] = null;
        ClearSlot(TraySlots[slotIndex]);

        Debug.Log($"[BlockSpawner] Consumed slot {slotIndex}. Remaining: " +
                  $"[{string.Join(", ", CurrentBlocks.Select(b => b == null ? "empty" : b.DisplayName))}]");

        // Refill only once all 3 slots have been used (classic Block Blast rule)
        if (CurrentBlocks.All(b => b == null))
        {
            Debug.Log("[BlockSpawner] All 3 slots used — generating new set.");
            GenerateNewBlocks();
        }
    }

    /// <summary>
    /// Replaces one randomly chosen tray slot with a different random block.
    /// Called by BattleManager when an enemy uses the TransformTrayBlock ability.
    ///
    /// Rules:
    ///   • Only slots that are currently filled (not null) are eligible.
    ///   • The replacement is always a different block than the current one,
    ///     so the player genuinely gets a new shape forced on them.
    ///   • The visual in the tray is rebuilt immediately so the player can see
    ///     the change right away.
    ///
    /// Returns true if a swap was made, false if the tray was empty.
    /// </summary>
    public bool TransformRandomTrayBlock()
    {
        // Collect indices of slots that actually have a block in them
        var filledIndices = new List<int>();
        for (int i = 0; i < CurrentBlocks.Count; i++)
            if (CurrentBlocks[i] != null) filledIndices.Add(i);

        if (filledIndices.Count == 0) return false;

        // Pick a random filled slot to transform
        int targetIdx = filledIndices[Random.Range(0, filledIndices.Count)];
        BlockDefinition current = CurrentBlocks[targetIdx];

        // Build a pool of all blocks that are NOT the current one
        var otherBlocks = BlockPool.Where(b => b != current).ToList();

        // Safety: if the pool has only one block type, just pick anything
        if (otherBlocks.Count == 0) otherBlocks = BlockPool.ToList();
        if (otherBlocks.Count == 0) return false;

        BlockDefinition replacement = otherBlocks[Random.Range(0, otherBlocks.Count)];

        // Swap in the data list
        CurrentBlocks[targetIdx] = replacement;

        // Rebuild the visual for that slot only
        ClearSlot(TraySlots[targetIdx]);
        SpawnPreviewInSlot(replacement, TraySlots[targetIdx], targetIdx);

        Debug.Log($"[BlockSpawner] Slot {targetIdx} transformed: " +
                  $"{current?.DisplayName} → {replacement.DisplayName}");
        return true;
    }

    /// <summary>Opens the Omni block picker panel.</summary>
    public void OpenOmniSelector()
    {
        if (OmniSelectorPanel != null)
            OmniSelectorPanel.SetActive(true);
    }

    /// <summary>Called when the player picks a block in the Omni panel.</summary>
    public void OnOmniBlockSelected(BlockDefinition def)
    {
        if (OmniSelectorPanel != null)
            OmniSelectorPanel.SetActive(false);

        // Replace the first null (used) slot with the chosen block
        for (int i = 0; i < CurrentBlocks.Count; i++)
        {
            if (CurrentBlocks[i] == null)
            {
                CurrentBlocks[i] = def;
                SpawnPreviewInSlot(def, TraySlots[i], i);
                return;
            }
        }
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private BlockDefinition PickRandom()
    {
        if (BlockPool == null || BlockPool.Count == 0) return null;
        return BlockPool[Random.Range(0, BlockPool.Count)];
    }

    private void ClearTray()
    {
        foreach (var slot in TraySlots) ClearSlot(slot);
    }

    private void ClearSlot(Transform slot)
    {
        foreach (Transform child in slot)
            Destroy(child.gameObject);
    }

    private void SpawnPreviewInSlot(BlockDefinition def, Transform slot, int slotIndex)
    {
        if (def == null || DraggableBlockPrefab == null) return;

        var go = Instantiate(DraggableBlockPrefab, slot);
        var draggable = go.GetComponent<DraggableBlock>();

        // Hand the block its data, the scene references it needs, AND which
        // slot index it belongs to — this is what ConsumeBlock uses to
        // remove exactly the right slot, even if two slots share the same
        // BlockDefinition asset.
        if (draggable != null) draggable.Initialise(def, this, Grid, DropZone, slotIndex, GridRendererRef);
    }
}