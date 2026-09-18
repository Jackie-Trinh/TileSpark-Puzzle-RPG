using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Enemy tracks the live state of one enemy in a battle.
/// 
///   • Maintains a cooldown counter per ability (indexed to EnemyDefinition.Abilities).
///   • When the enemy's move timer fires, it collects all READY abilities,
///     picks ONE to execute (or uses a normal hit if none are ready), and
///     fires OnUsingAbility so BattleManager can resolve the actual effect.
///   • A small status icon list can show active ability names above the sprite
///     (wire up the StatusContainer in the prefab to see this).
/// </summary>
public class Enemy : MonoBehaviour
{
    // ── Events ─────────────────────────────────────────────────────────────

    /// <summary>Fired when this enemy's HP reaches 0.</summary>
    public event Action<Enemy> OnDied;

    /// <summary>
    /// Fired when the enemy timer expires and they want to make a normal attack.
    /// BattleManager subscribes and applies attack damage.
    /// </summary>
    public event Action<Enemy> OnAttacking;

    /// <summary>
    /// Fired when the enemy wants to use a special ability.
    /// Payload: (this enemy, the ability to execute).
    /// BattleManager subscribes and calls ExecuteEnemyAbility().
    /// </summary>
    public event Action<Enemy, EnemyAbility> OnUsingAbility;

    // ── Inspector references (assign in EnemyPrefab) ───────────────────────
    [Header("UI References")]
    public Image HealthBarFill;
    public TextMeshProUGUI TimerLabel;
    public Image SpriteImage;

    [Tooltip("Optional parent object for status effect icon images.")]
    public Transform StatusIconContainer;

    [Tooltip("Prefab for a small status icon (Image + TMP label).")]
    public GameObject StatusIconPrefab;

    [Tooltip("Where floating damage numbers should appear for this enemy " +
             "(usually the same RectTransform as the sprite or health bar). " +
             "If left empty, this enemy's own RectTransform is used instead.")]
    public RectTransform DamageNumberAnchor;

    // ── Runtime state ──────────────────────────────────────────────────────
    public EnemyDefinition Definition { get; private set; }
    public int MaxHP { get; private set; }
    public int CurrentHP { get; private set; }
    public int MovesUntilAttack { get; private set; }
    public bool IsAlive => CurrentHP > 0;
    public bool IsTargeted { get; private set; }

    /// <summary>
    /// The RectTransform damage numbers should spawn above for this enemy.
    /// Falls back to this enemy's own RectTransform if DamageNumberAnchor
    /// wasn't explicitly assigned in the Inspector.
    /// </summary>
    public RectTransform EffectiveDamageAnchor =>
        DamageNumberAnchor != null ? DamageNumberAnchor : transform as RectTransform;

    // Per-ability cooldown counters — index matches Definition.Abilities[i].
    // 0 = ready now.  N = N attack-turns until ready again.
    private List<int> _abilityCooldowns = new();

    // ── Initialisation ─────────────────────────────────────────────────────

    /// <summary>
    /// Called once by BattleManager after instantiating the enemy GameObject.
    /// Scales HP/attack to the current stage and seeds all ability cooldowns.
    /// </summary>
    public void Initialise(EnemyDefinition def, int stage)
    {
        Definition = def;

        float hpScale = Mathf.Pow(def.HPScaling, stage - 1);
        float atkScale = Mathf.Pow(def.AtkScaling, stage - 1);

        MaxHP = Mathf.RoundToInt(def.BaseHP * hpScale);
        CurrentHP = MaxHP;
        MovesUntilAttack = def.BaseSpeed;

        // One cooldown slot per ability, all starting at 0 (ready immediately)
        _abilityCooldowns.Clear();
        if (def.Abilities != null)
            foreach (var _ in def.Abilities)
                _abilityCooldowns.Add(0);

        if (SpriteImage != null)
            SpriteImage.sprite = def.EnemySprite;

        RefreshUI();
    }

    // ── Called by BattleManager each player move ───────────────────────────

    /// <summary>
    /// Decrements the move counter by one each time the player places a block.
    /// When it reaches 0 the enemy takes their full turn:
    ///   1. Try to use a ready special ability (fires OnUsingAbility).
    ///   2. Also fire OnAttacking unless the ability replaces the normal hit.
    /// </summary>
    public void TickMoveCounter()
    {
        MovesUntilAttack--;

        if (MovesUntilAttack > 0)
        {
            RefreshUI();
            return;
        }

        // Timer hit 0 — enemy takes their turn
        MovesUntilAttack = Definition.BaseSpeed;

        bool abilityUsed = TryUseAbility();

        // Always deal normal hit unless the design says ability replaces it
        if (!abilityUsed || !Definition.AbilityReplacesNormalHit)
            OnAttacking?.Invoke(this);

        RefreshUI();
    }

    // ── Public API ─────────────────────────────────────────────────────────

    /// <summary>Deals damage to this enemy. Returns actual damage dealt.</summary>
    public int TakeDamage(int rawDamage)
    {
        CurrentHP = Mathf.Max(0, CurrentHP - rawDamage);
        RefreshUI();
        if (CurrentHP == 0) OnDied?.Invoke(this);
        return rawDamage;
    }

    /// <summary>Extends the attack timer (Freeze / Stun from player effects).</summary>
    public void ApplyFreeze(int extraMoves)
    {
        MovesUntilAttack += extraMoves;
        RefreshUI();
    }

    /// <summary>Toggles the targeting highlight on this enemy's sprite.</summary>
    public void SetTargeted(bool targeted)
    {
        IsTargeted = targeted;
        // TODO: toggle a highlight/glow child object here
    }

    // ── Private ────────────────────────────────────────────────────────────

    /// <summary>
    /// Scans the ability list for the first one whose cooldown has reached 0.
    /// Fires OnUsingAbility, resets that ability's cooldown, and ticks the rest.
    /// Returns true if an ability fired.
    /// </summary>
    private bool TryUseAbility()
    {
        if (Definition.Abilities == null || Definition.Abilities.Count == 0)
            return false;

        for (int i = 0; i < Definition.Abilities.Count; i++)
        {
            if (_abilityCooldowns[i] <= 0)
            {
                EnemyAbility chosen = Definition.Abilities[i];

                // Restart this ability's cooldown
                _abilityCooldowns[i] = chosen.CooldownAttacks;

                // Let BattleManager resolve the ability effect
                OnUsingAbility?.Invoke(this, chosen);

                // Tick all OTHER ability cooldowns
                TickOtherCooldowns(skipIndex: i);
                return true;
            }
        }

        // Nothing was ready — tick everything down for next turn
        TickOtherCooldowns(skipIndex: -1);
        return false;
    }

    /// <summary>Decrements every cooldown except the one at skipIndex.</summary>
    private void TickOtherCooldowns(int skipIndex)
    {
        for (int i = 0; i < _abilityCooldowns.Count; i++)
        {
            if (i != skipIndex && _abilityCooldowns[i] > 0)
                _abilityCooldowns[i]--;
        }
    }

    private void RefreshUI()
    {
        if (HealthBarFill != null)
            HealthBarFill.fillAmount = MaxHP > 0 ? (float)CurrentHP / MaxHP : 0f;

        if (TimerLabel != null)
            TimerLabel.text = MovesUntilAttack.ToString();
    }
}