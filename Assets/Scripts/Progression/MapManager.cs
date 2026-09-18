using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// MapManager
/// ── CENTERING BEHAVIOUR ────────────────────────────────────────────────
/// On load, the ScrollRect scrolls so the CURRENT stage tile is centred
/// vertically on screen. If the player is at a very early stage (e.g. 1-3),
/// there aren't enough tiles below to allow true centring — in that case
/// the scroll stops at the very bottom of the content (lowest stage at the
/// bottom of the screen) rather than showing empty space below stage 1.
///
/// ── TILE GENERATION ────────────────────────────────────────────────────
/// Tiles are generated in a range:
///   [currentStage - TilesBelow]  →  [currentStage + TilesAbove]
/// clamped so stage 1 is always the lowest tile shown.
/// Tiles above current stage are locked (dark, not interactable).
/// Tiles below are completed (grey, interactable — player can replay them).
/// The current stage tile is yellow and interactable.
///
/// ── SCROLL TIMING ──────────────────────────────────────────────────────
/// ContentSizeFitter and VerticalLayoutGroup don't finish their layout
/// pass until the END of the first frame after instantiation. Reading tile
/// positions in Start() would give (0,0) for everything. We wait one frame
/// via a coroutine before scrolling.
/// </summary>
public class MapManager : MonoBehaviour
{
    [Header("Tile Setup")]
    public GameObject StageTilePrefab;

    [Tooltip("The Content child of your ScrollRect — this is the parent " +
             "all tile GameObjects are instantiated into.")]
    public RectTransform TileContainer;

    [Tooltip("How many tiles to show ABOVE the current stage (locked stages).")]
    public int TilesAbove = 10;

    [Tooltip("How many tiles to show BELOW the current stage (completed stages). " +
             "Clamped so we never go below stage 1.")]
    public int TilesBelow = 10;

    [Header("Scroll View")]
    [Tooltip("The ScrollRect component on your map scroll view.")]
    public ScrollRect MapScrollRect;

    [Header("Player Marker")]
    public RectTransform PlayerMarker;

    [Header("Top Bar")]
    public TextMeshProUGUI ProfileNameLabel;
    public TextMeshProUGUI CurrentStageLabel;
    public TextMeshProUGUI GoldLabel;
    public TextMeshProUGUI DiamondLabel;

    // Tracks the RectTransform of the current stage tile so we can
    // scroll to it after layout finishes.
    private RectTransform _currentStageTileRect;
    private List<Button> _tileButtons = new();

    // ──────────────────────────────────────────────────────────────────────

    private void Start()
    {
        RefreshTopBar();
        BuildTiles();
        PlacePlayerMarker();

        // Layout is not complete yet in Start() — wait one frame then scroll.
        StartCoroutine(ScrollToCurrentStageNextFrame());
    }

    // ── Top bar ────────────────────────────────────────────────────────────

    private void RefreshTopBar()
    {
        var pd = GameManager.Instance.PlayerData;
        if (ProfileNameLabel) ProfileNameLabel.text = pd.PlayerName;
        if (CurrentStageLabel) CurrentStageLabel.text = $"Stage {pd.CurrentStage}";
        if (GoldLabel) GoldLabel.text = $"Gold: {pd.Gold.ToString()}";
        if (DiamondLabel) DiamondLabel.text = $"Diamonds: {pd.Diamonds.ToString()}";
    }

    // ── Tile building ──────────────────────────────────────────────────────

    private void BuildTiles()
    {
        int currentStage = GameManager.Instance.PlayerData.CurrentStage;

        // Calculate the visible stage range, clamped so we never go below 1.
        int lowestStage = Mathf.Max(1, currentStage - TilesBelow);
        int highestStage = currentStage + TilesAbove;

        // Clear any existing tiles from a previous build.
        foreach (Transform child in TileContainer)
            Destroy(child.gameObject);
        _tileButtons.Clear();
        _currentStageTileRect = null;

        // Instantiate from highest → lowest so the VerticalLayoutGroup
        // (which fills top-to-bottom in child order) puts higher stages
        // visually at the TOP of the scroll view, matching the convention
        // used throughout the rest of the project.
        for (int s = highestStage; s >= lowestStage; s--)
        {
            int stageNum = s;
            var go = Instantiate(StageTilePrefab, TileContainer);
            var btn = go.GetComponent<Button>();
            var label = go.GetComponentInChildren<TextMeshProUGUI>();
            var tileRect = go.GetComponent<RectTransform>();

            if (label != null) label.text = stageNum.ToString();

            if (stageNum < currentStage)
            {
                // Completed — greyed out, still interactable (player can replay)
                btn.interactable = true;
                go.GetComponent<Image>().color = new Color(0.5f, 0.5f, 0.5f);
            }
            else if (stageNum == currentStage)
            {
                // Current — highlighted yellow, interactable
                btn.interactable = true;
                go.GetComponent<Image>().color = Color.yellow;

                // Remember this tile's RectTransform so we can scroll to it.
                _currentStageTileRect = tileRect;
            }
            else
            {
                // Locked — dark, not interactable
                btn.interactable = false;
                go.GetComponent<Image>().color = new Color(0.2f, 0.2f, 0.2f);
            }

            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() =>
            {
                Debug.Log($"[MapManager] Tile clicked: stage {stageNum}");
                GameManager.Instance.StartBattle(stageNum);
            });

            _tileButtons.Add(btn);
        }
    }

    // ── Player marker ──────────────────────────────────────────────────────

    private void PlacePlayerMarker()
    {
        if (PlayerMarker == null || _currentStageTileRect == null) return;

        // Parent the marker to the current stage tile so it moves with
        // the tile when the scroll view scrolls.
        PlayerMarker.SetParent(_currentStageTileRect, worldPositionStays: false);
        PlayerMarker.anchoredPosition = new Vector2(-350f, 0f); // above the tile
    }

    // ── Scroll centering ───────────────────────────────────────────────────

    /// <summary>
    /// Waits one frame for Unity's layout system (ContentSizeFitter +
    /// VerticalLayoutGroup) to finish calculating all tile positions and
    /// the content rect's final size, then scrolls the ScrollRect so the
    /// current stage tile is vertically centred on screen.
    ///
    /// Why one frame? ContentSizeFitter runs during the layout pass at the
    /// END of the current frame — reading RectTransform positions before that
    /// gives (0,0) for every tile, making any scroll calculation meaningless.
    /// </summary>
    private IEnumerator ScrollToCurrentStageNextFrame()
    {
        // Wait for end of frame so layout has finished.
        yield return new WaitForEndOfFrame();

        if (MapScrollRect == null)
        {
            Debug.LogWarning("[MapManager] MapScrollRect is not assigned — " +
                              "cannot scroll to current stage. Drag in your " +
                              "ScrollRect component in the Inspector.");
            yield break;
        }

        if (_currentStageTileRect == null)
        {
            Debug.LogWarning("[MapManager] No current stage tile was found — cannot scroll.");
            yield break;
        }

        ScrollToTile(_currentStageTileRect);
    }

    /// <summary>
    /// Calculates and applies the correct ScrollRect.verticalNormalizedPosition
    /// to place the given tile at the vertical centre of the viewport.
    ///
    /// ── How the math works ─────────────────────────────────────────────
    /// ScrollRect.verticalNormalizedPosition is 0.0 = bottom, 1.0 = top.
    /// We want the tile's centre to align with the viewport's centre.
    ///
    /// Step 1: find the tile's Y position in Content-local space.
    ///   `anchoredPosition.y` gives us the tile's pivot relative to the
    ///   Content rect's pivot. We want the tile's CENTRE relative to the
    ///   Content rect's BOTTOM EDGE, which we call `tileCentreFromBottom`.
    ///
    /// Step 2: the ideal scroll position puts the tile centre at half the
    ///   viewport height above the bottom of the visible area:
    ///   `idealBottomY = tileCentreFromBottom - viewportHeight * 0.5f`
    ///
    /// Step 3: convert to a normalised 0..1 value:
    ///   `normalised = idealBottomY / (contentHeight - viewportHeight)`
    ///   Clamped to 0..1 so we never scroll past the content bounds —
    ///   this is what handles "too low a stage" gracefully (clamp to 0 =
    ///   lowest stage at bottom of screen, no empty space shown below).
    /// </summary>
    private void ScrollToTile(RectTransform tileRect)
    {
        // Force a layout rebuild to guarantee all sizes are up to date
        // (belt-and-suspenders on top of the WaitForEndOfFrame).
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(TileContainer);

        RectTransform content = TileContainer;
        RectTransform viewport = MapScrollRect.viewport != null
                                     ? MapScrollRect.viewport
                                     : MapScrollRect.GetComponent<RectTransform>();

        float contentHeight = content.rect.height;
        float viewportHeight = viewport.rect.height;

        if (contentHeight <= viewportHeight)
        {
            // Content is shorter than the viewport — no scrolling needed,
            // everything is already visible. Set to top (1.0).
            MapScrollRect.verticalNormalizedPosition = 1f;
            return;
        }

        // `anchoredPosition.y` for a top-anchored Content is NEGATIVE
        // (tiles go downward from y=0 at the top). We convert it to a
        // "distance from the bottom of the content" value instead, which
        // makes the normalised calculation straightforward.
        //
        // Content pivot is typically (0.5, 1) for a top-anchored layout,
        // so anchoredPosition.y of the tile is relative to the Content's
        // top edge. We find the tile centre from the content TOP first,
        // then convert to from-bottom by subtracting from contentHeight.
        float tileCentreFromTop = -tileRect.anchoredPosition.y + tileRect.rect.height * 0.5f;
        float tileCentreFromBottom = contentHeight - tileCentreFromTop;

        // Ideal scroll: tile centre sits at middle of viewport.
        float idealBottomY = tileCentreFromBottom - viewportHeight * 0.5f;

        // Normalise: 0 = scroll all the way down (bottom of content visible),
        // 1 = scroll all the way up (top of content visible).
        float scrollableHeight = contentHeight - viewportHeight;
        float normalised = Mathf.Clamp01(idealBottomY / scrollableHeight);

        MapScrollRect.verticalNormalizedPosition = normalised;

        Debug.Log($"[MapManager] Scrolled to stage {GameManager.Instance.PlayerData.CurrentStage}. " +
                  $"contentH={contentHeight:0}, viewportH={viewportHeight:0}, " +
                  $"tileCentreFromBottom={tileCentreFromBottom:0}, normalised={normalised:0.000}");
    }
}