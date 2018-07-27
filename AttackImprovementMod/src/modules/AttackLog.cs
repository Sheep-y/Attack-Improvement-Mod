using BattleTech;
using Harmony;
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
      private static string Separator = ",";

      private static readonly Type AttackType = typeof( AttackDirector.AttackSequence );
      private static readonly Type ArtilleyAttackType = typeof( ArtillerySequence );

      private static readonly Type MechType = typeof( Mech );
      private static readonly Type VehiType = typeof( Vehicle );
      private static readonly Type TurtType = typeof( Turret );
      private static readonly Type BuldType = typeof( BattleTech.Building );

      public override void ModStarts () {
         if ( Settings.AttackLogLevel == null ) return;
         PersistentLog = Settings.PersistentLog;

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
               Separator = Separator;
               break;
         }

         // Patch prefix early to increase chance of successful capture in face of other mods.
         // TODO: Move to CombatStartsOnce and merge code.
         switch ( Settings.AttackLogLevel.Trim().ToLower() ) {
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
               Patch( AttackType, "GetIndividualHits", NonPublic, "RecordSequenceWeapon", null );
               Patch( AttackType, "GetClusteredHits" , NonPublic, "RecordSequenceWeapon", null );
               Patch( AttackType, "GetCorrectedRoll" , NonPublic, "RecordAttackRoll", null );
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
         ROLL_LOG = new Logger( ModLogDir + "Log_Attack." + Settings.AttackLogFormat );
         idGenerator = new Random();
         thisSequenceId = GetNewId();

         if ( ! PersistentLog )
            ROLL_LOG.Delete();

         if ( ! ROLL_LOG.Exists() ) {
            StringBuilder logBuffer = new StringBuilder();
            logBuffer.Append( String.Join( Separator, new string[]{ "Time", "Actor", "Pilot", "Unit", "Target", "Pilot", "Unit", "Combat Id", "Attack Id", "Direction", "Range" } ) );
            if ( LogShot || PersistentLog ) {
               logBuffer.Append( Separator ).Append( String.Join( Separator, new string[]{ "Weapon", "Weapon Template", "Weapon Id", "Hit Roll", "Corrected", "Streak", "Final", "Hit%" } ) );
               if ( LogLocation || PersistentLog )
                  logBuffer.Append( Separator ).Append( String.Join( Separator, new string[]{ "Location Roll", "Head", "CT", "LT", "RT", "LA", "RA", "LL", "RL", "Called Part", "Called Multiplier" } ) );
               logBuffer.Append( Separator ).Append( "Hit Location" );
            }
            if ( LogDamage || PersistentLog )
               logBuffer.Append( Separator ).Append( String.Join( Separator, new string[]{ "Damage", "Stops At", "From Armor", "To Armor", "From HP", "To HP" } ) );
            if ( LogCritical || PersistentLog )
               logBuffer.Append( Separator ).Append( String.Join( Separator, new string[]{ "Max HP", "Crit Roll", "Base Crit%", "Crit Multiplier", "Crit%", "Slot Roll", "Crit Slot", "Crit Equipment", "From State", "To State" } ) );
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
         if ( idGenerator == null ) return;
         thisCombatId = GetNewId();

         if ( LoggerPatched ) return;
         LoggerPatched = true;

         // Patch Postfix late to increase odds of capturing modded values
         if ( LogShot )
            Patch( AttackType, "GetCorrectedRoll" , NonPublic, null, "LogMissedAttack" );

         if ( LogLocation ) {
            Patch( GetHitLocation( typeof( ArmorLocation ) ), null, "LogMechHit" );
            Patch( GetHitLocation( typeof( VehicleChassisLocations ) ), null, "LogVehicleHit" );
            Patch( TurtType, "GetHitLocation", new Type[]{ typeof( AbstractActor ), typeof( UnityEngine.Vector3 ), typeof( float ), typeof( ArmorLocation ), typeof( float ) }, null, "LogBuildingHit" );
            Patch( BuldType, "GetHitLocation", new Type[]{ typeof( AbstractActor ), typeof( UnityEngine.Vector3 ), typeof( float ), typeof( ArmorLocation ), typeof( float ) }, null, "LogBuildingHit" );
            Patch( TurtType, "GetAdjacentHitLocation", null, "LogBuildingClusterHit" );
            Patch( BuldType, "GetAdjacentHitLocation", null, "LogBuildingClusterHit" );
         }

         if ( LogDamage ) {
            DamageDummy = FillBlanks( 6 );
            Patch( MechType, "DamageLocation", NonPublic, null, "LogMechDamage" );
            Patch( VehiType, "DamageLocation", NonPublic, null, "LogVehicleDamage" );
            Patch( TurtType, "DamageLocation", NonPublic, null, "LogTurretDamage" );
            Patch( BuldType, "DamageBuilding", NonPublic, null, "LogBuildingDamage" );
         }

         if ( LogCritical ) {
            CritDummy = FillBlanks( 10 );
            Type CritRulesType = typeof( CritChanceRules );
            Patch( typeof( AttackDirector ), "GetRandomFromCache", new Type[]{ typeof( WeaponHitInfo ), typeof( int ) }, null, "LogCritRolls" );
            Patch( CritRulesType, "GetBaseCritChance", new Type[]{ MechType, typeof( ChassisLocations ), typeof( bool ) }, null, "LogBaseCritChance" );
            Patch( CritRulesType, "GetCritMultiplier", null, "LogCritMultiplier" );
            Patch( CritRulesType, "GetCritChance", null, "LogCritChance" );
            Patch( MechType, "GetComponentInSlot", null, "LogCritComp" );
            Patch( MechType, "CheckForCrit", NonPublic, null, "LogCritResult" );
         }
      }

      // ============ UTILS ============

      private static List<int> hitList; // Used to assign damage information
      private static Dictionary<string, int> hitMap; // Used to assign critical hit information
      private static List<string> log = new List<string>( 32 );

      [ HarmonyPriority( Priority.Last ) ]
      public static void WriteRollLog ( AttackDirector __instance ) {
         if ( __instance != null && __instance.IsAnyAttackSequenceActive )
            return; // Defer if Multi-Target is not finished
         ROLL_LOG.Log( String.Join( Environment.NewLine, log.ToArray() ) );
         log.Clear();
         hitList?.Clear();
         hitMap?.Clear();
         thisSequenceId = GetNewId();
         //Log( "Log written and HitMap Cleared\n" );
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

      private static string GetHitKey<T> ( string weapon, T hitLocation, string targetId ) {
         return weapon + "@" + hitLocation + "@" + targetId;
      }

      private static string FillBlanks ( int blankCount ) { // TODO: Cache!
         StringBuilder buf = new StringBuilder( blankCount * 3 );
         while ( blankCount-- > 0 ) buf.Append( Separator ).Append( "--" );
         return buf.ToString();
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
         if ( who is Building ) {
            teamName += "Building";
         } else {
            // TODO: Merge to one line
            if ( who.GetPilot() != null ) 
               teamName += who.GetPilot().Callsign;
            else if ( who is AbstractActor actor )
               teamName += actor.Nickname;
            else
               teamName += who.DisplayName;
         }
         teamName += Separator;
         return teamName + who.DisplayName + Separator;
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
            TeamAndCallsign( attacker ) +         // Attacker team, pilot, mech
            TeamAndCallsign( target ) +           // Target team, pilot, mech
            thisCombatId + Separator +                    // Combat Id
            thisSequenceId + Separator +                  // Attack Id
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
         //Log( $"GetIndividualHits / GetClusteredHits / ArtillerySequence = {weapon?.UIName} {thisWeapon?.uid}" );
      }

      internal static float thisRoll;
      internal static float thisStreak;

      [ HarmonyPriority( Priority.First ) ]
      public static void RecordAttackRoll ( float roll, Team team ) {
         //Log( "Roll = " + roll );
         thisRoll = roll;
         thisStreak = team?.StreakBreakingValue ?? 0;
      }

      internal static string GetShotLog () {
         string weaponName = thisWeapon?.UIName?.Replace( " +", "+" );
         return thisSequence + Separator + weaponName + Separator + thisWeapon?.defId + Separator + thisWeapon?.uid + Separator + thisRoll + Separator + ( thisCorrectedRoll + thisStreak ) + Separator + thisStreak + Separator + thisCorrectedRoll + Separator + thisHitChance;
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
               //Log( "MISS" );
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
         LogHitSequence( BuildingLocation.Structure.ToString(), randomRoll, "None", 0, false, "1" + FillBlanks( 7 ) );
      }

      private static void LogHitSequence<T,C> ( T hitLocation, float randomRoll, C bonusLocation, float bonusLocationMultiplier, bool canCrit, string line ) { try {
         line = GetShotLog() + Separator + randomRoll + Separator + line + Separator + bonusLocation + Separator + bonusLocationMultiplier + Separator + hitLocation;
         //Log( "HIT " + GetShotLog() + Separator + hitLocation + " >>> " + log.Count );
         if ( LogDamage ) {
            hitList.Add( log.Count );
            line += DamageDummy;
            if ( LogCritical ) {
               line += CritDummy;
               if ( canCrit ) {
                  string key = GetHitKey( thisWeapon?.uid, hitLocation, thisSequenceTargetId );
                  //Log( "Hit key = " + key );
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

      [ HarmonyPriority( Priority.First ) ]
      private static void RecordUnitDamage ( string loc, float totalDamage, float armour, float structure ) {
         //Log( $"{totalDamage} Damage @ {loc}" );
         lastLocation = loc;
         if ( thisDamage == null ) thisDamage = totalDamage;
         beforeArmour = armour;
         beforeStruct = structure;
         damageResolved = false;
      }

      [ HarmonyPriority( Priority.Last ) ]
      public static void LogMechDamage ( Mech __instance, ArmorLocation aLoc ) {
         if ( aLoc == ArmorLocation.None || aLoc == ArmorLocation.Invalid ) return;
         LogActorDamage( __instance.GetCurrentArmor( aLoc ), __instance.GetCurrentStructure( MechStructureRules.GetChassisLocationFromArmorLocation( aLoc ) ) );
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

      private static void LogActorDamage ( float afterArmour, float afterStruct ) { try {
         if ( damageResolved ) return;
         damageResolved = true;
         if ( hitList.Count <= 0 ) {
            Warn( "Damage Log cannot find matching hit record." );
            return;
         }
         string line = log[ hitList[0] ];
         if ( ( LogCritical && ! line.EndsWith( DamageDummy + CritDummy ) ) || ( ! LogCritical && ! line.EndsWith( DamageDummy ) ) ) {
            Warn( "Damage Log found an amended line, aborting." );
            hitList.RemoveAt( 0 );
            //Log( $"Hit list remaining: {hitList.Count}" );
            thisDamage = null;
            return;
         }

         if ( LogCritical )
            line = line.Substring( 0, line.Length - CritDummy.Length );
         line = line.Substring( 0, line.Length - DamageDummy.Length ) + Separator +
               thisDamage   + Separator + lastLocation + Separator +
               beforeArmour + Separator + afterArmour  + Separator +
               beforeStruct + Separator + afterStruct;
         if ( LogCritical )
            line += CritDummy;

         //Log( $"Log damage " + line );
         log[ hitList[0] ] = line;
         hitList.RemoveAt( 0 );
         //Log( $"Hit list remaining: {hitList.Count}" );
         thisDamage = null;
      }                 catch ( Exception ex ) { Error( ex ); } }
      

      // ============ Crit Log ============

      private static string CritDummy;

      private static float thisCritRoll, thisCritSlotRoll, thisBaseCritChance, thisCritMultiplier, thisCritChance, thisLocationMaxHP;
      private static bool checkCritComp = false;

      [ HarmonyPriority( Priority.Last ) ]
      public static void LogCritRolls ( float[] __result, int amount ) {
         if ( amount == 2 ) {
            //Log( $"Crit Roll = {__result[0]}" );
            thisCritRoll = __result[0];
            thisCritSlotRoll = __result[1];
            checkCritComp = true;
         }
      }

      [ HarmonyPriority( Priority.Last ) ]
      public static void LogBaseCritChance ( float __result, Mech target, ChassisLocations hitLocation ) {
         thisBaseCritChance = __result;
         //thisLocationHP = target.GetCurrentStructure( hitLocation );
         thisLocationMaxHP = target.GetMaxStructure( hitLocation );
         //Log( $"Location Max HP = {thisLocationMaxHP}" );
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

      private static int thisCritSlot;
      private static MechComponent thisCritComp = null;
      private static ComponentDamageLevel thisCompBefore;
      private static bool halfFullAmmo = false;

      [ HarmonyPriority( Priority.Last ) ]
      public static void LogCritComp ( MechComponent __result, ChassisLocations location, int index ) {
         if ( ! checkCritComp ) return;  // GetComponentInSlot is used in lots of places, and is better gated.
         //Log( $"Record Crit Comp @ {location} = {__result?.UIName}" );
         thisCritSlot = index;
         thisCritComp = __result;
         if ( __result != null ) {
            thisCompBefore = __result.DamageLevel;
            if ( __result is AmmunitionBox box )
               halfFullAmmo = box.CurrentAmmo > ( box.ammunitionBoxDef.Capacity / 2f );
         }
         checkCritComp = false;
      }

      [ HarmonyPriority( Priority.Last ) ]
      public static void LogCritResult ( Mech __instance, ChassisLocations location, Weapon weapon ) { try {
         string key = GetHitKey( weapon.uid, location, __instance.GUID );
         //Log( "Crit " + DateTime.Now.ToString( "s" ) + Separator + weapon.defId + Separator + __instance.GetPilot().Callsign + Separator + location );
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
               string thisCompAfter = thisCritComp is AmmunitionBox && halfFullAmmo ? "Explosion" : thisCritComp.DamageLevel.ToString();
               critLine += Separator + thisCritComp.UIName +
                           Separator + thisCompBefore +
                           Separator + thisCompAfter;
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