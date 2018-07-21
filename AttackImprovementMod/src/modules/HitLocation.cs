using BattleTech.UI;
using BattleTech;
using System.Collections.Generic;
using System.Reflection;
using System;
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
         if ( Settings.FixNonIntegerDamage )
            Patch( typeof( AbstractActor ), "GetAdjustedDamage", null, "FixDamageToInteger" );

         scale = Settings.FixHitDistribution ? SCALE : 1;
         CallShotClustered = Settings.CalledShotUseClustering || GameUseClusteredCallShot;

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
            Patch( MechGetHit, "OverrideMechCalledShot", null );
            Patch( VehicleGetHit, "OverrideVehicleCalledShot", null );
         }
         if ( Settings.FixGreyHeadDisease )
            Patch( MechGetHit, null, "FixGreyHeadDisease" );

         if ( Settings.FixVehicleCalledShot ) {
            // Store popup location
            Patch( typeof( SelectionStateFire ), "SetCalledShot", typeof( VehicleChassisLocations ), null, "RecordVehicleCalledShotFireLocation" );
            ReadoutProp = typeof( CombatHUDVehicleArmorHover ).GetProperty( "Readout", NonPublic | Instance );
            if ( ReadoutProp != null )
               Patch( typeof( CombatHUDVehicleArmorHover ), "OnPointerClick", typeof( PointerEventData ), null, "RecordVehicleCalledShotClickLocation" );
            else
               Error( "Can't find CombatHUDVehicleArmorHover.Readout. OnPointerClick not patched. Vehicle called shot may not work." );

            if ( GameUseClusteredCallShot ) // 1.0.x
               Patch( typeof( Vehicle ), "GetHitLocation", new Type[]{ typeof( AbstractActor ), typeof( Vector3 ), typeof( float ), typeof( ArmorLocation ) }, "RestoreVehicleCalledShotLocation_1_0", null );
            else // 1.1.x
               Patch( typeof( Vehicle ), "GetHitLocation", new Type[]{ typeof( AbstractActor ), typeof( Vector3 ), typeof( float ), typeof( ArmorLocation ), typeof( float ) }, "RestoreVehicleCalledShotLocation_1_1", null );
         }
      }

      private static bool ClusterChanceNeverMultiplyHead = true;
      private static float ClusterChanceOriginalLocationMultiplier = 1f;
      private static Dictionary< Dictionary<ArmorLocation, int>, int > HeadHitWeights;

      public override void CombatStarts () {
         ClusterChanceNeverMultiplyHead = CombatConstants.ToHit.ClusterChanceNeverMultiplyHead;
         ClusterChanceOriginalLocationMultiplier = CombatConstants.ToHit.ClusterChanceOriginalLocationMultiplier;
         if ( HeadHitWeights == null ) {
            HeadHitWeights = new Dictionary<Dictionary<ArmorLocation, int>, int>();
            foreach ( AttackDirection direction in Enum.GetValues( typeof( AttackDirection ) ) ) {
               if ( direction == AttackDirection.None ) continue;
               Dictionary<ArmorLocation, int> hitTable = Combat.HitLocation.GetMechHitTable( direction );
               if ( ! hitTable.TryGetValue( Head, out int head ) || head == 0 ) continue;
               HeadHitWeights.Add( hitTable, head );
            }
         }
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

      // ============ Fixes ============

      public static void FixDamageToInteger ( ref float __result ) {
         __result = Mathf.Round( __result );
      }

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

      public static bool OverrideMechCalledShot ( ref ArmorLocation __result, Dictionary<ArmorLocation, int> hitTable, float randomRoll, ArmorLocation bonusLocation, float bonusLocationMultiplier ) { try {
         __result = GetHitLocationFixed( hitTable, randomRoll, bonusLocation, bonusLocationMultiplier );
         return false;
      }                 catch ( Exception ex ) { return Error( ex ); } }

      public static void PrefixVehicleCalledShot ( VehicleChassisLocations bonusLocation, ref float bonusLocationMultiplier ) { try {
         bonusLocationMultiplier = FixMultiplier( bonusLocation, bonusLocationMultiplier );
      }                 catch ( Exception ex ) { Error( ex ); } }

      public static bool OverrideVehicleCalledShot ( ref VehicleChassisLocations __result, Dictionary<VehicleChassisLocations, int> hitTable, float randomRoll, VehicleChassisLocations bonusLocation, float bonusLocationMultiplier ) { try {
         __result = GetHitLocationFixed( hitTable, randomRoll, bonusLocation, bonusLocationMultiplier );
         return false;
      }                 catch ( Exception ex ) { return Error( ex ); } }

      // ============ GetHitLocation ============

      internal static int SumWeight<T> ( Dictionary<T, int> hitTable, T bonusLocation, float bonusLocationMultiplier, int SCALE ) {
         int totalWeight = 0;
         foreach ( int weight in hitTable.Values ) totalWeight += weight;
         totalWeight *= SCALE;
         if ( bonusLocationMultiplier != 1 && hitTable.ContainsKey( bonusLocation ) )
            totalWeight += (int)( (float) hitTable[ bonusLocation ] * ( bonusLocationMultiplier - 1 ) * SCALE );
         return totalWeight;
      }

      public static T GetHitLocationFixed<T> ( Dictionary<T, int> hitTable, float roll, T bonusLocation, float bonusLocationMultiplier ) {
         int totalWeight = SumWeight( hitTable, bonusLocation, bonusLocationMultiplier, SCALE );
         int goal = (int)( roll * (double)totalWeight ), i = 0;
         foreach ( KeyValuePair<T, int> location in hitTable ) {
            if ( location.Value <= 0 ) continue;
            if ( location.Key.Equals( bonusLocation ) )
               i += (int)( (float) location.Value * bonusLocationMultiplier * SCALE );
            else
               i += location.Value * SCALE;
            if ( i > goal )
               return location.Key;
         }
         throw new ApplicationException( "No valid hit location. Enable logging to see hitTable." );
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
      public static void RecordVehicleCalledShotClickLocation ( CombatHUDVehicleArmorHover __instance ) { try {
         HUDVehicleArmorReadout Readout = (HUDVehicleArmorReadout) ReadoutProp?.GetValue( __instance, null );
         if ( Readout?.HUD?.SelectionHandler?.ActiveState is SelectionStateFire selectionState )
            selectionState.calledShotLocation = TranslateLocation( selectionState.calledShotVLocation );
      }                 catch ( Exception ex ) { Error( ex ); } }

      // Store vehicle called shot location in mech location, so that it will be passed down event chain
      public static void RecordVehicleCalledShotFireLocation ( SelectionStateFire __instance, VehicleChassisLocations location ) {
         __instance.calledShotLocation = TranslateLocation( location );
      }

      public static bool RestoreVehicleCalledShotLocation_1_0 ( Vehicle __instance, ref int __result, AbstractActor attacker, Vector3 attackPosition, float hitLocationRoll, ArmorLocation calledShotLocation ) { try {
         __result = (int) Combat.HitLocation.GetHitLocation( attackPosition, __instance, hitLocationRoll, TranslateLocation( calledShotLocation ), attacker.CalledShotBonusMultiplier );
         return false;
      }                 catch ( Exception ex ) { return Error( ex ); } }

      public static bool RestoreVehicleCalledShotLocation_1_1 ( Vehicle __instance, ref int __result, AbstractActor attacker, Vector3 attackPosition, float hitLocationRoll, ArmorLocation calledShotLocation, float bonusMultiplier ) { try {
         __result = (int) Combat.HitLocation.GetHitLocation( attackPosition, __instance, hitLocationRoll, TranslateLocation( calledShotLocation ), bonusMultiplier );
         return false;
      }                 catch ( Exception ex ) { return Error( ex ); } }

      public static bool FixVehicleCalledShotFloatie ( ref string __result, ArmorLocation location ) { try {
         if ( (int) location >= 0 ) return true;
         __result = Vehicle.GetLongChassisLocation( TranslateLocation( location ) );
         return false;
      }                 catch ( Exception ex ) { return Error( ex ); } }

      public static ArmorLocation TranslateLocation ( VehicleChassisLocations location ) { return (ArmorLocation)(-(int)location); }
      public static VehicleChassisLocations TranslateLocation ( ArmorLocation location ) { return (VehicleChassisLocations)(-(int)location); }
   }
}