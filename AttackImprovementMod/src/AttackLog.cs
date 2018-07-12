using System;

namespace Sheepy.AttackImprovementMod {
   using static Mod;
   using static FixHitLocation;
   using static System.Reflection.BindingFlags;
   using System.IO;
   using BattleTech;
   using System.Collections.Generic;
   using System.Text;

   public class AttackLog : ModModule {

      internal const string ROLL_LOG = "Log_AttackRoll.txt";

      private static bool LogLocation = false;
      private static bool LogCritical = false;

      public override void InitPatch () {
         switch ( Mod.Settings.AttackLog.ToLower() ) {
            case "all":
            case "critical":
               //LogCritical = true;
               Patch( typeof( Mech ), "GetComponentInSlot", null, "RecordCritComp" );
               Patch( typeof( Mech ), "CheckForCrit", NonPublic, "LogCritComp", "LogCrit" );
               goto case "attack";

            case "location":
               LogLocation = true;
               Patch( GetHitLocation( typeof( ArmorLocation ) ), null, "LogMechHit" );
               Patch( GetHitLocation( typeof( VehicleChassisLocations ) ), null, "LogVehicleHit" );
               goto case "attack";

            case "attack":
               Type AttackType = typeof( AttackDirector.AttackSequence );
               Patch( AttackType, "GetIndividualHits", NonPublic, "RecordAttacker", null );
               Patch( AttackType, "GetClusteredHits" , NonPublic, "RecordAttacker", null );
               Patch( AttackType, "GetCorrectedRoll" , NonPublic, new Type[]{ typeof( float ), typeof( Team ) }, "RecordAttackRoll", "LogMissedAttack" );
               Patch( AttackType, "OnAttackSequenceFire", null, "WriteRollLog" );
               initLog();
               break;

            default:
               Warn( "Unknown AttackLog level " + Mod.Settings.AttackLog );
               goto case "none";
            case "none":
               break;
         }
      }

      public static void initLog () {
         if ( ! Mod.Settings.PersistentLog ) DeleteLog( ROLL_LOG );
         if ( ! File.Exists( LogDir + ROLL_LOG ) ) {
            logBuffer.Append( String.Join( "\t", new string[]{ "Attacker", "Weapon", "Hit Roll", "Corrected", "Streak", "Final", "To Hit" } ) );
            if ( LogLocation )
               logBuffer.Append( String.Join( "\t", new string[]{ "Location Roll", "Head/Turret", "CT/Front", "LT/Left", "RT/Right", "LA/Rear", "RA", "LL", "RL", "LL", "Called Part", "Called Bonus" } ) );
            logBuffer.Append( "\tHit Location" );
            if ( LogCritical )
               logBuffer.Append( String.Join( "\t", new string[]{ "Crit Roll", "HP", "Max HP", "Base Crit Chance", "Crit Chance", "Slot Roll", "Slot", "In Slot", "From", "To" } ) );
            logBuffer.Append( "\r\n" );
         }

      }

      private static MechComponent thisCritComp = null;
      private static string thisCritLog = "";
      private static bool ammoExploded = false;

      // ============ UTILS ============

      private static StringBuilder logBuffer = new StringBuilder();

      public static void WriteRollLog () {
         WriteLog( ROLL_LOG, logBuffer.ToString() );
         logBuffer.Length = 0;
      }

      // ============ Attack Log ============

      // Get attacker, weapon and hitchance before logging
      internal static string thisAttacker = "(unknown)";
      internal static string thisWeapon = "(unknown)";
      internal static float thisHitChance;
      public static void RecordAttacker ( AttackDirector.AttackSequence __instance, Weapon weapon, float toHitChance ) {
         thisAttacker = __instance.attacker.GetPilot()?.Callsign ?? __instance.attacker.Nickname;
         thisWeapon = weapon.defId.StartsWith( "Weapon_" ) ? weapon.defId.Substring( 7 ) : weapon.defId;
         thisHitChance = toHitChance;
      }

      internal static float thisRoll;
      internal static float thisStreak;
      public static void RecordAttackRoll ( float roll, Team team ) {
         thisRoll = roll;
         thisStreak = team?.StreakBreakingValue ?? 0;
      }

      internal static string GetHitLog () {
         return thisAttacker + "\t" + thisWeapon + "\t" + thisRoll + "\t" + ( thisCorrectedRoll + thisStreak ) + "\t" + thisStreak + "\t" + thisCorrectedRoll + "\t" + thisHitChance + "\t";
      }

      internal static float thisCorrectedRoll;
      public static void LogMissedAttack ( float __result, float roll, Team team ) {
         thisCorrectedRoll = __result;
         bool miss = __result > thisHitChance;
         if ( miss || ! LogLocation ) { // If miss, log now because hit location won't be rolled
            logBuffer.Append( GetHitLog() );
            if ( LogLocation ) {
               logBuffer.Append( "--" + // Location Roll
                                 "\t--\t--\t--\t--" +  // Head & Torsos
                                 "\t--\t--\t--\t--" + // Limbs
                                 "\t--\t--\tMiss" );   // Called shot and result
               if ( LogCritical )
                  logBuffer.Append( "\t--" + // Crit Roll
                                    "\t--\t--\t--\t--" +  // Location HP & Chances
                                    "\t--\t--\t--" +     // Slot info
                                    "\t--\t--" );       // Result
            } else {
               logBuffer.Append( miss ? "Miss" : "Hit" );
            }
            logBuffer.Append( "\r\n" );
         }
      }

      // ============ Location Log ============

      public static void LogMechHit ( ArmorLocation __result, Dictionary<ArmorLocation, int> hitTable, float randomRoll, ArmorLocation bonusLocation, float bonusLocationMultiplier ) { try {
         logBuffer.Append( GetHitLog() ).Append(
            randomRoll + "\t" +
            TryGet( hitTable, ArmorLocation.Head ) + "\t" +
            ( TryGet( hitTable, ArmorLocation.CenterTorso ) + TryGet( hitTable, ArmorLocation.CenterTorsoRear ) ) + "\t" +
            ( TryGet( hitTable, ArmorLocation.LeftTorso   ) + TryGet( hitTable, ArmorLocation.LeftTorsoRear   ) ) + "\t" +
            ( TryGet( hitTable, ArmorLocation.RightTorso  ) + TryGet( hitTable, ArmorLocation.RightTorsoRear  ) ) + "\t" +
            TryGet( hitTable, ArmorLocation.LeftArm  ) + "\t" +
            TryGet( hitTable, ArmorLocation.RightArm ) + "\t" +
            TryGet( hitTable, ArmorLocation.LeftLeg  ) + "\t" +
            TryGet( hitTable, ArmorLocation.RightLeg ) + "\t" +
            bonusLocation + "\t" +
            bonusLocationMultiplier + "\t" +
            __result ).Append( "\r\n" );
      }                 catch ( Exception ex ) { Error( ex ); } }

      public static void LogVehicleHit ( VehicleChassisLocations __result, Dictionary<VehicleChassisLocations, int> hitTable, float randomRoll, VehicleChassisLocations bonusLocation, float bonusLocationMultiplier ) { try {
         logBuffer.Append( GetHitLog() ).Append(
            GetHitLog() + "\t" +
            randomRoll + "\t" +
            TryGet( hitTable, VehicleChassisLocations.Turret ) + "\t" +
            TryGet( hitTable, VehicleChassisLocations.Front  ) + "\t" +
            TryGet( hitTable, VehicleChassisLocations.Left   ) + "\t" +
            TryGet( hitTable, VehicleChassisLocations.Right  ) + "\t" +
            TryGet( hitTable, VehicleChassisLocations.Rear   ) + "\t" +
            "--\t" +
            "--\t" +
            "--\t" +
            bonusLocation + "\t" +
            bonusLocationMultiplier + "\t" +
            __result ).Append( "\r\n" );
      }                 catch ( Exception ex ) { Error( ex ); } }

      // ============ Crit Log ============

      public static void RecordCritComp( MechComponent __result, ChassisLocations location, int index ) {
         if ( thisCritComp == null ) {
            thisCritComp = __result;
            thisCritLog = index + "\t";
            if ( __result != null ) {
               AmmunitionBox box = __result as AmmunitionBox;
               if ( box != null )
                  ammoExploded = ( (float) box.CurrentAmmo / (float) box.ammunitionBoxDef.Capacity ) >= 0.5f;
               thisCritLog +=  __result.Name + "\t" + __result.DamageLevel;
            } else
               thisCritLog +=  "--\t--";
         }
      }

      public static void LogCritComp ( ChassisLocations location, Weapon weapon ) {
         thisCritComp = null;
         thisCritLog = "";
         ammoExploded = false;
      }

      public static void LogCrit( ChassisLocations location, Weapon weapon ) {
         Log( "{0}\t{1}\t{2}\t{3}", weapon.Name, location, thisCritLog, ammoExploded ? "Explosion" : thisCritComp?.DamageLevel.ToString() );
      }
   }
}