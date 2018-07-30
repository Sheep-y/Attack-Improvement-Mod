using BattleTech;
using System.Collections.Generic;
using System.Reflection;
using System;
using System.Linq;

namespace Sheepy.BattleTechMod.AttackImprovementMod {
   using static ArmorLocation;
   using static BindingFlags;
   using static Mod;

   public class HitResolution : BattleModModule {

      public override void CombatStartsOnce () {
         if ( Settings.KillZeroHpLocation ) {
            Patch( typeof( Mech )   , "DamageLocation", NonPublic, null, "FixZombieMech" );
            Patch( typeof( Vehicle ), "DamageLocation", NonPublic, null, "FixZombieVehicle" );
            Patch( typeof( Turret ) , "DamageLocation", NonPublic, null, "FixZombieTurret" );
            Patch( typeof( BattleTech.Building ), "DamageBuilding", NonPublic, null, "FixZombieBuilding" );
         }

         if ( Settings.BalanceAmmoConsumption || Settings.BalanceEnemyAmmoConsumption ) {
            postHalf = new List<ChassisLocations> { ChassisLocations.LeftTorso, ChassisLocations.RightTorso, 
               ChassisLocations.LeftArm, ChassisLocations.RightArm, ChassisLocations.LeftLeg, ChassisLocations.RightLeg };
            Patch( typeof( Weapon ), "DecrementAmmo", "OverrideDecrementAmmo", null );
         }

         if ( Settings.AutoJettisonAmmo || Settings.AutoJettisonEnemyAmmo )
            Patch( typeof( MechHeatSequence ), "setState", NonPublic, null, "AutoJettisonAmmo" );
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

      // ============ Balanced Ammo Load ============

      [ Harmony.HarmonyPriority( Harmony.Priority.Low ) ]
      public static bool OverrideDecrementAmmo ( Weapon __instance, ref int __result, int stackItemUID ) { try {
         Weapon me = __instance;
         if ( me.AmmoCategory == AmmoCategory.NotSet || ! ( me.parent is Mech mech ) ) return true;
         if ( ! FriendOrFoe( mech, Settings.BalanceAmmoConsumption, Settings.BalanceEnemyAmmoConsumption ) ) return false;

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

         if ( me.ammoBoxes.Any( e => e.CurrentAmmo > e.AmmoCapacity ) ) { // Has bins over half-full
            SortAmmunitionBoxByExplosion( mech, me.ammoBoxes );
            // Step 1: Find bins that can be immediately reduced to half.
            // Step 2: Minimise explosion chance - CT > H > LT=RT > LA=RA > LL=RR
            needAmmo -= TryGetAmmo( me, stackItemUID, needAmmo, true );
            if ( needAmmo <= 0 ) return false;
         }

         // Step 3: Spend ammo from most weakest locations - LA=RA=LL=RR=LT=RT > CT > H
         SortAmmunitionBoxByRisk( mech, me.ammoBoxes );
         needAmmo -= TryGetAmmo( me, stackItemUID, needAmmo, false );
         __result = me.ShotsWhenFired - needAmmo;
         return false;
      }                 catch ( Exception ex ) { return Error( ex ); } }

      private static void SortAmmunitionBoxByExplosion ( Mech mech, List<AmmunitionBox> boxes ) {
         Comparer<ChassisLocations> comparer = LocationSorter( mech );
         // Default load order: CT > Head > LT/RT > LA/RA > LL/RL (prioritising the weaker side).
         Dictionary<ChassisLocations, int> locationOrder = new Dictionary<ChassisLocations, int> {
            { ChassisLocations.CenterTorso, 1 },
            { ChassisLocations.Head, 2 },
            { ChassisLocations.LeftTorso, 5 },
            { ChassisLocations.RightTorso, comparer( ChassisLocations.LeftTorso, ChassisLocations.RightTorso ) < 0 ? 6 : 4 },
            { ChassisLocations.LeftArm, 8 },
            { ChassisLocations.RightArm, comparer( ChassisLocations.LeftArm, ChassisLocations.RightArm ) < 0 ? 9 : 7 },
            { ChassisLocations.LeftLeg, 11 },
            { ChassisLocations.RightLeg, comparer( ChassisLocations.LeftLeg, ChassisLocations.RightLeg ) < 0 ? 12 : 10 }
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

      private static void SortAmmunitionBoxByExplosion ( Mech mech, List<AmmunitionBox> boxes ) {
         Comparer<ChassisLocations> comparer = LocationSorter( mech );
         // Default load order: CT > Head > LT/RT > LA/RA > LL/RL (prioritising the weaker side).
         Dictionary<ChassisLocations, int> locationOrder = new Dictionary<ChassisLocations, int> {
            { ChassisLocations.CenterTorso, 1 },
            { ChassisLocations.Head, 2 },
            { ChassisLocations.LeftTorso, 5 },
            { ChassisLocations.RightTorso, comparer( ChassisLocations.LeftTorso, ChassisLocations.RightTorso ) < 0 ? 6 : 4 },
            { ChassisLocations.LeftArm, 8 },
            { ChassisLocations.RightArm, comparer( ChassisLocations.LeftArm, ChassisLocations.RightArm ) < 0 ? 9 : 7 },
            { ChassisLocations.LeftLeg, 11 },
            { ChassisLocations.RightLeg, comparer( ChassisLocations.LeftLeg, ChassisLocations.RightLeg ) < 0 ? 12 : 10 }
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
         //Log( "Sorted Ammo by explosion risk: " + Join( ", ", boxes.Select( box => $"{box.UIName}@{(ChassisLocations)box.Location} ({box.CurrentAmmo})" ).ToArray() ) );
      }

      private static List<ChassisLocations> postHalf;

      private static void SortAmmunitionBoxByRisk ( Mech mech, List<AmmunitionBox> boxes ) {
         Comparer<ChassisLocations> comparer = LocationSorter( mech );
         // Default load order: CT > Head > LT/RT > LA/RA > LL/RL (prioritising the weaker side).
         Dictionary<ChassisLocations, int> locationOrder = new Dictionary<ChassisLocations, int> {
            { ChassisLocations.CenterTorso, 12 },
            { ChassisLocations.Head, 11 }
         };
         postHalf.sort( LocationSorter( mech ) );
         for ( int i = 0 ; i < 6 ; i++ )
            locationOrder.Add( postHalf[ i ], i + 1 );

         // If one leg is destroyed, lower the other leg's priority (because it doesn't matter when it is gone)
         if ( mech.GetLocationDamageLevel( ChassisLocations.LeftLeg ) >= LocationDamageLevel.NonFunctional )
            locationOrder[ ChassisLocations.RightLeg ] = 10;
         else if ( mech.GetLocationDamageLevel( ChassisLocations.RightLeg ) >= LocationDamageLevel.NonFunctional )
            locationOrder[ ChassisLocations.LeftLeg ] = 10;

         // Sort by location, then by ammo - draw from emptier bin first.
         boxes.Sort( ( a, b ) => {
            locationOrder.TryGetValue( (ChassisLocations) a.Location, out int aLoc );
            locationOrder.TryGetValue( (ChassisLocations) b.Location, out int bLoc );
            if ( aLoc != bLoc ) return aLoc - bLoc;
            return a.CurrentAmmo - b.CurrentAmmo;
         } );
         //Log( "Sorted Ammo by lost risk: " + Join( ", ", boxes.Select( box => $"{box.UIName}@{(ChassisLocations)box.Location} ({box.CurrentAmmo})" ).ToArray() ) );
      }

      public static Comparer<ChassisLocations> LocationSorter ( Mech mech ) {
         return ( a, b ) => {
            bool aDead = mech.GetLocationDamageLevel( a ), bDead = mech.GetLocationDamageLevel( b );
            if ( aDead != bDead ) { // Dead location has last priority
               if ( aDead >= LocationDamageLevel.Destroyed  ) return 1; 
               else if ( bDead >= LocationDamageLevel.Destroyed ) return -1;
            }
            if ( aDead ) return 0; // Both destroyed.
            float aArm = WeakestArmour( mech, a ), bArm = WeakestArmour( mech, b );
            if ( aArm == 0 || bArm == 0 ) { // Armor breached!
               if ( aArm != bArm ) return aArm - bArm; // Breached one goes first
               return WeakestHP( mech, a ) - WeakestHP( mech, b ); // Both breached, compare HP.
            }
            return aArm - bArm;
         };
      }

      public static float WeakestArmour ( Mech mech, ChassisLocations location ) {
         // For side torsos, report 0 if rear armour is breached.
         if ( location == ChassisLocations.LeftTorso ) {
            if ( mech.GetCurrentArmor( LeftTorsoRear ) <= 0 ) return 0;
         } else if ( location == ChassisLocations.RightTorso ) {
            if ( mech.GetCurrentArmor( RightTorsoRear ) <= 0 ) return 0;
         }
         float armour = mech.GetCurrentArmor( MechStructureRules.GetArmorFromChassisLocation( location ) );
         // Arms are at most as strong as the side torsos
         if ( location == ChassisLocations.LeftArm )
            return Math.min( armour, WeakestArmour( mech, ChassisLocations.LeftTorso ) );
         else if ( location == ChassisLocations.RightArm )
            return Math.min( armour, WeakestArmour( mech, ChassisLocations.RightTorso ) );
         return armour;
      }

      public static float WeakestHP ( Mech mech, ChassisLocations location ) {
         if ( location == LeftArm )
            return Math.min( mech.GetCurrentStructure( ChassisLocations.LeftArm ), mech.GetCurrentStructure( ChassisLocations.LeftTorso ) );
         else if ( location == RightArm )
            return Math.min( mech.GetCurrentStructure( ChassisLocations.RightArm ), mech.GetCurrentStructure( ChassisLocations.RightTorso ) );
         return mech.GetCurrentStructure( location );
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

      // ============ Jettison Ammo ============

      public static void AutoJettisonAmmo ( MechHeatSequence __instance ) { try {
         if ( ! __instance.IsComplete ) return;
         Mech mech = __instance.OwningMech;
         if ( mech == null || mech.IsDead || mech.HasMovedThisRound || mech.IsProne || mech.IsShutDown ) return;
         if ( ! FriendOrFoe( mech, Settings.AutoJettisonAmmo, Settings.AutoJettisonEnemyAmmo ) ) return false;

         Dictionary<AmmoCategory, bool> checkedType = new Dictionary<AmmoCategory, bool>();
         List<AmmunitionBox> jettison = new List<AmmunitionBox>();
         foreach ( AmmunitionBox box in mech.ammoBoxes ) {
            if ( box.CurrentAmmo <= 0 ) continue;
            AmmoCategory type = box.ammoCategory;
            if ( checkedType.ContainsKey( type ) ) {
               if ( checkedType[ type ] ) jettison.Add( box );
               continue;
            }
            bool canUseAmmo = mech.Weapons.Any( e => e.AmmoCategory == type && e.DamageLevel < ComponentDamageLevel.NonFunctional );
            if ( ! canUseAmmo ) jettison.Add( box );
            checkedType[ type ] = ! canUseAmmo;
         }

         if ( jettison.Count <= 0 ) return;
         foreach ( AmmunitionBox box in jettison )
            box.StatCollection.ModifyStat<int>( mech.uid, __instance.SequenceGUID, "CurrentAmmo", StatCollection.StatOperation.Set, 0, -1, true );
         foreach ( AmmoCategory type in checkedType.Where( e => e.Value ).Select( e => e.Key ) )
            Combat.MessageCenter.PublishMessage( new AddSequenceToStackMessage( new ShowActorInfoSequence( mech, 
               type + " AMMO JETTISONED", FloatieMessage.MessageNature.ComponentDestroyed, false ) ) );
      }                 catch ( Exception ex ) { Error( ex ); } }

   }
}