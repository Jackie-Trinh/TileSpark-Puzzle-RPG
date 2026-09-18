using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// DraggableBlock handles the drag-and-drop interaction for placing blocks,
/// AND builds the correct multi-cell visual shape from BlockDefinition.Shape.
///
/// ── HOW THE SHAPE IS DRAWN ────────────────────────────────────────────────
/// Each BlockDefinition.Shape entry is a Vector2Int (column, row) offset
/// relative to the block's pivot cell (0,0). On Initialise(), we spawn one
/// small Image per shape cell as a child of this object, positioned using
/// CellSize so the whole multi-cell shape renders correctly
///
/// ── HOW PLACEMENT WORKS ────────────
/// On OnBeginDrag we record WHICH shape-cell the player grabbed
/// (GetNearestShapeOffset). On drop, GridDropZone tells us which grid cell
/// the pointer is over, and we subtract the grabbed cell's offset to work
/// backwards to the shape's true (0,0) origin before calling CanPlace/PlaceBlock.
/// </summary>
public class DraggableBlock : MonoBehaviour,
    IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("Shape Rendering")]
    [Tooltip("Pixel size of one grid cell in the tray preview. Should match " +
             "(roughly) the size used by the GridLayoutGroup on the real grid " +
             "so the dragged shape looks correctly proportioned.")]
    public float CellSize = 50f;

    [Tooltip("Pixel gap between cells, purely visual.")]
    public float CellSpacing = 2f;

    [Tooltip("Prefab for ONE shape cell — just needs an Image component. " +
             "If left empty, a plain Image is created in code instead.")]
    public GameObject CellPrefab;

    [Header("Random Rotation")]
    [Tooltip("If true, each block spawns with a random 0/90/180/270 degree " +
             "rotation applied to its shape, adding placement variety. " +
             "The tray preview and the actual grid placement both use the " +
             "SAME rotated shape, so what you see is exactly what gets placed.")]
    public bool AllowRandomRotation = true;

    // ── Set by BlockSpawner.SpawnPreviewInSlot() ───────────────────────────
    public BlockDefinition Definition { get; private set; }
    private BlockSpawner _spawner;

    /// <summary>
    /// The shape actually used for both rendering and placement, AFTER any
    /// random rotation has been applied. Everything in this script should
    /// read from RotatedShape instead of Definition.Shape directly, so the
    /// visual and the placement logic can never disagree.
    /// </summary>
    private Vector2Int[] RotatedShape { get; set; }

    /// <summary>
    /// Which tray slot (0, 1, or 2) this block belongs to. Used instead of
    /// matching by BlockDefinition reference when calling ConsumeBlock,
    /// since two slots can hold the same block type at once.
    /// </summary>
    private int _slotIndex;

    // ── Scene references (handed in by BlockSpawner, never searched for) ──
    private BlockGrid _grid;
    private GridDropZone _dropZone;

    [Tooltip("Used to show a green/red preview of where this block would " +
             "land while dragging. Optional — if left unassigned, no preview " +
             "is shown but placement still works normally.")]
    private GridRenderer _gridRenderer;

    // ── Drag state ────────────────────────────────────────────────────────
    private CanvasGroup _canvasGroup;
    private RectTransform _rectTransform;
    private Transform _originalParent;
    private Vector3 _originalPosition;
    private Canvas _rootCanvas;
    private LayoutElement _layoutElement;   // used to opt out of parent Layout Groups

    // ──────────────────────────────────────────────────────────────────────
    private void Awake()
    {
        _canvasGroup = GetComponent<CanvasGroup>();
        _rectTransform = GetComponent<RectTransform>();

        // IMPORTANT: GetComponentInParent<Canvas>() only finds the NEAREST
        // Canvas in the parent chain. If any UI panel between this object
        // and the screen root has its own Canvas component (common when a
        // panel needs its own sort order), reparenting onto that nearest
        // Canvas during drag would attach this block to the WRONG part of
        // the hierarchy — which can visually drag the whole layout along
        // with it. We explicitly walk up to find the OUTERMOST root Canvas
        // (the one with isRootCanvas == true) instead.
        _rootCanvas = FindRootCanvas();

        // Safety: if the prefab is missing a CanvasGroup, add one so drag
        // never throws a NullReferenceException.
        if (_canvasGroup == null)
            _canvasGroup = gameObject.AddComponent<CanvasGroup>();

        // CRITICAL: tell any parent Layout Group (Horizontal/Vertical/Grid)
        // to completely ignore this object. Without this, the tray's layout
        // group fights our manual drag positioning every frame, producing
        // a misshapen "blob" that lags behind the cursor.
        _layoutElement = GetComponent<LayoutElement>();
        if (_layoutElement == null)
            _layoutElement = gameObject.AddComponent<LayoutElement>();
        _layoutElement.ignoreLayout = true;
    }

    /// <summary>
    /// Walks up the parent chain and returns the OUTERMOST Canvas — the
    /// true screen-level root Canvas — rather than the nearest one.
    /// A Canvas's `isRootCanvas` property is true only when it has no
    /// Canvas ancestor of its own.
    /// </summary>
    private Canvas FindRootCanvas()
    {
        var canvases = GetComponentsInParent<Canvas>();
        foreach (var c in canvases)
        {
            if (c.isRootCanvas) return c;
        }

        // Fallback: if for some reason none report isRootCanvas (rare),
        // use the last one found, which is the outermost in the array.
        return canvases.Length > 0 ? canvases[canvases.Length - 1] : null;
    }

    /// <summary>
    /// Called by BlockSpawner right after instantiation. Builds the visual
    /// shape from def.Shape and stores the scene references needed for
    /// placement math later.
    /// </summary>
    /// <param name="slotIndex">Which tray slot (0-2) this block belongs to —
    /// passed back to BlockSpawner.ConsumeBlock() on successful placement
    /// so the correct slot is cleared even if duplicate block types exist.</param>
    /// <param name="gridRenderer">Used to show the green/red placement preview
    /// while dragging.</param>
    public void Initialise(BlockDefinition def, BlockSpawner spawner,
                            BlockGrid grid, GridDropZone dropZone, int slotIndex,
                            GridRenderer gridRenderer = null)
    {
        Definition = def;
        _spawner = spawner;
        _grid = grid;
        _dropZone = dropZone;
        _slotIndex = slotIndex;
        _gridRenderer = gridRenderer;

        bool isMold = def.BlockType == BlockType.Mold;

        if (isMold)
        {
            // The Mold block intentionally has NO Shape array in its
            // BlockDefinition asset, because its actual placement logic
            // (BlockGrid.PlaceMoldBlock) always fills a fixed 3x3 box and
            // skips cells that are already occupied — it isn't a normal
            // fixed-shape block.
            //
            // However, BuildShapeVisual(), GetNearestShapeOffset(), and the
            // live placement preview all need SOME shape to work from, or
            // the block renders as nothing and can't be dragged. So we
            // generate a synthetic solid 3x3 shape purely for rendering and
            // grab-offset purposes. This is never used for the actual grid
            // write — TryPlaceAtPointer still calls PlaceMoldBlock(), which
            // has its own fill-only-empty-cells logic.
            RotatedShape = Build3x3Shape();
        }
        else if (AllowRandomRotation)
        {
            int steps = Random.Range(0, 4);   // 0,1,2,3 = 0°,90°,180°,270°
            RotatedShape = RotateShape(def.Shape, steps);
        }
        else
        {
            RotatedShape = def.Shape;
        }

        BuildShapeVisual();
    }

    /// <summary>Generates a solid 3×3 block of offsets, used only for the Mold block's visual.</summary>
    private Vector2Int[] Build3x3Shape()
    {
        var cells = new Vector2Int[9];
        int i = 0;
        for (int dc = 0; dc < 3; dc++)
            for (int dr = 0; dr < 3; dr++)
                cells[i++] = new Vector2Int(dc, dr);
        return cells;
    }

    /// <summary>
    /// Rotates a Shape array by 90° increments and normalises the result so
    /// the smallest column/row offset is always 0 — this keeps the rotated
    /// shape anchored at (0,0) just like an unrotated one, which is required
    /// for BlockGrid.CanPlace / PlaceBlock to treat the origin consistently.
    ///
    /// Rotation math (90° clockwise, since row increases DOWNWARD in our
    /// convention): (col, row) → (-row, col)
    /// Applying this `steps` times gives 90°, 180°, or 270° rotations.
    /// </summary>
    private Vector2Int[] RotateShape(Vector2Int[] shape, int steps)
    {
        if (shape == null || shape.Length == 0) return shape;

        var rotated = new Vector2Int[shape.Length];
        for (int i = 0; i < shape.Length; i++)
            rotated[i] = shape[i];

        for (int s = 0; s < steps; s++)
        {
            for (int i = 0; i < rotated.Length; i++)
            {
                var p = rotated[i];
                rotated[i] = new Vector2Int(-p.y, p.x);   // 90° clockwise
            }
        }

        // Normalise so the minimum col/row is 0 again — rotation can push
        // offsets negative, and BlockGrid always expects offsets starting
        // from (0,0) relative to the placement origin.
        int minX = int.MaxValue, minY = int.MaxValue;
        foreach (var p in rotated)
        {
            minX = Mathf.Min(minX, p.x);
            minY = Mathf.Min(minY, p.y);
        }
        for (int i = 0; i < rotated.Length; i++)
            rotated[i] = new Vector2Int(rotated[i].x - minX, rotated[i].y - minY);

        return rotated;
    }

    // ── Shape building ──────────────────────────────────────────────────────

    /// <summary>
    /// Destroys any previous shape cells, then spawns one Image per entry in
    /// RotatedShape, positioned so the whole multi-cell shape is visible.
    ///
    /// Coordinate system: Shape offsets are (column, row) where row increases
    /// DOWNWARD to match BlockGrid's convention (row 0 = top). We convert
    /// that into UI anchored position, where +Y is UP, so row must be
    /// negated when placed.
    /// </summary>
    private void BuildShapeVisual()
    {
        // Clear out any old cell visuals (e.g. if Initialise is ever re-called)
        foreach (Transform child in transform)
            Destroy(child.gameObject);

        if (RotatedShape == null || RotatedShape.Length == 0)
        {
            Debug.LogWarning($"[DraggableBlock] '{Definition.DisplayName}' has an empty Shape array!");
            return;
        }

        float step = CellSize + CellSpacing;

        foreach (var offset in RotatedShape)
        {
            GameObject cellGO = CellPrefab != null
                ? Instantiate(CellPrefab, transform)
                : CreatePlainImageCell();

            cellGO.transform.SetParent(transform, worldPositionStays: false);

            var cellRect = cellGO.GetComponent<RectTransform>();
            cellRect.sizeDelta = new Vector2(CellSize, CellSize);
            // offset.x = column → moves right.  offset.y = row → moves DOWN,
            // so we negate it for UI space where Y is up.
            cellRect.anchoredPosition = new Vector2(offset.x * step, -offset.y * step);

            var img = cellGO.GetComponent<Image>();
            if (img != null)
            {
                img.sprite = Definition.BlockSprite;
                img.color = Definition.BlockColor;
            }
        }

        // Resize the root RectTransform so the whole shape sits centred and
        // the drag-handle area covers all cells, not just a 1x1 square.
        FitRootToShape(step);
    }

    /// <summary>Creates a minimal GameObject with just an Image, used when no CellPrefab is assigned.</summary>
    private GameObject CreatePlainImageCell()
    {
        var go = new GameObject("ShapeCell", typeof(RectTransform), typeof(Image));
        return go;
    }

    /// <summary>
    /// Expands this object's own RectTransform to bound all generated cells,
    /// so raycasts (for dragging) work anywhere over the visible shape,
    /// not just near the pivot.
    /// </summary>
    private void FitRootToShape(float step)
    {
        int minX = 0, maxX = 0, minY = 0, maxY = 0;
        foreach (var offset in RotatedShape)
        {
            minX = Mathf.Min(minX, offset.x);
            maxX = Mathf.Max(maxX, offset.x);
            minY = Mathf.Min(minY, offset.y);
            maxY = Mathf.Max(maxY, offset.y);
        }

        float width = (maxX - minX + 1) * step;
        float height = (maxY - minY + 1) * step;
        _rectTransform.sizeDelta = new Vector2(width, height);
    }

    // ── Grab offset tracking ─────────────────────────────────────────────
    // Which shape-cell index the player's pointer was closest to when the
    // drag began. We use this so dropping correctly accounts for WHERE
    // within the shape the player grabbed it — not just the pivot.
    private Vector2Int _grabbedCellOffset = Vector2Int.zero;

    // ── Drag handlers ──────────────────────────────────────────────────────

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (_rootCanvas == null)
        {
            Debug.LogError("[DraggableBlock] No root Canvas found in parent hierarchy! " +
                            "Dragging cannot work without one.", this);
            return;
        }

        _originalParent = transform.parent;
        _originalPosition = transform.position;

        // Figure out which shape-cell (by Shape[] offset) is nearest to
        // where the player pressed down, in this object's LOCAL space.
        _grabbedCellOffset = GetNearestShapeOffset(eventData.position);

        // Move to the TRUE root canvas so it renders above the tray and the
        // grid, and so dragging this object can never affect any other
        // panel's layout or position.
        transform.SetParent(_rootCanvas.transform);
        transform.SetAsLastSibling();

        // Let raycasts pass through this object while dragging so
        // GridDropZone can correctly detect what's underneath the pointer.
        _canvasGroup.blocksRaycasts = false;
        _canvasGroup.alpha = 0.75f;
    }

    public void OnDrag(PointerEventData eventData)
    {
        _rectTransform.anchoredPosition += eventData.delta / _rootCanvas.scaleFactor;

        UpdatePreview(eventData.position);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        _canvasGroup.blocksRaycasts = true;
        _canvasGroup.alpha = 1f;

        // Always clear the preview on drop, whether it succeeds or not —
        // either the real grid colours take over (placed) or the cells
        // just go back to normal (cancelled).
        _gridRenderer?.ClearPreview();

        bool placed = TryPlaceAtPointer(eventData.position);

        if (!placed)
        {
            // Invalid drop — snap back to tray
            transform.SetParent(_originalParent);
            transform.position = _originalPosition;
        }
    }

    /// <summary>
    /// Computes where this block WOULD land if dropped at the given screen
    /// position right now, and asks GridRenderer to highlight those cells
    /// green (valid) or red (invalid). Does nothing if no GridRenderer was
    /// provided, or if the pointer isn't currently over the grid at all —
    /// in that case any previous preview is cleared instead.
    /// </summary>
    private void UpdatePreview(Vector2 screenPos)
    {
        if (_gridRenderer == null || _dropZone == null) return;

        if (!_dropZone.TryGetCellFromPointer(screenPos, out int pointerCol, out int pointerRow))
        {
            // Pointer left the grid entirely — nothing to preview right now
            _gridRenderer.ClearPreview();
            return;
        }

        bool isMold = Definition.BlockType == BlockType.Mold;

        List<Vector2Int> cells;
        bool isValid;

        if (isMold)
        {
            // Mold always fills a fixed 3x3 box starting at the pointer cell.
            // It is always "valid" to drop (it only fills empty cells inside
            // the box and skips ones already occupied), so we show it as
            // valid and just preview the 3x3 footprint.
            cells = GetMoldPreviewCells(pointerCol, pointerRow);
            isValid = true;
        }
        else
        {
            int originCol = pointerCol - _grabbedCellOffset.x;
            int originRow = pointerRow - _grabbedCellOffset.y;

            cells = GetShapeCells(RotatedShape, originCol, originRow);
            isValid = _grid.CanPlace(RotatedShape, originCol, originRow);
        }

        _gridRenderer.ShowPreview(cells, isValid);
    }

    /// <summary>Converts a Shape array + origin into the list of absolute grid cells it covers.</summary>
    private List<Vector2Int> GetShapeCells(Vector2Int[] shape, int originCol, int originRow)
    {
        var cells = new List<Vector2Int>(shape.Length);
        foreach (var offset in shape)
            cells.Add(new Vector2Int(originCol + offset.x, originRow + offset.y));
        return cells;
    }

    /// <summary>Returns the 9 cells of a 3×3 box starting at (originCol, originRow).</summary>
    private List<Vector2Int> GetMoldPreviewCells(int originCol, int originRow)
    {
        var cells = new List<Vector2Int>(9);
        for (int dc = 0; dc < 3; dc++)
            for (int dr = 0; dr < 3; dr++)
                cells.Add(new Vector2Int(originCol + dc, originRow + dr));
        return cells;
    }

    // ── Placement math (this is the actual bug fix) ─────────────────────────

    /// <summary>
    /// Finds which Shape[] entry is visually closest to where the player
    /// pressed down, by converting the press position into this object's
    /// local space and comparing against each cell's anchored position.
    ///
    /// This is what lets us correctly compute the placement origin even
    /// when the player grabs the block somewhere other than its pivot
    /// cell (e.g. the bottom-right corner of an L-shape).
    /// </summary>
    private Vector2Int GetNearestShapeOffset(Vector2 screenPos)
    {
        if (RotatedShape == null || RotatedShape.Length == 0)
            return Vector2Int.zero;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _rectTransform, screenPos, null, out Vector2 localPoint);

        float step = CellSize + CellSpacing;

        Vector2Int closest = RotatedShape[0];
        float bestDist = float.MaxValue;

        foreach (var offset in RotatedShape)
        {
            // Same conversion used in BuildShapeVisual: row is negated for UI space.
            Vector2 cellPos = new Vector2(offset.x * step, -offset.y * step);
            float dist = Vector2.Distance(localPoint, cellPos);

            if (dist < bestDist)
            {
                bestDist = dist;
                closest = offset;
            }
        }

        return closest;
    }

    /// <summary>
    /// Converts the pointer's screen position into a grid cell, then
    /// corrects for the grab offset recorded in OnBeginDrag so the shape's
    /// TRUE origin (its Shape[] pivot at offset (0,0)) is what gets passed
    /// into BlockGrid.CanPlace / PlaceBlock — regardless of which part of
    /// the shape the player actually grabbed.
    ///
    /// Example: an L-shape with Shape = {(0,0),(0,1),(0,2),(1,2)}. If the
    /// player grabs the (1,2) cell and drops it on grid cell (5,5), the true
    /// origin is (5,5) - (1,2) = (4,3) — that's where BlockGrid needs to
    /// check/write from so the WHOLE shape lands under the cursor correctly.
    /// </summary>
    private bool TryPlaceAtPointer(Vector2 screenPos)
    {
        if (_dropZone == null || _grid == null) return false;

        if (!_dropZone.TryGetCellFromPointer(screenPos, out int pointerCol, out int pointerRow))
            return false; // pointer was outside the grid entirely

        bool isMold = Definition.BlockType == BlockType.Mold;

        // IMPORTANT ORDERING NOTE:
        // BlockGrid.PlaceBlock() / PlaceMoldBlock() fire the OnBlockPlaced
        // event SYNCHRONOUSLY, which BattleManager listens to in order to
        // check for a deadlock (no playable moves left). If we call
        // ConsumeBlock() AFTER PlaceBlock(), BattleManager's deadlock check
        // runs against STALE tray data — it would still see the block we
        // are in the middle of placing, as if it were still available.
        // We must remove the block from the tray FIRST, so by the time the
        // grid event fires, Spawner.CurrentBlocks already reflects reality
        // (including any fresh refill if this was the 3rd block used).

        if (isMold)
        {
            _spawner.ConsumeBlock(_slotIndex);
            _grid.PlaceMoldBlock(Definition, pointerCol, pointerRow);
            Destroy(gameObject);
            return true;
        }

        // Subtract the grabbed cell's offset to find the shape's true (0,0) origin
        int originCol = pointerCol - _grabbedCellOffset.x;
        int originRow = pointerRow - _grabbedCellOffset.y;

        if (_grid.CanPlace(RotatedShape, originCol, originRow))
        {
            _spawner.ConsumeBlock(_slotIndex);

            // Use the overload that accepts an explicit shape, so the
            // rotated version actually gets written into the grid data —
            // not whatever Definition.Shape says by default.
            _grid.PlaceBlock(Definition, RotatedShape, originCol, originRow);
            Destroy(gameObject);
            return true;
        }

        return false; // didn't fit — caller will snap back to tray
    }
}