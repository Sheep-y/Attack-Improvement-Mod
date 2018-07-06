using BattleTech;
using BattleTech.UI;
using System;

namespace Sheepy.AttackImprovementMod {
   using static Mod;
   using System.Reflection;
   using UnityEngine;
   using System.Collections.Generic;

   public class RollCorrection : ModModule {
      
      private static bool DisableRollCorrection = false;
      private static readonly Dictionary<float, float> correctionCache = new Dictionary<float, float>(20);

      public override void InitPatch () {
         Settings.RollCorrectionStrength = RangeCheck( "RollCorrectionStrength", Settings.RollCorrectionStrength, 0f, 0f, 1.999f, 2f );
         DisableRollCorrection = Settings.RollCorrectionStrength == 0.0f;

         if ( ! DisableRollCorrection ) {
            if ( Settings.RollCorrectionStrength != 1.0f )
               Patch( typeof( AttackDirector.AttackSequence ), "GetCorrectedRoll", BindingFlags.NonPublic, new Type[]{ typeof( float ), typeof( Team ) }, "OverrideRollCorrection", null );
            if ( Settings.ShowRealWeaponHitChance )
               Patch( typeof( CombatHUDWeaponSlot ), "SetHitChance", typeof( float ), "ShowRealHitChance", null );
         }

         if ( Settings.MissStreakBreakerThreshold != 0.5f || Settings.MissStreakBreakerDivider != 5f ) {
            StreakBreakingValueProp = typeof( Team ).GetField( "streakBreakingValue", BindingFlags.NonPublic | BindingFlags.Instance );
            if ( StreakBreakingValueProp != null )
               Patch( typeof( Team ), "ProcessRandomRoll", new Type[]{ typeof( float ), typeof( bool ) }, "OverrideMissStreakBreaker", null );
            else
               Error( "Can't find Team.streakBreakingValue. Miss Streak Breaker cannot be patched." );
         }

         if ( Settings.ShowDecimalHitChance )
            Patch( typeof( CombatHUDWeaponSlot ), "SetHitChance", typeof( float ), "OverrideWeaponHitChance", null );
      }

      public override void CombatStarts () {
         FieldInfo rollCorrection = typeof( AttackDirector.AttackSequence ).GetField( "UseWeightedHitNumbers", BindingFlags.Static | BindingFlags.NonPublic );
         if ( rollCorrection == null )
            Warn( "Cannot find AttackDirector.AttackSequence.UseWeightedHitNumbers." );
         else {
            if ( DisableRollCorrection && (bool) rollCorrection.GetValue( null ) )
               rollCorrection.SetValue( null, false );
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
      }                 catch ( Exception ex ) { return Error( ex ); } }

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

      public static void ShowRealHitChance ( ref float chance ) { try {
         chance = Mathf.Clamp( chance, 0f, 1f );
         float corrected = float.NaN;
         correctionCache.TryGetValue( chance, out corrected );
         if ( corrected == float.NaN )
            correctionCache.Add( chance, corrected = ReverseRollCorrection( chance, Settings.RollCorrectionStrength ) );
         chance = corrected;
      }                 catch ( Exception ex ) { Log( ex ); } }

      private static MethodInfo HitChance = typeof( CombatHUDWeaponSlot ).GetMethod( "set_HitChance", BindingFlags.Instance | BindingFlags.NonPublic );
      private static MethodInfo Refresh = typeof( CombatHUDWeaponSlot ).GetMethod( "RefreshNonHighlighted", BindingFlags.Instance | BindingFlags.NonPublic );
      private static readonly object[] empty = new object[]{};

      // Override the original code to remove accuracy cap on display, since correction can push it above 95%.
      public static bool OverrideWeaponHitChance ( CombatHUDWeaponSlot __instance, float chance ) { try {
         HitChance.Invoke( __instance, new object[]{ chance } );
         __instance.HitChanceText.text = string.Format( "{0:0.0}%", Mathf.Clamp( chance * 100f, 0f, 100f ) );
         Refresh.Invoke( __instance, empty );
         return false;
      }                 catch ( Exception ex ) { return Error( ex ); } }
   }
}