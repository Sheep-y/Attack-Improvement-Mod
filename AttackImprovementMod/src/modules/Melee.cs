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

      public override void CombatStartsOnce () {
         if ( Settings.UnlockMeleePositioning )
            Patch( typeof( Pathing ), "GetMeleeDestsForTarget", typeof( AbstractActor ), "OverrideMeleeDestinations", null );
         /*
         if ( Settings.AllowDFACalledShotVehicle ) {
            Patch( typeof( SelectionStateJump ), "SetMeleeDest", BindingFlags.NonPublic, typeof( Vector3 ), null, "ShowDFACalledShotPopup" );
         }
         */
         if ( Settings.ShowBaseHitchance ) {
            Patch( typeof( CombatHUDWeaponSlot ), "UpdateToolTipsFiring", NonPublic, typeof( ICombatant ), "ShowBaseHitChance", null );
            Patch( typeof( CombatHUDWeaponSlot ), "UpdateToolTipsMelee", NonPublic, typeof( ICombatant ), "ShowBaseMeleeChance", null );
         }
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

      // ============ Base Chances ============

      public static void ShowBaseHitChance ( CombatHUDWeaponSlot __instance, ICombatant target ) { try {
         float baseChance = RollModifier.StepHitChance( Hit.GetBaseToHitChance( HUD.SelectedActor ) ) * 100;
         __instance.ToolTipHoverElement.BuffStrings.Add( "Base Hit Chance +" + string.Format( "{0:0.#}%", baseChance ) );
      }                 catch ( Exception ex ) { Error( ex ); } }

      public static void ShowBaseMeleeChance ( CombatHUDWeaponSlot __instance, ICombatant target ) { try {
         if ( HUD.SelectedActor is Mech ) {
            float baseChance = RollModifier.StepHitChance( Hit.GetBaseMeleeToHitChance( HUD.SelectedActor as Mech ) ) * 100;
            __instance.ToolTipHoverElement.BuffStrings.Add( "Base Hit Chance +" + string.Format( "{0:0.#}%", baseChance ) );
         }
      }                 catch ( Exception ex ) { Error( ex ); } }


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
      private static CombatHUDTooltipHoverElement tip;
      private static string thisModifier;

      public  static ToHit Hit { get; private set; }
      public  static ICombatant They { get; private set; }
      public  static Mech Us { get; private set; }
      public  static MeleeAttackType AttackType { get; private set; }
      public  static Weapon AttackWeapon { get; private set; }
      public  static Vector3 AttackPos { get; private set; }


      private static void SaveStates ( Mech attacker, ICombatant target, Weapon weapon, MeleeAttackType type ) {
         They = target;
         Us = attacker;
         AttackType = type;
         AttackWeapon = weapon;
         thisModifier = "(init)";
      }

      public static Func<AttackModifier> GetMeleeModifierFactor ( string factorId ) {
         switch ( factorId ) {
         case "armmounted":
            return () => { AttackModifier result = new AttackModifier( "PUNCHING ARM" );
               if ( AttackType == MeleeAttackType.DFA || They is Vehicle || They.IsProne ) return result;
               if ( Us.MechDef.Chassis.PunchesWithLeftArm ) {
                  if ( Us.IsLocationDestroyed( ChassisLocations.LeftArm ) ) return result;
               } else if ( Us.IsLocationDestroyed( ChassisLocations.RightArm ) ) return result;
               return result.SetValue( CombatConstants.ToHit.ToHitSelfArmMountedWeapon );
            };

         case "dfa":
            return () => new AttackModifier( "DEATH FROM ABOVE", Hit.GetDFAModifier( AttackType ) );

         case "height":
            return () => { AttackModifier result = new AttackModifier( "HEIGHT DIFF" );
               if ( AttackType == MeleeAttackType.DFA )
                  return result.SetValue( Hit.GetHeightModifier( Us.CurrentPosition.y, They.CurrentPosition.y ) );
               float diff = AttackPos.y - They.CurrentPosition.y;
               if ( Math.Abs( diff ) < HalfMaxMeleeVerticalOffset || ( diff < 0 && ! CombatConstants.ToHit.ToHitElevationApplyPenalties ) ) return result;
               float mod = CombatConstants.ToHit.ToHitElevationModifierPerLevel;
               return result.SetValue( diff <= 0 ? mod : -mod );
            };

         case "inspired":
            return () => new AttackModifier( "INSPIRED", Math.Min( 0f, Hit.GetAttackerAccuracyModifier( Us ) ) );

         case "obstruction" :
            return () => new AttackModifier( "OBSTRUCTED", Hit.GetCoverModifier( Us, They, HUD.SelectionHandler.ActiveState.FiringPreview.GetPreviewInfo( They ).LOFLevel ) );

         case "refire":
            return () => new AttackModifier( "RE-ATTACK", Hit.GetRefireModifier( AttackWeapon ) );

         case "selfchassis" :
            return () => new AttackModifier( Hit.GetMeleeChassisToHitModifier( Us, AttackType ) ).SetName( "CHASSIS PENALTY", "CHASSIS BONUS" );

         case "selfheat" :
            return () => new AttackModifier( "OVERHEAT", Hit.GetHeatModifier( Us ) );

         case "selfstoodup" :
            return () => new AttackModifier( "STOOD UP", Hit.GetStoodUpModifier( Us ) );

         case "selfterrain" :
            return () => new AttackModifier( "TERRAIN", Hit.GetSelfTerrainModifier( AttackPos, false ) );

         case "selfwalked" :
            return () => new AttackModifier( "ATTACK AFTER MOVE", Hit.GetSelfSpeedModifier( Us ) );

         case "sensorimpaired":
            return () => new AttackModifier( "SENSOR IMPAIRED", Math.Max( 0f, Hit.GetAttackerAccuracyModifier( Us ) ) );

         case "sprint" :
            return () => new AttackModifier( "SPRINTED", Hit.GetSelfSprintedModifier( Us ) );

         case "targeteffect" :
            return () => new AttackModifier( "TARGET EFFECTS", Hit.GetEnemyEffectModifier( They ) );

         case "targetevasion" :
            return () => { AttackModifier result = new AttackModifier( "TARGET MOVED" );
               if ( ! ( They is AbstractActor ) ) return result;
               return result.SetValue( Hit.GetEvasivePipsModifier( ((AbstractActor)They).EvasivePipsCurrent, AttackWeapon ) );
            };

         case "targetprone" :
            return () => new AttackModifier( "TARGET PRONE", Hit.GetTargetProneModifier( They, true ) );

         case "targetshutdown" :
            return () => new AttackModifier( "TARGET SHUTDOWN", Hit.GetTargetShutdownModifier( They, true ) );

         case "targetsize" :
            return () => new AttackModifier( "TARGET SIZE", Hit.GetTargetSizeModifier( They ) );

         case "targetterrain" :
            return () => new AttackModifier( "TARGET TERRAIN", Hit.GetTargetTerrainModifier( They, They.CurrentPosition, false ) );

         case "targetterrainmelee" : // Need to be different (an extra space) to avoid key collision
            return () => new AttackModifier( "TARGET TERRAIN ", Hit.GetTargetTerrainModifier( They, They.CurrentPosition, true ) );

         case "weaponaccuracy" :
            return () => new AttackModifier( "WEAPON ACCURACY", Hit.GetWeaponAccuracyModifier( Us, AttackWeapon ) );
         }
         return null;
      }

      internal static void InitMeleeModifiers ( string[] factors ) {
         HashSet<string> Factors = new HashSet<string>();
         foreach ( string e in factors ) Factors.Add( e.Trim().ToLower() );
         foreach ( string e in Factors ) {
            Func<AttackModifier> factor = GetMeleeModifierFactor( e );
            if ( factor == null )
               Warn( "Ignoring unknown accuracy component \"{0}\"", e );
            else
               Modifiers.Add( factor );
         }
         Log( "Melee and DFA modifiers: " + Join( ",", Factors.ToArray() ) );
      }

      private static MethodInfo contemplatingDFA = typeof( CombatHUDWeaponSlot ).GetMethod( "contemplatingDFA", NonPublic | Instance );

      public static bool OverrideMeleeToolTips ( CombatHUDWeaponSlot __instance, ICombatant target ) { try {
         CombatHUDWeaponSlot slot = __instance;
         tip = slot.ToolTipHoverElement;
         thisModifier = "(Init)";
         AttackPos = HUD.SelectionHandler.ActiveState.PreviewPos;
         bool isDFA = (bool) contemplatingDFA.Invoke( slot, new object[]{ target } );
         SaveStates( HUD.SelectedActor as Mech, target, slot.DisplayedWeapon, isDFA ? MeleeAttackType.DFA : MeleeAttackType.Punch );
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
         AttackPos = attackPosition;
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