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

      internal static Logger ROLL_LOG;

      private static bool LogShot, LogLocation, LogDamage, LogCritical;
      private static string Separator = ",";

      private static readonly Type AttackType = typeof( AttackDirector.AttackSequence );
      private static readonly Type ArtilleyAttackType = typeof( ArtillerySequence );

      private static string thisCombatId = string.Empty;

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
               Patch( MechType, "CheckForCrit", NonPublic, null, "LogCritResult" );
               goto case "damage";

            case "damage":
               DamageDummy = FillBlanks( 6 );
               Patch( MechType, "DamageLocation", NonPublic, "RecordMechDamage", "LogMechDamage" );
               Patch( VehiType, "DamageLocation", NonPublic, "RecordVehicleDamage", "LogVehicleDamage" );
               Patch( TurtType, "DamageLocation", NonPublic, "RecordTurretDamage", "LogTurretDamage" );
               Patch( BuldType, "DamageBuilding", NonPublic, "RecordBuildingDamage", "LogBuildingDamage" );
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
               Patch( AttackType, "GetIndividualHits", NonPublic, "RecordSequenceWeapon", null );
               Patch( AttackType, "GetClusteredHits" , NonPublic, "RecordSequenceWeapon", null );
               Patch( AttackType, "GetCorrectedRoll" , NonPublic, "RecordAttackRoll", "LogMissedAttack" );
               goto case "attack";

            case "attack":
               Patch( ArtilleyAttackType, "PerformAttack", NonPublic, "RecordArtilleryAttack", null );
               Patch( AttackType, "GenerateToHitInfo", NonPublic, "RecordAttack", null );
               Patch( typeof( AttackDirector ), "OnAttackComplete", null, "WriteRollLog" );
               InitLog();
               break;

            default:
               Warn( "Unknown AttackLogLevel " + Settings.AttackLogLevel );
               goto case "none";
            case null:
            case "none":
               break;
         }
      }

      public static void InitLog () {
         Info( "Init logger" );
         ROLL_LOG = new Logger( ModLogDir + "Log_Attack." + Settings.AttackLogFormat ){ LevelText = null, TimeFormat = null };
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
            WriteRollLog( null );
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

      [ HarmonyPriority( Priority.Last ) ]
      public static void WriteRollLog ( AttackDirector __instance ) {
         if ( __instance != null && __instance.IsAnyAttackSequenceActive )
            return; // Defer if Multi-Target is not finished
         ROLL_LOG.Info( string.Join( Environment.NewLine, log.ToArray() ) );
         log.Clear();
         hitList?.Clear();
         hitMap?.Clear();
         thisSequenceId = GetNewId();
         //Verbo( "Log written and HitMap Cleared\n" );
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

      private static string GetHitKey<T> ( string weapon, T hitLocation, string targetId ) where T : Enum  {
         return weapon + "/" + (int)(object) hitLocation + "/" + targetId;
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

      public static void ArchiveOldAttackLog () {
         // First, rename existing log to clear the way for this launch
         if ( ROLL_LOG.Exists() ) try {
            string from = ROLL_LOG.LogFile;
            FileInfo info = new FileInfo( from );
            if ( info.Length > 500 ) {
               string to = ModLogDir + "Log_Attack." + info.LastWriteTimeUtc.ToString("s").Replace(':','-') + Path.GetExtension( from );
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

      internal static string thisSequence = "";
      internal static string thisSequenceId = "";
      internal static string thisSequenceTargetId = "";

      [ HarmonyPriority( Priority.First ) ]
      public static void RecordArtilleryAttack ( ArtillerySequence __instance, ICombatant target ) {
         ArtillerySequence me = __instance;
         Weapon weapon = me.ArtilleryWeapon;
         float range = ( me.TargetPos - target.CurrentPosition ).magnitude;
         BuildSequenceLine( weapon.parent, target, AttackDirection.FromArtillery, range );
         RecordSequenceWeapon( weapon, 1f );
         RecordAttackRoll( 0f, null );
      }

      [ HarmonyPriority( Priority.First ) ]
      public static void RecordAttack ( AttackDirector.AttackSequence __instance ) {
         AttackDirector.AttackSequence me = __instance;
         AttackDirection direction = Combat.HitLocation.GetAttackDirection( me.attackPosition, me.target );
         float range = ( me.attackPosition - me.target.CurrentPosition ).magnitude;
         BuildSequenceLine( me.attacker, me.target, direction, range );
      }

      private static void BuildSequenceLine ( ICombatant attacker, ICombatant target, AttackDirection direction, float range ) {
         string time = DateTime.Now.ToString( "s" );
         thisSequenceTargetId = target.GUID;
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

      // ============ Shot Log ============

      internal static Weapon thisWeapon;
      internal static float thisHitChance;

      [ HarmonyPriority( Priority.First ) ]
      public static void RecordSequenceWeapon ( Weapon weapon, float toHitChance ) {
         thisHitChance = toHitChance;
         thisWeapon = weapon;
         //Verbo( "GetIndividualHits / GetClusteredHits / ArtillerySequence = {0} {1}", weapon?.UIName, thisWeapon?.uid );
      }

      internal static float thisRoll;
      internal static float thisStreak;

      [ HarmonyPriority( Priority.First ) ]
      public static void RecordAttackRoll ( float roll, Team team ) {
         //Verbo( "Roll = {0}", roll );
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
         return Join( Separator, new object[]{ thisSequence, weaponName, thisWeapon?.defId, uid, thisRoll, thisCorrectedRoll + thisStreak, thisStreak, thisCorrectedRoll, thisHitChance } );
      }

      internal static float thisCorrectedRoll;

      [ HarmonyPriority( Priority.Last ) ]
      public static void LogMissedAttack ( float __result, float roll, Team team ) {
         thisCorrectedRoll = __result;
         bool miss = __result > thisHitChance;
         if ( miss || ! LogLocation ) { // If miss, log now because hit location won't be rolled
            StringBuilder logBuffer = new StringBuilder();
            logBuffer.Append( GetShotLog() );
            if ( LogLocation ) {
               //Verbo( "MISS" );
               logBuffer.Append( FillBlanks( 11 ) + Separator + "(Miss)" );
               if ( LogDamage )
                  logBuffer.Append( DamageDummy );
               if ( LogCritical )
                  logBuffer.Append( CritDummy );
            } else
               logBuffer.Append( Separator ).Append( miss ? "(Miss)" : "(Hit)" );
            log.Add( logBuffer.ToString() );
         }
      }

      // ============ Location Log ============

      [ HarmonyPriority( Priority.Last ) ]
      public static void LogMechHit ( ArmorLocation __result, Dictionary<ArmorLocation, int> hitTable, float randomRoll, ArmorLocation bonusLocation, float bonusLocationMultiplier ) {
         LogHitSequence( MechStructureRules.GetChassisLocationFromArmorLocation( __result ), randomRoll, bonusLocation, bonusLocationMultiplier, true,
            TryGet( hitTable, ArmorLocation.Head ) + Separator +
            ( TryGet( hitTable, ArmorLocation.CenterTorso ) + TryGet( hitTable, ArmorLocation.CenterTorsoRear ) ) + Separator +
            ( TryGet( hitTable, ArmorLocation.LeftTorso   ) + TryGet( hitTable, ArmorLocation.LeftTorsoRear   ) ) + Separator +
            ( TryGet( hitTable, ArmorLocation.RightTorso  ) + TryGet( hitTable, ArmorLocation.RightTorsoRear  ) ) + Separator +
            TryGet( hitTable, ArmorLocation.LeftArm  ) + Separator +
            TryGet( hitTable, ArmorLocation.RightArm ) + Separator +
            TryGet( hitTable, ArmorLocation.LeftLeg  ) + Separator +
            TryGet( hitTable, ArmorLocation.RightLeg ) );
      }

      [ HarmonyPriority( Priority.Last ) ]
      public static void LogVehicleHit ( VehicleChassisLocations __result, Dictionary<VehicleChassisLocations, int> hitTable, float randomRoll, VehicleChassisLocations bonusLocation, float bonusLocationMultiplier ) {
         LogHitSequence( __result, randomRoll, bonusLocation, bonusLocationMultiplier, false,
            TryGet( hitTable, VehicleChassisLocations.Turret ) + Separator +
            TryGet( hitTable, VehicleChassisLocations.Front  ) + Separator +
            TryGet( hitTable, VehicleChassisLocations.Left   ) + Separator +
            TryGet( hitTable, VehicleChassisLocations.Right  ) + Separator +
            TryGet( hitTable, VehicleChassisLocations.Rear   ) + FillBlanks( 3 ) );
      }

      [ HarmonyPriority( Priority.Last ) ]
      public static void LogBuildingHit ( int __result, float hitLocationRoll, BuildingLocation calledShotLocation, float bonusMultiplier ) {
         LogHitSequence( BuildingLocation.Structure, hitLocationRoll, calledShotLocation, bonusMultiplier, false, "1" + FillBlanks( 7 ) );
      }

      [ HarmonyPriority( Priority.Last ) ]
      public static void LogBuildingClusterHit ( int __result, float randomRoll ) {
         LogHitSequence( BuildingLocation.Structure, randomRoll, "None", 0, false, "1" + FillBlanks( 7 ) );
      }

      private static void LogHitSequence<T,C> ( T hitLocation, float randomRoll, C bonusLocation, float bonusLocationMultiplier, bool canCrit, string line ) where T : Enum { try {
         line = GetShotLog() + Separator + randomRoll + Separator + line + Separator + bonusLocation + Separator + bonusLocationMultiplier + Separator + hitLocation;
         //Verbo( "HIT {0} {1} >>> {3}", GetShotLog(), hitLocation, log.Count );
         if ( LogDamage ) {
            hitList.Add( log.Count );
            line += DamageDummy;
            if ( LogCritical ) {
               line += CritDummy;
               if ( canCrit ) {
                  string key = GetHitKey( thisWeapon?.uid, hitLocation, thisSequenceTargetId );
                  //Verbo( "Hit map {0} = {1}", key, log.Count );
                  hitMap[ key ] = log.Count;
               }
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

      [ HarmonyPriority( Priority.First ) ]
      public static void RecordMechDamage ( Mech __instance, ArmorLocation aLoc, float totalDamage ) {
         if ( aLoc == ArmorLocation.None || aLoc == ArmorLocation.Invalid ) return;
         RecordUnitDamage( aLoc.ToString(), totalDamage,
            __instance.GetCurrentArmor( aLoc ), __instance.GetCurrentStructure( MechStructureRules.GetChassisLocationFromArmorLocation( aLoc ) ) );
      }

      [ HarmonyPriority( Priority.First ) ]
      public static void RecordVehicleDamage ( Vehicle __instance, VehicleChassisLocations vLoc, float totalDamage ) {
         if ( vLoc == VehicleChassisLocations.None || vLoc == VehicleChassisLocations.Invalid ) return;
         RecordUnitDamage( vLoc.ToString(), totalDamage, __instance.GetCurrentArmor( vLoc ), __instance.GetCurrentStructure( vLoc ) );
      }

      [ HarmonyPriority( Priority.First ) ]
      public static void RecordTurretDamage ( Turret __instance, BuildingLocation bLoc, float totalDamage ) {
         if ( bLoc == BuildingLocation.None || bLoc == BuildingLocation.Invalid ) return;
         RecordUnitDamage( bLoc.ToString(), totalDamage, __instance.GetCurrentArmor( bLoc ), __instance.GetCurrentStructure( bLoc ) );
      }

      [ HarmonyPriority( Priority.First ) ]
      public static void RecordBuildingDamage ( BattleTech.Building __instance, float totalDamage ) {
         RecordUnitDamage( BuildingLocation.Structure.ToString(), totalDamage, 0, __instance.CurrentStructure );
      }

      private static void RecordUnitDamage ( string loc, float totalDamage, float armour, float structure ) {
         //Verbo( "{0} Damage @ {1}", loc, totalDamage );
         lastLocation = loc;
         if ( thisDamage == null ) thisDamage = totalDamage;
         beforeArmour = armour;
         beforeStruct = structure;
         damageResolved = false;
      }

      [ HarmonyPriority( Priority.Last ) ]
      public static void LogMechDamage ( Mech __instance, ArmorLocation aLoc, Weapon weapon ) {
         if ( aLoc == ArmorLocation.None || aLoc == ArmorLocation.Invalid ) return;
         int line = LogActorDamage( __instance.GetCurrentArmor( aLoc ), __instance.GetCurrentStructure( MechStructureRules.GetChassisLocationFromArmorLocation( aLoc ) ) );
         if ( Settings.CritFollowDamageTransfer && hitMap != null ) {
            string newKey = GetHitKey( weapon.uid, aLoc, __instance.GUID );
            //Verbo( "Log damage transfer {0} = {1}", newKey, line );
            hitMap[ newKey ] = line;
         }
      }

      [ HarmonyPriority( Priority.Last ) ]
      public static void LogVehicleDamage ( Vehicle __instance, VehicleChassisLocations vLoc ) {
         if ( vLoc == VehicleChassisLocations.None || vLoc == VehicleChassisLocations.Invalid ) return;
         LogActorDamage( __instance.GetCurrentArmor( vLoc ), __instance.GetCurrentStructure( vLoc ) );
      }

      [ HarmonyPriority( Priority.Last ) ]
      public static void LogTurretDamage ( Turret __instance, BuildingLocation bLoc ) {
         if ( bLoc == BuildingLocation.None || bLoc == BuildingLocation.Invalid ) return;
         LogActorDamage( __instance.GetCurrentArmor( bLoc ), __instance.GetCurrentStructure( bLoc ) );
      }

      [ HarmonyPriority( Priority.Last ) ]
      public static void LogBuildingDamage ( BattleTech.Building __instance ) {
         LogActorDamage( 0, __instance.CurrentStructure );
      }

      private static int LogActorDamage ( float afterArmour, float afterStruct ) { try {
         if ( damageResolved ) return -1;
         damageResolved = true;
         if ( hitList.Count <= 0 ) {
            Warn( "Damage Log cannot find matching hit record. May be DFA self-damage?" );
            return -1;
         }
         int index = hitList[0];
         string line = log[ index ];
         if ( ( LogCritical && ! line.EndsWith( DamageDummy + CritDummy ) ) || ( ! LogCritical && ! line.EndsWith( DamageDummy ) ) ) {
            Warn( "Damage Log found an amended line, aborting." );
            hitList.RemoveAt( 0 );
            //Verbo( "Hit list remaining {0}", hitList.Count );
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
         //Verbo( "Log damage {0} @ {1}. Hit list remaining: {2}", thisDamage, lastLocation, hitList.Count );
         thisDamage = null;
         return index;
      }                 catch ( Exception ex ) { Error( ex ); return -1; } }


      // ============ Crit Log ============

      private static string CritDummy;

      private static float thisCritRoll, thisCritSlotRoll, thisBaseCritChance, thisCritMultiplier, thisCritChance, thisLocationMaxHP;
      private static bool ammoExploded, checkCritComp;
      private static int thisCritSlot;
      private static MechComponent thisCritComp;
      private static ComponentDamageLevel thisCompBefore;

      [ HarmonyPriority( Priority.Last ) ]
      public static void LogCritRolls ( float[] __result, int amount ) {
         if ( amount == 2 ) {
            //Verbo( "Crit Roll = {0} & {1}", __result[0], __result[1] );
            thisCritRoll = __result[0];
            thisCritSlotRoll = __result[1];
            checkCritComp = true;
         }
      }

      [ HarmonyPriority( Priority.Last ) ]
      public static void LogBaseCritChance ( float __result, Mech target, ChassisLocations hitLocation ) {
         thisBaseCritChance = __result;
         thisLocationMaxHP = target.GetMaxStructure( hitLocation );
      }

      [ HarmonyPriority( Priority.Last ) ]
      public static void LogCritMultiplier ( float __result ) {
         thisCritMultiplier = __result;
      }

      [ HarmonyPriority( Priority.Last ) ]
      public static void LogCritChance ( float __result ) {
         thisCritChance = __result;
         thisCritComp = null;
      }

      [ HarmonyPriority( Priority.Last ) ]
      public static void LogCritComp ( MechComponent __result, ChassisLocations location, int index ) {
         if ( ! checkCritComp ) return;  // GetComponentInSlot is used in lots of places, and is better gated.
         //Verbo( "Record Crit Comp @ {0} = {1}", location, __result?.UIName );
         thisCritSlot = index;
         thisCritComp = __result;
         if ( thisCritComp != null ) {
            thisCompBefore = thisCritComp.DamageLevel;
            ammoExploded = checkCritComp = false;
         }
      }

      [ HarmonyPriority( Priority.Last ) ]
      public static void LogAmmoExplosionFlag () {
         ammoExploded = true; // Not sure why, but this may not be triggered, so need pilot check as safeguard.
      }

      [ HarmonyPriority( Priority.Last ) ]
      public static void LogAmmoExplosionOnPilot ( InjuryReason reason ) {
         if ( reason != InjuryReason.AmmoExplosion ) return;
         ammoExploded = true;
      }

      [ HarmonyPriority( Priority.Last ) ]
      public static void LogCritResult ( Mech __instance, ChassisLocations location, Weapon weapon ) { try {
         string key = GetHitKey( weapon.uid, location, __instance.GUID );
         //Verbo( "Crit {0} {1} {2}, key {3}", weapon.UIName, __instance.GetPilot().Callsign, location, key );
         if ( ( ! hitMap.TryGetValue( key, out int lineIndex ) ) ) {
            Warn( "Critical Hit Log cannot find matching hit record: " + key );
            return;
         }
         string line = log[ lineIndex ];
         if ( ! line.EndsWith( CritDummy ) ) {
            Warn( "Critical Hit Log found duplicate crit: " + key );
            return;
         }
         string critLine = Separator + thisLocationMaxHP +
                           Separator + thisCritRoll +
                           Separator + thisBaseCritChance +
                           Separator + thisCritMultiplier +
                           Separator + thisCritChance;
         if ( thisCritSlot < 0 )
            critLine += Separator + "--" + Separator + "--" + Separator + "(No Crit)" + Separator + "--" + Separator + "--";
         else {
            critLine += Separator + thisCritSlotRoll +
                        Separator + ( thisCritSlot + 1 );
            if ( thisCritComp == null )
               critLine += Separator + "(Empty)" + Separator + "--" + Separator + "--";
            else {
               string thisCompAfter = ammoExploded ? "Explosion" : thisCritComp.DamageLevel.ToString();
               critLine += Separator + thisCritComp.UIName +
                           Separator + thisCompBefore +
                           Separator + thisCompAfter;
            }
         }
         //Verbo( "Crit Line = {0}", critLine );
         line = line.Substring( 0, line.Length - CritDummy.Length ) + critLine;
         log[ lineIndex ] = line;
         thisCritSlot = -1;
         thisCritComp = null;
      }                 catch ( Exception ex ) { Error( ex ); } }
   }
}