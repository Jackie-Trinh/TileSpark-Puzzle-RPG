using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// PlayerStatusDisplay handles two jobs in the battle UI:
///
///   1. DEBUFF ICON BAR
///      Reads the list of ActiveDebuffs from BattleManager and shows
///      one small icon per active debuff below the player's health bar.
///      Each icon displays the debuff name and moves remaining.
///      Icons are pooled (reused) so no garbage is created each frame.
///
///   2. STATUS MESSAGE POPUP
///      Subscribes to BattleUIEvents.OnStatusMessage and shows a short
///      text message that fades out after a couple of seconds.
///      This replaces Debug.Log for player-facing feedback.
/// </summary>
public class PlayerStatusDisplay : MonoBehaviour
{
    // ── Inspector fields ───────────────────────────────────────────────────

    [Header("Debuff Icon Bar")]
    [Tooltip("The horizontal layout group parent that holds the debuff icons.")]
    public Transform IconBar;

    [Tooltip("Small prefab with Image + two TMP labels (see setup notes above).")]
    public GameObject IconPrefab;

    [Tooltip("Sprite to show for each debuff type.  Index 0 = PlaceJunkBlock, " +
             "1 = TransformTrayBlock, 2 = PoisonPlayer, 3 = RemovePlacedBlock, " +
             "4 = DebuffPlayerStat, 5 = BlindPlayer.  Leave empty for a coloured square.")]
    public List<Sprite> DebuffSprites = new();

    [Tooltip("Colour tint used for each debuff type when no sprite is assigned.  " +
             "Same index order as DebuffSprites.")]
    public List<Color> DebuffColors = new()
    {
        new Color(0.6f, 0.6f, 0.6f),   // 0 PlaceJunkBlock  — grey
        new Color(0.8f, 0.5f, 0.0f),   // 1 TransformTray   — orange
        new Color(0.2f, 0.8f, 0.2f),   // 2 PoisonPlayer    — green
        new Color(0.5f, 0.0f, 0.5f),   // 3 RemoveBlock      — purple
        new Color(0.8f, 0.0f, 0.0f),   // 4 DebuffStat       — red
        new Color(0.0f, 0.5f, 0.9f),   // 5 BlindPlayer      — blue
    };

    [Header("Status Message Popup")]
    [Tooltip("A TextMeshPro label positioned above or below the grid. " +
             "Shows short feedback messages that fade out automatically.")]
    public TextMeshProUGUI StatusMessageLabel;

    [Tooltip("How long (seconds) the status message stays fully visible.")]
    public float MessageHoldTime  = 1.2f;

    [Tooltip("How long (seconds) the message takes to fade out.")]
    public float MessageFadeTime  = 0.6f;

    // ── Private state ──────────────────────────────────────────────────────

    // Pool of instantiated icon GameObjects so we never allocate mid-fight
    private readonly List<GameObject> _iconPool = new();

    // Coroutine reference so we can cancel a fade if a new message arrives
    private Coroutine _fadeCoroutine;

    // Short display names for each debuff type shown on the icon
    private static readonly string[] DebuffShortNames =
    {
        "Junk",     // PlaceJunkBlock
        "Swap",     // TransformTrayBlock
        "Poison",   // PoisonPlayer
        "Erase",    // RemovePlacedBlock
        "Debuff",   // DebuffPlayerStat
        "Blind",    // BlindPlayer
    };

    // ── Unity lifecycle ────────────────────────────────────────────────────

    private void Start()
    {
        // Subscribe to the status message event
        if (BattleUIEvents.Instance != null)
            BattleUIEvents.Instance.OnStatusMessage += ShowStatusMessage;

        // Hide the label initially
        if (StatusMessageLabel != null)
        {
            StatusMessageLabel.text  = "";
            StatusMessageLabel.alpha = 0f;
        }
    }

    private void OnDestroy()
    {
        if (BattleUIEvents.Instance != null)
            BattleUIEvents.Instance.OnStatusMessage -= ShowStatusMessage;
    }

    // ── Public API called by BattleManager ────────────────────────────────

    /// <summary>
    /// Rebuilds the debuff icon bar to match the current list of active debuffs.
    ///
    /// Call this from BattleManager after every change to the debuff list:
    ///   • After TickDebuffs() runs (each player move).
    ///   • After ExecuteEnemyAbility() adds a new debuff.
    ///
    /// The method hides all pooled icons first, then re-activates and
    /// populates only as many as there are active debuffs.
    /// </summary>
    public void Refresh(List<ActiveDebuff> activeDebuffs)
    {
        // Ensure we have enough pooled icons
        while (_iconPool.Count < activeDebuffs.Count)
            _iconPool.Add(Instantiate(IconPrefab, IconBar));

        // Hide every icon then re-show only what's needed
        foreach (var icon in _iconPool)
            icon.SetActive(false);

        for (int i = 0; i < activeDebuffs.Count; i++)
        {
            var debuff = activeDebuffs[i];
            var icon   = _iconPool[i];
            icon.SetActive(true);

            PopulateIcon(icon, debuff);
        }
    }

    // ── Private helpers ────────────────────────────────────────────────────

    /// <summary>
    /// Fills one icon object's visuals to represent the given debuff.
    ///
    /// Expected children in the IconPrefab (found by name):
    ///   "IconImage"     — Image component showing the debuff art
    ///   "DurationLabel" — TMP label showing moves remaining
    ///   "NameLabel"     — TMP label showing a short debuff name
    /// </summary>
    private void PopulateIcon(GameObject icon, ActiveDebuff debuff)
    {
        int typeIndex = (int)debuff.Source;

        // ── Icon image / colour ────────────────────────────────────────────
        var iconImage = icon.transform.Find("IconImage")?.GetComponent<Image>();
        if (iconImage != null)
        {
            // Use sprite if we have one for this type, otherwise plain colour
            if (typeIndex < DebuffSprites.Count && DebuffSprites[typeIndex] != null)
            {
                iconImage.sprite = DebuffSprites[typeIndex];
                iconImage.color  = Color.white;
            }
            else
            {
                iconImage.sprite = null;
                iconImage.color  = typeIndex < DebuffColors.Count
                    ? DebuffColors[typeIndex]
                    : Color.grey;
            }
        }

        // ── Short name label ───────────────────────────────────────────────
        var nameLabel = icon.transform.Find("NameLabel")?.GetComponent<TextMeshProUGUI>();
        if (nameLabel != null)
            nameLabel.text = typeIndex < DebuffShortNames.Length
                ? DebuffShortNames[typeIndex]
                : debuff.Source.ToString();

        // ── Duration label ─────────────────────────────────────────────────
        var durationLabel = icon.transform
            .Find("DurationLabel")?.GetComponent<TextMeshProUGUI>();
        if (durationLabel != null)
            durationLabel.text = debuff.MovesRemaining.ToString();
    }

    // ── Status message popup ───────────────────────────────────────────────

    /// <summary>
    /// Shows a short text message on screen then fades it out.
    /// Called via BattleUIEvents.OnStatusMessage.
    ///
    /// If a message is already showing, the previous fade is cancelled
    /// and the new message replaces it immediately.
    /// </summary>
    private void ShowStatusMessage(string message)
    {
        if (StatusMessageLabel == null) return;

        // Cancel any in-progress fade
        if (_fadeCoroutine != null)
            StopCoroutine(_fadeCoroutine);

        StatusMessageLabel.text  = message;
        StatusMessageLabel.alpha = 1f;

        _fadeCoroutine = StartCoroutine(FadeMessageOut());
    }

    /// <summary>
    /// Waits for MessageHoldTime, then linearly fades the label alpha to 0
    /// over MessageFadeTime seconds using a simple coroutine.
    ///
    /// Coroutines in Unity let us wait and animate over multiple frames
    /// without blocking other code.  yield return new WaitForSeconds() pauses
    /// the coroutine and lets the rest of the game continue running.
    /// </summary>
    private IEnumerator FadeMessageOut()
    {
        // Hold at full opacity
        yield return new WaitForSeconds(MessageHoldTime);

        // Fade out
        float elapsed = 0f;
        while (elapsed < MessageFadeTime)
        {
            elapsed += Time.deltaTime;
            StatusMessageLabel.alpha = Mathf.Lerp(1f, 0f, elapsed / MessageFadeTime);
            yield return null;  // wait one frame then continue
        }

        StatusMessageLabel.alpha = 0f;
        StatusMessageLabel.text  = "";
        _fadeCoroutine = null;
    }
}
