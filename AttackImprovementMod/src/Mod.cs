using BattleTech;
using BattleTech.UI;
using Harmony;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace Sheepy.AttackImprovementMod {
   using static System.Reflection.BindingFlags;

   /*
    * Fix hit location distribution and called shot bugs, with options to improve attack HUD such as better called shot precision. Each fix and features can be configurated individually by editing mod.json.
    */
   public class Mod {

      public const string VERSION = "2.0 preview 20180709";
      public static ModSettings Settings = new ModSettings();

      internal static bool GameUseClusteredCallShot = false; // True if game version is less than 1.1
      internal static bool GameHitLocationBugged = false; // True if game version is less than 1.1.1
      internal static string FALLBACK_LOG_DIR = "Mods/AttackImprovementMod/";
      internal const string LOG_NAME = "Log_AttackImprovementMod.txt";
      internal static string LogDir = "";
      internal static readonly HarmonyInstance harmony = HarmonyInstance.Create( "io.github.Sheep-y.AttackImprovementMod" );
      internal static readonly Dictionary<string, ModModule> modules = new Dictionary<string, ModModule>();

      public static void Init ( string directory, string settingsJSON ) {
         LogSettings( directory, settingsJSON );

         // Hook to combat starts
         patchClass = typeof( Mod );
         Patch( typeof( CombatHUD ), "Init", typeof( CombatGameState ), null, "CombatInit" );

         modules.Add( "Heat and Stability", new HeatAndStab() );
         modules.Add( "Line of Fire", new LineOfSight() );
         modules.Add( "Attack Action", new AttackAction() );
         modules.Add( "Called Shot HUD", new FixCalledShotPopUp() );
         modules.Add( "Melee", new Melee() );
         modules.Add( "Roll Modifier", new RollModifier() );
         modules.Add( "Roll Corrections", new RollCorrection() );
         modules.Add( "Hit Distribution", new FixHitLocation() );
         modules.Add( "Logger", new AttackLog() );

         foreach ( var mod in modules )  try {
            Log( "=== Patching " + mod.Key + " ===" );
            patchClass = mod.Value.GetType();
            mod.Value.InitPatch();
         }                 catch ( Exception ex ) { Error( ex ); }
         Log( "=== All Mod Modules Initialised ===\n" );
      }

      public static void LogSettings ( string directory, string settingsJSON ) {
         // Get log settings
         StringBuilder logCache = new StringBuilder().AppendFormat( "AIM Version: {0}\nMod Folder: {1}\n", VERSION, directory );
         try {
            Settings = JsonConvert.DeserializeObject<ModSettings>( settingsJSON );
            logCache.AppendFormat( "Mod Settings: {0}\n", JsonConvert.SerializeObject( Settings, Formatting.Indented ) );
         } catch ( Exception ) {
            logCache.Append( "Error: Cannot parse mod settings, using default." );
         }
         try {
            LogDir = Settings.LogFolder;
            if ( LogDir.Length <= 0 )
               LogDir = directory + "/";
            logCache.AppendFormat( "Log folder set to {0}. If that fails, fallback to {1}.", LogDir, FALLBACK_LOG_DIR );
            DeleteLog( LOG_NAME );
            Log( logCache.ToString() );
         }                 catch ( Exception ex ) { Error( ex ); }

         // Detect game features. Need a proper version parsing routine. Next time.
         if ( ( VersionInfo.ProductVersion + ".0.0" ).Substring( 0, 4 ) == "1.0." ) {
            GameUseClusteredCallShot = GameHitLocationBugged = true;
            Log( "Game is 1.0.x (Clustered Called Shot, Hit Location bugged)" );
         } else if ( ( VersionInfo.ProductVersion + ".0.0." ).Substring( 0, 6 ) == "1.1.0" ) {
            GameHitLocationBugged = true;
            Log( "Game is 1.1.0 (Non-Clustered Called Shot, Hit Location bugged)" );
         } else {
            Log( "Game is 1.1.1 or up (Non-Clustered Called Shot, Hit Location fixed)" );
         }
         Log();
      }

      // ============ Harmony ============

      private static Type patchClass;
      /* Find and create a HarmonyMethod from current patchClass. method must be public and has unique name. */
      internal static HarmonyMethod MakePatch ( string method ) {
         if ( method == null ) return null;
         MethodInfo mi = patchClass.GetMethod( method );
         if ( mi == null ) Error( "Cannot find patch method " + method );
         return new HarmonyMethod( mi );
      }

      internal static void Patch ( Type patchedClass, string patchedMethod, string prefix, string postfix ) {
         Patch( patchedClass, patchedMethod, Public | Instance, (Type[]) null, prefix, postfix );
      }

      internal static void Patch ( Type patchedClass, string patchedMethod, Type parameterType, string prefix, string postfix ) {
         Patch( patchedClass, patchedMethod, Public | Instance, new Type[]{ parameterType }, prefix, postfix );
      }

      internal static void Patch ( Type patchedClass, string patchedMethod, Type[] parameterTypes, string prefix, string postfix ) {
         Patch( patchedClass, patchedMethod, Public | Instance, parameterTypes, prefix, postfix );
      }

      internal static void Patch ( Type patchedClass, string patchedMethod, BindingFlags flags, string prefix, string postfix ) {
         Patch( patchedClass, patchedMethod, flags, (Type[]) null, prefix, postfix );
      }

      internal static void Patch ( Type patchedClass, string patchedMethod, BindingFlags flags, Type parameterType, string prefix, string postfix ) {
         Patch( patchedClass, patchedMethod, flags, new Type[]{ parameterType }, prefix, postfix );
      }

      internal static void Patch( Type patchedClass, string patchedMethod, BindingFlags flags, Type[] parameterTypes, string prefix, string postfix ) {
         MethodInfo patched;
         if ( ( flags & ( Static | Instance  ) ) == 0  ) flags |= Instance;
         if ( ( flags & ( Public | NonPublic ) ) == 0  ) flags |= Public;
         if ( parameterTypes == null )
            patched = patchedClass.GetMethod( patchedMethod, flags );
         else
            patched = patchedClass.GetMethod( patchedMethod, flags, null, parameterTypes, null );
         if ( patched == null ) {
            Error( "Cannot find {0}.{1}(...) to patch", patchedClass.Name, patchedMethod );
            return;
         }
         Patch( patched, prefix, postfix );
      }

      internal static void Patch( MethodInfo patched, string prefix, string postfix ) {
         if ( patched == null ) {
            Error( "Method not found. Cannot patch [ {0} : {1} ]", prefix, postfix );
            return;
         }
         HarmonyMethod pre = MakePatch( prefix ), post = MakePatch( postfix );
         if ( pre == null && post == null ) return; // MakePatch would have reported method not found
         harmony.Patch( patched, MakePatch( prefix ), MakePatch( postfix ) );
         Log( "Patched: {0} {1} [ {2} : {3} ]", patched.DeclaringType, patched, prefix, postfix );
      }

      // ============ UTILS ============

      internal static string Join<T> ( string separator, T[] array ) {
         StringBuilder result = new StringBuilder();
         for ( int i = 0, len = array.Length ; i < len ; i++ ) {
            if ( i > 0 ) result.Append( separator );
            result.Append( array[i]?.ToString() );
         }
         return result.ToString();
      }

      internal static int TryGet<T> ( Dictionary<T, int> table, T key ) {
         table.TryGetValue( key, out int result );
         return result;
      }

      internal static void RangeCheck ( string name, ref int val, int min, int max ) {
         float v = val;
         RangeCheck( name, ref v, min, min, max, max );
         val = Mathf.RoundToInt( v );
      }

      internal static void RangeCheck ( string name, ref float val, float min, float max ) {
         RangeCheck( name, ref val, min, min, max, max );
      }

      internal static void RangeCheck ( string name, ref float val, float shownMin, float realMin, float realMax, float shownMax ) {
         if ( realMin > realMax || shownMin > shownMax ) Error( "Incorrect range check params on " + name );
         float orig = val;
         if ( val < realMin )
            val = realMin;
         else if ( val > realMax )
            val = realMax;
         if ( orig < shownMin && orig > shownMax ) {
            string message = "Warning: " + name + " must be ";
            if ( shownMin > float.MinValue )
               if ( shownMax < float.MaxValue )
                  message += " between " + shownMin + " and " + shownMax;
               else
                  message += " >= " + shownMin;
            else
               message += " <= " + shownMin;
            Log( message + ". Setting to " + val );
         }
      }

      // ============ LOGS ============

      internal static void DeleteLog( string file ) {
         try {
            File.Delete( LogDir + file );
         } catch ( Exception ) { }
         try {
            File.Delete( FALLBACK_LOG_DIR + file );
         } catch ( Exception ) { }
      }

      internal static void Log( object message ) { Log( message.ToString() ); }
      internal static void Log( string message, params object[] args ) {
         try {
            if ( args != null && args.Length > 0 )
               message = string.Format( message, args );
         } catch ( Exception ) {}
         Log( message );
      }
      internal static void Log( string message = "" ) {
         string logName = LogDir + LOG_NAME;
         try {
            if ( ! File.Exists( logName ) ) 
               message = DateTime.Now.ToString( "o" ) + "\r\n\r\n" + message;
         } catch ( Exception ) {}
         WriteLog( LOG_NAME, message + "\r\n" );
      }

      internal static void Warn( object message ) { Warn( message.ToString() ); }
      internal static void Warn( string message ) { Log( "Warning: " + message ); }
      internal static void Warn( string message, params object[] args ) { Log( "Warning: " + message, args ); }

      internal static bool Error( object message ) { Error( message.ToString() ); return true; }
      internal static void Error( string message ) { Log( "Error: " + message ); }
      internal static void Error( string message, params object[] args ) { Log( "Error: " + message, args ); }

      internal static void WriteLog( string filename, string message ) {
         string logName = LogDir + filename;
         try {
            File.AppendAllText( logName, message );
         } catch ( Exception ) {
            try {
               logName = FALLBACK_LOG_DIR + filename;
               File.AppendAllText( logName, message );
            } catch ( Exception ex ) {
               Console.WriteLine( message );
               Console.Error.WriteLine( ex );
            }
         }
      }

      // ============ Game States ============

      internal static CombatHUD HUD;
      internal static CombatGameState Combat;
      internal static CombatGameConstants Constants;
      public static void CombatInit ( CombatHUD __instance ) {
         CacheCombatState();
         Mod.HUD = __instance;
         foreach ( var mod in modules ) try {
            mod.Value.CombatStarts();
         }                 catch ( Exception ex ) { Error( ex ); }
      }

      public static void CacheCombatState () {
         Combat = UnityGameInstance.BattleTechGame?.Combat;
         Constants = Combat?.Constants;
      }
   }

   public abstract class ModModule {
      public abstract void InitPatch();
      public virtual void CombatStarts () { }
   }
}