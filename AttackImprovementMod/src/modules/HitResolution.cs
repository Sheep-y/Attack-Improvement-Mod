using BattleTech;
using System.Collections.Generic;
using System;
using System.Linq;

namespace Sheepy.BattleTechMod.AttackImprovementMod {
   using static ArmorLocation;
   using static Mod;

   public class HitResolution : BattleModModule {

      public override void CombatStartsOnce () {
         if ( Settings.KillZeroHpLocation ) {
            Patch( typeof( Mech )   , "DamageLocation", null, "FixZombieMech" );
            Patch( typeof( Vehicle ), "DamageLocation", null, "FixZombieVehicle" );
            Patch( typeof( Turret ) , "DamageLocation", null, "FixZombieTurret" );
            Patch( typeof( BattleTech.Building ), "DamageBuilding", null, "FixZombieBuilding" );
         }

         if ( Settings.ShowMissMargin ) {
            hitChance = new Dictionary<int, float>();
            Patch( typeof( AttackDirector.AttackSequence ), "GetIndividualHits", "RecordHitChance", null );
            Patch( typeof( AttackDirector.AttackSequence ), "GetClusteredHits" , "RecordHitChance", null );
            Patch( typeof( AttackDirector ), "OnAttackComplete", null, "ClearHitChance" );
            Patch( typeof( AttackDirector.AttackSequence ), "OnAttackSequenceImpact", "SetImpact", "ClearImpact" );
            Patch( typeof( FloatieMessage ).GetConstructors().FirstOrDefault( e => e.GetParameters().Length == 8 && e.GetParameters()[2].ParameterType == typeof( Localize.Text ) ), null, "ShowMissChance" );
         }

         if ( Settings.BalanceAmmoConsumption || Settings.BalanceEnemyAmmoConsumption ) {
            nonCenter = new List<ChassisLocations> { ChassisLocations.LeftTorso, ChassisLocations.RightTorso,
               ChassisLocations.LeftArm, ChassisLocations.RightArm, ChassisLocations.LeftLeg, ChassisLocations.RightLeg };
            Patch( typeof( Weapon ), "DecrementAmmo", "OverrideDecrementAmmo", null );
            Patch( typeof( AttackDirector.AttackSequence ), "GenerateNumberOfShots", null, "ClearAmmoSort" );
         }

         if ( Settings.AutoJettisonAmmo || Settings.AutoJettisonEnemyAmmo )
            Patch( typeof( MechHeatSequence ), "setState", null, "AutoJettisonAmmo" );
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

      public override void CombatEnds () {
         hitChance?.Clear();
      }

      // ============ Zombie ============

      public static void FixZombieMech ( Mech __instance, ref float totalDamage, ArmorLocation aLoc ) { try {
         if ( aLoc == ArmorLocation.None || aLoc == ArmorLocation.Invalid ) return;
         float armour = __instance.GetCurrentArmor( aLoc );
         if ( armour >= totalDamage ) return;
         KillZombie( "mech", __instance.DisplayName, armour + __instance.GetCurrentStructure( MechStructureRules.GetChassisLocationFromArmorLocation( aLoc ) ), ref totalDamage );
      }                 catch ( Exception ex ) { Error( ex ); } }

      public static void FixZombieVehicle ( Vehicle __instance, ref float totalDamage, VehicleChassisLocations vLoc ) { try {
         if ( vLoc == VehicleChassisLocations.None || vLoc == VehicleChassisLocations.Invalid ) return;
         KillZombie( "vehicle", __instance.DisplayName, __instance.GetCurrentArmor( vLoc ) + __instance.GetCurrentStructure( vLoc ), ref totalDamage );
      }                 catch ( Exception ex ) { Error( ex ); } }

      public static void FixZombieTurret ( Turret __instance, ref float totalDamage, BuildingLocation bLoc ) { try {
         if ( bLoc == BuildingLocation.None || bLoc == BuildingLocation.Invalid ) return;
         KillZombie( "turret", __instance.DisplayName, __instance.GetCurrentArmor( bLoc ) + __instance.GetCurrentStructure( bLoc ), ref totalDamage );
      }                 catch ( Exception ex ) { Error( ex ); } }

      public static void FixZombieBuilding ( BattleTech.Building __instance, ref float totalDamage ) { try {
         KillZombie( "building", __instance.DisplayName, __instance.CurrentStructure, ref totalDamage );
      }                 catch ( Exception ex ) { Error( ex ); } }

      private static void KillZombie ( string type, string name, float HP, ref float totalDamage ) {
         float newHP = HP - totalDamage;
         if ( newHP >= 1 || newHP <= 0 ) return;
         Verbo( "Upgrading damage dealt to {1} by {2} to kill zombie {0}", type, name, newHP );
         totalDamage += newHP + 0.001f;
      }

      // ============ Miss margin ============

      private static int currentImpact;
      private static float currentRoll;
      private static Dictionary<int, float> hitChance;

      public static void RecordHitChance ( ref WeaponHitInfo hitInfo, float toHitChance ) {
         if ( hitChance.ContainsKey( hitInfo.attackSequenceId ) ) return;
         hitChance.Add( hitInfo.attackSequenceId, toHitChance );
      }

      public static void ClearHitChance () { try {
         if ( Combat?.AttackDirector?.IsAnyAttackSequenceActive ?? true )
            return; // Defer if Multi-Target is not finished. Defer when in doubt.
         hitChance.Clear();
      }                 catch ( Exception ex ) { Error( ex ); } }

      public static void SetImpact ( MessageCenterMessage message ) {
         if ( ! ( message is AttackSequenceImpactMessage impactMessage ) ) return;
         WeaponHitInfo info = impactMessage.hitInfo;
         currentImpact = info.attackSequenceId;
         currentRoll = info.toHitRolls[ impactMessage.hitIndex ];
      }
      
      public static void ClearImpact () {
         currentImpact = 0;
      }

      public static void ShowMissChance ( FloatieMessage __instance, FloatieMessage.MessageNature nature ) { try {
         if ( currentImpact == 0 || ( nature != FloatieMessage.MessageNature.Miss && nature != FloatieMessage.MessageNature.MeleeMiss ) ) return;
         if ( ! hitChance.TryGetValue( currentImpact, out float chance ) ) return;
         __instance.SetText( new Localize.Text( "Miss {0:0}%", new object[]{ ( currentRoll - chance ) * 100 } ) );
      }                 catch ( Exception ex ) { Error( ex ); } }

      // ============ Balanced Ammo Load ============

      private static Dictionary<ChassisLocations, int> ByExplosion, ByRisk;

      [ Harmony.HarmonyPriority( Harmony.Priority.Low ) ]
      public static bool OverrideDecrementAmmo ( Weapon __instance, ref int __result, int stackItemUID ) { try {
         Weapon me = __instance;
         if ( me.AmmoCategoryValue.Is_NotSet || ! ( me.parent is Mech mech ) ) return true;
         if ( ! FriendOrFoe( mech, Settings.BalanceAmmoConsumption, Settings.BalanceEnemyAmmoConsumption ) ) return true;

         int needAmmo = __result = me.ShotsWhenFired;
         if ( needAmmo <= 0 ) return false;

         int internalAmmo = me.InternalAmmo;
         if ( internalAmmo > 0 ) {
            if ( internalAmmo > needAmmo ) {
               SubtractAmmo( me, me.uid, stackItemUID, needAmmo, "InternalAmmo" );
               return false;
            } // else {
            ZeroAmmo( me, me.uid, stackItemUID, "InternalAmmo" );
            needAmmo -= internalAmmo;
            if ( needAmmo <= 0 ) return false;
         }

         List<AmmunitionBox> boxes = me.ammoBoxes.Where( e => e.CurrentAmmo > 0 ).ToList();
         if ( boxes.Count > 0 ) {
            if ( boxes.Count > 1 ) {
               if ( me.ammoBoxes.Any( e => e.CurrentAmmo > e.AmmoCapacity / 2 ) ) {
                  //Log( $"Draw up to {needAmmo} ammo for {me.UIName} prioritising explosion control." );
                  // Step 1: Draw from bins that can be immediately reduced to half immediately or almost immediately.
                  ByExplosion = SortAmmoBoxByExplosion( mech, boxes, ByExplosion );
                  needAmmo -= TryGetHalfAmmo( me, boxes, stackItemUID, needAmmo, needAmmo );
                  needAmmo -= TryGetHalfAmmo( me, boxes, stackItemUID, needAmmo, needAmmo*2 );
                  // Step 2: Minimise explosion chance - CT > H > LT=RT > LA=RA > LL=RR
                  needAmmo -= TryGetHalfAmmo( me, boxes, stackItemUID, needAmmo, Int32.MaxValue );
                  if ( needAmmo <= 0 ) return false;
               }
               // Step 3: Spend ammo from weakest locations - LA=RA=LL=RR=LT=RT > CT > H
               //Log( $"Draw up to {needAmmo} ammo for {me.UIName} prioritising lost risk." );
               ByRisk = SortAmmoBoxByRisk( mech, boxes, ByRisk );
            }
            needAmmo -= TryGetAmmo( me, boxes, stackItemUID, needAmmo );
         }
         __result = me.ShotsWhenFired - needAmmo;
         return false;
      }                 catch ( Exception ex ) { return Error( ex ); } }

      public static void ClearAmmoSort () {
         ByExplosion = ByRisk = null;
      }

      private static Dictionary<ChassisLocations, int> SortAmmoBoxByExplosion ( Mech mech, List<AmmunitionBox> boxes, Dictionary<ChassisLocations, int> locationOrder ) {
         if ( boxes.Count <= 1 ) return locationOrder;
         if ( locationOrder == null ) locationOrder = SortLocationByExplosion( mech, boxes );
         // Sort by location, then by ammo - draw from emptier bin first to get more bins below explosion threshold.
         boxes.Sort( ( a, b ) => {
            locationOrder.TryGetValue( (ChassisLocations) a.Location, out int aLoc );
            locationOrder.TryGetValue( (ChassisLocations) b.Location, out int bLoc );
            if ( aLoc != bLoc ) return aLoc - bLoc;
            return a.CurrentAmmo - b.CurrentAmmo;
         } );
         //Log( "Sorted Ammo by explosion control: " + Join( ", ", boxes.Select( box => $"{box.UIName}@{(ChassisLocations)box.Location} ({box.CurrentAmmo})" ).ToArray() ) );
         return locationOrder;
      }

      private static Dictionary<ChassisLocations, int> SortLocationByExplosion ( Mech mech, List<AmmunitionBox> boxes ) {
         if ( boxes.Count <= 1 ) return null;
         IComparer<ChassisLocations> comparer = LocationSorter( mech );
         // Load order with explosion risks: CT > Head > LT/RT > LA/RA > LL/RL (prioritising the weaker side).
         Dictionary<ChassisLocations, int> locationOrder = new Dictionary<ChassisLocations, int> {
            { ChassisLocations.CenterTorso, 1 },
            { ChassisLocations.Head, 2 },
            { ChassisLocations.LeftTorso, 5 },
            { ChassisLocations.RightTorso, comparer.Compare( ChassisLocations.LeftTorso, ChassisLocations.RightTorso ) < 0 ? 6 : 4 },
            { ChassisLocations.LeftArm, 8 },
            { ChassisLocations.RightArm, comparer.Compare( ChassisLocations.LeftArm, ChassisLocations.RightArm ) < 0 ? 9 : 7 },
            { ChassisLocations.LeftLeg, 11 },
            { ChassisLocations.RightLeg, comparer.Compare( ChassisLocations.LeftLeg, ChassisLocations.RightLeg ) < 0 ? 12 : 10 }
         };

         // If one leg is destroyed, boost the other leg's priority.
         if ( mech.GetLocationDamageLevel( ChassisLocations.LeftLeg ) >= LocationDamageLevel.NonFunctional )
            locationOrder[ ChassisLocations.RightLeg ] = 3;
         else if ( mech.GetLocationDamageLevel( ChassisLocations.RightLeg ) >= LocationDamageLevel.NonFunctional )
            locationOrder[ ChassisLocations.LeftLeg ] = 3;
         //Log( "Sorted Location by explosion control." );
         return locationOrder;
      }

      private static List<ChassisLocations> nonCenter;

      private static Dictionary<ChassisLocations, int> SortAmmoBoxByRisk ( Mech mech, List<AmmunitionBox> boxes, Dictionary<ChassisLocations, int> locationOrder ) {
         if ( boxes.Count <= 1 ) return locationOrder;
         if ( locationOrder == null ) locationOrder = SortLocationByRisk( mech, boxes );
         // Sort by location, then by ammo - draw from emptier bin first.
         boxes.Sort( ( a, b ) => {
            locationOrder.TryGetValue( (ChassisLocations) a.Location, out int aLoc );
            locationOrder.TryGetValue( (ChassisLocations) b.Location, out int bLoc );
            if ( aLoc != bLoc ) return aLoc - bLoc;
            return a.CurrentAmmo - b.CurrentAmmo;
         } );
         //Log( "Sorted Ammo by lost risk: " + Join( ", ", boxes.Select( box => $"{box.UIName}@{(ChassisLocations)box.Location} ({box.CurrentAmmo})" ).ToArray() ) );
         return locationOrder;
      }

      private static Dictionary<ChassisLocations, int> SortLocationByRisk ( Mech mech, List<AmmunitionBox> boxes ) {
         if ( boxes.Count <= 1 ) return null;
         // Load order when no explosion risks: LT/RT/LA/RA/LL/RL > CT > Head
         Dictionary<ChassisLocations, int> locationOrder = new Dictionary<ChassisLocations, int> {
            { ChassisLocations.CenterTorso, 9 },
            { ChassisLocations.Head, 8 }
         };
         nonCenter.Sort( LocationSorter( mech ) );
         for ( int i = 0 ; i < 6 ; i++ )
            locationOrder.Add( nonCenter[ i ], i + 1 );

         // If one leg is destroyed, lower the other leg's priority (because it doesn't matter when it is gone)
         if ( mech.GetLocationDamageLevel( ChassisLocations.LeftLeg ) >= LocationDamageLevel.NonFunctional )
            locationOrder[ ChassisLocations.RightLeg ] = 7;
         else if ( mech.GetLocationDamageLevel( ChassisLocations.RightLeg ) >= LocationDamageLevel.NonFunctional )
            locationOrder[ ChassisLocations.LeftLeg ] = 7;
         //Log( "Sorted Location by lost risk." );
         return locationOrder;
      }

      // https://stackoverflow.com/a/16839662/893578 by Timothy Shields
      public class FunctionalComparer<T> : IComparer<T> {
         private readonly Func<T, T, int> comparer;
         public FunctionalComparer ( Func<T, T, int> comparer ) { this.comparer = comparer; }
         public int Compare ( T x, T y ) { return comparer( x, y ); }
      }
      private static int Compare ( float a, float b ) { return a > b ? 1 : ( a < b ? -1 : 0 ); }

      private static IComparer<ChassisLocations> LocationSorter ( Mech mech ) {
         return new FunctionalComparer<ChassisLocations>( ( a, b ) => {
            LocationDamageLevel aDead = mech.GetLocationDamageLevel( a ), bDead = mech.GetLocationDamageLevel( b );
            if ( aDead != bDead ) { // Dead location has last priority
               if ( aDead >= LocationDamageLevel.Destroyed  ) return 1;
               else if ( bDead >= LocationDamageLevel.Destroyed ) return -1;
            }
            if ( aDead >= LocationDamageLevel.Destroyed ) return 0; // Both destroyed.
            float aArm = WeakestArmour( mech, a ), bArm = WeakestArmour( mech, b );
            if ( aArm == 0 || bArm == 0 ) { // Armor breached!
               if ( aArm != bArm ) return Compare( aArm, bArm ); // Breached one goes first
               return Compare( WeakestHP( mech, a ), WeakestHP( mech, b ) ); // Both breached, compare HP.
            }
            return Compare( aArm, bArm );
         } );
      }

      private static float WeakestArmour ( Mech mech, ChassisLocations location ) {
         // For side torsos, report 0 if rear armour is breached.
         if ( location == ChassisLocations.LeftTorso ) {
            if ( mech.GetCurrentArmor( LeftTorsoRear ) <= 0 ) return 0;
         } else if ( location == ChassisLocations.RightTorso ) {
            if ( mech.GetCurrentArmor( RightTorsoRear ) <= 0 ) return 0;
         }
         float armour = mech.GetCurrentArmor( (ArmorLocation) location );
         // Arms are at most as strong as the side torsos
         if ( location == ChassisLocations.LeftArm )
            return Math.Min( armour, WeakestArmour( mech, ChassisLocations.LeftTorso ) );
         else if ( location == ChassisLocations.RightArm )
            return Math.Min( armour, WeakestArmour( mech, ChassisLocations.RightTorso ) );
         return armour;
      }

      private static float WeakestHP ( Mech mech, ChassisLocations location ) {
         if ( location == ChassisLocations.LeftArm )
            return Math.Min( mech.GetCurrentStructure( ChassisLocations.LeftArm  ), mech.GetCurrentStructure( ChassisLocations.LeftTorso  ) );
         else if ( location == ChassisLocations.RightArm )
            return Math.Min( mech.GetCurrentStructure( ChassisLocations.RightArm ), mech.GetCurrentStructure( ChassisLocations.RightTorso ) );
         return mech.GetCurrentStructure( location );
      }

      private static int TryGetHalfAmmo ( Weapon me, List<AmmunitionBox> boxes, int stackItemUID, int maxDraw, int threshold ) {
         if ( maxDraw <= 0 ) return 0;
         int needAmmo = maxDraw;
         foreach ( AmmunitionBox box in boxes ) {
            int ammo = box.CurrentAmmo, spare = ammo - (int) Math.Ceiling( box.AmmoCapacity / 2f ) + 1;
            if ( spare <= 0 || spare > threshold ) continue;
            needAmmo -= SubtractAmmo( box, me.uid, stackItemUID, Math.Min( needAmmo, spare ) );
            if ( needAmmo <= 0 ) return maxDraw;
         }
         return maxDraw - needAmmo;
      }

      private static int TryGetAmmo ( Weapon weapon, List<AmmunitionBox> boxes, int stackItemUID, int maxDraw ) {
         int needAmmo = maxDraw;
         foreach ( AmmunitionBox box in boxes ) {
            int ammo = box.CurrentAmmo, drawn = Math.Min( needAmmo, ammo );
            if ( ammo <= 0 ) continue;
            if ( ammo > drawn )
               SubtractAmmo( box, weapon.uid, stackItemUID, drawn );
            else
               ZeroAmmo( box, weapon.uid, stackItemUID );
            needAmmo -= drawn;
            if ( needAmmo <= 0 ) return maxDraw;
         }
         return maxDraw - needAmmo;
      }

      private static int SubtractAmmo ( MechComponent box, string sourceID, int stackItemUID, int howMany, string ammoType = "CurrentAmmo" ) {
         //if ( box is AmmunitionBox ammo ) Log( $"Draw {howMany} ammo from {box.UIName}@{(ChassisLocations)box.Location} ({ammo.CurrentAmmo})" );
         box.StatCollection.ModifyStat<int>( sourceID, stackItemUID, ammoType, StatCollection.StatOperation.Int_Subtract, howMany, -1, true );
         return howMany;
      }

      private static void ZeroAmmo ( MechComponent box, string sourceID, int stackItemUID, string ammoType = "CurrentAmmo" ) {
         //if ( box is AmmunitionBox ammo ) Log( $"Draw all ammo from {box.UIName}@{(ChassisLocations)box.Location} ({ammo.CurrentAmmo})" );
         box.StatCollection.ModifyStat<int>( sourceID, stackItemUID, ammoType, StatCollection.StatOperation.Set, 0, -1, true );
      }

      // ============ Jettison Ammo ============

      public static void AutoJettisonAmmo ( MechHeatSequence __instance ) { try {
         if ( ! __instance.IsComplete ) return;
         Mech mech = __instance.OwningMech;
         if ( mech == null || mech.IsDead || mech.HasMovedThisRound || mech.IsProne || mech.IsShutDown ) return;
         if ( ! FriendOrFoe( mech, Settings.AutoJettisonAmmo, Settings.AutoJettisonEnemyAmmo ) ) return;

         Dictionary<long, bool> checkedType = new Dictionary<long, bool>();
         List<AmmunitionBox> jettison = new List<AmmunitionBox>();
         foreach ( AmmunitionBox box in mech.ammoBoxes ) {
            if ( box.CurrentAmmo <= 0 ) continue;
            long type = box.ammoCategoryValue.AmmoCategoryID;
            if ( checkedType.ContainsKey( type ) ) {
               if ( checkedType[ type ] ) jettison.Add( box );
               continue;
            }
            bool canUseAmmo = mech.Weapons.Any( e => e.AmmoCategoryValue.AmmoCategoryID == type && e.DamageLevel < ComponentDamageLevel.NonFunctional );
            if ( ! canUseAmmo ) jettison.Add( box );
            checkedType[ type ] = ! canUseAmmo;
         }

         if ( jettison.Count <= 0 ) return;
         foreach ( AmmunitionBox box in jettison ) ZeroAmmo( box, mech.uid, __instance.SequenceGUID );
         foreach ( AmmoCategory type in checkedType.Where( e => e.Value ).Select( e => e.Key ) )
            Combat.MessageCenter.PublishMessage( new AddSequenceToStackMessage( new ShowActorInfoSequence( mech,
               type + " AMMO JETTISONED", FloatieMessage.MessageNature.Buff, false ) ) );
      }                 catch ( Exception ex ) { Error( ex ); } }

   }
}