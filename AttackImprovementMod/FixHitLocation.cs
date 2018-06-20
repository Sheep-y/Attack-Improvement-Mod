using BattleTech;
using BattleTech.UI;
using System;
using System.Collections.Generic;
using System.IO;

namespace Sheepy.AttackImprovementMod {
   using Harmony;
   using System.Reflection;
   using UnityEngine;
   using static Mod;

   public class FixHitLocation {

      private const int SCALE = 1024; // Increase precisions of float to int conversions. Set it too high may cause overflow.
      internal static int scale = SCALE; // Actual scale. Determined by FixHitDistribution.

      internal static bool CallShotClustered = false; // True if clustering is enabled, OR is game is ver 1.0.4 or before

      internal static void InitPatch ( HarmonyInstance harmony ) {
         scale = Settings.FixHitDistribution ? SCALE : 1;
         CallShotClustered = Settings.CalledShotUseClustering || Mod.Pre_1_1;

         MethodInfo GetHitLocation = typeof( HitLocation ).GetMethod( "GetHitLocation", BindingFlags.Public | BindingFlags.Static ); // Only one public static GetHitLocation method.
         if ( GetHitLocation.GetParameters().Length != 4 || GetHitLocation.GetParameters()[1].ParameterType != typeof( float ) || GetHitLocation.GetParameters()[3].ParameterType != typeof( float ) ) {
            Log( "Error: Cannot find HitLocation.GetHitLocation( ?, float, ?, float ) to patch" );

         } else {
            bool patchMech    = Settings.CalledShotUseClustering || Settings.FixHitDistribution || Settings.MechCalledShotMultiplier != 1.0f,
                 patchVehicle = Settings.FixHitDistribution || Settings.VehicleCalledShotMultiplier != 1.0f,
                 patchPostfix = Settings.LogHitRolls;
            if ( patchMech || patchVehicle || patchPostfix ) {
               HarmonyMethod FixArmour  = patchMech    ? MakePatch( "PrefixArmourLocation"   ) : null;
               HarmonyMethod FixVehicle = patchVehicle ? MakePatch( "PrefixVehicleLocation"  ) : null;
               HarmonyMethod LogArmour  = patchPostfix ? MakePatch( "PostfixArmourLocation"  ) : null;
               HarmonyMethod LogVehicle = patchPostfix ? MakePatch( "PostfixVehicleLocation" ) : null;
               if ( patchMech    || patchPostfix )
                  harmony.Patch( GetHitLocation.MakeGenericMethod( typeof( ArmorLocation ) ), FixArmour, LogArmour );
               if ( patchVehicle || patchPostfix )
                  harmony.Patch( GetHitLocation.MakeGenericMethod( typeof( VehicleChassisLocations ) ), FixVehicle, LogVehicle );
               Log( string.Format( "GetHitLocation patched: M:[ {0} / {1} ]   V:[ {2} / {3} ].", new object[]{ FixArmour?.method.Name, LogArmour?.method.Name, FixVehicle?.method.Name, LogVehicle?.method.Name } ) );
            } else {
               Log( "GetHitLocation not patched." );
            }
         }

         if ( Settings.FixVehicleCalledShot ) {
            Patch( typeof( SelectionStateFire ), "SetCalledShot", typeof( VehicleChassisLocations ), null, "PostfixSetCalledShot" );
            if ( Mod.Pre_1_1 )
               Patch( typeof( Vehicle ), "GetHitLocation", new Type[]{ typeof( AbstractActor ), typeof( Vector3 ), typeof( float ), typeof( ArmorLocation ) }, "PrefixVehicleGetHitLocation_1_0", null );
            else
               Patch( typeof( Vehicle ), "GetHitLocation", new Type[]{ typeof( AbstractActor ), typeof( Vector3 ), typeof( float ), typeof( ArmorLocation ), typeof( float ) }, "PrefixVehicleGetHitLocation_1_1", null );
         }
      }

      // ============ UTILS ============

      internal static float FixMultiplier ( ArmorLocation location, float multiplier ) {
         if ( Settings.MechCalledShotMultiplier != 1.0f )
            multiplier *= Settings.MechCalledShotMultiplier;
         if ( location == ArmorLocation.Head && CallShotClustered && Combat.Constants.ToHit.ClusterChanceNeverMultiplyHead )
            return multiplier * Combat.Constants.ToHit.ClusterChanceOriginalLocationMultiplier;
         return multiplier;
      }

      internal static float FixMultiplier ( VehicleChassisLocations location, float multiplier ) {
         if ( Settings.VehicleCalledShotMultiplier != 1.0f )
            multiplier *= Settings.VehicleCalledShotMultiplier;
         // ClusterChanceNeverMultiplyHead does not apply to Vehicle
         return multiplier;
      }

      // ============ Prefix (fix things) ============


      public static bool PrefixArmourLocation ( ref ArmorLocation __result, ref Dictionary<ArmorLocation, int> hitTable, float randomRoll, ArmorLocation bonusLocation, ref float bonusLocationMultiplier ) {
         try {
            bonusLocationMultiplier = FixMultiplier( bonusLocation, bonusLocationMultiplier );
            if ( Settings.CalledShotUseClustering && bonusLocation != ArmorLocation.None ) {
               HitTableConstantsDef hitTables = Combat.Constants.HitTables;
               AttackDirection dir = AttackDirection.None;
               if      ( hitTable == hitTables.HitMechLocationFromFront ) dir = AttackDirection.FromFront;
               else if ( hitTable == hitTables.HitMechLocationFromLeft  ) dir = AttackDirection.FromLeft;
               else if ( hitTable == hitTables.HitMechLocationFromRight ) dir = AttackDirection.FromRight;
               else if ( hitTable == hitTables.HitMechLocationFromBack  ) dir = AttackDirection.FromBack;
               else if ( hitTable == hitTables.HitMechLocationProne     ) dir = AttackDirection.ToProne;
               else if ( hitTable == hitTables.HitMechLocationFromTop   ) dir = AttackDirection.FromTop;
               if ( dir != AttackDirection.None ) // Leave hitTable untouched if we don't recognise it
                  hitTable = Combat.Constants.GetMechClusterTable( bonusLocation, dir );
            }
            if ( Settings.FixHitDistribution ) return true;
            __result = GetHitLocation( hitTable, randomRoll, bonusLocation, bonusLocationMultiplier );
            return false;
         } catch ( Exception ex ) {
            return Log( ex );
         }
      }

      public static bool PrefixVehicleLocation ( ref VehicleChassisLocations __result, Dictionary<VehicleChassisLocations, int> hitTable, float randomRoll, VehicleChassisLocations bonusLocation, ref float bonusLocationMultiplier ) {
         try {
            bonusLocationMultiplier = FixMultiplier( bonusLocation, bonusLocationMultiplier );
            if ( ! Settings.FixHitDistribution ) return true;
            __result = GetHitLocation( hitTable, randomRoll, bonusLocation, bonusLocationMultiplier );
            return false;
         } catch ( Exception ex ) {
            return Log( ex );
         }
      }

      // ============ Postfix (log results) ============
      // Won't be patched if Settings.LogHitLocationCalculation is false

      public static void PostfixArmourLocation ( ArmorLocation __result, Dictionary<ArmorLocation, int> hitTable, float randomRoll, ArmorLocation bonusLocation, float bonusLocationMultiplier ) {
         // "Location Roll", "Head/Turret", "CT/Front", "LT/Left", "RT/Right", "LA/Rear", "RA", "LL", "RL", "Called Part", "Called Bonus", "Total Weight", "Goal", "Hit Location"
         try {
            int totalWeight = SumWeight( hitTable, bonusLocation, bonusLocationMultiplier, scale );
            RollCorrection.RollLog(
                  RollCorrection.GetHitLog() + "\t" +
                  randomRoll + "\t" +
                  TryGet( hitTable, ArmorLocation.Head ) + "\t" +
                ( TryGet( hitTable, ArmorLocation.CenterTorso ) + TryGet( hitTable, ArmorLocation.CenterTorsoRear ) ) + "\t" +
                ( TryGet( hitTable, ArmorLocation.LeftTorso   ) + TryGet( hitTable, ArmorLocation.LeftTorsoRear   ) ) + "\t" +
                ( TryGet( hitTable, ArmorLocation.RightTorso  ) + TryGet( hitTable, ArmorLocation.RightTorsoRear  ) ) + "\t" +
                  TryGet( hitTable, ArmorLocation.LeftArm  ) + "\t" +
                  TryGet( hitTable, ArmorLocation.RightArm ) + "\t" +
                  TryGet( hitTable, ArmorLocation.LeftLeg  ) + "\t" +
                  TryGet( hitTable, ArmorLocation.RightLeg ) + "\t" +
                  bonusLocation + "\t" +
                  bonusLocationMultiplier + "\t" +
                  totalWeight + "\t" +
                  (int)( randomRoll * totalWeight ) + "\t" +
                  __result );
         } catch ( Exception ex ) {
            Log( ex );
         }
      }

      public static void PostfixVehicleLocation ( VehicleChassisLocations __result, Dictionary<VehicleChassisLocations, int> hitTable, float randomRoll, VehicleChassisLocations bonusLocation, float bonusLocationMultiplier ) {
         // "Location Roll", "Head/Turret", "CT/Front", "LT/Left", "RT/Right", "LA/Rear", "RA", "LL", "RL", "Called Part", "Called Bonus", "Total Weight", "Goal", "Hit Location"
         try {
            int totalWeight = SumWeight( hitTable, bonusLocation, bonusLocationMultiplier, scale );
            RollCorrection.RollLog(
                  RollCorrection.GetHitLog() + "\t" +
                  randomRoll + "\t" +
                  TryGet( hitTable, VehicleChassisLocations.Turret ) + "\t" +
                  TryGet( hitTable, VehicleChassisLocations.Front  ) + "\t" +
                  TryGet( hitTable, VehicleChassisLocations.Left   ) + "\t" +
                  TryGet( hitTable, VehicleChassisLocations.Right  ) + "\t" +
                  TryGet( hitTable, VehicleChassisLocations.Rear   ) + "\t" +
                  "\t" +
                  "\t" +
                  "\t" +
                  bonusLocation + "\t" +
                  bonusLocationMultiplier + "\t" +
                  totalWeight + "\t" +
                  (int)( randomRoll * totalWeight ) + "\t" +
                  __result );
         } catch ( Exception ex ) {
            Log( ex );
         }
      }

      // ============ GetHitLocation ============

      internal static int SumWeight<T> ( Dictionary<T, int> hitTable, T bonusLocation, float bonusLocationMultiplier, int SCALE ) {
			int totalWeight = 0;
         foreach ( int weight in hitTable.Values ) totalWeight += weight;
         totalWeight *= SCALE;
         if ( bonusLocationMultiplier != 1 && hitTable.ContainsKey( bonusLocation ) )
            totalWeight += (int)( (float) hitTable[ bonusLocation ] * ( bonusLocationMultiplier - 1 ) * SCALE );
         return totalWeight;
      }

      public static T GetHitLocation<T> ( Dictionary<T, int> hitTable, float roll, T bonusLocation, float bonusLocationMultiplier ) {
         int totalWeight = SumWeight( hitTable, bonusLocation, bonusLocationMultiplier, SCALE );
			int goal = (int)( roll * (double)totalWeight ), i = 0;
			foreach ( KeyValuePair<T, int> location in hitTable ) {
				if ( location.Value <= 0 ) continue;
				if ( location.Key.Equals( bonusLocation ) )
					i += (int)( (float) location.Value * bonusLocationMultiplier * SCALE );
				else
					i += location.Value * SCALE;
				if ( i >= goal )
					return location.Key;
			}
         throw new ApplicationException( "No valid hit location. Enable logging to see hitTable." );
      }

      // ============ Vehicle Called Shot ============


      // Map Vehicle location to Mech location so that fire event can pass called shot location down
      public static void PostfixSetCalledShot ( SelectionStateFire __instance, VehicleChassisLocations location ) {
         __instance.calledShotLocation = translate( location );
      }

      public static bool PrefixVehicleGetHitLocation ( Vehicle __instance, ref int __result, AbstractActor attacker, Vector3 attackPosition, float hitLocationRoll, ArmorLocation calledShotLocation ) {
         try {
            __result = (int) Combat.HitLocation.GetHitLocation( attackPosition, __instance, hitLocationRoll, translate( calledShotLocation ), attacker.CalledShotBonusMultiplier );
            return false;
         } catch ( Exception ex ) {
            return Log( ex );
         }
		}

      public static bool PrefixVehicleGetHitLocation_1_1 ( Vehicle __instance, ref int __result, AbstractActor attacker, Vector3 attackPosition, float hitLocationRoll, ArmorLocation calledShotLocation, float bonusMultiplier ) {
         Log( "Get: " + calledShotLocation );
         Log( "Translated: " + translateLocation( calledShotLocation ) );
         try {
            __result = (int) Combat.HitLocation.GetHitLocation( attackPosition, __instance, hitLocationRoll, translate( calledShotLocation ), bonusMultiplier );
            return false;
         } catch ( Exception ex ) {
            return Log( ex );
         }
		}

      public static ArmorLocation translate ( VehicleChassisLocations location ) {
         //return (ArmorLocation)(int)location;
         switch ( location ) {
            case VehicleChassisLocations.Turret : return ArmorLocation.Head;
            case VehicleChassisLocations.Front  : return ArmorLocation.CenterTorso;
            case VehicleChassisLocations.Left   : return ArmorLocation.LeftTorso;
            case VehicleChassisLocations.Right  : return ArmorLocation.RightTorso;
            case VehicleChassisLocations.Rear   : return ArmorLocation.CenterTorsoRear;
         }
         return ArmorLocation.None;
      }

      public static VehicleChassisLocations translate( ArmorLocation location ) {
         switch ( location ) {
            case ArmorLocation.Head            : return VehicleChassisLocations.Turret;
            case ArmorLocation.CenterTorso     : return VehicleChassisLocations.Front;
            case ArmorLocation.LeftTorso       : return VehicleChassisLocations.Left;
            case ArmorLocation.RightTorso      : return VehicleChassisLocations.Right;
            case ArmorLocation.CenterTorsoRear : return VehicleChassisLocations.Rear;
         }
         return VehicleChassisLocations.None;
      }
   }
}