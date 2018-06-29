using BattleTech;
using BattleTech.UI;
using System;

namespace Sheepy.AttackImprovementMod {
   using System.Reflection;
   using static Mod;

   public class HeatAndStab {

      internal static void InitPatch () {
         if ( Settings.ShowHeatAndStab ) {
            Patch( typeof( CombatHUDActorDetailsDisplay ), "RefreshInfo", "ShowHeatAndStab", null );
            Patch( typeof( CombatHUDActorInfo ), "RefreshPredictedHeatInfo", null, "RecordRefresh" );
            Patch( typeof( CombatHUDActorInfo ), "RefreshPredictedStabilityInfo", null, "RecordRefresh" );
            Patch( typeof( CombatHUDMechTray ), "Update", BindingFlags.NonPublic, null, "RefreshHeatAndStab" );
         }
      }

      // ============ Fixes ============

      public static bool ShowHeatAndStab ( CombatHUDActorDetailsDisplay __instance ) { try {
         // Only override mechs. Other actors are unimportant to us.
         if ( __instance.DisplayedActor == null || ! ( __instance.DisplayedActor is Mech ) )
            return true;

         Mech mech = (Mech) __instance.DisplayedActor;
         int jets = mech.WorkingJumpjets;
         string line1 = mech.weightClass.ToString(), line2;
         if ( jets > 0 ) line1 += ", " + jets + " JETS";
         int baseHeat = mech.CurrentHeat + mech.TempHeat,
               baseStab = (int) mech.CurrentStability;
         if ( __instance.DisplayedActor.team.IsLocalPlayer ) { // Two lines in selection panel
            line1 = "·\n" + line1;
            CombatSelectionHandler selection = HUD?.SelectionHandler;
            int newHeat = baseHeat, newStab = baseStab;
            if ( selection != null && selection.SelectedActor == mech ) {
               newHeat += selection.ProjectedHeatForState;
               if ( ! mech.HasMovedThisRound )
                  newHeat += mech.StatCollection.GetValue<int>( "EndMoveHeat" );
               if ( ! mech.HasAppliedHeatSinks )
                  newHeat = Math.Min( Math.Max( 0, newHeat - mech.AdjustedHeatsinkCapacity ), mech.MaxHeat );
               newStab += (int) selection.ProjectedStabilityForState;
            }
            line2  = "Heat " + baseHeat + ( baseHeat == newHeat ? "" :  " >> " + newHeat ) + "\n";
            line2 += "Stab " + (int) mech.CurrentStability + ( baseStab == newStab ? "" :  " >> " + newStab );

         } else { // One line in target panel
            line2 = "Heat " + baseHeat + ", Stab " + baseStab;
         }
         __instance.ActorWeightText.text = line1 + "\n" + line2;
         __instance.JumpJetsHolder.SetActive( false );
         return false;
      }                 catch ( Exception ex ) { return Log( ex ); } }

      private static bool needRefresh = false;
      public static void RecordRefresh () {
         needRefresh = true;
      }

      public static void RefreshHeatAndStab ( CombatHUDMechTray __instance ) {
         if ( ! needRefresh ) return;
         __instance?.ActorInfo?.DetailsDisplay?.RefreshInfo();
         needRefresh = false;
      }
   }
}