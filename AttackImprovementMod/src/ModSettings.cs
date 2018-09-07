using System;

namespace Sheepy.BattleTechMod.AttackImprovementMod {

   public class ModSettings {

      //
      [ JsonSection( "User Interfaces" ) ]
      //

      [ JsonComment( new string[]{
        "Fix Multi-Target cancelling so that you can cancel target by target without leaving MT mode.  Default true.",
        "You can still quickly switch out of Multi-Target by pressing another action." } ) ]
      public bool FixMultiTargetBackout = true;

      [ JsonComment( "Fix the bug that once you attacked an headshot immune enemy, all mechs will be immune from headshots from the same direction until you load game." ) ]
      public bool FixGreyHeadDisease = true;

      [ JsonComment( "Fix the bug that rear paper doll is incorrectly showing front structure.  Default true." ) ]
      public bool FixPaperDollRearStructure = true;

      [ JsonComment( "Show structural damage through armour.  i.e. When an armoured location is damaged, it will be displayed in a stripped pattern.  Default true." ) ]
      public bool ShowUnderArmourDamage = true;

      [ JsonComment( "Show tonnage in selection panel (bottom left) and target panel (top).  Mech class will be shortened.  Default false because it's too dense." ) ]
      public bool ShowUnitTonnage = false;

      [ JsonComment( "Show heat and stability number in selection panel (bottom left) and target panel (top).  Default true." ) ]
      public bool ShowHeatAndStab = true;

      [ JsonComment( "Fix the issue that walk / sprint does not project line of sight / fire at same height and may leads to different results." ) ]
      public bool FixLosPreviewHeight = true;

      [ JsonComment( "Show ammo count in the list of components when you mouseover the paper doll.  Default true for friends." ) ]
      public bool ShowAmmoInTooltip = true;
      public bool ShowEnemyAmmoInTooltip = false;

      [ JsonComment( "Set armour colour of floating bar of Player / Enemy / Ally.  Default \"cyan\" for player, \"teal\" for ally.  Set to empty to not change." ) ]
      public string FloatingArmorColourPlayer = "cyan";
      public string FloatingArmorColourEnemy = "";
      public string FloatingArmorColourAlly = "teal";

      /* Fix heat projection when moving into or away from terrain that affects cooldown.  Default true. *
      public bool FixHeatPrediction = true;

      /* Float heat number after jump, ranged attack, or end of turn (Brace, Melee, or DFA). Default true. *
      public bool FloatHeatAfterJump   = true;
      public bool FloatHeatAfterAttack = true;
      public bool FloatHeatAtTurnEnd   = true;

      /* Float target stability number after attack. Default true. *
      public bool FloatTargetStability = true;

      /* Float self stability number after DFA attack or end of turn. *
      public bool FloatStabilityAfterDFA  = true;
      public bool FloatStabilityAtTurnEnd = true;
      /**/

      //
      [ JsonSection( "Line of Sight" ) ]
      //

      [ JsonComment( "Make lines wider or thinner.  Default 2 and 1.5 times of game default.  Set to 0 to not mess with it." ) ]
      public decimal LOSWidth = 2;
      public decimal LOSWidthBlocked = 1.5m;

      [ JsonComment( new string[]{
        "Make obstruction marker bigger or smaller by multiplying its height and width.  Default 1.5.",
        "Set to 1 to use game default, or 0 to hide the marker." } ) ]
      public decimal LOSMarkerBlockedMultiplier = 1.5m;

      [ JsonComment( "Controls whether indirect attack lines / can't attack lines are dotted.  Default both true." ) ]
      public bool LOSIndirectDotted = true;
      public bool LOSNoAttackDotted = true;

      [ JsonComment( "Controls whether other attack lines are dotted.  Default all false." ) ]
      public bool LOSMeleeDotted = false;
      public bool LOSClearDotted = false;
      public bool LOSBlockedPreDotted  = false;
      public bool LOSBlockedPostDotted = false;

      [ JsonComment( new string[]{
      "Change fire line colour (html syntax). \"#FF0000\" is red, \"#00FF00\" is green etc.  Set to empty to leave alone.",
      "The colour orders are Front, Left, Right, Back, Prone.  Set to empty string to use game default." }  ) ]
      public string LOSMeleeColors = "#F00,#0FF,#0FF,#0F8,#F00";
      public string LOSClearColors = "#F00,#0FF,#0FF,#0F8,#F00";
      public string LOSBlockedPreColors  = "#D0F,#D0F,#D0F,#D0F,#D0F";
      public string LOSBlockedPostColors = "#C8E,#C8E,#C8E,#C8E,#C8E";
      public string LOSIndirectColors = "#F00,#0FF,#0FF,#0F8,#F00";
      public string LOSNoAttackColors = "";

      [ JsonComment( "Number of points of indirect attack lines and jump lines.  Game uses 18.  Default 48 for a smoother curve." ) ]
      public int ArcLinePoints = 48;

      [ JsonComment( "Change marker colour of the facing indicator. Colours are for Front, Left, Right, Back, Prone." ) ]
      //public string FacingMarkerPlayerColors = "#FFFF,#AAFD,#AAFD,#BFBF,#FFBF"; // Darker sides and green back
      //public string FacingMarkerEnemyColors  = "#FFFF,#AAFD,#AAFD,#FBBF,#FFBF"; // Darker sides and red back
      public string FacingMarkerPlayerColors = "#FFFF,#CFCF,#CFCF,#AFAF,#FF8F";
      public string FacingMarkerEnemyColors  = "#FFFF,#FCCF,#FCCF,#FAAF,#FF8F";
      public string FacingMarkerTargetColors = "#F41F,#F41F,#F41F,#F41F,#F41F";

      //
       [ JsonSection( "Called Shots" ) ]
      //

      [ JsonComment( new string[]{
        "Did you know you can called shot the head of headshot immune boss?",
        "You can do that before any headshot immune unit has been attacked.  But it won't have any effect.  Default true." } ) ]
      public bool FixBossHeadCalledShotDisplay = true;

      [ JsonComment( "Enable clustering effect for called shots against mechs.  Default true." ) ]
      public bool CalledShotUseClustering = true;

      [ JsonComment( new string[]{
        "Increase or decrease called shot multiplier against mech.  0 to disable called shot, 1 is original strength.",
        "Default is 0.33 to counter the effect of CalledShotClusterStrength." } ) ]
      public decimal MechCalledShotMultiplier = 0.33m;

      [ JsonComment( new string[]{
        "Increase or decrease called shot multiplier against mech.  0 to disable called shot, 1 is original strength.",
        "Default is 0.75 to balance vehicle's lower number of locations." } ) ]
      public decimal VehicleCalledShotMultiplier = 0.75m;

      [ JsonComment( "Override called shot percentage display of mech locations to show modded shot distribution. Default true." ) ]
      public bool ShowRealMechCalledShotChance = true;

      [ JsonComment( "Override called shot percentage display of vehicle locations to show modded shot distribution. Default true." ) ]
      public bool ShowRealVehicleCalledShotChance = true;

      [ JsonComment( new string[]{
        "Format of called shot location percentages, in C# String.Format syntax.",
        "Use \"{0:0.0}%\" to *always* show one decimal, or \"{0:0.#}%\" for *up to* one decimal. Default is \"\" to leave alone." } ) ]
      public string CalledChanceFormat = "";

      //
       [ JsonSection( "Individual To Hit Modifiers" ) ]
      //

      [ JsonComment( "Modify base weapon hit chance.  -0.05 to make all base accuracy -5%, 0.1 to make them +10% etc.  Default 0." ) ]
      public decimal BaseHitChanceModifier = 0;

      [ JsonComment( "Modify base melee hit chance.  -0.05 to make all melee and DFA accuracy -5%, 0.1 to make them +10% etc.  Default 0." ) ]
      public decimal MeleeHitChanceModifier = 0;

      [ JsonComment( "Allow attacks from low ground to high ground to incur accuracy penalty.  Default true." ) ]
      public bool AllowLowElevationPenalty = true;

      [ JsonComment( "Directional to hit modifiers.  Effective only if \"Direction\" is in the modifier factor list(s).  Default front 0, side -1, back -2." ) ]
      public int ToHitMechFromFront = 0;
      public int ToHitMechFromSide = -1;
      public int ToHitMechFromRear = -2;
      public int ToHitVehicleFromFront = 0;
      public int ToHitVehicleFromSide = -1;
      public int ToHitVehicleFromRear = -2;

      [ JsonComment( "Hit modifier after jumping, added on top of movement modifier (if any).  Effective only if \"Jumped\" is in the modifier factor list(s).  Default 0." ) ]
      public int ToHitSelfJumped = 0;

      //
       [ JsonSection( "Net To Hit Modifiers" ) ]
      //

      [ JsonComment( "Allow bonus total modifier to increase hit chance.  Default true." ) ]
      public bool AllowNetBonusModifier = true;

      //[ JsonComment( "Fix potential target height discrepancy between modifier breakdown and total accuracy.  Default true." ) ]
      //public bool FixModifierTargetHeight = true;

      [ JsonComment( new string[]{
        "Step of hit chance.  Game default is 0.05, or 5%.  Hit chance is always rounded down.",
        "Default 0 to remove hit chance step, so that odd gunnery stats can enjoy their +2.5% hit chance." } ) ]
      public decimal HitChanceStep = 0;

      [ JsonComment( new string[]{
        "Max and min hit chance after all modifiers but before roll correction. Default 0.95 and 0.05, same as game default.",
        "Note that 100% hit chance (max) may still miss if roll correction is enabled." } ) ]
      public decimal MaxFinalHitChance = 0.95m;
      public decimal MinFinalHitChance = 0.05m;

      [ JsonComment( "Make hit chance modifier has diminishing return rather than simple add and subtract.  Default false." ) ]
      public bool DiminishingHitChanceModifier = false;

      [ JsonComment( new string[]{
        "Diminishing Bonus: 2-Base^(Bonus/Divisor).  Default 2-0.8^(Bonus/6) and caps at +16.",
        "Example: +3 Bonus @ 80% Base ToHit == 1.1 x 0.8 == 88% Hit" } ) ]
      public decimal DiminishingBonusPowerBase = 0.8m;
      public decimal DiminishingBonusPowerDivisor = 6m;
      public int DiminishingBonusMax = 16;

      [ JsonComment( new string[]{
        "Diminishing Penalty: Base^(Penalty/Divisor).  Default 0.8^(Penalty/3.3) and caps at +32.",
        "Example: +6 Penalty @ 80% Base ToHit == 67% x 0.8 == 53% Hit" } ) ]
      public decimal DiminishingPenaltyPowerBase = 0.8m;
      public decimal DiminishingPenaltyPowerDivisor = 3.3m;
      public int DiminishingPenaltyMax = 32;

      [ JsonComment( new string[]{
        "Specify set of hit modifiers of ranged attacks. Leave empty to keep it unchanged.  Order and letter case does not matter.",
        "Default \"ArmMounted, Direction, Height, Indirect, Inspired, Jumped, LocationDamage, Obstruction, Precision, Range, Refire, SelfHeat, SelfStoodUp, SelfTerrain, SensorImpaired, SensorLock, Sprint, TargetEffect, TargetEvasion, TargetProne, TargetShutdown, TargetSize, TargetTerrain, Walked, WeaponAccuracy, WeaponDamage\".",
        "You can remove some options or replace SelfTerrain and TargetTerrain with SelfTerrainMelee and TargetTerrainMelee." } ) ]
      public string RangedAccuracyFactors = "ArmMounted, Direction, Height, Indirect, Inspired, Jumped, LocationDamage, Obstruction, Precision, Range, Refire, SelfHeat, SelfStoodUp, SelfTerrain, SensorImpaired, SensorLock, Sprint, TargetEffect, TargetEvasion, TargetProne, TargetShutdown, TargetSize, TargetTerrain, Walked, WeaponAccuracy, WeaponDamage";

      [ JsonComment( new string[]{
        "Specify set of hit modifiers of melee and DFA attacks. Leave empty to keep it unchanged.  Order and letter case does not matter.",
        "Default \"DFA, Direction, Height, Inspired, Jumped, SelfChassis, SelfHeat, SelfStoodUp, SelfTerrainMelee, Sprint, TargetEffect, TargetEvasion, TargetProne, TargetShutdown, TargetSize, TargetTerrainMelee, Walked, WeaponAccuracy\".",
        "Other options are ArmMounted, Obstruction, Refire, SelfTerrain, SensorImpaired, TargetTerrain." } ) ]
      public string MeleeAccuracyFactors = "DFA, Direction, Height, Inspired, Jumped, SelfChassis, SelfHeat, SelfStoodUp, SelfTerrainMelee, Sprint, TargetEffect, TargetEvasion, TargetProne, TargetShutdown, TargetSize, TargetTerrainMelee, Walked, WeaponAccuracy";

      //
      [ JsonSection( "To Hit Rolls" ) ]
      //

      [ JsonComment( new string[]{
        "Increase or decrease roll correction strength.  0 to disable roll correction, 1 is original strength, max is 2 for double strength.",
        "Default is 0.5 for less correction." } ) ]
      public decimal RollCorrectionStrength = 0.5m;

      [ JsonComment( new string[]{
        "Set miss streak breaker threshold.  Only attacks with hit rate above the threshold will add to streak breaker.",
        "Default is 0.5, same as game default.  Set to 1 to disable miss streak breaker." } ) ]
      public decimal MissStreakBreakerThreshold = 0.5m;

      [ JsonComment( new string[]{
        "Set miss streak breaker divider.  Set to negative integer or zero to make it a constant bonus, e.g. -5 = 5% bonus per miss.",
        "Otherwise, MissStreakBreakerThreshold is deduced from triggering attack's hit rate, then divided by this much, then added to streak breaker's chance modifier.",
        "Default is 5, same as game default." } ) ]
      public decimal MissStreakBreakerDivider = 5;

      //
       [ JsonSection( "Modifiers Preview" ) ]
      //

      [ JsonComment( "Display base hit chance in weapon mouseover hint.  Default true." ) ]
      public bool ShowBaseHitchance = true;

      [ JsonComment( "Apply self moved modifier during preview, before the move actually take place.  Default true." ) ]
      public bool FixSelfSpeedModifierPreview = true;

      [ JsonComment( "Show corrected hit chance in weapon panel, instead of original (fake) hit chance, before streak breaker.  Default false." ) ]
      public bool ShowCorrectedHitChance = false;

      [ JsonComment( new string[]{
        "Format of called shot location percentages, in C# String.Format syntax.",
        "Game default is \"{0:0}%\". Use \"{0:0.0}%\" to *always* show one decimal, or \"{0:0.#}%\" for *up to* one decimal.",
        "Default is \"\", which will use \"{0:0.#}%\" if HitChanceStep is 0 and DiminishingHitChanceModifier is false, otherwise leave alone." } ) ]
      public string HitChanceFormat = "";

      //
      [ JsonSection( "Melee and DFA" ) ]
      //

      [ JsonComment( "Allow all possible melee / DFA attack positions." ) ]
      public bool IncreaseMeleePositionChoice = true;
      public bool IncreaseDFAPositionChoice = true;

      [ JsonComment( "Break the restriction that one must stay still to melee adjacent mech." ) ]
      public bool UnlockMeleePositioning = true;

      /* [ JsonComment( "Allow melee attack on sprint.  Default false." ) ]
      public bool AllowChargeAttack = false;

      [ JsonComment( "Fire support weapon after sprint.  Default false." ) ]
      public bool FireSupportWeaponOnCharge = false;

      /* [ JsonComment( "Allow DFA called shot on vehicles." ) ]
      public bool AllowDFACalledShotVehicle = true;
      /**/

      //
      [ JsonSection( "Critical Hits" ) ]
      //

      [ JsonComment( "Skip critical checks if target is dead.  Default true.  Applies to vehicle and turret too." ) ]
      public bool SkipCritingDeadMech = true;

      [ JsonComment( new string[] {
        "Overrides AICritChanceBaseMultiplier in CombatGameConstants.json.  Default 0.2, same as game.",
        "Set to 1 for same crit chance as players, or set to 0 to prevent enemy or allies from dealing crit." } ) ]
      public decimal CritChanceEnemy = 0.2m;
      public decimal CritChanceAlly = 1;

      [ JsonComment( "Critical rate on vehicles, relative to mech.  Set to 0 to disable." ) ]
      public decimal CriChanceVsVehicle = 1;

      [ JsonComment( "Critical rate on turret, relative to mech.  Set to 0 to disable." ) ]
      public decimal CritChanceVsTurret = 1;

      [ JsonComment( "Crit is now checked on final damaged location by following damage transfer.  Default true." ) ]
      public bool CritFollowDamageTransfer = true;

      [ JsonComment( "Fix the case where a location with full sctructure but zero armour can be crit'ed.  Default true." ) ]
      public bool FixFullStructureCrit = true;

      [ JsonComment( new string[]{
        "A weapon must deal this much total damage to a location for through armour crit to roll.  Default 9.  Set to 0 for no threshold.",
        "A number between 0 and 1 (exclusive) means a fraction of max armour, 0 and -1 means a fraction of current armour,",
        "while 1 and above means a fixed damage threshold." } ) ]
      public decimal ThroughArmorCritThreshold = 9;

      [ JsonComment( new string[]{
        "Base crit chance of a location with zero armour but full structure.  Works by fully replacing mech crit system.",
        "Set to 0 to disable through armour critical.  Default 0.  Can be 0 to 1, e.g. 0.2 for 20%.",
        "When logging through armour crits, the Max HP column logs the max armour." } ) ]
      public decimal CritChanceZeroArmor = 0;

      [ JsonComment( new string[]{
        "Base crit chance of a location with full armour.  Must not be higher than ThroughArmorCritChanceZeroArmor.",
        "Actual through armour crit rate is between this and zero armour chance.  Default 0.  Can be -1 to 1, e.g. 0.1 for 10%.",
        "If negative, crit will not happens until armour is sufficiently weakened." } ) ]
      public decimal CritChanceFullArmor = 0;

      [ JsonComment( "Base crit chance of a structurally hit location if it is at zero structure, can be 0 to 1.  Default 1, same as game." ) ]
      public decimal CritChanceZeroStructure = 1;

      [ JsonComment( "Base crit chance of a structurally hit location if it is at full structure, can be -1 to 1.  Default 0, same as game." ) ]
      public decimal CritChanceFullStructure = 0;

      [ JsonComment( new string[] {
        "Min crit chance of a structurally hit location, after factoring structure% but before multiplier.  Default 0.5, same as game.",
        "This setting simply overrides ResolutionConstants.MinCritChance in CombatGameConstants.json." } ) ]
      public decimal CritChanceMin = 0.5m;

      [ JsonComment( "Max crit chance of a structurally hit location, after factoring structure% but before multiplier.  Default 1, same as game." ) ]
      public decimal CritChanceMax = 1;

      [ JsonComment( "Ignore (reroll) destroyed component when rolling crit." ) ]
      public bool CritIgnoreDestroyedComponent = false;

      [ JsonComment( "Ignore (reroll) empty slots when rolling crit." ) ]
      public bool CritIgnoreEmptySlots = false;

      [ JsonComment( "Roll on next damage transfer location if a location has nothing to crit." ) ]
      public bool CritLocationTransfer = false;

      [ JsonComment( "Deduce rolled crit from crit% and keep rolling crit until crit% is zero." ) ]
      public bool MultupleCrits = false;

      [ JsonComment( "Set to true to kill turret and vehicles when their ammo explodes.  Default true." ) ]
      public bool AmmoExplosionKillTurret = true;
      public bool AmmoExplosionKillVehicle = true;

      //
      [ JsonSection( "Hit Resolution" ) ]
      //

      [ JsonComment( "Yang has improved autoloader's algorithm to balance ammo draw to minimise ammo explosion chance.  Default true for friends." ) ]
      public bool BalanceAmmoConsumption = true;
      public bool BalanceEnemyAmmoConsumption = false;

      [ JsonComment( "When an ammo is useless, such as because the weapon is destroyed, eject the ammo at end of turn if not prone.  Default true for friends." ) ]
      public bool AutoJettisonAmmo = true;
      public bool AutoJettisonEnemyAmmo = false;

      [ JsonComment( "Increase hit distribution precision for degrading called shots.  Default true." ) ]
      public bool FixHitDistribution = true;

      [ JsonComment( "If a location would become a zombie part with zero hp, make sure it is destroyed instead. Default true." ) ]
      public bool KillZeroHpLocation = true;

      //
      [ JsonSection( "Logging" ) ]
      //

      [ JsonComment( new string[]{
        "Log attack info to \"Log_Attack.txt\", for copy and paste to Excel to make it human readable.",
        "Setting can be \"None\", \"Attack\", \"Shot\", \"Location\", \"Damage\", \"Critical\", or \"All\", from simplest to heaviest.  Default \"None\".",
        "\"All\" is currently same as \"Critical\", but more levels may be added in future.  Letter case does not matter." } ) ]
      public string AttackLogLevel = "All";

      [ JsonComment( "Format of attack log.  Can be \"csv\", \"tsv\", or \"txt\" (same as tsv). Default \"csv\"." ) ]
      public string AttackLogFormat = "csv";

      [ JsonComment( new string[] {
        "How many old attack log to keep.  Logs are archived on game launch, if attack log is enabled." +
        "Default is 4 for 4MB.  Set to 0 to not keep any old logs." }  ) ]
      public int AttackLogArchiveMaxMB = 4;

      [ JsonComment( "Location of mod log and roll log.  Default is \"\" to put them in mod folder.  Relative path would be relative to BATTLETECH exe." ) ]
      public string LogFolder = "";

      [ JsonComment( "Used to determine which settings should be updated and how.  Please do not change." ) ]
      public int? SettingVersion = null;


      [ Obsolete( "[v2.3 pre 20180827] Default 0. Replaced by CritChanceZeroArmor." ) ]
      public decimal? ThroughArmorCritChanceZeroArmor = null;
      [ Obsolete( "[v2.3 pre 20180827] Default 0. Replaced by CritChanceFullArmor." ) ]
      public decimal? ThroughArmorCritChanceFullArmor = null;

      [ Obsolete( "[v2.0] Default empty. Replaced by LOSMeleeColors." ) ]
      public string LOSMeleeColor = null;
      [ Obsolete( "[v2.0] Default empty. Replaced by LOSClearColors." ) ]
      public string LOSClearColor = null;
      [ Obsolete( "[v2.0] Default empty, except Pre = #D0F. Replaced by LOSBlockedPreColors." ) ]
      public string LOSBlockedPreColor  = null;
      [ Obsolete( "[v2.0] Default empty, except Post = #C8E. Replaced by LOSBlockedPostColors." ) ]
      public string LOSBlockedPostColor = null;
      [ Obsolete( "[v2.0] Default empty. Replaced by LOSIndirectColors." ) ]
      public string LOSIndirectColor = null;
      [ Obsolete( "[v2.0] Default empty. Replaced by LOSNoAttackColors." ) ]
      public string LOSNoAttackColor = null;
      [ Obsolete( "[v2.0] Default false. Replaced by AttackLogArchiveMaxMB." ) ]
      public bool? PersistentLog = false;

      [ Obsolete( "[v2.0-rc] Default true.  Replaced by ShowUnderArmourDamage." ) ]
      public bool? PaperDollDivulgeUnderskinDamage = null;
      [ Obsolete( "[v2.0-rc] Default true.  Replaced by KillZeroHpLocation." ) ]
      public bool? FixNonIntegerDamage = null;
      [ Obsolete( "[v2.0-20170712] Default 2.  Replaced by LOSWidth." ) ]
      public decimal? LOSWidthMultiplier = null;
      [ Obsolete( "[v2.0-20170712] Default 3.  Replaced by LOSWidthBlocked." ) ]
      public decimal? LOSWidthBlockedMultiplier = null;

      [ Obsolete( "[v1.0] Default false.  Renamed to ShowCorrectedHitChance." ) ]
      public bool? ShowRealWeaponHitChance = null;
      [ Obsolete( "[v1.0] Default false.  Upgraded to CalledChanceFormat." ) ]
      public bool? ShowDecimalCalledChance = null;
      [ Obsolete( "[v1.0] Default false.  Upgraded to HitChanceFormat." ) ]
      public bool? ShowDecimalHitChance = null;
      [ Obsolete( "[v1.0] Default false.  Upgraded to AttackLogLevel." ) ]
      public bool? LogHitRolls = null;
   }
}