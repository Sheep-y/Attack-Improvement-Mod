using BattleTech.UI;
using BattleTech;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine.EventSystems;
using UnityEngine;

namespace Sheepy.BattleTechMod.AttackImprovementMod {
   using static ArmorLocation;
   using static BindingFlags;
   using static Mod;

   public class HitLocation : BattleModModule {

      private const int SCALE = 1024; // Increase precisions of float to int conversions. Set it too high may cause overflow.
      internal static int scale = SCALE; // Actual scale. Determined by FixHitDistribution.

      internal static bool CallShotClustered = false; // True if clustering is enabled, OR is game is ver 1.0.4 or before

      public override void CombatStartsOnce () {
         scale = Settings.FixHitDistribution ? SCALE : 1;
         CallShotClustered = Settings.CalledShotUseClustering;

         bool prefixMech    = Settings.MechCalledShotMultiplier    != 1.0f || Settings.CalledShotUseClustering,
              prefixVehicle = Settings.VehicleCalledShotMultiplier != 1.0f || Settings.FixVehicleCalledShot;
         MethodInfo MechGetHit    = AttackLog.GetHitLocation( typeof( ArmorLocation ) ),
                    VehicleGetHit = AttackLog.GetHitLocation( typeof( VehicleChassisLocations ) );
         if ( prefixMech )
            Patch( MechGetHit, "PrefixMechCalledShot", null );
         if ( prefixVehicle )
            Patch( VehicleGetHit, "PrefixVehicleCalledShot", null );
         if ( prefixVehicle )
            Patch( typeof( Mech ), "GetLongArmorLocation", Static, typeof( ArmorLocation ), "FixVehicleCalledShotFloatie", null );
         if ( Settings.FixHitDistribution ) {
            ScaledMechHitTables = new Dictionary<Dictionary<ArmorLocation, int>, Dictionary<ArmorLocation, int>>();
            ScaledVehicleHitTables = new Dictionary<Dictionary<VehicleChassisLocations, int>, Dictionary<VehicleChassisLocations, int>>();
            Patch( MechGetHit, "ScaleMechHitTable", null );
            Patch( VehicleGetHit, "ScaleVehicleHitTable", null );
         }

         if ( Settings.FixGreyHeadDisease ) {
            HeadHitWeights = new Dictionary<Dictionary<ArmorLocation, int>, int>();
            Patch( MechGetHit, null, "FixGreyHeadDisease" );
         }

         if ( Settings.FixVehicleCalledShot ) {
            // Store popup location
            Patch( typeof( SelectionStateFire ), "SetCalledShot", typeof( VehicleChassisLocations ), null, "RecordVehicleCalledShotFireLocation" );

            ReadoutProp = typeof( CombatHUDVehicleArmorHover ).GetProperty( "Readout", NonPublic | Instance );
            if ( ReadoutProp != null )
               Patch( typeof( CombatHUDVehicleArmorHover ), "OnPointerClick", typeof( PointerEventData ), null, "RecordVehicleCalledShotClickLocation" );
            else
               Error( "Can't find CombatHUDVehicleArmorHover.Readout. OnPointerClick not patched. Vehicle called shot may not work." );

            Patch( typeof( Vehicle ), "GetHitLocation", new Type[]{ typeof( AbstractActor ), typeof( Vector3 ), typeof( float ), typeof( ArmorLocation ), typeof( float ) }, "RestoreVehicleCalledShotLocation", null );
         }
      }

      private static bool ClusterChanceNeverMultiplyHead = true;
      private static float ClusterChanceOriginalLocationMultiplier = 1f;
      private static Dictionary<Dictionary<ArmorLocation, int>, int> HeadHitWeights;

      public override void CombatStarts () {
         ClusterChanceNeverMultiplyHead = CombatConstants.ToHit.ClusterChanceNeverMultiplyHead;
         ClusterChanceOriginalLocationMultiplier = CombatConstants.ToHit.ClusterChanceOriginalLocationMultiplier;

         if ( Settings.FixHitDistribution ) {
            foreach ( AttackDirection direction in Enum.GetValues( typeof( AttackDirection ) ) ) {
               if ( direction == AttackDirection.None ) continue;
               if ( direction != AttackDirection.ToProne ) {
                  Dictionary<VehicleChassisLocations, int> hitTableV = Combat.HitLocation.GetVehicleHitTable( direction );
                  ScaledVehicleHitTables.Add( hitTableV, ScaleHitTable( hitTableV ) );
               }
               Dictionary<ArmorLocation, int> hitTableM = Combat.HitLocation.GetMechHitTable( direction );
               ScaledMechHitTables.Add( hitTableM, ScaleHitTable( hitTableM ) );
               if ( direction != AttackDirection.FromArtillery )
                  foreach ( ArmorLocation armor in hitTableM.Keys ) {
                     if ( hitTableM[ armor ] <= 0 ) continue;
                     Dictionary<ArmorLocation, int> hitTableC = CombatConstants.GetMechClusterTable( armor, direction );
                     ScaledMechHitTables.Add( hitTableC, ScaleHitTable( hitTableC ) );
                  }
            }
         }

         if ( Settings.FixGreyHeadDisease ) {
            List<Dictionary<ArmorLocation, int>> hitTables;
            if ( Settings.FixHitDistribution ) 
               hitTables = ScaledMechHitTables.Values.ToList();
            else {
               hitTables = new List<Dictionary<ArmorLocation, int>>();
               foreach ( AttackDirection direction in Enum.GetValues( typeof( AttackDirection ) ) ) {
                  if ( direction == AttackDirection.None ) continue;
                  hitTables.Add( Combat.HitLocation.GetMechHitTable( direction ) );
               }
            }
            foreach ( Dictionary<ArmorLocation, int> hitTable in hitTables )
               if ( hitTable.TryGetValue( Head, out int head ) && head > 0 )
                  HeadHitWeights.Add( hitTable, head );
         }
      }

      public override void CombatEnds () {
         HeadHitWeights?.Clear();
         ScaledMechHitTables?.Clear();
         ScaledVehicleHitTables?.Clear();
      }

      // ============ UTILS ============

      internal static float FixMultiplier ( ArmorLocation location, float multiplier ) {
         if ( location == None ) return 0;
         if ( Settings.MechCalledShotMultiplier != 1.0f )
            multiplier *= Settings.MechCalledShotMultiplier;
         if ( location == Head && CallShotClustered && ClusterChanceNeverMultiplyHead )
            return multiplier * ClusterChanceOriginalLocationMultiplier;
         return multiplier;
      }

      internal static float FixMultiplier ( VehicleChassisLocations location, float multiplier ) {
         if ( location == VehicleChassisLocations.None ) return 0;
         if ( Settings.VehicleCalledShotMultiplier != 1.0f )
            multiplier *= Settings.VehicleCalledShotMultiplier;
         // ClusterChanceNeverMultiplyHead does not apply to Vehicle
         return multiplier;
      }

      // ============ Called Shot ============

      private static Dictionary<Dictionary<ArmorLocation, int>, Dictionary<ArmorLocation, int>> ScaledMechHitTables;
      private static Dictionary<Dictionary<VehicleChassisLocations, int>, Dictionary<VehicleChassisLocations, int>> ScaledVehicleHitTables;

      public static void PrefixMechCalledShot ( ref Dictionary<ArmorLocation, int> hitTable, ArmorLocation bonusLocation, ref float bonusLocationMultiplier ) { try {
         bonusLocationMultiplier = FixMultiplier( bonusLocation, bonusLocationMultiplier );
         if ( Settings.CalledShotUseClustering && bonusLocation != ArmorLocation.None ) {
            HitTableConstantsDef hitTables = CombatConstants.HitTables;
            AttackDirection dir = AttackDirection.None;
            if      ( hitTable == hitTables.HitMechLocationFromFront ) dir = AttackDirection.FromFront;
            else if ( hitTable == hitTables.HitMechLocationFromLeft  ) dir = AttackDirection.FromLeft;
            else if ( hitTable == hitTables.HitMechLocationFromRight ) dir = AttackDirection.FromRight;
            else if ( hitTable == hitTables.HitMechLocationFromBack  ) dir = AttackDirection.FromBack;
            else if ( hitTable == hitTables.HitMechLocationProne     ) dir = AttackDirection.ToProne;
            else if ( hitTable == hitTables.HitMechLocationFromTop   ) dir = AttackDirection.FromTop;
            if ( dir != AttackDirection.None ) // Leave hitTable untouched if we don't recognise it
               hitTable = CombatConstants.GetMechClusterTable( bonusLocation, dir );
         }
      }                 catch ( Exception ex ) { Error( ex ); } }

      public static void ScaleMechHitTable ( ref Dictionary<ArmorLocation, int> hitTable ) { try {
         if ( ! ScaledMechHitTables.TryGetValue( hitTable, out Dictionary<ArmorLocation, int> scaled ) ) {
            ScaledMechHitTables.Add( hitTable, scaled = ScaleHitTable( hitTable ) );
            Warn( "New unscaled hit table [{0}]", Join( ",", hitTable.Select( e => e.Key+"="+e.Value ) ) );
            if ( Settings.FixGreyHeadDisease && scaled.TryGetValue( Head, out int head ) && head > 0 )
               HeadHitWeights.Add( scaled, head ); // Would be too late if head is already removed, thus the warning
         }
         hitTable = scaled;
      }                 catch ( Exception ex ) { Error( ex ); } }

      public static void PrefixVehicleCalledShot ( VehicleChassisLocations bonusLocation, ref float bonusLocationMultiplier ) { try {
         bonusLocationMultiplier = FixMultiplier( bonusLocation, bonusLocationMultiplier );
      }                 catch ( Exception ex ) { Error( ex ); } }

      public static void ScaleVehicleHitTable ( ref Dictionary<VehicleChassisLocations, int> hitTable ) { try {
         if ( ! ScaledVehicleHitTables.TryGetValue( hitTable, out Dictionary<VehicleChassisLocations, int> scaled ) )
            ScaledVehicleHitTables.Add( hitTable, scaled = ScaleHitTable( hitTable ) );
         hitTable = scaled;
      }                 catch ( Exception ex ) { Error( ex ); } }

      public static Dictionary<T, int> ScaleHitTable <T> ( Dictionary<T, int> input ) {
         Dictionary<T, int> output = new Dictionary<T, int>( input.Count );
         foreach ( var pair in input ) output.Add( pair.Key, pair.Value * SCALE );
         return output;
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

      // Not the most efficient fix since it is called per shot - same as the bugged head removal code - but this is dead simple
      public static void FixGreyHeadDisease ( Dictionary<ArmorLocation, int> hitTable ) {
         // Re-attach missing head after hit location is rolled
         if ( ! hitTable.ContainsKey( Head ) && HeadHitWeights.ContainsKey( hitTable ) )
            hitTable.Add( Head, HeadHitWeights[ hitTable ] );
      }

      // ============ Vehicle Called Shot ============

      private static PropertyInfo ReadoutProp = null;

      // Somehow PostfixSetCalledShot is NOT called since 1.1 beta. So need to override PostPointerClick to make sure called shot location is translated
      public static void RecordVehicleCalledShotClickLocation ( CombatHUDVehicleArmorHover __instance ) {
         HUDVehicleArmorReadout Readout = (HUDVehicleArmorReadout) ReadoutProp?.GetValue( __instance, null );
         if ( Readout?.HUD?.SelectionHandler?.ActiveState is SelectionStateFire selectionState )
            selectionState.calledShotLocation = TranslateLocation( selectionState.calledShotVLocation );
      }

      // Store vehicle called shot location in mech location, so that it will be passed down event chain
      public static void RecordVehicleCalledShotFireLocation ( SelectionStateFire __instance, VehicleChassisLocations location ) {
         __instance.calledShotLocation = TranslateLocation( location );
      }

      [ Harmony.HarmonyPriority( Harmony.Priority.Low ) ]
      public static bool RestoreVehicleCalledShotLocation ( Vehicle __instance, ref int __result, AbstractActor attacker, Vector3 attackPosition, float hitLocationRoll, ArmorLocation calledShotLocation, float bonusMultiplier ) { try {
         __result = (int) Combat.HitLocation.GetHitLocation( attackPosition, __instance, hitLocationRoll, TranslateLocation( calledShotLocation ), bonusMultiplier );
         return false;
      }                 catch ( Exception ex ) { return Error( ex ); } }

      [ Harmony.HarmonyPriority( Harmony.Priority.Low ) ]
      public static bool FixVehicleCalledShotFloatie ( ref string __result, ArmorLocation location ) { try {
         if ( (int) location >= 0 ) return true;
         __result = Vehicle.GetLongChassisLocation( TranslateLocation( location ) );
         return false;
      }                 catch ( Exception ex ) { return Error( ex ); } }

      public static ArmorLocation TranslateLocation ( VehicleChassisLocations location ) { return (ArmorLocation)(-(int)location); }
      public static VehicleChassisLocations TranslateLocation ( ArmorLocation location ) { return (VehicleChassisLocations)(-(int)location); }
   }
}