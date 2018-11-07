using BattleTech;
using Harmony;
using Sheepy.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Reflection;
using System.Text;

namespace Sheepy.BattleTechMod.AttackImprovementMod {
   using static Mod;
   using static System.Reflection.BindingFlags;

   public class AttackLog : BattleModModule {

      private const bool DebugLog = false;

      internal static Logger ROLL_LOG;

      private static bool LogShot, LogLocation, LogDamage, LogCritical;
      private static string Separator = ",";

      private static readonly Type AttackType = typeof( AttackDirector.AttackSequence );
      private static readonly Type ArtilleyAttackType = typeof( ArtillerySequence );

      private static string thisCombatId = "";

#pragma warning disable CS0162 // Disable "unreachable code" warnings due to DebugLog flag
      public override void CombatStartsOnce () {
         if ( Settings.AttackLogLevel == null ) return;

         Type MechType = typeof( Mech );
         Type VehiType = typeof( Vehicle );
         Type TurtType = typeof( Turret );
         Type BuldType = typeof( BattleTech.Building );

         switch ( Settings.AttackLogFormat.Trim().ToLower() ) {
            default:
               Warn( "Unknown AttackLogFormat " + Settings.AttackLogFormat );
               Settings.AttackLogFormat = "csv";
               goto case "csv";
            case "csv":
               Separator = ",";
               break;
            case "tsv":
            case "txt":
               Separator = "\t";
               break;
         }

         // Patch prefix early to increase chance of successful capture in face of other mods.
         switch ( Settings.AttackLogLevel.Trim().ToLower() ) {
            case "all":
            case "critical":
               LogCritical = true;
               CritDummy = FillBlanks( 10 );
               Type CritRulesType = typeof( CritChanceRules );
               Patch( typeof( AttackDirector ), "GetRandomFromCache", new Type[]{ typeof( WeaponHitInfo ), typeof( int ) }, null, "LogCritRolls" );
               Patch( CritRulesType, "GetBaseCritChance", new Type[]{ MechType, typeof( ChassisLocations ), typeof( bool ) }, null, "LogBaseCritChance" );
               Patch( CritRulesType, "GetCritMultiplier", null, "LogCritMultiplier" );
               Patch( CritRulesType, "GetCritChance", null, "LogCritChance" );
               Patch( MechType, "GetComponentInSlot", null, "LogCritComp" );
               Patch( typeof( AttackDirector.AttackSequence ), "FlagAttackCausedAmmoExplosion", null, "LogAmmoExplosionFlag" );
               Patch( typeof( Pilot ), "SetNeedsInjury", null, "LogAmmoExplosionOnPilot" );
               Patch( MechType, "CheckForCrit", null, "LogCritResult" );
               goto case "damage";

            case "damage":
               DamageDummy = FillBlanks( 6 );
               Patch( MechType, "DamageLocation", "RecordMechDamage", "LogMechDamage" );
               Patch( VehiType, "DamageLocation", "RecordVehicleDamage", "LogVehicleDamage" );
               Patch( TurtType, "DamageLocation", "RecordTurretDamage", "LogTurretDamage" );
               Patch( BuldType, "DamageBuilding", "RecordBuildingDamage", "LogBuildingDamage" );
               LogDamage = true;
               goto case "location";

            case "location":
               LogLocation = true;
               Patch( GetHitLocation( typeof( ArmorLocation ) ), null, "LogMechHit" );
               Patch( GetHitLocation( typeof( VehicleChassisLocations ) ), null, "LogVehicleHit" );
               Patch( TurtType, "GetHitLocation", new Type[]{ typeof( AbstractActor ), typeof( UnityEngine.Vector3 ), typeof( float ), typeof( int ), typeof( float ) }, null, "LogBuildingHit" );
               Patch( BuldType, "GetHitLocation", new Type[]{ typeof( AbstractActor ), typeof( UnityEngine.Vector3 ), typeof( float ), typeof( int ), typeof( float ) }, null, "LogBuildingHit" );
               Patch( TurtType, "GetAdjacentHitLocation", null, "LogBuildingClusterHit" );
               Patch( BuldType, "GetAdjacentHitLocation", null, "LogBuildingClusterHit" );
               goto case "shot";

            case "shot":
               LogShot = true;
               Patch( AttackType, "GetIndividualHits", "RecordSequenceWeapon", null );
               Patch( AttackType, "GetClusteredHits" , "RecordSequenceWeapon", null );
               Patch( AttackType, "GetCorrectedRoll" , "RecordAttackRoll", "LogMissedAttack" );
               Patch( MechType, "CheckForHeatDamage" , "RecordOverheatCheck", "WriteSpecialLog" );
               Patch( MechType, "ApplyHeatDamage"    , "RecordOverheat", "LogOverheat" );
               goto case "attack";

            case "attack":
               Patch( ArtilleyAttackType, "PerformAttack", "RecordArtilleryAttack", null );
               Patch( AttackType, "GenerateToHitInfo", "RecordAttack", "LogSelfAttack" );
               Patch( typeof( AttackDirector ), "OnAttackComplete", null, "WriteRollLog" );
               TryRun( ModLog, InitLog );
               break;

            default:
               Warn( "Unknown AttackLogLevel " + Settings.AttackLogLevel );
               goto case "none";
            case null:
            case "none":
               break;
         }
      }

      public override void CombatEnds () {
         TryRun( ModLog, ForceWriteLog );
      }

      public static void CreateLogger ( string filename, bool time = false ) {
         if ( time ) filename += "." + TimeToFilename( DateTime.UtcNow );
         string file = filename + "." + Settings.AttackLogFormat;
         if ( ! time && File.Exists( file ) ) try {
            using ( File.Open( file, FileMode.Open ) ){}
         } catch ( IOException ) {
            Warn( "Cannot open " + file );
            CreateLogger( filename, true );
            return;
         }
         Info( "Init attack logging to " + file );
         ROLL_LOG = new Logger( file ){ LevelText = null, TimeFormat = null };
         ROLL_LOG.OnError = ( ex ) => Error( ex ); // Write attack log errors to mod log.
      }

      public static void InitLog () {
         CreateLogger( ModLogDir + "Log_Attack" );
         idGenerator = new Random();
         thisSequenceId = GetNewId();
         ArchiveOldAttackLog();

         if ( ! ROLL_LOG.Exists() ) {
            StringBuilder logBuffer = new StringBuilder();
            logBuffer.Append( string.Join( Separator, new string[]{ "Log v2.1", "Actor", "Pilot", "Unit", "Target", "Pilot", "Unit", "Combat Id", "Round", "Phase", "Attack Id", "Direction", "Range" } ) );
            // LogShot
            logBuffer.Append( Separator ).Append( string.Join( Separator, new string[]{ "Weapon", "Weapon Template", "Weapon Id", "Hit Roll", "Corrected", "Streak", "Final", "Hit%" } ) );
            // LogLocation
            logBuffer.Append( Separator ).Append( string.Join( Separator, new string[]{ "Location Roll", "Head", "CT", "LT", "RT", "LA", "RA", "LL", "RL", "Called Part", "Called Multiplier" } ) );
            // LogShot (cont.)
            logBuffer.Append( Separator ).Append( "Hit Location" );
            // LogDamage
            logBuffer.Append( Separator ).Append( string.Join( Separator, new string[]{ "Damage", "Stops At", "From Armor", "To Armor", "From HP", "To HP" } ) );
            // LogCritical
            logBuffer.Append( Separator ).Append( string.Join( Separator, new string[]{ "Max HP", "Crit Roll", "Base Crit%", "Crit Multiplier", "Crit%", "Slot Roll", "Crit Slot", "Crit Equipment", "From State", "To State" } ) );
            log.Add( logBuffer.ToString() );
            WriteRollLog();
         }

         if ( LogDamage )
            hitList = new List<int>( 16 );
         if ( LogCritical )
            hitMap = new Dictionary<string, int>( 16 );
      }

      public override void CombatStarts () {
         if ( idGenerator == null ) return;
         thisCombatId = GetNewId();
      }

      // ============ UTILS ============

      private static List<int> hitList; // Used to assign damage information
      private static Dictionary<string, int> hitMap; // Used to assign critical hit information
      private static List<string> log = new List<string>( 32 );

      public static void ForceWriteLog () {
         if ( log.Count <= 0 ) return;
         if ( DebugLog ) Verbo( "Force write {0} lines of log.\n", log.Count );
         ROLL_LOG.Info( string.Join( Environment.NewLine, log.ToArray() ) );
         log.Clear();
         ROLL_LOG.Flush();
      }

      [ HarmonyPriority( Priority.VeryLow ) ]
      public static void WriteRollLog () {
         if ( Combat?.AttackDirector?.IsAnyAttackSequenceActive ?? true )
            return; // Defer if Multi-Target is not finished. Defer when in doubt.
         ForceWriteLog();
         hitList?.Clear();
         hitMap?.Clear();
         thisSequenceId = GetNewId();
         if ( DebugLog ) Verbo( "HitMap Cleared" );
      }

      [ HarmonyPriority( Priority.VeryLow ) ]
      public static void WriteSpecialLog () {
         if ( log.Count <= 0 ) return;
         if ( Combat?.AttackDirector?.IsAnyAttackSequenceActive ?? true )
            return; // Defer if Multi-Target is not finished. Defer when in doubt.
         ForceWriteLog();
      }

      internal static MethodInfo GetHitLocation ( Type generic ) {
         return typeof( BattleTech.HitLocation ).GetMethod( "GetHitLocation", Public | Static ).MakeGenericMethod( generic );
      }

      internal static Random idGenerator; // Use an independent generator to make sure we don't affect the game's own RNG or be affected.

      public static string GetNewId () {
         byte[] buffer = new byte[24];
         idGenerator.NextBytes( buffer );
         return Convert.ToBase64String( buffer );
      }

      private static string GetHitKey ( string weapon, object hitLocation, string targetId ) {
         if ( weapon != null && weapon.Length > 8 ) weapon = weapon.Substring( weapon.Length - 5 ); // "Melee" or "0_DFA"
         return weapon + "/" + hitLocation?.ToString() + "/" + targetId;
      }

      private static List<string> blankCache = new List<string>(12);

      private static string FillBlanks ( int blankCount ) {
         while ( blankCache.Count <= blankCount ) blankCache.Add( null );
         string result = blankCache[ blankCount ];
         if ( result == null ) {
            StringBuilder buf = new StringBuilder( blankCount * 3 );
            for ( int i = blankCount ; i > 0 ; i-- )
               buf.Append( Separator ).Append( "--" );
            result = blankCache[ blankCount ] = buf.ToString();
         }
         return result;
      }

      public static string TeamAndCallsign ( ICombatant who ) {
         if ( who == null ) return "null" + Separator + "null" + Separator + "null" + Separator;
         Team team = who.team;
         string teamName;
         if ( team == null )
            teamName = "null";
         else if ( team.IsLocalPlayer )
            teamName = "Player";
         else if ( team.IsEnemy( Combat.LocalPlayerTeam ) )
            teamName = "OpFor";
         else if ( team.IsFriendly( Combat.LocalPlayerTeam ) )
            teamName = "Allies";
         else
            teamName = "NPC";
         teamName += Separator;
         if ( who is Building )
            teamName += "Building";
         else
            teamName += who.GetPilot()?.Callsign ?? ( who as AbstractActor )?.Nickname ?? who.DisplayName;
         teamName += Separator;
         return teamName + who.DisplayName + Separator;
      }

      private static string TimeToFilename ( DateTime time ) {
         return time.ToString("s").Replace(':','-');
      }

      public static void ArchiveOldAttackLog () {
         // First, rename existing log to clear the way for this launch
         if ( ROLL_LOG.Exists() ) try {
            string from = ROLL_LOG.LogFile;
            FileInfo info = new FileInfo( from );
            if ( info.Length > 500 ) {
               string to = ModLogDir + "Log_Attack." + TimeToFilename( info.LastWriteTimeUtc ) + Path.GetExtension( from );
               Info( "Archiving attack log to {1}", from, to );
               File.Move( from, to );
            } else {
               Info( "Deleting old Log_Attack because it is empty: {0}", from );
               ROLL_LOG.Delete();
            }
         } catch ( Exception ex ) { Error( ex ); }

         ThreadPool.QueueUserWorkItem( ( state ) => { TryRun( ModLog, ClearOldLogs ); } );
      }

      public static void ClearOldLogs () {
         FileInfo[] oldLogs = Directory.GetFiles( ModLogDir )
            .Where( e => e.Contains( "Log_Attack.20" ) )
            .Select( e => new FileInfo( e ) )
            .OrderBy( e => e.LastWriteTimeUtc )
            .ToArray();

         long cap = Settings.AttackLogArchiveMaxMB * 1024L * 1024L,
               sum = oldLogs.Select( e => e.Length ).Sum();
         Info( "Background: Found {0} old log(s) totalling {1:#,##0} bytes. Cap is {2:#,##0}.", oldLogs.Length, sum, cap );
         if ( sum <= cap ) return;

         int deleted = 0;
         foreach ( FileInfo f in oldLogs ) try {
            if ( cap > 0 && oldLogs.Length - deleted <= 1 ) break; // Keep at least one log if cap is non-zero.
            //Verbo( "Background: Deleting {0}", f.Name );
            f.Delete();
            deleted++;
            sum -= f.Length;
            if ( sum <= cap || sum <= 0 ) break;
         } catch ( Exception e ) { Error( e ); }
         Info( "Background: {0} old logs deleted.", deleted );
      }

      // ============ Attack Log ============

      internal static string thisSequence = "", thisSequenceId = "", thisSequenceTargetId = "", thisAttackerId = "";

      [ HarmonyPriority( Priority.VeryHigh ) ]
      public static void RecordArtilleryAttack ( ArtillerySequence __instance, ICombatant target ) {
         ArtillerySequence me = __instance;
         Weapon weapon = me.ArtilleryWeapon;
         float range = ( me.TargetPos - target.CurrentPosition ).magnitude;
         BuildSequenceLine( weapon.parent, target, AttackDirection.FromArtillery, range );
         RecordSequenceWeapon( weapon, 1f );
         RecordAttackRoll( 0f, null );
      }

      [ HarmonyPriority( Priority.VeryHigh ) ]
      public static void RecordAttack ( AttackDirector.AttackSequence __instance ) {
         AttackDirector.AttackSequence me = __instance;
         AttackDirection direction = Combat.HitLocation.GetAttackDirection( me.attackPosition, me.target );
         float range = ( me.attackPosition - me.target.CurrentPosition ).magnitude;
         BuildSequenceLine( me.attacker, me.target, direction, range );
      }

      [ HarmonyPriority( Priority.VeryLow ) ]
      public static void LogSelfAttack ( AttackDirector.AttackSequence __instance ) {
         if ( thisWeapon?.WeaponSubType == WeaponSubType.DFA && LogShot ) {
            if ( DebugLog ) Verbo( "Adding {0} DFA self-damage lines", 2 );
            BuildSpecialSequenceLine( __instance.attacker, __instance.attacker, "ToSelf", "DFA" );
            LogSelfHitSequence( ArmorLocation.LeftLeg  ); // 64
            LogSelfHitSequence( ArmorLocation.RightLeg ); // 128
         }
      }

      private static void BuildSequenceLine ( ICombatant attacker, ICombatant target, AttackDirection direction, float range ) {
         string time = DateTime.Now.ToString( "s" );
         if ( DebugLog ) Verbo( "Build Sequence {0} => {1}", attacker?.GUID, target?.GUID );
         thisAttackerId = attacker?.GUID;
         thisSequenceTargetId = target?.GUID;
         thisSequence =
            time + Separator +
            TeamAndCallsign( attacker ) + // Attacker team, pilot, mech
            TeamAndCallsign( target ) +   // Target team, pilot, mech
            thisCombatId + Separator +
            Combat.TurnDirector.CurrentRound + Separator +
            Combat.TurnDirector.CurrentPhase + Separator +
            thisSequenceId + Separator +  // Attack Id
            direction + Separator +
            range;
         if ( ! LogShot )
            log.Add( thisSequence );
      }

      private static void BuildSpecialSequenceLine ( AbstractActor attacker, AbstractActor target, string direction, string cause ) {
         string time = DateTime.Now.ToString( "s" );
         if ( DebugLog ) Verbo( "Build {2} Sequence {0} => {1}", attacker?.GUID, target?.GUID, cause );
         thisSequence =
            time + Separator + TeamAndCallsign( attacker ) + TeamAndCallsign( target ) +
            thisCombatId + Separator +
            Combat.TurnDirector.CurrentRound + Separator +
            Combat.TurnDirector.CurrentPhase + Separator +
            thisSequenceId + Separator +
            direction + Separator + "0" + Separator + // Direction and distance
            cause + Separator + "Special Damage" + FillBlanks( 6 );
      }

      // ============ Shot Log ============

      internal static Weapon thisWeapon;
      internal static float thisHitChance;

      [ HarmonyPriority( Priority.VeryHigh ) ]
      public static void RecordSequenceWeapon ( Weapon weapon, float toHitChance ) {
         thisHitChance = toHitChance;
         thisWeapon = weapon;
         if ( DebugLog ) Verbo( "New Sequence = {0} {1}", weapon?.UIName, thisWeapon?.uid );
      }

      internal static float thisRoll;
      internal static float thisStreak;

      [ HarmonyPriority( Priority.VeryHigh ) ]
      public static void RecordAttackRoll ( float roll, Team team ) {
         if ( DebugLog ) Verbo( "Roll = {0}", roll );
         thisRoll = roll;
         thisStreak = team?.StreakBreakingValue ?? 0;
      }

      internal static string GetShotLog () {
         string weaponName = thisWeapon?.UIName?.ToString().Replace( " +", "+" );
         string uid = thisWeapon?.uid;
         if ( uid != null ) {
            if ( uid.EndsWith( "_Melee" ) ) uid = "Melee";
            else if ( uid.EndsWith( "_DFA" ) ) uid = "DFA";
         }
         return new object[]{ thisSequence, weaponName, thisWeapon?.defId, uid, thisRoll, thisCorrectedRoll + thisStreak, thisStreak, thisCorrectedRoll, thisHitChance }.Concat( Separator );
      }

      internal static float thisCorrectedRoll;

      [ HarmonyPriority( Priority.VeryLow ) ]
      public static void LogMissedAttack ( float __result, float roll, Team team ) {
         thisCorrectedRoll = __result;
         bool miss = __result > thisHitChance;
         if ( miss || ! LogLocation ) { // If miss, log now because hit location won't be rolled
            string line = GetShotLog();
            if ( LogLocation ) {
               if ( DebugLog ) Verbo( "MISS" );
               line += FillBlanks( 11 ) + Separator + "(Miss)";
               if ( LogDamage ) {
                  line += DamageDummy;
                  if ( LogCritical )
                     line += CritDummy;
               }
            } else
               line += Separator + ( miss ? "(Miss)" : "(Hit)" );
            log.Add( line );
         }
      }

      [ HarmonyPriority( Priority.VeryHigh ) ]
      public static void RecordOverheatCheck ( Mech __instance, string attackerID ) {
         if ( ! __instance.IsOverheated ) return;
         AbstractActor attacker = attackerID != __instance.GUID ? Combat.FindActorByGUID( attackerID ) : __instance;
         BuildSpecialSequenceLine( attacker, __instance, "Internal", __instance.IsShutDown ? "Shutdown" : "Overheat" );
      }

      [ HarmonyPriority( Priority.VeryHigh ) ]
      public static void RecordOverheat ( Mech __instance, ChassisLocations location ) {
         beforeStruct = __instance.GetCurrentStructure( location );
      }

      [ HarmonyPriority( Priority.VeryLow ) ]
      public static void LogOverheat ( Mech __instance, ChassisLocations location, float damageAmount ) {
         string line = thisSequence;
         if ( DebugLog ) Verbo( "Overheat damage {1} to {0}", location, damageAmount );
         if ( LogLocation ) {
            line += FillBlanks( 11 ) + Separator + "--";
            if ( LogDamage ) {
               line += Separator + damageAmount
                     + Separator + location // stops at
                     + FillBlanks( 2 ) // armours
                     + Separator + beforeStruct + Separator + __instance.GetCurrentStructure( location );
               if ( LogCritical )
                  line += CritDummy;
            }
         }
         log.Add( line );
      }

      // ============ Location Log ============

      [ HarmonyPriority( Priority.VeryLow ) ]
      public static void LogMechHit ( ArmorLocation __result, Dictionary<ArmorLocation, int> hitTable, float randomRoll, ArmorLocation bonusLocation, float bonusLocationMultiplier ) {
         LogHitSequence( MechStructureRules.GetChassisLocationFromArmorLocation( __result ), randomRoll, bonusLocation, bonusLocationMultiplier,
            TryGet( hitTable, ArmorLocation.Head ) + Separator +
            ( TryGet( hitTable, ArmorLocation.CenterTorso ) + TryGet( hitTable, ArmorLocation.CenterTorsoRear ) ) + Separator +
            ( TryGet( hitTable, ArmorLocation.LeftTorso   ) + TryGet( hitTable, ArmorLocation.LeftTorsoRear   ) ) + Separator +
            ( TryGet( hitTable, ArmorLocation.RightTorso  ) + TryGet( hitTable, ArmorLocation.RightTorsoRear  ) ) + Separator +
            TryGet( hitTable, ArmorLocation.LeftArm  ) + Separator +
            TryGet( hitTable, ArmorLocation.RightArm ) + Separator +
            TryGet( hitTable, ArmorLocation.LeftLeg  ) + Separator +
            TryGet( hitTable, ArmorLocation.RightLeg ) );
      }

      [ HarmonyPriority( Priority.VeryLow ) ]
      public static void LogVehicleHit ( VehicleChassisLocations __result, Dictionary<VehicleChassisLocations, int> hitTable, float randomRoll, VehicleChassisLocations bonusLocation, float bonusLocationMultiplier ) {
         LogHitSequence( __result, randomRoll, bonusLocation, bonusLocationMultiplier,
            TryGet( hitTable, VehicleChassisLocations.Turret ) + Separator +
            TryGet( hitTable, VehicleChassisLocations.Front  ) + Separator +
            TryGet( hitTable, VehicleChassisLocations.Left   ) + Separator +
            TryGet( hitTable, VehicleChassisLocations.Right  ) + Separator +
            TryGet( hitTable, VehicleChassisLocations.Rear   ) + FillBlanks( 3 ) );
      }

      [ HarmonyPriority( Priority.VeryLow ) ]
      public static void LogBuildingHit ( int __result, float hitLocationRoll, BuildingLocation calledShotLocation, float bonusMultiplier ) {
         LogHitSequence( BuildingLocation.Structure, hitLocationRoll, calledShotLocation, bonusMultiplier, "1" + FillBlanks( 7 ) );
      }

      [ HarmonyPriority( Priority.VeryLow ) ]
      public static void LogBuildingClusterHit ( int __result, float randomRoll ) {
         LogHitSequence( BuildingLocation.Structure, randomRoll, "None", 0, "1" + FillBlanks( 7 ) );
      }

      private static void LogHitSequence<T,C> ( T hitLocation, float randomRoll, C bonusLocation, float bonusLocationMultiplier, string hitTable ) where T : Enum { try {
         hitTable = GetShotLog() + Separator + randomRoll + Separator + hitTable + Separator + bonusLocation + Separator + bonusLocationMultiplier + Separator + hitLocation;
         if ( DebugLog ) Verbo( "HIT {0} {1} >>> {2}", GetShotLog(), hitLocation, log.Count );
         if ( LogDamage ) {
            hitList.Add( log.Count );
            hitTable += DamageDummy;
            if ( LogCritical ) {
               hitTable += CritDummy;
               if ( thisWeapon != null ) {
                  string key = GetHitKey( thisWeapon.uid, hitLocation, thisSequenceTargetId );
                  if ( DebugLog ) Verbo( "Hit map {0} = {1}", key, log.Count );
                  hitMap[ key ] = log.Count;
               }
            }
         }
         log.Add( hitTable );
      }                 catch ( Exception ex ) { Error( ex ); } }

      private static void LogSelfHitSequence ( ArmorLocation hitLocation ) { try {
         if ( DebugLog ) Verbo( "SELF HIT {0} {1} >>> {2}", thisSequence, hitLocation, log.Count );
         string line = thisSequence;
         if ( LogLocation ) {
            line += FillBlanks( 11 ) + Separator + hitLocation;
            if ( LogDamage ) {
               line += DamageDummy;
               if ( LogCritical )
                  line += CritDummy;
               hitList.Add( log.Count );
            }
         }
         log.Add( line );
      }                 catch ( Exception ex ) { Error( ex ); } }

      // ============ Damage Log ============

      private static string DamageDummy;

      private static float? thisDamage = null;
      private static string lastLocation;
      private static float beforeArmour;
      private static float beforeStruct;
      private static bool damageResolved;

      [ HarmonyPriority( Priority.VeryHigh ) ]
      public static void RecordMechDamage ( Mech __instance, ArmorLocation aLoc, float totalDamage ) {
         if ( aLoc == ArmorLocation.None || aLoc == ArmorLocation.Invalid ) return;
         RecordUnitDamage( aLoc.ToString(), totalDamage,
            __instance.GetCurrentArmor( aLoc ), __instance.GetCurrentStructure( MechStructureRules.GetChassisLocationFromArmorLocation( aLoc ) ) );
      }

      [ HarmonyPriority( Priority.VeryHigh ) ]
      public static void RecordVehicleDamage ( Vehicle __instance, VehicleChassisLocations vLoc, float totalDamage ) {
         if ( vLoc == VehicleChassisLocations.None || vLoc == VehicleChassisLocations.Invalid ) return;
         RecordUnitDamage( vLoc.ToString(), totalDamage, __instance.GetCurrentArmor( vLoc ), __instance.GetCurrentStructure( vLoc ) );
      }

      [ HarmonyPriority( Priority.VeryHigh ) ]
      public static void RecordTurretDamage ( Turret __instance, BuildingLocation bLoc, float totalDamage ) {
         if ( bLoc == BuildingLocation.None || bLoc == BuildingLocation.Invalid ) return;
         RecordUnitDamage( bLoc.ToString(), totalDamage, __instance.GetCurrentArmor( bLoc ), __instance.GetCurrentStructure( bLoc ) );
      }

      [ HarmonyPriority( Priority.VeryHigh ) ]
      public static void RecordBuildingDamage ( BattleTech.Building __instance, float totalDamage ) {
         RecordUnitDamage( BuildingLocation.Structure.ToString(), totalDamage, 0, __instance.CurrentStructure );
      }

      private static void RecordUnitDamage ( string loc, float totalDamage, float armour, float structure ) {
         if ( DebugLog ) Verbo( "{0} Damage @ {1}", loc, totalDamage );
         lastLocation = loc;
         if ( thisDamage == null ) thisDamage = totalDamage;
         beforeArmour = armour;
         beforeStruct = structure;
         damageResolved = false;
      }

      private static bool IsLoggedDamage ( DamageType type ) {
         switch ( type ) {
            case DamageType.Weapon:
            case DamageType.Overheat:
            case DamageType.OverheatSelf:
            case DamageType.DFA:
            case DamageType.DFASelf:
               return true;
         }
         return false;
      }

      [ HarmonyPriority( Priority.VeryLow ) ]
      public static void LogMechDamage ( Mech __instance, ArmorLocation aLoc, Weapon weapon, DamageType damageType ) {
         if ( aLoc == ArmorLocation.None || aLoc == ArmorLocation.Invalid || ! IsLoggedDamage( damageType ) ) return;
         int line = LogActorDamage( __instance.GetCurrentArmor( aLoc ), __instance.GetCurrentStructure( MechStructureRules.GetChassisLocationFromArmorLocation( aLoc ) ) );
         if ( line >= 0 && Settings.CritFollowDamageTransfer && hitMap != null ) {
            string newKey = GetHitKey( weapon.uid, aLoc, __instance.GUID );
            if ( DebugLog ) Verbo( "Log damage transfer {0} = {1}", newKey, line );
            hitMap[ newKey ] = line;
         }
      }

      [ HarmonyPriority( Priority.VeryLow ) ]
      public static void LogVehicleDamage ( Vehicle __instance, VehicleChassisLocations vLoc ) {
         if ( vLoc == VehicleChassisLocations.None || vLoc == VehicleChassisLocations.Invalid ) return;
         LogActorDamage( __instance.GetCurrentArmor( vLoc ), __instance.GetCurrentStructure( vLoc ) );
      }

      [ HarmonyPriority( Priority.VeryLow ) ]
      public static void LogTurretDamage ( Turret __instance, BuildingLocation bLoc ) {
         if ( bLoc == BuildingLocation.None || bLoc == BuildingLocation.Invalid ) return;
         LogActorDamage( __instance.GetCurrentArmor( bLoc ), __instance.GetCurrentStructure( bLoc ) );
      }

      [ HarmonyPriority( Priority.VeryLow ) ]
      public static void LogBuildingDamage ( BattleTech.Building __instance ) {
         LogActorDamage( 0, __instance.CurrentStructure );
      }

      private static int LogActorDamage ( float afterArmour, float afterStruct ) { try {
         if ( damageResolved ) return -1;
         damageResolved = true;
         if ( hitList.Count <= 0 ) {
            Warn( "Damage Log cannot find matching hit record." );
            return -1;
         }
         int index = hitList[0];
         if ( log.Count <= index ) {
            Warn( "Damage Log does not contain index #{0}, aborting.", index );
            return -1;
         }
         string line = log[ index ];
         if ( ( LogCritical && ! line.EndsWith( DamageDummy + CritDummy ) ) || ( ! LogCritical && ! line.EndsWith( DamageDummy ) ) ) {
            Warn( "Damage Log found an amended line, aborting." );
            hitList.RemoveAt( 0 );
            if ( DebugLog ) Verbo( "Hit list remaining {0}", hitList.Count );
            thisDamage = null;
            return -1;
         }

         if ( LogCritical )
            line = line.Substring( 0, line.Length - CritDummy.Length );
         line = line.Substring( 0, line.Length - DamageDummy.Length ) + Separator +
               thisDamage   + Separator + lastLocation + Separator +
               beforeArmour + Separator + afterArmour  + Separator +
               beforeStruct + Separator + afterStruct;
         if ( LogCritical )
            line += CritDummy;

         log[ index ] = line;
         hitList.RemoveAt( 0 );
         if ( DebugLog ) Verbo( "Log damage {0} @ {1}. Hit list remaining: {2}. Line index {3}", thisDamage, lastLocation, hitList.Count, index );
         thisDamage = null;
         return index;
      }                 catch ( Exception ex ) { Error( ex ); return -1; } }


      // ============ Crit Log ============

      private static string CritDummy;

      private static string thisCritLocation;
      private static float thisCritRoll, thisCritSlotRoll, thisBaseCritChance, thisCritMultiplier, thisCritChance, thisLocationMaxHP;
      private static bool ammoExploded, checkCritComp;
      private static int thisCritSlot = -1;
      internal static MechComponent thisCritComp;
      private static ComponentDamageLevel thisCompBefore;

      [ HarmonyPriority( Priority.VeryLow ) ]
      public static void LogCritRolls ( float[] __result, int amount ) {
         if ( amount == 2 ) {
            LogAIMCritRoll( __result[0] );
            LogAIMSlotRoll( __result[1] );
         }
      }

      public static void LogAIMCritRoll ( float crit ) {
         if ( DebugLog ) Verbo( "Crit Roll = {0}", crit );
         thisCritRoll = crit;
      }

      public static void LogAIMSlotRoll ( float slot ) {
         if ( DebugLog ) Verbo( "Slot Roll = {0}", slot );
         thisCritSlotRoll = slot;
         checkCritComp = true;
      }

      [ HarmonyPriority( Priority.VeryLow ) ]
      public static void LogBaseCritChance ( float __result, Mech target, ChassisLocations hitLocation ) {
         thisBaseCritChance = __result;
         thisLocationMaxHP = target.GetMaxStructure( hitLocation );
      }

      // Used by universal crit system of this mod (through armour crit included)
      public static void LogAIMBaseCritChance ( float chance, float maxHP ) {
         thisBaseCritChance = chance;
         thisLocationMaxHP = maxHP;
      }

      [ HarmonyPriority( Priority.VeryLow ) ]
      public static void LogCritMultiplier ( float __result ) {
         thisCritMultiplier = __result;
      }

      [ HarmonyPriority( Priority.VeryLow ) ]
      public static void LogCritChance ( float __result, ChassisLocations hitLocation ) {
         LogAIMCritChance( __result, hitLocation );
      }

      public static float LogAIMCritChance ( float chance, object hitLocation ) {
         thisCritChance = chance;
         thisCritLocation = hitLocation.ToString();
         thisCritComp = null;
         return chance;
      }

      [ HarmonyPriority( Priority.VeryLow ) ]
      public static void LogCritComp ( MechComponent __result, int index ) {
         if ( ! checkCritComp ) return;  // GetComponentInSlot is used in lots of places, and is better gated.
         if ( DebugLog ) Verbo( "Record Crit Comp = {0} of {1}", __result?.UIName, __result?.parent?.GUID );
         thisCritSlot = index;
         thisCritComp = __result;
         if ( thisCritComp != null ) {
            thisCompBefore = thisCritComp.DamageLevel;
            ammoExploded = false;
         }
         checkCritComp = false;
      }

      [ HarmonyPriority( Priority.VeryLow ) ]
      public static void LogAmmoExplosionFlag () {
         ammoExploded = true; // Not sure why, but this may not be triggered, so need pilot check as safeguard.
      }

      [ HarmonyPriority( Priority.VeryLow ) ]
      public static void LogAmmoExplosionOnPilot ( InjuryReason reason ) {
         if ( reason != InjuryReason.AmmoExplosion ) return;
         ammoExploded = true;
      }

      [ HarmonyPriority( Priority.VeryLow ) ]
      public static void LogCritResult ( ICombatant __instance, Weapon weapon ) { try {
         if ( AnyNull( __instance, weapon, thisCritLocation ) ) return;
         if ( __instance.GUID == thisAttackerId ) {
            if ( DebugLog ) Verbo( "Skip logging self crit" );
         } else {
            string key = GetHitKey( weapon.uid, thisCritLocation, __instance.GUID );
            if ( DebugLog ) Verbo( "Crit by {0}, key {1}", weapon.UIName, key );
            if ( ( ! hitMap.TryGetValue( key, out int lineIndex ) ) ) {
               Warn( "Critical Hit Log cannot find matching hit record: {0}", key );
               return;
            }
            string line = log[ lineIndex ];
            if ( ! line.EndsWith( CritDummy ) ) {
               Warn( "Critical Hit Log found duplicate crit {0}", key );
               return;
            }
            string critLine = Separator + thisLocationMaxHP +
                              Separator + thisCritRoll +
                              Separator + thisBaseCritChance +
                              Separator + thisCritMultiplier +
                              Separator + thisCritChance;
            if ( thisCritSlot < 0 || thisCritChance <= 0 )
               critLine += Separator + "--" + Separator + "--" + Separator + "(No Crit)" + Separator + "--" + Separator + "--";
            else {
               critLine += Separator + thisCritSlotRoll +
                           Separator + ( thisCritSlot + 1 );
               if ( thisCritComp == null )
                  critLine += Separator + "(Empty)" + Separator + "--" + Separator + "--";
               else {
                  string thisCompAfter = ammoExploded ? "Explosion" : thisCritComp.DamageLevel.ToString();
                  critLine += Separator + thisCritComp.UIName?.ToString() +
                              Separator + thisCompBefore +
                              Separator + thisCompAfter;
               }
            }
            line = line.Substring( 0, line.Length - CritDummy.Length ) + critLine;
            log[ lineIndex ] = line;
         }
         thisLocationMaxHP = thisCritRoll = thisCritSlotRoll = thisBaseCritChance = thisCritMultiplier = thisCritChance = 0;
         thisCritLocation = "None";
         thisCritSlot = -1;
         thisCritComp = null;
         checkCritComp = ammoExploded = false;
      }                 catch ( Exception ex ) { Error( ex ); } }
#pragma warning restore CS0162 // Restore "unreachable code" warnings
   }
}