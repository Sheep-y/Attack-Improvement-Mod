using BattleTech;
using BattleTech.UI;
using System;
using System.Collections.Generic;

namespace Sheepy.AttackImprovementMod {
   using System.Reflection;
   using static Mod;
   using static FixHitLocation;

   public class FixCalledShotPopUp {

      internal static void InitPatch () {
         if ( Settings.ShowRealMechCalledShotChance || Settings.ShowRealVehicleCalledShotChance ) {

            Type CalledShot = typeof( CombatHUDCalledShotPopUp );
            Patch( CalledShot, "set_ShownAttackDirection", typeof( AttackDirection ), null, "RecordAttackDirection" );

            if ( Settings.ShowRealMechCalledShotChance )
               Patch( CalledShot, "GetHitPercent", BindingFlags.NonPublic | BindingFlags.Instance, new Type[]{ typeof( ArmorLocation ), typeof( ArmorLocation ) }, "OverrideHUDMechCalledShotPercent", null );

            if ( Settings.ShowRealVehicleCalledShotChance )
               Patch( CalledShot, "GetHitPercent", BindingFlags.NonPublic | BindingFlags.Instance, new Type[]{ typeof( VehicleChassisLocations ), typeof( VehicleChassisLocations ) }, "OverrideHUDVehicleCalledShotPercent", null );

         } else if ( Settings.ShowDecimalCalledChance ) {
            Log( "Warning: Decimal Called Shot Chance requires ShowRealMechCalledShotChance and/or ShowRealVehicleCalledShotChance" );
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

      private static bool CacheNeedRefresh( Object hitTable, int targetedLocation ) {
         bool result = ! Object.ReferenceEquals( hitTable, LastHitTable ) || lastCalledShotLocation != (int) targetedLocation;
         if ( result ) {
            LastHitTable = hitTable;
            lastCalledShotLocation = (int) targetedLocation;
         }
         return result;
      }

		public static bool OverrideHUDMechCalledShotPercent ( ref string __result, ArmorLocation location, ArmorLocation targetedLocation ) {
         try {
            Dictionary<ArmorLocation, int> hitTable = ( targetedLocation == ArmorLocation.None || ! FixHitLocation.CallShotClustered )
                                                    ? Combat.HitLocation.GetMechHitTable( AttackDirection )
                                                    : Combat.Constants.GetMechClusterTable( targetedLocation, AttackDirection );
            if ( CacheNeedRefresh( hitTable, (int) targetedLocation ) )
               HitTableTotalWeight = SumWeight( hitTable, targetedLocation, FixMultiplier( targetedLocation, ActorCalledShotBonus ), scale );

			   int local = TryGet( hitTable, location ) * scale;
            if ( location == targetedLocation )
               local = (int)( (float) local * FixMultiplier( targetedLocation, ActorCalledShotBonus ) );

            __result = FineTuneAndFormat( hitTable, location, local );
            return false;
         } catch ( Exception ex ) {
            return Log( ex );
         }
		}

		public static bool OverrideHUDVehicleCalledShotPercent ( ref string __result, VehicleChassisLocations location, VehicleChassisLocations targetedLocation ) {
         if ( ! Settings.FixVehicleCalledShot )
            targetedLocation = VehicleChassisLocations.None; // Disable called location if vehicle called shot is not fixed

         try {
            Dictionary<VehicleChassisLocations, int> hitTable = Combat.HitLocation.GetVehicleHitTable( AttackDirection );
            if ( CacheNeedRefresh( hitTable, (int) targetedLocation ) )
               HitTableTotalWeight = SumWeight( hitTable, targetedLocation, FixMultiplier( targetedLocation, ActorCalledShotBonus ), scale );

			   int local = TryGet( hitTable, location ) * scale;
            if ( location == targetedLocation )
               local = (int)( (float) local * FixMultiplier( targetedLocation, ActorCalledShotBonus ) );

            __result = FineTuneAndFormat( hitTable, location, local );
            return false;
         } catch ( Exception ex ) {
            return Log( ex );
         }
		}

      // ============ Subroutines ============

      private static string FineTuneAndFormat<T> ( Dictionary<T, int> hitTable, T location, int local  ) {
         if ( ! Settings.FixHitDistribution ) { // If hit distribution is biased
            T def = default(T), last = def;
            foreach ( KeyValuePair<T, int> pair in hitTable ) {
               if ( pair.Value == 0 ) continue;
               if ( last.Equals( def ) && pair.Key.Equals( location ) ) {
                  local++; // First location get one more weight
                  break;
               }
               last = pair.Key;
            }
            if ( last.Equals( location ) ) local--; // Last location get one less weight
         }
         //Log( "HUD M Total @ " + location + ( location == targetedLocation ? "(Called)" : "") + " = " + local + "/" + HitTableTotalWeight + "(x" + FixMultiplier( targetedLocation, ActorCalledShotBonus ) + "x" + scale + ")" );
         if ( Settings.ShowDecimalCalledChance )
			   return DeciPercent( local );
         else
			   return IntPercent ( local );
      }

      private static string DeciPercent ( int localWeight ) {
			float perc = (float) localWeight * 100f / (float) HitTableTotalWeight;
			return string.Format("{0:0.0}%", perc );
      }

      private static string IntPercent  ( int localWeight ) {
			float perc = (float) localWeight * 100f / (float) HitTableTotalWeight;
			return string.Format("{0:0}%", perc );
      }
   }
}