namespace Sheepy.AttackImprovementMod {

   /* This class is used to load old settings, so that they can be upgraded to new settings */
   public class OldSettings : ModSettings {
      /* Version 1.0 to 1.0.1 */
      public bool? ShowRealWeaponHitChance = null; // bool, default false. Renamed to ShowCorrectedHitChance.
      public bool? ShowDecimalCalledChance = null; // bool, default false. Upgraded to CalledChanceFormat.
      public bool? ShowDecimalHitChance = null; // bool, default false. Upgraded to HitChanceFormat.
      public bool? LogHitRolls = null; // bool, default false. Upgraded to AttackLogLevel.
   }
}