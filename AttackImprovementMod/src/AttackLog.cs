using System;

namespace Sheepy.AttackImprovementMod {
   using static Mod;
   using static FixHitLocation;
   using System.Reflection;
   using System.IO;
   using BattleTech;
   using System.Collections.Generic;

   class AttackLog {

      internal const string ROLL_LOG = "Log_AttackRoll.txt";

      internal static void InitPatch () {
         if ( Mod.Settings.LogHitRolls ) {
            GetHitLocation = typeof( HitLocation ).GetMethod( "GetHitLocation", BindingFlags.Public | BindingFlags.Static ); // Only one public static GetHitLocation method.
            if ( GetHitLocation.GetParameters().Length != 4 || GetHitLocation.GetParameters()[1].ParameterType != typeof( float ) || GetHitLocation.GetParameters()[3].ParameterType != typeof( float ) ) {
               Log( "Error: Cannot patch HitLocation.GetHitLocation( ?, float, ?, float ). Called shot modding and attack logging disabled." );
               return;
            }

            Type AttackType = typeof( AttackDirector.AttackSequence );
            if ( ! Mod.Settings.PersistentLog ) DeleteLog( ROLL_LOG );
            Patch( AttackType, "GetIndividualHits", BindingFlags.NonPublic | BindingFlags.Instance, "RecordAttacker", null );
            Patch( AttackType, "GetClusteredHits" , BindingFlags.NonPublic | BindingFlags.Instance, "RecordAttacker", null );
            Patch( AttackType, "GetCorrectedRoll" , BindingFlags.NonPublic | BindingFlags.Instance, new Type[]{ typeof( float ), typeof( Team ) }, "RecordAttackRoll", "LogMissedAttack" );
            MethodInfo MechGetHit    = GetHitLocation.MakeGenericMethod( typeof( ArmorLocation ) ),
                       VehicleGetHit = GetHitLocation.MakeGenericMethod( typeof( VehicleChassisLocations ) );
            Patch( MechGetHit, null, "LogMechHit" );
            Patch( VehicleGetHit, null, "LogVehicleHit" );
            if ( ! File.Exists( ROLL_LOG ) )
               RollLog( String.Join( "\t", new string[]{ "Attacker", "Weapon", "Hit Roll", "Corrected", "Streak", "Final", "To Hit", "Location Roll", "Head/Turret", "CT/Front", "LT/Left", "RT/Right", "LA/Rear", "RA", "LL", "RL", "Called Part", "Called Bonus", "Total Weight", "Goal", "Hit Location" } ) );
         }
      }

      // ============ UTILS ============

      internal static void RollLog ( String message ) {
         WriteLog( ROLL_LOG, message + "\r\n" );
      }

      // ============ Log ============

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

      internal static float thisCorrectedRoll;
      public static void LogMissedAttack ( float __result, float roll, Team team ) {
         thisCorrectedRoll = __result;
         if ( __result > thisHitChance ) // Miss, log now because hit location won't be rolled
            RollLog( GetHitLog() +
               "\t--" + // Roll; Empty cells are added so that copy and paste will override any old data in Excel, instead of leaving them in place and make it confusing
               "\t--\t--\t--\t--" +  // Head & Torsos
               "\t--\t--\t--\t--" +  // Limbs
               "\t--\t--\t--\t--" ); // called shot and result
      }

      public static void LogMechHit ( ArmorLocation __result, Dictionary<ArmorLocation, int> hitTable, float randomRoll, ArmorLocation bonusLocation, float bonusLocationMultiplier ) { try {
         // "Location Roll", "Head/Turret", "CT/Front", "LT/Left", "RT/Right", "LA/Rear", "RA", "LL", "RL", "Called Part", "Called Bonus", "Total Weight", "Goal", "Hit Location"
         int totalWeight = SumWeight( hitTable, bonusLocation, bonusLocationMultiplier, scale );
         RollLog(
               GetHitLog() + "\t" +
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
               totalWeight + "\t" +
               (int)( randomRoll * totalWeight ) + "\t" +
               __result );
      } catch ( Exception ex ) { Log( ex ); } }

      public static void LogVehicleHit ( VehicleChassisLocations __result, Dictionary<VehicleChassisLocations, int> hitTable, float randomRoll, VehicleChassisLocations bonusLocation, float bonusLocationMultiplier ) { try {
         // "Location Roll", "Head/Turret", "CT/Front", "LT/Left", "RT/Right", "LA/Rear", "RA", "LL", "RL", "Called Part", "Called Bonus", "Total Weight", "Goal", "Hit Location"
         int totalWeight = SumWeight( hitTable, bonusLocation, bonusLocationMultiplier, scale );
         RollLog(
               GetHitLog() + "\t" +
               randomRoll + "\t" +
               TryGet( hitTable, VehicleChassisLocations.Turret ) + "\t" +
               TryGet( hitTable, VehicleChassisLocations.Front  ) + "\t" +
               TryGet( hitTable, VehicleChassisLocations.Left   ) + "\t" +
               TryGet( hitTable, VehicleChassisLocations.Right  ) + "\t" +
               TryGet( hitTable, VehicleChassisLocations.Rear   ) + "\t" +
               "\t" +
               "\t" +
               "\t" +
               bonusLocation + "\t" +
               bonusLocationMultiplier + "\t" +
               totalWeight + "\t" +
               (int)( randomRoll * totalWeight ) + "\t" +
               __result );
      } catch ( Exception ex ) { Log( ex ); } }

      internal static string GetHitLog () {
         return thisAttacker + "\t" + thisWeapon + "\t" + thisRoll + "\t" + ( thisCorrectedRoll + thisStreak ) + "\t" + thisStreak + "\t" + thisCorrectedRoll + "\t" + thisHitChance;
      }
   }
}
