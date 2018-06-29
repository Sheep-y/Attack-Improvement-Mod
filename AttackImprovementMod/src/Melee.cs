using System;

namespace Sheepy.AttackImprovementMod {
   using BattleTech;
   using System.Collections.Generic;
   using System.Reflection;
   using UnityEngine;
   using static Mod;

   class Melee {

      internal static void InitPatch () {
         if ( Mod.Settings.IncreaseMeleePositionChoice || Mod.Settings.IncreaseDFAPositionChoice )
            Patch( typeof( EncounterLayerParent ), "Start", null, "ClearMeleeDFALocationLimit" );
         if ( Mod.Settings.UnlockMeleePositioning )
            Patch( typeof( Pathing ), "GetMeleeDestsForTarget", typeof( AbstractActor ), "OverrideMeleeDestinations", null );
      }

      public static void ClearMeleeDFALocationLimit () { try {
         PropertyInfo move = typeof( CombatGameConstants ).GetProperty( "MoveConstants" );
         MovementConstants con = Combat.Constants.MoveConstants;
         if ( Mod.Settings.IncreaseMeleePositionChoice )
            con.NumMeleeDestinationChoices = 6;
         if ( Mod.Settings.IncreaseDFAPositionChoice )
            con.NumDFADestinationChoices = 6;
         move.SetValue( Combat.Constants, con, null );
      }                 catch ( Exception ex ) { Log( ex ); } }

      public static bool OverrideMeleeDestinations ( ref List<PathNode> __result, Pathing __instance, AbstractActor target ) { try {
         AbstractActor owner = __instance.OwningActor;
         // Not skipping AI cause them to hang up
         if ( ! owner.team.IsLocalPlayer || owner.VisibilityToTargetUnit( target ) < VisibilityLevel.LOSFull )
            return true;

         List<Vector3> adjacentPointsOnGrid = Combat.HexGrid.GetAdjacentPointsOnGrid( target.CurrentPosition );
         PathNodeGrid grids = __instance.CurrentGrid;
         MovementConstants moves = Combat.Constants.MoveConstants;

         List<PathNode> pathNodesForPoints = Pathing.GetPathNodesForPoints( adjacentPointsOnGrid, grids );
         for ( int i = pathNodesForPoints.Count - 1; i >= 0; i-- ) {
            if ( Mathf.Abs( pathNodesForPoints[i].Position.y - target.CurrentPosition.y ) > moves.MaxMeleeVerticalOffset || grids.FindBlockerReciprocal( pathNodesForPoints[i].Position, target.CurrentPosition ) )
               pathNodesForPoints.RemoveAt( i );
         }

         if ( pathNodesForPoints.Count > 1 ) {
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
      }                 catch ( Exception ex ) { return Log( ex ); } }
   }
}