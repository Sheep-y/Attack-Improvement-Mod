using BattleTech.UI;
using BattleTech;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using static System.Reflection.BindingFlags;

namespace Sheepy.BattleTechMod.AttackImprovementMod {
   using static Mod;

   public class ModifierList : BattleModModule {

      public override void CombatStartsOnce () {
         if ( BattleMod.FoundMod( "io.github.guetler.CBTMovement" ) ) // Don't log to BTML unless we're sure CBTMovement is nonzero
            Warn( "CBTMovement detected.  Both jump modifier will apply; please make sure either is zero. (AIM modfiier is factored in preview; CBT Movement does not.)" );

         if ( Settings.RangedAccuracyFactors != null || Settings.MeleeAccuracyFactors != null )
            Patch( typeof( ToHit ), "GetToHitChance", "RecordAttackPosition", null );

         if ( Settings.RangedAccuracyFactors != null ) {
            InitRangedModifiers( Settings.RangedAccuracyFactors.Split( ',' ) );
            if ( RangedModifiers.Count > 0 ) {
               Patch( typeof( ToHit ), "GetAllModifiers", new Type[]{ typeof( AbstractActor ), typeof( Weapon ), typeof( ICombatant ), typeof( Vector3 ), typeof( Vector3 ), typeof( LineOfFireLevel ), typeof( bool ) }, "OverrideRangedModifiers", null );
               Patch( typeof( CombatHUDWeaponSlot ), "UpdateToolTipsFiring", NonPublic, typeof( ICombatant ), "OverrideRangedToolTips", null );
            }
         }
         if ( Settings.MeleeAccuracyFactors != null ) {
            InitMeleeModifiers( Settings.MeleeAccuracyFactors.Split( ',' ) );
            if ( MeleeModifiers.Count > 0 ) {
               contemplatingDFA = typeof( CombatHUDWeaponSlot ).GetMethod( "contemplatingDFA", NonPublic | Instance );
               if ( contemplatingDFA == null ) Warn( "CombatHUDWeaponSlot.contemplatingDFA not found, DFA will be regarded as normal melee." );
               Patch( typeof( ToHit ), "GetAllMeleeModifiers", new Type[]{ typeof( Mech ), typeof( ICombatant ), typeof( Vector3 ), typeof( MeleeAttackType ) }, "OverrideMeleeModifiers", null );
               Patch( typeof( CombatHUDWeaponSlot ), "UpdateToolTipsMelee", NonPublic, typeof( ICombatant ), "OverrideMeleeToolTips", null );
            }
         }
      }

      private static float HalfMaxMeleeVerticalOffset = 4f;

      public override void CombatStarts () {
         Hit = Combat.ToHit;
         MovementConstants con = CombatConstants.MoveConstants;
         HalfMaxMeleeVerticalOffset = con.MaxMeleeVerticalOffset / 2;
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

      private static List<Func<AttackModifier>> RangedModifiers, MeleeModifiers;
      private static CombatHUDTooltipHoverElement tip;
      private static string thisModifier;

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

      internal static HashSet<string> InitModifiers ( List<Func<AttackModifier>> list, Func<string,Func<AttackModifier>> mapper, string[] factors ) {
         HashSet<string> Factors = new HashSet<string>();
         foreach ( string e in factors ) Factors.Add( e.Trim().ToLower() );
         foreach ( string e in Factors ) try {
            Func<AttackModifier> factor = mapper( e );
            if ( factor == null ) factor = GetCommonModifierFactor( e );
            if ( factor == null )
               Warn( "Unknown accuracy component \"{0}\"", e );
            else
               list.Add( factor );
         } catch ( Exception ex ) { Error( ex ); }
         return Factors;
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
            return () => new AttackModifier( "TARGET EFFECTS", Hit.GetEnemyEffectModifier( Target ) );

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

      public static void SetToolTips ( CombatHUDWeaponSlot slot, List<Func<AttackModifier>> factors ) { try {
         AttackPos = HUD.SelectionHandler.ActiveState.PreviewPos;
         tip = slot.ToolTipHoverElement;
         thisModifier = "(Init)";
         int TotalModifiers = 0;
         foreach ( var modifier in factors ) {
            AttackModifier mod = modifier();
            thisModifier = mod.DisplayName;
            TotalModifiers += AddToolTipDetail( mod );
         }
         if ( TotalModifiers < 0 && ! CombatConstants.ResolutionConstants.AllowTotalNegativeModifier )
            TotalModifiers = 0;
         tip.BasicModifierInt = TotalModifiers;
      } catch ( Exception ) {
         // Reset before giving up
         tip?.DebuffStrings.Clear();
         tip?.BuffStrings.Clear();
         throw;
      } }

      public static float SumModifiers ( List<Func<AttackModifier>> factors ) {
         thisModifier = "(Init)";
         int TotalModifiers = 0;
         foreach ( var modifier in factors ) {
            AttackModifier mod = modifier();
            thisModifier = mod.DisplayName;
            TotalModifiers += Mathf.RoundToInt( mod.Value );
         }
         if ( TotalModifiers < 0 && ! CombatConstants.ResolutionConstants.AllowTotalNegativeModifier )
            return 0;
         return TotalModifiers;
      }

      private static int AddToolTipDetail( AttackModifier tooltip ) {
         int mod = Mathf.RoundToInt( tooltip.Value );
         if ( mod == 0 ) return 0;
         if ( mod > 0 )
            tip.DebuffStrings.Add( tooltip.DisplayName + " +" + mod );
         else // if ( mod < 0 )
            tip.BuffStrings.Add( tooltip.DisplayName + " " + mod );
         return mod;
      }

      // ============ Ranged ============

      private static bool IsMoraleAttack;
      public  static LineOfFireLevel LineOfFire { get; private set; } // Ranged only. Do not use for melee

      internal static void InitRangedModifiers ( string[] factors ) {
         RangedModifiers = new List<Func<AttackModifier>>();
         HashSet<string> Factors = InitModifiers( RangedModifiers, GetRangedModifierFactor, factors );
         Info( "Ranged modifiers ({0}): {1}", RangedModifiers.Count, Join( ",", Factors ) );
      }

      public static Func<AttackModifier> GetRangedModifierFactor ( string factorId ) {
         switch ( factorId ) {
         case "armmounted":
            return () => new AttackModifier( "ARM MOUNTED", Hit.GetSelfArmMountedModifier( AttackWeapon ) );

         case "range":
            return () => { 
               float modifier = Hit.GetRangeModifier( AttackWeapon, AttackPos, TargetPos );
               AttackModifier result = new AttackModifier( modifier );
               float range = Vector3.Distance( AttackPos, TargetPos );
               int shownRange = (int) Math.Floor( range );
               if ( range < AttackWeapon.MinRange ) return result.SetName( $"MIN RANGE ({shownRange}m)" );
               if ( range < AttackWeapon.ShortRange ) return result.SetName( $"SHORT RANGE ({shownRange}m)" );
               if ( range < AttackWeapon.MediumRange ) return result.SetName( $"MEDIUM RANGE ({shownRange}m)" );
               if ( range < AttackWeapon.LongRange ) return result.SetName( $"LONG RANGE ({shownRange}m)" );
               if ( range < AttackWeapon.MaxRange ) return result.SetName( $"MAX RANGE ({shownRange}m)" );
               return result.SetName( $"OUT OF RANGE ({shownRange}m)" );
            };

         case "height":
            return () => new AttackModifier( "HEIGHT", Hit.GetHeightModifier( AttackPos.y, TargetPos.y ) );

         case "indirect" :
            return () => new AttackModifier( "INDIRECT FIRE", Hit.GetIndirectModifier( Attacker, LineOfFire < LineOfFireLevel.LOFObstructed && AttackWeapon.IndirectFireCapable ) );

         case "locationdamage" :
            return () => {
               if ( Attacker is Mech mech ) {
                  string location = Mech.GetAbbreviatedChassisLocation( (ChassisLocations) AttackWeapon.Location );
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

      [ Harmony.HarmonyPriority( Harmony.Priority.Low ) ]
      public static bool OverrideRangedToolTips ( CombatHUDWeaponSlot __instance, ICombatant target ) { try {
         CombatHUDWeaponSlot slot = __instance;
         LineOfFire = HUD.SelectionHandler.ActiveState.FiringPreview.GetPreviewInfo( target as AbstractActor ).LOFLevel;
         IsMoraleAttack = HUD.SelectionHandler.ActiveState.SelectionType == SelectionType.FireMorale;
         SaveStates( HUD.SelectedActor, target, slot.DisplayedWeapon );
         SetToolTips( slot, RangedModifiers );
         return false;
      } catch ( Exception ex ) {
         return Error( new ApplicationException( "Error in the ranged modifier *after* '" + thisModifier + "'", ex ) );
      } }

      [ Harmony.HarmonyPriority( Harmony.Priority.Low ) ]
      public static bool OverrideRangedModifiers ( ref float __result, AbstractActor attacker, Weapon weapon, ICombatant target, Vector3 attackPosition, Vector3 targetPosition, LineOfFireLevel lofLevel, bool isCalledShot ) { try {
         LineOfFire = lofLevel;
         IsMoraleAttack = isCalledShot;
         SaveStates( attacker, target, weapon );
         __result = SumModifiers( RangedModifiers );
         return false;
      } catch ( Exception ex ) {
         return Error( new ApplicationException( "Error in the ranged modifier *after* '" + thisModifier + "'", ex ) );
      } }

      // ============ Melee ============

      private static MethodInfo contemplatingDFA;
      public  static MeleeAttackType AttackType { get; private set; }

      internal static void InitMeleeModifiers ( string[] factors ) {
         MeleeModifiers = new List<Func<AttackModifier>>();
         HashSet<string> Factors = InitModifiers( MeleeModifiers, GetMeleeModifierFactor, factors );
         Info( "Melee and DFA modifiers ({0}): {1}", MeleeModifiers.Count, Join( ",", Factors ) );
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
         return Error( new ApplicationException( "Error in the melee modifier *after* '" + thisModifier + "'", ex ) );
      } }

      [ Harmony.HarmonyPriority( Harmony.Priority.Low ) ]
      public static bool OverrideMeleeModifiers ( ref float __result, Mech attacker, ICombatant target, Vector3 targetPosition, MeleeAttackType meleeAttackType ) { try {
         AttackType = meleeAttackType;
         Weapon weapon = ( meleeAttackType == MeleeAttackType.DFA ) ? attacker.DFAWeapon : attacker.MeleeWeapon;
         SaveStates( attacker, target, weapon );
         __result = SumModifiers( MeleeModifiers );
         return false;
      } catch ( Exception ex ) {
         return Error( new ApplicationException( "Error in the melee modifier *after* '" + thisModifier + "'", ex ) );
      } }
   }
}