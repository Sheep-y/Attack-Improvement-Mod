using System;
using System.Text.RegularExpressions;
using Sheepy.Logging;

namespace Sheepy.BattleTechMod.AttackImprovementMod {

   public class Mod : BattleMod {

      public static ModSettings Settings = new ModSettings();

      public static void Init ( string directory, string settingsJSON ) {
         new Mod().Start( ref ModLog );
         ModLog.LogLevel = System.Diagnostics.SourceLevels.Verbose;
      }

      public override void ModStarts () {
         ModLogDir = LogDir;
         LoadSettings( ref Settings, SanitizeSettings );
         NormaliseSettings();
         new Logger( LogDir + "Log_AttackImprovementMod.txt" ).Delete(); // Delete log of old version

         Add( new HeauUpDisplay(){ Name = "Head Up Display" } ); // Created first to prepare callout handling
         Add( new UserInterfacePanels(){ Name = "User Interface Panels" } );
         Add( new Targetting(){ Name = "Targetting" } );
         Add( new WeaponInfo(){ Name = "Weapons Information" } );
         Add( new LineOfSight(){ Name = "Line of Fire" } );
         Add( new CalledShotPopUp(){ Name = "Called Shot HUD" } );
         Add( new Melee(){ Name = "Melee" } );
         Add( new RollModifier(){ Name = "Roll Modifier" } );
         Add( new ModifierList(){ Name = "Modifier List" } );
         Add( new RollCorrection(){ Name = "Roll Corrections" } );
         Add( new HitLocation(){ Name = "Hit Location" } );
         Add( new Criticals(){ Name = "Criticals" } );
         Add( new HitResolution(){ Name = "Hit Resolution" } );
         Add( new AttackLog(){ Name = "Logger" } );
      }

      public override void GameStartsOnce () {
         Info( "Detected Mods: " + BattleMod.GetModList().Concat() );
      }

      private void SanitizeSettings ( ModSettings settings ) {
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
            settings.AttackLogArchiveMaxMB = settings.PersistentLog == false ? 4 : 64;

         settings.ShowUnderArmourDamage = settings.PaperDollDivulgeUnderskinDamage.GetValueOrDefault( settings.ShowUnderArmourDamage );
         settings.KillZeroHpLocation = settings.FixNonIntegerDamage.GetValueOrDefault( settings.KillZeroHpLocation );

         if ( settings.LOSWidthMultiplier != null && settings.LOSWidthMultiplier != 2 )
            settings.LOSWidth = settings.LOSWidthMultiplier.GetValueOrDefault( 2 );
         if ( settings.LOSWidthBlockedMultiplier != null && settings.LOSWidthBlockedMultiplier != 3 )
            settings.LOSWidthBlocked = settings.LOSWidthBlockedMultiplier.GetValueOrDefault( 3 ) * 0.75m;

         settings.ShowCorrectedHitChance = settings.ShowRealWeaponHitChance.GetValueOrDefault( settings.ShowCorrectedHitChance );
         if ( settings.ShowDecimalCalledChance == true && settings.CalledChanceFormat == "" )
            settings.CalledChanceFormat = "{0:0.0}%"; // Keep digits consistent
         // if ( old.ShowDecimalHitChance == true ); // Use new default either way, don't change
         if ( settings.LogHitRolls == true && ( settings.AttackLogLevel == null || settings.AttackLogLevel.Trim().ToLower() == "none" ) )
            settings.AttackLogLevel = "All";

         if ( settings.ThroughArmorCritChanceZeroArmor != null )
            settings.CritChanceZeroArmor = settings.ThroughArmorCritChanceZeroArmor.GetValueOrDefault( 0 );
         if ( settings.ThroughArmorCritChanceFullArmor != null )
            settings.CritChanceFullArmor = settings.ThroughArmorCritChanceFullArmor.GetValueOrDefault( 0 );
         if ( settings.ShowHeatAndStab == false )
            settings.ShowNumericInfo = false;
         if ( settings.SkipCritingDeadMech == false )
            settings.SkipBeatingDeadMech = "";
         if ( settings.MultupleCrits == false )
            settings.MultipleCrits = false;
#pragma warning restore CS0618

         RangeCheck( "LoadoutColourSaturation", ref settings.SaturationOfLoadout, 0, 1 );
         RangeCheck( "SpecialTerrainDotSize", ref settings.SpecialTerrainDotSize, 0, 10 );
         RangeCheck( "NormalTerrainDotSize", ref settings.NormalTerrainDotSize, 0, 10 );
         RangeCheck( "MovementPreviewRadius", ref settings.MovementPreviewRadius, 0, 32 );

         RangeCheck( "LOSWidth", ref settings.LOSWidth, 0, 10 );
         RangeCheck( "LOSWidthBlocked", ref settings.LOSWidthBlocked, 0, 10 );
         RangeCheck( "LOSMarkerBlockedMultiplier", ref settings.LOSMarkerBlockedMultiplier, 0, 10 );
         RangeCheck( "LOSHueDeviation", ref settings.LOSHueDeviation, 0, 0.5m );
         RangeCheck( "LOSHueHalfCycleMS", ref settings.LOSHueHalfCycleMS, 0, 300_000 );
         RangeCheck( "LOSBrightnessDeviation", ref settings.LOSBrightnessDeviation, 0, 0.5m );
         RangeCheck( "LOSBrightnessHalfCycleMS", ref settings.LOSBrightnessHalfCycleMS, 0, 300_000 );
         RangeCheck( "ArcLinePoints", ref settings.ArcLinePoints, 2, 127 );

         RangeCheck( "MechCalledShotMultiplier", ref settings.MechCalledShotMultiplier, 0, 1024 );
         RangeCheck( "VehicleCalledShotMultiplier", ref settings.VehicleCalledShotMultiplier, 0, 1024 );

         RangeCheck( "BaseHitChanceModifier" , ref settings.BaseHitChanceModifier, -10, 10 );
         RangeCheck( "MeleeHitChanceModifier", ref settings.MeleeHitChanceModifier, -10, 10 );
         RangeCheck( "ToHitMechFromFront", ref settings.ToHitMechFromFront, -20, 20 );
         RangeCheck( "ToHitMechFromSide" , ref settings.ToHitMechFromSide , -20, 20 );
         RangeCheck( "ToHitMechFromRear" , ref settings.ToHitMechFromRear , -20, 20 );
         RangeCheck( "ToHitVehicleFromFront", ref settings.ToHitVehicleFromFront, -20, 20 );
         RangeCheck( "ToHitVehicleFromSide" , ref settings.ToHitVehicleFromSide , -20, 20 );
         RangeCheck( "ToHitVehicleFromRear" , ref settings.ToHitVehicleFromRear , -20, 20 );
         RangeCheck( "ToHitSelfJumped", ref settings.ToHitSelfJumped, -20, 20 );

         RangeCheck( "HitChanceStep", ref settings.HitChanceStep, 0, 1 );
         RangeCheck( "MaxFinalHitChance", ref settings.MaxFinalHitChance, 0.1m, 1 );
         RangeCheck( "MinFinalHitChance", ref settings.MinFinalHitChance, 0, 1 );

         RangeCheck( "RollCorrectionStrength", ref settings.RollCorrectionStrength, 0, 0, 1.999m, 2 );
         RangeCheck( "MissStreakBreakerThreshold", ref settings.MissStreakBreakerThreshold, 0, 1 );
         RangeCheck( "MissStreakBreakerDivider"  , ref settings.MissStreakBreakerDivider, -100, 100 );

         RangeCheck( "CritChanceEnemy", ref settings.CritChanceEnemy, 0, 1000 );
         RangeCheck( "CritChanceAlly", ref settings.CritChanceAlly, 0, 1000 );
         RangeCheck( "CriChanceVsVehicle", ref settings.CriChanceVsVehicle, 0, 1000 );
         RangeCheck( "CritChanceVsTurret" , ref settings.CritChanceVsTurret, 0, 1000 );
         RangeCheck( "ThroughArmorCritThreshold", ref settings.ThroughArmorCritThreshold, -1, 10000 );
         RangeCheck( "CritChanceZeroArmor", ref settings.CritChanceZeroArmor, 0, 2 );
         RangeCheck( "CritChanceFullArmor", ref settings.CritChanceFullArmor, -1, settings.CritChanceZeroArmor );
         RangeCheck( "CritChanceZeroStructure", ref settings.CritChanceZeroStructure, 0, 2 );
         RangeCheck( "CritChanceFullStructure", ref settings.CritChanceFullStructure, -1, settings.CritChanceZeroStructure );
         RangeCheck( "CritChanceMin", ref settings.CritChanceMin, 0, 1 );
         RangeCheck( "CritChanceMax", ref settings.CritChanceMax, settings.CritChanceMin, 1 );

         // Is 1TB a reasonable limit of how many logs to keep?
         RangeCheck( "AttackLogArchiveMaxMB", ref settings.AttackLogArchiveMaxMB, 0, 1024*1024 );

         if ( settings.SettingVersion == null ) settings.SettingVersion = 0; // Pre-2.1 settings does not have SettingVersion
         if ( settings.SettingVersion < 2_001_000 ) { // Pre-2.1.0
            Info( "Upgrading settings to 2.1" );
            if ( settings.LOSMeleeColors == "" ) settings.LOSMeleeColors = "#F00,#0FF,#0FF,#0F8,#F00";
            if ( settings.LOSClearColors == "" ) settings.LOSClearColors = "#F00,#0FF,#0FF,#0F8,#F00";
            if ( settings.LOSIndirectColors == "" ) settings.LOSIndirectColors = "#F00,#0FF,#0FF,#0F8,#F00";
            string original = settings.MeleeAccuracyFactors.ToLower();
            if ( ! string.IsNullOrEmpty( original ) ) { // Update customised melee modifiers
               if ( original.Contains( "selfwalked" ) ) settings.MeleeAccuracyFactors = original.Replace( "selfwalked", "walked" );
               if ( ! original.Contains( "direction" ) ) settings.MeleeAccuracyFactors += ", Direction";
               if ( ! original.Contains( "jumped" ) ) settings.MeleeAccuracyFactors += ", Jumped";
               if ( ! original.Contains( "selfterrainmelee" ) ) settings.MeleeAccuracyFactors += ", SelfTerrainMelee";
            }
            if ( (float) settings.DiminishingBonusPowerBase == 0.8f ) settings.DiminishingBonusPowerBase = 0.8m;
            if ( (float) settings.DiminishingPenaltyPowerBase == 0.8f ) settings.DiminishingPenaltyPowerBase = 0.8m;
            if ( (float) settings.DiminishingPenaltyPowerDivisor == 3.3f) settings.DiminishingPenaltyPowerDivisor = 3.3m;
            // Add SelfTerrainMelee and spacing
            if ( settings.MeleeAccuracyFactors == "DFA,Height,Inspired,SelfChassis,SelfHeat,SelfStoodUp,SelfWalked,Sprint,TargetEffect,TargetEvasion,TargetProne,TargetShutdown,TargetSize,TargetTerrainMelee,WeaponAccuracy" )
               settings.MeleeAccuracyFactors = "Direction, DFA, Height, Inspired, Jumped, SelfChassis, SelfHeat, SelfStoodUp, SelfTerrainMelee, Sprint, TargetEffect, TargetEvasion, TargetProne, TargetShutdown, TargetSize, TargetTerrainMelee, Walked, WeaponAccuracy";
            else if ( settings.MeleeAccuracyFactors.ToLower().Contains( "selfwalked" ) )
               settings.MeleeAccuracyFactors = Regex.Replace( settings.MeleeAccuracyFactors, "SelfWalked", "Walked", RegexOptions.IgnoreCase );
            settings.AttackLogLevel = "All"; // Log is now enabled by default with new background logger
         }
         if ( settings.SettingVersion < 2_005_000 ) { // Reducing facing ring opacity
            if ( settings.FacingMarkerPlayerColors == "#FFFF,#CFCF,#CFCF,#AFAF,#FF8F" )
               settings.FacingMarkerPlayerColors = "#FFFA,#CFCA,#CFCA,#AFAC,#FF8A";
            if ( settings.FacingMarkerEnemyColors == "#FFFF,#FCCF,#FCCF,#FAAF,#FF8F" )
               settings.FacingMarkerEnemyColors = "#FFFA,#FCCA,#FCCA,#FAAC,#FF8A";
         }
         if ( settings.SettingVersion < 2_006_000 ) { // Update from 2.5 to 3.0 preview 20180925
            // Old hint was too long for elites and cause line wrapping
            if ( settings.ShortPilotHint == "HP:{1}/{2} ST:{3},{4},{5},{6}" )
               settings.ShortPilotHint = "G:{3} P:{4} G:{5} T:{6}";
            // Update floating armour colour to match nameplate text
            if ( settings.FloatingArmorColourPlayer == "cyan" && string.IsNullOrEmpty( settings.FloatingArmorColourEnemy ) && settings.FloatingArmorColourAlly == "teal" ) {
               settings.FloatingArmorColourPlayer = "#BFB";
               settings.FloatingArmorColourEnemy = "#FBB";
               settings.FloatingArmorColourAlly = "#8FF";
            }
         }
         if ( settings.SettingVersion < 2_007_000 ) { // Update from 3.0 preview 20180925
            if ( settings.ShowEnemyWounds == "{0}, Wounds {1}" )
               settings.ShowEnemyWounds = ", Wounds {1}";
            if ( settings.NameplateColourPlayer == "#BFB" )
               settings.NameplateColourPlayer = "#CFC";
            if ( settings.FloatingArmorColourPlayer == "#BFB" )
               settings.FloatingArmorColourPlayer = "#CFC";
            settings.SettingVersion = 2_007_000;
         }
      }

      private void MigrateColors ( string old, ref string now ) {
         if ( old == null ) return;
         if ( old == "" ) now = "";
         if ( string.IsNullOrEmpty( now ) ) return;
         int pos = now.IndexOf( ',' );
         if ( pos < 0 ) return;
         now = old + now.Substring( pos );
      }

      /* Changes that we don't want to write back to settings.json */
      private void NormaliseSettings () {
         NullIfEmpty( ref Settings.ShortPilotHint );
         NullIfEmpty( ref Settings.WeaponRangeFormat );
         NullIfEmpty( ref Settings.ShowEnemyWounds );
         NullIfEmpty( ref Settings.ShowAllyHealth );
         NullIfEmpty( ref Settings.ShowPlayerHealth );
         NullIfEmpty( ref Settings.ShowAlphaDamageInLoadout );

         NullIfEmpty( ref Settings.NameplateColourPlayer );
         NullIfEmpty( ref Settings.NameplateColourEnemy );
         NullIfEmpty( ref Settings.NameplateColourAlly );
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

         NullIfEmpty( ref Settings.RangedAccuracyFactors );
         NullIfEmpty( ref Settings.MeleeAccuracyFactors );
         //NullIfEmpty( ref Settings.MixingIndirectFire ); Never null after validation

         NullIfEmpty( ref Settings.MaxMeleeVerticalOffsetByClass );

         NullIfEmpty( ref Settings.SkipBeatingDeadMech );

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

      public static UnityEngine.Color?[] ParseColours ( params string[] htmlColours ) {
         UnityEngine.Color?[] result = new UnityEngine.Color?[ htmlColours.Length ];
         for ( int i = 0, len = result.Length ; i < len ; i++ )
            result[ i ] = ParseColour( htmlColours[ i ] );
         return AllNull( result ) ? null : result;
      }

      public static bool IsCalloutPressed  { get => BattleTech.BTInput.Instance.Combat_ToggleCallouts().IsPressed; }

      public static bool HasMod ( params string[] mods ) { try {
         return BattleMod.FoundMod( mods );
      } catch ( Exception ex ) {
         Error( ex );
         return false;
      } }

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