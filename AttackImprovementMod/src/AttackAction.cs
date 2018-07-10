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
            if ( targetedCombatant == null )
               Warn( "Cannot find SelectionState.targetedCombatant." );
            if ( RemoveTargetedCombatant == null )
               Error( "Cannot find RemoveTargetedCombatant(), SelectionStateFireMulti not patched" );
            else {
               Patch( typeof( CombatSelectionHandler ), "BackOutOneStep", NonPublic, null, "PreventMultiTargetBackout" );
               Patch( typeof( SelectionStateFireMulti ), "get_CanBackOut", "OverrideMultiTargetCanBackout", null );
               Patch( typeof( SelectionStateFireMulti ), "BackOut", "OverrideMultiTargetBackout", null );
               //Patch( typeof( WeaponRangeIndicators ), "ShowLinesToAllEnemies", NonPublic, "LogB", null );
            }
         }
      }

      // ============ Fixes ============

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

      private static FieldInfo targetedCombatant = typeof( SelectionState ).GetField( "targetedCombatant", NonPublic | Instance );
      private static MethodInfo RemoveTargetedCombatant = typeof( SelectionStateFireMulti ).GetMethod( "RemoveTargetedCombatant", NonPublic | Instance );
      private static object[] RemoveTargetParams = new object[]{ null, false };

      public static bool OverrideMultiTargetBackout ( SelectionStateFireMulti __instance ) { try {
         SelectionStateFireMulti me = __instance;
         int count = me.AllTargetedCombatantsCount;
         if ( count > 0 ) {
            // Change target to reset keyboard focus and thus dim cancelled target's LOS
            targetedCombatant?.SetValue( me, count > 1 ? me.AllTargetedCombatants[ count - 2 ] : null );
            RemoveTargetedCombatant.Invoke( me, RemoveTargetParams );
            ReAddStateData = true;
         }
         return true;
      }                 catch ( Exception ex ) { return Error( ex ); } }
   }
}