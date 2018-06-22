using BattleTech;
using BattleTech.UI;
using System;

namespace Sheepy.AttackImprovementMod {
   using static Mod;

   public class HeatAndStab {

      internal static void InitPatch () {
         if ( Settings.ShowHeatAndStab ) {
            Type actorDetails = typeof( CombatHUDActorDetailsDisplay );
            Patch( actorDetails, "RefreshInfo", "ShowHeatAndStab", null );
         }
      }

      // ============ Fixes ============

      private static CombatHUDActorDetailsDisplay Display;
      public static bool ShowHeatAndStab ( CombatHUDActorDetailsDisplay __instance ) {
         try {
            // Only override mechs. Other actors are unimportant to us.
			   if ( __instance.DisplayedActor == null || ! ( __instance.DisplayedActor is Mech ) ) {
               Display = null;
               return true;
            }

            Display = __instance;
            Mech mech = (Mech) __instance.DisplayedActor;
            int jets = mech.WorkingJumpjets;
            string line1 = mech.weightClass.ToString(), line2;
            if ( jets > 0 ) line1 += ", " + jets + " JETS";
            if ( __instance.DisplayedActor.team.IsLocalPlayer ) { // Two lines in selection panel
               line1 = "·\n" + line1;
               CombatSelectionHandler selection = HUD?.SelectionHandler;
               if ( selection != null && selection.ProjectedHeatForState != mech.CurrentHeat && false )
                  line2 = "Heat " + mech.CurrentHeat + " >> " + selection.ProjectedHeatForState;
               else
                  line2 = "Heat " + mech.CurrentHeat;
               line2 += "\n";
               if ( selection != null && selection.ProjectedStabilityForState != mech.CurrentStability && false )
                  line2 += "Stab " + (int) mech.CurrentStability + " >> " + (int) selection.ProjectedStabilityForState;
               else
                  line2 += "Stab " + (int) mech.CurrentStability;

            } else { // One line in target panel
               line2 = "Heat " + mech.CurrentHeat + ", Stab " + mech.CurrentStability;
            }
			   __instance.ActorWeightText.text = line1 + "\n" + line2;
			   __instance.JumpJetsHolder.SetActive( false );
            return false;
         } catch ( Exception ex ) {
            return Log( ex );
         }
      }
   }
}