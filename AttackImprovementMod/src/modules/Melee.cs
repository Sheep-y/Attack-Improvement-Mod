using BattleTech.UI;
using BattleTech;
using Harmony;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sheepy.BattleTechMod.AttackImprovementMod {
   using static Mod;
   using static System.Reflection.BindingFlags;

   public class Melee : BattleModModule {

      public override void CombatStartsOnce () {
         if ( Settings.UnlockMeleePositioning && BattleMod.FoundMod( "de.morphyum.MeleeMover", "MeleeMover.MeleeMover" ) ) {
            BattleMod.BTML_LOG.Warn( Mod.Name + " detected morphyum's MeleeMover, melee positioning unlock left in MeleeMover's hands." );
            Settings.UnlockMeleePositioning = false;
         }
         if ( Settings.UnlockMeleePositioning )
            Patch( typeof( Pathing ), "GetMeleeDestsForTarget", typeof( AbstractActor ), null, null, "UnlockMeleeDests" );
         /*
         if ( Settings.AllowDFACalledShotVehicle ) {
            Patch( typeof( SelectionStateJump ), "SetMeleeDest", NonPublic, typeof( Vector3 ), null, "ShowDFACalledShotPopup" );
         }
         */
      }

      public static IEnumerable<CodeInstruction> UnlockMeleeDests ( IEnumerable<CodeInstruction> input ) {
         return ReplaceIL( input,
            ( code ) => code.opcode.Name == "ldc.r4" && code.operand != null && code.operand.Equals( 10f ),
            ( code ) => { code.operand = 0f; return code; },
            1, "UnlockMeleePositioning", ModLog
            );
      }

      private static float MaxMeleeVerticalOffset = 8f;

      public override void CombatStarts () {
         MovementConstants con = CombatConstants.MoveConstants;
         MaxMeleeVerticalOffset = con.MaxMeleeVerticalOffset;
         if ( Settings.IncreaseMeleePositionChoice || Settings.IncreaseDFAPositionChoice ) {
            if ( Settings.IncreaseMeleePositionChoice )
               con.NumMeleeDestinationChoices = 6;
            if ( Settings.IncreaseDFAPositionChoice )
               con.NumDFADestinationChoices = 6;
            typeof( CombatGameConstants ).GetProperty( "MoveConstants" ).SetValue( CombatConstants, con, null );
         }
      }

      /*
      public static void ShowDFACalledShotPopup ( SelectionStateJump __instance ) { try {
         if ( __instance.TargetedCombatant is Vehicle )
            HUD.ShowCalledShotPopUp( __instance.SelectedActor, __instance.TargetedCombatant as AbstractActor );
      }                 catch ( Exception ex ) { Error( ex ); } }
      */
   }
}