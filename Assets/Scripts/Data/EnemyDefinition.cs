using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// EnemyDefinition stores all BASE data for one enemy type.
/// It now also holds the list of EnemyAbility assets this enemy can use
/// on their attack turns, in addition to their normal hit.
///
/// HOW TO ADD ABILITIES:
///   1. Create an EnemyAbility asset  (Create → TileSpark → Enemy Ability).
///   2. Configure the ability in its Inspector.
///   3. Open this EnemyDefinition and drag the ability into the Abilities list.
///   Multiple abilities can be added.  On each attack turn the enemy
///   picks which ability to use based on cooldowns (handled by Enemy.cs).
/// </summary>
[CreateAssetMenu(fileName = "Enemy_New", menuName = "TileSpark/Enemy Definition")]
public class EnemyDefinition : ScriptableObject
{
    [Header("Identity")]
    public string EnemyName;
    public Sprite EnemySprite;

    [Header("Base Stats (Stage 1)")]
    public int   BaseHP     = 50;
    public float BaseAttack = 8f;   // damage dealt by the normal hit each turn
    public int   BaseSpeed  = 3;    // player moves before this enemy takes a turn

    [Header("Scaling (multiplier applied per stage above 1)")]
    [Tooltip("HP is multiplied by HPScaling^(stage-1).  1.1 = +10% per stage.")]
    public float HPScaling  = 1.1f;

    [Tooltip("Attack is multiplied by AtkScaling^(stage-1).")]
    public float AtkScaling = 1.05f;

    [Header("Special Abilities")]
    [Tooltip("List of special abilities this enemy can use on their attack turn " +
             "in addition to their normal hit.  Leave empty for a basic enemy. " +
             "Each ability has its own cooldown so they can stack up.")]
    public List<EnemyAbility> Abilities = new();

    [Tooltip("If true the enemy skips their normal hit whenever they use a " +
             "special ability (the ability IS the attack for that turn). " +
             "If false they always deal normal hit damage AND may use an ability.")]
    public bool AbilityReplacesNormalHit = false;
}
