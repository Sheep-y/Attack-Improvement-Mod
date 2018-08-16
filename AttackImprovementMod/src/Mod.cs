using System;
using System.Text.RegularExpressions;
using Sheepy.Logging;

namespace Sheepy.BattleTechMod.AttackImprovementMod {

   public class Mod : BattleMod {

      public static ModSettings Settings = new ModSettings();

      public static void Init ( string directory, string settingsJSON ) {
         new Mod().Start( ref ModLog );
      }

      public override void ModStarts () {
         ModLogDir = LogDir;
         LoadSettings( ref Settings, SanitizeSettings );
         NormaliseSettings();
         new Logger( LogDir + "Log_AttackImprovementMod.txt" ).Delete(); // Delete log of old version
         Info();

         Add( new UserInterface(){ Name = "User Interface" } );
         Add( new LineOfSight(){ Name = "Line of Fire" } );
         Add( new CalledShotPopUp(){ Name = "Called Shot HUD" } );
         Add( new Melee(){ Name = "Melee" } );
         Add( new RollModifier(){ Name = "Roll Modifier" } );
         Add( new ModifierList(){ Name = "Modifier List" } );
         Add( new RollCorrection(){ Name = "Roll Corrections" } );
         Add( new HitLocation(){ Name = "Hit Location" } );
         Add( new HitResolution(){ Name = "Hit Resolution" } );
         Add( new AttackLog(){ Name = "Logger" } );
      }

      public override void GameStarts () {
         ModLog.LogLevel = System.Diagnostics.SourceLevels.Verbose;
         Info( "Detected Mods: " + Join( ", ", BattleMod.GetModList() ) );
      }

      private ModSettings SanitizeSettings ( ModSettings settings ) {
         // Switch log folder if specified
         if ( ! string.IsNullOrEmpty( settings.LogFolder ) && settings.LogFolder != LogDir ) {
            Log.Delete();
            if ( ! settings.LogFolder.EndsWith( "/" ) && ! settings.LogFolder.EndsWith( "\\" ) )
               settings.LogFolder += "/";
            LogDir = settings.LogFolder;
            Log.Info( "{2} {0} Version {1} In {3}\r\n", Name, Version, DateTime.Now.ToString( "s" ), BaseDir );
         }

#pragma warning disable CS0618 // Disable "this is obsolete" warnings since we must read them to upgrade them.
         MigrateColors( settings.LOSMeleeColor      , ref settings.LOSMeleeColors       );
         MigrateColors( settings.LOSClearColor      , ref settings.LOSClearColors       );
         MigrateColors( settings.LOSBlockedPreColor , ref settings.LOSBlockedPreColors  );
         MigrateColors( settings.LOSBlockedPostColor, ref settings.LOSBlockedPostColors );
         MigrateColors( settings.LOSIndirectColor   , ref settings.LOSIndirectColors    );
         if ( settings.LOSNoAttackColor != null && settings.LOSNoAttackColors == null)
            settings.LOSNoAttackColors = settings.LOSNoAttackColor;
         if ( settings.PersistentLog != null )
            settings.AttackLogArchiveMaxMB = settings.PersistentLog == false ? 4 : 128;

         settings.ShowUnderArmourDamage = settings.PaperDollDivulgeUnderskinDamage.GetValueOrDefault( settings.ShowUnderArmourDamage );
         settings.KillZeroHpLocation = settings.FixNonIntegerDamage.GetValueOrDefault( settings.KillZeroHpLocation );

         if ( settings.LOSWidthMultiplier != null && settings.LOSWidthMultiplier != 2 )
            settings.LOSWidth = settings.LOSWidthMultiplier.GetValueOrDefault( 2 );
         if ( settings.LOSWidthBlockedMultiplier != null && settings.LOSWidthBlockedMultiplier != 3 )
            settings.LOSWidthBlocked = settings.LOSWidthBlockedMultiplier.GetValueOrDefault( 3 ) * 0.75m;

         // Add SelfTerrainMelee and spacing to 2.0 default
         if ( settings.MeleeAccuracyFactors == "DFA,Height,Inspired,SelfChassis,SelfHeat,SelfStoodUp,SelfWalked,Sprint,TargetEffect,TargetEvasion,TargetProne,TargetShutdown,TargetSize,TargetTerrainMelee,WeaponAccuracy" )
            settings.MeleeAccuracyFactors = "Direction, DFA, Height, Inspired, Jumped, SelfChassis, SelfHeat, SelfStoodUp, SelfTerrainMelee, Sprint, TargetEffect, TargetEvasion, TargetProne, TargetShutdown, TargetSize, TargetTerrainMelee, Walked, WeaponAccuracy";
         else if ( settings.MeleeAccuracyFactors.ToLower().Contains( "selfwalked" ) )
            settings.MeleeAccuracyFactors = Regex.Replace( settings.MeleeAccuracyFactors, "SelfWalked", "Walked", RegexOptions.IgnoreCase );

         settings.ShowCorrectedHitChance = settings.ShowRealWeaponHitChance.GetValueOrDefault( settings.ShowCorrectedHitChance );
         if ( settings.ShowDecimalCalledChance == true && settings.CalledChanceFormat == "" )
            settings.CalledChanceFormat = "{0:0.0}%"; // Keep digits consistent
         // if ( old.ShowDecimalHitChance == true ); // Same as new default, don't change
         if ( settings.LogHitRolls == true && ( settings.AttackLogLevel == null || settings.AttackLogLevel.Trim().ToLower() == "none" ) )
            settings.AttackLogLevel = "All";
#pragma warning restore CS0618

         RangeCheck( "LOSWidth", ref Settings.LOSWidth, 0, 10 );
         RangeCheck( "LOSWidthBlocked", ref Settings.LOSWidthBlocked, 0, 10 );
         RangeCheck( "LOSMarkerBlockedMultiplier", ref Settings.LOSMarkerBlockedMultiplier, 0, 10 );
         RangeCheck( "ArcLineSegments", ref Settings.ArcLinePoints, 1, 1000 );

         RangeCheck( "MechCalledShotMultiplier", ref Settings.MechCalledShotMultiplier, 0, 1024 );
         RangeCheck( "VehicleCalledShotMultiplier", ref Settings.VehicleCalledShotMultiplier, 0, 1024 );

         RangeCheck( "BaseHitChanceModifier", ref Settings.BaseHitChanceModifier, -10, 10 );
         RangeCheck( "MeleeHitChanceModifier", ref Settings.MeleeHitChanceModifier, -10, 10 );
         RangeCheck( "ToHitMechFromFront", ref Settings.ToHitMechFromFront, -20, 20 );
         RangeCheck( "ToHitMechFromSide" , ref Settings.ToHitMechFromSide , -20, 20 );
         RangeCheck( "ToHitMechFromRear" , ref Settings.ToHitMechFromRear , -20, 20 );
         RangeCheck( "ToHitVehicleFromFront", ref Settings.ToHitVehicleFromFront, -20, 20 );
         RangeCheck( "ToHitVehicleFromSide" , ref Settings.ToHitVehicleFromSide , -20, 20 );
         RangeCheck( "ToHitVehicleFromRear" , ref Settings.ToHitVehicleFromRear , -20, 20 );
         RangeCheck( "ToHitSelfJumped", ref Settings.ToHitSelfJumped, -20, 20 );

         RangeCheck( "HitChanceStep", ref Settings.HitChanceStep, 0, 1 );
         RangeCheck( "MaxFinalHitChance", ref Settings.MaxFinalHitChance, 0.1m, 1 );
         RangeCheck( "MinFinalHitChance", ref Settings.MinFinalHitChance, 0, 1 );

         RangeCheck( "RollCorrectionStrength", ref Settings.RollCorrectionStrength, 0, 0, 1.999m, 2 );
         RangeCheck( "MissStreakBreakerThreshold", ref Settings.MissStreakBreakerThreshold, 0, 1 );
         RangeCheck( "MissStreakBreakerDivider", ref Settings.MissStreakBreakerDivider, -100, 100 );

         // Is 1TB a reasonable limit of how many logs to keep?
         RangeCheck( "AttackLogArchiveMaxMB", ref Settings.AttackLogArchiveMaxMB, 0, 1024*1024 );

         if ( Settings.SettingVersion == null ) Settings.SettingVersion = 0;
         if ( Settings.SettingVersion < 2_001_000 ) { // Pre-2.1.0
            Settings.AttackLogLevel = "All"; // Log is now enabled by default with new background logger
            string original = settings.MeleeAccuracyFactors.ToLower();
            if ( ! string.IsNullOrEmpty( original ) ) { // Update customised melee modifiers
               if ( original.Contains( "selfwalked" ) ) Settings.MeleeAccuracyFactors = original.Replace( "selfwalked", "walked" );
               if ( ! original.Contains( "direction" ) ) Settings.MeleeAccuracyFactors += ", Direction";
               if ( ! original.Contains( "jumped" ) ) Settings.MeleeAccuracyFactors += ", Jumped";
               if ( ! original.Contains( "selfterrainmelee" ) ) Settings.MeleeAccuracyFactors += ", SelfTerrainMelee";
            }
            if ( Settings.DiminishingBonusPowerBase == 0.800000011920929m ) Settings.DiminishingBonusPowerBase = 0.8m;
            if ( Settings.DiminishingPenaltyPowerBase == 0.800000011920929m ) Settings.DiminishingPenaltyPowerBase = 0.8m;
            if ( Settings.DiminishingPenaltyPowerDivisor == 3.2999999523162842m ) Settings.DiminishingPenaltyPowerDivisor = 3.3m;
         }
         Settings.SettingVersion = 2_001_000;

         return settings;
      }

      private void MigrateColors ( string old, ref string now ) {
         if ( string.IsNullOrEmpty( old ) || now == null ) return;
         int pos = now.IndexOf( ',' );
         if ( pos < 0 ) return;
         now = old + now.Substring( pos );
      }

      /* Changes that we don't want to write back to settings.json */
      private void NormaliseSettings () {
         NullIfEmpty( ref Settings.FloatingArmorColourPlayer );
         NullIfEmpty( ref Settings.FloatingArmorColourEnemy );
         NullIfEmpty( ref Settings.FloatingArmorColourAlly );

         NullIfEmpty( ref Settings.LOSMeleeColors );
         NullIfEmpty( ref Settings.LOSClearColors );
         NullIfEmpty( ref Settings.LOSBlockedPreColors );
         NullIfEmpty( ref Settings.LOSBlockedPostColors );
         NullIfEmpty( ref Settings.LOSIndirectColors );
         NullIfEmpty( ref Settings.LOSNoAttackColors );

         NullIfEmpty( ref Settings.FacingMarkerPlayerColors );
         NullIfEmpty( ref Settings.FacingMarkerEnemyColors  );
         NullIfEmpty( ref Settings.FacingMarkerTargetColors );

         NullIfEmpty( ref Settings.CalledChanceFormat );
         NullIfEmpty( ref Settings.HitChanceFormat );

         NullIfEmpty( ref Settings.MeleeAccuracyFactors );
         NullIfEmpty( ref Settings.AttackLogLevel );
      }

      internal static bool FriendOrFoe ( BattleTech.AbstractActor subject, bool TrueIfFriend, bool TrueIfFoe ) {
         if ( TrueIfFriend == TrueIfFoe ) return TrueIfFriend;
         bool isFriend = subject.team.IsFriendly( Combat.LocalPlayerTeam );
         return isFriend == TrueIfFriend; // Same as ( isFriend && TrueIfFriend ) || ( ! isFriend && TrueIfFoe );
      }

      // ============ Utils ============

      public static UnityEngine.Color? ParseColour ( string htmlColour ) {
         if ( htmlColour == "" || htmlColour == null ) return null;
         if ( UnityEngine.ColorUtility.TryParseHtmlString( htmlColour, out UnityEngine.Color result ) )
            return result;
         Error( "Cannot parse \"" + htmlColour + "\" as colour." );
         return null;
      }

      // ============ Logging ============

      internal static string ModLogDir = ""; // A static variable for roll log
      internal static Logger ModLog = BattleMod.BTML_LOG;

      public static void Stacktrace () { Info( Logger.Stacktrace ); }
      public static void Trace ( object message = null, params object[] args ) { ModLog.Trace( message, args ); }
      public static void Verbo ( object message = null, params object[] args ) { ModLog.Verbo( message, args ); }
      public static void Info  ( object message = null, params object[] args ) { ModLog.Info( message, args ); }
      public static void Warn  ( object message = null, params object[] args ) { ModLog.Warn( message, args ); }
      public static bool Error ( object message = null, params object[] args ) { ModLog.Error( message, args ); return true; }
   }
}