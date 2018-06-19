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
            Patch( AttackType, "GetIndividualHits", BindingFlags.NonPublic | BindingFlags.Instance, "PrefixGetHits", null );
            Patch( AttackType, "GetClusteredHits" , BindingFlags.NonPublic | BindingFlags.Instance, "PrefixGetHits", null );
            Patch( AttackType, "GetCorrectedRoll" , BindingFlags.NonPublic | BindingFlags.Instance, new Type[]{ typeof( float ), typeof( Team ) }, "PrefixLogRoll", "PostfixLogRoll" );
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
            if ( Settings.RollCorrectionStrength != 1.0f )
               Patch( AttackType, "GetCorrectedRoll", BindingFlags.NonPublic | BindingFlags.Instance, new Type[]{ typeof( float ), typeof( Team ) }, "PrefixGetCorrectedRoll", null );

            if ( rollCorrected && Settings.ShowRealWeaponHitChance ) {
               for ( int i = 0 ; i < 20 ; i++ )
                  correctionCache[i] = ReverseRollCorrection( 0.05f * i, Settings.RollCorrectionStrength );
               Patch( typeof( CombatHUDWeaponSlot ), "SetHitChance", typeof( float ), "PrefixWeaponHitChance", null );
            }
         }
         if ( Settings.ShowDecimalHitChance )
            Patch( typeof( CombatHUDWeaponSlot ), "SetHitChance", typeof( float ), "PrefixWeaponDecimalChance", null );
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
                b = Math.Pow( a / ( 4096*Math.Pow( 6, 3d/2d )*s ) + ( 250*t - 125 ) / ( 1024 * s ), 1d/3d );
         return (float)( b + (125*s-250)/(1536*s*b) + 0.5 );
      }

      // ============ Fixes ============

      public static bool PrefixGetCorrectedRoll ( ref float __result, float roll, Team team ) {
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

      public static void PrefixWeaponHitChance ( ref float chance ) {
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
      public static bool PrefixWeaponDecimalChance ( CombatHUDWeaponSlot __instance, float chance ) {
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
      internal static float thisHitChance = 0f;
      public static void PrefixGetHits ( AttackDirector.AttackSequence __instance, Weapon weapon, float toHitChance ) {
         thisAttacker = __instance.attacker.GetPilot()?.Callsign ?? __instance.attacker.Nickname;
         thisWeapon = weapon.defId;
         thisHitChance = toHitChance;
      }
      // TODO: We can reset the fields in postfix to be safe, may be in next mod update

      internal static float thisRoll;
      internal static float thisStreak;
      public static void PrefixLogRoll ( float roll, Team team ) {
         thisRoll = roll;
         thisStreak = team?.StreakBreakingValue ?? 0;
      }

      internal static float thisCorrectedRoll;
      public static void PostfixLogRoll ( float __result, float roll, Team team ) {
         thisCorrectedRoll = __result;
         if ( __result >= thisHitChance ) // Miss, log now because hit location won't be rolled
            RollLog( GetHitLog() );
      }

      internal static string GetHitLog () {
         return thisAttacker + "\t" + thisWeapon + "\t" + thisRoll + "\t" + ( thisCorrectedRoll + thisStreak ) + "\t" + thisStreak + "\t" + thisCorrectedRoll + "\t" + thisHitChance;
      }
   }
}