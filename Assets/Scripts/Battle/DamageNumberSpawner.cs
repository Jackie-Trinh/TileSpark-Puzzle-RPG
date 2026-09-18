using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// DamageNumberSpawner is the single entry point for creating floating
/// damage/heal numbers anywhere in the battle UI. BattleManager and Enemy
/// both call into this whenever damage or healing happens.
///
/// STACKING BEHAVIOUR:
/// If several damage numbers spawn at the same anchor point within a short
/// window (e.g. a multi-hit combo, or poison ticking the same frame as a
/// block-clear hit), each new number starts higher up than the last one
/// still on screen, so they stack upward in a readable column instead of
/// overlapping illegibly. Once a number finishes its animation and is
/// destroyed, the stack count for that anchor decreases again.
/// </summary>
public class DamageNumberSpawner : MonoBehaviour
{
    [Header("Prefab & Canvas")]
    [Tooltip("The DamageNumber prefab to instantiate for each popup.")]
    public GameObject DamageNumberPrefab;

    [Tooltip("The root Canvas RectTransform — spawned numbers are parented " +
             "here so they always render on top and use correct screen space.")]
    public RectTransform PopupCanvas;

    [Header("Stacking")]
    [Tooltip("Vertical pixel gap between stacked numbers at the same anchor.")]
    public float StackSpacing = 40f;

    [Tooltip("Maximum number of stacked popups tracked per anchor before " +
             "older stack positions start being reused (prevents numbers " +
             "drifting off-screen during very long combo chains).")]
    public int MaxStackHeight = 6;

    // Tracks how many DamageNumbers are CURRENTLY active per anchor
    // RectTransform, so each new one knows how high to start.
    private readonly Dictionary<RectTransform, int> _activeStackCounts = new();

    /// <summary>
    /// Spawns a floating damage/heal number directly above the given anchor.
    /// </summary>
    /// <param name="anchor">The RectTransform to spawn above — e.g. the
    /// player's HUD position, or a specific enemy's sprite/health bar.</param>
    /// <param name="amount">The number to display.</param>
    /// <param name="kind">Which colour/style preset to use.</param>
    public void Spawn(RectTransform anchor, int amount, DamageNumberKind kind)
    {
        if (DamageNumberPrefab == null || PopupCanvas == null || anchor == null)
        {
            Debug.LogWarning("[DamageNumberSpawner] Missing prefab, canvas, or anchor reference — skipping popup.");
            return;
        }

        // Work out the current stack height at this anchor
        int currentStack = _activeStackCounts.TryGetValue(anchor, out int count) ? count : 0;
        int clampedStack = Mathf.Min(currentStack, MaxStackHeight);
        float stackOffset = clampedStack * StackSpacing;

        // Convert the anchor's world position into a local position inside
        // the popup canvas, so the number appears in the right screen spot
        // regardless of how deep the anchor is nested in the hierarchy.
        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(null, anchor.position);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            PopupCanvas, screenPoint, null, out Vector2 localPoint);

        var go = Instantiate(DamageNumberPrefab, PopupCanvas);
        var rect = go.GetComponent<RectTransform>();
        rect.anchoredPosition = localPoint;

        var damageNumber = go.GetComponent<DamageNumber>();
        damageNumber.Initialise(amount, kind, stackOffset);

        // Track this popup so the stack count increases while it's alive,
        // and decreases automatically once it destroys itself.
        _activeStackCounts[anchor] = currentStack + 1;
        StartCoroutine(DecrementStackWhenDone(anchor, damageNumber));
    }

    /// <summary>
    /// Waits until the given DamageNumber's GameObject is destroyed (its own
    /// animation coroutine handles that), then decrements the stack counter
    /// for its anchor so future popups don't keep climbing forever.
    /// </summary>
    private System.Collections.IEnumerator DecrementStackWhenDone(RectTransform anchor, DamageNumber number)
    {
        // Wait one frame at a time until the object no longer exists.
        // Using `number == null` (Unity's overloaded null check) correctly
        // detects when the underlying GameObject has been destroyed.
        while (number != null)
            yield return null;

        if (_activeStackCounts.TryGetValue(anchor, out int count))
            _activeStackCounts[anchor] = Mathf.Max(0, count - 1);
    }
}
