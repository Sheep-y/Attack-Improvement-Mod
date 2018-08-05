using System;

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
         Log();

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
         Log( "Detected Mods: " + Join( ", ", BattleMod.GetModList() ) );
      }

      private ModSettings SanitizeSettings ( ModSettings settings ) {
         // Switch log folder if specified
         if ( ! String.IsNullOrEmpty( settings.LogFolder ) && settings.LogFolder != LogDir ) {
            Logger.Delete();
            if ( ! settings.LogFolder.EndsWith( "/" ) && ! settings.LogFolder.EndsWith( "\\" ) )
               settings.LogFolder += "/";
            LogDir = settings.LogFolder;
            Logger.Info( "{2} {0} Version {1} In {3}\r\n", Name, Version, DateTime.Now.ToString( "s" ), BaseDir );
         }

#pragma warning disable CS0618 // Disable "this is obsolete" warnings since we must read them to upgrade them.
         settings.ShowUnderArmourDamage = settings.PaperDollDivulgeUnderskinDamage.GetValueOrDefault( settings.ShowUnderArmourDamage );
         settings.KillZeroHpLocation = settings.FixNonIntegerDamage.GetValueOrDefault( settings.KillZeroHpLocation );

         if ( settings.LOSWidthMultiplier != null && settings.LOSWidthMultiplier != 2f )
            settings.LOSWidth = settings.LOSWidthMultiplier.GetValueOrDefault( 2f );
         if ( settings.LOSWidthBlockedMultiplier != null && settings.LOSWidthBlockedMultiplier != 3f )
            settings.LOSWidthBlocked = settings.LOSWidthBlockedMultiplier.GetValueOrDefault( 3f ) * 0.75f;

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
         NullIfEmpty( ref Settings.LOSMeleeColors );
         NullIfEmpty( ref Settings.LOSClearColors );
         NullIfEmpty( ref Settings.LOSBlockedPreColors );
         NullIfEmpty( ref Settings.LOSBlockedPostColors );
         NullIfEmpty( ref Settings.LOSIndirectColors );
         NullIfEmpty( ref Settings.LOSNoAttackColors );

         NullIfEmpty( ref Settings.DirectionMarkerColors );
         NullIfEmpty( ref Settings.DirectionMarkerActiveColors );

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

      // ============ Logging ============

      internal static string ModLogDir = ""; // A static variable for roll log
      internal static Logger ModLog = Logger.BTML_LOG;

      public static void Trace ( object message = null, params object[] args ) { ModLog.Trace( message, args ); }
      public static void Vocal ( object message = null, params object[] args ) { ModLog.Vocal( message, args ); }
      public static void Log   ( object message = null, params object[] args ) { ModLog.Info( message, args ); }
      public static void Warn  ( object message = null, params object[] args ) { ModLog.Warn( message, args ); }
      public static bool Error ( object message = null, params object[] args ) { ModLog.Error( message, args ); return true; }
   }
}