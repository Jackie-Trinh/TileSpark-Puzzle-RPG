using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

/// <summary>
/// DamageNumber is a small floating combat-text popup
///
/// It is entirely self-contained: once spawned, it animates itself
/// (floats upward while fading out) over a configurable duration, then
/// destroys its own GameObject. Nothing else needs to manage its lifetime.
/// </summary>
public class DamageNumber : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Usually auto-found on the same GameObject, but can be assigned manually.")]
    public TextMeshProUGUI Label;

    [Header("Animation Settings")]
    [Tooltip("How far upward (in pixels) the number drifts over its lifetime.")]
    public float FloatDistance = 80f;

    [Tooltip("Total time in seconds before this number fully disappears.")]
    public float Lifetime = 1.1f;

    [Tooltip("Fraction of Lifetime spent fully opaque before fading begins. " +
             "0.3 means it stays fully visible for the first 30% of its life.")]
    [Range(0f, 1f)]
    public float HoldFraction = 0.25f;

    [Tooltip("Slight random horizontal drift range, so stacked numbers don't " +
             "look perfectly robotic when several appear close together.")]
    public float HorizontalJitter = 15f;

    [Header("Colours")]
    public Color PlayerDamageColor = new Color(1f, 0.25f, 0.25f);   // red — damage TO player
    public Color EnemyDamageColor  = new Color(1f, 0.85f, 0.2f);    // yellow/gold — damage TO enemy
    public Color HealColor         = new Color(0.3f, 1f, 0.4f);     // green — healing
    public Color CritColor         = new Color(1f, 0.4f, 1f);       // magenta — critical hits

    private RectTransform _rect;
    private Vector2       _startPos;

    private void Awake()
    {
        _rect = GetComponent<RectTransform>();
        if (Label == null) Label = GetComponent<TextMeshProUGUI>();
    }

    /// <summary>
    /// Configures this popup's text, colour, and starting screen position,
    /// then begins its float-and-fade animation immediately.
    ///
    /// Call this right after Instantiate() — do NOT call Initialise more
    /// than once on the same instance.
    /// </summary>
    /// <param name="amount">The number to display (damage dealt, healed, etc).</param>
    /// <param name="kind">Which colour preset to use.</param>
    /// <param name="stackOffset">Extra vertical pixels to start higher up,
    /// used by DamageNumberSpawner so multiple simultaneous hits stack
    /// upward instead of overlapping each other.</param>
    public void Initialise(int amount, DamageNumberKind kind, float stackOffset)
    {
        Label.text = amount.ToString();

        Label.color = kind switch
        {
            DamageNumberKind.PlayerDamage => PlayerDamageColor,
            DamageNumberKind.EnemyDamage  => EnemyDamageColor,
            DamageNumberKind.Heal         => HealColor,
            DamageNumberKind.Critical     => CritColor,
            _                             => Color.white,
        };

        // Critical hits get a slightly bigger font for extra punch
        if (kind == DamageNumberKind.Critical)
            Label.fontSize *= 1.3f;

        // Apply the stacking offset (pushes this number above earlier ones
        // that are still animating at the same anchor point) plus a small
        // random horizontal jitter so a burst of numbers doesn't look
        // perfectly stacked in a single robotic column.
        float jitterX = Random.Range(-HorizontalJitter, HorizontalJitter);
        _rect.anchoredPosition += new Vector2(jitterX, stackOffset);
        _startPos = _rect.anchoredPosition;

        StartCoroutine(AnimateAndDestroy());
    }

    /// <summary>
    /// Animates the number floating upward while fading out, then destroys
    /// the GameObject. Runs once per spawned instance.
    /// </summary>
    private IEnumerator AnimateAndDestroy()
    {
        float holdTime = Lifetime * HoldFraction;
        float fadeTime = Lifetime - holdTime;
        float elapsed  = 0f;

        // Phase 1: hold fully visible while already starting to rise slightly,
        // so the popup feels snappy rather than static at the very start.
        while (elapsed < holdTime)
        {
            elapsed += Time.deltaTime;
            float t  = elapsed / holdTime;
            _rect.anchoredPosition = _startPos + Vector2.up * (FloatDistance * 0.3f * t);
            yield return null;
        }

        // Phase 2: continue rising and fade alpha down to 0
        elapsed = 0f;
        Color startColor = Label.color;
        while (elapsed < fadeTime)
        {
            elapsed += Time.deltaTime;
            float t  = elapsed / fadeTime;

            float riseAmount = FloatDistance * (0.3f + 0.7f * t);
            _rect.anchoredPosition = _startPos + Vector2.up * riseAmount;

            Color c = startColor;
            c.a = Mathf.Lerp(1f, 0f, t);
            Label.color = c;

            yield return null;
        }

        Destroy(gameObject);
    }
}

/// <summary>Which colour/style preset a spawned DamageNumber should use.</summary>
public enum DamageNumberKind
{
    PlayerDamage,   // red — shown when the PLAYER takes damage
    EnemyDamage,    // yellow/gold — shown when an ENEMY takes damage
    Heal,           // green — shown for healing numbers
    Critical,       // magenta — shown for critical hits (overrides Player/Enemy colour)
}
