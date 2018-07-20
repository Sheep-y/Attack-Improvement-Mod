using BattleTech;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System;
using static System.Reflection.BindingFlags;

namespace Sheepy.BattleTechMod.AttackImprovementMod {
   using static Mod;

   public class AttackLog : BattleModModule {

      internal static Logger ROLL_LOG;

      private static bool LogShot, LogLocation, LogDamage, LogCritical;
      private static bool PersistentLog = false;

      private static readonly Type MechType = typeof( Mech );
      private static readonly Type VehiType = typeof( Vehicle );
      private static readonly Type TurtType = typeof( Turret );
      private static readonly Type BuldType = typeof( BattleTech.Building );

      public override void ModStarts () {
         PersistentLog = Settings.PersistentLog;
         // Patch prefix early to increase chance of successful capture in face of other mods
         switch ( Settings.AttackLogLevel?.Trim().ToLower() ) {
            case "all":
            case "critical":
               LogCritical = true;
               goto case "damage";

            case "damage":
               Patch( MechType, "DamageLocation", NonPublic, "RecordMechDamage", null );
               Patch( VehiType, "DamageLocation", NonPublic, "RecordVehicleDamage", null );
               Patch( TurtType, "DamageLocation", NonPublic, "RecordTurretDamage", null );
               Patch( BuldType, "DamageBuilding", NonPublic, "RecordBuildingDamage", null );
               LogDamage = true;
               goto case "location";

            case "location":
               LogLocation = true;
               goto case "shot";

            case "shot":
               LogShot = true;
               Type AttackType = typeof( AttackDirector.AttackSequence );
               Patch( AttackType, "GetIndividualHits", NonPublic, "RecordSequenceWeapon", null );
               Patch( AttackType, "GetClusteredHits" , NonPublic, "RecordSequenceWeapon", null );
               Patch( AttackType, "GetCorrectedRoll" , NonPublic, "RecordAttackRoll", null );
               goto case "attack";

            case "attack":
               Patch( typeof( AttackDirector.AttackSequence ), "GenerateToHitInfo", NonPublic, "RecordAttack", null );
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
         ROLL_LOG = new Logger( ModLogDir + "Log_Attack.txt" );
         idGenerator = new Random();
         thisSequenceId = GetNewId();

         if ( ! PersistentLog )
            ROLL_LOG.Delete();

         if ( ! ROLL_LOG.Exists() ) {
            StringBuilder logBuffer = new StringBuilder();
            logBuffer.Append( String.Join( "\t", new string[]{ "Time", "Actor Team", "Actor Pilot", "Actor Unit", "Target Team", "Target Pilot", "Target Unit", "Combat Id", "Attack Id", "Direction", "Range" } ) );
            if ( LogShot || PersistentLog ) {
               logBuffer.Append( "\t" ).Append( String.Join( "\t", new string[]{ "Weapon", "Hit Roll", "Corrected", "Streak", "Final", "Hit%" } ) );
               if ( LogLocation || PersistentLog )
                  logBuffer.Append( "\t" ).Append( String.Join( "\t", new string[]{ "Location Roll", "Head/Turret", "CT/Front", "LT/Left", "RT/Right", "LA/Rear", "RA", "LL", "RL", "Called Part", "Called Multiplier" } ) );
               logBuffer.Append( "\tHit Location" );
            }
            if ( LogDamage || PersistentLog )
               logBuffer.Append( "\t" ).Append( String.Join( "\t", new string[]{ "Damage", "Stops At", "From Armor", "To Armor", "From HP", "To HP" } ) );
            if ( LogCritical || PersistentLog )
               logBuffer.Append( "\t" ).Append( String.Join( "\t", new string[]{ "Max HP", "Crit Roll", "Base Crit%", "Crit Multiplier", "Crit%", "Slot Roll", "Crit Slot", "Crit Equipment", "From State", "To State" } ) );
            log.Add( logBuffer.ToString() );
            WriteRollLog( null );
         }

         if ( LogDamage )
            hitList = new List<int>( 16 );
         if ( LogCritical )
            hitMap = new Dictionary<string, int>( 16 );
      }

      private static string thisCombatId = String.Empty;
      private bool LoggerPatched = false;

      public override void CombatStarts () {
         thisCombatId = GetNewId();

         if ( LoggerPatched ) return;
         LoggerPatched = true;

         // Patch Postfix late to increase odds of capturing modded values
         if ( LogShot )
            Patch( typeof( AttackDirector.AttackSequence ), "GetCorrectedRoll" , NonPublic, null, "LogMissedAttack" );

         if ( LogLocation ) {
            Patch( GetHitLocation( typeof( ArmorLocation ) ), null, "LogMechHit" );
            Patch( GetHitLocation( typeof( VehicleChassisLocations ) ), null, "LogVehicleHit" );
            Patch( TurtType, "GetHitLocation", new Type[]{ typeof( AbstractActor ), typeof( UnityEngine.Vector3 ), typeof( float ), typeof( ArmorLocation ), typeof( float ) }, null, "LogBuildingHit" );
            Patch( BuldType, "GetHitLocation", new Type[]{ typeof( AbstractActor ), typeof( UnityEngine.Vector3 ), typeof( float ), typeof( ArmorLocation ), typeof( float ) }, null, "LogBuildingHit" );
            Patch( TurtType, "GetAdjacentHitLocation", null, "LogBuildingClusterHit" );
            Patch( BuldType, "GetAdjacentHitLocation", null, "LogBuildingClusterHit" );
         }

         if ( LogDamage ) {
            Patch( MechType, "DamageLocation", NonPublic, null, "LogMechDamage" );
            Patch( VehiType, "DamageLocation", NonPublic, null, "LogVehicleDamage" );
            Patch( TurtType, "DamageLocation", NonPublic, null, "LogTurretDamage" );
            Patch( BuldType, "DamageBuilding", NonPublic, null, "LogBuildingDamage" );
         }

         if ( LogCritical ) {
            Type CritRulesType = typeof( CritChanceRules );
            Patch( typeof( AttackDirector ), "GetRandomFromCache", new Type[]{ typeof( WeaponHitInfo ), typeof( int ) }, null, "RecordCritRolls" );
            Patch( CritRulesType, "GetBaseCritChance", new Type[]{ MechType, typeof( ChassisLocations ), typeof( bool ) }, null, "RecordBaseCritChance" );
            Patch( CritRulesType, "GetCritMultiplier", null, "RecordCritMultiplier" );
            Patch( CritRulesType, "GetCritChance", null, "RecordCritChance" );
            Patch( MechType, "GetComponentInSlot", null, "RecordCritComp" );
            Patch( MechType, "CheckForCrit", NonPublic, null, "LogCritResult" );
         }
      }

      // ============ UTILS ============

      private static List<int> hitList; // Used to assign damage information
      private static Dictionary<string, int> hitMap; // Used to assign critical hit information
      private static List<string> log = new List<string>( 32 );

      public static void WriteRollLog ( AttackDirector __instance ) {
         if ( __instance != null && __instance.IsAnyAttackSequenceActive )
            return; // Defer if Multi-Target is not finished
         ROLL_LOG.Log( String.Join( Environment.NewLine, log.ToArray() ) );
         log.Clear();
         hitList?.Clear();
         hitMap?.Clear();
         thisSequenceId = GetNewId();
         //Log( "Log written and HitMap Cleared" );
      }

      internal static MethodInfo GetHitLocation ( Type generic ) {
         return typeof( BattleTech.HitLocation ).GetMethod( "GetHitLocation", Public | Static ).MakeGenericMethod( generic );
      }
      
      internal static Random idGenerator; // Use an independent generator to make sure we don't affect the game's own RNG or be affected.

      public static string GetNewId () {
         byte[] buffer = new byte[32];
         idGenerator.NextBytes( buffer );
         return BitConverter.ToString( buffer ).Replace( "-", "" );
      }

      private static string GetHitKey<T> ( string weapon, T hitLocation, string targetId ) {
         return weapon + "@" + hitLocation + "@" + targetId + "@";
      }

      public static string TeamAndCallsign ( ICombatant who ) {
         if ( who == null ) return "null\tnull\tnull\t";
         Team team = who.team;
         string teamName;
         if ( team == null )
            teamName = "null";
         else if ( team.IsLocalPlayer )
            teamName = "Player";
         else if ( team.IsEnemy( Combat.LocalPlayerTeam ) )
            teamName = "OpFor";
         else if ( team.IsEnemy( Combat.LocalPlayerTeam ) )
            teamName = "Allies";
         else
            teamName = "NPC";
         teamName += "\t";
         if ( who.GetPilot() != null ) 
            teamName += who.GetPilot().Callsign;
         else if ( who is AbstractActor actor )
            teamName += actor.Nickname;
         else
            teamName += who.DisplayName;
         teamName += "\t";
         return teamName + who.DisplayName + "\t";
      }

      // ============ Attack Log ============

      internal static string thisSequence = "";
      internal static string thisSequenceId = "";
      internal static string thisSequenceTargetId = "";

      public static void RecordAttack ( AttackDirector.AttackSequence __instance ) {
         AttackDirector.AttackSequence me = __instance;
         string time = DateTime.Now.ToString( "s" );
         AttackDirection direction = Combat.HitLocation.GetAttackDirection( me.attackPosition, me.target );
         float range = ( me.attackPosition - me.target.CurrentPosition ).magnitude;
         thisSequenceTargetId = me.target.GUID;

         thisSequence = 
            time + "\t" + 
            TeamAndCallsign( me.attacker ) +         // Attacker team, pilot, mech
            TeamAndCallsign( me.target ) +           // Target team, pilot, mech
            thisCombatId + "\t" +                    // Combat Id
            thisSequenceId + "\t" +                  // Attack Id
            direction + "\t" +
            range;
         if ( ! LogShot )
            log.Add( thisSequence );
      }

      // ============ Shot Log ============

      internal static string thisWeapon = "";
      internal static string thisWeaponName = "";
      internal static float thisHitChance;

      public static void RecordSequenceWeapon ( AttackDirector.AttackSequence __instance, Weapon weapon, float toHitChance ) {
         thisHitChance = toHitChance;
         thisWeapon = weapon.GUID;
         thisWeaponName = ( weapon.defId.StartsWith( "Weapon_" ) ? weapon.defId.Substring( 7 ) : weapon.defId );
         //Log( $"GetIndividualHits & GetClusteredHits = {thisWeaponName} {thisWeapon}" );
      }

      internal static float thisRoll;
      internal static float thisStreak;

      public static void RecordAttackRoll ( float roll, Team team ) {
         //Log( "Roll = " + roll );
         thisRoll = roll;
         thisStreak = team?.StreakBreakingValue ?? 0;
      }

      internal static string GetShotLog () {
         return thisSequence + "\t" + thisWeaponName + "\t" + thisRoll + "\t" + ( thisCorrectedRoll + thisStreak ) + "\t" + thisStreak + "\t" + thisCorrectedRoll + "\t" + thisHitChance;
      }

      internal static float thisCorrectedRoll;

      public static void LogMissedAttack ( float __result, float roll, Team team ) {
         thisCorrectedRoll = __result;
         bool miss = __result > thisHitChance;
         if ( miss || ! LogLocation ) { // If miss, log now because hit location won't be rolled
            StringBuilder logBuffer = new StringBuilder();
            logBuffer.Append( GetShotLog() );
            if ( LogLocation ) {
               //Log( "MISS" );
               logBuffer.Append( "\t--" + // Location Roll
                                 "\t--\t--\t--\t--" +  // Head & Torsos
                                 "\t--\t--\t--\t--" + // Limbs
                                 "\t--\t--\t(Miss)" );   // Called shot and result
               if ( LogDamage )
                  logBuffer.Append( DamageDummy );
               if ( LogCritical )
                  logBuffer.Append( CritDummy );
            } else
               logBuffer.Append( miss ? "\t(Miss)" : "\t(Hit)" );
            log.Add( logBuffer.ToString() );
         }
      }

      // ============ Location Log ============

      public static void LogMechHit ( ArmorLocation __result, Dictionary<ArmorLocation, int> hitTable, float randomRoll, ArmorLocation bonusLocation, float bonusLocationMultiplier ) {
         LogHitSequence( MechStructureRules.GetChassisLocationFromArmorLocation( __result ), randomRoll, bonusLocation, bonusLocationMultiplier, true,
            TryGet( hitTable, ArmorLocation.Head ) + "\t" +
            ( TryGet( hitTable, ArmorLocation.CenterTorso ) + TryGet( hitTable, ArmorLocation.CenterTorsoRear ) ) + "\t" +
            ( TryGet( hitTable, ArmorLocation.LeftTorso   ) + TryGet( hitTable, ArmorLocation.LeftTorsoRear   ) ) + "\t" +
            ( TryGet( hitTable, ArmorLocation.RightTorso  ) + TryGet( hitTable, ArmorLocation.RightTorsoRear  ) ) + "\t" +
            TryGet( hitTable, ArmorLocation.LeftArm  ) + "\t" +
            TryGet( hitTable, ArmorLocation.RightArm ) + "\t" +
            TryGet( hitTable, ArmorLocation.LeftLeg  ) + "\t" +
            TryGet( hitTable, ArmorLocation.RightLeg ) );
      }

      public static void LogVehicleHit ( VehicleChassisLocations __result, Dictionary<VehicleChassisLocations, int> hitTable, float randomRoll, VehicleChassisLocations bonusLocation, float bonusLocationMultiplier ) {
         LogHitSequence( __result, randomRoll, bonusLocation, bonusLocationMultiplier, false,
            TryGet( hitTable, VehicleChassisLocations.Turret ) + "\t" +
            TryGet( hitTable, VehicleChassisLocations.Front  ) + "\t" +
            TryGet( hitTable, VehicleChassisLocations.Left   ) + "\t" +
            TryGet( hitTable, VehicleChassisLocations.Right  ) + "\t" +
            TryGet( hitTable, VehicleChassisLocations.Rear   ) + "\t--\t--\t--" );
      }

      public static void LogBuildingHit ( int __result, float hitLocationRoll, BuildingLocation calledShotLocation, float bonusMultiplier ) {
         LogHitSequence( BuildingLocation.Structure, hitLocationRoll, calledShotLocation, bonusMultiplier, false, "1\t--\t--\t--\t--\t--\t--\t--" );
      }

      public static void LogBuildingClusterHit ( int __result, float randomRoll ) {
         LogHitSequence( BuildingLocation.Structure.ToString(), randomRoll, "None", 0, false, "1\t--\t--\t--\t--\t--\t--\t--" );
      }

      private static void LogHitSequence<T,C> ( T hitLocation, float randomRoll, C bonusLocation, float bonusLocationMultiplier, bool canCrit, string line ) { try {
         line = GetShotLog() + "\t" + randomRoll + "\t" + line + "\t" + bonusLocation + "\t" + bonusLocationMultiplier + "\t" + hitLocation;
         //Log( "HIT " + GetShotLog() + "\t" + hitLocation + " >>> " + log.Count );
         if ( LogDamage ) {
            hitList.Add( log.Count );
            line += DamageDummy;
            if ( LogCritical ) {
               line += CritDummy;
               if ( canCrit ) {
                  string key = GetHitKey( thisWeapon, hitLocation, thisSequenceTargetId );
                  //Log( "Hit key = " + key );
                  hitMap[ key ] = log.Count;
               }
            }
         }
         log.Add( line );
      }                 catch ( Exception ex ) { Error( ex ); } }

      // ============ Damage Log ============

      private const string DamageDummy = 
                              "\t--\t--" +         // Damage and Last Location
                              "\t--\t--\t--\t--";  // Location Armour and HP

      private static float? thisDamage = null;
      private static string lastLocation;
      private static float beforeArmour;
      private static float beforeStruct;
      private static bool damageResolved;

      public static void RecordMechDamage ( Mech __instance, ArmorLocation aLoc, float totalDamage ) {
         if ( aLoc == ArmorLocation.None || aLoc == ArmorLocation.Invalid ) return;
         RecordUnitDamage( aLoc.ToString(), totalDamage,
            __instance.GetCurrentArmor( aLoc ), __instance.GetCurrentStructure( MechStructureRules.GetChassisLocationFromArmorLocation( aLoc ) ) );
      }

      public static void RecordVehicleDamage ( Vehicle __instance, VehicleChassisLocations vLoc, float totalDamage ) {
         if ( vLoc == VehicleChassisLocations.None || vLoc == VehicleChassisLocations.Invalid ) return;
         RecordUnitDamage( vLoc.ToString(), totalDamage, __instance.GetCurrentArmor( vLoc ), __instance.GetCurrentStructure( vLoc ) );
      }

      public static void RecordTurretDamage ( Turret __instance, BuildingLocation bLoc, float totalDamage ) {
         if ( bLoc == BuildingLocation.None || bLoc == BuildingLocation.Invalid ) return;
         RecordUnitDamage( bLoc.ToString(), totalDamage, __instance.GetCurrentArmor( bLoc ), __instance.GetCurrentStructure( bLoc ) );
      }

      public static void RecordBuildingDamage ( BattleTech.Building __instance, float totalDamage ) {
         RecordUnitDamage( BuildingLocation.Structure.ToString(), totalDamage, 0, __instance.CurrentStructure );
      }

      private static void RecordUnitDamage ( string loc, float totalDamage, float armour, float structure ) {
         //Log( $"{totalDamage} Damage @ {loc}" );
         lastLocation = loc;
         if ( thisDamage == null ) thisDamage = -totalDamage;
         beforeArmour = armour;
         beforeStruct = structure;
         damageResolved = false;
      }

      public static void LogMechDamage ( Mech __instance, ArmorLocation aLoc ) {
         if ( aLoc == ArmorLocation.None || aLoc == ArmorLocation.Invalid ) return;
         LogActorDamage( __instance.GetCurrentArmor( aLoc ), __instance.GetCurrentStructure( MechStructureRules.GetChassisLocationFromArmorLocation( aLoc ) ) );
      }

      public static void LogVehicleDamage ( Vehicle __instance, VehicleChassisLocations vLoc ) {
         if ( vLoc == VehicleChassisLocations.None || vLoc == VehicleChassisLocations.Invalid ) return;
         LogActorDamage( __instance.GetCurrentArmor( vLoc ), __instance.GetCurrentStructure( vLoc ) );
      }

      public static void LogTurretDamage ( Turret __instance, BuildingLocation bLoc ) {
         if ( bLoc == BuildingLocation.None || bLoc == BuildingLocation.Invalid ) return;
         LogActorDamage( __instance.GetCurrentArmor( bLoc ), __instance.GetCurrentStructure( bLoc ) );
      }

      public static void LogBuildingDamage ( BattleTech.Building __instance ) {
         LogActorDamage( 0, __instance.CurrentStructure );
      }

      private static void LogActorDamage ( float afterArmour, float afterStruct ) { try {
         if ( damageResolved ) return;
         damageResolved = true;
         if ( hitList.Count <= 0 ) {
            Warn( "Damage Log cannot find matching hit record." );
            return;
         }
         string line = log[ hitList[0] ];
         if ( LogCritical )
            line = line.Substring( 0, line.Length - CritDummy.Length );

         line = line.Substring( 0, line.Length - DamageDummy.Length ) + "\t" +
               thisDamage   + "\t" + lastLocation + "\t" +
               beforeArmour + "\t" + afterArmour  + "\t" +
               beforeStruct + "\t" + afterStruct;

         if ( LogCritical )
            line += CritDummy;
         //Log( $"Log damage " + line );
         log[ hitList[0] ] = line;
         hitList.RemoveAt( 0 );
         //Log( $"Hit list remaining: {hitList.Count}" );
         thisDamage = null;
      }                 catch ( Exception ex ) { Error( ex ); } }
      

      // ============ Crit Log ============

      private const string CritDummy = 
         "\t--" +             // Max HP
         "\t--\t--\t--\t--" + // Crit Roll and %
         "\t--\t--\t--" +     // Slot info
         "\t--\t--";          // Crit Result

      private static float thisCritRoll, thisCritSlotRoll;
      public static void RecordCritRolls ( float[] __result, int amount ) {
         if ( amount == 2 ) {
            //Log( $"Crit Roll = {__result[0]}" );
            thisCritRoll = __result[0];
            thisCritSlotRoll = __result[1];
         }
      }

      private static float thisBaseCritChance, thisCritMultiplier, thisCritChance, thisLocationMaxHP;
      public static void RecordBaseCritChance ( float __result, Mech target, ChassisLocations hitLocation ) {
         thisBaseCritChance = __result;
         //thisLocationHP = target.GetCurrentStructure( hitLocation );
         thisLocationMaxHP = target.GetMaxStructure( hitLocation );
         //Log( $"Location Max HP = {thisLocationMaxHP}" );
      }
      public static void RecordCritMultiplier ( float __result ) {
         thisCritMultiplier = __result;
      }
      public static void RecordCritChance ( float __result ) {
         thisCritChance = __result;
         thisCritComp = null;
      }

      private static int thisCritSlot;
      private static MechComponent thisCritComp = null;
      private static ComponentDamageLevel thisCompBefore;
      private static bool halfFullAmmo = false;

      public static void RecordCritComp ( MechComponent __result, ChassisLocations location, int index ) {
         if ( thisCritComp == __result ) return;
         //Log( $"Record Crit Comp @ {location} = {__result.UIName}" );
         thisCritSlot = index;
         thisCritComp = __result;
         if ( __result != null ) {
            if ( __result is AmmunitionBox box )
               halfFullAmmo = ( (float)box.CurrentAmmo / (float)box.ammunitionBoxDef.Capacity ) >= 0.5f;
            thisCompBefore = __result.DamageLevel;
         }
      }

      public static void LogCritResult ( Mech __instance, ChassisLocations location, Weapon weapon ) { try {
         string key = GetHitKey( weapon.GUID, location, __instance.GUID );
         //Log( "Crit " + DateTime.Now.ToString( "s" ) + "\t" + weapon.defId + "\t" + __instance.GetPilot().Callsign + "\t" + location );
         if ( ( ! hitMap.TryGetValue( key, out int lineIndex ) ) ) {
            Warn( "Critical Hit Log cannot find matching hit record: " + key );
            return;
         }
         string line = log[ lineIndex ];
         if ( ! line.EndsWith( CritDummy ) ) {
            Warn( "Critical Hit Log found duplicate crit: " + key );
            return;
         }
         string critLine = "\t" + thisLocationMaxHP +
                           "\t" + thisCritRoll +
                           "\t" + thisBaseCritChance +
                           "\t" + thisCritMultiplier +
                           "\t" + thisCritChance;
         if ( thisCritSlot < 0 )
            critLine += "\t--\t--\t(No Crit)\t--\t--";
         else {
            critLine += "\t" + thisCritSlotRoll +
                        "\t" + ( thisCritSlot + 1 );
            if ( thisCritComp == null )
               critLine += "\t(Empty)\t--\t--";
            else {
               string thisCompAfter = thisCritComp is AmmunitionBox && halfFullAmmo ? "Explosion" : thisCritComp.DamageLevel.ToString();
               critLine += "\t" + thisCritComp.UIName +
                           "\t" + thisCompBefore +
                           "\t" + thisCompAfter;
            }
         }
         //Log( $"Crit Line = {critLine}" );
         line = line.Substring( 0, line.Length - CritDummy.Length ) + critLine;
         log[ lineIndex ] = line;
         thisCritSlot = -1;
         thisCritComp = null;
         halfFullAmmo = false;
      }                 catch ( Exception ex ) { Error( ex ); } }
   }
}