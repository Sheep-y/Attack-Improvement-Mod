namespace Sheepy.AttackImprovementMod {

   public class ModSettings {
      /* Called shot have absolutely no effect on vehicles, probaly because there isn't enough time to implement it.
       * In the code, Mech Locations and Vehicle Locations are two separate enum, meaning there are two set of states and methods to handle hit location .
       * And vechicle's called shot method state/chain is only partially complete.  You can't reach the terminal station if any part of the rail is broken.
       *
       * Note that Vehicle called shot has no clustering; the default vehicle clustering is a real mess, and there are too few locations to cluster.
       *
       * This mod can re-enable called shot on vehicles. HBS do you need a proven remote freelancer? */
      public bool FixVehicleCalledShot = true;

      /* The game's hit distribution is verified (on ver 1.0.4) to be very imprecise because of improper float to int conversion.
       * For example it would rounded all head shot% to a bigger chances, causing almost double non-called headshot then intended.
       *
       * This mod can replace the "static HitLocation.GetHitLocation" with an improved version that has 1000x better precision, without using slow floats.
       * Enabling this fix would lower global headshot chances, but called shots chances can be fixed by enabling FixHeadCalledShot. */
      public bool FixHitDistribution = true;

      /* Mech head called shot may have lower weight multiplier than others locations because called shot borrows LRM clustering weights.
       * This mod can fix it to have same multiplier as other locations.
       *
       * Only apply if CalledShotClusterStrength is non-zero, and if ClusterChanceNeverMultiplyHead is true, and only applies to head called shots.
       * Torso shot does not regard head as adjacent and this won't increase head hit chance.
       * Enabling this fix would greatly boost called shot head shot chance, by default tempered by CalledShotWeightMultiplier. */
      public bool FixHeadCalledShot = true;

      /* Called shot against mechs are amplified by clustering rule which make them much more powerful than the game seem to believe.
       * Head hit chance is up to 40% when head multiplier is fixed.
       *
       * This setting let you tune called shot's weight multipliers.  Default is 0.33 to counter the effect of FixHeadCalledShot.
       * Set to 1.0 to disable adjustment (and a 40% chance to headshot with called shot mastery, if FixHeadCalledShot is true).
       * A modifier < 1 lowers the power of called shot, and vice versa. */
      public float MechCalledShotMultiplier = 0.33f;

      /* Called shot didn't work on vehicles without modding.
       * Once that is fixed, unmodified called shot is pretty powerful on vechicles, which does not use clustering rule.
       * This setting tries to fix that. Default 0.75 to tune down vehicle called shot. */
      public float VehicleCalledShotMultiplier = 0.75f;

      /* Override called shot percentage display of mech locations to show true shot distribution.
       * Work with any combination of fixes; disable all.of them to get the true % of the base game.
       *
       * It is possible that the game will update called shot algorithm and cause this override to become inaccurate,
       * then you may disable this override until the mod updates. */
      public bool ShowRealMechCalledShotChance = true;

      /* Override called shot percentage display of vehicle locations to show true shot distribution.
       * When the mod was authored, vehicles do not respect called shot. This override reflects the bug.
       *
       * It is possible that the game will update called shot algorithm and cause this override to become inaccurate,
       * then you may disable this override until the mod updates. */
      public bool ShowRealVehicleCalledShotChance = true;

      /* If called shot percentage display is overridden, location hit chance can be displayed to one decimal point. */
      public bool ShowDecimalCalledChance = false;

      /* BattleTech is known to adjust Hit Rolls, so that 75% hit is actually 84% and 25% is actuall 16%.
       * This roll "correction" can be disabled.
       *
       * 0.0 disables roll correction. 1.0 means use original correction, 2.0 means double strength, etc.
       * Default is 0.5 which lowers correction strength to half. */
      public float RollCorrectionStrength = 0.5f;

      /* Show the real (corrected) hit chances in weapon panel, instead of original chances. */
      public bool ShowRealWeaponHitChance = false;

      /* Override weapon hit chance display to one decimal point.
       * Can be enabled independently from RollCorrectionStrength and ShowRealWeaponHitChance,
       * for example if some other mods do their own display correction and you want to see the decimals. */
      public bool ShowDecimalHitChance = false;

      /* Enable this to log hit roll, hit correction, location roll, location weight and other info to "BATTLETECH\Mods\FixHitLocation\log_roll.txt" in tab separated value (tsv).
       * Weight of front armours and rear armours are combined to keep the log simple.
       *
       * Expect high disk activity when enabled. Can log even when all other fixes are disabled.  Default disabled. */
      public bool LogHitRolls = false;
   }

}