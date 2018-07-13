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
      private static bool PersistentLog = false;

      public override void InitPatch () {
         PersistentLog = Mod.Settings.PersistentLog;
         switch ( Mod.Settings.AttackLogLevel?.Trim().ToLower() ) {
            case "all":
            case "critical":
               LogCritical = true;
               Type MechType = typeof( Mech );
               Type CritRulesType = typeof( CritChanceRules );
               Patch( typeof( AttackDirector ), "GetRandomFromCache", new Type[]{ typeof( WeaponHitInfo ), typeof( int ) }, null, "RecordCritRolls" );
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
               Patch( typeof( AttackDirector ), "OnAttackComplete", null, "WriteRollLog" );
               initLog();
               break;

            default:
               Warn( "Unknown AttackLogLevel " + Mod.Settings.AttackLogLevel );
               goto case "none";
            case null:
            case "none":
               break;
         }
      }

      public static void initLog () {
         if ( ! PersistentLog ) DeleteLog( ROLL_LOG );

         if ( ! File.Exists( LogDir + ROLL_LOG ) ) {
            StringBuilder logBuffer = new StringBuilder();
            logBuffer.Append( String.Join( "\t", new string[]{ "Team", "Attacker", "Weapon", "Hit Roll", "Corrected", "Streak", "Final", "Hit%" } ) );
            if ( LogLocation || PersistentLog )
               logBuffer.Append( "\t" ).Append( String.Join( "\t", new string[]{ "Location Roll", "Head/Turret", "CT/Front", "LT/Left", "RT/Right", "LA/Rear", "RA", "LL", "RL", "Called Part", "Called Multiplier" } ) );
            logBuffer.Append( "\tHit Location" );
            if ( LogCritical || PersistentLog )
               logBuffer.Append( "\t" ).Append( String.Join( "\t", new string[]{ "HP", "Max HP", "Crit Roll", "Base Crit%", "Crit Multiplier", "Crit%", "Slot Roll", "Crit Slot", "Crit Equipment", "From State", "To State" } ) );
            log.Add( logBuffer.ToString() );
            WriteRollLog( null );
         }

         if ( LogCritical )
            hitMap = new Dictionary<string, int>( 16 );
      }

      // ============ UTILS ============

      private static Dictionary<string, int> hitMap; // Used to assign critical hit information
      private static List<string> log = new List<string>( 64 );

      public static void WriteRollLog ( AttackDirector __instance ) {
         if ( __instance != null && __instance.IsAnyAttackSequenceActive )
            return; // Defer if Multi-Target is not finished
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

      public static string TeamName ( Team team ) {
         if ( team == null ) 
            return "null";
         if ( team.IsLocalPlayer )
            return "Player";
         if ( team.IsEnemy( Combat.LocalPlayerTeam ) )
            return "OpFor";
         if ( team.IsEnemy( Combat.LocalPlayerTeam ) )
            return "Allies";
         return "NPC";
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
         thisAttack = TeamName( team );
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
            if ( LogLocation || PersistentLog ) {
               logBuffer.Append( "--" + // Location Roll
                                 "\t--\t--\t--\t--" +  // Head & Torsos
                                 "\t--\t--\t--\t--" + // Limbs
                                 "\t--\t--\t(Miss)" );   // Called shot and result
               if ( LogCritical || PersistentLog )
                  logBuffer.Append( CritDummy );
            } else
               logBuffer.Append( miss ? "(Miss)" : "(Hit)" );
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
         if ( LogCritical ) {
            string key = thisWeapon + "@" + MechStructureRules.GetChassisLocationFromArmorLocation( __result );
            hitMap[ key ] = log.Count;
         }
         if ( LogCritical || PersistentLog )
            line += CritDummy;
         log.Add( line );
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
         if ( LogCritical || PersistentLog )
            line += CritDummy;
         log.Add( line );
      }                 catch ( Exception ex ) { Error( ex ); } }

      // ============ Crit Log ============

      private const string CritDummy = "\t--\t--" +         // Location HP
                                       "\t--\t--\t--\t--" + // Crit Roll and %
                                       "\t--\t--\t--" +     // Slot info
                                       "\t--\t--";          // Crit Result

      private static float thisCritRoll, thisCritSlotRoll;
      public static void RecordCritRolls ( float[] __result, int amount ) {
         if ( amount == 2 ) {
            thisCritRoll = __result[0];
            thisCritSlotRoll = __result[1];
         }
      }

      private static float thisBaseCritChance, thisCritMultiplier, thisCritChance, thisLocationHP, thisLocationMaxHP;
      public static void RecordBaseCritChance ( float __result, Mech target, ChassisLocations hitLocation ) {
         thisBaseCritChance = __result;
         thisLocationHP = target.GetCurrentStructure( hitLocation );
         thisLocationMaxHP = target.GetMaxStructure( hitLocation );
      }
      public static void RecordCritMultiplier ( float __result ) {
         thisCritMultiplier = __result;
      }
      public static void RecordCritChance ( float __result ) {
         thisCritChance = __result;
      }

      private static int thisCritSlot;
      private static MechComponent thisCritComp = null;
      private static ComponentDamageLevel thisCompBefore;
      private static bool halfFullAmmo = false;

      public static void RecordCritComp ( MechComponent __result, ChassisLocations location, int index ) {
         if ( thisCritComp == null ) {
            thisCritSlot = index;
            thisCritComp = __result;
            if ( __result != null ) {
               if ( __result is AmmunitionBox box )
                  halfFullAmmo = ( (float)box.CurrentAmmo / (float)box.ammunitionBoxDef.Capacity ) >= 0.5f;
               thisCompBefore = __result.DamageLevel;
            }
         }
      }

      public static void LogCritComp ( ChassisLocations location, Weapon weapon ) {
         thisCritSlot = -1;
         thisCritComp = null;
         halfFullAmmo = false;
      }

      public static void LogCrit ( ChassisLocations location, Weapon weapon ) {
         string key = weapon.GUID + "@" + location;
         if ( ( ! hitMap.TryGetValue( key, out int lineIndex ) ) || lineIndex >= log.Count ) {
            Warn( "Critical Hit Log cannot find matching hit record: " + key );
            return;
         }
         string line = log[ lineIndex ];
         if ( ! line.EndsWith( "\t--" ) )
            Warn( "Critical Hit Log found duplicate crit: " + key );
         string critLine = 
               thisLocationHP + "\t" +
               thisLocationMaxHP + "\t" +
               thisCritRoll + "\t" +
               thisBaseCritChance + "\t" +
               thisCritMultiplier + "\t" +
               thisCritChance + "\t";
         if ( thisCritSlot < 0 )
            critLine += "--\t--\t(No Crit)\t--\t--";
         else {
            critLine += 
               thisCritSlotRoll + "\t" + 
               ( thisCritSlot + 1 ) + "\t";
            if ( thisCritComp == null )
               critLine += "(Empty)\t--\t--";
            else {
               string thisCompAfter = thisCritComp is AmmunitionBox && halfFullAmmo ? "Explosion" : thisCritComp.DamageLevel.ToString();
               critLine +=
                  thisCritComp.Name + "\t" +
                  thisCompBefore + "\t" +
                  thisCompAfter;
            }
         }
         line = line.Substring( 0, line.Length - CritDummy.Length + 1 ) + critLine;
         log[ lineIndex ] = line;
      }
   }
}