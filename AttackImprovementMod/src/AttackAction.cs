using System;
using System.Reflection;

namespace Sheepy.AttackImprovementMod {
   using static Mod;
   using static System.Reflection.BindingFlags;
   using BattleTech.UI;
   using BattleTech;
   using System.Collections.Generic;

   public class AttackAction : ModModule {

      public override void InitPatch () {
         if ( Mod.Settings.FixMultiTargetBackout ) {
            Patch( typeof( CombatSelectionHandler ), "BackOutOneStep", NonPublic, null, "PreventMultiTargetBackout" );
            Patch( typeof( SelectionStateFireMulti ), "get_CanBackOut", "OverrideMultiTargetCanBackout", null );
            Patch( typeof( SelectionStateFireMulti ), "BackOut", "OverrideMultiTargetBackout", null );
            //Patch( typeof( WeaponRangeIndicators ), "ShowLinesToAllEnemies", NonPublic, "LogB", null );
         }
      }

      // ============ Fixes ============

      public static void LogB ( AbstractActor selectedActor, bool usingMultiFire, List<ICombatant> lockedTargets, bool isMelee ) {
         // Trying to debug LOS not updated correctly after backout.  State seems to be correct.
         if ( ! isMelee && usingMultiFire ) {
            List<AbstractActor> allEnemies = selectedActor.Combat.AllEnemies;
            List<ICombatant> allPossibleTargets = HUD.SelectionHandler.ActiveState.FiringPreview.AllPossibleTargets;
            Log( "{0} Enemies, {1} Possible.", allEnemies.Count, allPossibleTargets.Count );
            foreach ( ICombatant tar in allPossibleTargets )
               Log( "{0} is {1}", tar.GetPilot().Callsign, lockedTargets.Contains( tar ) ? "locked" : "unlocked" );
         }
      }


      private static bool ReAddStateData = false;

      public static void PreventMultiTargetBackout ( CombatSelectionHandler __instance ) {
         if ( ReAddStateData )
            // Re-add self state onto selection stack to prevent next backout from cancelling command
            __instance.NotifyChange( CombatSelectionHandler.SelectionChange.StateData );
      }

      public static bool OverrideMultiTargetCanBackout ( SelectionStateFireMulti __instance, ref bool __result ) {
         __result = __instance.Orders == null && __instance.AllTargetedCombatantsCount > 0;
         return false;
      }

      private static MethodInfo RemoveTargetedCombatant = typeof( SelectionStateFireMulti ).GetMethod( "RemoveTargetedCombatant", NonPublic | Instance );
      private static object[] RemoveTargetParams = new object[]{ null, false };

      public static bool OverrideMultiTargetBackout ( SelectionStateFireMulti __instance ) { try {
         SelectionStateFireMulti me = __instance;
         if ( me.AllTargetedCombatantsCount > 0 ) {
            RemoveTargetedCombatant.Invoke( me, RemoveTargetParams );
            //WeaponRangeIndicators.Instance.UpdateTargetingLines( me.SelectedActor, me.PreviewPos, me.PreviewRot, me.IsPositionLocked, me.TargetedCombatant, true, me.AllTargetedCombatants, false );
            ReAddStateData = true;
         }
         return true;
      }                 catch ( Exception ex ) { return Error( ex ); } }
   }
}