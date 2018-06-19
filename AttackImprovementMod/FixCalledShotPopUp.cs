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

            // Eavesdrops HUD assignment
            Patch( CalledShot, "Init", typeof( CombatHUD ), null, "PostfixCombatHUDInit" );

            // Eavesdrops Attack Direction
            Patch( CalledShot, "set_ShownAttackDirection", typeof( AttackDirection ), null, "PostfixShownAttackDirection" );

            if ( Settings.ShowRealMechCalledShotChance )
               Patch( CalledShot, "GetHitPercent", BindingFlags.NonPublic | BindingFlags.Instance, new Type[]{ typeof( ArmorLocation ), typeof( ArmorLocation ) }, "PrefixMechHUDPercent", null );

            if ( Settings.ShowRealVehicleCalledShotChance )
               Patch( CalledShot, "GetHitPercent", BindingFlags.NonPublic | BindingFlags.Instance, new Type[]{ typeof( VehicleChassisLocations ), typeof( VehicleChassisLocations ) }, "PrefixVehicleHUDPercent", null );
         }
      }

      // ============ Game States ============

      private static CombatHUD HUD; // Save intercepted Combat HUD
      public static void PostfixCombatHUDInit ( CombatHUD HUD ) {
         FixCalledShotPopUp.HUD = HUD;
      }

      private static float ActorCalledShotBonus { // Defer property resolution until value is used
         get { return HUD.SelectedActor.CalledShotBonusMultiplier; }
      }

      private static AttackDirection AttackDirection; // Save intercepted attack direction
      public static void PostfixShownAttackDirection ( AttackDirection value ) {
         AttackDirection = value;
      }

      // ============ HUD Override ============

      private static Object LastHitTable;
      private static int HitTableTotalWeight;
      private static VehicleChassisLocations lastCalledShotLocation; // Vehicle does not have cluster so need to update cache with call shot location

      // Can't get private fields. Not sure why. Method does not get called.
		// public static bool PrefixMechHUDPercent ( ref Dictionary<ArmorLocation, int> ___currentHitTable, ref CombatHUD ___HUD, ref AttackDirection ___shownAttackDirection, ref string __result, ArmorLocation location, ArmorLocation targetedLocation ) {
		public static bool PrefixMechHUDPercent ( ref string __result, ArmorLocation location, ArmorLocation targetedLocation ) {
         try {
            Dictionary<ArmorLocation, int> hitTable = targetedLocation == ArmorLocation.None
                                                    ? Combat.HitLocation.GetMechHitTable( AttackDirection )
                                                    : Combat.Constants.GetMechClusterTable( targetedLocation, AttackDirection );
            if ( ! Object.ReferenceEquals( hitTable, LastHitTable ) ) {
               // Cached table changes with type (Mech/Vehicle), targetedLocation, and AttackDiection.
               LastHitTable = hitTable;
               HitTableTotalWeight = SumWeight( hitTable, targetedLocation, FixMultiplier( targetedLocation, ActorCalledShotBonus ), scale );
            }
			   int local = TryGet( hitTable, location ) * scale;
            if ( local != 0 && location == targetedLocation )
               local = (int)( (float) local * FixMultiplier( targetedLocation, ActorCalledShotBonus ) );

            __result = FineTuneAndFormat( hitTable, location, local );
            return false;
         } catch ( Exception ex ) {
            return Log( ex );
         }
		}

		public static bool PrefixVehicleHUDPercent ( ref string __result, VehicleChassisLocations location, VehicleChassisLocations targetedLocation ) {
         if ( ! Settings.FixVehicleCalledShot )
            targetedLocation = VehicleChassisLocations.None; // Disable called location if vehicle called shot is not fixed

         try {
            Dictionary<VehicleChassisLocations, int> hitTable = Combat.HitLocation.GetVehicleHitTable( AttackDirection ); // Vehicle has no cluster table
            if ( ! Object.ReferenceEquals( hitTable, LastHitTable ) || lastCalledShotLocation != targetedLocation ) {
               LastHitTable = hitTable;
               lastCalledShotLocation = targetedLocation;
               HitTableTotalWeight = SumWeight( hitTable, targetedLocation, FixMultiplier( targetedLocation, ActorCalledShotBonus ), scale );
            }
			   int local = TryGet( hitTable, location ) * scale;
            if ( local != 0 && location == targetedLocation )
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