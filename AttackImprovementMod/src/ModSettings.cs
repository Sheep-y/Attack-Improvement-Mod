using System;

namespace Sheepy.BattleTechMod.AttackImprovementMod {

   public class ModSettings {

      //
      [ JsonSection( "User Interfaces" ) ]
      //

      [ JsonComment( new String[]{
        "Fix Multi-Target cancelling so that you can cancel target by target without leaving MT mode.  Default true.",
        "You can still quickly switch out of Multi-Target by pressing another action." } ) ]
      public bool FixMultiTargetBackout = true;

      [ JsonComment( "Fix the bug that rear paper doll is incorrectly showing front structure.  Default true." ) ]
      public bool FixPaperDollRearStructure = true;

      [ JsonComment( "Show structural damage through armour.  i.e. When an armoured location is damaged, it will be displayed in a stripped pattern.  Default true." ) ]
      public bool PaperDollDivulgeUnderskinDamage = true;

      [ JsonComment( "Show tonnage in selection panel (bottom left) and target panel (top).  Mech class will be shortened.  Default false because it's too dense." ) ]
      public bool ShowUnitTonnage = false;

      [ JsonComment( "Show heat and stability number in selection panel (bottom left) and target panel (top).  Default true." ) ]
      public bool ShowHeatAndStab = true;

      [ JsonComment( "Fix the bug that walk / sprint may not accurately project correct line of sight / fire, due to a prediction point too close to the ground." ) ]
      public bool FixNonJumpLosPreview = true;

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

      [ JsonComment( "Make lines wider or thinner.  Default 3, 2, and 1.5 times.  Set to 0 to use game default." ) ]
      public float LOSWidth = 2f;
      public float LOSWidthBlocked = 1.5f;

      [ JsonComment( new String[]{
        "Make obstruction marker bigger or smaller by multiplying its height and width.  Default 1.5.",
        "Set to 1 to use game default, or 0 to hide the marker." } ) ]
      public float LOSMarkerBlockedMultiplier = 1.5f;

      [ JsonComment( "Controls whether indirect attack lines / can't attack lines are dotted. Default both true." ) ]
      public bool LOSIndirectDotted = true;
      public bool LOSNoAttackDotted = true;

      [ JsonComment( "Controls whether other attack lines are dotted. Default all false." ) ]
      public bool LOSMeleeDotted = false;
      public bool LOSClearDotted = false;
      public bool LOSBlockedPreDotted  = false;
      public bool LOSBlockedPostDotted = false;

      [ JsonComment( new String[]{
        "Change fire line colour (html syntax). \"#FF0000\" is red, \"#00FF00\" is green etc.  Set to empty to leave alone.",
        "Default \"#D0F\" for blocked pre, \"#C8E\" for blocked post, and empty for the rest.  Supports RGB and RGBA." } ) ]
      public string LOSMeleeColor = "";
      public string LOSClearColor = "";
      public string LOSBlockedPreColor  = "#D0F";
      public string LOSBlockedPostColor = "#C8E";
      public string LOSIndirectColor = "";
      public string LOSNoAttackColor = "";

      [ JsonComment( "Number of points of indirect attack lines and jump lines. Game uses 18. Default 48 for a smoother curve." ) ]
      public int ArcLinePoints = 48;

      //
       [ JsonSection( "Called Shots" ) ]
      //

      [ JsonComment( "Enable Vehicle Called Shot, which the game did not implement fully. Default true." ) ]
      public bool FixVehicleCalledShot = true;

      [ JsonComment( new String[]{
        "Did you know you can called shot the head of headshot immune boss?",
        "You can do that before any headshot immune unit has been attacked (not necessary an enemy).  And, surprise, it won't have any effect!",
        "Default true.  Disable to give yourself the false hope of headshoting the boss, provided FixGreyHeadDisease is true." } ) ]
      public bool FixBossHeadCalledShotDisplay = true;

      [ JsonComment( "Enable clustering effect for called shots against mechs. Default true." ) ]
      public bool CalledShotUseClustering = true;

      [ JsonComment( new String[]{
        "Increase or decrease called shot multiplier against mech.  0 to disable called shot, 1 is original strength.",
        "Default is 0.33 to counter the effect of CalledShotClusterStrength." } ) ]
      public float MechCalledShotMultiplier = 0.33f;

      [ JsonComment( new String[]{
        "Increase or decrease called shot multiplier against mech.  0 to disable called shot, 1 is original strength.",
        "Default is 0.75 to balance vehicle's lower number of locations." } ) ]
      public float VehicleCalledShotMultiplier = 0.75f;

      [ JsonComment( "Override called shot percentage display of mech locations to show modded shot distribution. Default true." ) ]
      public bool ShowRealMechCalledShotChance = true;

      [ JsonComment( "Override called shot percentage display of vehicle locations to show modded shot distribution. Default true." ) ]
      public bool ShowRealVehicleCalledShotChance = true;

      [ JsonComment( new String[]{
        "Format of called shot location percentages, in C# String.Format syntax.",
        "Use \"{0:0.0}%\" to *always* show one decimal, or \"{0:0.#}%\" for *up to* one decimal. Default is \"\" to leave alone." } ) ]
      public string CalledChanceFormat = "";

      //
       [ JsonSection( "To Hit Modifiers" ) ]
      //

      [ JsonComment( "Allow bonus total modifier to increase hit chance.  Default true." ) ]
      public bool AllowNetBonusModifier = true;

      [ JsonComment( "Allow attacks from low ground to high ground to incur accuracy penalty." ) ]
      public bool AllowLowElevationPenalty = true;

      [ JsonComment( new String[]{
        "Step of hit chance, range 0 to 0.2.  Game default is 0.05, or 5%.  Hit chance is always rounded down.",
        "Default 0 to remove hit chance step, so that odd piloting stat can enjoy their +2.5% hit chance." } ) ]
      public float HitChanceStep = 0;

      [ JsonComment( "Modify base weapon hit chance.  -0.05 to make all base accuracy -5%, 0.1 to make them +10% etc.  Default 0." ) ]
      public float BaseHitChanceModifier = 0f;

      [ JsonComment( "Modify base melee hit chance.  -0.05 to make all melee and DFA accuracy -5%, 0.1 to make them +10% etc.  Default 0." ) ]
      public float MeleeHitChanceModifier = 0f;

      [ JsonComment( new String[]{
        "Max and min hit chance after all modifiers but before roll correction. Default 0.95 and 0.05, same as game default.",
        "Note that 100% hit chance (max) may still miss if roll correction is enabled." } ) ]
      public float MaxFinalHitChance = 0.95f;
      public float MinFinalHitChance = 0.05f;

      [ JsonComment( "Make hit chance modifier has diminishing return rather than simple add and subtract.  Default false." ) ]
      public bool DiminishingHitChanceModifier = false;

      [ JsonComment( new String[]{
        "Diminishing Bonus: 2-Base^(Bonus/Divisor).  Default 2-0.75^(Bonus/6) and caps at +16.",
        "Example: +3 Bonus @ 80% Base ToHit == 1.1 x 0.8 == 88% Hit" } ) ]
      public double DiminishingBonusPowerBase = 0.8f;
      public double DiminishingBonusPowerDivisor = 6f;
      public int DiminishingBonusMax = 16;

      [ JsonComment( new String[]{
        "Diminishing Penalty: Base^(Penalty/Divisor).  Default 0.8^(Penalty/3.3) and caps at +32.",
        "Example: +6 Penalty @ 80% Base ToHit == 67% x 0.8 == 53% Hit" } ) ]
      public double DiminishingPenaltyPowerBase = 0.8f;
      public double DiminishingPenaltyPowerDivisor = 3.3f;
      public int DiminishingPenaltyMax = 32;

      //
      [ JsonSection( "To Hit Rolls" ) ]
      //

      [ JsonComment( new String[]{
        "Increase or decrease roll correction strength.  0 to disable roll correction, 1 is original strength, max is 2 for double strength.",
        "Default is 0.5 for less correction." } ) ]
      public float RollCorrectionStrength = 0.5f;

      [ JsonComment( new String[]{
        "Set miss streak breaker threshold.  Only attacks with hit rate above the threshold will add to streak breaker.",
        "Default is 0.5, same as game default.  Set to 1 to disable miss streak breaker." } ) ]
      public float MissStreakBreakerThreshold = 0.5f;

      [ JsonComment( new String[]{
        "Set miss streak breaker divider. Set to negative integer or zero to make it a (positive) constant %.",
        "Otherwise, MissStreakBreakerThreshold is deduced from triggering attack's hit rate, then divided by this much, then added to streak breaker's chance modifier.",
        "Default is 5, same as game default." } ) ]
      public float MissStreakBreakerDivider = 5f;

      [ JsonComment( "Show corrected hit chance in weapon panel, instead of original (fake) hit chance, before streak breaker.  Default false." ) ]
      public bool ShowCorrectedHitChance = false;

      [ JsonComment( new String[]{
        "Format of called shot location percentages, in C# String.Format syntax.",
        "Game default is \"{0:0}%\". Use \"{0:0.0}%\" to *always* show one decimal, or \"{0:0.#}%\" for *up to* one decimal.",
        "Default is \"\", which will use \"{0:0.#}%\" if HitChanceStep is 0, otherwise leave alone." } ) ]
      public string HitChanceFormat = "";

      //
      [ JsonSection( "Hit Locations" ) ]
      //

      [ JsonComment( "Increase hit distribution precision for degrading called shots.  Default true.  Fix hit distribution bug on game ver 1.1.0 and below." ) ]
      public bool FixHitDistribution = true;

      [ JsonComment( "Fix the bug that once you attacked an headshot immune enemy, all mechs will be immune from headshots from the same direction until you load game." ) ]
      public bool FixGreyHeadDisease = true;

      //
      [ JsonSection( "Melee and DFA" ) ]
      //

      [ JsonComment( "Allow all possible melee / DFA attack positions." ) ]
      public bool IncreaseMeleePositionChoice = true;
      public bool IncreaseDFAPositionChoice = true;

      [ JsonComment( "Break the restriction that one must stay still to melee adjacent mech." ) ]
      public bool UnlockMeleePositioning = true;

      [ JsonComment( "Allow melee attack on sprint.  Default false." ) ]
      public bool AllowChargeAttack = false;

      [ JsonComment( "Fire support weapon after sprint.  Default false." ) ]
      public bool FireSupportWeaponOnCharge = false;

      /* [ JsonComment( "Allow DFA called shot on vehicles." ) ]
      public bool AllowDFACalledShotVehicle = true;
      /**/

      [ JsonComment( new String[]{
        "Specify set of hit modifiers of melee and DFA attacks. Leave empty to keep it unchanged.  Order and letter case does not matter.",
        "Default \"DFA,Height,Inspired,SelfChassis,SelfHeat,SelfStoodUp,SelfWalked,Sprint,TargetEffect,TargetEvasion,TargetProne,TargetShutdown,TargetSize,TargetTerrainMelee,WeaponAccuracy\".",
        "Other options are \"ArmMounted,Obstruction,Refire,SelfTerrain,SensorImpaired,TargetTerrain\"." } ) ]
      public string MeleeAccuracyFactors = "DFA,Height,Inspired,SelfChassis,SelfHeat,SelfStoodUp,SelfWalked,Sprint,TargetEffect,TargetEvasion,TargetProne,TargetShutdown,TargetSize,TargetTerrainMelee,WeaponAccuracy";

      //
      [ JsonSection( "Damage" ) ]
      //

      [ JsonComment( new String[]{
        "Fix the bug that damage may not be in integer, which causes other bugs.  Default true.",
        "Does not retroactively fix in-battle saves with partial damage, but will not break them or be broken by them either." } ) ]
      public bool FixNonIntegerDamage = true;

      //
      [ JsonSection( "Logging" ) ]
      //

      [ JsonComment( new String[]{
        "Log attack info to \"Log_Attack.txt\", for copy and paste to Excel to make it human readable.",
        "Setting can be \"None\", \"Attack\", \"Shot\", \"Location\", \"Damage\", \"Critical\", or \"All\", from simplest to heaviest.  Default \"None\".",
        "\"All\" is currently same as \"Critical\", but more levels may be added in future.  Letter case does not matter." } ) ]
      public string AttackLogLevel = "None";

      [ JsonComment( "If true, don't clear attack log when the game launches." ) ]
      public bool PersistentLog = false;

      [ JsonComment( "Location of mod log and roll log.  Default is \"\" to put them in mod folder.  Relative path would be relative to BATTLETECH exe." ) ]
      public string LogFolder = "";




      [ Obsolete( "Default false.  Renamed to ShowCorrectedHitChance." ) ]
      public bool? ShowRealWeaponHitChance = null;

      [ Obsolete( "Default false.  Upgraded to CalledChanceFormat." ) ]
      public bool? ShowDecimalCalledChance = null;

      [ Obsolete( "Default false.  Upgraded to HitChanceFormat." ) ]
      public bool? ShowDecimalHitChance = null;

      [ Obsolete( "Default false.  Upgraded to AttackLogLevel." ) ]
      public bool? LogHitRolls = null;
   }
}