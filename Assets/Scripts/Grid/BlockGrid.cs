using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// BlockGrid manages the 9×9 game grid.
/// 
/// Responsibilities:
///   • Stores which block type occupies each cell (null = empty).
///   • Validates whether a block shape can be placed at a given position.
///   • Places blocks and then checks for completed rows/columns.
///   • Fires events so BattleManager can react (deal damage, apply effects).
/// </summary>
public class BlockGrid : MonoBehaviour
{
    // ── Configuration ──────────────────────────────────────────────────────
    [Header("Grid Size")]
    public int Columns = 9;
    public int Rows = 9;

    // ── Events (BattleManager listens to these) ────────────────────────────
    /// <summary>Fired after lines are cleared. Payload = clear result data.</summary>
    public event Action<ClearResult> OnLinesCleared;

    /// <summary>Fired after every placement (including zero clears).</summary>
    public event Action OnBlockPlaced;

    // ── Internal state ─────────────────────────────────────────────────────
    // grid[col, row] = the BlockType occupying that cell, or null if empty
    private BlockType?[,] _grid;

    // ── Unity lifecycle ────────────────────────────────────────────────────
    private void Awake()
    {
        _grid = new BlockType?[Columns, Rows];
    }

    // ── Public API ─────────────────────────────────────────────────────────

    /// <summary>
    /// Returns true if the given shape can be placed with its top-left corner
    /// at (originCol, originRow) — i.e. all cells are in-bounds and empty.
    /// </summary>
    public bool CanPlace(Vector2Int[] shape, int originCol, int originRow)
    {
        foreach (var offset in shape)
        {
            int c = originCol + offset.x;
            int r = originRow + offset.y;

            if (c < 0 || c >= Columns || r < 0 || r >= Rows) return false;
            if (_grid[c, r] != null) return false;
        }
        return true;
    }

    /// <summary>
    /// Places a block on the grid using its DEFAULT (unrotated) shape from
    /// the BlockDefinition asset, and immediately checks for completed lines.
    /// Returns a ClearResult describing what was cleared (may be empty).
    ///
    /// Call this from BattleManager when the player drops a block with no
    /// rotation applied. If rotation is in use (see DraggableBlock's
    /// AllowRandomRotation), call the PlaceBlock(def, shape, col, row)
    /// overload below instead, passing the actual rotated shape.
    /// </summary>
    public ClearResult PlaceBlock(BlockDefinition def, int originCol, int originRow)
        => PlaceBlock(def, def.Shape, originCol, originRow);

    /// <summary>
    /// Places a block on the grid using an EXPLICIT shape (which may be a
    /// rotated version of def.Shape) rather than always reading def.Shape
    /// directly. This is what makes random block rotation actually affect
    /// the grid data, not just the visual preview.
    /// </summary>
    /// <param name="def">Which block type/colour to write into the grid cells.</param>
    /// <param name="shape">The actual shape offsets to use — pass a rotated
    /// shape here if the block was spawned with rotation applied.</param>
    public ClearResult PlaceBlock(BlockDefinition def, Vector2Int[] shape, int originCol, int originRow)
    {
        // 1. Write each cell of the shape into the grid
        foreach (var offset in shape)
        {
            int c = originCol + offset.x;
            int r = originRow + offset.y;
            _grid[c, r] = def.BlockType;
        }

        // 2. Check which rows and columns are now full
        var result = CheckAndClearLines(def);

        // 3. Notify listeners
        OnBlockPlaced?.Invoke();
        if (result.TotalLinesCleared > 0)
            OnLinesCleared?.Invoke(result);

        return result;
    }

    /// <summary>
    /// Special Mold block placement: fills all EMPTY cells inside the 3×3 area
    /// at (originCol, originRow).  Existing blocks are preserved.
    /// </summary>
    public ClearResult PlaceMoldBlock(BlockDefinition moldDef, int originCol, int originRow)
    {
        for (int dc = 0; dc < 3; dc++)
            for (int dr = 0; dr < 3; dr++)
            {
                int c = originCol + dc;
                int r = originRow + dr;
                if (c < 0 || c >= Columns || r < 0 || r >= Rows) continue;
                if (_grid[c, r] == null)                          // only fill empty
                    _grid[c, r] = moldDef.BlockType;
            }

        var result = CheckAndClearLines(moldDef);
        OnBlockPlaced?.Invoke();
        if (result.TotalLinesCleared > 0)
            OnLinesCleared?.Invoke(result);

        return result;
    }

    /// <summary>
    /// Returns true if there is no valid placement for ANY of the given shapes.
    ///
    /// IMPORTANT: availableBlocks (BlockSpawner.CurrentBlocks) can legitimately
    /// contain null entries — a tray slot becomes null the moment its block is
    /// placed, and stays null until all three slots are empty and a fresh set
    /// is generated. We must skip null entries here instead of dereferencing
    /// them, otherwise this throws a NullReferenceException as soon as one
    /// slot (but not all three) has been used.
    /// </summary>
    public bool IsGridDeadlocked(List<BlockDefinition> availableBlocks)
    {
        foreach (var def in availableBlocks)
        {
            if (def == null) continue;   // empty tray slot — nothing to check

            for (int c = 0; c < Columns; c++)
                for (int r = 0; r < Rows; r++)
                {
                    if (CanPlace(def.Shape, c, r)) return false;
                }
        }
        return true; // no block can fit anywhere = game over
    }

    /// <summary>Returns a copy of the grid state (useful for UI rendering).</summary>
    public BlockType?[,] GetGridSnapshot() => (BlockType?[,])_grid.Clone();

    // ── Enemy ability helpers ──────────────────────────────────────────────

    /// <summary>
    /// Forces a single cell to contain the given BlockType, regardless of
    /// whether it is already occupied.  Used by the enemy PlaceJunkBlock
    /// ability so the enemy can always drop junk onto an empty cell.
    ///
    /// IMPORTANT: this does NOT fire OnBlockPlaced or check for line clears.
    /// BattleManager calls this directly and handles any follow-up logic itself.
    /// If you want line-clear checks after placing junk, call CheckAndClearLinesPublic()
    /// afterwards (see below).
    /// </summary>
    /// <param name="col">Column index (0 = left).</param>
    /// <param name="row">Row index (0 = top).</param>
    /// <param name="type">The BlockType to write into the cell.</param>
    public void ForceSetCell(int col, int row, BlockType type)
    {
        if (col < 0 || col >= Columns || row < 0 || row >= Rows)
        {
            Debug.LogWarning($"[BlockGrid] ForceSetCell out of bounds: ({col},{row})");
            return;
        }

        _grid[col, row] = type;

        // Notify GridRenderer that the visual needs updating.
        // We reuse OnBlockPlaced since it just means "grid state changed".
        OnBlockPlaced?.Invoke();
    }

    /// <summary>
    /// Removes the block at a single cell, making it empty.
    /// Used by the enemy RemovePlacedBlock ability.
    ///
    /// Like ForceSetCell this does NOT trigger line-clear checks — removing
    /// a block can never complete a line, so that is intentional.
    /// Does nothing if the cell is already empty or out of bounds.
    /// </summary>
    /// <param name="col">Column index.</param>
    /// <param name="row">Row index.</param>
    public void ClearCell(int col, int row)
    {
        if (col < 0 || col >= Columns || row < 0 || row >= Rows)
        {
            Debug.LogWarning($"[BlockGrid] ClearCell out of bounds: ({col},{row})");
            return;
        }

        _grid[col, row] = null;

        // Notify GridRenderer so the visual cell goes back to its empty colour.
        OnBlockPlaced?.Invoke();
    }

    // ── Private helpers ────────────────────────────────────────────────────

    /// <summary>
    /// Scans every row and column.  Any that are completely full get added
    /// to the clear list, their cells are erased, and a ClearResult is built.
    /// </summary>
    private ClearResult CheckAndClearLines(BlockDefinition lastPlaced)
    {
        var result = new ClearResult();

        // --- Check rows ---
        for (int r = 0; r < Rows; r++)
        {
            if (IsRowFull(r))
            {
                result.ClearedRows.Add(r);
                var types = CollectRowTypes(r);
                result.BlockTypesClearedInRows.AddRange(types);
                ClearRow(r);
            }
        }

        // --- Check columns ---
        for (int c = 0; c < Columns; c++)
        {
            if (IsColFull(c))
            {
                result.ClearedCols.Add(c);
                var types = CollectColTypes(c);
                result.BlockTypesClearedInCols.AddRange(types);
                ClearCol(c);
            }
        }

        result.WasFullClear = result.ClearedRows.Count > 0 && result.ClearedCols.Count > 0;
        return result;
    }

    private bool IsRowFull(int row)
    {
        for (int c = 0; c < Columns; c++)
            if (_grid[c, row] == null) return false;
        return true;
    }

    private bool IsColFull(int col)
    {
        for (int r = 0; r < Rows; r++)
            if (_grid[col, r] == null) return false;
        return true;
    }

    private List<BlockType> CollectRowTypes(int row)
    {
        var list = new List<BlockType>();
        for (int c = 0; c < Columns; c++)
            if (_grid[c, row].HasValue) list.Add(_grid[c, row].Value);
        return list;
    }

    private List<BlockType> CollectColTypes(int col)
    {
        var list = new List<BlockType>();
        for (int r = 0; r < Rows; r++)
            if (_grid[col, r].HasValue) list.Add(_grid[col, r].Value);
        return list;
    }

    private void ClearRow(int row)
    {
        for (int c = 0; c < Columns; c++) _grid[c, row] = null;
    }

    private void ClearCol(int col)
    {
        for (int r = 0; r < Rows; r++) _grid[col, r] = null;
    }
}

// ── Data structures ────────────────────────────────────────────────────────

/// <summary>
/// Returned by PlaceBlock / PlaceMoldBlock describing everything that was cleared.
/// BattleManager reads this to calculate damage and apply effects.
/// </summary>
[Serializable]
public class ClearResult
{
    public List<int> ClearedRows = new();
    public List<int> ClearedCols = new();

    /// <summary>All block types found in cleared rows (may contain duplicates).</summary>
    public List<BlockType> BlockTypesClearedInRows = new();

    /// <summary>All block types found in cleared columns (may contain duplicates).</summary>
    public List<BlockType> BlockTypesClearedInCols = new();

    /// <summary>True when at least one row AND one column were cleared simultaneously.</summary>
    public bool WasFullClear;

    public int TotalLinesCleared => ClearedRows.Count + ClearedCols.Count;
}