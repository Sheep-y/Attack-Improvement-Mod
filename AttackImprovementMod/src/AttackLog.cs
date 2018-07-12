using System;

namespace Sheepy.AttackImprovementMod {
   using static Mod;
   using static System.Reflection.BindingFlags;
   using System.IO;
   using BattleTech;
   using System.Collections.Generic;
   using System.Text;
   using System.Reflection;

   public class AttackLog : ModModule {

      internal const string ROLL_LOG = "Log_AttackRoll.txt";

      private static bool LogLocation = false;
      private static bool LogCritical = false;

      public override void InitPatch () {
         switch ( Mod.Settings.AttackLog.ToLower() ) {
            case "all":
            case "critical":
               LogCritical = true;
               Type MechType = typeof( Mech );
               Type CritRulesType = typeof( CritChanceRules );
               Patch( typeof( AttackDirector ), "GetRandomFromCache", new Type[]{ typeof( WeaponHitInfo ), typeof( int ) }, null, "RecordCritRolls" );
               Patch( CritRulesType, "GetBaseCritChance", new Type[]{ typeof( Vehicle ), typeof( VehicleChassisLocations ) }, null, "RecordBaseCritChance" );
               Patch( CritRulesType, "GetBaseCritChance", new Type[]{ MechType, typeof( ChassisLocations ), typeof( bool ) }, null, "RecordBaseCritChance" );
               Patch( CritRulesType, "GetCritMultiplier", null, "RecordCritMultiplier" );
               Patch( CritRulesType, "GetCritChance", null, "RecordCritChance" );
               Patch( MechType, "GetComponentInSlot", null, "RecordCritComp" );
               Patch( MechType, "CheckForCrit", NonPublic, "LogCritComp", "LogCrit" );
               goto case "location";

            case "location":
               LogLocation = true;
               Patch( GetHitLocation( typeof( ArmorLocation ) ), null, "LogMechHit" );
               Patch( GetHitLocation( typeof( VehicleChassisLocations ) ), null, "LogVehicleHit" );
               goto case "attack";

            case "attack":
               Type AttackType = typeof( AttackDirector.AttackSequence );
               Patch( AttackType, "GetIndividualHits", NonPublic, "RecordAttacker", null );
               Patch( AttackType, "GetClusteredHits" , NonPublic, "RecordAttacker", null );
               Patch( AttackType, "GetCorrectedRoll" , NonPublic, "RecordAttackRoll", "LogMissedAttack" );
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
         bool PersistentLog = Mod.Settings.PersistentLog;
         if ( ! PersistentLog ) DeleteLog( ROLL_LOG );

         if ( ! File.Exists( LogDir + ROLL_LOG ) ) {
            StringBuilder logBuffer = new StringBuilder();
            logBuffer.Append( String.Join( "\t", new string[]{ "Team", "Attacker", "Weapon", "Hit Roll", "Corrected", "Streak", "Final", "Hit%" } ) );
            if ( LogLocation || PersistentLog )
               logBuffer.Append( String.Join( "\t", new string[]{ "Location Roll", "Head/Turret", "CT/Front", "LT/Left", "RT/Right", "LA/Rear", "RA", "LL", "RL", "LL", "Called Part", "Called Bonus" } ) );
            logBuffer.Append( "\tHit Location" );
            if ( LogCritical || PersistentLog )
               logBuffer.Append( String.Join( "\t", new string[]{ "Crit Roll", "HP", "Max HP", "Base Crit%", "Crit Multiplier", "Crit%", "Slot Roll", "Slot", "In Slot", "From", "To" } ) );
            log.Add( logBuffer.ToString() );
            WriteRollLog();
         }

         if ( LogCritical )
            hitMap = new Dictionary<string, int>( 16 );
      }

      // ============ UTILS ============

      private static Dictionary<string, int> hitMap; // Used to assign critical hit information
      private static List<string> log = new List<string>( 64 );

      public static void WriteRollLog () {
         StringBuilder logBuffer = new StringBuilder();
         foreach ( string line in log )
            logBuffer.Append( line ).Append( "\r\n" );
         WriteLog( ROLL_LOG, logBuffer.ToString() );
         log.Clear();
         hitMap?.Clear();
      }

      internal static MethodInfo GetHitLocation ( Type generic ) {
         return typeof( HitLocation ).GetMethod( "GetHitLocation", Public | Static ).MakeGenericMethod( generic );
      }

      // ============ Attack Log ============

      // Get attacker, weapon and hitchance before logging
      internal static string thisAttack = "";
      internal static string thisWeapon = "";
      internal static float thisHitChance;
      public static void RecordAttacker ( AttackDirector.AttackSequence __instance, Weapon weapon, float toHitChance ) {
         thisHitChance = toHitChance;
         thisWeapon = weapon.GUID;
         AbstractActor attacker = __instance.attacker;
         Team team = attacker?.team;
         if ( team == null ) {
            thisAttack = "--\t--\t--";
            return;
         }
         if ( team.IsLocalPlayer )
            thisAttack = "Player";
         else if ( team.IsEnemy( Combat.LocalPlayerTeam ) )
            thisAttack = "OpFor";
         else if ( team.IsEnemy( Combat.LocalPlayerTeam ) )
            thisAttack = "Allies";
         else
            thisAttack = "NPC";
         thisAttack += "\t" + ( attacker.GetPilot()?.Callsign ?? attacker.Nickname );
         thisAttack += "\t" + ( weapon.defId.StartsWith( "Weapon_" ) ? weapon.defId.Substring( 7 ) : weapon.defId );
      }

      internal static float thisRoll;
      internal static float thisStreak;
      public static void RecordAttackRoll ( float roll, Team team ) {
         thisRoll = roll;
         thisStreak = team?.StreakBreakingValue ?? 0;
      }

      internal static string GetHitLog () {
         return thisAttack + "\t" + thisRoll + "\t" + ( thisCorrectedRoll + thisStreak ) + "\t" + thisStreak + "\t" + thisCorrectedRoll + "\t" + thisHitChance + "\t";
      }

      internal static float thisCorrectedRoll;
      public static void LogMissedAttack ( float __result, float roll, Team team ) {
         thisCorrectedRoll = __result;
         bool miss = __result > thisHitChance;
         if ( miss || ! LogLocation ) { // If miss, log now because hit location won't be rolled
            StringBuilder logBuffer = new StringBuilder();
            logBuffer.Append( GetHitLog() );
            if ( LogLocation ) {
               logBuffer.Append( "--" + // Location Roll
                                 "\t--\t--\t--\t--" +  // Head & Torsos
                                 "\t--\t--\t--\t--" + // Limbs
                                 "\t--\t--\tMiss" );   // Called shot and result
               if ( LogCritical )
                  logBuffer.Append( CritDummy );
            } else {
               logBuffer.Append( miss ? "Miss" : "Hit" );
            }
            log.Add( logBuffer.ToString() );
         }
      }

      // ============ Location Log ============

      public static void LogMechHit ( ArmorLocation __result, Dictionary<ArmorLocation, int> hitTable, float randomRoll, ArmorLocation bonusLocation, float bonusLocationMultiplier ) { try {
         string line = 
            GetHitLog() +
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
            __result;
         SetupCritLog( line, __result.ToString() );
      }                 catch ( Exception ex ) { Error( ex ); } }

      public static void LogVehicleHit ( VehicleChassisLocations __result, Dictionary<VehicleChassisLocations, int> hitTable, float randomRoll, VehicleChassisLocations bonusLocation, float bonusLocationMultiplier ) { try {
         string line = 
            GetHitLog() +
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
            __result;
         SetupCritLog( line, __result.ToString() );
      }                 catch ( Exception ex ) { Error( ex ); } }

      public static void SetupCritLog ( string line, string location ) {
         if ( hitMap != null ) {
            hitMap[ thisWeapon + "@" + location ] = log.Count;
            line += CritDummy;
         }
         log.Add( line );
      }

      // ============ Crit Log ============

      private const string CritDummy = "\t--" +             // Crit Roll
                                       "\t--\t--" +         // Location HP
                                       "\t--\t--\t--" +     // Crit Chances
                                       "\t--\t--\t--" +     // Slot info
                                       "\t--\t--";          // Crit Result

      private static float thisCritRoll, thisCritSlotRoll;
      public static void RecordCritRolls ( float[] __result, int amount ) {
         if ( amount == 2 ) {
            thisCritRoll = __result[0];
            thisCritSlotRoll = __result[0];
         }
      }

      private static float thisBaseCritChance, thisCritMultiplier, thisCritChance;
      public static void RecordBaseCritChance ( float __result ) {
         thisBaseCritChance = __result;
      }
      public static void RecordCritMultiplier ( float __result ) {
         thisCritMultiplier = __result;
      }
      public static void RecordCritChance ( float __result ) {
         thisCritChance = __result;
      }

      private static MechComponent thisCritComp = null;
      private static string thisCritSlot = "";
      private static bool ammoExploded = false;

      public static void RecordCritComp( MechComponent __result, ChassisLocations location, int index ) {
         if ( thisCritComp == null ) {
            thisCritComp = __result;
            thisCritSlot = index + "\t";
            if ( __result != null ) {
               AmmunitionBox box = __result as AmmunitionBox;
               if ( box != null )
                  ammoExploded = ( (float) box.CurrentAmmo / (float) box.ammunitionBoxDef.Capacity ) >= 0.5f;
               thisCritSlot +=  __result.Name + "\t" + __result.DamageLevel;
            } else
               thisCritSlot +=  "--\t--";
         }
      }

      public static void LogCritComp ( ChassisLocations location, Weapon weapon ) {
         thisCritComp = null;
         thisCritSlot = "";
         ammoExploded = false;
      }

      public static void LogCrit( ChassisLocations location, Weapon weapon ) {
         Log( "{0}\t{1}\t{2}\t{3}", weapon.Name, location, thisCritSlot, ammoExploded ? "Explosion" : thisCritComp?.DamageLevel.ToString() );
      }
   }
}