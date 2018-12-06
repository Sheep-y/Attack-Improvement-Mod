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
            "Weapon_LRM_LRM5_2-Delta", "Weapon_LRM_LRM10_2-Delta", "Weapon_LRM_LRM15_2-Delta" } );
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
            case "Weapon_LRM_LRM5_2-Delta":
            case "Weapon_LRM_LRM10_2-Delta":
            case "Weapon_LRM_LRM15_2-Delta":
               if ( weaponsToFix.Contains( id ) ) FixDeltaLRM( __instance );
               break;
         }
      }                 catch ( Exception ex ) { Error( ex ); } }

      public static void FixedWeapon ( string id ) {
         weaponsToFix.Remove( id );
         if ( weaponsToFix.Count > 0 ) return;
         weaponDefs = null;
         weaponsToFix = null;
         Info( "All weapon stats corrected." );
      }

      public static void FixDeltaLRM ( WeaponDef def ) {
         FixedWeapon( def.Description.Id );
         PropertyInfo prop = typeof( WeaponDef ).GetProperty( "Instability" );
         if ( def.Instability != 5 || ! def.BonusValueA.Contains( "+ 2" ) || prop == null ) {
            Info( "Stat or property mismatched; " + def.Description.Id + " not corrected." );
         } else {
            prop.SetValue( def, 4, null );
            Info( def.Description.Id + " instability corrected to 4." );
         }  
      }
   }
}