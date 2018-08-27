using BattleTech;
using Harmony;
using Localize;
using System.Reflection;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sheepy.BattleTechMod.AttackImprovementMod {
   using static Mod;
   using static System.Reflection.BindingFlags;

   public class Criticals : BattleModModule {

      private static Type MechType = typeof( Mech );
      private static MethodInfo CheckForCrit;

      private static float ThroughArmorCritThreshold = 0, ThroughArmorCritThresholdPerc = 0, ThroughArmorBaseCritChance, ThroughArmorVarCritChance;

      public override void CombatStartsOnce () {
         MethodInfo ResolveWeaponDamage = MechType.GetMethod( "ResolveWeaponDamage", new Type[]{ typeof( WeaponHitInfo ), typeof( Weapon ), typeof( MeleeAttackType ) } );

         if ( Settings.SkipCritingDeadMech )
            Patch( ResolveWeaponDamage, "Skip_BeatingDeadMech", null );

         if ( Settings.ThroughArmorCritChanceZeroArmor > 0 && HasCheckForCrit() ) {
            if ( Settings.FixFullStructureCrit ) {
               Warn( "FullStructureCrit disabled because ThroughArmorCritical is enabled, meaning full structure can be crit'ed." );
               Settings.FixFullStructureCrit = false;
            }
            armoured = new Dictionary<ArmorLocation, float>();
            damages = new Dictionary<ArmorLocation, float>();
            Patch( ResolveWeaponDamage, "AddThroughArmorCritical", null );
            Patch( typeof( WeaponHitInfo ), "ConsolidateCriticalHitInfo", "Override_ConsolidateCriticalHitInfo", null );
            Patch( typeof( CritChanceRules ), "GetCritChance" , "GetAIMCritChance", null );
            ThroughArmorBaseCritChance = (float) Settings.ThroughArmorCritChanceFullArmor;
            ThroughArmorVarCritChance = (float) Settings.ThroughArmorCritChanceZeroArmor - ThroughArmorBaseCritChance;
            if ( Settings.ThroughArmorCritThreshold > 1 )
               ThroughArmorCritThreshold = (float) Settings.ThroughArmorCritThreshold;
            else
               ThroughArmorCritThresholdPerc = (float)Settings.ThroughArmorCritThreshold;
            //Info( "ThroughArmorCritChance is {0:##0}% to {1:##0}%.", ThroughArmorBaseCritChance * 100, ThroughArmorVarCritChance * 100 );
            if ( Settings.ThroughArmorCritThreshold > 0 && ! Settings.CritFollowDamageTransfer )
               Warn( "Disabling CritFollowDamageTransfer will impact ThroughArmorCritThreshold calculation." );

         } else if ( Settings.FixFullStructureCrit ) {
            Patch( ResolveWeaponDamage, "RecordCritMech", "ClearCritMech" );
            Patch( typeof( WeaponHitInfo ), "ConsolidateCriticalHitInfo", null, "RemoveFullStructureLocationsFromCritList" );
         }

         if ( Settings.CritFollowDamageTransfer ) {
            Patch( MechType, "TakeWeaponDamage", "RecordHitInfo", "ClearHitInfo" );
            Patch( MechType, "DamageLocation", NonPublic, "UpdateCritLocation", null );
         }
      }

      private static bool HasCheckForCrit () { try {
         if ( CheckForCrit != null ) return true;
         CheckForCrit = MechType.GetMethod( "CheckForCrit", NonPublic | Instance );
         if ( CheckForCrit == null ) Warn( "Mech.CheckForCrit not found. One or more crit features disabled." );
         return CheckForCrit != null;
      } catch ( Exception ex ) {
         Error( ex );
         return false;
      } }

      [ HarmonyPriority( Priority.High ) ]
      public static bool Skip_BeatingDeadMech ( Mech __instance ) {
         if ( __instance.IsFlaggedForDeath || __instance.IsDead ) return false;
         return true;
      }

      // ============ ThroughArmorCritical ============

      private static Dictionary<ArmorLocation, float> armoured, damages;
      private static Dictionary<int, float> damaged;
      private static ArmorLocation? ThroughArmor;

      // Consolidate critical hit info and split into armoured and structurally damaged locations.
      private static void SplitCriticalHitInfo ( Mech mech, WeaponHitInfo info, Func<float> damageFunc ) {
         if ( armoured == null || damages == null ) return;

         int i = 0, len = info.numberOfShots;
         if ( ThroughArmorCritThreshold > 0 || ThroughArmorCritThresholdPerc > 0 ) {
            float damage = damageFunc();
            for ( ; i < len ; i++ ) {
               ArmorLocation key = (ArmorLocation) info.hitLocations[i];
               damages.TryGetValue( key, out float allDamage );
               allDamage += damage;
               damages[key] = allDamage;
            }
         } else {
            for ( ; i < len ; i++ )
               damages[ (ArmorLocation) info.hitLocations[i] ] = 1;
         }

         armoured.Clear();
         damaged = new Dictionary<int, float>();
         //Verbo( "SplitCriticalHitInfo found {0} hit locations.", damages.Count );
         foreach ( var damage in damages ) {
            ArmorLocation armour = damage.Key;
            if ( armour == ArmorLocation.None || armour == ArmorLocation.Invalid ) continue;
            ChassisLocations location = MechStructureRules.GetChassisLocationFromArmorLocation( armour );
            if ( mech.IsLocationDestroyed( location ) ) continue;
            if ( mech.GetCurrentArmor( armour ) <= 0 && mech.GetCurrentStructure( location ) < mech.GetMaxStructure( location ) )
               damaged.Add( (int) armour, damage.Value );
            else if ( damage.Value > ThroughArmorCritThreshold
                      && ( ThroughArmorCritThresholdPerc == 0 || damage.Value > ThroughArmorCritThresholdPerc * mech.GetMaxArmor( armour ) ) )
               armoured.Add( armour, damage.Value );
            //else
            //   Verbo( "{0} damage ({1}) on {2} not reach threshold {3} & {4}%", armour, damage.Value, mech.DisplayName, ThroughArmorCritThreshold, ThroughArmorCritThresholdPerc*100 );
         }
         damages.Clear();
      }

      public static bool Override_ConsolidateCriticalHitInfo ( ref Dictionary<int, float> __result ) {
         if ( damaged == null ) return true;
         //foreach ( int i in damaged.Keys ) Verbo( "Crit list: Damaged {0}", (ArmorLocation) i );
         __result = damaged; // Use the result from SplitCriticalHitInfo
         damaged = null;
         return false;
      }

      public static void AddThroughArmorCritical ( Mech __instance, WeaponHitInfo hitInfo, Weapon weapon, MeleeAttackType meleeAttackType ) { try {
         Mech mech = __instance;

         SplitCriticalHitInfo( mech, hitInfo, () => {
            float damage = weapon.parent == null ? weapon.DamagePerShot : weapon.DamagePerShotAdjusted( weapon.parent.occupiedDesignMask );
            AbstractActor abstractActor = mech.Combat.FindActorByGUID(hitInfo.attackerId);
            LineOfFireLevel lineOfFireLevel = abstractActor.VisibilityCache.VisibilityToTarget( mech ).LineOfFireLevel;
            return mech.GetAdjustedDamage( damage, weapon.Category, mech.occupiedDesignMask, lineOfFireLevel, false );
         } );

         if ( armoured == null || armoured.Count <= 0 ) return;


         foreach ( var damagedArmour in armoured ) {
            ThroughArmor = damagedArmour.Key;
            CheckForCrit.Invoke( mech, new object[]{ hitInfo, 0, weapon } ); // After its GetCritChance transpiled to our own GetCritChance
            //CheckThroughArmourCrit( mech, hitInfo, damagedArmour.Key, weapon );
         }
         ThroughArmor = null;
      }                 catch ( Exception ex ) { Error( ex ); } }

      public static bool GetAIMCritChance ( ref float __result, ICombatant target, ChassisLocations hitLocation, Weapon weapon, bool shouldLog = false ) {
         if ( ThroughArmor == null ) return true;
         __result = GetThroughArmourCritChance( target, ThroughArmor.GetValueOrDefault(), weapon );
         return false;
      }

      //float critChance = GetThroughArmourCritChance( mech, armour, weapon );

      public static float GetThroughArmourCritChance ( ICombatant target, ArmorLocation hitLocation, Weapon weapon ) {
         bool logCrit = CritChanceRules.attackLogger.IsDebugEnabled;
         if ( target.StatCollection.GetValue<bool>( "CriticalHitImmunity" ) ) {
            if ( logCrit ) CritChanceRules.attackLogger.LogDebug( string.Format( "[GetCritChance] CriticalHitImmunity!", new object[ 0 ] ) );
            return 0;
         }
         float chance = 0, critMultiplier = 0;
         if ( target is Mech )
            chance = GetThroughArmourBaseCritChance( (Mech) target, hitLocation );
         if ( chance > 0 ) {
            //chance = Mathf.Max( change, CombatConstants.ResolutionConstants.MinCritChance ); // Min Chance does not apply to TAC
            critMultiplier = Combat.CritChance.GetCritMultiplier( target, weapon, true );
            if ( logCrit ) CritChanceRules.attackLogger.LogDebug( string.Format( "[GetCritChance] base = {0}, multiplier = {1}!", chance, critMultiplier ) );
         }
         float result = chance * critMultiplier;
         AttackLog.LogCritChance( result );
         return result;
      }

      public static float GetThroughArmourBaseCritChance ( Mech target, ArmorLocation hitLocation ) {
         if ( CritChanceRules.attackLogger.IsDebugEnabled )
            CritChanceRules.attackLogger.LogDebug( string.Format( "Location Current Armour = {0}, Location Max Armour = {1}", target.GetCurrentArmor( hitLocation ), target.GetMaxArmor( hitLocation ) ) );
         float curr = target.GetCurrentArmor( hitLocation ), max = target.GetMaxArmor( hitLocation ), armorPercentage = curr / max;
         float result = ThroughArmorBaseCritChance + ( 1f - armorPercentage ) * ThroughArmorVarCritChance;
         AttackLog.LogThroughArmourCritChance( result, max );
         return result;
      }

      // ============ FixFullStructureCrit ============

      private static Mech thisCritMech;

      public static void RecordCritMech ( Mech __instance ) {
         thisCritMech = __instance;
      }

      public static void ClearCritMech () {
         thisCritMech = null;
      }

      public static void RemoveFullStructureLocationsFromCritList ( Dictionary<int, float> __result ) { try {
         if ( thisCritMech == null ) return;
         HashSet<int> removeList = new HashSet<int>();
         __result.Remove( (int) ArmorLocation.None );
         __result.Remove( (int) ArmorLocation.Invalid );
         foreach ( int armourInt in __result.Keys ) {
            ChassisLocations location = MechStructureRules.GetChassisLocationFromArmorLocation( (ArmorLocation) armourInt );
            float curr = thisCritMech.StructureForLocation( (int) location ), max = thisCritMech.MaxStructureForLocation( (int) location );
            if ( curr == max ) removeList.Add( armourInt );
         }
         foreach ( ChassisLocations location in removeList ) {
            Verbo( "Prevented {0} crit on {1} because it is not structurally damaged.", location, thisCritMech.DisplayName );
            __result.Remove( (int) location );
         }
      }                 catch ( Exception ex ) { Error( ex ); } }

      // ============ CritFollowDamageTransfer ============

      private static int[] thisHitLocations;
      private static int thisHitIndex;

      public static void RecordHitInfo ( WeaponHitInfo hitInfo, int hitIndex ) {
         thisHitLocations = hitInfo.hitLocations;
         thisHitIndex = hitIndex;
      }

      public static void ClearHitInfo () {
         thisHitLocations = null;
      }

      // Update hit location so that it will be consolidated by ConsolidateCriticalHitInfo
      public static void UpdateCritLocation ( ArmorLocation aLoc ) {
         if ( thisHitLocations == null ) return;
         if ( thisHitIndex < 0 || thisHitIndex >= thisHitLocations.Length ) return;
         thisHitLocations[ thisHitIndex ] = (int) aLoc;
      }

   }
}