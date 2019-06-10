using BattleTech.UI;
using BattleTech;
using Localize;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using static System.Reflection.BindingFlags;

namespace Sheepy.BattleTechMod.AttackImprovementMod {
   using static Mod;

   public class ModifierList : BattleModModule {

      public override void CombatStartsOnce () {
         if ( HasMod( "io.github.guetler.CBTMovement" ) ) // Don't log to BTML unless we're sure CBTMovement is nonzero
            Warn( "CBTMovement detected.  Both jump modifier will apply; please make sure either is zero. (AIM modfiier is factored in preview; CBT Movement does not.)" );

         if ( Settings.RangedAccuracyFactors != null || Settings.MeleeAccuracyFactors != null || Settings.SmartIndirectFire )
            Patch( typeof( ToHit ), "GetToHitChance", "RecordAttackPosition", null );

         if ( Settings.RangedAccuracyFactors != null ) {
            InitRangedModifiers( Settings.RangedAccuracyFactors.Split( ',' ) );
            if ( HasRangedModifier() ) {
               Patch( typeof( ToHit ), "GetAllModifiers", new Type[]{ typeof( AbstractActor ), typeof( Weapon ), typeof( ICombatant ), typeof( Vector3 ), typeof( Vector3 ), typeof( LineOfFireLevel ), typeof( bool ) }, "OverrideRangedModifiers", null );
               Patch( typeof( CombatHUDWeaponSlot ), "UpdateToolTipsFiring", typeof( ICombatant ), "OverrideRangedToolTips", null );
            }
         }
         if ( HasRangedModifier() || Settings.SmartIndirectFire ) {
            Patch( typeof( ToHit ), "GetAllModifiers", new Type[]{ typeof( AbstractActor ), typeof( Weapon ), typeof( ICombatant ), typeof( Vector3 ), typeof( Vector3 ), typeof( LineOfFireLevel ), typeof( bool ) }, "SaveRangedModifierState", null );
            Patch( typeof( CombatHUDWeaponSlot ), "UpdateToolTipsFiring", typeof( ICombatant ), "SaveRangedToolTipState", null );
         }
         if ( Settings.MeleeAccuracyFactors != null ) {
            InitMeleeModifiers( Settings.MeleeAccuracyFactors.Split( ',' ) );
            if ( HasMeleeModifier() ) {
               contemplatingDFA = typeof( CombatHUDWeaponSlot ).GetMethod( "contemplatingDFA", NonPublic | Instance );
               if ( contemplatingDFA == null ) Warn( "CombatHUDWeaponSlot.contemplatingDFA not found, DFA will be regarded as normal melee." );
               Patch( typeof( ToHit ), "GetAllMeleeModifiers", new Type[]{ typeof( Mech ), typeof( ICombatant ), typeof( Vector3 ), typeof( MeleeAttackType ) }, "OverrideMeleeModifiers", null );
               Patch( typeof( CombatHUDWeaponSlot ), "UpdateToolTipsMelee", typeof( ICombatant ), "OverrideMeleeToolTips", null );
            }
         }
         if ( Settings.ReverseInCombatModifier ) {
            if ( ! HasRangedModifier() ) {
               Patch( typeof( CombatHUDWeaponSlot ), "UpdateToolTipsFiring", null, "ReverseModifiersSign" );
               Patch( typeof( CombatHUDWeaponSlot ), "UpdateToolTipsSelf", null, "ReverseModifiersSign" );
            }
            if ( ! HasMeleeModifier() )
               Patch( typeof( CombatHUDWeaponSlot ), "UpdateToolTipsMelee", null, "ReverseModifiersSign" );
            Patch( typeof( CombatHUDToolTipGeneric ), "SetNewToolTipHovering", null, "ReverseNetModifierColour" );
         }
      }

      private static float HalfMaxMeleeVerticalOffset = 4f;

      public override void CombatStarts () {
         Hit = Combat.ToHit;
         MovementConstants con = CombatConstants.MoveConstants;
         HalfMaxMeleeVerticalOffset = Settings.MaxMeleeVerticalOffsetByClass != null
            ? Melee.MaxMeleeVerticalOffsetByClass[ 1 ] / 2
            : CombatConstants.MoveConstants.MaxMeleeVerticalOffset / 2;
      }

      // ============ Common ============

      public struct AttackModifier {
         public string DisplayName;
         public float Value;
         public AttackModifier ( string name ) : this( name, 0f ) {}
         public AttackModifier ( float modifier = 0f ) : this( null, modifier ) {}
         public AttackModifier ( string name, float modifier ) {  DisplayName = name ?? "???"; Value = modifier; }
         public AttackModifier SetValue ( float modifier ) { Value = modifier; return this; }
         public AttackModifier SetName  ( string name ) { DisplayName = name ?? "???"; return this; }
         public AttackModifier SetName  ( string penalty, string bonus ) { DisplayName = Value >= 0 ? penalty : bonus; return this; }
      }

      private static Dictionary<string, Func<AttackModifier>> RangedModifiers, MeleeModifiers;
      private static CombatHUDTooltipHoverElement tip;
      private static string thisModifier;

      public static bool HasRangedModifier ( string modifier = null ) { return HasModifier( modifier, RangedModifiers ); }
      public static bool HasMeleeModifier ( string modifier = null ) { return HasModifier( modifier, MeleeModifiers ); }
      private static bool HasModifier ( string modifier, Dictionary<string, Func<AttackModifier>> Factors ) {
         if ( string.IsNullOrEmpty( modifier ) ) return Factors != null;
         return Factors.ContainsKey( modifier.ToLower() );
      }

      public  static ToHit Hit { get; private set; }
      public  static ICombatant Target { get; private set; }
      public  static AbstractActor Attacker { get; private set; }
      public  static Weapon AttackWeapon { get; private set; }
      public  static Vector3 AttackPos { get; private set; }
      public  static Vector3 TargetPos { get; private set; }

      private static void SaveStates ( AbstractActor attacker, ICombatant target, Weapon weapon ) {
         Attacker = attacker;
         Target = target;
         AttackWeapon = weapon;
         thisModifier = "(init)";
      }

      public static void RecordAttackPosition ( Vector3 attackPosition, Vector3 targetPosition ) {
         AttackPos = attackPosition;
         TargetPos = targetPosition;
      }

      internal static void InitModifiers ( ref Dictionary<string, Func<AttackModifier>> list, Func<string,Func<AttackModifier>> mapper, string[] factors ) {
         list = new Dictionary<string, Func<AttackModifier>>();
         HashSet<string> Factors = new HashSet<string>();
         foreach ( string e in factors ) Factors.Add( e?.Trim().ToLower() );
         foreach ( string e in Factors ) {
            Func<AttackModifier> factor = null;
            try {
               factor = mapper( e ) ?? GetCommonModifierFactor( e );
            } catch ( Exception ex ) { Error( ex ); }
            if ( factor == null )
               Warn( "Unknown accuracy factor \"{0}\"", e );
            else
               list.Add( e, factor );
         }
         if ( list.Count > 0 ) return;
         list = null;
      }

      public static Func<AttackModifier> GetCommonModifierFactor ( string factorId ) {
         switch ( factorId ) {
         case "direction":
            return () => {
               AttackDirection dir = Combat.HitLocation.GetAttackDirection( AttackPos, Target );
               if ( Target is Mech mech ) {
                  if ( mech.IsProne ) return new AttackModifier(); // Prone is another modifier
                  if ( dir == AttackDirection.FromFront ) return new AttackModifier( "FRONT ATTACK", Settings.ToHitMechFromFront );
                  if ( dir == AttackDirection.FromLeft || dir == AttackDirection.FromRight ) return new AttackModifier( "SIDE ATTACK" , Settings.ToHitMechFromSide );
                  if ( dir == AttackDirection.FromBack ) return new AttackModifier( "REAR ATTACK", Settings.ToHitMechFromRear );
               } else if ( Target is Vehicle vehicle ) {
                  if ( dir == AttackDirection.FromFront ) return new AttackModifier( "FRONT ATTACK", Settings.ToHitVehicleFromFront );
                  if ( dir == AttackDirection.FromLeft || dir == AttackDirection.FromRight ) return new AttackModifier( "SIDE ATTACK" , Settings.ToHitVehicleFromSide );
                  if ( dir == AttackDirection.FromBack ) return new AttackModifier( "REAR ATTACK", Settings.ToHitVehicleFromRear );
               }
               return new AttackModifier();
            };

         case "inspired":
            return () => new AttackModifier( "INSPIRED", Math.Min( 0f, Hit.GetAttackerAccuracyModifier( Attacker ) ) );

         case "jumped" :
            return () => new AttackModifier( "JUMPED", RollModifier.GetJumpedModifier( Attacker ) );

         case "selfheat" :
            return () => new AttackModifier( "OVERHEAT", Hit.GetHeatModifier( Attacker ) );

         case "selfstoodup" :
            return () => new AttackModifier( "STOOD UP", Hit.GetStoodUpModifier( Attacker ) );

         case "selfterrain" :
            return () => new AttackModifier( "TERRAIN", Hit.GetSelfTerrainModifier( AttackPos, false ) );

         case "selfterrainmelee" :
            return () => new AttackModifier( "TERRAIN", Hit.GetSelfTerrainModifier( AttackPos, true ) );

         case "sensorimpaired":
            return () => new AttackModifier( "SENSOR IMPAIRED", Math.Max( 0f, Hit.GetAttackerAccuracyModifier( Attacker ) ) );

         case "sprint" :
            return () => new AttackModifier( "SPRINTED", Hit.GetSelfSprintedModifier( Attacker ) );

         case "targeteffect" :
            return () => new AttackModifier( "TARGET EFFECTS", Hit.GetEnemyEffectModifier( Target, AttackWeapon) );

         case "targetsize" :
            return () => new AttackModifier( "TARGET SIZE", Hit.GetTargetSizeModifier( Target ) );

         case "targetterrain" :
            return () => new AttackModifier( "TARGET TERRAIN", Hit.GetTargetTerrainModifier( Target, TargetPos, false ) );

         case "targetterrainmelee" :
            return () => new AttackModifier( "TARGET TERRAIN", Hit.GetTargetTerrainModifier( Target, TargetPos, true ) );

         case "walked" :
            return () => new AttackModifier( "MOVED", Hit.GetSelfSpeedModifier( Attacker ) );

         case "weaponaccuracy" :
            return () => new AttackModifier( "WEAPON ACCURACY", Hit.GetWeaponAccuracyModifier( Attacker, AttackWeapon ) );
         }
         return null;
      }

      public static void SetToolTips ( CombatHUDWeaponSlot slot, Dictionary<string, Func<AttackModifier>> factors ) { try {
         AttackPos = ActiveState.PreviewPos;
         tip = slot.ToolTipHoverElement;
         thisModifier = "(Init)";
         int TotalModifiers = 0;
         foreach ( var modifier in factors ) {
            thisModifier = modifier.Key;
            AttackModifier mod = modifier.Value();
            TotalModifiers += AddToolTipDetail( mod );
         }
         if ( TotalModifiers < 0 && ! CombatConstants.ResolutionConstants.AllowTotalNegativeModifier )
            TotalModifiers = 0;
         tip.BasicModifierInt = Settings.ReverseInCombatModifier ? -TotalModifiers : TotalModifiers;
      } catch ( Exception ) {
         // Reset before giving up
         tip?.DebuffStrings.Clear();
         tip?.BuffStrings.Clear();
         throw;
      } }

      public static float SumModifiers ( Dictionary<string, Func<AttackModifier>> factors ) {
         thisModifier = "(Init)";
         int TotalModifiers = 0;
         foreach ( var modifier in factors ) {
            thisModifier = modifier.Key;
            AttackModifier mod = modifier.Value();
            TotalModifiers += Mathf.RoundToInt( mod.Value );
         }
         if ( TotalModifiers < 0 && ! CombatConstants.ResolutionConstants.AllowTotalNegativeModifier )
            return 0;
         return TotalModifiers;
      }

      private static int AddToolTipDetail ( AttackModifier tooltip ) {
         int mod = Mathf.RoundToInt( tooltip.Value );
         if ( mod == 0 ) return 0;
         List<Text> TipList = mod > 0 ? tip.DebuffStrings : tip.BuffStrings;
         if ( Settings.ReverseInCombatModifier ) mod = -mod;
         string numTxt = ( mod > 0 ? " +" : " " ) + mod;
         TipList.Add( new Text( tooltip.DisplayName + numTxt ) );
         return Settings.ReverseInCombatModifier ? -mod : mod;
      }

      // ============ Ranged ============

      private static bool IsMoraleAttack;
      public  static LineOfFireLevel LineOfFire { get; private set; } // Ranged only. Do not use for melee

      internal static void InitRangedModifiers ( string[] factors ) {
         InitModifiers( ref RangedModifiers, GetRangedModifierFactor, factors );
         if ( RangedModifiers != null ) Info( "Ranged modifiers ({0}): {1}", RangedModifiers.Count, RangedModifiers.Keys );
      }

      private static string SmartRange ( float min, float range, float max ) {
         if ( min <= 0 || range-min > max-range )
            return " (<" + (int) max + "m)"; // Show next range boundery when no lower boundary or target is closer to next than lower.
         return " (>" + (int) min + "m)";
      }

      public static Func<AttackModifier> GetRangedModifierFactor ( string factorId ) {
         switch ( factorId ) {
         case "armmounted":
            return () => new AttackModifier( "ARM MOUNTED", Hit.GetSelfArmMountedModifier( AttackWeapon ) );

         case "range": // Depended by ShowNeutralRangeInBreakdown
            return () => {
               Weapon w = AttackWeapon;
               float range = Vector3.Distance( AttackPos, TargetPos ), modifier = Hit.GetRangeModifierForDist( w, range );
               AttackModifier result = new AttackModifier( modifier );
               if ( range < w.MinRange ) return result.SetName( $"MIN RANGE (<{(int)w.MinRange}m)" );
               if ( range < w.ShortRange ) return result.SetName( "SHORT RANGE" + SmartRange( w.MinRange, range, w.ShortRange ) );
               if ( range < w.MediumRange ) return result.SetName( "MED RANGE" + SmartRange( w.ShortRange, range, w.MediumRange ) );
               if ( range < w.LongRange ) return result.SetName( "LONG RANGE" + SmartRange( w.MediumRange, range, w.LongRange ) );
               if ( range < w.MaxRange ) return result.SetName( "MAX RANGE" + SmartRange( w.LongRange, range, w.MaxRange ) );
               return result.SetName( $"OUT OF RANGE (>{(int)w.MaxRange}m)" );
            };

         case "height":
            return () => new AttackModifier( "HEIGHT", Hit.GetHeightModifier( AttackPos.y, TargetPos.y ) );

         case "indirect" :
            return () => new AttackModifier( "INDIRECT FIRE", Hit.GetIndirectModifier( Attacker, LineOfFire < LineOfFireLevel.LOFObstructed && AttackWeapon.IndirectFireCapable ) );

         case "locationdamage" :
            return () => {
               if ( Attacker is Mech mech ) {
                  Text location = Mech.GetAbbreviatedChassisLocation( (ChassisLocations) AttackWeapon.Location );
                  return new AttackModifier( $"{location} DAMAGED", MechStructureRules.GetToHitModifierLocationDamage( mech, AttackWeapon ) );
               } else
                  return new AttackModifier( "CHASSIS DAMAGED", Hit.GetSelfDamageModifier( Attacker, AttackWeapon ) );
            };

         case "obstruction" :
            return () => new AttackModifier( "OBSTRUCTED", Hit.GetCoverModifier( Attacker, Target, LineOfFire ) );

         case "precision":
            return () => new AttackModifier( CombatConstants.CombatUIConstants.MoraleAttackDescription.Name, Hit.GetMoraleAttackModifier( Target, IsMoraleAttack ) );

         case "refire":
            return () => new AttackModifier( "RECOIL", Hit.GetRefireModifier( AttackWeapon ) );

         case "targetevasion" :
            return () => new AttackModifier( "TARGET MOVED", Hit.GetTargetSpeedModifier( Target, AttackWeapon ) );

         case "targetprone" :
            return () => new AttackModifier( "TARGET PRONE", Hit.GetTargetProneModifier( Target, false ) );

         case "targetshutdown" :
            return () => new AttackModifier( "TARGET SHUTDOWN", Hit.GetTargetShutdownModifier( Target, false ) );

         case "sensorlock" :
            return () => new AttackModifier( "SENSOR LOCK", Hit.GetTargetDirectFireModifier( Target, LineOfFire < LineOfFireLevel.LOFObstructed && AttackWeapon.IndirectFireCapable) );

         case "weapondamage" :
            return () => {
               AttackModifier result = new AttackModifier( "WEAPON DAMAGED" );
               if ( ! ( Attacker is Mech mech ) ) return result;
               return result.SetValue( MechStructureRules.GetToHitModifierWeaponDamage( mech, AttackWeapon ) );
            };
         }
         return null;
      }

      [ Harmony.HarmonyPriority( Harmony.Priority.VeryHigh ) ]
      public static void SaveRangedToolTipState ( CombatHUDWeaponSlot __instance, ICombatant target ) { try {
         CombatHUDWeaponSlot slot = __instance;
         LineOfFire = ActiveState.FiringPreview.GetPreviewInfo( target as AbstractActor ).LOFLevel;
         IsMoraleAttack = ActiveState.SelectionType == SelectionType.FireMorale;
         SaveStates( HUD.SelectedActor, target, slot.DisplayedWeapon );
      }                 catch ( Exception ex ) { Error( ex ); } }

      [ Harmony.HarmonyPriority( Harmony.Priority.Low ) ]
      public static bool OverrideRangedToolTips ( CombatHUDWeaponSlot __instance, ICombatant target ) { try {
         SetToolTips( __instance, RangedModifiers );
         return false;
      } catch ( Exception ex ) {
         return Error( new ApplicationException( "Error in ranged modifier '" + thisModifier + "'", ex ) );
      } }

      [ Harmony.HarmonyPriority( Harmony.Priority.VeryHigh ) ]
      public static void SaveRangedModifierState ( ref float __result, AbstractActor attacker, Weapon weapon, ICombatant target, Vector3 attackPosition, Vector3 targetPosition, LineOfFireLevel lofLevel, bool isCalledShot ) { try {
         LineOfFire = lofLevel;
         IsMoraleAttack = isCalledShot;
         SaveStates( attacker, target, weapon );
      }                 catch ( Exception ex ) { Error( ex ); } }

      [ Harmony.HarmonyPriority( Harmony.Priority.Low ) ]
      public static bool OverrideRangedModifiers ( ref float __result, AbstractActor attacker, Weapon weapon, ICombatant target, Vector3 attackPosition, Vector3 targetPosition, LineOfFireLevel lofLevel, bool isCalledShot ) { try {
         __result = SumModifiers( RangedModifiers );
         return false;
      } catch ( Exception ex ) {
         return Error( new ApplicationException( "Error in ranged modifier '" + thisModifier + "'", ex ) );
      } }

      // ============ Melee ============

      private static MethodInfo contemplatingDFA;
      public  static MeleeAttackType AttackType { get; private set; }

      internal static void InitMeleeModifiers ( string[] factors ) {
         InitModifiers( ref MeleeModifiers, GetMeleeModifierFactor, factors );
         if ( MeleeModifiers != null ) Info( "Melee and DFA modifiers ({0}): {1}", MeleeModifiers.Count, MeleeModifiers.Keys );
      }

      public static Func<AttackModifier> GetMeleeModifierFactor ( string factorId ) {
         switch ( factorId ) {
         case "armmounted":
            return () => { AttackModifier result = new AttackModifier( "PUNCHING ARM" );
               if ( AttackType == MeleeAttackType.DFA || Target is Vehicle || Target.IsProne || ! ( Attacker is Mech mech ) ) return result;
               if ( mech.MechDef.Chassis.PunchesWithLeftArm ) {
                  if ( mech.IsLocationDestroyed( ChassisLocations.LeftArm ) ) return result;
               } else if ( mech.IsLocationDestroyed( ChassisLocations.RightArm ) ) return result;
               return result.SetValue( CombatConstants.ToHit.ToHitSelfArmMountedWeapon );
            };

         case "dfa":
            return () => new AttackModifier( "DEATH FROM ABOVE", Hit.GetDFAModifier( AttackType ) );

         case "height":
            return () => { AttackModifier result = new AttackModifier( "HEIGHT DIFF" );
               if ( AttackType == MeleeAttackType.DFA )
                  return result.SetValue( Hit.GetHeightModifier( Attacker.CurrentPosition.y, Target.CurrentPosition.y ) );
               float diff = AttackPos.y - Target.CurrentPosition.y;
               if ( Math.Abs( diff ) < HalfMaxMeleeVerticalOffset || ( diff < 0 && ! CombatConstants.ToHit.ToHitElevationApplyPenalties ) ) return result;
               float mod = CombatConstants.ToHit.ToHitElevationModifierPerLevel;
               return result.SetValue( diff <= 0 ? mod : -mod );
            };

         case "obstruction" :
            return () => new AttackModifier( "OBSTRUCTED", Hit.GetCoverModifier( Attacker, Target, Combat.LOS.GetLineOfFire( Attacker, AttackPos, Target, TargetPos, Target.CurrentRotation, out Vector3 collision ) ) );

         case "refire":
            return () => new AttackModifier( "RE-ATTACK", Hit.GetRefireModifier( AttackWeapon ) );

         case "selfchassis" :
            return () => new AttackModifier( Hit.GetMeleeChassisToHitModifier( Attacker, AttackType ) ).SetName( "CHASSIS PENALTY", "CHASSIS BONUS" );

         case "targetevasion" :
            return () => { AttackModifier result = new AttackModifier( "TARGET MOVED" );
               if ( ! ( Target is AbstractActor actor ) ) return result;
               return result.SetValue( Hit.GetEvasivePipsModifier( actor.EvasivePipsCurrent, AttackWeapon ) );
            };

         case "targetprone" :
            return () => new AttackModifier( "TARGET PRONE", Hit.GetTargetProneModifier( Target, true ) );

         case "targetshutdown" :
            return () => new AttackModifier( "TARGET SHUTDOWN", Hit.GetTargetShutdownModifier( Target, true ) );
         }
         return null;
      }

      [ Harmony.HarmonyPriority( Harmony.Priority.Low ) ]
      public static bool OverrideMeleeToolTips ( CombatHUDWeaponSlot __instance, ICombatant target ) { try {
         CombatHUDWeaponSlot slot = __instance;
         bool isDFA = (bool) contemplatingDFA?.Invoke( slot, new object[]{ target } );
         AttackType = isDFA ? MeleeAttackType.DFA : MeleeAttackType.Punch;
         SaveStates( HUD.SelectedActor, target, slot.DisplayedWeapon );
         SetToolTips( __instance, MeleeModifiers );
         return false;
      } catch ( Exception ex ) {
         return Error( new ApplicationException( "Error in melee modifier '" + thisModifier + "'", ex ) );
      } }

      [ Harmony.HarmonyPriority( Harmony.Priority.Low ) ]
      public static bool OverrideMeleeModifiers ( ref float __result, Mech attacker, ICombatant target, Vector3 targetPosition, MeleeAttackType meleeAttackType ) { try {
         AttackType = meleeAttackType;
         Weapon weapon = ( meleeAttackType == MeleeAttackType.DFA ) ? attacker.DFAWeapon : attacker.MeleeWeapon;
         SaveStates( attacker, target, weapon );
         __result = SumModifiers( MeleeModifiers );
         return false;
      } catch ( Exception ex ) {
         return Error( new ApplicationException( "Error in melee modifier '" + thisModifier + "'", ex ) );
      } }

      // ============ Reverse Modifier Sign ============

      public static void ReverseModifiersSign ( CombatHUDWeaponSlot __instance ) { try {
         foreach ( Text txt in __instance.ToolTipHoverElement.BuffStrings ) ReverseModifierSign( txt );
         foreach ( Text txt in __instance.ToolTipHoverElement.DebuffStrings ) ReverseModifierSign( txt );
         __instance.ToolTipHoverElement.BasicModifierInt *= -1;
      }                 catch ( Exception ex ) { Error( ex ); } }

      private static void ReverseModifierSign ( Text txt ) {
         List<Text.Part> parts = txt.m_parts;
         if ( parts != null && parts.Count == 1 && parts[0].args != null && parts[0].args.Length == 2 && parts[0].args[1] is int )
            parts[0].args[1] = - (int) parts[0].args[1];
      }

      public static void ReverseNetModifierColour ( CombatHUDToolTipGeneric __instance, bool useModifier, int BasicModifier ) { try {
         if ( ! useModifier ) return;
         UILookAndColorConstants con = uiManager.UILookAndColorConstants;
         __instance.BasicModifier.color = BasicModifier >= 0 ? con.Buff.color : con.DeBuff.color;
      }                 catch ( Exception ex ) { Error( ex ); } }
   }
}