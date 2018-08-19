using BattleTech.UI;
using BattleTech;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sheepy.BattleTechMod.AttackImprovementMod {
   using static Mod;
   using static System.Reflection.BindingFlags;

   public class Criticals : BattleModModule {

      public override void CombatStartsOnce () {
         if ( Settings.SkipCritingDeadMech ) 
            Patch( typeof( Mech ), "CheckForCrit", NonPublic, "Skip_CheckForCrit", null );
      }

      public override void CombatStarts () {
      }

      public static bool Skip_CheckForCrit ( Mech __instance ) {
         if ( __instance.IsFlaggedForDeath || __instance.IsDead ) return false;
         return true;
      }
   }
}