using System;
using System.Collections.Generic;

namespace Sheepy.BattleTechMod.AttackImprovementMod {

   public class Mod : BattleMod {

      public static ModSettings Settings = new ModSettings();

      internal static bool GameUseClusteredCallShot = false; // True if game version is less than 1.1
      internal static bool GameHitLocationBugged = false; // True if game version is less than 1.1.1
      internal static readonly Dictionary<string, BattleModModule> modules = new Dictionary<string, BattleModModule>();

      public static void Init ( string directory, string settingsJSON ) {
         new Mod().Start( ref ModLog );
      }

      public override void ModStarts () {
         ModLogDir = LogDir;
         LoadSettings( ref Settings, SanitizeSettings );
         NormaliseSettings();
         Log( "Do NOT change settings here. This is just a log." );
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

         Add( new UserInterface(){ Name = "User Interface" } );
         Add( new LineOfSight(){ Name = "Line of Fire" } );
         Add( new CalledShotPopUp(){ Name = "Called Shot HUD" } );
         Add( new Melee(){ Name = "Melee" } );
         Add( new RollModifier(){ Name = "Roll Modifier" } );
         Add( new RollCorrection(){ Name = "Roll Corrections" } );
         Add( new HitLocation(){ Name = "Hit Distribution" } );
         Add( new AttackLog(){ Name = "Logger" } ); // Must be after all other modules if we want to log modded data
      }

      public override void GameStarts () {
         Log( "Detected Mods: " + Join( ", ", BattleMod.GetModList() ) );
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
         settings.ShowUnderArmourDamage = settings.PaperDollDivulgeUnderskinDamage.GetValueOrDefault( settings.ShowUnderArmourDamage );
         settings.KillZeroHpLocation = settings.FixNonIntegerDamage.GetValueOrDefault( settings.KillZeroHpLocation );

         if ( settings.LOSWidthMultiplier != null && settings.LOSWidthMultiplier != 2f )
            settings.LOSWidth = settings.LOSWidthMultiplier;
         if ( settings.LOSWidthBlockedMultiplier != null && settings.LOSWidthBlockedMultiplier != 3f )
            settings.LOSWidthBlocked = settings.LOSWidthBlockedMultiplier * 0.75;

         settings.ShowCorrectedHitChance = settings.ShowRealWeaponHitChance.GetValueOrDefault( settings.ShowCorrectedHitChance );
         if ( settings.ShowDecimalCalledChance == true && settings.CalledChanceFormat == "" )
            settings.CalledChanceFormat = "{0:0.0}%"; // Keep digits consistent
         // if ( old.ShowDecimalHitChance == true ); // Same as new default, don't change
         if ( settings.LogHitRolls == true && ( settings.AttackLogLevel == null || settings.AttackLogLevel.Trim().ToLower() == "none" ) )
            settings.AttackLogLevel = "All";
#pragma warning restore CS0618

         RangeCheck( "LOSWidth", ref Settings.LOSWidth, 0f, 10f );
         RangeCheck( "LOSWidthBlocked", ref Settings.LOSWidthBlocked, 0f, 10f );
         RangeCheck( "LOSMarkerBlockedMultiplier", ref Settings.LOSMarkerBlockedMultiplier, 0f, 10f );
         RangeCheck( "ArcLineSegments", ref Settings.ArcLinePoints, 1, 1000 );

         RangeCheck( "MechCalledShotMultiplier", ref Settings.MechCalledShotMultiplier, 0f, 1024f );
         RangeCheck( "VehicleCalledShotMultiplier", ref Settings.VehicleCalledShotMultiplier, 0f, 1024f );

         RangeCheck( "HitChanceStep", ref Settings.HitChanceStep, 0f, 1f );
         RangeCheck( "BaseHitChanceModifier", ref Settings.BaseHitChanceModifier, -10f, 10f );
         RangeCheck( "MeleeHitChanceModifier", ref Settings.MeleeHitChanceModifier, -10f, 10f );
         RangeCheck( "MaxFinalHitChance", ref Settings.MaxFinalHitChance, 0.1f, 1f );
         RangeCheck( "MinFinalHitChance", ref Settings.MinFinalHitChance, 0f, 1f );

         RangeCheck( "RollCorrectionStrength", ref Settings.RollCorrectionStrength, 0f, 0f, 1.999f, 2f );
         RangeCheck( "MissStreakBreakerThreshold", ref Settings.MissStreakBreakerThreshold, 0f, 1f );
         RangeCheck( "MissStreakBreakerDivider", ref Settings.MissStreakBreakerDivider, -100f, 100f );
         
         if ( ! settings.PersistentLog ) {
            // In version 1.0, I thought we may need to keep two logs: attack/location rolls and critical rolls. They are now merged, and the old log may be removed.
            new Logger( LogDir + "Log_AttackRoll.txt" ).Delete();
         }

         return settings;
      }

      /* Changes that we don't want to write back to settings.json */
      private void NormaliseSettings () {
         // Colours that fail to parse will be changed to empty string
         LineOfSight.Parse( ref Settings.LOSMeleeColor );
         LineOfSight.Parse( ref Settings.LOSClearColor );
         LineOfSight.Parse( ref Settings.LOSBlockedPreColor );
         LineOfSight.Parse( ref Settings.LOSBlockedPostColor );
         LineOfSight.Parse( ref Settings.LOSIndirectColor );
         LineOfSight.Parse( ref Settings.LOSNoAttackColor );

         NullIfEmpty( ref Settings.CalledChanceFormat );
         NullIfEmpty( ref Settings.HitChanceFormat );

         NullIfEmpty( ref Settings.MeleeAccuracyFactors );
         NullIfEmpty( ref Settings.AttackLogLevel );
      }

      // ============ Logging ============

      internal static string ModLogDir = ""; // A static variable for roll log
      internal static Logger ModLog = Logger.BTML_LOG;

      public static void Log ( object message ) { ModLog.Log( message ); }
      public static void Log ( string message = "" ) { ModLog.Log( message ); }
      public static void Log ( string message, params object[] args ) { ModLog.Log( message, args ); }
      
      public static void Warn ( object message ) { ModLog.Warn( message ); }
      public static void Warn ( string message ) { ModLog.Warn( message ); }
      public static void Warn ( string message, params object[] args ) { ModLog.Warn( message, args ); }

      public static bool Error ( object message ) { return ModLog.Error( message ); }
      public static void Error ( string message ) { ModLog.Error( message ); }
      public static void Error ( string message, params object[] args ) { ModLog.Error( message, args ); }
   }
}