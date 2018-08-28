using BattleTech;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace Sheepy.BattleTechMod.AttackImprovementMod {
   using static ArmorLocation;
   using static BindingFlags;
   using static Mod;

   public class HitLocation : BattleModModule {

      private const int SCALE = 1024; // Increase precisions of float to int conversions. Set it too high may cause overflow.
      internal static int scale = SCALE; // Actual scale. Determined by FixHitDistribution.

      internal static bool CallShotClustered = false; // True if clustering is enabled, OR is game is ver 1.0.4 or before

      private static float MechCalledShotMultiplier, VehicleCalledShotMultiplier;

      public override void CombatStartsOnce () {
         scale = Settings.FixHitDistribution ? SCALE : 1;
         CallShotClustered = Settings.CalledShotUseClustering;
         MechCalledShotMultiplier = (float) Settings.MechCalledShotMultiplier;
         VehicleCalledShotMultiplier = (float) Settings.VehicleCalledShotMultiplier;

         bool prefixMech    = MechCalledShotMultiplier    != 1 || Settings.CalledShotUseClustering,
              prefixVehicle = VehicleCalledShotMultiplier != 1 || Settings.FixVehicleCalledShot;
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
            Patch( typeof( BattleTech.HitLocation ), "GetMechHitTable", null, "FixGreyHeadDisease" );
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
      }

      public override void CombatEnds () {
         HeadHitWeights?.Clear();
         ScaledMechHitTables?.Clear();
         ScaledVehicleHitTables?.Clear();
      }

      // ============ UTILS ============

      internal static float FixMultiplier ( ArmorLocation location, float multiplier ) {
         if ( location == None ) return 0;
         if ( MechCalledShotMultiplier != 1 )
            multiplier *= MechCalledShotMultiplier;
         if ( location == Head && CallShotClustered && ClusterChanceNeverMultiplyHead )
            return multiplier * ClusterChanceOriginalLocationMultiplier;
         return multiplier;
      }

      internal static float FixMultiplier ( VehicleChassisLocations location, float multiplier ) {
         if ( location == VehicleChassisLocations.None ) return 0;
         if ( VehicleCalledShotMultiplier != 1 )
            multiplier *= VehicleCalledShotMultiplier;
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
         if ( ! ScaledMechHitTables.TryGetValue( hitTable, out Dictionary<ArmorLocation, int> scaled ) )
            ScaledMechHitTables.Add( hitTable, scaled = ScaleHitTable( hitTable ) );
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
            totalWeight += (int)( hitTable[ bonusLocation ] * ( bonusLocationMultiplier - 1 ) * SCALE );
         return totalWeight;
      }

      public static void FixGreyHeadDisease ( Dictionary<ArmorLocation, int> __result ) {
         if ( __result == null ) return;
         if ( __result.TryGetValue( Head, out int head ) ) {
            // Has head. Cache it.
            if ( HeadHitWeights.ContainsKey( __result ) ) return;
            HeadHitWeights[ __result ] = head;
         } else {
            // No head. Add it?
            if ( ! HeadHitWeights.TryGetValue( __result, out head ) ) return;
            __result[ Head ] = head;
         }
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