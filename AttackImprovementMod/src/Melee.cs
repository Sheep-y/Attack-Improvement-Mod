using System;

namespace Sheepy.AttackImprovementMod {
   using BattleTech;
   using System.Reflection;
   using static Mod;

   class Melee {

      internal static void InitPatch () {
         if ( Mod.Settings.IncreaseMeleePositionChoice || Mod.Settings.IncreaseDFAPositionChoice )
            Patch( typeof( EncounterLayerParent ), "Start", null, "ClearMeleeDFALocationLimit" );
      }

      public static void ClearMeleeDFALocationLimit () {
         PropertyInfo move = typeof( CombatGameConstants ).GetProperty( "MoveConstants" );
         try {
            MovementConstants con = Combat.Constants.MoveConstants;
            if ( Mod.Settings.IncreaseMeleePositionChoice )
               con.NumMeleeDestinationChoices = 6;
            if ( Mod.Settings.IncreaseDFAPositionChoice )
               con.NumDFADestinationChoices = 6;
            move.SetValue( Combat.Constants, con, null );
         } catch ( Exception ex ) {
            Log( ex );
         }
      }
   }
}