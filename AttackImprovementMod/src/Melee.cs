using System;

namespace Sheepy.AttackImprovementMod {
   using BattleTech;
   using BattleTech.UI;
   using System.Collections.Generic;
   using System.Reflection;
   using UnityEngine;
   using static Mod;
   using static System.Reflection.BindingFlags;

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
         if ( Settings.MeleeAccuracyComponent.Trim() != "" )
            initMeleeAccuacyOverride( Settings.MeleeAccuracyComponent.Split( ',' ) );
      }

      /*
      public static void ShowDFACalledShotPopup ( SelectionStateJump __instance ) { try {
         if ( __instance.TargetedCombatant is Vehicle )
            HUD.ShowCalledShotPopUp( __instance.SelectedActor, __instance.TargetedCombatant as AbstractActor );
      }                 catch ( Exception ex ) { Error( ex ); } }
      */

      private static float MaxMeleeVerticalOffset = 8f;
      private static float HalfMaxMeleeVerticalOffset = 4f;

      public override void CombatStarts () {
         Hit = Combat.ToHit;
         MovementConstants con = Constants.MoveConstants;
         MaxMeleeVerticalOffset = con.MaxMeleeVerticalOffset;
         HalfMaxMeleeVerticalOffset = MaxMeleeVerticalOffset / 2;
         if ( Mod.Settings.IncreaseMeleePositionChoice )
            con.NumMeleeDestinationChoices = 6;
         if ( Mod.Settings.IncreaseDFAPositionChoice )
            con.NumDFADestinationChoices = 6;
         typeof( CombatGameConstants ).GetProperty( "MoveConstants" ).SetValue( Constants, con, null );
      }

      // Almost a direct copy of the original, only to remove melee position locking code
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

      // ============ Melee Accuracy ============

      private static Action[] ToolTips;

      private static ToHit Hit;
      private static CombatHUDWeaponSlot slot;
      private static ICombatant they;
      private static Mech us;
      private static MeleeAttackType attackType;

      internal static void initMeleeAccuacyOverride ( string[] factors ) {
         HashSet<string> Factors = new HashSet<string>();
         foreach ( string e in factors ) Factors.Add( e.Trim().ToLower() );

         var tooltips = new List<Action>();
         foreach ( string e in Factors ) {
            switch ( e ) {
            case "armmounted":
               tooltips.Add( () => {
                  if ( attackType == MeleeAttackType.DFA || they is Vehicle || they.IsProne ) return;
                  if ( us.MechDef.Chassis.PunchesWithLeftArm ) {
                     if ( us.IsLocationDestroyed( ChassisLocations.LeftArm ) ) return;
                  } else if ( us.IsLocationDestroyed( ChassisLocations.RightArm ) ) return;
                  AddToolTipDetail( "PUNCHING ARM", (int) Constants.ToHit.ToHitSelfArmMountedWeapon );
               } ); break;

            case "dfa":
               tooltips.Add( () => AddToolTipDetail( "DEATH FROM ABOVE", (int) Hit.GetDFAModifier( attackType ) ) ); break;

            case "height":
               tooltips.Add( () => {
                  int mod; float diff = 0;
                  if ( attackType == MeleeAttackType.DFA ) {
                     mod = (int) Hit.GetHeightModifier( us.CurrentPosition.y, they.TargetPosition.y );
                  } else {
                     diff = HUD.SelectionHandler.ActiveState.PreviewPos.y - they.CurrentPosition.y;
                     if ( Math.Abs( diff ) < HalfMaxMeleeVerticalOffset || ( diff < 0 && ! Constants.ToHit.ToHitElevationApplyPenalties ) ) return;
                     mod = (int) Constants.ToHit.ToHitElevationModifierPerLevel;
                  }
                  AddToolTipDetail( "HEIGHT DIFF", diff <= 0 ? mod : -mod );
               } ); break;

            case "inspired":
               tooltips.Add( () => AddToolTipDetail( "INSPIRED", Math.Max( 0, (int) Hit.GetAttackerAccuracyModifier( us ) ) ) ); break;

            case "obsruction" :
               tooltips.Add( () => AddToolTipDetail( "OBSTRUCTED", (int) Hit.GetCoverModifier( us, they, HUD.SelectionHandler.ActiveState.FiringPreview.GetPreviewInfo( they ).LOFLevel ) ) ); break;

            case "refire":
               tooltips.Add( () => AddToolTipDetail( "RE-ATTACK", (int) Hit.GetRefireModifier( slot.DisplayedWeapon ) ) ); break;

            case "selfchassis" :
               tooltips.Add( () => {
                  int mod = (int) Hit.GetMeleeChassisToHitModifier( us, attackType );
                  AddToolTipDetail( mod < 0 ? "CHASSIS BONUS" : "CHASSIS PENALTY", mod );
               } ); break;

            case "selfheat" :
               tooltips.Add( () => AddToolTipDetail( "OVERHEAT", (int) Hit.GetHeatModifier( us ) ) ); break;

            case "selfstoodup" :
               tooltips.Add( () => AddToolTipDetail( "STOOD UP", (int) Hit.GetStoodUpModifier( us ) ) ); break;

            case "selfterrain" :
               tooltips.Add( () => AddToolTipDetail( "TERRAIN", (int) Hit.GetSelfTerrainModifier( HUD.SelectionHandler.ActiveState.PreviewPos, false ) ) ); break;

            case "selfwalked" :
               tooltips.Add( () => AddToolTipDetail( "MOVED SELF", (int) Hit.GetSelfSpeedModifier( us ) ) ); break;

            case "sensorimpaired":
               tooltips.Add( () => AddToolTipDetail( "SENSOR IMPAIRED", Math.Min( 0, (int) Hit.GetAttackerAccuracyModifier( us ) ) ) ); break;

            case "sprint" :
               tooltips.Add( () => AddToolTipDetail( "SPRINTED", (int) Hit.GetSelfSprintedModifier( us ) ) ); break;

            case "targeteffect" :
               tooltips.Add( () => AddToolTipDetail( "TARGET EFFECTS", (int) Hit.GetEnemyEffectModifier( they ) ) ); break;

            case "targetevasion" :
               tooltips.Add( () => {
                  if ( ! ( they is AbstractActor ) ) return;
                  AddToolTipDetail( "TARGET MOVED", (int) Hit.GetEvasivePipsModifier( ((AbstractActor)they).EvasivePipsCurrent, slot.DisplayedWeapon ) );
               } ); break;

            case "targetprone" :
               tooltips.Add( () => AddToolTipDetail( "TARGET PRONE", (int) Hit.GetTargetProneModifier( they, true ) ) ); break;

            case "targetshutdown" :
               tooltips.Add( () => AddToolTipDetail( "TARGET SHUTDOWN", (int) Hit.GetTargetShutdownModifier( they, true ) ) ); break;

            case "targetsize" :
               tooltips.Add( () => AddToolTipDetail( "TARGET SIZE", (int) Hit.GetTargetSizeModifier( they ) ) ); break;

            case "targetterrain" :
               tooltips.Add( () => AddToolTipDetail( "TARGET TERRAIN", (int) Hit.GetTargetTerrainModifier( they, they.CurrentPosition, false ) ) ); break;

            case "targetterrainmelee" :
               tooltips.Add( () => AddToolTipDetail( "TARGET TERRAIN", (int) Hit.GetTargetTerrainModifier( they, they.CurrentPosition, true ) ) ); break;

            case "weaponaccuracy" :
               tooltips.Add( () => AddToolTipDetail( "WEAPON ACCURACY", (int) Hit.GetWeaponAccuracyModifier( us, slot.DisplayedWeapon ) ) ); break;

            default :
               Warn( "Ignoring unknown accuracy component \"{0}\"", e ); break;
            }
         }
         if ( tooltips.Count > 0 ) {
            ToolTips = tooltips.ToArray();
            Patch( typeof( CombatHUDWeaponSlot ), "UpdateToolTipsMelee", NonPublic, typeof( ICombatant ), "OverrideMeleeToolTips", null );
         }

         string[] array = new string[ Factors.Count ];
         Factors.CopyTo( array );
         Log( "Melee and DFA modifiers: " + Join( ",", array ) );
      }

      private static MethodInfo contemplatingDFA = typeof( CombatHUDWeaponSlot ).GetMethod( "contemplatingDFA", NonPublic | Instance );

      public static bool OverrideMeleeToolTips ( CombatHUDWeaponSlot __instance, ICombatant target ) { try {
         slot = __instance;
         they = target;
         us = HUD.SelectedActor as Mech;
         bool isDFA = (bool) contemplatingDFA.Invoke( slot, new object[]{ they } );
         attackType = isDFA ? MeleeAttackType.DFA : MeleeAttackType.Punch;
         slot.ToolTipHoverElement.BasicModifierInt = (int) Combat.ToHit.GetAllMeleeModifiers( us, they, they.CurrentPosition, attackType );
         foreach ( var func in ToolTips )
            func();
         return false;
      }                 catch ( Exception ex ) { return Error( ex ); } }

      private static void AddToolTipDetail( string description, int modifier )
      {
         if ( modifier == 0 ) {}
         else if ( modifier > 0 )
            slot.ToolTipHoverElement.DebuffStrings.Add( description + " +" + modifier );
         else // if ( modifier < 0 )
            slot.ToolTipHoverElement.BuffStrings.Add( description + " " + modifier );
      }
   }
}