using BattleTech;
using BattleTech.UI;
using System;

namespace Sheepy.AttackImprovementMod {
   using static Mod;
   using System.Reflection;
   using UnityEngine;

   public class RollCorrection {
      
      private static bool DisableRollCorrection = false;
      private static readonly float[] correctionCache = new float[21];
      private static string WeaponHitChanceFormat = "{0:0}%";

      internal static void InitPatch () {
         DisableRollCorrection = Settings.RollCorrectionStrength == 0.0f;

         FieldInfo rollCorrection = typeof( AttackDirector.AttackSequence ).GetField( "UseWeightedHitNumbers", BindingFlags.Static | BindingFlags.NonPublic );
         bool rollCorrected = true;
         if ( rollCorrection == null )
            Log( "Warning: Cannot find AttackDirector.AttackSequence.UseWeightedHitNumbers." );
         else 
            rollCorrected = (bool) rollCorrection.GetValue( null );

         if ( DisableRollCorrection ) {
            if ( ! rollCorrected ) {
               Log( "Roll correction is already disabled." );
            } else {
               rollCorrection.SetValue( null, false );
            }
            rollCorrected = false;

         } else {
            if ( Settings.RollCorrectionStrength < 0 ) {
               Log( "Error: RollCorrectionStrength must not be negative." );
               Settings.RollCorrectionStrength = 1.0f;
            } else if ( Settings.RollCorrectionStrength > 1.9999f ) {
               if ( Settings.RollCorrectionStrength > 2 )
                  Log( "Warning: RollCorrectionStrength must be less than 2." );
               Settings.RollCorrectionStrength = 1.9999f; // Max! 1.99999 results in NaN in reverse correction
            }

            if ( Settings.RollCorrectionStrength != 1.0f )
               Patch( typeof( AttackDirector.AttackSequence ), "GetCorrectedRoll", BindingFlags.NonPublic, new Type[]{ typeof( float ), typeof( Team ) }, "OverrideRollCorrection", null );
            if ( rollCorrected && Settings.ShowRealWeaponHitChance ) {
               for ( int i = 0 ; i < 21 ; i++ )
                  correctionCache[i] = ReverseRollCorrection( 0.05f * i, Settings.RollCorrectionStrength );
               Patch( typeof( CombatHUDWeaponSlot ), "SetHitChance", typeof( float ), "ShowRealHitChance", null );
            }
         }
         if ( Settings.MissStreakBreakerThreshold != 0.5f || Settings.MissStreakBreakerDivider != 5f ) {
            StreakBreakingValueProp = typeof( Team ).GetField( "streakBreakingValue", BindingFlags.NonPublic );
            if ( StreakBreakingValueProp != null )
               Patch( typeof( Team ), "ProcessRandomRoll", new Type[]{ typeof( float ), typeof( bool ) }, "OverrideMissStreakBreaker", null );
            else
               Log( "Error: Can't find Team.streakBreakingValue. Miss Streak Breaker cannot be patched." );
         }

         if ( Settings.ShowDecimalHitChance ) {
            WeaponHitChanceFormat = "{0:0.0}%";
            Patch( typeof( CombatHUDWeaponSlot ), "SetHitChance", typeof( float ), "OverrideWeaponHitChance", null );
            if ( ! Settings.ShowRealWeaponHitChance )
               Log( "Warning: ShowDecimalHitChance without ShowRealWeaponHitChance" );
         } else if ( Settings.ShowRealWeaponHitChance ) {
            Patch( typeof( CombatHUDWeaponSlot ), "SetHitChance", typeof( float ), "OverrideWeaponHitChance", null );
         }
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
      }                 catch ( Exception ex ) { return Log( ex ); } }

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
      }                 catch ( Exception ex ) { return Log( ex ); } }

      public static void ShowRealHitChance ( ref float chance ) { try {
         chance = Mathf.Clamp( chance, 0f, 1f );
         int i = (int)( ( chance + 0.00001f ) / 0.05f );
         if ( Math.Abs( i * 0.05f - chance ) < 0.00001f )
            chance = correctionCache[ i ];
         else {
            Log( "Uncached hit chance reversal from " + chance + ", diff: " + ( i * 0.05f - chance ) );
            chance = ReverseRollCorrection( chance, Settings.RollCorrectionStrength );
         }
      }                 catch ( Exception ex ) { Log( ex ); } }

      private static MethodInfo HitChance = typeof( CombatHUDWeaponSlot ).GetMethod( "set_HitChance", BindingFlags.Instance | BindingFlags.NonPublic );
      private static MethodInfo Refresh = typeof( CombatHUDWeaponSlot ).GetMethod( "RefreshNonHighlighted", BindingFlags.Instance | BindingFlags.NonPublic );
      private static readonly object[] empty = new object[]{};

      // Override the original code to remove accuracy cap on display, since correction can push it above 95%.
      public static bool OverrideWeaponHitChance ( CombatHUDWeaponSlot __instance, float chance ) { try {
         HitChance.Invoke( __instance, new object[]{ chance } );
         __instance.HitChanceText.text = string.Format( WeaponHitChanceFormat, Mathf.Clamp( chance * 100f, 0f, 100f ) );
         Refresh.Invoke( __instance, empty );
         return false;
      }                 catch ( Exception ex ) { return Log( ex ); } }
   }
}