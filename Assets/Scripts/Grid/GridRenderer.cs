using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// GridRenderer draws the visual grid on screen using Unity UI Image components.
///
/// It listens to BlockGrid for state changes and redraws accordingly.
///
/// PLACEMENT PREVIEW — while a block is being dragged,
/// DraggableBlock calls ShowPreview() every frame with the cells the block
/// would occupy if dropped right now, plus whether that placement is valid.
/// Valid cells are tinted green, invalid cells are tinted red. ClearPreview()
/// restores the affected cells back to their real grid appearance.
///
/// The GridLayoutGroup handles all the cell positioning automatically —
/// Set cell size and spacing in the Inspector.
/// </summary>
public class GridRenderer : MonoBehaviour
{
    [Header("References")]
    public BlockGrid Grid;             // the logic grid
    public RectTransform GridContainer;    // parent with GridLayoutGroup
    public GameObject CellPrefab;       // prefab with Image component

    [Header("Block Definitions (index must match BlockType int value)")]
    public BlockDefinition[] BlockDefinitions; // drag all your block assets here

    [Header("Placement Preview Colours")]
    [Tooltip("Tint applied to cells the dragged block WOULD occupy if it fits here.")]
    public Color ValidPreviewColor = new Color(0.2f, 1f, 0.2f, 0.6f);

    [Tooltip("Tint applied to cells the dragged block would occupy if it does NOT fit here.")]
    public Color InvalidPreviewColor = new Color(1f, 0.2f, 0.2f, 0.6f);

    // Internal pool of cell Image components
    private Image[,] _cellImages;

    // Cells currently showing a preview tint, so we know what to clear
    // before applying a new preview or restoring real grid colours.
    private readonly List<Vector2Int> _previewedCells = new();

    private void Start()
    {
        InitialiseCells();
        // Refresh the visual whenever the grid changes
        Grid.OnBlockPlaced += RefreshDisplay;
        Grid.OnLinesCleared += _ => RefreshDisplay();
    }

    /// <summary>
    /// Creates one UI Image per grid cell, arranged by GridLayoutGroup.
    ///
    /// CONVENTION: row 0 = TOP of the grid, increasing row = downward.
    /// This matches GridDropZone's pointer-to-cell math and DraggableBlock's
    /// shape rendering, so all three systems agree on "up."
    ///
    /// GridLayoutGroup always places the FIRST child it receives in the
    /// top-left corner and fills left-to-right, then top-to-bottom. So to
    /// get row 0 at the top, we must instantiate row 0's cells FIRST.
    /// </summary>
    private void InitialiseCells()
    {
        int cols = Grid.Columns;
        int rows = Grid.Rows;
        _cellImages = new Image[cols, rows];

        // Row 0 first (top), increasing row goes down — matches GridDropZone
        // and DraggableBlock's shape offset convention.
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
            {
                var go = Instantiate(CellPrefab, GridContainer);
                var img = go.GetComponent<Image>();
                _cellImages[c, r] = img;
                img.color = new Color(0.2f, 0.2f, 0.2f, 0.5f); // empty cell tint
            }
    }

    /// <summary>
    /// Reads the current grid snapshot and updates every cell's colour/sprite.
    /// Called after every block placement or line clear.
    ///
    /// Any active preview is cleared first — otherwise a placement that
    /// happens to land on a still-previewed cell could leave a stale
    /// green/red tint behind instead of the real placed-block colour.
    /// </summary>
    public void RefreshDisplay()
    {
        ClearPreview();

        var snapshot = Grid.GetGridSnapshot();

        for (int c = 0; c < Grid.Columns; c++)
            for (int r = 0; r < Grid.Rows; r++)
            {
                ApplyRealCellColor(c, r, snapshot);
            }
    }

    // ── Placement preview ────────────────────────────────────────────────

    /// <summary>
    /// Highlights the given grid cells to show where a dragged block would
    /// land if dropped right now. Call this every frame while dragging
    /// (e.g. from DraggableBlock.OnDrag), passing the cells the shape would
    /// occupy and whether that placement is currently valid.
    ///
    /// Cells outside the grid bounds are silently ignored (this can happen
    /// while the pointer is near an edge), so it is always safe to call.
    /// </summary>
    /// <param name="cells">Grid cells the block would occupy if dropped now.</param>
    /// <param name="isValid">Whether BlockGrid.CanPlace() returned true for this position.</param>
    public void ShowPreview(List<Vector2Int> cells, bool isValid)
    {
        // Clear whatever was previewed last frame before applying the new one.
        // This naturally handles the block moving to a different position,
        // a different rotation/shape, or leaving the grid entirely.
        ClearPreview();

        Color tint = isValid ? ValidPreviewColor : InvalidPreviewColor;

        foreach (var cell in cells)
        {
            if (cell.x < 0 || cell.x >= Grid.Columns) continue;
            if (cell.y < 0 || cell.y >= Grid.Rows) continue;

            _cellImages[cell.x, cell.y].color = tint;
            _previewedCells.Add(cell);
        }
    }

    /// <summary>
    /// Removes any active placement preview, restoring previously-highlighted
    /// cells back to their real grid appearance (empty grey, or the actual
    /// placed block's colour if something is already there).
    ///
    /// Call this when a drag ends (whether placed or cancelled) and at the
    /// start of every RefreshDisplay() call for safety.
    /// </summary>
    public void ClearPreview()
    {
        if (_previewedCells.Count == 0) return;

        var snapshot = Grid.GetGridSnapshot();
        foreach (var cell in _previewedCells)
            ApplyRealCellColor(cell.x, cell.y, snapshot);

        _previewedCells.Clear();
    }

    /// <summary>
    /// Sets one cell's Image back to whatever the real grid data says it should be.
    ///
    /// IMPORTANT: we look up the BlockDefinition by matching its BlockType field
    /// rather than using the enum integer value as a direct array index. The
    /// index approach silently breaks if the BlockDefinitions array in the
    /// Inspector is filled in a different order than the enum values, causing
    /// placed blocks to show the wrong color. Matching by BlockType is order-
    /// independent and works correctly regardless of how the array is arranged.
    /// </summary>
    private void ApplyRealCellColor(int c, int r, BlockType?[,] snapshot)
    {
        var img = _cellImages[c, r];
        if (snapshot[c, r].HasValue)
        {
            BlockType type = snapshot[c, r].Value;

            // Find the definition whose BlockType field matches — safe regardless
            // of the order the assets were dragged into the Inspector array.
            BlockDefinition def = null;
            foreach (var d in BlockDefinitions)
            {
                if (d != null && d.BlockType == type) { def = d; break; }
            }

            if (def != null)
            {
                img.color = def.BlockColor;
                img.sprite = def.BlockSprite;
            }
            else
            {
                // No matching definition found — show a warning color so the
                // missing asset is obvious rather than silently invisible.
                img.color = Color.magenta;
                img.sprite = null;
                Debug.LogWarning($"[GridRenderer] No BlockDefinition found for BlockType.{type}. " +
                                  "Make sure a BlockDefinition asset with that BlockType is in " +
                                  "the BlockDefinitions array on GridRenderer.");
            }
        }
        else
        {
            img.color = new Color(0.2f, 0.2f, 0.2f, 0.5f);
            img.sprite = null;
        }
    }

    private void OnDestroy()
    {
        if (Grid == null) return;
        Grid.OnBlockPlaced -= RefreshDisplay;
        Grid.OnLinesCleared -= _ => RefreshDisplay();
    }
}