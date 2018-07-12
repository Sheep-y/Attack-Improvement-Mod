namespace Sheepy.AttackImprovementMod {

   public class ModSettings {

      /* Fix Multi-Target cancelling so that you can cancel target by target without leaving MT mode. Default true.
         You can still quickly switch out of it by pressing another action. */
      public bool FixMultiTargetBackout = true;
      
      /* Try to fix the bug that damage is not in integer, which cause various other bugs. */
      public bool FixNonIntegerDamage = true;

      /* Show heat and stability number in selection panel (bottom left) and target panel (top).  Default true. */
      public bool ShowHeatAndStab = true;

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

      ///
      /// Line of Sight
      ///

      /* Make lines wider or thinner.  Default 2 times, 3 times, and 1.5 times. */
      public float LOSWidthMultiplier = 2f;
      public float LOSWidthBlockedMultiplier = 3f;
      public float LOSMarkerBlockedMultiplier = 1.5f;

      /* Controls whether indirect attack lines / can't attack lines are dotted. Default both true. */
      public bool LOSIndirectDotted = true;
      public bool LOSNoAttackDotted = true;
      /* Controls whether other attack lines are dotted. Default all false. */
      public bool LOSMeleeDotted = false;
      public bool LOSClearDotted = false;
      public bool LOSBlockedPreDotted  = false;
      public bool LOSBlockedPostDotted = false;

      /* Change fire line colour (html syntax). "#FF0000FF" is red, "#00FF00FF" is green etc.  Set to empty to leave alone.
         Default #D0F for blocked pre, #C8E for blocked post, and empty for the rest. */
      public string LOSMeleeColor = "";
      public string LOSClearColor = "";
      public string LOSBlockedPreColor  = "#D0F";
      public string LOSBlockedPostColor = "#C8E";
      public string LOSIndirectColor = "";
      public string LOSNoAttackColor = "";

      /* Number of points of indirect attack lines and jump lines. Game uses 18. Default 48 for a smoother curve. */
      public int ArcLinePoints = 48;

      ///
      /// Called Shots
      ///

      /* Enable Vehicle Called Shot, which the game did not implement fully. Default true. */
      public bool FixVehicleCalledShot = true;

      /* Enable clustering effect for called shots against mechs. Default true. */
      public bool CalledShotUseClustering = true;

      /* Increase or decrease called shot multiplier against mech.  0 to disable called shot, 1 is original strength.
       * Default is 0.33 to counter the effect of CalledShotClusterStrength. */
      public float MechCalledShotMultiplier = 0.33f;

      /* Increase or decrease called shot multiplier against mech.  0 to disable called shot, 1 is original strength.
       * Default is 0.75 to balance vehicle's lower number of locations. */
      public float VehicleCalledShotMultiplier = 0.75f;

      /* Override called shot percentage display of mech locations to show modded shot distribution. Default true. */
      public bool ShowRealMechCalledShotChance = true;

      /* Override called shot percentage display of vehicle locations to show modded shot distribution. Default true. */
      public bool ShowRealVehicleCalledShotChance = true;

      /* Format of called shot location percentages, in C# String.Format syntax.
       * Use "{0:0.0}%" to *always* show one decimal, or "{0:0.#}%" for *up to* one decimal. Default is "" to leave alone. */
      public string CalledChanceFormat = "";

      ///
      /// To Hit Bonus and Penalty
      ///

      /* Allow bonus total modifier to increase hit chance. Defaul true. */
      public bool AllowNetBonusModifier = true;

      /* Allow attacks from low ground to high ground to incur accuracy penalty. */
      public bool AllowLowElevationPenalty = true;

      /* Step of hit chance, range 0 to 0.2.  Game default is 0.05, or 5%.  Hit chance is always rounded down.
       * Default 0 to remove hit chance step, so that odd piloting stat can enjoy their +2.5% hit chance. */
      public float HitChanceStep = 0;

      /* Modify base weapon hit chance. -0.05 to make all base accuracy -5%, 0.1 to make them +10% etc. Default 0. */
      public float BaseHitChanceModifier = 0f;

      /* Modify base melee hit chance. -0.05 to make all melee and DFA accuracy -5%, 0.1 to make them +10% etc. Default 0. */
      public float MeleeHitChanceModifier = 0f;

      /* Max hit chance after all modifiers but before roll correction. Default 0.95, same as game default.
       * Note that 100% hit chance (max) may still miss if roll correction is enabled. */
      public float MaxFinalHitChance = 0.95f;

      /* Min hit chance after all modifiers but before roll correction. Default 0.05, same as game default. */
      public float MinFinalHitChance = 0.05f;

      /* Make hit chance modifier has diminishing return rather than simple add and subtract. Default false. */
      public bool DiminishingHitChanceModifier = false;

      /* Diminishing Bonus: 2-Base^(Bonus/Divisor), Default 2-0.75^(Bonus/6) and caps at +16.
         Example: +3 Bonus @ 80% Base ToHit == 1.1 x 0.8 == 88% Hit */
      public double DiminishingBonusPowerBase = 0.8f;
      public double DiminishingBonusPowerDivisor = 6f;
      public int DiminishingBonusMax = 16;

      /* Diminishing Penalty: Base^(Penalty/Divisor), Default 0.8^(Penalty/3.3) and caps at +32.
         Example: +6 Penalty @ 80% Base ToHit == 67% x 0.8 == 53% Hit */
      public double DiminishingPenaltyPowerBase = 0.8f;
      public double DiminishingPenaltyPowerDivisor = 3.3f;
      public int DiminishingPenaltyMax = 32;

      ///
      /// To Hit Rolls Correction
      ///

      /* Increase hit distribution precision for degrading called shots. Default true. Fix hit distribution bug on game ver 1.1.0 and below. */
      public bool FixHitDistribution = true;

      /* Increase or decrease roll correction strength. 0 to disable roll correction, 1 is original strength, max is 2 for double strength.
       * Default is 0.5 for less correction. */
      public float RollCorrectionStrength = 0.5f;

      /* Set miss streak breaker threshold. Only attacks with hit rate above the threshold will add to streak breaker.
       * Default is 0.5, same as game default. Set to 1 to disable miss streak breaker. */
      public float MissStreakBreakerThreshold = 0.5f;

      /* Set miss streak breaker divider. Set to negative integer or zero to make it a (positive) constant %.
       * Otherwise, MissStreakBreakerThreshold is deduced from triggering attack's hit rate, then divided by this much, then added to streak breaker's chance modifier.
       * Default is 5, same as game default. */
      public float MissStreakBreakerDivider = 5f;

      /* Show corrected hit chance in weapon panel, instead of original (fake) hit chance, before streak breaker. Default false. */
      public bool ShowCorrectedHitChance = false;

      /* Format of called shot location percentages, in C# String.Format syntax.
       * Game default is "{0:0}%". Use "{0:0.0}%" to *always* show one decimal, or "{0:0.#}%" for *up to* one decimal.
       * Default is "", which will use "{0:0.#}%" if HitChanceStep is 0, otherwise leave alone. */
      public string HitChanceFormat = "";

      ///
      /// Melee and DFA
      ///

      /* Allow all possible melee / DFA attack positions. */
      public bool IncreaseMeleePositionChoice = true;
      public bool IncreaseDFAPositionChoice = true;

      /* Break the restriction that one must stay still to melee adjacent mech. */
      public bool UnlockMeleePositioning = true;

      /* Allow melee attack on sprint. Default false. */
      public bool AllowChargeAttack = false;

      /* Fire support weapon after sprint.  Default false. */
      public bool FireSupportWeaponOnCharge = false;

      /* Allow DFA called shot on vehicles *
      public bool AllowDFACalledShotVehicle = true;

      /* Specify set of hit modifiers of melee and DFA attacks. Leave empty to keep it unchanged. Order and letter case does not matter.
       * Default "DFA,Height,Inspired,SelfChassis,SelfHeat,SelfStoodUp,SelfWalked,Sprint,TargetEffect,TargetEvasion,TargetProne,TargetShutdown,TargetSize,TargetTerrainMelee,WeaponAccuracy".
       * Other options are "ArmMounted,Obsruction,Refire,SelfTerrain,SensorImpaired,TargetTerrain". */
      public string MeleeAccuracyFactors = "DFA,Height,Inspired,SelfChassis,SelfHeat,SelfStoodUp,SelfWalked,Sprint,TargetEffect,TargetEvasion,TargetProne,TargetShutdown,TargetSize,TargetTerrainMelee,WeaponAccuracy";

      ///
      /// Logging
      ///

      /* Log attacker, weapon, hit roll, correction, location roll, location weights etc. to "BATTLETECH\Mods\FixHitLocation\Log_AttackRoll.txt", for copy and paste to Excel.
       * Default disabled. */
      public bool LogHitRolls = false;

      /* If true, don't clear log on mod load (game launch). */
      public bool PersistentLog = false;

      /* Location of mod log and roll log. Default is "" to put them in mod folder. Relative path would be relative to BATTLETECH exe. */
      public string LogFolder = "";
   }

}