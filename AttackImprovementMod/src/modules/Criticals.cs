using BattleTech.UI;
using BattleTech;
using Harmony;
using System.Reflection;
using System;
using System.Collections.Generic;

namespace Sheepy.BattleTechMod.AttackImprovementMod {
   using static Mod;
   using static System.Reflection.BindingFlags;

   public class Criticals : BattleModModule {

      private static Type MechType = typeof( Mech );
      private static MethodInfo CheckForCrit;

      public override void CombatStartsOnce () {
         MethodInfo ResolveWeaponDamage = MechType.GetMethod( "ResolveWeaponDamage", new Type[]{ typeof( WeaponHitInfo ), typeof( Weapon ), typeof( MeleeAttackType ) } );

         if ( Settings.SkipCritingDeadMech ) 
            Patch( ResolveWeaponDamage, "Skip_BeatingDeadMech", null );

         if ( Settings.ThroughArmorCritChanceZeroArmor > 0 && HasCheckForCrit() ) {
            if ( Settings.FixFullStructureCrit ) {
               Warn( "FullStructureCrit disabled because ThroughArmorCritical is enabled, meaning full structure can be crit'ed." );
               Settings.FixFullStructureCrit = false;
            }
            armoured = new Dictionary<ArmorLocation, float>();
            damaged = new Dictionary<int, float>();
            Patch( ResolveWeaponDamage, "ThroughArmorCritical", null );
            Patch( typeof( WeaponHitInfo ), "ConsolidateCriticalHitInfo", "Override_ConsolidateCriticalHitInfo", null );

         } else if ( Settings.FixFullStructureCrit ) {
            Patch( ResolveWeaponDamage, "RecordCritMech", "ClearCritMech" );
            Patch( typeof( WeaponHitInfo ), "ConsolidateCriticalHitInfo", null, "RemoveFullStructureLocationsFromCritList" );
         }

         if ( Settings.CritFollowDamageTransfer ) {
            Patch( MechType, "TakeWeaponDamage", "RecordHitInfo", "ClearHitInfo" );
            Patch( MechType, "DamageLocation", NonPublic, "UpdateCritLocation", null );
         }
      }

      public override void CombatStarts () {
      }

      private static bool HasCheckForCrit () { try {
         if ( CheckForCrit != null ) return true;
         CheckForCrit = MechType.GetMethod( "CheckForCrit", NonPublic | Instance );
         if ( CheckForCrit == null ) Warn( "Mech.CheckForCrit not found. One or more crit features disabled." );
         return CheckForCrit != null;
      } catch ( Exception ex ) {
         Error( ex );
         return false;
      } }

      [ HarmonyPriority( Priority.High ) ]
      public static bool Skip_BeatingDeadMech ( Mech __instance ) {
         if ( __instance.IsFlaggedForDeath || __instance.IsDead ) return false;
         return true;
      }

      // ============ ThroughArmorCritical ============

      private static Dictionary<ArmorLocation, float> armoured;
      private static Dictionary<int, float> damaged;

      private static void SplitCriticalHitInfo ( Mech mech, WeaponHitInfo info, Func<float> damageFunc ) {
         if ( armoured == null || damaged == null ) return;
         
         Dictionary<ArmorLocation, float> damages = new Dictionary<ArmorLocation, float>();
         int i = 0, len = info.numberOfShots;
         if ( Settings.ThroughArmorCritThreshold > 0 ) {
            float damage = damageFunc();
            for ( ; i < len ; i++ ) {
               ArmorLocation key = (ArmorLocation) info.hitLocations[i];
               damages.TryGetValue( key, out float allDamage );
               allDamage += damage;
               damages[key] = allDamage;
            }
         } else {
            for ( ; i < len ; i++ )
               damages[ (ArmorLocation) info.hitLocations[i] ] = 1;
         }

         armoured.Clear();
         damaged.Clear();
         float threshold = (float) Settings.ThroughArmorCritThreshold;
         foreach ( var damage in damages ) {
            ArmorLocation armour = damage.Key;
            if ( armour == ArmorLocation.None || armour == ArmorLocation.Invalid ) continue;
            ChassisLocations location = MechStructureRules.GetChassisLocationFromArmorLocation( armour );
            if ( mech.IsLocationDestroyed( location ) ) continue;
            if ( mech.GetCurrentArmor( armour ) <= 0 && mech.GetCurrentStructure( location ) < mech.GetMaxStructure( location ) )
               damaged.Add( (int) armour, damage.Value );
            else if ( damage.Value > threshold )
               armoured.Add( armour, damage.Value );
            //else
            //   Info( "{0} damage ({1}) on {2} not reach threshold {3}", armour, damage.Value, mech.DisplayName, threshold );
         }
      }

      public static bool Override_ConsolidateCriticalHitInfo ( ref Dictionary<int, float> __result ) {
         if ( damaged == null ) return true;
         __result = damaged; // Use the result from SplitCriticalHitInfo
         damaged = null;
         return false;
      }

      public static void ThroughArmorCritical ( Mech __instance, WeaponHitInfo hitInfo, Weapon weapon, MeleeAttackType meleeAttackType ) { try {
         Mech me = __instance;

         SplitCriticalHitInfo( me, hitInfo, () => {
            float damage = weapon.parent == null ? weapon.DamagePerShot : weapon.DamagePerShotAdjusted( weapon.parent.occupiedDesignMask );
            AbstractActor abstractActor = me.Combat.FindActorByGUID(hitInfo.attackerId);
            LineOfFireLevel lineOfFireLevel = abstractActor.VisibilityCache.VisibilityToTarget( me ).LineOfFireLevel;
            return me.GetAdjustedDamage( damage, weapon.Category, me.occupiedDesignMask, lineOfFireLevel, false );
         } );
         
         if ( armoured == null || armoured.Count <= 0 ) return;

         foreach ( var keyValuePair in armoured ) {
            Info( $"Check through armour crit on {keyValuePair.Key}" );
         }
      }                 catch ( Exception ex ) { Error( ex ); } }

      // ============ FixFullStructureCrit ============

      private static Mech thisCritMech;

      public static void RecordCritMech ( Mech __instance ) {
         thisCritMech = __instance;
      }

      public static void ClearCritMech () {
         thisCritMech = null;
      }

      public static void RemoveFullStructureLocationsFromCritList ( Dictionary<int, float> __result ) { try {
         if ( thisCritMech == null ) return;
         HashSet<int> removeList = new HashSet<int>();
         __result.Remove( (int) ArmorLocation.None );
         __result.Remove( (int) ArmorLocation.Invalid );
         foreach ( int armourInt in __result.Keys ) {
            ChassisLocations location = MechStructureRules.GetChassisLocationFromArmorLocation( (ArmorLocation) armourInt );
            float curr = thisCritMech.StructureForLocation( (int) location ), max = thisCritMech.MaxStructureForLocation( (int) location );
            if ( curr == max ) removeList.Add( armourInt );
         }
         foreach ( ChassisLocations location in removeList ) {
            Verbo( "Prevented {0} crit on {1} because it is not structurally damaged.", location, thisCritMech.DisplayName );
            __result.Remove( (int) location );
         }
      }                 catch ( Exception ex ) { Error( ex ); } }

      // ============ CritFollowDamageTransfer ============

      private static int[] thisHitLocations;
      private static int thisHitIndex;

      public static void RecordHitInfo ( WeaponHitInfo hitInfo, int hitIndex ) {
         thisHitLocations = hitInfo.hitLocations;
         thisHitIndex = hitIndex;
      }

      public static void ClearHitInfo () {
         thisHitLocations = null;
      }

      // Update hit location so that it will be consolidated by ConsolidateCriticalHitInfo
      public static void UpdateCritLocation ( ArmorLocation aLoc ) {
         if ( thisHitLocations == null ) return;
         if ( thisHitIndex < 0 || thisHitIndex >= thisHitLocations.Length ) return;
         thisHitLocations[ thisHitIndex ] = (int) aLoc;
      }
      
   }
}