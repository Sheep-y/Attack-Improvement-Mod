using BattleTech.UI;
using BattleTech;
using System.Collections.Generic;
using System.Reflection;
using System;
using System.Linq;
using UnityEngine.EventSystems;
using UnityEngine;

namespace Sheepy.BattleTechMod.AttackImprovementMod {
   using static ArmorLocation;
   using static BindingFlags;
   using static Mod;

   public class HitResolution : BattleModModule {

      private const int SCALE = 1024; // Increase precisions of float to int conversions. Set it too high may cause overflow.
      internal static int scale = SCALE; // Actual scale. Determined by FixHitDistribution.

      internal static bool CallShotClustered = false; // True if clustering is enabled, OR is game is ver 1.0.4 or before

      public override void CombatStartsOnce () {
         if ( Settings.KillZeroHpLocation ) {
            Patch( typeof( Mech )   , "DamageLocation", NonPublic, null, "FixZombieMech" );
            Patch( typeof( Vehicle ), "DamageLocation", NonPublic, null, "FixZombieVehicle" );
            Patch( typeof( Turret ) , "DamageLocation", NonPublic, null, "FixZombieTurret" );
            Patch( typeof( BattleTech.Building ), "DamageBuilding", NonPublic, null, "FixZombieBuilding" );
         }

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

         if ( Settings.BalanceAmmoLoad || Settings.BalanceEnemyAmmoLoad ) {
            Patch( typeof( Weapon ), "DecrementAmmo", "OverrideDecrementAmmo", null );
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

      // ============ Called Shot ============

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

      // ============ Zombie ============

      public static void FixZombieMech ( Mech __instance, ref float totalDamage, ArmorLocation aLoc ) {
         if ( aLoc == ArmorLocation.None || aLoc == ArmorLocation.Invalid ) return;
         float armour = __instance.GetCurrentArmor( aLoc );
         if ( armour >= totalDamage ) return;
         KillZombie( "mech", __instance.DisplayName, armour + __instance.GetCurrentStructure( MechStructureRules.GetChassisLocationFromArmorLocation( aLoc ) ), ref totalDamage );
      }

      public static void FixZombieVehicle ( Vehicle __instance, ref float totalDamage, VehicleChassisLocations vLoc ) {
         if ( vLoc == VehicleChassisLocations.None || vLoc == VehicleChassisLocations.Invalid ) return;
         KillZombie( "vehicle", __instance.DisplayName, __instance.GetCurrentArmor( vLoc ) + __instance.GetCurrentStructure( vLoc ), ref totalDamage );
      }

      public static void FixZombieTurret ( Turret __instance, ref float totalDamage, BuildingLocation bLoc ) {
         if ( bLoc == BuildingLocation.None || bLoc == BuildingLocation.Invalid ) return;
         KillZombie( "turret", __instance.DisplayName, __instance.GetCurrentArmor( bLoc ) + __instance.GetCurrentStructure( bLoc ), ref totalDamage );
      }

      public static void FixZombieBuilding ( BattleTech.Building __instance, ref float totalDamage ) {
         KillZombie( "building", __instance.DisplayName, __instance.CurrentStructure, ref totalDamage );
      }

      private static void KillZombie ( string type, string name, float HP, ref float totalDamage ) {
         float newHP = HP - totalDamage;
         if ( newHP >= 1 || newHP <= 0 ) return;
         Log( "Upgrading damage dealt to {1} by {2} to kill zombie {0}", type, name, newHP );
         totalDamage += newHP + 0.001f;
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
      public static void RecordVehicleCalledShotClickLocation ( CombatHUDVehicleArmorHover __instance ) {
         HUDVehicleArmorReadout Readout = (HUDVehicleArmorReadout) ReadoutProp?.GetValue( __instance, null );
         if ( Readout?.HUD?.SelectionHandler?.ActiveState is SelectionStateFire selectionState )
            selectionState.calledShotLocation = TranslateLocation( selectionState.calledShotVLocation );
      }

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

      // ============ Balanced Ammo Load ============

      public static bool OverrideDecrementAmmo ( Weapon __instance, ref int __result, int stackItemUID ) { try {
         Weapon me = __instance;
         if ( me.AmmoCategory == AmmoCategory.NotSet || ! ( me.parent is Mech mech ) ) return true;
         bool isFriend = mech.team.IsFriendly( Combat.LocalPlayerTeam );
         if ( ! ( ( Settings.BalanceAmmoLoad && isFriend ) || ( Settings.BalanceEnemyAmmoLoad && ! isFriend ) ) ) return true;

         int needAmmo = __result = me.ShotsWhenFired;
         int internalAmmo = me.InternalAmmo;
         if ( internalAmmo > 0 ) {
            if ( internalAmmo > needAmmo ) {
               me.StatCollection.ModifyStat<int>( me.uid, stackItemUID, "InternalAmmo", StatCollection.StatOperation.Int_Subtract, needAmmo, -1, true );
               return false;
            } // else {
            me.StatCollection.ModifyStat<int>( me.uid, stackItemUID, "InternalAmmo", StatCollection.StatOperation.Set, 0, -1, true );
            needAmmo -= internalAmmo;
            if ( needAmmo <= 0 ) return false;
         }

         // Yeah we are sorting everytime. Not easy to cache since it varies by structure and armour.
         SortAmmunitionBoxes( mech, me.ammoBoxes );
         needAmmo -= TryGetAmmo( me, stackItemUID, needAmmo, true ); // Above half
         if ( needAmmo <= 0 ) return false;

         needAmmo -= TryGetAmmo( me, stackItemUID, needAmmo, false ); // Below half
         __result = me.ShotsWhenFired - needAmmo;
         return false;
      }                 catch ( Exception ex ) { return Error( ex ); } }

      private static void SortAmmunitionBoxes ( Mech mech, List<AmmunitionBox> boxes ) {
         // Default load order: CT > Head > LT/RT > LA/RA > LL/RL (prioritising the weaker side).
         Dictionary<ChassisLocations, int> locationOrder = new Dictionary<ChassisLocations, int> {
            { ChassisLocations.CenterTorso, 1 },
            { ChassisLocations.Head, 2 },
            { ChassisLocations.LeftTorso, 5 },
            { ChassisLocations.RightTorso, LeftInDanger( mech, LeftTorso, LeftTorsoRear, RightTorso, RightTorsoRear ) ? 6 : 4 },
            { ChassisLocations.LeftArm, 8 },
            { ChassisLocations.RightArm, LeftInDanger( mech, LeftArm, RightArm ) ? 9 : 7 },
            { ChassisLocations.LeftLeg, 11 },
            { ChassisLocations.RightLeg, LeftInDanger( mech, LeftLeg, RightLeg ) ? 12 : 10 }
         };

         // If one leg is destroyed, boost the other leg's priority.
         if ( mech.GetLocationDamageLevel( ChassisLocations.LeftLeg ) >= LocationDamageLevel.NonFunctional )
            locationOrder[ ChassisLocations.RightLeg ] = 3;
         else if ( mech.GetLocationDamageLevel( ChassisLocations.RightLeg ) >= LocationDamageLevel.NonFunctional )
            locationOrder[ ChassisLocations.LeftLeg ] = 3;

         // Sort by location, then by ammo - draw from emptier bin first to get more bins below explosion threshold.
         boxes.Sort( ( a, b ) => {
            locationOrder.TryGetValue( (ChassisLocations) a.Location, out int aLoc );
            locationOrder.TryGetValue( (ChassisLocations) b.Location, out int bLoc );
            if ( aLoc != bLoc ) return aLoc - bLoc;
            return a.CurrentAmmo - b.CurrentAmmo;
         } );

         //Log( "Sorted Ammo: " + Join( ", ", boxes.Select( box => $"{box.UIName}@{(ChassisLocations)box.Location} ({box.CurrentAmmo})" ).ToArray() ) );
      }

      public static bool LeftInDanger ( Mech mech, ArmorLocation leftFront, ArmorLocation rightFront ) {
         return LeftInDanger( mech, leftFront, leftFront, rightFront, rightFront );
      }

      public static bool LeftInDanger ( Mech mech, ArmorLocation leftFront, ArmorLocation leftRear, ArmorLocation rightFront, ArmorLocation rightRear ) {
         ChassisLocations left  = MechStructureRules.GetChassisLocationFromArmorLocation( leftFront );
         ChassisLocations right = MechStructureRules.GetChassisLocationFromArmorLocation( rightFront );
         float leftHP = mech.GetCurrentStructure( left ), rightHP = mech.GetCurrentStructure( right );
         if ( leftHP != rightHP ) return leftHP < rightHP;
         leftHP = mech.GetCurrentArmor( leftFront );
         if ( leftFront != leftRear ) leftHP = Math.Min( leftHP, mech.GetCurrentArmor( leftRear ) );
         rightHP = mech.GetCurrentArmor( rightFront );
         if ( rightFront != rightRear ) rightHP = Math.Min( rightHP, mech.GetCurrentArmor( rightRear ) );
         return leftHP < rightHP;
      }

      private static int TryGetAmmo ( Weapon me, int stackItemUID, int maxDraw, bool aboveHalf ) {
         if ( maxDraw <= 0 ) return 0;
         int needAmmo = maxDraw;
         foreach ( AmmunitionBox box in me.ammoBoxes ) {
            int drawn = 0, ammo = box.CurrentAmmo;
            if ( ammo <= 0 ) continue;
            if ( aboveHalf ) {
               int half = box.AmmoCapacity / 2;
               if ( ammo <= half ) continue;
               drawn = Math.Min( needAmmo, ammo - half );
            } else 
               drawn = Math.Min( needAmmo, ammo );
            if ( ammo > drawn )
               box.StatCollection.ModifyStat<int>( me.uid, stackItemUID, "CurrentAmmo", StatCollection.StatOperation.Int_Subtract, drawn, -1, true );
            else
               box.StatCollection.ModifyStat<int>( me.uid, stackItemUID, "CurrentAmmo", StatCollection.StatOperation.Set, 0, -1, true );
            needAmmo -= drawn;
            if ( needAmmo <= 0 ) break;
         }
         //Log( ( aboveHalf ? "Before": "After" ) + " Half : " + Join( ", ", me.ammoBoxes.Select( box => $"{box.UIName}@{(ChassisLocations)box.Location} ({box.CurrentAmmo})" ).ToArray() ) );
         return maxDraw - needAmmo;
      }
   }
}