using BattleTech;
using BattleTech.UI;
using System;
using System.Collections.Generic;

namespace Sheepy.AttackImprovementMod {
   using Sheepy.BattleTechMod;

   public class Mod : ModBase {

      public static ModSettings Settings = new ModSettings();

      internal static bool GameUseClusteredCallShot = false; // True if game version is less than 1.1
      internal static bool GameHitLocationBugged = false; // True if game version is less than 1.1.1
      internal static readonly Dictionary<string, ModModule> modules = new Dictionary<string, ModModule>();

      public static void Init ( string directory, string settingsJSON ) {
         new Mod().Init( ref modLog );
      }

      public override void Startup () {
         LoadSettings<ModSettings>( ref Settings, SanitizeSettings );
         new Logger( LogDir + "Log_AttackImprovementMod.txt" ).Delete(); // Delete log of old version

         // Hook to combat starts
         Patch( typeof( CombatHUD ), "Init", typeof( CombatGameState ), null, "CombatInit" );

         modules.Add( "Logger", new AttackLog() ); // @TODO Must be above RollCorrection as long as GetCorrectedRoll is overriden
         modules.Add( "User Interface", new UserInterface() );
         modules.Add( "Line of Fire", new LineOfSight() );
         modules.Add( "Called Shot HUD", new CalledShotPopUp() );
         modules.Add( "Melee", new Melee() );
         modules.Add( "Roll Modifier", new RollModifier() );
         modules.Add( "Roll Corrections", new RollCorrection() );
         modules.Add( "Hit Distribution", new HitLocation() );

         foreach ( var mod in modules )  try {
            Log( "=== Patching " + mod.Key + " ===" );
            mod.Value.Startup();
         }                 catch ( Exception ex ) { Error( ex ); }
         Log( "=== All Mod Modules Initialised ===\n" );
      }

      public void LogSettings () {
         // Cache log lines until after we determined folder and deleted old log
         /*
         try {
            Settings = JsonConvert.DeserializeObject<OldSettings>( settingsJSON );
            logCache.AppendFormat( "Mod Settings: {0}\r\n", JsonConvert.SerializeObject( Settings, Formatting.Indented ) );
         } catch ( Exception ) {
            logCache.Append( "Error: Cannot parse mod settings, using default." );
         }
         try {
            LogDir = Settings.LogFolder;
            if ( LogDir == null || LogDir.Length <= 0 )
               LogDir = directory + "/";
            logCache.AppendFormat( "Log folder set to {0}.", LogDir );
            DeleteLog( LogName );
            Log( logCache.ToString() );
         }                 catch ( Exception ex ) { Error( ex ); }
         UpgradeSettings( Settings );

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
         */
      }

      private ModSettings SanitizeSettings ( ModSettings settings ) {
         // Switch log folder if specified
         if ( ! String.IsNullOrEmpty( settings.LogFolder ) && settings.LogFolder != LogDir ) {
            Logger.Delete();
            if ( ! settings.LogFolder.EndsWith( "/" ) && ! settings.LogFolder.EndsWith( "\\" ) )
               settings.LogFolder += "/";
            LogDir = settings.LogFolder;
            Logger.Log( "{2} {0} Version {1} In {3}\r\n", Name, Version, DateTime.Now.ToString( "s" ), BaseDir );
         }

#pragma warning disable CS0618 // Disable "this is obsolete" warnings since we must read them to upgrade them.
         if ( settings.ShowRealWeaponHitChance == true )
            settings.ShowCorrectedHitChance = true;
         if ( settings.ShowDecimalCalledChance == true && settings.CalledChanceFormat == "" )
            settings.CalledChanceFormat = "{0:0.0}%"; // Keep digits consistent
         // if ( old.ShowDecimalHitChance == true ); // Same as new default, don't change
         if ( settings.LogHitRolls == true && ( settings.AttackLogLevel == null || settings.AttackLogLevel.Trim().ToLower() == "none" ) )
            settings.AttackLogLevel = "All";
#pragma warning restore CS0618 // Disable "this is obsolete" warnings since we must read them to upgrade them.

         if ( ! settings.PersistentLog ) {
            // In version 1.0, I thought we may need to keep two logs: attack/location rolls and critical rolls. They are now merged, and the old log may be removed.
            new Logger( LogDir + "Log_AttackRoll.txt" ).Delete();
         }

         return settings;
      }

      // ============ Logging ============

      private static Logger modLog = Logger.BTML_LOG;

      public static void Log ( object message ) { modLog.Log( message ); }
      public static void Log ( string message = "" ) { modLog.Log( message ); }
      public static void Log ( string message, params object[] args ) { modLog.Log( message, args ); }
      
      public static void Warn ( object message ) { modLog.Warn( message ); }
      public static void Warn ( string message ) { modLog.Warn( message ); }
      public static void Warn ( string message, params object[] args ) { modLog.Warn( message, args ); }

      public static bool Error ( object message ) { return modLog.Error( message ); }
      public static void Error ( string message ) { modLog.Error( message ); }
      public static void Error ( string message, params object[] args ) { modLog.Error( message, args ); }

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
}