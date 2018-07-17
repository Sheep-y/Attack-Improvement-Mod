using BattleTech.UI;
using BattleTech;
using Sheepy.BattleTechMod;
using System.Collections.Generic;
using System.Reflection;
using System;
using UnityEngine;
using static System.Reflection.BindingFlags;

namespace Sheepy.AttackImprovementMod {
   using static Mod;

   public class Melee : BattleModModule {

      public override void ModStarts () {
         if ( Settings.UnlockMeleePositioning )
            Patch( typeof( Pathing ), "GetMeleeDestsForTarget", typeof( AbstractActor ), "OverrideMeleeDestinations", null );
         /*
         if ( Settings.AllowDFACalledShotVehicle ) {
            Patch( typeof( SelectionStateJump ), "SetMeleeDest", BindingFlags.NonPublic, typeof( Vector3 ), null, "ShowDFACalledShotPopup" );
         }
         */
         if ( NullIfEmpty( ref Settings.MeleeAccuracyFactors ) != null ) {
            InitMeleeModifiers( Settings.MeleeAccuracyFactors.Split( ',' ) );
            if ( Modifiers.Count > 0 ) {
               Patch( typeof( ToHit ), "GetToHitChance", "RecordAttackPosition", null );
               Patch( typeof( ToHit ), "GetAllMeleeModifiers", new Type[]{ typeof( Mech ), typeof( ICombatant ), typeof( Vector3 ), typeof( MeleeAttackType ) }, "OverrideMeleeModifiers", null );
               Patch( typeof( CombatHUDWeaponSlot ), "UpdateToolTipsMelee", NonPublic, typeof( ICombatant ), "OverrideMeleeToolTips", null );
            }
         }
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
         MovementConstants con = CombatConstants.MoveConstants;
         MaxMeleeVerticalOffset = con.MaxMeleeVerticalOffset;
         HalfMaxMeleeVerticalOffset = MaxMeleeVerticalOffset / 2;
         if ( Settings.IncreaseMeleePositionChoice )
            con.NumMeleeDestinationChoices = 6;
         if ( Settings.IncreaseDFAPositionChoice )
            con.NumDFADestinationChoices = 6;
         typeof( CombatGameConstants ).GetProperty( "MoveConstants" ).SetValue( CombatConstants, con, null );
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

         if ( pathNodesForPoints.Count > 1 ) {
            MovementConstants moves = CombatConstants.MoveConstants;
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

      private static Dictionary<string, Func<float>> Modifiers = new Dictionary<string, Func<float>>();

      private static ToHit Hit;
      private static CombatHUDTooltipHoverElement tip;
      private static ICombatant they;
      private static Mech us;
      private static MeleeAttackType attackType;
      private static Weapon attackWeapon;
      private static Vector3 attackPos;
      private static string thisModifier;

      private static void SaveStates ( Mech attacker, ICombatant target, Weapon weapon, MeleeAttackType type ) {
         they = target;
         us = attacker;
         attackType = type;
         attackWeapon = weapon;
         thisModifier = "(init)";
      }

      internal static void InitMeleeModifiers ( string[] factors ) {
         HashSet<string> Factors = new HashSet<string>();
         foreach ( string e in factors ) Factors.Add( e.Trim().ToLower() );

         foreach ( string e in Factors ) {
            switch ( e ) {
            case "armmounted":
               Modifiers.Add( "PUNCHING ARM", () => {
                  if ( attackType == MeleeAttackType.DFA || they is Vehicle || they.IsProne ) return 0f;
                  if ( us.MechDef.Chassis.PunchesWithLeftArm ) {
                     if ( us.IsLocationDestroyed( ChassisLocations.LeftArm ) ) return 0f;
                  } else if ( us.IsLocationDestroyed( ChassisLocations.RightArm ) ) return 0f;
                  return CombatConstants.ToHit.ToHitSelfArmMountedWeapon;
               } ); break;

            case "dfa":
               Modifiers.Add( "DEATH FROM ABOVE", () => Hit.GetDFAModifier( attackType ) ); break;

            case "height":
               Modifiers.Add( "HEIGHT DIFF", () => {
                  if ( attackType == MeleeAttackType.DFA )
                     return Hit.GetHeightModifier( us.CurrentPosition.y, they.CurrentPosition.y );
                  float diff = attackPos.y - they.CurrentPosition.y;
                  if ( Math.Abs( diff ) < HalfMaxMeleeVerticalOffset || ( diff < 0 && ! CombatConstants.ToHit.ToHitElevationApplyPenalties ) ) return 0;
                  float mod = CombatConstants.ToHit.ToHitElevationModifierPerLevel;
                  return diff <= 0 ? mod : -mod;
               } ); break;

            case "inspired":
               Modifiers.Add( "INSPIRED", () => Math.Max( 0f, Hit.GetAttackerAccuracyModifier( us ) ) ); break;

            case "obsruction" :
               Modifiers.Add( "OBSTRUCTED", () => Hit.GetCoverModifier( us, they, HUD.SelectionHandler.ActiveState.FiringPreview.GetPreviewInfo( they ).LOFLevel ) ); break;

            case "refire":
               Modifiers.Add( "RE-ATTACK", () => Hit.GetRefireModifier( attackWeapon ) ); break;

            case "selfchassis" :
               Modifiers.Add( "CHASSIS PENALTY\nCHASSIS BONUS", () => Hit.GetMeleeChassisToHitModifier( us, attackType ) ); break;

            case "selfheat" :
               Modifiers.Add( "OVERHEAT", () => Hit.GetHeatModifier( us ) ); break;

            case "selfstoodup" :
               Modifiers.Add( "STOOD UP", () => Hit.GetStoodUpModifier( us ) ); break;

            case "selfterrain" :
               Modifiers.Add( "TERRAIN", () => Hit.GetSelfTerrainModifier( attackPos, false ) ); break;

            case "selfwalked" :
               Modifiers.Add( "ATTACK AFTER MOVE", () => Hit.GetSelfSpeedModifier( us ) ); break;

            case "sensorimpaired":
               Modifiers.Add( "SENSOR IMPAIRED", () => Math.Min( 0f, Hit.GetAttackerAccuracyModifier( us ) ) ); break;

            case "sprint" :
               Modifiers.Add( "SPRINTED", () => Hit.GetSelfSprintedModifier( us ) ); break;

            case "targeteffect" :
               Modifiers.Add( "TARGET EFFECTS", () => Hit.GetEnemyEffectModifier( they ) ); break;

            case "targetevasion" :
               Modifiers.Add( "TARGET MOVED", () => {
                  if ( ! ( they is AbstractActor ) ) return 0f;
                  return Hit.GetEvasivePipsModifier( ((AbstractActor)they).EvasivePipsCurrent, attackWeapon );
               } ); break;

            case "targetprone" :
               Modifiers.Add( "TARGET PRONE", () => Hit.GetTargetProneModifier( they, true ) ); break;

            case "targetshutdown" :
               Modifiers.Add( "TARGET SHUTDOWN", () => Hit.GetTargetShutdownModifier( they, true ) ); break;

            case "targetsize" :
               Modifiers.Add( "TARGET SIZE", () => (int) Hit.GetTargetSizeModifier( they ) ); break;

            case "targetterrain" :
               Modifiers.Add( "TARGET TERRAIN", () => Hit.GetTargetTerrainModifier( they, they.CurrentPosition, false ) ); break;

            case "targetterrainmelee" : // Need to be different (an extra space) to avoid key collision
               Modifiers.Add( "TARGET TERRAIN ", () => Hit.GetTargetTerrainModifier( they, they.CurrentPosition, true ) ); break;

            case "weaponaccuracy" :
               Modifiers.Add( "WEAPON ACCURACY", () => Hit.GetWeaponAccuracyModifier( us, attackWeapon ) ); break;

            default :
               Warn( "Ignoring unknown accuracy component \"{0}\"", e ); break;
            }
         }

         string[] array = new string[ Factors.Count ];
         Factors.CopyTo( array );
         Log( "Melee and DFA modifiers: " + Join( ",", array ) );
      }

      private static MethodInfo contemplatingDFA = typeof( CombatHUDWeaponSlot ).GetMethod( "contemplatingDFA", NonPublic | Instance );

      public static bool OverrideMeleeToolTips ( CombatHUDWeaponSlot __instance, ICombatant target ) { try {
         CombatHUDWeaponSlot slot = __instance;
         tip = slot.ToolTipHoverElement;
         attackPos = HUD.SelectionHandler.ActiveState.PreviewPos;
         bool isDFA = (bool) contemplatingDFA.Invoke( slot, new object[]{ target } );
         SaveStates( HUD.SelectedActor as Mech, target, slot.DisplayedWeapon, isDFA ? MeleeAttackType.DFA : MeleeAttackType.Punch );
         tip.BasicModifierInt = (int) Combat.ToHit.GetAllMeleeModifiers( us, they, they.CurrentPosition, attackType );
         foreach ( var factors in Modifiers ) {
            thisModifier = factors.Key;
            AddToolTipDetail( factors.Key, (int) factors.Value() );
         }
         return false;
      } catch ( Exception ex ) {
         // Reset before handing over control
         tip.DebuffStrings.Clear();
         tip.BuffStrings.Clear();
         return Error( new ApplicationException( "Melee modifier '" + thisModifier + "' error", ex ) );
      } }

      public static void RecordAttackPosition ( Vector3 attackPosition ) {
         attackPos = attackPosition;
      }

      public static bool OverrideMeleeModifiers ( ref float __result, Mech attacker, ICombatant target, Vector3 targetPosition, MeleeAttackType meleeAttackType) { try {
         Weapon weapon = ( meleeAttackType == MeleeAttackType.DFA ) ? attacker.DFAWeapon : attacker.MeleeWeapon;
         SaveStates( attacker, target, weapon, meleeAttackType );
         int modifiers = 0;
         foreach ( var factors in Modifiers ) {
            thisModifier = factors.Key;
            modifiers += (int) factors.Value();
         }
         if ( modifiers < 0 && ! CombatConstants.ResolutionConstants.AllowTotalNegativeModifier )
            modifiers = 0;
         __result = modifiers;
         return false;
      } catch ( Exception ex ) {
         return Error( new ApplicationException( "Melee modifier '" + thisModifier + "' error", ex ) );
      } }

      private static void AddToolTipDetail( string desc, int modifier ) {
         if ( modifier == 0 ) return;
         if ( desc.Contains( "\n" ) ) desc = desc.Split( '\n' )[ modifier < 0 ? 1 : 0 ];
         if ( modifier > 0 )
            tip.DebuffStrings.Add( desc + " +" + modifier );
         else // if ( modifier < 0 )
            tip.BuffStrings.Add( desc + " " + modifier );
      }
   }
}