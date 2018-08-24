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

         if ( Settings.FixFullStructureCrit ) {
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
         return CheckForCrit == null;
      } catch ( Exception ex ) {
         Error( ex );
         return false;
      } }

      [ HarmonyPriority( Priority.High ) ]
      public static bool Skip_BeatingDeadMech ( Mech __instance ) {
         if ( __instance.IsFlaggedForDeath || __instance.IsDead ) return false;
         return true;
      }

      // ============ FixFullStructureCrit ============

      private static Mech thisCritMech;

      public static void RecordCritMech ( Mech __instance ) {
         thisCritMech = __instance;
      }

      public static void ClearCritMech () {
         thisCritMech = null;
      }

      public static void RemoveFullStructureLocationsFromCritList ( Dictionary<int, float> __result ) {
         if ( thisCritMech == null ) return;
         List<ChassisLocations> fullStructureLocations = new List<ChassisLocations>(4);
         foreach ( int armorLocation in __result.Keys ) {
            ChassisLocations location = MechStructureRules.GetChassisLocationFromArmorLocation( (ArmorLocation) armorLocation );
            float curr = thisCritMech.StructureForLocation( (int) location ), max = thisCritMech.MaxStructureForLocation( (int) location );
            if ( curr == max ) fullStructureLocations.Add( location );
         }
         foreach ( ChassisLocations location in fullStructureLocations ) {
            Verbo( "Prevented {0} crit on {1} because it is not structurally damaged.", location, thisCritMech.DisplayName );
            __result.Remove( (int) location );
         }
      }

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