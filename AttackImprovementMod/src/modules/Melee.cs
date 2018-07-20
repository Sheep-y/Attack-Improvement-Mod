using BattleTech.UI;
using BattleTech;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using static System.Reflection.BindingFlags;

namespace Sheepy.BattleTechMod.AttackImprovementMod {
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

      public struct AttackModifier {
         public string DisplayName;
         public float Value;
         public AttackModifier ( string name ) : this( name, 0f ) {}
         public AttackModifier ( float modifier ) : this( "???", modifier ) {}
         public AttackModifier ( string name, float modifier ) {  DisplayName = name ?? "???"; Value = modifier; }
         public AttackModifier SetValue ( float modifier ) { Value = modifier; return this; }
         public AttackModifier SetName  ( string name ) { DisplayName = name ?? "???"; return this; }
         public AttackModifier SetName  ( string penalty, string bonus ) { DisplayName = Value >= 0 ? penalty : bonus; return this; }
      }

      private static List<Func<AttackModifier>> Modifiers = new List<Func<AttackModifier>>();

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
               Modifiers.Add( () => { AttackModifier result = new AttackModifier( "PUNCHING ARM" );
                  if ( attackType == MeleeAttackType.DFA || they is Vehicle || they.IsProne ) return result;
                  if ( us.MechDef.Chassis.PunchesWithLeftArm ) {
                     if ( us.IsLocationDestroyed( ChassisLocations.LeftArm ) ) return result;
                  } else if ( us.IsLocationDestroyed( ChassisLocations.RightArm ) ) return result;
                  return result.SetValue( CombatConstants.ToHit.ToHitSelfArmMountedWeapon );
               } ); break;

            case "dfa":
               Modifiers.Add( () => new AttackModifier( "DEATH FROM ABOVE", Hit.GetDFAModifier( attackType ) ) ); break;

            case "height":
               Modifiers.Add( () => { AttackModifier result = new AttackModifier( "HEIGHT DIFF" );
                  if ( attackType == MeleeAttackType.DFA )
                     return result.SetValue( Hit.GetHeightModifier( us.CurrentPosition.y, they.CurrentPosition.y ) );
                  float diff = attackPos.y - they.CurrentPosition.y;
                  if ( Math.Abs( diff ) < HalfMaxMeleeVerticalOffset || ( diff < 0 && ! CombatConstants.ToHit.ToHitElevationApplyPenalties ) ) return result;
                  float mod = CombatConstants.ToHit.ToHitElevationModifierPerLevel;
                  return result.SetValue( diff <= 0 ? mod : -mod );
               } ); break;

            case "inspired":
               Modifiers.Add( () => new AttackModifier( "INSPIRED", Math.Min( 0f, Hit.GetAttackerAccuracyModifier( us ) ) ) ); break;

            case "obstruction" :
               Modifiers.Add( () => new AttackModifier( "OBSTRUCTED", Hit.GetCoverModifier( us, they, HUD.SelectionHandler.ActiveState.FiringPreview.GetPreviewInfo( they ).LOFLevel ) ) ); break;

            case "refire":
               Modifiers.Add( () => new AttackModifier( "RE-ATTACK", Hit.GetRefireModifier( attackWeapon ) ) ); break;

            case "selfchassis" :
               Modifiers.Add( () => new AttackModifier( Hit.GetMeleeChassisToHitModifier( us, attackType ) ).SetName( "CHASSIS PENALTY", "CHASSIS BONUS" ) ); break;

            case "selfheat" :
               Modifiers.Add( () => new AttackModifier( "OVERHEAT", Hit.GetHeatModifier( us ) ) ); break;

            case "selfstoodup" :
               Modifiers.Add( () => new AttackModifier( "STOOD UP", Hit.GetStoodUpModifier( us ) ) ); break;

            case "selfterrain" :
               Modifiers.Add( () => new AttackModifier( "TERRAIN", Hit.GetSelfTerrainModifier( attackPos, false ) ) ); break;

            case "selfwalked" :
               Modifiers.Add( () => new AttackModifier( "ATTACK AFTER MOVE", Hit.GetSelfSpeedModifier( us ) ) ); break;

            case "sensorimpaired":
               Modifiers.Add( () => new AttackModifier( "SENSOR IMPAIRED", Math.Max( 0f, Hit.GetAttackerAccuracyModifier( us ) ) ) ); break;

            case "sprint" :
               Modifiers.Add( () => new AttackModifier( "SPRINTED", Hit.GetSelfSprintedModifier( us ) ) ); break;

            case "targeteffect" :
               Modifiers.Add( () => new AttackModifier( "TARGET EFFECTS", Hit.GetEnemyEffectModifier( they ) ) ); break;

            case "targetevasion" :
               Modifiers.Add( () => { AttackModifier result = new AttackModifier( "TARGET MOVED" );
                  if ( ! ( they is AbstractActor ) ) return result;
                  return result.SetValue( Hit.GetEvasivePipsModifier( ((AbstractActor)they).EvasivePipsCurrent, attackWeapon ) );
               } ); break;

            case "targetprone" :
               Modifiers.Add( () => new AttackModifier( "TARGET PRONE", Hit.GetTargetProneModifier( they, true ) ) ); break;

            case "targetshutdown" :
               Modifiers.Add( () => new AttackModifier( "TARGET SHUTDOWN", Hit.GetTargetShutdownModifier( they, true ) ) ); break;

            case "targetsize" :
               Modifiers.Add( () => new AttackModifier( "TARGET SIZE", Hit.GetTargetSizeModifier( they ) ) ); break;

            case "targetterrain" :
               Modifiers.Add( () => new AttackModifier( "TARGET TERRAIN", Hit.GetTargetTerrainModifier( they, they.CurrentPosition, false ) ) ); break;

            case "targetterrainmelee" : // Need to be different (an extra space) to avoid key collision
               Modifiers.Add( () => new AttackModifier( "TARGET TERRAIN ", Hit.GetTargetTerrainModifier( they, they.CurrentPosition, true ) ) ); break;

            case "weaponaccuracy" :
               Modifiers.Add( () => new AttackModifier( "WEAPON ACCURACY", Hit.GetWeaponAccuracyModifier( us, attackWeapon ) ) ); break;

            default :
               Warn( "Ignoring unknown accuracy component \"{0}\"", e ); break;
            }
         }

         Log( "Melee and DFA modifiers: " + Join( ",", Factors.ToArray() ) );
      }

      private static MethodInfo contemplatingDFA = typeof( CombatHUDWeaponSlot ).GetMethod( "contemplatingDFA", NonPublic | Instance );

      public static bool OverrideMeleeToolTips ( CombatHUDWeaponSlot __instance, ICombatant target ) { try {
         CombatHUDWeaponSlot slot = __instance;
         tip = slot.ToolTipHoverElement;
         thisModifier = "(Init)";
         attackPos = HUD.SelectionHandler.ActiveState.PreviewPos;
         bool isDFA = (bool) contemplatingDFA.Invoke( slot, new object[]{ target } );
         SaveStates( HUD.SelectedActor as Mech, target, slot.DisplayedWeapon, isDFA ? MeleeAttackType.DFA : MeleeAttackType.Punch );
         if ( Settings.ShowBaseHitchance && HUD.SelectedActor is Mech ) {
            float baseChance = RollModifier.StepHitChance( Hit.GetBaseMeleeToHitChance( HUD.SelectedActor as Mech ) ) * 100;
            tip.BuffStrings.Add( "Base Hit Chance +" + string.Format( "{0:0.#}%", baseChance ) );
         }
         int TotalModifiers = 0;
         foreach ( var modifier in Modifiers ) {
            AttackModifier mod = modifier();
            thisModifier = mod.DisplayName;
            TotalModifiers += Mathf.RoundToInt( mod.Value );
            AddToolTipDetail( mod );
         }
         tip.BasicModifierInt = TotalModifiers; //Mathf.RoundToInt( Combat.ToHit.GetAllMeleeModifiers( us, they, they.CurrentPosition, attackType ) );
         return false;
      } catch ( Exception ex ) {
         // Reset before handing over control
         tip?.DebuffStrings.Clear();
         tip?.BuffStrings.Clear();
         return Error( new ApplicationException( "Melee modifier '" + thisModifier + "' error", ex ) );
      } }

      public static void RecordAttackPosition ( Vector3 attackPosition ) {
         attackPos = attackPosition;
      }

      public static bool OverrideMeleeModifiers ( ref float __result, Mech attacker, ICombatant target, Vector3 targetPosition, MeleeAttackType meleeAttackType) { try {
         Weapon weapon = ( meleeAttackType == MeleeAttackType.DFA ) ? attacker.DFAWeapon : attacker.MeleeWeapon;
         thisModifier = "(Init)";
         SaveStates( attacker, target, weapon, meleeAttackType );
         int modifiers = 0;
         foreach ( var modifier in Modifiers ) {
            AttackModifier mod = modifier();
            thisModifier = mod.DisplayName;
            modifiers += Mathf.RoundToInt( mod.Value );
         }
         if ( modifiers < 0 && ! CombatConstants.ResolutionConstants.AllowTotalNegativeModifier )
            modifiers = 0;
         __result = modifiers;
         return false;
      } catch ( Exception ex ) {
         return Error( new ApplicationException( "Melee modifier '" + thisModifier + "' error", ex ) );
      } }

      private static void AddToolTipDetail( AttackModifier tooltip ) {
         int mod = Mathf.RoundToInt( tooltip.Value );
         if ( mod == 0 ) return;
         if ( mod > 0 )
            tip.DebuffStrings.Add( tooltip.DisplayName + " +" + mod );
         else // if ( mod < 0 )
            tip.BuffStrings.Add( tooltip.DisplayName + " " + mod );
      }
   }
}