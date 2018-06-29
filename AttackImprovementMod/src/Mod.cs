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

      internal static bool GameUseClusteredCallShot = false; // True if game version is less than 1.1
      internal static bool GameHitLocationBugged = false; // True if game version is less than 1.1.1
      internal const string FALLBACK_LOG_DIR = "Mods/AttackImprovementMod/";
      internal const string LOG_NAME = "Log_AttackImprovementMod.txt";
      internal static string LogDir = "";
      internal static HarmonyInstance harmony = HarmonyInstance.Create( "io.github.Sheep-y.AttackImprovementMod" );

      public static void Init ( string directory, string settingsJSON ) {
         // Get log settings
         string logCache = "";
         try {
            Settings = JsonConvert.DeserializeObject<ModSettings>( settingsJSON );
            logCache =  "Mod Settings: " + JsonConvert.SerializeObject( Settings, Formatting.Indented );
         } catch ( Exception ex ) {
            logCache = string.Format( "Error: Cannot read mod settings, using default: {0}", ex );
         }
         try {
            if ( Settings.LogFolder.Length <= 0 ) {
               LogDir = Path.GetDirectoryName( Assembly.GetExecutingAssembly().Location ) + "/";
               logCache += "\nLog folder set to " + LogDir + ". If that fails, fallback to " + FALLBACK_LOG_DIR + "." ;
            }
            DeleteLog( LOG_NAME );
            Log( logCache );
         } catch ( Exception ex ) { Log( ex ); }

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

         // Patching
         if ( Settings.ShowRealMechCalledShotChance || Settings.ShowRealVehicleCalledShotChance || Settings.ShowHeatAndStab ) {
            patchClass = typeof( Mod );
            Patch( typeof( CombatHUD ), "Init", typeof( CombatGameState ), null, "RecordCombatHUD" );
         }

         if ( Settings.LogHitRolls )
            LoadModule( "Logger", typeof( AttackLog ) );
         LoadModule( "Roll Corrections", typeof( RollCorrection ) );
         LoadModule( "Called Shot and Hit Location", typeof( FixHitLocation ) );
         LoadModule( "Called Shot HUD", typeof( FixCalledShotPopUp ) );
         LoadModule( "Heat and Stability", typeof( HeatAndStab ) );
         //LoadModule( "Line of Fire", typeof( LineOfFire ) );
         LoadModule( "Melee", typeof( Melee ) );
         Log();
      }

      // ============ Harmony ============

      private static Type patchClass;
      /* Find and create a HarmonyMethod from current patchClass. method must be public and has unique name. */
      internal static HarmonyMethod MakePatch ( string method ) {
         if ( method == null ) return null;
         MethodInfo mi = patchClass.GetMethod( method );
         if ( mi == null ) Log( "Error: Cannot find patch method " + method );
         return new HarmonyMethod( mi );
      }

      internal static void Patch ( Type patchedClass, string patchedMethod, string prefix, string postfix ) {
         Patch( patchedClass, patchedMethod, BindingFlags.Public | BindingFlags.Instance, (Type[]) null, prefix, postfix );
      }

      internal static void Patch ( Type patchedClass, string patchedMethod, Type parameterType, string prefix, string postfix ) {
         Patch( patchedClass, patchedMethod, BindingFlags.Public | BindingFlags.Instance, new Type[]{ parameterType }, prefix, postfix );
      }

      internal static void Patch ( Type patchedClass, string patchedMethod, Type[] parameterTypes, string prefix, string postfix ) {
         Patch( patchedClass, patchedMethod, BindingFlags.Public | BindingFlags.Instance, parameterTypes, prefix, postfix );
      }

      internal static void Patch ( Type patchedClass, string patchedMethod, BindingFlags flags, string prefix, string postfix ) {
         Patch( patchedClass, patchedMethod, flags, (Type[]) null, prefix, postfix );
      }

      internal static void Patch ( Type patchedClass, string patchedMethod, BindingFlags flags, Type parameterType, string prefix, string postfix ) {
         Patch( patchedClass, patchedMethod, flags, new Type[]{ parameterType }, prefix, postfix );
      }

      internal static void Patch( Type patchedClass, string patchedMethod, BindingFlags flags, Type[] parameterTypes, string prefix, string postfix ) {
         MethodInfo patched;
         if ( ( flags | BindingFlags.Static | BindingFlags.Instance  ) == 0  ) flags |= BindingFlags.Instance;
         if ( ( flags | BindingFlags.Public | BindingFlags.NonPublic ) == 0  ) flags |= BindingFlags.Public;
         if ( parameterTypes == null )
            patched = patchedClass.GetMethod( patchedMethod, flags );
         else
            patched = patchedClass.GetMethod( patchedMethod, flags, null, parameterTypes, null );
         if ( patched == null ) {
            Log( string.Format( "Error: Cannot find {0}.{1}(...) to patch", new Object[]{ patchedClass.Name, patchedMethod } ) );
            return;
         }
         Patch( patched, prefix, postfix );
      }

      internal static void Patch( MethodInfo patched, string prefix, string postfix ) {
         HarmonyMethod pre = MakePatch( prefix ), post = MakePatch( postfix );
         if ( pre == null && post == null ) return; // MakePatch would have reported method not found
         harmony.Patch( patched, MakePatch( prefix ), MakePatch( postfix ) );
         Log( string.Format( "Patched: {0} {1} [ {2} : {3} ]", new object[]{ patched.DeclaringType, patched, prefix, postfix } ) );
      }

      // ============ UTILS ============

      internal static void DeleteLog( string file ) {
         try {
            File.Delete( LogDir + file );
         } catch ( Exception ) { }
         try {
            File.Delete( FALLBACK_LOG_DIR + file );
         } catch ( Exception ) { }
      }

      internal static bool Log( object message ) { Log( message.ToString() ); return true; }
      internal static void Log( string message = "" ) {
         string logName = LogDir + LOG_NAME;
         try {
            if ( ! File.Exists( logName ) ) 
               message = DateTime.Now.ToString( "o" ) + "\r\n\r\n" + message;
         } catch ( Exception ) {}
         WriteLog( LOG_NAME, message + "\r\n" );
      }

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

      internal static int TryGet<T> ( Dictionary<T, int> table, T key ) {
         table.TryGetValue( key, out int result );
         return result;
      }

      private static void LoadModule( string name, Type module ) {
         Log( "=== Patching " + name + " ===" );
         patchClass = module;
         try {
            MethodInfo m = module.GetMethod( "InitPatch", BindingFlags.Static | BindingFlags.NonPublic );
            if ( m != null ) 
               m.Invoke( null, null );
            else
               Log( "Cannot Initiate " + module );
         } catch ( Exception ex ) { Log( ex ); }
      }

      // ============ Game States ============

      internal static CombatHUD HUD;
      public static void RecordCombatHUD ( CombatHUD __instance ) {
         Mod.HUD = __instance;
      }

      // A shortcut to get CombatGameConstants
      internal static CombatGameState Combat { get { return UnityGameInstance.BattleTechGame.Combat; } }
   }
}