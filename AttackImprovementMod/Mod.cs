using BattleTech;
using BattleTech.UI;
using Harmony;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace Sheepy.AttackImprovementMod {

   /*
    * Fix hit location distribution and called shot bugs, with options to improve attack HUD such as better called shot precision. Each fix and features can be configurated individually by editing mod.json.
    */
   public class Mod {

      public static ModSettings Settings = new ModSettings();

      internal static bool Pre_1_1 = false; // True if game version is less than 1.1
      internal const string LOG_DIR = "Mods/AttackImprovementMod/";
      internal static HarmonyInstance harmony = HarmonyInstance.Create( "io.github.Sheep-y.AttackImprovementMod" );

      static void Main () { // Sometimes I run quick tests as a console app here
         /**
         foreach ( MemberInfo e in typeof( Team ).GetMembers( BindingFlags.NonPublic | BindingFlags.Instance ) )
            Console.WriteLine( e );
         FieldInfo m1 = typeof( Team ).GetField( "streakBreakingValue", BindingFlags.NonPublic | BindingFlags.Instance );
         Console.WriteLine( m1 );
         /**/ /**
         for ( int i = 0 ; i < 20 ; i++ ) {
            float roll = i * 0.05f;
            float rev1 = RollCorrection.ReverseRollCorrection( roll, 0.5f  ), rrev1 = RollCorrection.CorrectRoll( rev1, 0.5f ),
                  rev2 = RollCorrection.ReverseRollCorrection( roll, 1f    ), rrev2 = RollCorrection.CorrectRoll( rev2, 1f ),
                  rev3 = RollCorrection.ReverseRollCorrection( roll, 1.9999f), rrev3 = RollCorrection.CorrectRoll( rev3, 1.9999f );
            Console.WriteLine( string.Format( "{0:0.00} => [Half correction] {1:0.0000}, Re-rev {2:0.0000}   [Full] {3:0.0000}, Re-rev {4:0.0000}   [Double] {5:0.0000}, Re-rev {6:0.0000}",
               new object[]{ roll, rev1, rrev1, rev2, rrev2, rev3, rrev3 } ) );
         } /**/
      }

      public static void Init ( string directory, string settingsJSON ) {
         DeleteLog( LOG_NAME );
         Pre_1_1 = ( VersionInfo.ProductVersion + ".0.0" ).Substring( 0, 4 ) == "1.0.";
         Log( Pre_1_1 ? "Game is Pre-1.1 (Clustered Called Shot)" : "Game is Post-1.1 (Non-Clustered Called Shot)" );

         try {
            Settings = JsonConvert.DeserializeObject<ModSettings>(settingsJSON);
            Log( "Mod Settings: " + JsonConvert.SerializeObject( Settings, Formatting.Indented ) );
         } catch ( Exception ex ) {
            Log( string.Format( "Error: Cannot read mod settings, using default: {0}", ex ) );
         }

         try {
            if ( Settings.ShowRealMechCalledShotChance || Settings.ShowRealVehicleCalledShotChance || Settings.ShowHeatAndStab ) {
               patchClass = typeof( Mod );
               Patch( typeof( CombatHUD ), "Init", typeof( CombatGameState ), null, "RecordCombatHUD" );
            }

            Log( "=== Patching Roll Corrections and Logger ===" );
            patchClass = typeof( RollCorrection );
            RollCorrection.InitPatch();

            Log( "=== Patching Hit Location Bugfixs and Logger ===" );
            patchClass = typeof( FixHitLocation );
            FixHitLocation.InitPatch( harmony );

            Log( "=== Patching Called Shot HUD ===" );
            patchClass = typeof( FixCalledShotPopUp );
            FixCalledShotPopUp.InitPatch();

            Log( "=== Patching Heat and Stability ===" );
            patchClass = typeof( HeatAndStab );
            HeatAndStab.InitPatch();

         } catch ( Exception ex ) {
            Log( ex );
         }
      }

      // ============ Harmony ============

      private static Type patchClass;
      /* Find and create a HarmonyMethod from current patchClass. method must be public and has unique name. */
      internal static HarmonyMethod MakePatch ( string method ) {
         if ( method == null ) return null;
         MethodInfo mi = patchClass.GetMethod( method );
         if ( mi == null ) Log( "Cannot find patch method " + method );
         return new HarmonyMethod( mi );
      }

      /* Find and patch a method with prefix and/or postfix. */
      internal static void Patch ( Type patchedClass, string patchedMethod, string prefix, string postfix ) {
         Patch( patchedClass, patchedMethod, BindingFlags.Public | BindingFlags.Instance, (Type[]) null, prefix, postfix );
      }

      /* Find and patch a method with prefix and/or postfix. */
      internal static void Patch ( Type patchedClass, string patchedMethod, Type parameterType, string prefix, string postfix ) {
         Patch( patchedClass, patchedMethod, BindingFlags.Public | BindingFlags.Instance, new Type[]{ parameterType }, prefix, postfix );
      }

      /* Find and patch a method with prefix and/or postfix. */
      internal static void Patch ( Type patchedClass, string patchedMethod, Type[] parameterTypes, string prefix, string postfix ) {
         Patch( patchedClass, patchedMethod, BindingFlags.Public | BindingFlags.Instance, parameterTypes, prefix, postfix );
      }

      /* Find and patch a method with prefix and/or postfix. */
      internal static void Patch ( Type patchedClass, string patchedMethod, BindingFlags flags, string prefix, string postfix ) {
         Patch( patchedClass, patchedMethod, flags, (Type[]) null, prefix, postfix );
      }

      /* Find and patch a method with prefix and/or postfix. */
      internal static void Patch ( Type patchedClass, string patchedMethod, BindingFlags flags, Type parameterType, string prefix, string postfix ) {
         Patch( patchedClass, patchedMethod, flags, new Type[]{ parameterType }, prefix, postfix );
      }

      /* Find and patch a method with prefix and/or postfix. */
      internal static void Patch( Type patchedClass, string patchedMethod, BindingFlags flags, Type[] parameterTypes, string prefix, string postfix ) {
         MethodInfo patched;
         if ( parameterTypes == null )
            patched = patchedClass.GetMethod( patchedMethod, flags );
         else
            patched = patchedClass.GetMethod( patchedMethod, flags, null, parameterTypes, null );
         if ( patched == null ) {
            Log( string.Format( "Error: Cannot find {0}.{1}(...) to patch", new Object[]{ patchedClass.Name, patchedMethod } ) );
            return;
         }
         harmony.Patch( patched, MakePatch( prefix ), MakePatch( postfix ) );
         Log( string.Format( "Patched: {0} {1} [ {2} : {3} ]", new object[]{ patchedClass, patched, prefix, postfix } ) );
      }

      // ============ UTILS ============

      internal static void DeleteLog( string file ) {
         try {
            File.Delete( file );
         } catch ( Exception ) { }
      }

      private static string LOG_NAME = LOG_DIR + "log.txt";
      internal static bool Log( object message ) { Log( message.ToString() ); return true; }
      internal static void Log( string message ) {
         try {
            if ( ! File.Exists( LOG_NAME ) ) message = DateTime.Now.ToString( "o" ) + "\r\n\r\n" + message;
            File.AppendAllText( LOG_NAME, message + "\r\n" );
         } catch ( Exception ex ) {
            Console.WriteLine( message );
            Console.Error.WriteLine( ex );
         }
      }

      internal static int TryGet<T> ( Dictionary<T, int> table, T key ) {
         table.TryGetValue( key, out int result );
         return result;
      }
      
      // ============ Game States ============

      internal static CombatHUD HUD;
      public static void RecordCombatHUD ( CombatHUD __instance ) {
         Log( __instance );
         Mod.HUD = __instance;
      }

      // A shortcut to get CombatGameConstants
      internal static CombatGameState Combat { get { return UnityGameInstance.BattleTechGame.Combat; } }
   }
}