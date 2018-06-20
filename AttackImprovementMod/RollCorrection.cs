using BattleTech;
using BattleTech.UI;
using System;

namespace Sheepy.AttackImprovementMod {
   using static Mod;
   using System.Reflection;
   using System.IO;

   public class RollCorrection {

      private static bool DisableRollCorrection = false;
      private static float[] correctionCache = new float[20];

      internal static void InitPatch () {
         DeleteLog( ROLL_LOG );
         DisableRollCorrection = Settings.RollCorrectionStrength == 0.0f;
         Type AttackType = typeof( AttackDirector.AttackSequence );

         FieldInfo rollCorrection = typeof( AttackDirector.AttackSequence ).GetField( "UseWeightedHitNumbers", BindingFlags.Static | BindingFlags.NonPublic );
         if ( rollCorrection == null ) {
            Log( "Error: Cannot find AttackDirector.AttackSequence.UseWeightedHitNumbers; roll correction settings not applied." );
            return;
         }
         bool rollCorrected = (bool) rollCorrection.GetValue( null );

         if ( Settings.LogHitRolls ) {
            Patch( AttackType, "GetIndividualHits", BindingFlags.NonPublic | BindingFlags.Instance, "RecordAttacker", null );
            Patch( AttackType, "GetClusteredHits" , BindingFlags.NonPublic | BindingFlags.Instance, "RecordAttacker", null );
            Patch( AttackType, "GetCorrectedRoll" , BindingFlags.NonPublic | BindingFlags.Instance, new Type[]{ typeof( float ), typeof( Team ) }, "RecordAttackRoll", "LogMissedAttack" );
            RollLog( String.Join( "\t", new string[]{ "Attacker", "Weapon", "Hit Roll", "Corrected", "Streak", "Final", "To Hit", "Location Roll", "Head/Turret", "CT/Front", "LT/Left", "RT/Right", "LA/Rear", "RA", "LL", "RL", "Called Part", "Called Bonus", "Total Weight", "Goal", "Hit Location" } ) );
         }

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
            }
            if ( Settings.RollCorrectionStrength >= 2 ) {
               if ( Settings.RollCorrectionStrength > 2 )
                  Log( "Error: RollCorrectionStrength must be less than 2." );
               Settings.RollCorrectionStrength = 1.9999f; // Max! 1.99999 results in NaN in reverse correction
            }
            if ( Settings.RollCorrectionStrength != 1.0f )
               Patch( AttackType, "GetCorrectedRoll", BindingFlags.NonPublic | BindingFlags.Instance, new Type[]{ typeof( float ), typeof( Team ) }, "OverrideRollCorrection", null );

            if ( rollCorrected && Settings.ShowRealWeaponHitChance ) {
               for ( int i = 0 ; i < 20 ; i++ )
                  correctionCache[i] = ReverseRollCorrection( 0.05f * i, Settings.RollCorrectionStrength );
               Patch( typeof( CombatHUDWeaponSlot ), "SetHitChance", typeof( float ), "ShowRealHitChance", null );
            }
         }
         if ( Settings.MissStreakBreakerThreshold != 0.5f || Settings.MissStreakBreakerDivider != 5f ) {
            StreakBreakingValueProp = typeof( Team ).GetField( "streakBreakingValue", BindingFlags.NonPublic | BindingFlags.Instance );
            if ( StreakBreakingValueProp != null )
               Patch( typeof( Team ), "ProcessRandomRoll", new Type[]{ typeof( float ), typeof( bool ) }, "OverrideMissStreakBreaker", null );
            else
               Log( "Error: Can't find Team.streakBreakingValue. Miss Streak Breaker cannot be patched." );
         }

         if ( Settings.ShowDecimalHitChance )
            Patch( typeof( CombatHUDWeaponSlot ), "SetHitChance", typeof( float ), "OverrideWeaponHitChanceFormat", null );
      }

      // ============ UTILS ============

      private static string ROLL_LOG = Mod.LOG_DIR + "log_roll.txt";
      internal static bool RollLog( String message ) {
         File.AppendAllText( ROLL_LOG, message + "\r\n" );
         return true;
      }

      public static float CorrectRoll( float roll, float strength ) {
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

      public static bool OverrideRollCorrection ( ref float __result, float roll, Team team ) {
         try {
				roll = CorrectRoll( roll, Settings.RollCorrectionStrength );
				if ( team != null )
					roll -= team.StreakBreakingValue;
				__result = roll;
            return false;
         } catch ( Exception ex ) {
            return Log( ex );
         }
      }

      private static FieldInfo StreakBreakingValueProp = null;
      public static bool OverrideMissStreakBreaker ( Team __instance, float targetValue, bool succeeded ) {
         try {
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
         } catch ( Exception ex ) {
            return Log( ex );
         }
      }

      public static void ShowRealHitChance ( ref float chance ) {
         int i = (int)( ( chance + 0.00001f ) / 0.05f );
         if ( i >= 0 && i < 20 && Math.Abs( i * 0.05f - chance ) < 0.00001 )
            chance = correctionCache[ i ];
         else {
            Log( "Uncached hit chance reversal from " + chance + ", diff: " + ( i * 0.05f - chance ) );
            chance = ReverseRollCorrection( chance, Settings.RollCorrectionStrength );
         }
      }

      private static MethodInfo HitChance = typeof( CombatHUDWeaponSlot ).GetMethod( "set_HitChance", BindingFlags.Instance | BindingFlags.NonPublic );
      private static MethodInfo Refresh = typeof( CombatHUDWeaponSlot ).GetMethod( "RefreshNonHighlighted", BindingFlags.Instance | BindingFlags.NonPublic );
      private static object[] empty = new object[]{};

      // Override the original code to show accuracy in decimal points
      public static bool OverrideWeaponHitChanceFormat ( CombatHUDWeaponSlot __instance, float chance ) {
         try {
            HitChance.Invoke( __instance, new object[]{ chance } );
			   __instance.HitChanceText.text = string.Format( "{0:0.0}%", Math.Max( 0f, Math.Min( chance * 100f, 100f ) ) );
            Refresh.Invoke( __instance, empty );
            return false;
         } catch ( Exception ex ) {
            return Log( ex );
         }
      }

      // ============ Log ============

      // Get attacker, weapon and hitchance before logging
      internal static string thisAttacker = "(unknown)";
      internal static string thisWeapon = "(unknown)";
      internal static float thisHitChance;
      public static void RecordAttacker ( AttackDirector.AttackSequence __instance, Weapon weapon, float toHitChance ) {
         thisAttacker = __instance.attacker.GetPilot()?.Callsign ?? __instance.attacker.Nickname;
         thisWeapon = weapon.defId.StartsWith( "Weapon_" ) ? weapon.defId.Substring( 7 ) : weapon.defId;
         thisHitChance = toHitChance;
      }

      internal static float thisRoll;
      internal static float thisStreak;
      public static void RecordAttackRoll ( float roll, Team team ) {
         thisRoll = roll;
         thisStreak = team?.StreakBreakingValue ?? 0;
      }

      internal static float thisCorrectedRoll;
      public static void LogMissedAttack ( float __result, float roll, Team team ) {
         thisCorrectedRoll = __result;
         if ( __result > thisHitChance ) // Miss, log now because hit location won't be rolled
            RollLog( GetHitLog() +
               "\t--" + // Roll; Empty cells are added so that copy and paste will override any old data in Excel, instead of leaving them in place and make it confusing
               "\t--\t--\t--\t--" +  // Head & Torsos
               "\t--\t--\t--\t--" +  // Limbs
               "\t--\t--\t--\t--" ); // called shot and result
      }

      internal static string GetHitLog () {
         return thisAttacker + "\t" + thisWeapon + "\t" + thisRoll + "\t" + ( thisCorrectedRoll + thisStreak ) + "\t" + thisStreak + "\t" + thisCorrectedRoll + "\t" + thisHitChance;
      }
   }
}