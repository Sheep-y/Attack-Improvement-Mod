using BattleTech;
using System;
using System.Collections.Generic;

namespace Sheepy.BattleTechMod.AttackImprovementMod {
   using System.Reflection;
   using static Mod;

   public class GameData : BattleModModule {

      public override void GameStarts () {
         if ( ! Settings.FixWeaponStats ) return;
         weaponDefs = new Dictionary<string, WeaponDef>();
         weaponsToFix = new HashSet<string>( new string[]{ 
            "Weapon_LRM_LRM5_2-Delta", "Weapon_LRM_LRM10_2-Delta", "Weapon_LRM_LRM15_2-Delta", // Since Launch
            "Weapon_SRM_SRM6_1-Holly", "Weapon_SRM_SRM6_2-Holly", // 1.0 to 1.2.1, fixed in 1.3
            "Weapon_Laser_MediumLaserER_2-BrightBloom", "Weapon_Laser_SmallLaserER_2-BlazeFire", "Weapon_Laser_LargeLaserER_2-BlazeFire", // Patch 1.3
            "Weapon_Laser_LargeLaserPulse_2-Exostar", "Weapon_Laser_MediumLaserPulse_2-Magna400P", // Patch 1.3
            "Weapon_PPC_PPCER_0-STOCK", "Weapon_PPC_PPCER_1-MagnaFirestar", "Weapon_PPC_PPCER_2-TiegartMagnum" } ); // Patch 1.3
         Patch( typeof( WeaponDef ), "FromJSON", null, "FixWeaponStats" );
      }

      private static Dictionary<string, WeaponDef> weaponDefs;
      private static HashSet<string> weaponsToFix;

      public static void FixWeaponStats ( WeaponDef __instance ) { try {
         string id = __instance.Description?.Id;
         if ( weaponDefs == null || id == null ) return;
         weaponDefs.Remove( id );
         weaponDefs.Add( id, __instance );
         switch ( id ) {
            case "Weapon_PPC_PPC_0-STOCK":
               if ( __instance.statusEffects.Length == 1 ) {
                  PPC_STATUS = HBS.Util.JSONSerializationUtility.ToJSON( __instance.statusEffects[0] );
                  Info( "PPC debuff cloned." );
               } else
                  Warn( "PPC debuff mismatch." );
               break;

            case "Weapon_LRM_LRM5_2-Delta":
            case "Weapon_LRM_LRM10_2-Delta":
            case "Weapon_LRM_LRM15_2-Delta":
               if ( CanFixWeapon( id ) )
                  TryFixProp( id, __instance, "Instability", e => e.Instability == 5 && e.BonusValueA.Contains( "+ 2" ), 4 );
               break;

            case "Weapon_SRM_SRM6_1-Holly":
            case "Weapon_SRM_SRM6_2-Holly":
               if ( CanFixWeapon( id ) )
                  TryFixProp( id, __instance, "AccuracyModifier", e => e.AccuracyModifier < 0 && e.BonusValueB == "", 0 );
               break;

            case "Weapon_Laser_SmallLaserER_2-BlazeFire":
            case "Weapon_Laser_MediumLaserER_2-BrightBloom":
            case "Weapon_Laser_LargeLaserER_2-BlazeFire":
               if ( CanFixWeapon( id ) )
                  TryFixProp( id, __instance, "BonusValueA", e => e.CriticalChanceMultiplier == 1.3f && e.BonusValueA == "" && e.BonusValueB == "", "+ 5% Crit." );
               break;

            case "Weapon_Laser_LargeLaserPulse_2-Exostar":
            case "Weapon_Laser_MediumLaserPulse_2-Magna400P":
               if ( CanFixWeapon( id ) )
                  TryFixProp( id, __instance, "BonusValueA", e => e.AccuracyModifier == -3 && e.BonusValueA == "" && e.BonusValueB == "", "+ 1 Acc." );
               break;

            case "Weapon_PPC_PPCER_0-STOCK":
            case "Weapon_PPC_PPCER_1-MagnaFirestar":
            case "Weapon_PPC_PPCER_2-TiegartMagnum":
               if ( CanFixWeapon( id ) ) {
                  EffectData debuff = new EffectData();
                  HBS.Util.JSONSerializationUtility.FromJSON( debuff, PPC_STATUS );
                  TryFixProp( id, __instance, "statusEffects", e => e.statusEffects.IsNullOrEmpty(), new EffectData[]{ debuff } );
               }
               break;
         }
      }                 catch ( Exception ex ) { Error( ex ); } }

      public static bool CanFixWeapon ( string id ) {
         if ( ! weaponsToFix.Contains( id ) ) return false;
         weaponsToFix.Remove( id );
         if ( weaponsToFix.Count <= 0 ) {
            weaponDefs = null;
            weaponsToFix = null;
            Info( "Last weapon checked." );
         }
         return true;
      }

      public static void TryFixProp ( string id, WeaponDef def, string propName, Func<WeaponDef,bool> checker, object value ) {
         PropertyInfo prop = typeof( WeaponDef ).GetProperty( propName );
         if ( prop != null && checker.Invoke( def ) ) {
            prop.SetValue( def, value, null );
            Info( id + " " + propName + " corrected." );
         } else
            Info( id + " " + propName + " seems to be already fixed." );
      }

      private static string PPC_STATUS = "{\"durationData\":{\"duration\":1,\"ticksOnActivations\":true,\"useActivationsOfTarget\":true,\"ticksOnEndOfRound\":false,\"ticksOnMovements\":false,\"stackLimit\":0,\"clearedWhenAttacked\":false},\"targetingData\":{\"effectTriggerType\":\"OnHit\",\"triggerLimit\":0,\"extendDurationOnTrigger\":0,\"specialRules\":\"NotSet\",\"effectTargetType\":\"NotSet\",\"range\":0,\"forcePathRebuild\":false,\"forceVisRebuild\":false,\"showInTargetPreview\":true,\"showInStatusPanel\":true},\"effectType\":\"StatisticEffect\",\"Description\":{\"Id\":\"AbilityDefPPC\",\"Name\":\"SENSORS IMPAIRED\",\"Details\":\"[AMT] Difficulty to all of this unit's attacks until its next activation.\",\"Icon\":\"uixSvgIcon_status_sensorsImpaired\"},\"nature\":\"Debuff\",\"statisticData\":{\"appliesEachTick\":false,\"effectsPersistAfterDestruction\":false,\"statName\":\"AccuracyModifier\",\"operation\":\"Float_Add\",\"modValue\":\"1.0\",\"modType\":\"System.Single\",\"additionalRules\":\"NotSet\",\"targetCollection\":\"NotSet\",\"targetWeaponCategory\":\"NotSet\",\"targetWeaponType\":\"NotSet\",\"targetAmmoCategory\":\"NotSet\",\"targetWeaponSubType\":\"NotSet\"},\"tagData\":null,\"floatieData\":null,\"actorBurningData\":null,\"vfxData\":null,\"instantModData\":null,\"poorlyMaintainedEffectData\":null}";
   }
}