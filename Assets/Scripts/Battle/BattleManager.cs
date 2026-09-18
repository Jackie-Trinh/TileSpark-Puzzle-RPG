using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// BattleManager is the central coordinator for a battle.
///
/// Handles the full enemy ability system.
/// Every special ability an enemy uses is routed through
/// ExecuteEnemyAbility(), which reads the EnemyAbility ScriptableObject
/// and applies the correct effect to the player, the grid, or the tray.
///
/// It also maintains the list of ActiveDebuffs on the player and ticks
/// them down each move via TickDebuffs().
///
/// ── Flow per player move ───────────────────────────────────────────────
///  1. Player places a block  →  BlockGrid.PlaceBlock() runs.
///  2. BlockGrid fires OnLinesCleared  →  HandleClear() runs.
///  3. BlockGrid fires OnBlockPlaced   →  HandleBlockPlaced() runs.
///       a. TickDebuffs() — advances poison / blind / debuff timers.
///       b. Each alive enemy's TickMoveCounter() runs.
///          • If the timer hits 0, Enemy fires OnAttacking and/or OnUsingAbility.
///       c. HandleEnemyAttack()  →  normal hit damage.
///       d. ExecuteEnemyAbility() →  special ability effect.
///  4. Win / loss checked.
/// </summary>
public class BattleManager : MonoBehaviour
{
    // ── Scene references ───────────────────────────────────────────────────
    [Header("Core Systems")]
    public BlockGrid Grid;
    public BlockSpawner Spawner;
    public GridRenderer GridRenderer;

    [Header("Enemy Setup")]
    public Transform EnemyContainer;
    public GameObject EnemyPrefab;
    public List<StageConfig> StageConfigs;

    [Header("Player UI")]
    public Image PlayerHealthBarFill;
    public TextMeshProUGUI PlayerHPLabel;
    public TextMeshProUGUI StageLabel;
    public TextMeshProUGUI TotalEnemyHPLabel;

    [Header("Damage Numbers")]
    [Tooltip("Drag in the GameObject that has the DamageNumberSpawner component.")]
    public DamageNumberSpawner DamageNumbers;

    [Tooltip("Where floating damage/heal numbers should appear for the PLAYER " +
             "(e.g. the player's sprite or health bar RectTransform).")]
    public RectTransform PlayerDamageAnchor;

    [Header("Debuff / Status UI")]
    [Tooltip("Assign the PlayerStatusDisplay component that lives on the player HUD.")]
    public PlayerStatusDisplay StatusDisplay;

    [Header("Ability Card Database")]
    public AbilityCardDatabase AbilityDB;

    // ── Runtime state ──────────────────────────────────────────────────────
    private List<Enemy> _enemies = new();
    private Enemy _currentTarget;
    private int _stageNumber;
    private bool _battleOver = false;

    // Consecutive line-clear counter for mold block reward
    private int _consecutiveLineClears = 0;

    // Amplify: next clear deals double damage (from player block effect)
    private bool _amplifyActive = false;

    // ── Active player debuffs ──────────────────────────────────────────────
    // Each entry is one active debuff that expires after N player moves.
    // BattleManager owns this list and ticks it in HandleBlockPlaced().
    private List<ActiveDebuff> _activeDebuffs = new();

    // Convenience properties that read the debuff list
    // so other methods do not need to scan the list repeatedly.

    /// <summary>True while any PoisonPlayer debuff is active on the player.</summary>
    private bool IsPlayerPoisoned => _activeDebuffs.Any(d =>
        d.Source == EnemyAbilityType.PoisonPlayer && d.MovesRemaining > 0);

    /// <summary>True while any BlindPlayer debuff is active on the player.</summary>
    public bool IsPlayerBlinded => _activeDebuffs.Any(d =>
        d.Source == EnemyAbilityType.BlindPlayer && d.MovesRemaining > 0);

    // ──────────────────────────────────────────────────────────────────────
    private void Start()
    {
        _stageNumber = GameManager.Instance.CurrentStage;

        Grid.OnLinesCleared += HandleClear;
        Grid.OnBlockPlaced += HandleBlockPlaced;

        SpawnEnemies();
        RefreshPlayerUI();
        Spawner.GenerateNewBlocks();
    }

    // ── Enemy spawning ─────────────────────────────────────────────────────

    private void SpawnEnemies()
    {
        var config = StageConfigs[(_stageNumber - 1) % StageConfigs.Count];

        foreach (var def in config.Enemies)
        {
            var go = Instantiate(EnemyPrefab, EnemyContainer);
            var enemy = go.GetComponent<Enemy>();
            enemy.Initialise(def, _stageNumber);

            // Subscribe to all three enemy events
            enemy.OnDied += HandleEnemyDied;
            enemy.OnAttacking += HandleEnemyAttack;
            enemy.OnUsingAbility += ExecuteEnemyAbility;  // NEW

            _enemies.Add(enemy);
        }

        if (_enemies.Count > 0) SetTarget(_enemies[0]);

        if (StageLabel) StageLabel.text = $"Stage {_stageNumber}";
        RefreshTotalEnemyHP();
    }

    // ── Core event handlers ────────────────────────────────────────────────

    /// <summary>
    /// Called once per block placed.
    /// <summary>
    /// Called once per block placed.
    ///   1. Tick active debuffs (poison damage, blind countdown, debuff expiry).
    ///   2. Tick each enemy's move counter (may trigger attacks / abilities).
    ///   3. Check for deadlock — no playable blocks AND no usable powers.
    /// </summary>
    private void HandleBlockPlaced()
    {
        if (_battleOver) return;

        // 1. Advance all player debuffs first so they apply on the move
        //    the player just made, then expire correctly.
        TickDebuffs();

        // 2. Tick enemies
        foreach (var e in _enemies)
            if (e.IsAlive) e.TickMoveCounter();

        // 3. Deadlock check.
        //
        // Spawner.CurrentBlocks intentionally contains null for any tray
        // slot already placed this cycle — the tray only refills once ALL
        // THREE slots are used. DraggableBlock now calls ConsumeBlock()
        // BEFORE PlaceBlock() fires this event, so by the time we get here,
        // CurrentBlocks is always up to date: either it shows the 1-2 real
        // blocks still remaining, or — if this was the last of the 3 — a
        // brand new fully-refilled set. BlockGrid.IsGridDeadlocked() already
        // safely skips any null entries, so we can check on every move
        // without the false-positive/false-negative timing issues we had
        // before.
        //
        // The player only loses here if:
        //   • none of the blocks CURRENTLY in the tray can be placed anywhere, AND
        //   • they have no Bomb / Omni / Potion power left to use instead.
        if (Grid.IsGridDeadlocked(Spawner.CurrentBlocks) && !HasUsablePowers())
        {
            EndBattle(playerWon: false);
        }
    }

    /// <summary>
    /// Called when BlockGrid detects completed lines.
    /// Calculates damage (zero if blinded), applies player block effects,
    /// tracks combos and the mold block counter.
    /// </summary>
    private void HandleClear(ClearResult result)
    {
        if (_battleOver) return;

        // ── Damage ────────────────────────────────────────────────────────
        float totalDamage = 0f;

        if (IsPlayerBlinded)
        {
            // Blinded: blocks clear the grid normally but deal no damage
            BattleUIEvents.Instance?.NotifyStatusMessage("Blinded! No damage this move.");
        }
        else
        {
            totalDamage = CalculateDamage(result);

            if (_amplifyActive)
            {
                totalDamage *= 2f;
                _amplifyActive = false;
            }

            bool isCrit = Random.value < GameManager.Instance.PlayerData.Luck;
            if (isCrit) totalDamage *= 2f;

            int finalDamage = Mathf.RoundToInt(totalDamage);
            if (_currentTarget != null && _currentTarget.IsAlive)
            {
                _currentTarget.TakeDamage(finalDamage);

                // Show a floating number above the enemy that was hit.
                // Crits get their own colour regardless of the normal
                // "enemy damage" colour, so they stand out at a glance.
                DamageNumbers?.Spawn(
                    _currentTarget.EffectiveDamageAnchor,
                    finalDamage,
                    isCrit ? DamageNumberKind.Critical : DamageNumberKind.EnemyDamage);
            }
        }

        // ── Block effects ─────────────────────────────────────────────────
        var allCleared = result.BlockTypesClearedInRows
            .Concat(result.BlockTypesClearedInCols)
            .Distinct();

        foreach (var bType in allCleared)
            ApplyEffect(GetActiveEffect(bType), result);

        // ── Consecutive clear counter (mold block) ────────────────────────
        _consecutiveLineClears++;
        if (_consecutiveLineClears >= 3)
        {
            _consecutiveLineClears = 0;
            Spawner.AddMoldBlock();
        }

        // ── Max-combo check ───────────────────────────────────────────────
        if (result.WasFullClear)
            BattleUIEvents.Instance?.NotifyMaxCombo();

        RefreshTotalEnemyHP();
    }

    // ── Enemy event handlers ───────────────────────────────────────────────

    /// <summary>Normal attack: deals scaled damage minus player defense.</summary>
    private void HandleEnemyAttack(Enemy enemy)
    {
        if (_battleOver) return;

        float rawAtk = enemy.Definition.BaseAttack
                        * Mathf.Pow(enemy.Definition.AtkScaling, _stageNumber - 1);
        float defense = GameManager.Instance.PlayerData.Defense;
        int damage = Mathf.Max(1, Mathf.RoundToInt(rawAtk - defense));

        ApplyDamageToPlayer(damage);
    }

    private void HandleEnemyDied(Enemy enemy)
    {
        if (_currentTarget == enemy)
        {
            var next = _enemies.FirstOrDefault(e => e.IsAlive && e != enemy);
            SetTarget(next);
        }

        if (_enemies.All(e => !e.IsAlive))
            EndBattle(playerWon: true);
    }

    // ── ENEMY ABILITY EXECUTION ────────────────────────────────────────────

    /// <summary>
    /// Central dispatcher for all enemy special abilities.
    /// Called via the Enemy.OnUsingAbility event each time an enemy's
    /// ability cooldown reaches 0 and they take their turn.
    ///
    /// Each case reads the relevant fields from the EnemyAbility asset
    /// (which you configure in the Unity Inspector) and performs the effect.
    ///
    /// To add a new ability type:
    ///   1. Add a value to EnemyAbilityType enum.
    ///   2. Add a case here.
    ///   3. Add any new fields to EnemyAbility.cs if the effect needs parameters.
    /// </summary>
    private void ExecuteEnemyAbility(Enemy source, EnemyAbility ability)
    {
        if (_battleOver) return;

        Debug.Log($"[BattleManager] {source.Definition.EnemyName} uses {ability.AbilityName}");

        switch (ability.AbilityType)
        {
            // ── Place 1×1 junk blocks on the grid ─────────────────────────
            case EnemyAbilityType.PlaceJunkBlock:
                ExecutePlaceJunkBlock(ability);
                break;

            // ── Replace a random tray block with a different random block ──
            case EnemyAbilityType.TransformTrayBlock:
                ExecuteTransformTrayBlock();
                break;

            // ── Apply a poison DoT debuff to the player ────────────────────
            case EnemyAbilityType.PoisonPlayer:
                ExecutePoisonPlayer(ability);
                break;

            // ── Remove a random placed block from the grid ─────────────────
            case EnemyAbilityType.RemovePlacedBlock:
                ExecuteRemovePlacedBlock(ability);
                break;

            // ── Debuff one or more player stats ────────────────────────────
            case EnemyAbilityType.DebuffPlayerStat:
                ExecuteDebuffPlayerStat(ability);
                break;

            // ── Blind the player (no damage for N moves) ───────────────────
            case EnemyAbilityType.BlindPlayer:
                ExecuteBlindPlayer(ability);
                break;
        }

        // Refresh UI after any ability so status icons stay current
        StatusDisplay?.Refresh(_activeDebuffs);
        RefreshPlayerUI();
    }

    // ── Individual ability implementations ────────────────────────────────

    /// <summary>
    /// PlaceJunkBlock: drops N 1×1 filler blocks on random EMPTY grid cells.
    ///
    /// Effect on gameplay: clutters the grid, making line clears harder
    /// because the junk cells are plain and have no special block type — 
    /// they still count as filled cells that block line completion.
    ///
    /// Implementation note: we call Grid.ForceSetCell() which bypasses the
    /// normal "can place?" check so the enemy can always perform this action.
    /// </summary>
    private void ExecutePlaceJunkBlock(EnemyAbility ability)
    {
        // Collect all currently empty cells
        var emptyCells = new List<Vector2Int>();
        var snapshot = Grid.GetGridSnapshot();

        for (int c = 0; c < Grid.Columns; c++)
            for (int r = 0; r < Grid.Rows; r++)
            {
                if (snapshot[c, r] == null)
                    emptyCells.Add(new Vector2Int(c, r));
            }

        if (emptyCells.Count == 0) return;  // grid is completely full — nothing to do

        // Place up to JunkBlockCount junk blocks in random empty cells
        int placedCount = 0;
        while (placedCount < ability.JunkBlockCount && emptyCells.Count > 0)
        {
            int idx = Random.Range(0, emptyCells.Count);
            Vector2Int pos = emptyCells[idx];
            emptyCells.RemoveAt(idx);   // don't pick the same cell twice

            // BlockType.Red is used as a neutral junk type.
            // You could add a dedicated BlockType.Junk if you prefer.
            Grid.ForceSetCell(pos.x, pos.y, BlockType.Red);
            placedCount++;
        }

        BattleUIEvents.Instance?.NotifyStatusMessage($"Enemy placed {placedCount} junk block(s) on the grid!");
    }

    /// <summary>
    /// TransformTrayBlock: picks one of the player's three tray blocks
    /// at random and replaces it with a different random block type.
    ///
    /// Effect on gameplay: disrupts the player's planned placements,
    /// especially punishing if the player was setting up a combo.
    /// </summary>
    private void ExecuteTransformTrayBlock()
    {
        // Ask the spawner to swap a random tray slot
        bool changed = Spawner.TransformRandomTrayBlock();

        if (changed)
            BattleUIEvents.Instance?.NotifyStatusMessage("Enemy transformed one of your blocks!");
        else
            BattleUIEvents.Instance?.NotifyStatusMessage("Enemy tried to transform a block but the tray was empty.");
    }

    /// <summary>
    /// PoisonPlayer: applies a poison debuff that lasts PoisonDurationMoves.
    /// Each move the player makes while poisoned, TickDebuffs() deals
    /// (PoisonDamagePercent * MaxHP) damage to the player.
    ///
    /// Multiple poison stacks DO NOT stack — a new application simply
    /// refreshes the duration to whichever is longer.
    /// </summary>
    private void ExecutePoisonPlayer(EnemyAbility ability)
    {
        // Check if already poisoned — refresh duration if the new one is longer
        var existing = _activeDebuffs.FirstOrDefault(d =>
            d.Source == EnemyAbilityType.PoisonPlayer);

        if (existing != null)
        {
            existing.MovesRemaining = Mathf.Max(existing.MovesRemaining,
                                                    ability.PoisonDurationMoves);
            existing.PoisonDamagePercent = ability.PoisonDamagePercent;
        }
        else
        {
            _activeDebuffs.Add(new ActiveDebuff
            {
                Source = EnemyAbilityType.PoisonPlayer,
                MovesRemaining = ability.PoisonDurationMoves,
                PoisonDamagePercent = ability.PoisonDamagePercent,
            });
        }

        BattleUIEvents.Instance?.NotifyStatusMessage(
            $"You are poisoned for {ability.PoisonDurationMoves} moves! ({ability.PoisonDamagePercent * 100f:0}% HP / move)");
    }

    /// <summary>
    /// RemovePlacedBlock: erases BlocksToRemove random OCCUPIED cells from the grid.
    ///
    /// Effect on gameplay: undoes the player's work, potentially breaking
    /// a nearly-complete row or column they were building.
    /// </summary>
    private void ExecuteRemovePlacedBlock(EnemyAbility ability)
    {
        // Collect all occupied cells
        var occupiedCells = new List<Vector2Int>();
        var snapshot = Grid.GetGridSnapshot();

        for (int c = 0; c < Grid.Columns; c++)
            for (int r = 0; r < Grid.Rows; r++)
            {
                if (snapshot[c, r] != null)
                    occupiedCells.Add(new Vector2Int(c, r));
            }

        if (occupiedCells.Count == 0)
        {
            BattleUIEvents.Instance?.NotifyStatusMessage("Enemy tried to remove a block but the grid was empty.");
            return;
        }

        int removed = 0;
        while (removed < ability.BlocksToRemove && occupiedCells.Count > 0)
        {
            int idx = Random.Range(0, occupiedCells.Count);
            Vector2Int pos = occupiedCells[idx];
            occupiedCells.RemoveAt(idx);

            Grid.ClearCell(pos.x, pos.y);   // new public method added to BlockGrid
            removed++;
        }

        BattleUIEvents.Instance?.NotifyStatusMessage($"Enemy removed {removed} block(s) from the grid!");
    }

    /// <summary>
    /// DebuffPlayerStat: reduces one or more player stats by a percentage.
    /// The exact amounts removed are recorded in the ActiveDebuff so they
    /// can be restored precisely when the debuff expires in TickDebuffs().
    ///
    /// Example: DebuffedStats = Damage | Speed, DebuffAmount = 0.25
    ///   → Damage reduced by 25% of its current value.
    ///   → Speed  reduced by 25% of its current value.
    ///   Both are restored after DebuffDurationMoves player moves.
    /// </summary>
    private void ExecuteDebuffPlayerStat(EnemyAbility ability)
    {
        var pd = GameManager.Instance.PlayerData;
        var debuff = new ActiveDebuff
        {
            Source = EnemyAbilityType.DebuffPlayerStat,
            MovesRemaining = ability.DebuffDurationMoves,
            AffectedStats = ability.DebuffedStats,
        };

        // Apply each flagged stat reduction and record how much was removed
        if (ability.DebuffedStats.HasFlag(DebuffTargetFlags.Damage))
        {
            float amount = pd.BaseDamage * ability.DebuffAmount;
            pd.BaseDamage -= amount;
            debuff.RemovedAmounts[DebuffTargetFlags.Damage] = amount;
        }

        if (ability.DebuffedStats.HasFlag(DebuffTargetFlags.Speed))
        {
            // Speed is an int; round the reduction
            int amount = Mathf.Max(1, Mathf.RoundToInt(pd.Speed * ability.DebuffAmount));
            pd.Speed = Mathf.Max(1, pd.Speed - amount);
            debuff.RemovedAmounts[DebuffTargetFlags.Speed] = amount;
        }

        if (ability.DebuffedStats.HasFlag(DebuffTargetFlags.Defense))
        {
            float amount = pd.Defense * ability.DebuffAmount;
            pd.Defense -= amount;
            debuff.RemovedAmounts[DebuffTargetFlags.Defense] = amount;
        }

        if (ability.DebuffedStats.HasFlag(DebuffTargetFlags.Luck))
        {
            float amount = pd.Luck * ability.DebuffAmount;
            pd.Luck = Mathf.Max(0f, pd.Luck - amount);
            debuff.RemovedAmounts[DebuffTargetFlags.Luck] = amount;
        }

        if (ability.DebuffedStats.HasFlag(DebuffTargetFlags.MaxHP))
        {
            int amount = Mathf.RoundToInt(pd.MaxHP * ability.DebuffAmount);
            pd.MaxHP = Mathf.Max(1, pd.MaxHP - amount);
            pd.CurrentHP = Mathf.Min(pd.CurrentHP, pd.MaxHP);  // cap HP to new max
            debuff.RemovedAmounts[DebuffTargetFlags.MaxHP] = amount;
        }

        _activeDebuffs.Add(debuff);

        BattleUIEvents.Instance?.NotifyStatusMessage(
            $"Your stats were debuffed for {ability.DebuffDurationMoves} moves!");
    }

    /// <summary>
    /// BlindPlayer: adds a blind debuff for BlindDurationMoves moves.
    /// While blinded, HandleClear() skips all damage calculations so the
    /// player clears lines but deals zero damage to enemies.
    ///
    /// Re-applying blind while already blinded refreshes the duration.
    /// </summary>
    private void ExecuteBlindPlayer(EnemyAbility ability)
    {
        var existing = _activeDebuffs.FirstOrDefault(d =>
            d.Source == EnemyAbilityType.BlindPlayer);

        if (existing != null)
            existing.MovesRemaining = Mathf.Max(existing.MovesRemaining,
                                                ability.BlindDurationMoves);
        else
            _activeDebuffs.Add(new ActiveDebuff
            {
                Source = EnemyAbilityType.BlindPlayer,
                MovesRemaining = ability.BlindDurationMoves,
            });

        BattleUIEvents.Instance?.NotifyStatusMessage(
            $"You are blinded for {ability.BlindDurationMoves} moves! Clears deal no damage.");
    }

    // ── Debuff tick (called once per player move) ──────────────────────────

    /// <summary>
    /// Advances every active debuff by one player move.
    ///
    /// Called at the START of HandleBlockPlaced() so the debuff applies on
    /// the move the player just made before we check if it has expired.
    ///
    /// Order of operations per debuff:
    ///   1. Apply ongoing effect (poison damage).
    ///   2. Decrement MovesRemaining.
    ///   3. If MovesRemaining hits 0 → expire: restore any stat reductions.
    /// </summary>
    private void TickDebuffs()
    {
        // Iterate backwards so we can safely remove expired entries
        for (int i = _activeDebuffs.Count - 1; i >= 0; i--)
        {
            var debuff = _activeDebuffs[i];

            // ── Apply ongoing effects ──────────────────────────────────────
            if (debuff.Source == EnemyAbilityType.PoisonPlayer)
            {
                int poisonDamage = Mathf.Max(1, Mathf.RoundToInt(
                    GameManager.Instance.PlayerData.MaxHP * debuff.PoisonDamagePercent));

                ApplyDamageToPlayer(poisonDamage);
                BattleUIEvents.Instance?.NotifyStatusMessage(
                    $"Poison deals {poisonDamage} damage! ({debuff.MovesRemaining - 1} moves left)");
            }

            // ── Tick the duration down ────────────────────────────────────
            debuff.MovesRemaining--;

            // ── Expire if duration reached 0 ──────────────────────────────
            if (debuff.MovesRemaining <= 0)
            {
                ExpireDebuff(debuff);
                _activeDebuffs.RemoveAt(i);
            }
        }

        // Refresh UI after all ticks
        StatusDisplay?.Refresh(_activeDebuffs);
    }

    /// <summary>
    /// Reverses any stat changes made by a debuff when it expires.
    /// Only DebuffPlayerStat actually modifies stats — the others are
    /// pure flag-based effects that simply stop applying once removed.
    /// </summary>
    private void ExpireDebuff(ActiveDebuff debuff)
    {
        if (debuff.Source != EnemyAbilityType.DebuffPlayerStat) return;

        var pd = GameManager.Instance.PlayerData;

        // Restore each stat by the exact amount that was removed
        if (debuff.RemovedAmounts.TryGetValue(DebuffTargetFlags.Damage, out float dmg))
            pd.BaseDamage += dmg;

        if (debuff.RemovedAmounts.TryGetValue(DebuffTargetFlags.Speed, out float spd))
            pd.Speed += (int)spd;

        if (debuff.RemovedAmounts.TryGetValue(DebuffTargetFlags.Defense, out float def))
            pd.Defense += def;

        if (debuff.RemovedAmounts.TryGetValue(DebuffTargetFlags.Luck, out float lck))
            pd.Luck = Mathf.Min(1f, pd.Luck + lck);

        if (debuff.RemovedAmounts.TryGetValue(DebuffTargetFlags.MaxHP, out float hp))
        {
            pd.MaxHP += (int)hp;
            // Do NOT restore CurrentHP — losing max HP is a resource cost
        }

        BattleUIEvents.Instance?.NotifyStatusMessage("A debuff has expired. Stats restored.");
    }

    // ── Targeting ─────────────────────────────────────────────────────────

    public void SetTarget(Enemy target)
    {
        if (_currentTarget != null) _currentTarget.SetTargeted(false);
        _currentTarget = target;
        if (_currentTarget != null) _currentTarget.SetTargeted(true);
    }

    public void OnEnemyClicked(Enemy enemy)
    {
        if (enemy.IsAlive) SetTarget(enemy);
    }

    // ── Powers ────────────────────────────────────────────────────────────

    public void UseBomb(int col, int row)
    {
        var pd = GameManager.Instance.PlayerData;
        if (pd.BombCount <= 0) return;
        pd.BombCount--;

        int bombDamage = Mathf.RoundToInt(pd.BaseDamage * 3f);
        if (_currentTarget != null && _currentTarget.IsAlive)
        {
            _currentTarget.TakeDamage(bombDamage);
            DamageNumbers?.Spawn(_currentTarget.EffectiveDamageAnchor, bombDamage, DamageNumberKind.EnemyDamage);
        }

        RefreshTotalEnemyHP();
    }

    public void UseOmniBlock()
    {
        var pd = GameManager.Instance.PlayerData;
        if (pd.OmniCount <= 0) return;
        pd.OmniCount--;
        Spawner.OpenOmniSelector();
    }

    public void UseHealthPotion()
    {
        var pd = GameManager.Instance.PlayerData;
        if (pd.PotionCount <= 0) return;
        pd.PotionCount--;

        int healAmount = Mathf.RoundToInt(pd.MaxHP * 0.5f);
        pd.CurrentHP = Mathf.Min(pd.MaxHP, pd.CurrentHP + healAmount);
        RefreshPlayerUI();

        DamageNumbers?.Spawn(PlayerDamageAnchor, healAmount, DamageNumberKind.Heal);
    }

    // ── Damage calculation ─────────────────────────────────────────────────

    private float CalculateDamage(ClearResult result)
    {
        float baseDmg = GameManager.Instance.PlayerData.BaseDamage;
        float dmg = baseDmg * result.TotalLinesCleared;
        if (result.WasFullClear) dmg *= 1.5f;
        return dmg;
    }

    // ── Player block effects ───────────────────────────────────────────────

    private BlockEffect GetActiveEffect(BlockType bType)
    {
        var bindings = GameManager.Instance.PlayerData.BlockBindings;
        var binding = bindings?.Find(b => b.BlockTypeIndex == (int)bType);

        if (binding != null && !string.IsNullOrEmpty(binding.ActiveCardId))
        {
            var card = AbilityDB?.GetCard(binding.ActiveCardId);
            if (card != null) return card.GrantedEffect;
        }
        return BlockEffect.None;
    }

    private void ApplyEffect(BlockEffect effect, ClearResult result)
    {
        var target = _currentTarget;
        var pd = GameManager.Instance.PlayerData;

        switch (effect)
        {
            case BlockEffect.Burn:
                Debug.Log("Burn applied! (wire up DoT component here)");
                break;
            case BlockEffect.Freeze:
                target?.ApplyFreeze(2);
                break;
            case BlockEffect.Poison:
                Debug.Log("Poison on enemy applied! (wire up DoT component here)");
                break;
            case BlockEffect.Shield:
                Debug.Log("Shield activated! (wire up shield component here)");
                break;
            case BlockEffect.Heal:
                int healAmount = 10;
                pd.CurrentHP = Mathf.Min(pd.MaxHP, pd.CurrentHP + healAmount);
                RefreshPlayerUI();
                DamageNumbers?.Spawn(PlayerDamageAnchor, healAmount, DamageNumberKind.Heal);
                break;
            case BlockEffect.Stun:
                target?.ApplyFreeze(999);
                break;
            case BlockEffect.Amplify:
                _amplifyActive = true;
                break;
            case BlockEffect.GoldBonus:
                GameManager.Instance.AddGold(5 * result.TotalLinesCleared);
                break;
        }
    }

    // ── Player health ──────────────────────────────────────────────────────

    private void ApplyDamageToPlayer(int damage)
    {
        var pd = GameManager.Instance.PlayerData;
        pd.CurrentHP = Mathf.Max(0, pd.CurrentHP - damage);
        RefreshPlayerUI();

        // Every source of player damage (enemy attacks, poison ticks, etc.)
        // routes through this single method, so hooking the popup here
        // covers all of them without needing to touch each call site.
        DamageNumbers?.Spawn(PlayerDamageAnchor, damage, DamageNumberKind.PlayerDamage);

        if (pd.CurrentHP <= 0) EndBattle(playerWon: false);
    }

    // ── Win / Loss ─────────────────────────────────────────────────────────

    private void EndBattle(bool playerWon)
    {
        if (_battleOver) return;
        _battleOver = true;

        if (playerWon)
        {
            int goldReward = 50 + _stageNumber * 10;
            GameManager.Instance.AddGold(goldReward);

            // Advance both PlayerData.CurrentStage AND GameManager.CurrentStage.
            // PlayerData.CurrentStage is the saved value used by the map screen.
            // GameManager.CurrentStage is the live value BattleHUD reads when
            // the Next Stage button calls StartBattle(GameManager.CurrentStage).
            // Without syncing both, OnNextStage() would restart the same stage.
            int nextStage = _stageNumber + 1;
            GameManager.Instance.PlayerData.CurrentStage = nextStage;
            GameManager.Instance.CurrentStage = nextStage;

            // Restore full HP so the player starts the next stage at full health.
            GameManager.Instance.PlayerData.CurrentHP = GameManager.Instance.PlayerData.MaxHP;

            GameManager.Instance.SaveGame();
        }

        // ── Clean up all remaining tray blocks ───────────────────────────────
        // DraggableBlock objects that were sitting in the tray, or that were
        // mid-drag when the battle ended, keep existing in the scene unless we
        // explicitly destroy them. Any block that is currently being dragged
        // has already been reparented to the root Canvas (in OnBeginDrag), so
        // it floats above the result panel and remains draggable — which looks
        // wrong and confusing. We destroy everything owned by the spawner here.
        CleanUpTrayBlocks();

        // Also clear any placement preview that might still be showing on the grid.
        GridRenderer?.ClearPreview();

        // Clear all debuffs so they don't carry over via PlayerData.
        _activeDebuffs.Clear();
        StatusDisplay?.Refresh(_activeDebuffs);

        BattleUIEvents.Instance?.ShowResultScreen(playerWon, _stageNumber);
    }

    /// <summary>
    /// Destroys every DraggableBlock GameObject currently in the tray slots
    /// and any that were reparented to the Canvas during an active drag.
    /// Also disables the Spawner so no new blocks can appear after battle ends.
    /// </summary>
    private void CleanUpTrayBlocks()
    {
        if (Spawner == null) return;

        // Destroy any blocks still visually sitting inside TraySlots
        if (Spawner.TraySlots != null)
        {
            foreach (var slot in Spawner.TraySlots)
            {
                if (slot == null) continue;
                foreach (Transform child in slot)
                    Destroy(child.gameObject);
            }
        }

        // Also search the whole scene for any DraggableBlock that escaped its
        // tray slot during an active drag (reparented to root Canvas in OnBeginDrag).
        // We include inactive objects in case any block was disabled mid-drag.
        var allDraggables = FindObjectsByType<DraggableBlock>(FindObjectsInactive.Include);
        foreach (var block in allDraggables)
            Destroy(block.gameObject);

        // Disable the spawner so it cannot generate new blocks after battle ends.
        Spawner.gameObject.SetActive(false);
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private bool HasUsablePowers()
    {
        var pd = GameManager.Instance.PlayerData;
        return pd.BombCount > 0 || pd.OmniCount > 0 || pd.PotionCount > 0;
    }

    private void RefreshPlayerUI()
    {
        var pd = GameManager.Instance.PlayerData;
        float fill = pd.MaxHP > 0 ? (float)pd.CurrentHP / pd.MaxHP : 0f;
        if (PlayerHealthBarFill) PlayerHealthBarFill.fillAmount = fill;
        if (PlayerHPLabel) PlayerHPLabel.text = $"{pd.CurrentHP}/{pd.MaxHP}";
    }

    private void RefreshTotalEnemyHP()
    {
        int total = _enemies.Where(e => e.IsAlive).Sum(e => e.CurrentHP);
        if (TotalEnemyHPLabel) TotalEnemyHPLabel.text = $"Enemy HP: {total}";
    }

    private void OnDestroy()
    {
        Grid.OnLinesCleared -= HandleClear;
        Grid.OnBlockPlaced -= HandleBlockPlaced;
    }
}

// ─────────────────────────────────────────────────────────────────────────────

/// <summary>Defines which enemies appear in one stage.</summary>
[System.Serializable]
public class StageConfig
{
    public string StageName;
    public List<EnemyDefinition> Enemies;
    public Sprite BackgroundSprite;
}