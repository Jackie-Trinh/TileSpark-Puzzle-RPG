using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// GridDropZone sits on the Grid Container RectTransform and converts
/// a screen-space pointer position into a (column, row) grid cell index.
/// GridLayoutGroup ALWAYS lays out children starting from the rect's
/// TOP-LEFT CORNER, regardless of the RectTransform's pivot setting. So
/// instead of guessing based on pivot, we directly compute the top-left
/// corner's position using rect.width/height and rect.pivot, which is
/// guaranteed correct for any pivot configuration.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class GridDropZone : MonoBehaviour
{
    [Header("Grid Reference")]
    public BlockGrid Grid;

    // Cached layout info
    private RectTransform _rectTransform;
    private GridLayoutGroup _layoutGroup;

    private void Awake()
    {
        _rectTransform = GetComponent<RectTransform>();
        _layoutGroup = GetComponent<GridLayoutGroup>();
    }

    /// <summary>
    /// Given a screen-space position (from PointerEventData.position),
    /// returns the grid (col, row) the pointer is over.
    /// Returns false if the pointer is outside the grid.
    /// </summary>
    public bool TryGetCellFromPointer(Vector2 screenPos, out int col, out int row)
    {
        col = row = -1;

        if (Grid == null)
        {
            Debug.LogError("[GridDropZone] 'Grid' field is not assigned in the Inspector! " +
                            "Select the GameObject with GridDropZone and drag in the " +
                            "object that has the BlockGrid component.", this);
            return false;
        }

        if (_layoutGroup == null)
        {
            Debug.LogError("[GridDropZone] No GridLayoutGroup component found on this " +
                            "GameObject. GridDropZone must be on the same object as the " +
                            "GridLayoutGroup that lays out your grid cells.", this);
            return false;
        }

        // Get the pointer position in this RectTransform's LOCAL space.
        // This point is measured relative to the rect's PIVOT, in any direction.
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _rectTransform, screenPos, null, out Vector2 localPoint))
            return false;

        Rect rect = _rectTransform.rect;

        // rect.xMin / rect.yMax give us the TRUE top-left corner of the
        // RectTransform in local space, correctly accounting for whatever
        // pivot is set (0,0 / 0.5,0.5 / 1,1 / anything).
        float topLeftX = rect.xMin;
        float topLeftY = rect.yMax;

        // Distance from the pointer to the top-left corner
        float localX = localPoint.x - topLeftX;          // grows rightward
        float localY = topLeftY - localPoint.y;           // grows downward

        float cellW = _layoutGroup.cellSize.x + _layoutGroup.spacing.x;
        float cellH = _layoutGroup.cellSize.y + _layoutGroup.spacing.y;

        // Also account for the GridLayoutGroup's own internal padding,
        // which shifts the first cell inward from the rect's true corner.
        localX -= _layoutGroup.padding.left;
        localY -= _layoutGroup.padding.top;

        col = Mathf.FloorToInt(localX / cellW);
        row = Mathf.FloorToInt(localY / cellH);

        // Bounds check
        if (col < 0 || col >= Grid.Columns) return false;
        if (row < 0 || row >= Grid.Rows) return false;

        return true;
    }
}