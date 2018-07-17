using System;
using System.Collections.Generic;

namespace Sheepy.BattleTechMod.AttackImprovementMod {

   public class Mod : BattleMod {

      public static ModSettings Settings = new ModSettings();

      internal static bool GameUseClusteredCallShot = false; // True if game version is less than 1.1
      internal static bool GameHitLocationBugged = false; // True if game version is less than 1.1.1
      internal static readonly Dictionary<string, BattleModModule> modules = new Dictionary<string, BattleModModule>();

      public static void Init ( string directory, string settingsJSON ) {
         new Mod().Start( ref modLog );
      }

      public override void ModStarts () {
         LoadSettings<ModSettings>( ref Settings, SanitizeSettings );
         new Logger( LogDir + "Log_AttackImprovementMod.txt" ).Delete(); // Delete log of old version

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

         Add( new AttackLog(){ Name = "Logger" } ); // @TODO Must be above RollCorrection as long as GetCorrectedRoll is overriden
         Add( new UserInterface(){ Name = "User Interface" } );
         Add( new LineOfSight(){ Name = "Line of Fire" } );
         Add( new CalledShotPopUp(){ Name = "Called Shot HUD" } );
         Add( new Melee(){ Name = "Melee" } );
         Add( new RollModifier(){ Name = "Roll Modifier" } );
         Add( new RollCorrection(){ Name = "Roll Corrections" } );
         Add( new HitLocation(){ Name = "Hit Distribution" } );
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
   }
}