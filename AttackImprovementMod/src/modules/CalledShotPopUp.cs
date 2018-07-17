using BattleTech.UI;
using BattleTech;
using Sheepy.BattleTechMod;
using System.Collections.Generic;
using System;
using static System.Reflection.BindingFlags;

namespace Sheepy.AttackImprovementMod {
   using static HitLocation;
   using static Mod;

   public class CalledShotPopUp : BattleModModule {

      private static string CalledShotHitChanceFormat = "{0:0}%";

      public override void ModStarts () {
         if ( NullIfEmpty( ref Settings.CalledChanceFormat ) != null )
            CalledShotHitChanceFormat = Settings.CalledChanceFormat;

         if ( Settings.ShowRealMechCalledShotChance || Settings.ShowRealVehicleCalledShotChance || Settings.CalledChanceFormat != null ) {
            Type CalledShot = typeof( CombatHUDCalledShotPopUp );
            Patch( CalledShot, "set_ShownAttackDirection", typeof( AttackDirection ), null, "RecordAttackDirection" );

            if ( Settings.ShowRealMechCalledShotChance || Settings.CalledChanceFormat != null )
               Patch( CalledShot, "GetHitPercent", NonPublic, new Type[]{ typeof( ArmorLocation ), typeof( ArmorLocation ) }, "OverrideHUDMechCalledShotPercent", null );

            if ( Settings.ShowRealVehicleCalledShotChance || Settings.CalledChanceFormat != null )
               Patch( CalledShot, "GetHitPercent", NonPublic, new Type[]{ typeof( VehicleChassisLocations ), typeof( VehicleChassisLocations ) }, "OverrideHUDVehicleCalledShotPercent", null );
         }
      }

      // ============ Game States ============

      private static float ActorCalledShotBonus { get { return HUD.SelectedActor.CalledShotBonusMultiplier; } }

      private static AttackDirection AttackDirection;
      public static void RecordAttackDirection ( AttackDirection value ) {
         AttackDirection = value;
      }

      // ============ HUD Override ============

      private static Object LastHitTable;
      private static int HitTableTotalWeight;
      private static int lastCalledShotLocation;

      private static bool CacheNeedRefresh ( Object hitTable, int targetedLocation ) {
         bool result = ! Object.ReferenceEquals( hitTable, LastHitTable ) || lastCalledShotLocation != (int) targetedLocation;
         if ( result ) {
            LastHitTable = hitTable;
            lastCalledShotLocation = (int) targetedLocation;
         }
         return result;
      }

      public static bool OverrideHUDMechCalledShotPercent ( ref string __result, ArmorLocation location, ArmorLocation targetedLocation ) { try {
         Dictionary<ArmorLocation, int> hitTable = ( targetedLocation == ArmorLocation.None || ! HitLocation.CallShotClustered || ! Settings.ShowRealMechCalledShotChance )
                                                   ? Combat.HitLocation.GetMechHitTable( AttackDirection )
                                                   : CombatConstants.GetMechClusterTable( targetedLocation, AttackDirection );
         if ( CacheNeedRefresh( hitTable, (int) targetedLocation ) )
            HitTableTotalWeight = SumWeight( hitTable, targetedLocation, FixMultiplier( targetedLocation, ActorCalledShotBonus ), scale );

         int local = TryGet( hitTable, location ) * scale;
         if ( location == targetedLocation )
            local = (int)( (float) local * FixMultiplier( targetedLocation, ActorCalledShotBonus ) );

         __result = FineTuneAndFormat( hitTable, location, local, Settings.ShowRealMechCalledShotChance );
         return false;
      }                 catch ( Exception ex ) { return Error( ex ); } }

      public static bool OverrideHUDVehicleCalledShotPercent ( ref string __result, VehicleChassisLocations location, VehicleChassisLocations targetedLocation ) { try {
         if ( ! Settings.FixVehicleCalledShot || ! Settings.ShowRealVehicleCalledShotChance )
            targetedLocation = VehicleChassisLocations.None; // Disable called location if vehicle called shot is not fixed

         Dictionary<VehicleChassisLocations, int> hitTable = Combat.HitLocation.GetVehicleHitTable( AttackDirection );
         if ( CacheNeedRefresh( hitTable, (int) targetedLocation ) )
            HitTableTotalWeight = SumWeight( hitTable, targetedLocation, FixMultiplier( targetedLocation, ActorCalledShotBonus ), scale );

         int local = TryGet( hitTable, location ) * scale;
         if ( location == targetedLocation )
            local = (int)( (float) local * FixMultiplier( targetedLocation, ActorCalledShotBonus ) );

         __result = FineTuneAndFormat( hitTable, location, local, Settings.ShowRealVehicleCalledShotChance );
         return false;

      }                 catch ( Exception ex ) { return Error( ex ); } }

      // ============ Subroutines ============

      private static string FineTuneAndFormat<T> ( Dictionary<T, int> hitTable, T location, int local, bool simulate  ) {
         if ( GameHitLocationBugged && ! Settings.FixHitDistribution && simulate ) { // If hit distribution is bugged, simulate it.
            T def = default(T), last = def;
            foreach ( KeyValuePair<T, int> e in hitTable ) {
               if ( e.Value == 0 ) continue;
               if ( last.Equals( def ) && e.Key.Equals( location ) ) {
                  local++; // First location get one more weight
                  break;
               }
               last = e.Key;
            }
            if ( last.Equals( location ) ) local--; // Last location get one less weight
         }
         float perc = (float) local * 100f / (float) HitTableTotalWeight;
         return string.Format( CalledShotHitChanceFormat, perc );
      }
   }
}