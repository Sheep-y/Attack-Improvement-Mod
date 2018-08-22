using BattleTech.UI;
using BattleTech;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sheepy.BattleTechMod.AttackImprovementMod {
   using Harmony;
   using static Mod;
   using static System.Reflection.BindingFlags;

   public class Criticals : BattleModModule {

      public override void CombatStartsOnce () {
         if ( Settings.SkipCritingDeadMech )
            Patch( typeof( Mech ), "ResolveWeaponDamage", new Type[]{ typeof( WeaponHitInfo ), typeof( Weapon ), typeof( MeleeAttackType ) }, "Skip_BeatingDeadMech", null );
      }

      public override void CombatStarts () {
      }

      [ HarmonyPriority( Priority.HigherThanNormal ) ]
      public static bool Skip_BeatingDeadMech ( Mech __instance ) {
         if ( __instance.IsFlaggedForDeath || __instance.IsDead ) return false;
         return true;
      }
   }
}