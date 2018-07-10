using System;

namespace Sheepy.AttackImprovementMod {
   using BattleTech;
   using BattleTech.UI;
   using System.Collections.Generic;
   using UnityEngine;
   using static Mod;

   public class Melee : ModModule {

      public override void InitPatch () {
         ModSettings Settings = Mod.Settings;
         if ( Settings.UnlockMeleePositioning )
            Patch( typeof( Pathing ), "GetMeleeDestsForTarget", typeof( AbstractActor ), "OverrideMeleeDestinations", null );
         /*
         if ( Settings.AllowDFACalledShotVehicle ) {
            Patch( typeof( SelectionStateJump ), "SetMeleeDest", BindingFlags.NonPublic, typeof( Vector3 ), null, "ShowDFACalledShotPopup" );
         }
         */
      }

      public static void ShowDFACalledShotPopup ( SelectionStateJump __instance ) { try {
         if ( __instance.TargetedCombatant is Vehicle )
            HUD.ShowCalledShotPopUp( __instance.SelectedActor, __instance.TargetedCombatant as AbstractActor );
      }                 catch ( Exception ex ) { Error( ex ); } }

      private static float MaxMeleeVerticalOffset = 8f;

      public override void CombatStarts () {
         MovementConstants con = Constants.MoveConstants;
         MaxMeleeVerticalOffset = con.MaxMeleeVerticalOffset;
         if ( Mod.Settings.IncreaseMeleePositionChoice )
            con.NumMeleeDestinationChoices = 6;
         if ( Mod.Settings.IncreaseDFAPositionChoice )
            con.NumDFADestinationChoices = 6;
         typeof( CombatGameConstants ).GetProperty( "MoveConstants" ).SetValue( Constants, con, null );
      }

      public static bool OverrideMeleeDestinations ( ref List<PathNode> __result, Pathing __instance, AbstractActor target ) { try {
         AbstractActor owner = __instance.OwningActor;
         // Not skipping AI cause them to hang up
         if ( ! owner.team.IsLocalPlayer || owner.VisibilityToTargetUnit( target ) < VisibilityLevel.LOSFull )
            return true;

         Vector3 pos = target.CurrentPosition;
         float currentY = pos.y;
         PathNodeGrid grids = __instance.CurrentGrid;

         List<Vector3> adjacentPointsOnGrid = Combat.HexGrid.GetAdjacentPointsOnGrid( pos );
         List<PathNode> pathNodesForPoints = Pathing.GetPathNodesForPoints( adjacentPointsOnGrid, grids );
         for ( int i = pathNodesForPoints.Count - 1; i >= 0; i-- ) {
            if ( Mathf.Abs( pathNodesForPoints[i].Position.y - currentY ) > MaxMeleeVerticalOffset || grids.FindBlockerReciprocal( pathNodesForPoints[i].Position, pos ) )
               pathNodesForPoints.RemoveAt( i );
         }

         if ( ! Mod.Settings.IncreaseMeleePositionChoice && pathNodesForPoints.Count > 1 ) {
            MovementConstants moves = Constants.MoveConstants;
            if ( moves.SortMeleeHexesByPathingCost )
               pathNodesForPoints.Sort( (a, b) => a.CostToThisNode.CompareTo( b.CostToThisNode ) );
            else
               pathNodesForPoints.Sort( (a, b) => Vector3.Distance( a.Position, owner.CurrentPosition ).CompareTo( Vector3.Distance( b.Position, owner.CurrentPosition ) ) );

            int num = pathNodesForPoints.Count, max = moves.NumMeleeDestinationChoices;
            if ( num > max )
               pathNodesForPoints.RemoveRange( max - 1, num - max );
         }
         __result = pathNodesForPoints;
         return false;
      }                 catch ( Exception ex ) { return Error( ex ); } }
   }
}