using BattleTech;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sheepy.BattleTechMod.AttackImprovementMod {
   using static Mod;

   public class RollModifier : BattleModModule {

      public override void CombatStartsOnce () {
         Type ToHitType = typeof( ToHit );
         if ( Settings.AllowNetBonusModifier && ! Settings.DiminishingHitChanceModifier )
            Patch( ToHitType, "GetSteppedValue", new Type[]{ typeof( float ), typeof( float ) }, "ProcessNetBonusModifier", null );
         if ( Settings.BaseHitChanceModifier != 0f )
            Patch( ToHitType, "GetBaseToHitChance", new Type[]{ typeof( AbstractActor ) }, null, "ModifyBaseHitChance" );
         if ( Settings.MeleeHitChanceModifier != 0f )
            Patch( ToHitType, "GetBaseMeleeToHitChance", new Type[]{ typeof( Mech ) }, null, "ModifyBaseMeleeHitChance" );
         /*
         if ( Settings.FixModifierTargetHeight ) {
            Patch( ToHitType, "GetAllModifiers", "FixAllModifiersTargetHeight", null );
            Patch( ToHitType, "GetAllMeleeModifiers", "FixAllModifiersTargetHeight", null ); // Should be always using CurrentPosition, but really won't hurt
            Patch( ToHitType, "GetAllModifiersDescription", "FixAllModifiersTargetHeight", null );
         }
         Patch( ToHitType, "GetAllModifiers", "LogModifiers", null );
         Patch( ToHitType, "GetHeightModifier", "LogHeightModifier", null );
         */

         if ( Settings.HitChanceStep != 0.05f || Settings.MaxFinalHitChance != 0.95f || Settings.MinFinalHitChance != 0.05f || Settings.DiminishingHitChanceModifier ) {
            if ( ! Settings.DiminishingHitChanceModifier )
               Patch( ToHitType, "GetUMChance", new Type[]{ typeof( float ), typeof( float ) }, "OverrideHitChanceStepNClamp", null );
            else {
               Patch( ToHitType, "GetUMChance", new Type[]{ typeof( float ), typeof( float ) }, "OverrideHitChanceDiminishing", null );
               diminishingBonus = new float[ Settings.DiminishingBonusMax ];
               diminishingPenalty = new float[ Settings.DiminishingPenaltyMax ];
               FillDiminishingModifiers();
            }
         }
      }

      public override void CombatStarts () {
         if ( Settings.AllowNetBonusModifier ) {
            CombatResolutionConstantsDef con = CombatConstants.ResolutionConstants;
            con.AllowTotalNegativeModifier = true;
            typeof( CombatGameConstants ).GetProperty( "ResolutionConstants" ).SetValue( CombatConstants, con, null );
         }
         if ( Settings.AllowLowElevationPenalty ) {
            ToHitConstantsDef con = CombatConstants.ToHit;
            con.ToHitElevationApplyPenalties = true;
            typeof( CombatGameConstants ).GetProperty( "ToHit" ).SetValue( CombatConstants, con, null );
         }
         if ( Settings.AllowNetBonusModifier && steppingModifier == null && ! Settings.DiminishingHitChanceModifier )
            FillSteppedModifiers(); // Use Combat Constants and must be lazily loaded 
      }

      // ============ Preparations ============

      private static float[] steppingModifier, diminishingBonus, diminishingPenalty;

      private static void FillSteppedModifiers () {
         List<float> mods = new List<float>(22);
         float lastMod = float.NaN;
         for ( int i = 1 ; ; i++ ) {
            float mod = GetSteppedModifier( i );
            if ( float.IsNaN( mod ) || mod == lastMod ) break;
            mods.Add( mod );
            if ( mod == 0 || mod <= -1f ) break;
            lastMod = mod;
         }
         steppingModifier = mods.ToArray();
         Log( "Stepping ToHit Multipliers\t" + Join( "\t", steppingModifier ) );
      }

      internal static float GetSteppedModifier ( float modifier ) {
         int[] Levels = CombatConstants.ToHit.ToHitStepThresholds;
         float[] values = CombatConstants.ToHit.ToHitStepValues;
         int mod = Mathf.RoundToInt( modifier ), lastLevel = int.MaxValue;
         float result = 0;
         for ( int i = Levels.Length - 1 ; i >= 0 ; i-- ) {
            int level = Levels[ i ];
            if ( mod < level ) continue;
            int modInLevel = Mathf.Min( mod - level, lastLevel - level );
            result -= (float)modInLevel * values[ i ];
            lastLevel = level;
         }
         return result;
      }

      private static void FillDiminishingModifiers () {
         for ( int i = 1 ; i <= Settings.DiminishingBonusMax ; i++ )
            diminishingBonus[ i-1 ] = (float) ( 2.0 - Math.Pow( Settings.DiminishingBonusPowerBase, (double) i / Settings.DiminishingBonusPowerDivisor ) );
         for ( int i = 1 ; i <= Settings.DiminishingPenaltyMax ; i++ )
            diminishingPenalty[ i-1 ] = (float) Math.Pow( Settings.DiminishingPenaltyPowerBase, (double) i / Settings.DiminishingPenaltyPowerDivisor );
         Log( "Diminishing hit% multipliers (bonus)\t" + Join( "\t", diminishingBonus ) );
         Log( "Diminishing hit% multipliers (penalty)\t" + Join( "\t", diminishingPenalty ) );
      }

      // ============ Fixes ============

      public static void ModifyBaseHitChance ( ref float __result ) {
         __result += Settings.BaseHitChanceModifier;
      }

      public static void ModifyBaseMeleeHitChance ( ref float __result ) {
         __result += Settings.MeleeHitChanceModifier;
      }

      [ Harmony.HarmonyPriority( Harmony.Priority.Low ) ]
      public static bool ProcessNetBonusModifier ( ref float __result, float originalHitChance, float modifier ) {
         int mod = Mathf.RoundToInt( modifier );
         __result = originalHitChance;
         if ( mod < 0 ) { // Bonus
            mod = Math.Min( -mod, steppingModifier.Length );
            __result -= steppingModifier[ mod - 1 ];
         }  else if ( mod > 0 ) { // Penalty
            mod = Math.Min( mod, steppingModifier.Length );
            __result += steppingModifier[ mod - 1 ];
         }
         //Log( "ProcessNetBonusModifier - Base Hit {0}, Modifier {1}, result {2}", originalHitChance, modifier, __result );
         return false;
      }

      [ Harmony.HarmonyPriority( Harmony.Priority.Low ) ]
      public static bool OverrideHitChanceStepNClamp ( ToHit __instance, ref float __result, float baseChance, float totalModifiers ) {
         // A pretty intense routine that AI use to evaluate attacks, try catch disabled.
         //Log( "OverrideHitChanceStepNClamp - Base Hit {0}, Modifier {1}", baseChance, totalModifiers );
         __result = ClampHitChance( __instance.GetSteppedValue( baseChance, totalModifiers ) );
         return false;
      }

      [ Harmony.HarmonyPriority( Harmony.Priority.Low ) ]
      public static bool OverrideHitChanceDiminishing ( ToHit __instance, ref float __result, float baseChance, float totalModifiers ) { try {
         // A pretty intense routine that AI use to evaluate attacks, try catch disabled.
         int mod = Mathf.RoundToInt( totalModifiers );
         if ( mod < 0 ) {
            mod = Math.Min( Settings.DiminishingBonusMax, -mod );
            baseChance *= diminishingBonus  [ mod -1 ];
         } else if ( mod > 0 ) {
            mod = Math.Min( Settings.DiminishingPenaltyMax, mod );
            baseChance *= diminishingPenalty[ mod -1 ];
         }
         __result = ClampHitChance( baseChance );
         return false;
      }                 catch ( Exception ex ) { return Error( ex ); } }

      public static float StepHitChance( float chance ) {
         float step = Settings.HitChanceStep;
         if ( step > 0f ) {
            chance += step/2f;
            chance -= chance % step;
         }
         return chance;
      }

      public static float ClampHitChance( float chance ) {
         chance = StepHitChance( chance );
         if      ( chance >= Settings.MaxFinalHitChance ) return Settings.MaxFinalHitChance;
         else if ( chance <= Settings.MinFinalHitChance ) return Settings.MinFinalHitChance;
         return chance;
      }

      /*
      public static void FixAllModifiersTargetHeight ( ICombatant target, ref Vector3 targetPosition ) {
         // Almost everything use CurrentPosition, so don't use TargetPosition!
         // But this does not work on CombatHUDWeaponSlot.UpdateToolTipsFiring, leaving for next release
         targetPosition = target.CurrentPosition;
      }

      public static void LogHeightModifier ( float attackerY, float targetY ) {
         Log( ">>> {0}, {1} <<<", attackerY, targetY );
      }

      public static void LogModifiers ( ToHit __instance, AbstractActor attacker, Weapon weapon, ICombatant target, Vector3 attackPosition, Vector3 targetPosition, LineOfFireLevel lofLevel, bool isCalledShot ) { TryRun( ModLog, () => {
         Log( "==== {0}, {1}, {2} ====", attackPosition.y, targetPosition.y, target.CurrentPosition.y );
      /*
         ToHit me = __instance;
         bool flag = lofLevel < LineOfFireLevel.LOFObstructed && weapon.IndirectFireCapable;
         Log( "==== {0}, {1}, {2} ====", attackPosition.y, targetPosition.y, target.CurrentPosition.y );
         Log( "rangeModifier = {0}", me.GetRangeModifier(weapon, attackPosition, targetPosition) );
         Log( "coverModifier = {0}", me.GetCoverModifier(attacker, target, lofLevel) );
         Log( "selfSpeedModifier = {0}", me.GetSelfSpeedModifier(attacker) );
         Log( "selfSprintedModifier = {0}", me.GetSelfSprintedModifier(attacker) );
         Log( "selfArmMountedModifier = {0}", me.GetSelfArmMountedModifier(weapon) );
         Log( "stoodUpModifier = {0}", me.GetStoodUpModifier(attacker) );
         Log( "heightModifier = {0}", me.GetHeightModifier(attackPosition.y, targetPosition.y) );
         Log( "heatModifier = {0}", me.GetHeatModifier(attacker) );
         Log( "targetTerrainModifier = {0}", me.GetTargetTerrainModifier(target, targetPosition, false) );
         Log( "selfTerrainModifier = {0}", me.GetSelfTerrainModifier(attackPosition, false) );
         Log( "targetSpeedModifier = {0}", me.GetTargetSpeedModifier(target, weapon) );
         Log( "selfDamageModifier = {0}", me.GetSelfDamageModifier(attacker, weapon) );
         Log( "targetSizeModifier = {0}", me.GetTargetSizeModifier(target) );
         Log( "targetShutdownModifier = {0}", me.GetTargetShutdownModifier(target, false) );
         Log( "targetProneModifier = {0}", me.GetTargetProneModifier(target, false) );
         Log( "weaponAccuracyModifier = {0}", me.GetWeaponAccuracyModifier(attacker, weapon) );
         Log( "attackerAccuracyModifier = {0}", me.GetAttackerAccuracyModifier(attacker) );
         Log( "enemyEffectModifier = {0}", me.GetEnemyEffectModifier(target) );
         Log( "refireModifier = {0}", me.GetRefireModifier(weapon) );
         Log( "targetDirectFireModifier = {0}", me.GetTargetDirectFireModifier(target, flag) );
         Log( "indirectModifier = {0}", me.GetIndirectModifier(attacker, flag) );
         Log( "moraleAttackModifier = {0}", me.GetMoraleAttackModifier(target, isCalledShot) );
      } ); }
      /**/
   }
}