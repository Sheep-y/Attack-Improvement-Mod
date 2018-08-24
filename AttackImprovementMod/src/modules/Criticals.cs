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
            damaged = new Dictionary<int, float>();
            Patch( ResolveWeaponDamage, "ThroughArmorCritical", null );
            Patch( typeof( WeaponHitInfo ), "ConsolidateCriticalHitInfo", "Override_ConsolidateCriticalHitInfo", null );

         } else if ( Settings.FixFullStructureCrit ) {
            Patch( ResolveWeaponDamage, "RecordCritMech", "ClearCritMech" );
            Patch( typeof( WeaponHitInfo ), "ConsolidateCriticalHitInfo", null, "RemoveFullStructureLocationsFromCritList" );
         }

         if ( Settings.CritFollowDamageTransfer ) {
            Patch( MechType, "TakeWeaponDamage", "RecordHitInfo", "ClearHitInfo" );
            Patch( MechType, "DamageLocation", NonPublic, "UpdateCritLocation", null );
         }
      }

      public override void CombatStarts () {
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

      private static Dictionary<ArmorLocation, float> armoured;
      private static Dictionary<int, float> damaged;

      private static void SplitCriticalHitInfo ( Mech mech, WeaponHitInfo info, Func<float> damageFunc ) {
         if ( armoured == null || damaged == null ) return;
         
         Dictionary<ArmorLocation, float> damages = new Dictionary<ArmorLocation, float>();
         int i = 0, len = info.numberOfShots;
         if ( Settings.ThroughArmorCritThreshold > 0 ) {
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
         damaged.Clear();
         float threshold = (float) Settings.ThroughArmorCritThreshold;
         foreach ( var damage in damages ) {
            ArmorLocation armour = damage.Key;
            if ( armour == ArmorLocation.None || armour == ArmorLocation.Invalid ) continue;
            ChassisLocations location = MechStructureRules.GetChassisLocationFromArmorLocation( armour );
            if ( mech.IsLocationDestroyed( location ) ) continue;
            if ( mech.GetCurrentArmor( armour ) <= 0 && mech.GetCurrentStructure( location ) < mech.GetMaxStructure( location ) )
               damaged.Add( (int) armour, damage.Value );
            else if ( damage.Value > threshold )
               armoured.Add( armour, damage.Value );
            //else
            //   Info( "{0} damage ({1}) on {2} not reach threshold {3}", armour, damage.Value, mech.DisplayName, threshold );
         }
      }

      public static bool Override_ConsolidateCriticalHitInfo ( ref Dictionary<int, float> __result ) {
         if ( damaged == null ) return true;
         __result = damaged; // Use the result from SplitCriticalHitInfo
         damaged = null;
         return false;
      }

      public static void ThroughArmorCritical ( Mech __instance, WeaponHitInfo hitInfo, Weapon weapon, MeleeAttackType meleeAttackType ) { try {
         Mech mech = __instance;

         SplitCriticalHitInfo( mech, hitInfo, () => {
            float damage = weapon.parent == null ? weapon.DamagePerShot : weapon.DamagePerShotAdjusted( weapon.parent.occupiedDesignMask );
            AbstractActor abstractActor = mech.Combat.FindActorByGUID(hitInfo.attackerId);
            LineOfFireLevel lineOfFireLevel = abstractActor.VisibilityCache.VisibilityToTarget( mech ).LineOfFireLevel;
            return mech.GetAdjustedDamage( damage, weapon.Category, mech.occupiedDesignMask, lineOfFireLevel, false );
         } );
         
         if ( armoured == null || armoured.Count <= 0 ) return;

         foreach ( var keyValuePair in armoured )
            CheckThroughArmourCrit( mech, hitInfo, keyValuePair.Key, weapon );
      }                 catch ( Exception ex ) { Error( ex ); } }

      private static HBS.Logging.ILog atkLog;
      private static string attackSequence;
      private static void aLog( string message, params object[] args ) {
         if ( atkLog.IsLogEnabled )
            atkLog.Log( string.Format( attackSequence + message, args ) );
      }

      private static void PublishMessage ( Mech mech, string message, object arg, FloatieMessage.MessageNature type ) {
         mech.Combat.MessageCenter.PublishMessage( new AddSequenceToStackMessage( 
            new ShowActorInfoSequence( mech, new Text( message, new object[] { arg } ), type, true ) ) );
      }

      public static void CheckThroughArmourCrit ( Mech mech, WeaponHitInfo hitInfo, ArmorLocation armour, Weapon weapon ) {
         atkLog = AbstractActor.attackLogger;
         if ( weapon == null ) {
            atkLog.LogError( "CheckForCrit had a null weapon!" );
            return;
         }
         ChassisLocations location = MechStructureRules.GetChassisLocationFromArmorLocation( armour );
         CombatGameState Combat = mech.Combat;
         if ( atkLog.IsLogEnabled ) {
            attackSequence = string.Format( "SEQ:{0}: WEAP:{1} Loc:{2}", hitInfo.attackSequenceId, hitInfo.attackWeaponIndex, location.ToString() );
            aLog( "Base crit chance: {0:P2}", Combat.CritChance.GetBaseCritChance( mech, location, true ) );
            aLog( "Modifiers : {0}", Combat.CritChance.GetCritMultiplierDescription( mech, weapon ) );
         }
         float critChance = Combat.CritChance.GetCritChance(mech, location, weapon, true);
         float[] randomFromCache = Combat.AttackDirector.GetRandomFromCache( hitInfo, 2 );
         aLog( "Final crit chance: {0:P2}", critChance );
         aLog( "Crit roll: {0:P2}", randomFromCache[0] );
         if ( randomFromCache[ 0 ] <= critChance ) {
            float slotCount = mech.MechDef.GetChassisLocationDef( location ).InventorySlots;
            int slot = (int)(slotCount * randomFromCache[1]);
            MechComponent componentInSlot = mech.GetComponentInSlot( location, slot );
            if ( componentInSlot != null ) {
               aLog( "Critical Hit! Found {0} in slot {1}", componentInSlot.Name, slot );
               PlayCritEffects( mech, location, weapon, componentInSlot );
               AttackDirector.AttackSequence attackSequence = Combat.AttackDirector.GetAttackSequence(hitInfo.attackSequenceId);
               if ( attackSequence != null )
                  attackSequence.FlagAttackScoredCrit( componentInSlot as Weapon, componentInSlot as AmmunitionBox );
               ComponentDamageLevel componentDamageLevel = componentInSlot.DamageLevel;
               if ( componentInSlot is Weapon && componentDamageLevel == ComponentDamageLevel.Functional ) {
                  componentDamageLevel = ComponentDamageLevel.Penalized;
                  PublishMessage( mech, "{0} CRIT", componentInSlot.UIName, FloatieMessage.MessageNature.CriticalHit );
               } else if ( componentDamageLevel != ComponentDamageLevel.Destroyed ) {
                  componentDamageLevel = ComponentDamageLevel.Destroyed;
                  PublishMessage( mech, "{0} DESTROYED", componentInSlot.UIName, FloatieMessage.MessageNature.ComponentDestroyed );
               }
               componentInSlot.DamageComponent( hitInfo, componentDamageLevel, true );
               aLog( "Critical: {3} new damage state: {4}", componentInSlot.Name, componentDamageLevel );
            } else
               aLog( "Critical Hit! No component in slot {0}", slot );
         } else if ( atkLog.IsLogEnabled ) {
            aLog( "No crit" );
         }
      }

      public static void PlayCritEffects ( Mech mech, ChassisLocations location, Weapon weapon, MechComponent componentInSlot ) {
         if ( mech.GameRep != null ) {
            AmmunitionBox AmmoCrited = componentInSlot as AmmunitionBox;
            Jumpjet jumpjetCrited = componentInSlot as Jumpjet;
            HeatSinkDef heatsinkCrited = componentInSlot.componentDef as HeatSinkDef;
            if ( weapon.weaponRep != null && weapon.weaponRep.HasWeaponEffect ) {
               WwiseManager.SetSwitch<AudioSwitch_weapon_type>( weapon.weaponRep.WeaponEffect.weaponImpactType, mech.GameRep.audioObject );
            } else {
               WwiseManager.SetSwitch<AudioSwitch_weapon_type>( AudioSwitch_weapon_type.laser_medium, mech.GameRep.audioObject );
            }
            WwiseManager.SetSwitch<AudioSwitch_surface_type>( AudioSwitch_surface_type.mech_critical_hit, mech.GameRep.audioObject );
            WwiseManager.PostEvent<AudioEventList_impact>( AudioEventList_impact.impact_weapon, mech.GameRep.audioObject, null, null );
            WwiseManager.PostEvent<AudioEventList_explosion>( AudioEventList_explosion.explosion_small, mech.GameRep.audioObject, null, null );
            if ( mech.team.LocalPlayerControlsTeam )
               AudioEventManager.PlayAudioEvent( "audioeventdef_musictriggers_combat", "critical_hit_friendly ", null, null );
            else if ( !mech.team.IsFriendly( Combat.LocalPlayerTeam ) )
               AudioEventManager.PlayAudioEvent( "audioeventdef_musictriggers_combat", "critical_hit_enemy", null, null );
            if ( jumpjetCrited == null && heatsinkCrited == null && AmmoCrited == null && componentInSlot.DamageLevel > ComponentDamageLevel.Functional )
               mech.GameRep.PlayComponentCritVFX( (int) location );
            if ( AmmoCrited != null && componentInSlot.DamageLevel > ComponentDamageLevel.Functional )
               mech.GameRep.PlayVFX( (int) location, Combat.Constants.VFXNames.componentDestruction_AmmoExplosion, true, Vector3.zero, true, -1f );
         }
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