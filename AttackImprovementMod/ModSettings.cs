namespace Sheepy.AttackImprovementMod {

   public class ModSettings {
      /* Enable Vehicle Called Shot, which the game did not implement fully. Default true. */
      public bool FixVehicleCalledShot = true;

      /* Fix hit distribution bug which increase head shot % of all attacks. Default true. */
      public bool FixHitDistribution = true;

      /* Enable clustering effect for called shots against mechs. Default true. */
      public bool CalledShotUseClustering = true;

      /* Increase or decrease called shot multiplier against mech.  0 to disable called shot, 1 is original strength.
       * Default is 0.33 to counter the effect of CalledShotClusterStrength. */
      public float MechCalledShotMultiplier = 0.33f;

      /* Increase or decrease called shot multiplier against mech.  0 to disable called shot, 1 is original strength.
       * Default is 0.75 to balance vehicle's lower number of locations. */
      public float VehicleCalledShotMultiplier = 0.75f;

      /* Override called shot percentage display of mech locations to show true shot distribution, with bug fix or not. Default true. */
      public bool ShowRealMechCalledShotChance = true;

      /* Override called shot percentage display of vehicle locations to show true shot distribution, with bug fix or not. Default true. */
      public bool ShowRealVehicleCalledShotChance = true;

      /* Display chance to one decimal IF AND ONLY IF called shot chance is overridden (above). Default false. */
      public bool ShowDecimalCalledChance = false;

      /* Increase or decrease roll correction strength. 0 to disable roll correction, 1 is original strength, 2 for double.
       * Default is 0.5 for less correction. */
      public float RollCorrectionStrength = 0.5f;

      /* Show adjusted hit chance in weapon panel, instead of original (fake) hit chance. Default false. */
      public bool ShowRealWeaponHitChance = false;

      /* Show hit chance to one decimal in weapon panel. Default false. */
      public bool ShowDecimalHitChance = false;

      /* Log attacker, weapon, hit roll, correction, location roll, location weights etc. to "BATTLETECH\Mods\FixHitLocation\log_roll.txt", for copy and paste to Excel.
       * Default disabled. */
      public bool LogHitRolls = false;
   }

}