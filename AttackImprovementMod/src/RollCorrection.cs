using BattleTech;
using BattleTech.UI;
using System;

namespace Sheepy.AttackImprovementMod {
   using static Mod;
   using static System.Reflection.BindingFlags;
   using System.Reflection;
   using UnityEngine;
   using System.Collections.Generic;

   public class RollCorrection : ModModule {
      
      private static bool NoRollCorrection = false;
      private static Dictionary<float, float> correctionCache;
      private static string WeaponHitChanceFormat = "{0:0}%";

      public override void InitPatch () {
         RangeCheck( "RollCorrectionStrength", ref Settings.RollCorrectionStrength, 0f, 0f, 1.999f, 2f );
         NoRollCorrection = Settings.RollCorrectionStrength == 0.0f;

         if ( ! NoRollCorrection ) {
            if ( Settings.RollCorrectionStrength != 1.0f )
               Patch( typeof( AttackDirector.AttackSequence ), "GetCorrectedRoll", NonPublic, new Type[]{ typeof( float ), typeof( Team ) }, "OverrideRollCorrection", null );
            if ( Settings.ShowCorrectedHitChance ) {
               correctionCache = new Dictionary<float, float>(20);
               Patch( typeof( CombatHUDWeaponSlot ), "SetHitChance", typeof( float ), "ShowCorrectedHitChance", null );
            }
         } else if ( Settings.ShowCorrectedHitChance )
            Log( "ShowCorrectedHitChance auto-disabled because roll Corection is disabled." );

         if ( Settings.MissStreakBreakerThreshold != 0.5f || Settings.MissStreakBreakerDivider != 5f ) {
            if ( Settings.MissStreakBreakerThreshold == 1f || Settings.MissStreakBreakerDivider == 0f )
               Patch( typeof( Team ), "ProcessRandomRoll", new Type[]{ typeof( float ), typeof( bool ) }, "BypassMissStreakBreaker", null );
            else {
               StreakBreakingValueProp = typeof( Team ).GetField( "streakBreakingValue", NonPublic | Instance );
               if ( StreakBreakingValueProp != null )
                  Patch( typeof( Team ), "ProcessRandomRoll", new Type[]{ typeof( float ), typeof( bool ) }, "OverrideMissStreakBreaker", null );
               else
                  Error( "Can't find Team.streakBreakingValue. Miss Streak Breaker cannot be patched. (Can instead try to disable it.)" );
            }
         }

         if ( Settings.ShowDecimalHitChance )
            WeaponHitChanceFormat = "{0:0.0}%";
         if ( Settings.ShowDecimalHitChance || Settings.ShowCorrectedHitChance || Settings.MinFinalHitChance < 0.05f || Settings.MaxFinalHitChance > 0.95f )
            Patch( typeof( CombatHUDWeaponSlot ), "SetHitChance", typeof( float ), "OverrideDisplayedHitChance", null );
      }

      FieldInfo rollCorrection = typeof( AttackDirector.AttackSequence ).GetField( "UseWeightedHitNumbers", Static | NonPublic );

      public override void CombatStarts () {
         if ( correctionCache != null )
            Log( "Combat starts with {0} reverse roll correction cached from previous battles.", correctionCache.Count );
         if ( rollCorrection != null ) {
            if ( NoRollCorrection )
               rollCorrection.SetValue( null, false );
         } else
            Warn( "Cannot find AttackDirector.AttackSequence.UseWeightedHitNumbers." );
      }

      // ============ UTILS ============

      public static float CorrectRoll ( float roll, float strength ) {
         strength /= 2;
         return (float)( (Math.Pow(1.6*roll-0.8,3)+0.5)*strength + roll*(1-strength) );
      }

      // A reverse algorithm of AttackDirector.GetCorrectedRoll
      internal static float ReverseRollCorrection ( float target, float strength ) {
         if ( strength == 0.0f ) return target;
         // Solving r for target = ((1.6r-0.8)^3+0.5)*(s/2)+r*(1-s/2)
         double t = target, t2 = t*t, s = strength, s2 = s*s, s3 = s2*s,
                a = 125 * Math.Sqrt( ( 13824*t2*s - 13824*t*s - 125*s3 + 750*s2 + 1956*s + 1000 ) / s ),
                b = a / ( 4096*Math.Pow( 6, 3d/2d )*s ) + ( 250*t - 125 ) / ( 1024 * s ),
                c = Math.Pow( b, 1d/3d );
         return c == 0 ? target : (float)( c + (125*s-250)/(1536*s*c) + 0.5 );
      }

      // ============ Fixes ============

      public static bool OverrideRollCorrection ( ref float __result, float roll, Team team ) { try {
         roll = CorrectRoll( roll, Settings.RollCorrectionStrength );
         if ( team != null )
            roll -= team.StreakBreakingValue;
         __result = roll;
         return false;
      }                 catch ( Exception ex ) { return Error( ex ); } }

      public static bool BypassMissStreakBreaker () {
         return false;
      }

      private static FieldInfo StreakBreakingValueProp = null;
      public static bool OverrideMissStreakBreaker ( Team __instance, float targetValue, bool succeeded ) { try {
         if ( succeeded ) {
            StreakBreakingValueProp.SetValue( __instance, 0f );

         } else if ( targetValue > Settings.MissStreakBreakerThreshold ) {
            float mod;
            if ( Settings.MissStreakBreakerDivider > 0 )
               mod = ( targetValue - Settings.MissStreakBreakerThreshold ) / Settings.MissStreakBreakerDivider;
            else
               mod = - Settings.MissStreakBreakerDivider;
            StreakBreakingValueProp.SetValue( __instance, __instance.StreakBreakingValue + mod );
         }
         return false;
      }                 catch ( Exception ex ) { return Error( ex ); } }

      public static void ShowCorrectedHitChance ( ref float chance ) { try {
         chance = Mathf.Clamp( chance, 0f, 1f );
         if ( ! correctionCache.TryGetValue( chance, out float corrected ) )
            correctionCache.Add( chance, corrected = ReverseRollCorrection( chance, Settings.RollCorrectionStrength ) );
         chance = corrected;
      }                 catch ( Exception ex ) { Error( ex ); } }

      private static MethodInfo HitChance = typeof( CombatHUDWeaponSlot ).GetMethod( "set_HitChance", Instance | NonPublic );
      private static MethodInfo Refresh = typeof( CombatHUDWeaponSlot ).GetMethod( "RefreshNonHighlighted", Instance | NonPublic );
      private static readonly object[] empty = new object[]{};

      // Override the original code to remove accuracy cap on display, since correction or other settings can push it above 95%.
      public static bool OverrideDisplayedHitChance ( CombatHUDWeaponSlot __instance, float chance ) { try {
         HitChance.Invoke( __instance, new object[]{ chance } );
         __instance.HitChanceText.text = string.Format( WeaponHitChanceFormat, Mathf.Clamp( chance * 100f, 0f, 100f ) );
         Refresh.Invoke( __instance, empty );
         return false;
      }                 catch ( Exception ex ) { return Error( ex ); } }
   }
}