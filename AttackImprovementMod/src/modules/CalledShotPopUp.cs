using BattleTech.UI;
using BattleTech;
using System;
using System.Collections.Generic;
using System.Reflection;
using static System.Reflection.BindingFlags;

namespace Sheepy.BattleTechMod.AttackImprovementMod {
   using static Mod;
   using static HitLocation;

   public class CalledShotPopUp : BattleModModule {

      private static string CalledShotHitChanceFormat = "{0:0}%";

      public override void CombatStartsOnce () {
         Type CalledShot = typeof( CombatHUDCalledShotPopUp );
         if ( Settings.ShowLocationInfoInCalledShot )
            Patch( CalledShot, "UpdateMechDisplay", null, "ShowCalledLocationHP" );

         if ( Settings.CalledChanceFormat != null )
            CalledShotHitChanceFormat = Settings.CalledChanceFormat;

         if ( Settings.FixBossHeadCalledShotDisplay ) {
            currentHitTableProp = typeof( CombatHUDCalledShotPopUp ).GetProperty( "currentHitTable", NonPublic | Instance );
            if ( currentHitTableProp == null )
               Error( "Cannot find CombatHUDCalledShotPopUp.currentHitTable, boss head called shot display not fixed. Boss should still be immune from headshot." );
            else
               Patch( CalledShot, "UpdateMechDisplay", "FixBossHead", "CleanupBossHead" );
         }

         if ( Settings.ShowRealMechCalledShotChance || Settings.ShowRealVehicleCalledShotChance || Settings.CalledChanceFormat != null ) {
            Patch( CalledShot, "set_ShownAttackDirection", typeof( AttackDirection ), null, "RecordAttackDirection" );

            if ( Settings.ShowRealMechCalledShotChance || Settings.CalledChanceFormat != null )
               Patch( CalledShot, "GetHitPercent", new Type[]{ typeof( ArmorLocation ), typeof( ArmorLocation ) }, "OverrideHUDMechCalledShotPercent", null );

            if ( Settings.ShowRealVehicleCalledShotChance || Settings.CalledChanceFormat != null )
               Patch( CalledShot, "GetHitPercent", new Type[]{ typeof( VehicleChassisLocations ), typeof( VehicleChassisLocations ) }, "OverrideHUDVehicleCalledShotPercent", null );
         }
      }

      public override void CombatEnds () {
         title = null;
      }

      // ============ Hover Info ============

      private static TMPro.TextMeshProUGUI title;

      public static void ShowCalledLocationHP ( CombatHUDCalledShotPopUp __instance ) { try {
         if ( title == null ) {
            title = UnityEngine.GameObject.Find( "calledShot_Title" )?.GetComponent<TMPro.TextMeshProUGUI>();
            title.enableAutoSizing = false;
            if ( title == null ) return;
         }

         CombatHUDCalledShotPopUp me = __instance;
         ArmorLocation hoveredArmor = me.MechArmorDisplay.HoveredArmor;
         if ( me.locationNameText.text.StartsWith( "-" ) ) {
            title.SetText( "Called Shot" );
         } else if ( me.DisplayedActor is Mech mech ) {
            float hp = mech.GetCurrentStructure( MechStructureRules.GetChassisLocationFromArmorLocation( hoveredArmor ) );
            if ( hp <= 0 ) {
               title.SetText( "Called Shot" );
               me.locationNameText.SetText( "-choose target-", ZeroObjects );
            } else {
               float mhp = mech.GetMaxStructure( MechStructureRules.GetChassisLocationFromArmorLocation( hoveredArmor ) ),
                     armour = mech.GetCurrentArmor( hoveredArmor ), marmour = mech.GetMaxArmor( hoveredArmor );
               title.text = me.locationNameText.text;
               me.locationNameText.text = string.Format( "{0:0}/{1:0} <#FFFFFF>{2:0}/{3:0}", hp, mhp, armour, marmour );
            }
         }
      }                 catch ( Exception ex ) { Error( ex ); } }

      // ============ Game States ============

      private static float ActorCalledShotBonus { get { return HUD.SelectedActor.CalledShotBonusMultiplier; } }

      private static AttackDirection AttackDirection;

      public static void RecordAttackDirection ( AttackDirection value ) {
         AttackDirection = value;
      }

      // ============ Boss heads ============

      private static PropertyInfo currentHitTableProp;
      private static int head;

      public static void FixBossHead ( CombatHUDCalledShotPopUp __instance ) {
         if ( __instance.DisplayedActor?.CanBeHeadShot ?? true ) return;
         Dictionary<ArmorLocation, int> currentHitTable = (Dictionary<ArmorLocation, int>) currentHitTableProp.GetValue( __instance, null );
         if ( currentHitTable == null || ! currentHitTable.TryGetValue( ArmorLocation.Head, out head ) ) return;
         currentHitTable[ ArmorLocation.Head ] = 0;
      }

      public static void CleanupBossHead ( CombatHUDCalledShotPopUp __instance ) {
         if ( head <= 0 ) return;
         Dictionary<ArmorLocation, int> currentHitTable = (Dictionary<ArmorLocation, int>) currentHitTableProp.GetValue( __instance, null );
         currentHitTable[ ArmorLocation.Head ] = head;
         head = 0;
      }

      // ============ HUD Override ============

      private static Object LastHitTable;
      private static int HitTableTotalWeight;
      private static int lastCalledShotLocation;

      private static bool CacheNeedRefresh ( Object hitTable, int targetedLocation ) {
         bool result = ! Object.ReferenceEquals( hitTable, LastHitTable ) || lastCalledShotLocation != targetedLocation;
         if ( result ) {
            LastHitTable = hitTable;
            lastCalledShotLocation = targetedLocation;
         }
         return result;
      }

      [ Harmony.HarmonyPriority( Harmony.Priority.Low ) ]
      public static bool OverrideHUDMechCalledShotPercent ( ref string __result, ArmorLocation location, ArmorLocation targetedLocation ) { try {
         Dictionary<ArmorLocation, int> hitTable = ( targetedLocation == ArmorLocation.None || ! CallShotClustered || ! Settings.ShowRealMechCalledShotChance )
                                                   ? Combat.HitLocation.GetMechHitTable( AttackDirection )
                                                   : CombatConstants.GetMechClusterTable( targetedLocation, AttackDirection );
         if ( CacheNeedRefresh( hitTable, (int) targetedLocation ) )
            HitTableTotalWeight = SumWeight( hitTable, targetedLocation, FixMultiplier( targetedLocation, ActorCalledShotBonus ), scale );

         int local = TryGet( hitTable, location ) * scale;
         if ( location == targetedLocation )
            local = (int)( local * FixMultiplier( targetedLocation, ActorCalledShotBonus ) );

         __result = FineTuneAndFormat( hitTable, location, local, Settings.ShowRealMechCalledShotChance );
         return false;
      }                 catch ( Exception ex ) { return Error( ex ); } }

      [ Harmony.HarmonyPriority( Harmony.Priority.Low ) ]
      public static bool OverrideHUDVehicleCalledShotPercent ( ref string __result, VehicleChassisLocations location, VehicleChassisLocations targetedLocation ) { try {
         Dictionary<VehicleChassisLocations, int> hitTable = Combat.HitLocation.GetVehicleHitTable( AttackDirection );
         if ( CacheNeedRefresh( hitTable, (int) targetedLocation ) )
            HitTableTotalWeight = SumWeight( hitTable, targetedLocation, FixMultiplier( targetedLocation, ActorCalledShotBonus ), scale );

         int local = TryGet( hitTable, location ) * scale;
         if ( location == targetedLocation )
            local = (int)( local * FixMultiplier( targetedLocation, ActorCalledShotBonus ) );

         __result = FineTuneAndFormat( hitTable, location, local, Settings.ShowRealVehicleCalledShotChance );
         return false;
      }                 catch ( Exception ex ) { return Error( ex ); } }

      // ============ Subroutines ============

      private static string FineTuneAndFormat<T> ( Dictionary<T, int> hitTable, T location, int local, bool simulate  ) { try {
         float perc = local * 100f / HitTableTotalWeight;
         return string.Format( CalledShotHitChanceFormat, perc );
      } catch ( Exception ex ) {
         Error( ex );
         return "ERR";
      } }
   }
}