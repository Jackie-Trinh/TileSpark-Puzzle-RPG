/// <summary>
/// ActiveDebuff tracks one debuff that is currently applied to the player.
/// BattleManager holds a list of these and ticks them down each player move.
/// BattleManager is responsible for applying and expiring debuffs.
/// </summary>
public class ActiveDebuff
{
    // ── Which effect this is ───────────────────────────────────────────────

    /// <summary>The ability type that created this debuff (e.g. PoisonPlayer).</summary>
    public EnemyAbilityType Source;

    // ── Duration tracking ─────────────────────────────────────────────────

    /// <summary>
    /// Moves remaining before this debuff expires.
    /// Decremented by BattleManager.TickDebuffs() after each player move.
    /// </summary>
    public int MovesRemaining;

    // ── Poison-specific data ───────────────────────────────────────────────

    /// <summary>
    /// Fraction of the player's MAX HP dealt as damage each move while poisoned.
    /// Only meaningful when Source == PoisonPlayer.
    /// </summary>
    public float PoisonDamagePercent;

    // ── Debuff-specific data ───────────────────────────────────────────────

    /// <summary>
    /// Which stats were reduced by this debuff.
    /// Only meaningful when Source == DebuffPlayerStat.
    /// </summary>
    public DebuffTargetFlags AffectedStats;

    /// <summary>
    /// Absolute amounts subtracted from each stat when the debuff was applied.
    /// Stored so we can restore exactly the right values when it expires.
    /// Key = DebuffTargetFlags single-bit value, Value = amount removed.
    ///
    /// Example: if Damage was reduced by 5.0f, this stores {Damage → 5.0f}.
    /// </summary>
    public System.Collections.Generic.Dictionary<DebuffTargetFlags, float> RemovedAmounts
        = new();

    // ── Blind-specific data ────────────────────────────────────────────────
    // (No extra fields needed: blind is just "Source == BlindPlayer" + MovesRemaining.)
}
