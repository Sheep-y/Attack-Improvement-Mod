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

      internal const string LOG_DIR = "Mods/AttackImprovementMod/";
      internal static HarmonyInstance harmony = HarmonyInstance.Create( "io.github.Sheep-y.AttackImprovementMod" );

      static void Main () { // Sometimes I run quick tests as a console app here
         /**/
         for ( int i = 0 ; i < 20 ; i++ ) {
            float strength = 1f, roll = i * 0.05f;
            float rev = RollCorrection.ReverseRollCorrection( roll, strength ), rrev = RollCorrection.CorrectRoll( rev, strength );
            Console.WriteLine( roll + " => Reversed " + rev + ", Re-reversed " + rrev );
            strength = 0.5f;
            rev = RollCorrection.ReverseRollCorrection( roll, strength );
            rrev = RollCorrection.CorrectRoll( rev, strength );
            Console.WriteLine( roll + " => Reversed " + rev + ", Re-reversed " + rrev );
            int x = (int)( roll / 0.05f );
            if ( x >= 0 && x < 20 && x * 0.05f == roll ) {

            } else
               Console.WriteLine( "Problem" );
         }
         Console.ReadKey(); /**/
      }

      public static void Init ( string directory, string settingsJSON ) {
         DeleteLog( LOG_NAME );

         try {
            Settings = JsonConvert.DeserializeObject<ModSettings>(settingsJSON);
         } catch ( Exception ex ) {
            Log( string.Format( "Error: Cannot read mod settings, using default: {0}", ex ) );
         }

         try {
            Log( "=== Patching Roll Corrections ===" );
            patchClass = typeof( RollCorrection );
            RollCorrection.InitPatch();

            Log( "=== Patching Hit Location Bugfixs ===" );
            patchClass = typeof( FixHitLocation );
            FixHitLocation.InitPatch( harmony );

            Log( "=== Patching Called Shot HUD ===" );
            patchClass = typeof( FixCalledShotPopUp );
            FixCalledShotPopUp.InitPatch();

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
         Log( string.Format( "Patched: {0}.{1}(...)", new object[]{ patchedClass.Name, patchedMethod } ) );
      }

      // ============ UTILS ============

      internal static void DeleteLog( string file ) {
         try {
            File.Delete( file );
         } catch ( Exception ) { }
      }

      private static string LOG_NAME = LOG_DIR + "log.txt";
      internal static bool Log( Exception message ) { Log( message.ToString() ); return true; }
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

      private static CombatGameState CombatCache;
      internal static CombatGameState Combat {
         get {
            if ( CombatCache == null ) CombatCache = UnityGameInstance.BattleTechGame.Combat;
            return CombatCache;
         }
      }
   }
}