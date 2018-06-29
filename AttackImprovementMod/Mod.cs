using BattleTech;
using BattleTech.UI;
using Harmony;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;

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

      private static void checkNaN ( float count ) {
         float block = 2f/5f, add = count*block, max = 100000;
         for ( int i = 0 ; i <= max ; i++ ) {
            float strength = add + ((float)i)*block/max;
            if ( strength > 1.9999f ) continue;
            for ( int j = 0 ; j <= 10000 ; j++ ) {
               float acc = ((float)j)/10000f, corrected = RollCorrection.ReverseRollCorrection( acc, strength );
               if ( float.IsNaN( corrected ) ) Console.WriteLine( corrected + " = " + acc + ", " + strength );
            }
         }
      }

      static void Main () { // Sometimes I run quick tests as a console app here
         /*
         new Thread( () => checkNaN(0) ).Start();
         new Thread( () => checkNaN(1) ).Start();
         new Thread( () => checkNaN(2) ).Start();
         new Thread( () => checkNaN(3) ).Start();
         new Thread( () => checkNaN(4) ).Start();
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
         Console.ReadKey();
      }

      public static void Init ( string directory, string settingsJSON ) {
         string logCache = "";
         try {
            Settings = JsonConvert.DeserializeObject<ModSettings>( settingsJSON );
            logCache =  "Mod Settings: " + JsonConvert.SerializeObject( Settings, Formatting.Indented );
         } catch ( Exception ex ) {
            logCache = string.Format( "Error: Cannot read mod settings, using default: {0}", ex );
         }

         LogDir = Path.GetDirectoryName( Assembly.GetExecutingAssembly().Location ) + "/";
         logCache += "\nLog folder set to " + LogDir + ". If that fails, fallback to " + FALLBACK_LOG_DIR + "." ;
         DeleteLog( LOG_NAME );
         Log( logCache );

         // Need a proper version parsing routine. Next time.
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

         try {
            if ( Settings.ShowRealMechCalledShotChance || Settings.ShowRealVehicleCalledShotChance ) {
               patchClass = typeof( Mod );
               Patch( typeof( CombatHUD ), "Init", typeof( CombatGameState ), null, "RecordCombatHUD" );
            }

            LoadModule( "Roll Corrections and Logger", typeof( RollCorrection ) );
            LoadModule( "Hit Location Bugfixs and Logger", typeof( FixHitLocation ) );
            LoadModule( "Called Shot HUD", typeof( FixCalledShotPopUp ) );
            Log();

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
         if ( mi == null ) Log( "Error: Cannot find patch method " + method );
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
         HarmonyMethod pre = MakePatch( prefix ), post = MakePatch( postfix );
         if ( pre == null && post == null ) return; // MakePatch would have reported method not found
         harmony.Patch( patched, MakePatch( prefix ), MakePatch( postfix ) );
         Log( string.Format( "Patched: {0} {1} [ {2} : {3} ]", new object[]{ patchedClass, patched, prefix, postfix } ) );
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
         MethodInfo m = module.GetMethod( "InitPatch", BindingFlags.Static | BindingFlags.NonPublic );
         if ( m != null ) 
            m.Invoke( null, null );
         else
            Log( "Cannot Initiate " + module );
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